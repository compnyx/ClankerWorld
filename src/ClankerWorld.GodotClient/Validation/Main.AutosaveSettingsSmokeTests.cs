using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using ClankerWorld.GodotClient.Pairing;
using ClankerWorld.GodotClient.UI;
using Environment = System.Environment;

namespace ClankerWorld.GodotClient;

public partial class Main
{
    private async Task VerifyAutosaveSettingsAsync()
    {
        var originalRegistration = registration;
        var originalKey = deviceKey;
        var originalUrl = worldUrlInput.Text;
        var originalCi = Environment.GetEnvironmentVariable("CI");
        Environment.SetEnvironmentVariable("CI", "true");
        using var key = OwnerDeviceKey.CreateEphemeralForContinuousIntegration();
        try
        {
            // Exercise both response orders, reopening the same world's panel, and a late failure.
            foreach (var (sameWorld, oldFirst, oldFails) in new[]
                { (false, false, false), (false, true, false), (true, false, false),
                  (true, true, false), (false, false, true) })
            {
                using var host = new AutosaveSettingsSmokeHost(key.PublicKeySpkiBase64);
                registration = new(host.Authority, "smoke-device", key.PublicKeyFingerprint, host.Address);
                deviceKey = key;
                worldUrlInput.Text = host.Address;
                settingsPanel.Show();
                worldSettingsContent.Show();
                AcceptAutosaveSmokeWorld("world-A");
                var oldRead = RefreshAutosaveSettingsAsync();
                var oldReply = await host.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                if (sameWorld)
                {
                    settingsPanel.Hide();
                    settingsPanel.Show();
                }
                else AcceptAutosaveSmokeWorld("world-B");
                var currentWorld = sameWorld ? "world-A" : "world-B";
                // Old controls must not be usable even before the new read starts.
                await ApplyAutosaveSettingsAsync();
                if (host.Configuration is not null || !autosaveApplyButton.Disabled)
                    throw new InvalidOperationException("A world change or closed panel must invalidate autosave Apply.");
                var newRead = RefreshAutosaveSettingsAsync();
                var newReply = await host.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                void ReleaseOld()
                {
                    if (oldFails) oldReply.SetResult(null);
                    else oldReply.SetResult(new("world-A", false, 1, 3, DateTimeOffset.UnixEpoch, -1));
                }
                if (oldFirst)
                {
                    ReleaseOld();
                    await oldRead.WaitAsync(TimeSpan.FromSeconds(5));
                    if (autosaveSettingsLoaded || !autosaveApplyButton.Disabled)
                        throw new InvalidOperationException("A superseded read must not enable Apply while the current read is pending.");
                }
                newReply.SetResult(new(currentWorld, true, 10, 5, DateTimeOffset.UnixEpoch, -1));
                await newRead.WaitAsync(TimeSpan.FromSeconds(5));
                var currentStatus = autosaveSettingsStatus.Text;
                if (!oldFirst)
                {
                    ReleaseOld();
                    await oldRead.WaitAsync(TimeSpan.FromSeconds(5));
                }
                if (!autosaveSettingsLoaded || autosaveApplyButton.Disabled ||
                    !autosaveEnabledToggle.ButtonPressed || autosaveIntervalChoice.GetSelectedId() != 10 ||
                    autosaveRotationChoice.GetSelectedId() != 5 || autosaveSettingsStatus.Text != currentStatus)
                    throw new InvalidOperationException("An older autosave response replaced the current world's settings or status.");
                await ApplyAutosaveSettingsAsync();
                if (host.Configuration is not { Enabled: true, IntervalMinutes: 10, RotationCount: 5 })
                    throw new InvalidOperationException("Apply must sign the current displayed autosave settings.");

                AcceptAutosaveSmokeWorld("world-C");
                await ApplyAutosaveSettingsAsync();
                if (host.ConfigureCount != 1 || autosaveSettingsLoaded || !autosaveApplyButton.Disabled)
                    throw new InvalidOperationException("Previously loaded settings must not be applied after a world change.");

                var mismatchedRead = RefreshAutosaveSettingsAsync();
                var mismatch = await host.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                mismatch.SetResult(new("wrong-world", true, 1, 3, DateTimeOffset.UnixEpoch, -1));
                await mismatchedRead.WaitAsync(TimeSpan.FromSeconds(5));
                if (autosaveSettingsLoaded || !autosaveApplyButton.Disabled)
                    throw new InvalidOperationException("A response naming another world must not enable Apply.");
                if (sameWorld && oldFirst)
                {
                    var closingRead = RefreshAutosaveSettingsAsync();
                    var closingReply = await host.Reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    settingsPanel.Hide();
                    settingsPanel.Show();
                    closingReply.SetResult(new("world-C", true, 1, 3, DateTimeOffset.UnixEpoch, -1));
                    await closingRead.WaitAsync(TimeSpan.FromSeconds(5));
                    if (autosaveSettingsLoaded || !autosaveApplyButton.Disabled)
                        throw new InvalidOperationException("Closing and reopening must discard the old read even without a replacement read.");
                }
            }
        }
        finally
        {
            settingsPanel.Hide();
            observationSession.ResetAfterLoad();
            registration = originalRegistration;
            deviceKey = originalKey;
            worldUrlInput.Text = originalUrl;
            Environment.SetEnvironmentVariable("CI", originalCi);
            RefreshControlAvailability();
            statusToast.Hide();
        }
    }

