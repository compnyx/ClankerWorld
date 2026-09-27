using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Playtest;

/// <summary>
/// Local, non-Jev retrieval from authoritative social memories. No new memories
/// are inferred, and neither public visibility nor kinship grants another actor access.
/// </summary>
internal static class PrivateWorldMemoryRetrieval
{
    private const int MaximumScanned = 256;
    private const int MaximumReturned = 4;
    private const int MaximumSummaryLength = 160;

    internal static IReadOnlyList<CognitionMemoryExcerpt> Retrieve(
        IEnumerable<SocietySocialMemory> source,
        string ownerId,
        long worldTick,
        IReadOnlyList<CognitionCandidate> candidates)
    {
        var contextTerms = Terms(string.Join(' ', candidates.Select(candidate =>
            $"{candidate.Id} {candidate.Description} {candidate.DestinationId}")));
        return source
            .Where(memory => string.Equals(memory.OwnerId, ownerId, StringComparison.Ordinal) &&
                memory.TombstonedTick is null && memory.SourceTick >= 0 &&
                memory.SourceTick <= worldTick && memory.Id is { Length: > 0 and <= 128 } &&
                memory.SubjectId is { Length: > 0 and <= 128 } &&
                !string.IsNullOrWhiteSpace(memory.Summary))
            .OrderByDescending(memory => memory.SourceTick)
            .ThenBy(memory => memory.Id, StringComparer.Ordinal)
            .Take(MaximumScanned)
            .Select(memory =>
            {
                var summary = memory.Summary.Trim();
                if (summary.Length > MaximumSummaryLength)
                    summary = summary[..MaximumSummaryLength];
                return new
                {
                    Memory = memory,
                    Summary = summary,
                    Score = Terms(summary).Count(contextTerms.Contains),
                };
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.SourceTick)
            .ThenBy(item => item.Memory.Id, StringComparer.Ordinal)
            .Take(MaximumReturned)
            .Select(item => new CognitionMemoryExcerpt(
                item.Memory.Id,
                item.Memory.OwnerId,
                item.Memory.SubjectId,
                item.Summary,
                item.Memory.SourceTick))
            .ToArray();
    }

    private static HashSet<string> Terms(string text)
    {
        var words = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var part in term.Split([':', '_', '-', '.', ',', ';', '(', ')', '/', '\\'],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length >= 3) words.Add(part.ToLowerInvariant());
            }
        }
        return words;
    }
}
