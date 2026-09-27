using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Tests;

public sealed class SeededHarnessTests
{
    public static IEnumerable<object[]> SeedCorpus =>
    [
        ["camp-alpha", "249b2930ffe84b64271803eae490bc28d25b7e00f3969f6ffa064727c50e299c"],
        ["camp-beta", "bdc5341c4244ed1dfa2ec5dd4c3a359a8b0ae3e168b14934a34ec2701d048d37"],
        ["camp-gamma", "aa516f4770adda5d1eeded2764ebe8fced8c643cb86c0d05ec0c3276faa977d6"],
    ];

    [Theory]
    [MemberData(nameof(SeedCorpus))]
    public void FixedSeedCorpusProducesValidCanonicalMapManifest(string seed, string expectedManifestDigest)
    {
        var first = SeededMapGenerator.Generate(seed);
        var second = SeededMapGenerator.Generate(seed);

        Assert.True(MapAcceptance.Validate(first).IsValid);
        Assert.Equal(expectedManifestDigest, first.ManifestDigest);
        Assert.Equal(first.ManifestDigest, second.ManifestDigest);
        Assert.True(MapManifestCodec.Encode(first).SequenceEqual(MapManifestCodec.Encode(second)));
    }

    [Fact]
    public void EqualCostRouteUsesTheDeclaredStableTieBreakOrder()
    {
        var map = SeededMapGenerator.Generate("camp-alpha");
        var origin = new GridPoint(0, 0);
        var destination = new GridPoint(2, 1);
        var expected = new[]
        {
            new GridPoint(0, 0),
            new GridPoint(1, 0),
            new GridPoint(2, 0),
            new GridPoint(2, 1),
        };

        for (var run = 0; run < 10; run++)
        {
            Assert.Equal(expected, DeterministicRouteFinder.Find(map, origin, destination));
        }
    }

    [Fact]
    public void TerrainIndexDoesNotKeepOldPassabilityAfterMapTilesChange()
    {
        var original = SeededMapGenerator.Generate("camp-alpha");
        var site = new GridPoint(2, 2);
        Assert.True(original.IsPassable(site));
        Assert.True(original.IsBuildable(site));

        var revised = original with
        {
            Tiles = original.Tiles.Select(tile => tile.Position == site
                ? tile with { Terrain = TerrainKind.Mountain } : tile).ToArray(),
        };

        Assert.True(revised.IsPassable(site));
        Assert.False(revised.IsBuildable(site));
        Assert.Equal(200, revised.FootTravelCost(site));
        Assert.True(original.IsPassable(site));
    }

    [Fact]
    public void NarrowRiverCanBeCrossedOnFootButNeitherRiverNorMountainCanBeBuiltOn()
    {
        var river = new GridPoint(2, 1);
        var mountain = new GridPoint(1, 0);
        var map = TerrainMap(5, 3, point => point == river ? TerrainKind.River :
            point == mountain ? TerrainKind.Mountain : TerrainKind.Meadow);

        Assert.True(map.IsPassable(river));
        Assert.False(map.IsBuildable(river));
        Assert.Equal(200, map.FootTravelCost(river));
        Assert.True(map.IsPassable(mountain));
        Assert.False(map.IsBuildable(mountain));
        Assert.Equal(200, map.FootTravelCost(mountain));
        Assert.Contains(river, DeterministicRouteFinder.Find(map, new GridPoint(0, 1), new GridPoint(4, 1)));
        Assert.Contains(river, DeterministicRouteFinder.Find(map, new GridPoint(2, 0), new GridPoint(2, 2)));
    }

    [Fact]
    public void TwoTileWideRiverRemainsImpassable()
    {
        var map = TerrainMap(6, 3, point => point.X is 2 or 3 ? TerrainKind.River : TerrainKind.Meadow);

        Assert.All(map.Tiles.Where(tile => tile.Terrain == TerrainKind.River),
            tile => Assert.False(map.IsPassable(tile.Position)));
        Assert.Throws<InvalidOperationException>(() =>
            DeterministicRouteFinder.Find(map, new GridPoint(1, 1), new GridPoint(4, 1)));
    }

    private static SeededMap TerrainMap(int width, int height, Func<GridPoint, TerrainKind> terrain) =>
        new(width, height, 0,
            [.. from y in Enumerable.Range(0, height)
                from x in Enumerable.Range(0, width)
                let point = new GridPoint(x, y)
                select new TerrainTile(point, terrain(point))],
            [], [], string.Empty);

    [Fact]
    public void ScriptedActorMovesHarvestsConsumesAndSleepsInOrderedTicks()
    {
        var genesis = ScriptedHarness.CreateGenesis("camp-alpha");
        var final = ScriptedHarness.RunEntireSequence("camp-alpha");

        Assert.Equal(final.Map.GetObject("bedroll").Position, final.Actor.Position);
        Assert.Equal(ResourceState.Depleted, final.GetResource("berry-patch").State);
        Assert.Equal(0, final.Actor.FoodItems);
        Assert.True(final.Actor.HungerBasisPoints > genesis.Actor.HungerBasisPoints);
        Assert.True(final.Actor.EnergyBasisPoints > genesis.Actor.EnergyBasisPoints);
        Assert.Equal(
            Enumerable.Range(1, final.Events.Count).Select(number => (long)number),
            final.Events.Select(worldEvent => worldEvent.EventId));
        Assert.Equal(
            Enumerable.Range(1, final.Events.Count).Select(number => (long)number),
            final.Events.Select(worldEvent => worldEvent.WorldTick));
        Assert.Contains(final.Events, worldEvent => worldEvent.Detail == "harvest:berry-patch");
        Assert.Contains(final.Events, worldEvent => worldEvent.Detail == "consume:actor-scout");
        Assert.Equal("sleep:actor-scout", final.Events[^1].Detail);
    }

    [Fact]
    public void SameSeedAndScriptProduceIdenticalCanonicalStateAndEventDigests()
    {
        var first = ScriptedHarness.RunEntireSequence("camp-beta");
        var second = ScriptedHarness.RunEntireSequence("camp-beta");

        Assert.Equal(HarnessPersistence.StateDigest(first), HarnessPersistence.StateDigest(second));
        Assert.Equal(HarnessPersistence.EventDigest(first), HarnessPersistence.EventDigest(second));
    }

    [Fact]
    public void SaveReloadAndPhysicalReplayProduceTheSameFinalDigestsAsTheCleanRun()
    {
        var clean = ScriptedHarness.RunEntireSequence("camp-gamma");
        var beforeSave = ScriptedHarness.RunToFoodConsumed(ScriptedHarness.CreateGenesis("camp-gamma"));
        var save = HarnessPersistence.Save(beforeSave);
        var loaded = HarnessPersistence.Load(save);
        var resumed = ScriptedHarness.FinishAfterFood(loaded);
        var persistedFinal = HarnessPersistence.Save(resumed);

        Assert.Equal(HarnessPersistence.StateDigest(clean), HarnessPersistence.StateDigest(resumed));
        Assert.Equal(HarnessPersistence.EventDigest(clean), HarnessPersistence.EventDigest(resumed));
        Assert.True(
            persistedFinal.SnapshotBytes.SequenceEqual(HarnessPersistence.Save(resumed).SnapshotBytes));
        Assert.True(
            persistedFinal.EventLogBytes.SequenceEqual(HarnessPersistence.Save(resumed).EventLogBytes));
    }
}