    private void AcceptAutosaveSmokeWorld(string worldId)
    {
        observationSession.ResetAfterLoad();
        var snapshot = new OwnerWorldSnapshot(worldId, 1, "autosave-smoke-map", [new(0, 0, "meadow")], [], [], null, 0)
        {
            Authoring = new(true, 0, 0, 0, "map", "map", "clear", "spring", []),
        };
        var handshake = new OwnerWorldHandshake(new(1, 1),
            ["owner-observation.read.v1", "inhabitant-inspection.read.v1", "spatial-knowledge.read.v1",
             "owner-control.request.v1", "paused-authoring.request.v1"], []);
        if (!observationSession.TryAccept(new(handshake, new(snapshot, new(1, 0, []))), 0, out var failure))
            throw new InvalidOperationException("Autosave observation fixture rejected: " + failure);
        RefreshControlAvailability();
    }

    private sealed class AutosaveSettingsSmokeHost : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly HttpListener listener = new();
        private readonly string publicKey;
        public OwnerAuthorityIdentity Authority { get; } = new("smoke-server", "smoke-authority");
        public string Address { get; }
        public Channel<TaskCompletionSource<WorldAutosaveSettings?>> Reads { get; } =
            Channel.CreateUnbounded<TaskCompletionSource<WorldAutosaveSettings?>>();
        public OwnerAutosaveConfigurationAction? Configuration { get; private set; }
        public int ConfigureCount { get; private set; }

        public AutosaveSettingsSmokeHost(string publicKey)
        {
            this.publicKey = publicKey;
            var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
            reservation.Stop();
            Address = $"http://127.0.0.1:{port}/";
            listener.Prefixes.Add(Address);
            listener.Start();
            _ = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                while (listener.IsListening)
                    _ = ReplyAsync(await listener.GetContextAsync().ConfigureAwait(false));
            }
            catch (HttpListenerException) when (!listener.IsListening) { }
            catch (ObjectDisposedException) { }
        }

        private async Task ReplyAsync(HttpListenerContext context)
        {
            using var body = await JsonDocument.ParseAsync(context.Request.InputStream).ConfigureAwait(false);
            var envelope = body.RootElement;
            if (!OwnerPairingProtocol.VerifyP256Sha256P1363(publicKey,
                    envelope.GetProperty("canonicalProof").GetString()!, envelope.GetProperty("signatureBase64").GetString()!))
                throw new InvalidOperationException("Autosave smoke requests must be signed with the ephemeral device key.");
            object response;
            switch (context.Request.Url!.AbsolutePath)
            {
                case OwnerPairingEndpoints.ChallengeIssue:
                    response = new OwnerChallenge(Authority, "smoke-device", Guid.NewGuid().ToString("N"),
                        "smoke-nonce", DateTimeOffset.UtcNow.AddMinutes(1));
                    break;
                case OwnerPairingEndpoints.OwnerAutosaveStatus:
                    var reply = new TaskCompletionSource<WorldAutosaveSettings?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    await Reads.Writer.WriteAsync(reply).ConfigureAwait(false);
                    var settings = await reply.Task.ConfigureAwait(false);
                    if (settings is null)
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                        response = new { error = "Controlled stale read failure." };
                    }
                    else response = settings;
                    break;
                case OwnerPairingEndpoints.OwnerAutosaveConfigure:
                    ConfigureCount++;
                    Configuration = envelope.GetProperty("action").Deserialize<OwnerAutosaveConfigurationAction>(JsonOptions)!;
                    // Stop after inspecting the actual signed Apply, without triggering an observation refresh.
                    context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                    response = new { error = "Controlled configure refusal." };
                    break;
                default:
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    response = new { error = "No fixture for this endpoint." };
                    break;
            }
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.OutputStream, response,
                response.GetType(), JsonOptions).ConfigureAwait(false);
            context.Response.Close();
        }

        public void Dispose() => listener.Close();
    }
}
