using System.Text.Json;

namespace ClankerWorld.Viewer.Control;

public sealed record ProviderUsageRow(string Provider, string Model, string Role,
    long Attempts, long Completed, long Failed, long Abandoned,
    long InputTokens, long OutputTokens);

public sealed record ProviderUsageStatus(long Attempts, long Completed, long Failed,
    long Abandoned, long InputTokens, long OutputTokens, long? AttemptLimit,
    bool LimitReached, IReadOnlyList<ProviderUsageRow> Rows);

public sealed record ProviderUsageLimitAction(long? AttemptLimit, long AdditionalCalls = 0);

public sealed class ProviderUsageLimitReachedException : InvalidOperationException
{
    public ProviderUsageLimitReachedException() : base("The optional paid-call limit was reached. Owner consent is required before another paid call.") { }
}

/// <summary>
/// Installation-lifetime, provider-bill-independent accounting. Each hosted
/// attempt is durably reserved before the HTTP call; retries and abandoned
/// calls therefore consume separate allowances. Token totals are reported
/// only when a provider actually returns them.
/// </summary>
public sealed class ProviderUsageStore
{
    private const int SchemaVersion = 1;
    private const int MaximumRows = 65; // 64 named combinations plus one overflow row
    private readonly object gate = new();
    private readonly string path;
    private State state;
    private bool limitNotified;

    private sealed record Pending(string Id, string Provider, string Model, string Role);
    private sealed record State(int SchemaVersion, long? AttemptLimit,
        List<ProviderUsageRow> Rows, List<Pending> Pending);

    public event Action? LimitReached;

    public ProviderUsageStore(string path)
    {
        this.path = path ?? throw new ArgumentNullException(nameof(path));
        if (File.Exists(path))
        {
            state = JsonSerializer.Deserialize<State>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Provider usage state is empty.");
            if (state.SchemaVersion != SchemaVersion || state.Rows is null || state.Pending is null ||
                state.AttemptLimit < 0 || state.Rows.Count > MaximumRows ||
                state.Rows.Any(row => row.Attempts < 0 || row.Completed < 0 || row.Failed < 0 ||
                    row.Abandoned < 0 || row.InputTokens < 0 || row.OutputTokens < 0))
                throw new InvalidDataException("Provider usage state is invalid.");
            // A process exit can abandon a charged call, but must not make
            // that reserved call disappear from the lifetime meter.
            foreach (var pending in state.Pending)
                AddOutcome(state, pending, "abandoned", 0, 0);
            state.Pending.Clear();
            if (state.Rows.Sum(row => row.Attempts) > 0) Save();
        }
        else state = new State(SchemaVersion, null, [], []);
    }

    public ProviderUsageStatus Capture()
    {
        lock (gate)
        {
            var rows = state.Rows.OrderBy(row => row.Provider, StringComparer.Ordinal)
                .ThenBy(row => row.Model, StringComparer.Ordinal).ThenBy(row => row.Role, StringComparer.Ordinal).ToArray();
            return new ProviderUsageStatus(rows.Sum(row => row.Attempts), rows.Sum(row => row.Completed),
                rows.Sum(row => row.Failed), rows.Sum(row => row.Abandoned),
                rows.Sum(row => row.InputTokens), rows.Sum(row => row.OutputTokens),
                state.AttemptLimit, IsBlocked(), rows);
        }
    }

