using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.Society;
using ClankerWorld.Simulation.World;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class GeographyGeneratorTests
{
    [Theory]
    [InlineData(WorldSizePreset.Small)]
    [InlineData(WorldSizePreset.Medium)]
    public void GeneratedGeographySupportsAnEmptyPlayableCampAndNoBuildHighGround(WorldSizePreset size)
    {
        var map = GeneratedCampMapGenerator.Generate(new GeographyOptions(
            "river-world-a", size, WrapEastWest: true));

        Assert.Equal(GeographyGenerator.Dimensions(size), (map.Width, map.Height));
        Assert.True(MapAcceptance.Validate(map, allowEmptyCamp: true).IsValid);
        Assert.DoesNotContain(map.CampObjects, item => item.Kind == "founder");
        Assert.Contains(map.Tiles, item => item.Terrain == TerrainKind.River);
        Assert.Contains(map.Tiles, item => item.Terrain == TerrainKind.Mountain);
        Assert.All(map.Tiles.Where(item => item.Terrain is TerrainKind.Mountain or TerrainKind.Peak),
            item => Assert.False(map.IsBuildable(item.Position)));
        Assert.All(map.CampObjects, item => Assert.True(map.IsPassable(item.Position)));
        Assert.True(map.Resources.Count > 20, "Generated worlds need usable sites beyond the starter camp.");
        Assert.All(map.Resources, site => Assert.True(map.IsPassable(site.Position)));
        Assert.DoesNotContain(map.Resources, site => map.CampObjects.Any(item => item.Position == site.Position));
    }

    [Fact]
    public void GeneratedTreesAreDistinctDeterministicObjectsWithOneTreePerTile()
    {
        var options = new GeographyOptions("object-forest", WorldSizePreset.Small, WrapEastWest: true);
        var first = GeneratedCampMapGenerator.Generate(options);
        var second = GeneratedCampMapGenerator.Generate(options);
        var trees = first.Resources.Where(site => site.TreeKind is "broadleaf" or "conifer").ToArray();
        var orchards = first.Resources.Where(site => site.TreeKind == "orchard").ToArray();

        Assert.True(trees.Length > 20, "A generated forest needs visible individual trees, not only a terrain tint.");
        Assert.Contains(trees, tree => first.Tiles.Any(tile => tile.Position == tree.Position &&
            tile.Terrain == TerrainKind.Meadow));
        Assert.All(trees, tree =>
        {
            Assert.Equal("construction", tree.Kind);
            Assert.True(tree.IsRenewable);
            Assert.True(tree.TreeKind is "broadleaf" or "conifer");
            Assert.Single(first.Resources, resource => resource.Position == tree.Position);
        });
        Assert.Equal(trees.Length, trees.Select(tree => tree.Position).Distinct().Count());
        Assert.NotEmpty(orchards);
        Assert.All(orchards, orchard =>
        {
            Assert.Equal("fruit", orchard.Kind);
            Assert.True(orchard.IsRenewable);
            Assert.Single(first.Resources, resource => resource.Position == orchard.Position);
        });
        Assert.Equal(trees.Length + orchards.Length,
            first.Resources.Where(resource => resource.TreeKind is not null)
                .Select(resource => resource.Position).Distinct().Count());
        Assert.Equal(first.ManifestDigest, second.ManifestDigest);
        Assert.Equal(first.Resources, second.Resources);

        var overlapping = first with
        {
            Resources = first.Resources.Select(resource =>
                resource.Id == trees[1].Id ? resource with { Position = trees[0].Position } : resource).ToArray(),
        };
        overlapping = overlapping with { ManifestDigest = MapManifestCodec.Digest(overlapping) };
        Assert.False(MapAcceptance.Validate(overlapping, allowEmptyCamp: true).IsValid);
    }

    [Fact]
    public void TreeStumpAndPlantedSaplingPersistAcrossGeneratedWorldReload()
    {
        var options = new GeographyOptions("saved-tree-stages", WorldSizePreset.Small);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var tree = world.ExportState().Map.Resources.First(site => site.TreeKind is not null);
        var standing = world.WorldSystems.Ecology.GetResource(tree.Id);
        Assert.Equal(1, standing.Quantity);
        var cut = EcologyRules.Harvest(standing, 1);
        Assert.True(cut.IsValid);
        Assert.Equal(EcologyResourceState.Depleted, cut.Resource!.State);
        Assert.Equal(0, cut.Resource.Quantity);

        var state = world.ExportState();
        var planted = cut.Resource with
        {
            State = EcologyResourceState.Regenerating,
            IsPlanted = true,
            NextRegenerationDay = 3,
        };
        state = state with
        {
            Resources = state.Resources.Select(resource => resource.ResourceId == tree.Id
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = state.WorldSystems! with
            {
                Ecology = state.WorldSystems.Ecology with
                {
                    Resources = state.WorldSystems.Ecology.Resources.Select(resource =>
                        resource.Id == tree.Id ? planted : resource).ToArray(),
                },
            },
        };
        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(state));
        using var restored = PrivateWorldRuntime.Restore(saved);
        var projection = new OwnerWorldObservationStore(restored).GetSnapshot();
        var visible = Assert.Single(projection.Resources, resource => resource.Id == tree.Id);
        Assert.Equal(tree.TreeKind, visible.TreeKind);
        Assert.True(visible.IsPlanted);
        Assert.Equal(0, visible.Quantity);

        var beforeGrowth = EcologyRules.Regenerate(planted,
            WorldCalendarRules.FromTick(2 * restored.WorldSystems.Config.TicksPerDay, restored.WorldSystems.Config),
            restored.WorldSystems.Config);
        Assert.True(beforeGrowth.IsPlanted);
        var grown = EcologyRules.Regenerate(beforeGrowth,
            WorldCalendarRules.FromTick(3 * restored.WorldSystems.Config.TicksPerDay, restored.WorldSystems.Config),
            restored.WorldSystems.Config);
        Assert.False(grown.IsPlanted);
        Assert.Equal(1, grown.Quantity);
    }

    [Fact]
    public void PickedOrchardTreePersistsAndRegrowsFruitThroughVisibleStages()
    {
        var options = new GeographyOptions("saved-orchard-stages", WorldSizePreset.Small);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var orchard = world.ExportState().Map.Resources.First(resource => resource.TreeKind == "orchard");
        var fruiting = world.WorldSystems.Ecology.GetResource(orchard.Id);
        Assert.Equal("fruit", fruiting.Kind);
        Assert.Equal(1, fruiting.Quantity);
        Assert.Equal("fruiting", Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Resources,
            resource => resource.Id == orchard.Id).TreeStage);

        var pick = EcologyRules.Harvest(fruiting, 1);
        Assert.True(pick.IsValid);
        var picked = pick.Resource! with
        {
            NextRegenerationDay = 3,
            RegenerationSeason = SeasonKind.Winter,
        };
        var state = world.ExportState() with
        {
            Resources = world.ExportState().Resources.Select(resource => resource.ResourceId == orchard.Id
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = world.WorldSystems with
            {
                Ecology = world.WorldSystems.Ecology with
                {
                    Resources = world.WorldSystems.Ecology.Resources.Select(resource =>
                        resource.Id == orchard.Id ? picked : resource).ToArray(),
                },
            },
        };
        using var pickedWorld = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(
            PrivateWorldRuntimeCodec.Encode(state)));
        Assert.Equal("picked", Assert.Single(new OwnerWorldObservationStore(pickedWorld).GetSnapshot().Resources,
            resource => resource.Id == orchard.Id).TreeStage);

        var config = world.WorldSystems.Config;
        var growing = EcologyRules.Regenerate(picked,
            WorldCalendarRules.FromTick(config.TicksPerDay, config), config);
        Assert.Equal(EcologyResourceState.Regenerating, growing.State);
        var growingState = pickedWorld.ExportState();
        growingState = growingState with
        {
            WorldSystems = growingState.WorldSystems! with
            {
                Ecology = growingState.WorldSystems.Ecology with
                {
                    Resources = growingState.WorldSystems.Ecology.Resources.Select(resource =>
                        resource.Id == orchard.Id ? growing : resource).ToArray(),
                },
            },
        };
        using var growingWorld = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(
            PrivateWorldRuntimeCodec.Encode(growingState)));
        Assert.Equal("growing", Assert.Single(new OwnerWorldObservationStore(growingWorld).GetSnapshot().Resources,
            resource => resource.Id == orchard.Id).TreeStage);
        var regrown = EcologyRules.Regenerate(growing,
            WorldCalendarRules.FromTick(3 * config.TicksPerDay, config), config);
        Assert.Equal(1, regrown.Quantity);
        Assert.Equal(EcologyResourceState.Available, regrown.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EarlierGeneratedWorldStillLoadsWithItsOriginalResources(bool retainsWoodlandTrees)
    {
        var options = new GeographyOptions("old-forest-save", WorldSizePreset.Small);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var current = world.ExportState();
        var originalResources = current.Map.Resources
            .Where(resource => !resource.Id.StartsWith("orchard-", StringComparison.Ordinal) &&
                (retainsWoodlandTrees || !resource.Id.StartsWith("tree-", StringComparison.Ordinal)))
            .Select(resource => retainsWoodlandTrees ? resource : resource with { TreeKind = null }).ToArray();
        var oldIds = originalResources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var oldMap = current.Map with { Resources = originalResources, ManifestDigest = string.Empty };
        oldMap = oldMap with { ManifestDigest = MapManifestCodec.Digest(oldMap) };
        var oldSystems = current.WorldSystems! with
        {
            Ecology = current.WorldSystems.Ecology with
            {
                Resources = current.WorldSystems.Ecology.Resources.Where(resource => oldIds.Contains(resource.Id)).ToArray(),
            },
            Chunks = current.WorldSystems.Chunks.Select(chunk => ChunkManifestCodec.WithDigest(chunk with
            {
                Resources = chunk.Resources.Where(resource => oldIds.Contains(resource.ResourceId)).ToArray(),
            })).ToArray(),
        };
        var oldCheckpoint = current with
        {
            Map = oldMap,
            Resources = current.Resources.Where(resource => oldIds.Contains(resource.ResourceId)).ToArray(),
            WorldSystems = oldSystems,
        };

        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(
            PrivateWorldRuntimeCodec.Encode(oldCheckpoint)));
        Assert.Equal(oldMap.ManifestDigest, restored.ExportState().Map.ManifestDigest);
        Assert.DoesNotContain(restored.ExportState().Map.Resources, resource => resource.TreeKind == "orchard");
        if (!retainsWoodlandTrees)
            Assert.DoesNotContain(restored.ExportState().Map.Resources, resource => resource.TreeKind is not null);
    }

    [Fact]
    public async Task AgentReplantsHarvestedTreeFromCarriedSeed()
    {
        var options = new GeographyOptions("agent-replanting", WorldSizePreset.Small);
        var initial = StartedGeneratedWorld(options);
        var map = initial.Map;
        var actor = initial.Inhabitants[0].InhabitantId;
        var occupied = initial.Inhabitants.Skip(1).Select(person => person.Position).ToHashSet();
        var treeSite = map.Resources.Where(site => site.TreeKind is "broadleaf" or "conifer" && site.IsRenewable &&
                map.IsReachableFromCampOnFoot(site.Position))
            .Select(site => new
            {
                Site = site,
                Stand = map.FootNeighbors(site.Position).FirstOrDefault(position =>
                    map.IsPassable(position) && !occupied.Contains(position) &&
                    !map.Resources.Any(resource => resource.Position == position)),
            })
            .First(item => map.Contains(item.Stand) && map.IsPassable(item.Stand));
        var stump = initial.WorldSystems!.Ecology.GetResource(treeSite.Site.Id) with
        {
            Quantity = 0,
            State = EcologyResourceState.Regenerating,
            NextRegenerationDay = 6,
        };
        var seededInventory = InventoryFixture.AddLot(initial.Society.Society.Inventory,
            "replant-test-seed", "seed", actor, 1);
        var state = initial with
        {
            Inhabitants = initial.Inhabitants.Select(person => person.InhabitantId == actor
                ? person with { Position = treeSite.Stand, HungerBasisPoints = 9_000, EnergyBasisPoints = 9_000 }
                : person).ToArray(),
            Resources = initial.Resources.Select(resource => resource.ResourceId == treeSite.Site.Id
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = initial.WorldSystems with
            {
                Ecology = initial.WorldSystems.Ecology with
                {
                    Resources = initial.WorldSystems.Ecology.Resources.Select(resource =>
                        resource.Id == stump.Id ? stump : resource).ToArray(),
                },
            },
            Society = initial.Society with
            {
                Society = initial.Society.Society with { Inventory = seededInventory },
            },
        };
        using var world = PrivateWorldRuntime.Restore(state,
            id => id == actor ? new ReplantProvider() : new DeterministicDecisionProvider());
        for (var tick = 0; tick < 12 && !world.ExportState().Events.Any(item => item.Kind == "tree_replanted"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(world.ExportState().Events, item => item.Kind == "tree_replanted" &&
            item.Detail.Contains(treeSite.Site.Id, StringComparison.Ordinal));
        Assert.True(world.WorldSystems.Ecology.GetResource(treeSite.Site.Id).IsPlanted);
        Assert.DoesNotContain(world.Society.Inventory.Lots, lot => lot.Id == "replant-test-seed" && lot.Quantity > 0);
        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(saved);
        Assert.True(restored.WorldSystems.Ecology.GetResource(treeSite.Site.Id).IsPlanted);
        Assert.True(Assert.Single(new OwnerWorldObservationStore(restored).GetSnapshot().Resources,
            resource => resource.Id == treeSite.Site.Id).IsPlanted);
    }

    [Fact]
    public async Task AgentPicksOrchardFruitAndEatsTheDistinctItem()
    {
        var options = new GeographyOptions("agent-orchard-fruit", WorldSizePreset.Small);
        var initial = StartedGeneratedWorld(options);
        var map = initial.Map;
        var actor = initial.Inhabitants[0].InhabitantId;
        var occupied = initial.Inhabitants.Skip(1).Select(person => person.Position).ToHashSet();
        var orchard = map.Resources.Where(resource => resource.TreeKind == "orchard" &&
                map.IsReachableFromCampOnFoot(resource.Position))
            .Select(resource => new
            {
                Site = resource,
                Stand = map.FootNeighbors(resource.Position).FirstOrDefault(position =>
                    map.IsPassable(position) && !occupied.Contains(position) &&
                    !map.Resources.Any(other => other.Position == position)),
            })
            .First(item => map.Contains(item.Stand) && map.IsPassable(item.Stand));
        var otherFoodIds = map.Resources.Where(resource => resource.Kind == "food")
            .Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var state = initial with
        {
            Inhabitants = initial.Inhabitants.Select(person => person.InhabitantId == actor
                ? person with { Position = orchard.Stand, HungerBasisPoints = 4_000, EnergyBasisPoints = 9_000 }
                : person).ToArray(),
            Resources = initial.Resources.Select(resource => otherFoodIds.Contains(resource.ResourceId)
                ? resource with { State = ResourceState.Depleted } : resource).ToArray(),
            WorldSystems = initial.WorldSystems! with
            {
                Ecology = initial.WorldSystems.Ecology with
                {
                    Resources = initial.WorldSystems.Ecology.Resources.Select(resource =>
                        otherFoodIds.Contains(resource.Id)
                            ? resource with { Quantity = 0, State = EcologyResourceState.Depleted }
                            : resource).ToArray(),
                },
            },
        };
        using var world = PrivateWorldRuntime.Restore(state,
            id => id == actor ? new OrchardProvider() : new DeterministicDecisionProvider());
        for (var tick = 0; tick < 12 && !world.ExportState().Events.Any(item => item.Kind == "fruit_harvested"); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        Assert.Contains(world.ExportState().Events, item => item.Kind == "fruit_harvested" &&
            item.Detail.Contains(orchard.Site.Id, StringComparison.Ordinal));
        Assert.Equal(0, world.WorldSystems.Ecology.GetResource(orchard.Site.Id).Quantity);
        Assert.Equal("picked", Assert.Single(new OwnerWorldObservationStore(world).GetSnapshot().Resources,
            resource => resource.Id == orchard.Site.Id).TreeStage);
        Assert.Contains(world.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "fruit" && lot.Quantity == 4);
        for (var tick = 0; tick < 12 && !world.ExportState().Events.Any(item => item.Kind == "food_consumed" &&
                 item.Detail == actor); tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
        Assert.Contains(world.ExportState().Events, item => item.Kind == "food_consumed" && item.Detail == actor);
        Assert.True(world.Inhabitants.Single(person => person.InhabitantId == actor).HungerBasisPoints > 4_000);
        using var restored = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(
            PrivateWorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Contains(restored.Society.Inventory.Lots, lot => lot.OwnerId == actor && lot.ItemKind == "fruit" && lot.Quantity > 0);
        Assert.Equal("growing", Assert.Single(new OwnerWorldObservationStore(restored).GetSnapshot().Resources,
            resource => resource.Id == orchard.Site.Id).TreeStage);
    }

    private sealed class OrchardProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var chosen = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id is "harvest_food" or "consume_food");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = chosen is null ? request.Observation.Candidates : [chosen],
                },
            }, cancellationToken);
        }
    }

    private sealed class ReplantProvider : IDecisionProvider
    {
        public DecisionProviderKind Kind => DecisionProviderKind.Deterministic;
        public long ProviderEpoch => 0;
        public ValueTask<CognitionDecisionResponse> DecideAsync(CognitionDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var desired = request.Observation.Candidates.FirstOrDefault(candidate => candidate.Id == "replant_tree");
            return new DeterministicDecisionProvider().DecideAsync(request with
            {
                Observation = request.Observation with
                {
                    Candidates = desired is null ? request.Observation.Candidates : [desired],
                },
            }, cancellationToken);
        }
    }

    private static PrivateWorldRuntimeState StartedGeneratedWorld(GeographyOptions options)
    {
        using var setup = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var map = setup.ExportState().Map;
        var bedroll = map.GetObject("bedroll").Position;
        var startingTiles = map.Tiles.Where(tile =>
                Math.Abs(tile.Position.X - bedroll.X) <= 5 &&
                Math.Abs(tile.Position.Y - bedroll.Y) <= 5 &&
                map.IsPassable(tile.Position) &&
                !map.CampObjects.Any(item => item.Position == tile.Position) &&
                !map.Resources.Any(item => item.Position == tile.Position))
            .Take(4).Select(tile => tile.Position).ToArray();
        Assert.Equal(4, startingTiles.Length);
        for (var index = 0; index < 4; index++)
            setup.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), startingTiles[index]);
        setup.StartWorld();
        return setup.ExportState();
    }

    [Fact]
    public async Task GeneratedCampCanStartAdvanceAndRestoreWithItsOwnGeography()
    {
        var options = new GeographyOptions("generated-life", WorldSizePreset.Small, WrapEastWest: true);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup, geographyOptions: options);
        var initial = world.ExportState();
        Assert.Equal(options, initial.Geography);
        Assert.True(initial.Map.WrapsEastWest);
        Assert.Empty(world.Inhabitants);
        Assert.True(world.Society.IsPaused);
        var projection = new OwnerWorldObservationStore(world).GetSnapshot();
        Assert.Empty(projection.Tiles);
        Assert.Equal((256, 128), (projection.PackedTerrain?.Width, projection.PackedTerrain?.Height));
        Assert.Equal("terrain-kind-v1", projection.PackedTerrain!.Encoding);
        Assert.True(projection.WrapsEastWest);
        Assert.Equal(WeatherRules.RegionSize, projection.WeatherRegionSize);
        Assert.Equal((256 / WeatherRules.RegionSize) * (128 / WeatherRules.RegionSize),
            projection.WeatherRegions.Count);
        Assert.True(projection.WeatherRegions.Select(region => region.Weather).Distinct().Count() > 1,
            "A generated world must not have one planet-wide weather condition.");
        Assert.All(projection.WeatherRegions,
            region => Assert.InRange(region.SoilMoisture ?? -1, 0, 100));
        var terrainBytes = Convert.FromBase64String(projection.PackedTerrain.Data);
        Assert.Equal(initial.Map.Tiles.Count, terrainBytes.Length);
        Assert.Equal((byte)initial.Map.Tiles[0].Terrain, terrainBytes[0]);
        var townStorage = initial.Map.GetObject("storage").Position;
        var positions = initial.Map.Tiles.Where(tile =>
                tile.Position.X >= townStorage.X - 1 && tile.Position.X < townStorage.X + 5 &&
                tile.Position.Y >= townStorage.Y - 1 && tile.Position.Y < townStorage.Y + 4 &&
                initial.Map.IsPassable(tile.Position) &&
                !initial.Map.CampObjects.Any(item => item.Position == tile.Position) &&
                !initial.Map.Resources.Any(item => item.Position == tile.Position))
            .Take(4).Select(tile => tile.Position).ToArray();
        Assert.Equal(4, positions.Length);
        for (var index = 0; index < positions.Length; index++)
            world.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), positions[index]);
        world.StartWorld();
        Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var saved = PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState()));
        using var restored = PrivateWorldRuntime.Restore(saved);
        Assert.Equal(1, restored.WorldTick);
        Assert.Equal(initial.Map.ManifestDigest, restored.ExportState().Map.ManifestDigest);
        Assert.Equal(options, restored.ExportState().Geography);
        Assert.True(restored.ExportState().Map.WrapsEastWest);
        using var legacyCheckpoint = PrivateWorldRuntime.Restore(saved with
        {
            Map = saved.Map with { WrapsEastWest = false },
        });
        Assert.True(legacyCheckpoint.ExportState().Map.WrapsEastWest);
        Assert.True(new OwnerWorldObservationStore(restored).GetSnapshot().WrapsEastWest);
        Assert.Equal(4, restored.Inhabitants.Count);
        Assert.True(restored.WorldSystems.Chunks.Count > 1);
        Assert.All(restored.ExportState().Map.Resources, site =>
        {
            var chunk = Assert.Single(restored.WorldSystems.Chunks, item =>
                item.Coordinate == ChunkRules.ToChunkCoordinate(site.Position, item.ChunkSize));
            Assert.Contains(chunk.Resources, item => item.ResourceId == site.Id &&
                item.LocalPosition == ChunkRules.ToLocalPoint(site.Position, chunk.ChunkSize));
        });
    }

    [Theory]
    [InlineData("river-world-a", true)]
    [InlineData("river-world-b", false)]
    public void GeneratedRiversFollowAnAcyclicRouteToWater(string seed, bool wrap)
    {
        var map = GeographyGenerator.Generate(new GeographyOptions(seed, WorldSizePreset.Small, wrap));

        Assert.Equal(0, map.Width % GeographyGenerator.ChunkSize);
        Assert.Equal(0, map.Height % GeographyGenerator.ChunkSize);
        Assert.True(map.Count(WaterKind.Ocean) > 0);
        Assert.True(map.Count(WaterKind.River) > 0);

        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
            {
                if (map.At(x, y).Water != WaterKind.River) continue;
                var visited = new HashSet<(int X, int Y)>();
                var cursor = (X: x, Y: y);
                while (map.At(cursor.X, cursor.Y).Water is WaterKind.Land or WaterKind.River)
                {
                    Assert.True(visited.Add(cursor), $"Drainage cycle from {x},{y}");
                    var next = map.DownstreamAt(cursor.X, cursor.Y);
                    Assert.NotNull(next);
                    Assert.Equal(1, Math.Abs(next.Value.Y - cursor.Y) +
                        Math.Min(Math.Abs(next.Value.X - cursor.X),
                            wrap ? map.Width - Math.Abs(next.Value.X - cursor.X) : map.Width));
                    cursor = next.Value;
                }

                Assert.True(map.At(cursor.X, cursor.Y).Water is WaterKind.Ocean or WaterKind.Lake);
            }
    }

    [Fact]
    public void SeedAndOptionsDetermineTheGeography()
    {
        var options = new GeographyOptions("world-one", WorldSizePreset.Small);
        var first = GeographyGenerator.Generate(options);
        var again = GeographyGenerator.Generate(options);
        var different = GeographyGenerator.Generate(options with { Seed = "world-two" });
        var changed = 0;
        for (var y = 0; y < first.Height; y++)
            for (var x = 0; x < first.Width; x++)
            {
                Assert.Equal(first.At(x, y), again.At(x, y));
                if (first.At(x, y) != different.At(x, y)) changed++;
            }

        Assert.True(changed > first.Width);
    }

    [Fact]
    public void ClimateSelectionChangesPlayableGroundAndKeepsPolarCapsOptional()
    {
        var dryOptions = new GeographyOptions("climate-choice", WorldSizePreset.Small,
            ClimateMode: ClimateMode.Uniform, SelectedClimate: ClimateZone.Dry, LatitudeCooling: false);
        var dry = GeographyGenerator.Generate(dryOptions);
        var tropical = GeographyGenerator.Generate(dryOptions with { SelectedClimate = ClimateZone.Tropical });
        Assert.Equal(ClimateZone.Dry, dry.At(50, 50).Climate);
        Assert.Equal(ClimateZone.Tropical, tropical.At(50, 50).Climate);
        Assert.Equal(dry.At(50, 50).Temperature, tropical.At(50, 50).Temperature);
        var dryMap = GeneratedCampMapGenerator.Generate(dryOptions);
        var tropicalMap = GeneratedCampMapGenerator.Generate(dryOptions with { SelectedClimate = ClimateZone.Tropical });
        Assert.Contains(dryMap.Tiles, tile => tile.Terrain == TerrainKind.Sand);
        Assert.Contains(tropicalMap.Tiles, tile => tile.Terrain == TerrainKind.Forest);
        Assert.All(dryMap.Resources.Where(site => site.Id.StartsWith("wild-", StringComparison.Ordinal)),
            site => Assert.True(site.Kind is "stone" or "fiber"));
        Assert.Contains(tropicalMap.Resources, site => site.Id.StartsWith("wild-", StringComparison.Ordinal) &&
            site.Kind == "construction" && site.IsRenewable);
        Assert.NotEqual(dryMap.ManifestDigest, tropicalMap.ManifestDigest);
        Assert.True(MapAcceptance.Validate(dryMap, allowEmptyCamp: true).IsValid);

        var capped = GeographyGenerator.Generate(dryOptions with { LatitudeCooling = true });
        Assert.Equal(ClimateZone.Polar, capped.At(50, 0).Climate);
        Assert.Equal(ClimateZone.Dry, capped.At(50, capped.Height / 2).Climate);

        var dominant = GeographyGenerator.Generate(dryOptions with { ClimateMode = ClimateMode.Dominant });
        var dryLand = 0;
        var otherLand = 0;
        for (var y = 0; y < dominant.Height; y++)
            for (var x = 0; x < dominant.Width; x++)
            {
                var tile = dominant.At(x, y);
                if (tile.Water != WaterKind.Land) continue;
                if (tile.Climate == ClimateZone.Dry) dryLand++;
                else otherLand++;
            }
        Assert.True(dryLand > otherLand, "The selected climate should dominate land.");
        Assert.True(otherLand > 0, "Dominant mode should still allow natural climate regions.");
    }

    [Fact]
    public void RegionalWeatherUsesClimateWithoutChangingTheGlobalCalendar()
    {
        var config = WorldSystemsConfig.Default;
        var dryWetDays = 0;
        var tropicalWetDays = 0;
        for (var day = 0; day < 100; day++)
        {
            var season = WorldCalendarRules.GetSeason(day % config.DaysPerYear, config);
            var dry = WeatherRules.WeatherForRegion("climate-choice", day, season, config,
                1, 1, 4, ClimateZone.Dry);
            var tropical = WeatherRules.WeatherForRegion("climate-choice", day, season, config,
                1, 1, 4, ClimateZone.Tropical);
            if (dry is WeatherKind.Rain or WeatherKind.Storm) dryWetDays++;
            if (tropical is WeatherKind.Rain or WeatherKind.Storm) tropicalWetDays++;
            Assert.NotEqual(WeatherKind.Snow, tropical);
        }
        Assert.True(tropicalWetDays > dryWetDays);
    }

    [Fact]
    public void ResourceAbundanceChangesRealSitesWithoutOverloadingChunks()
    {
        var options = new GeographyOptions("abundance-choice", WorldSizePreset.Small);
        var sparse = GeneratedCampMapGenerator.Generate(options with { ResourceAbundance = ResourceAbundance.Sparse });
        var normal = GeneratedCampMapGenerator.Generate(options);
        var abundant = GeneratedCampMapGenerator.Generate(options with { ResourceAbundance = ResourceAbundance.Abundant });
        Assert.True(sparse.Resources.Count < normal.Resources.Count);
        Assert.True(normal.Resources.Count < abundant.Resources.Count);
        Assert.NotEqual(sparse.ManifestDigest, abundant.ManifestDigest);
        Assert.True(MapAcceptance.Validate(abundant, allowEmptyCamp: true).IsValid);
        using var world = new PrivateWorldRuntime(options.Seed,
            startPace: WorldStartPace.FounderSetup,
            geographyOptions: options with { ResourceAbundance = ResourceAbundance.Abundant });
        Assert.All(world.WorldSystems.Chunks,
            chunk => Assert.True(chunk.Resources.Count <= world.WorldSystems.Config.MaxResourcesPerChunk));
    }
}
