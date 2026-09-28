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
        var belief = new SocietyAgentBelief(
            "belief-hidden-store", "founder-scout", "Mira told me a hidden store is beneath the old oak.",
            SocietyBeliefProvenance.Hearsay, 4_200, 0, SourceAgentId: "founder-mira");
        state = state with
        {
            JevEnabled = true,
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Memories = memories.OrderBy(item => item.Id).ToArray(),
                    Beliefs = [belief],
                },
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
        var retrievedBelief = Assert.Single(scout.RetrievedMemories,
            item => item.Kind == "belief" && item.Id == belief.Id);
        Assert.Equal("hearsay", retrievedBelief.Provenance);
        Assert.Equal(4_200, retrievedBelief.ConfidenceBasisPoints);
        Assert.Equal("founder-mira", retrievedBelief.SourceAgentId);
        Assert.DoesNotContain(scout.RetrievedMemories!, item => item.Id is "secret" or "forgotten");
        Assert.Equal("secret", Assert.Single(mira.RetrievedMemories!).Id);
        Assert.All(observations, observation => Assert.All(observation.RetrievedMemories!,
            memory => Assert.Equal(observation.InhabitantId, memory.OwnerId)));
        Assert.Equal(memories.OrderBy(item => item.Id), world.Society.Memories);
        Assert.Equal(belief, Assert.Single(world.Society.Beliefs!));
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
    public async Task JevCompactsOnlyOwnerSourcesAndTheNextPersonalDecisionKeepsBeliefEvidence()
    {
        using var seed = new PrivateWorldRuntime("memory-compaction");
        var state = seed.ExportState();
        var memories = new[]
        {
            new SocietySocialMemory("experience-1", "founder-scout", "founder-mira", "Mira helped me fix the roof.", "private", 0),
            new SocietySocialMemory("experience-2", "founder-scout", "founder-rowan", "I learned to repair the garden fence.", "private", 0),
            new SocietySocialMemory("experience-3", "founder-scout", "founder-ilya", "Rowan and I promised to share the harvest.", "private", 0),
            new SocietySocialMemory("other-agent-secret", "founder-mira", "founder-scout", "My private cache is under the stone.", "private", 0),
        };
        var belief = new SocietyAgentBelief("belief-cache", "founder-scout",
            "Mira said a hidden cache is beneath the old oak.", SocietyBeliefProvenance.Hearsay,
            4_200, 0, SourceAgentId: "founder-mira");
        state = state with
        {
            JevEnabled = true,
            Society = state.Society with
            {
                Society = state.Society.Society with
                {
                    Memories = memories.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
                    Beliefs = [belief],
                },
            },
        };

        var provider = new JevThenPersonalProvider();
        using var world = PrivateWorldRuntime.Restore(state, id => id == "founder-scout"
            ? provider
            : new DeterministicDecisionProvider());
        var beforeSocietyEvents = world.Society.Events;
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var compaction = Assert.Single(world.Society.MemoryCompactions!, item => item.OwnerId == "founder-scout");
        Assert.Equal(4, compaction.Sources.Count);
        Assert.Contains(compaction.Sources, item => item.Kind == SocietyMemorySourceKind.Belief && item.SourceId == belief.Id);
        Assert.DoesNotContain(compaction.Sources, item => item.SourceId == "other-agent-secret");
        Assert.Equal(belief, Assert.Single(world.Society.Beliefs!));
        Assert.Equal(beforeSocietyEvents, world.Society.Events.Take(beforeSocietyEvents.Count).ToArray());
        Assert.DoesNotContain(world.ExportState().Events, item => item.Detail.Contains("hidden cache", StringComparison.Ordinal));

        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using (var restored = PrivateWorldRuntime.Restore(saved))
            Assert.Equal(compaction.Sources, Assert.Single(restored.Society.MemoryCompactions!,
                item => item.OwnerId == "founder-scout").Sources);

        world.Pause();
        provider.UseJev = false;
        Assert.True(world.SetJevEnabled(false));
        world.Resume();
        for (var tick = 0; tick < 40 && provider.PersonalObservation is null; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var personal = Assert.IsType<InhabitantObservation>(provider.PersonalObservation);
        Assert.Null(personal.MemoryCompactionCandidates);
        var recalledBelief = Assert.Single(personal.RetrievedMemories!, item => item.Id == belief.Id);
        Assert.Equal("hearsay", recalledBelief.Provenance);
        Assert.Equal(4_200, recalledBelief.ConfidenceBasisPoints);
        Assert.Equal("founder-mira", recalledBelief.SourceAgentId);
        Assert.Equal("founder-scout", recalledBelief.OwnerId);
        Assert.DoesNotContain(personal.RetrievedMemories!, item => item.Id == "other-agent-secret");
        Assert.Equal(beforeSocietyEvents, world.Society.Events.Take(beforeSocietyEvents.Count).ToArray());
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

    private sealed class JevThenPersonalProvider : IDecisionProvider
    {
        public bool UseJev { get; set; } = true;
        public InhabitantObservation? PersonalObservation { get; private set; }
        public DecisionProviderKind Kind => UseJev ? DecisionProviderKind.Jev : DecisionProviderKind.LargeLanguageModel;
        public long ProviderEpoch => 1;

        public ValueTask<CognitionDecisionResponse> DecideAsync(
            CognitionDecisionRequest request, CancellationToken cancellationToken = default)
        {
            var selected = request.Observation.Candidates[0].Id;
            var probabilities = request.Observation.Candidates.ToDictionary(item => item.Id,
                item => item.Id == selected ? 1d : 0d, StringComparer.Ordinal);
            if (UseJev)
            {
                Assert.InRange(request.Observation.MemoryCompactionCandidates?.Count ?? 0, 4, 12);
                Assert.All(request.Observation.MemoryCompactionCandidates!, item =>
                    Assert.Equal(request.Observation.InhabitantId, item.OwnerId));
                var scores = request.Observation.MemoryCompactionCandidates!.Select(item =>
                    new CognitionMemoryCompactionScore(item.Id, item.OwnerId, item.Kind, item.SourceTick,
                        item.Kind == "belief" ? 10_000 : 2_000, item.Kind == "belief" ? 9_000 : 6_000)).ToArray();
                return ValueTask.FromResult(new CognitionDecisionResponse(
                    request.RequestId, request.Observation.InhabitantId, DecisionProviderKind.Jev, ProviderEpoch,
                    request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                    request.Observation.ObservationDigest, selected, 1, probabilities,
                    MemoryCompactionScores: scores));
            }

            PersonalObservation = request.Observation;
            return ValueTask.FromResult(new CognitionDecisionResponse(
                request.RequestId, request.Observation.InhabitantId, DecisionProviderKind.LargeLanguageModel,
                ProviderEpoch, request.Observation.RunEpoch, request.Observation.DecisionGeneration,
                request.Observation.ObservationDigest, selected, 1, probabilities));
        }
    }
}
