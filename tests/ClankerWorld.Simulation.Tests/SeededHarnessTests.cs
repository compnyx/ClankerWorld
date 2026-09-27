using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Kernel;

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
        var map = TerrainMap(3, 2, _ => TerrainKind.Meadow);
        var origin = new GridPoint(0, 0);
        var destination = new GridPoint(2, 1);
        var expected = new[] { origin, new GridPoint(1, 1), destination };

        for (var run = 0; run < 10; run++)
        {
            Assert.Equal(expected, DeterministicRouteFinder.Find(map, origin, destination));
        }
    }

    [Fact]
    public void DiagonalRoutesHaveAStableCostAndCannotClipBlockedCorners()
    {
        var open = TerrainMap(4, 4, _ => TerrainKind.Meadow);
        var origin = new GridPoint(0, 0);
        var diagonal = new GridPoint(1, 1);
        Assert.True(open.CanFootStep(origin, diagonal));
        Assert.Equal(141, open.FootStepCost(origin, diagonal));
        Assert.Equal(new[] { origin, diagonal, new GridPoint(2, 2) },
            DeterministicRouteFinder.Find(open, origin, new GridPoint(2, 2)));

        var wall = open with { Tiles = open.Tiles.Select(tile => tile.Position == new GridPoint(1, 0)
            ? tile with { Terrain = TerrainKind.Peak } : tile).ToArray() };
        Assert.False(wall.CanFootStep(origin, diagonal));
        Assert.DoesNotContain(diagonal, wall.FootNeighbors(origin));
        Assert.Equal(new[] { origin, new GridPoint(0, 1), diagonal },
            DeterministicRouteFinder.Find(wall, origin, diagonal));
    }

    [Fact]
    public void WrappedDiagonalRequiresBothShouldersAtTheSeam()
    {
        var origin = new GridPoint(0, 1);
        var destination = new GridPoint(4, 2);
        var wrapped = TerrainMap(5, 4, _ => TerrainKind.Meadow) with { WrapsEastWest = true };
        Assert.True(wrapped.CanFootStep(origin, destination));
        Assert.Contains(destination, wrapped.FootNeighbors(origin));
        Assert.Equal(new[] { origin, destination }, DeterministicRouteFinder.Find(wrapped, origin, destination));

        var blocked = wrapped with { Tiles = wrapped.Tiles.Select(tile => tile.Position == new GridPoint(4, 1)
            ? tile with { Terrain = TerrainKind.Peak } : tile).ToArray() };
        Assert.False(blocked.CanFootStep(origin, destination));
        Assert.DoesNotContain(destination, blocked.FootNeighbors(origin));
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
            point.Y == 1 ? TerrainKind.Meadow : TerrainKind.Ocean);
        var mountainMap = TerrainMap(5, 3, point => point == mountain
            ? TerrainKind.Mountain : TerrainKind.Meadow);

        Assert.True(map.IsPassable(river));
        Assert.False(map.IsBuildable(river));
        Assert.Equal(200, map.FootTravelCost(river));
        Assert.True(mountainMap.IsPassable(mountain));
        Assert.False(mountainMap.IsBuildable(mountain));
        Assert.Equal(200, mountainMap.FootTravelCost(mountain));
        Assert.Contains(river, DeterministicRouteFinder.Find(map, new GridPoint(0, 1), new GridPoint(4, 1)));

        var vertical = TerrainMap(3, 5, point => point == new GridPoint(1, 2)
            ? TerrainKind.River : point.X == 1 ? TerrainKind.Meadow : TerrainKind.Ocean);
        Assert.Contains(new GridPoint(1, 2), DeterministicRouteFinder.Find(vertical,
            new GridPoint(1, 0), new GridPoint(1, 4)));
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

    [Fact]
    public void WrappedRoutesAndMovementCrossEitherSeamOnlyWhenTheWorldWraps()
    {
        var bounded = TerrainMap(5, 3, _ => TerrainKind.Meadow);
        var wrapped = bounded with { WrapsEastWest = true };
        var west = new GridPoint(0, 1);
        var east = new GridPoint(4, 1);

        Assert.Equal(new[] { west, east }, DeterministicRouteFinder.Find(wrapped, west, east));
        Assert.Equal(new[] { east, west }, DeterministicRouteFinder.Find(wrapped, east, west));
        Assert.Equal(new[] { new GridPoint(0, 0), new GridPoint(4, 0) },
            DeterministicRouteFinder.Find(wrapped, new GridPoint(0, 0), new GridPoint(4, 0)));
        Assert.Equal(5, DeterministicRouteFinder.Find(bounded, west, east).Count);

        var blockedSeam = wrapped with { Tiles = wrapped.Tiles.Select(tile => tile.Position == east
            ? tile with { Terrain = TerrainKind.Peak } : tile).ToArray() };
        Assert.Equal(new[] { west, new GridPoint(1, 1), new GridPoint(2, 1), new GridPoint(3, 1) },
            DeterministicRouteFinder.Find(blockedSeam, west, new GridPoint(3, 1)));

        var across = DeterministicMovementResolver.Resolve(wrapped,
            [new MovementActor("walker", west, 0)], [new MovementIntent("walker", east)]);
        var blocked = DeterministicMovementResolver.Resolve(bounded,
            [new MovementActor("walker", west, 0)], [new MovementIntent("walker", east)]);
        Assert.Equal(east, across.GetActor("walker").Position);
        Assert.Equal(west, blocked.GetActor("walker").Position);
        Assert.Empty(blocked.Events);
    }

    [Fact]
    public void WrappedSeamRiverIsWalkableOnlyWhenItsOppositeBanksAreDry()
    {
        var seamRiver = new GridPoint(0, 1);
        var map = TerrainMap(5, 3, point => point == seamRiver ? TerrainKind.River :
            point.Y != 1 && point.X is 0 or 1 ? TerrainKind.Ocean : TerrainKind.Meadow)
            with { WrapsEastWest = true };
        Assert.True(map.IsPassable(seamRiver));
        Assert.Equal(200, map.FootTravelCost(seamRiver));
        Assert.Equal(new[] { new GridPoint(4, 1), seamRiver, new GridPoint(1, 1) },
            DeterministicRouteFinder.Find(map, new GridPoint(4, 1), new GridPoint(1, 1)));

        var wide = map with { Tiles = map.Tiles.Select(tile => tile.Position == new GridPoint(1, 1)
            ? tile with { Terrain = TerrainKind.River } : tile).ToArray() };
        Assert.False(wide.IsPassable(seamRiver));
        Assert.False(wide.IsPassable(new GridPoint(1, 1)));
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
