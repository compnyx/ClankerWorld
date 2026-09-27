using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;

namespace ClankerWorld.Simulation.Tests;

public sealed class PrivateWorldMemoryRetrievalTests
{
    [Fact]
    public async Task JevOffRetrievesOnlyLivingOwnersExistingMemoriesAndKeepsThemAcrossSave()
    {
        using var seed = new PrivateWorldRuntime("memory-fallback");
        var state = seed.ExportState();
        var longSummary = "Mira knows where food grows. " + new string('x', 300);
        var memories = new[]
        {
            new SocietySocialMemory("food", "founder-scout", "founder-mira",
                longSummary, "private", 0),
            new SocietySocialMemory("recent", "founder-scout", "founder-rowan",
                "Rowan repaired the old camp roof.", "private", 0),
            new SocietySocialMemory("extra-1", "founder-scout", "founder-rowan", "A quiet evening.", "private", 0),
            new SocietySocialMemory("extra-2", "founder-scout", "founder-rowan", "A cloudy day.", "private", 0),
            new SocietySocialMemory("extra-3", "founder-scout", "founder-rowan", "A small song.", "private", 0),
            new SocietySocialMemory("extra-4", "founder-scout", "founder-rowan", "A shared joke.", "private", 0),
            new SocietySocialMemory("secret", "founder-mira", "founder-scout",
                "My private hidden supply is under a stone.", "public", 0),
            new SocietySocialMemory("forgotten", "founder-scout", "founder-mira",
                "A false abandoned promise.", "private", 0, TombstonedTick: 0),
        };
        state = state with
        {
            JevEnabled = true,
            Society = state.Society with
            {
                Society = state.Society.Society with { Memories = memories.OrderBy(item => item.Id).ToArray() },
            },
        };
        var encoded = PrivateWorldRuntimeCodec.Encode(state);
        var observations = new List<InhabitantObservation>();
        using var world = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(encoded),
            _ => new CapturingProvider(observations));
        world.Pause();
        Assert.True(world.SetJevEnabled(false));
        world.Resume();
        Assert.False(world.JevEnabled);
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        var scout = observations.Single(item => item.InhabitantId == "founder-scout");
        var mira = observations.Single(item => item.InhabitantId == "founder-mira");
        Assert.Contains(scout.RetrievedMemories!, item => item.Id == "food");
        Assert.Equal(4, scout.RetrievedMemories!.Count);
        Assert.Equal("food", scout.RetrievedMemories[0].Id);
        Assert.Equal(160, scout.RetrievedMemories[0].Summary.Length);
        Assert.DoesNotContain(scout.RetrievedMemories!, item => item.Id is "secret" or "forgotten");
        Assert.Equal("secret", Assert.Single(mira.RetrievedMemories!).Id);
        Assert.All(observations, observation => Assert.All(observation.RetrievedMemories!,
            memory => Assert.Equal(observation.InhabitantId, memory.OwnerId)));
        Assert.Equal(memories.OrderBy(item => item.Id), world.Society.Memories);
        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        Assert.False(saved.JevEnabled);
        Assert.Equal(memories.OrderBy(item => item.Id), saved.Society.Society.Memories);

        var repeatObservations = new List<InhabitantObservation>();
        using var repeat = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(encoded),
            _ => new CapturingProvider(repeatObservations));
        repeat.Pause();
        repeat.SetJevEnabled(false);
        repeat.Resume();
        Assert.True((await repeat.AdvanceOneTickAsync()).Advanced);
        var repeatedScout = repeatObservations.Single(item => item.InhabitantId == "founder-scout");
        Assert.Equal(scout.RetrievedMemories, repeatedScout.RetrievedMemories);
        Assert.Equal(scout.ObservationDigest, repeatedScout.ObservationDigest);
    }

    [Fact]
    public async Task ProviderFailureDoesNotCreateOrBroadcastMemories()
    {
        using var seed = new PrivateWorldRuntime("memory-failure");
        var state = seed.ExportState();
        var memory = new SocietySocialMemory("secret", "founder-scout", "founder-mira",
            "Secret map marker that must remain private.", "private", 0);
        state = state with
        {
            JevEnabled = false,
            Society = state.Society with
            {
                Society = state.Society.Society with { Memories = [memory] },
            },
        };
        var observed = new List<InhabitantObservation>();
        using var world = PrivateWorldRuntime.Restore(state, id => id == "founder-scout"
            ? new FailingHostedProvider(observed)
            : new CapturingProvider(observed));
        Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        for (var attempt = 0; attempt < 20 &&
             !world.ExportState().Events.Any(item => item.Kind == "hosted_decision_completed"); attempt++)
        {
            await Task.Delay(10);
            Assert.True((await world.AdvanceOneTickNonBlockingAsync()).Advanced);
        }
        Assert.Contains(world.ExportState().Events, item => item.Kind == "hosted_decision_completed");
        Assert.Contains(observed, item => item.InhabitantId == "founder-scout" &&
            item.RetrievedMemories!.Any(retrieved => retrieved.Id == memory.Id));
        Assert.DoesNotContain(observed.Where(item => item.InhabitantId != "founder-scout"),
            item => item.RetrievedMemories!.Any(retrieved => retrieved.Id == memory.Id));
        Assert.Equal(memory, Assert.Single(world.Society.Memories));
        Assert.DoesNotContain(world.ExportState().Events,
            item => item.Detail.Contains("Secret map marker", StringComparison.Ordinal));
    }

    private sealed class CapturingProvider(List<InhabitantObservation> observed) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            lock (observed) observed.Add(request.Observation);
            var selected = request.Observation.Candidates[0].Id;
            return ValueTask.FromResult(new CognitionDecisionResponse(
                request.RequestId, request.Observation.InhabitantId, Kind, ProviderEpoch,
                request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1,
                new Dictionary<string, double> { [selected] = 1 }));
        }
    }

    private sealed class FailingHostedProvider(List<InhabitantObservation> observed) : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            lock (observed) observed.Add(request.Observation);
            throw new InvalidOperationException("provider unavailable");
        }
    }
}