    public ProviderUsageStatus Configure(ProviderUsageLimitAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.AttemptLimit is < 1 or > 1_000_000 || action.AdditionalCalls is < 0 or > 1_000_000 ||
            action.AdditionalCalls > 0 && action.AttemptLimit is not null)
            throw new ArgumentOutOfRangeException(nameof(action), "Use a positive call cap or grant, not both.");
        bool notify;
        ProviderUsageStatus result;
        lock (gate)
        {
            var before = IsBlocked();
            State next;
            if (action.AdditionalCalls > 0)
            {
                if (state.AttemptLimit is null)
                    throw new InvalidOperationException("Enable a paid-call limit before granting more calls.");
                next = state with
                {
                    AttemptLimit = checked(Math.Max(state.AttemptLimit.Value,
                    state.Rows.Sum(row => row.Attempts)) + action.AdditionalCalls)
                };
            }
            else next = state with { AttemptLimit = action.AttemptLimit };
            Save(next);
            state = next;
            if (!IsBlocked()) limitNotified = false;
            notify = !before && IsBlocked();
            if (notify) limitNotified = true;
            result = Capture();
        }
        if (notify) LimitReached?.Invoke();
        return result;
    }

    public string Begin(string provider, string model, string role)
    {
        bool notify = false;
        string? ticket = null;
        lock (gate)
        {
            if (IsBlocked())
            {
                notify = !limitNotified;
                limitNotified = true;
            }
            else
            {
                var pending = new Pending(Guid.NewGuid().ToString("N"), Clean(provider), Clean(model), Clean(role));
                // Bound the cardinality, preserving exact overall totals.
                if (state.Rows.Count >= MaximumRows - 1 && !state.Rows.Any(row => Same(row, pending)))
                    pending = pending with { Provider = "other", Model = "other", Role = "other" };
                var next = Copy(state);
                AddOutcome(next, pending, "started", 0, 0);
                next.Pending.Add(pending);
                Save(next);
                state = next;
                ticket = pending.Id;
            }
        }
        if (notify) LimitReached?.Invoke();
        return ticket ?? throw new ProviderUsageLimitReachedException();
    }

    public void Finish(string ticket, string outcome, int inputTokens = 0, int outputTokens = 0)
    {
        if (outcome is not ("completed" or "failed" or "abandoned") || inputTokens < 0 || outputTokens < 0)
            throw new ArgumentException("Invalid paid-call outcome or token count.", nameof(outcome));
        bool notify;
        lock (gate)
        {
            var pending = state.Pending.FirstOrDefault(item => item.Id == ticket);
            if (pending is null) return;
            var next = Copy(state);
            next.Pending.Remove(pending);
            AddOutcome(next, pending, outcome, inputTokens, outputTokens);
            Save(next);
            state = next;
            notify = IsBlocked() && !limitNotified;
            if (notify) limitNotified = true;
        }
        if (notify) LimitReached?.Invoke();
    }

    private bool IsBlocked() => state.AttemptLimit is { } cap &&
        state.Rows.Sum(row => row.Attempts) >= cap;

    private static State Copy(State source) => source with
    {
        Rows = [.. source.Rows],
        Pending = [.. source.Pending],
    };

    private static void AddOutcome(State target, Pending pending, string outcome, int input, int output)
    {
        var index = target.Rows.FindIndex(row => Same(row, pending));
        var row = index < 0 ? new ProviderUsageRow(pending.Provider, pending.Model, pending.Role,
            0, 0, 0, 0, 0, 0) : target.Rows[index];
        row = row with
        {
            Attempts = row.Attempts + (outcome == "started" ? 1 : 0),
            Completed = row.Completed + (outcome == "completed" ? 1 : 0),
            Failed = row.Failed + (outcome == "failed" ? 1 : 0),
            Abandoned = row.Abandoned + (outcome == "abandoned" ? 1 : 0),
            InputTokens = row.InputTokens + input,
            OutputTokens = row.OutputTokens + output,
        };
        if (index < 0) target.Rows.Add(row); else target.Rows[index] = row;
    }

    private static bool Same(ProviderUsageRow row, Pending pending) => row.Provider == pending.Provider &&
        row.Model == pending.Model && row.Role == pending.Role;

    private static string Clean(string value) => value is { Length: > 0 and <= 64 } &&
        !value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("secret", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("api-key", StringComparison.OrdinalIgnoreCase) &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':' or '/')
        ? value : "other";

    private void Save(State? candidate = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(candidate ?? state));
        File.Move(temp, path, true);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
