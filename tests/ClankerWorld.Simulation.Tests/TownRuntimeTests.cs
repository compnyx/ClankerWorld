using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;
using ClankerWorld.GodotClient.UI;
using ClankerWorld.Viewer.Observation;
using System.Text.Json;
using GodotOwnerWorldSnapshot = ClankerWorld.GodotClient.UI.OwnerWorldSnapshot;

namespace ClankerWorld.Simulation.Tests;

public sealed class TownRuntimeTests
{
    private static readonly JsonSerializerOptions GodotJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PausedFounderTownMembershipAndBordersSurviveSaveLoadAndProjectToOwnerAndTelemetry()
    {
        var geography = new GeographyOptions("first-town-persistence", WorldSizePreset.Small);
        var directory = Directory.CreateTempSubdirectory("clankerworld-town-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup, newWorldGeography: geography);
            using var world = file.LoadOrCreate(geography.Seed);
            var foundingTown = Assert.Single(world.Towns);
            Assert.Equal(TownBorderRules.FirstTownId, foundingTown.Id);
            Assert.Equal(TownBorderRules.FirstTownName, foundingTown.Name);
            Assert.Equal("founding", foundingTown.FoundingState);
            Assert.Empty(foundingTown.ResidentIds);
            Assert.Empty(foundingTown.AssignedBuildingIds);
            Assert.NotEmpty(foundingTown.BorderTiles);
            Assert.True(world.Society.IsPaused);

            var founderIds = new List<string>();
            var founderPositions = FounderPositions(world.ExportState().Map);
            for (var index = 0; index < founderPositions.Length; index++)
            {
                var founderId = "founder:" + Guid.NewGuid().ToString("N");
                founderIds.Add(founderId);
                world.PlaceFounder(founderId, founderPositions[index]);
            }

            Assert.True(world.Society.IsPaused);
            var populatedTown = Assert.Single(world.Towns);
            Assert.Equal("founding", populatedTown.FoundingState);
            Assert.Equal(founderIds.Order(StringComparer.Ordinal), populatedTown.ResidentIds);
            var ownerSnapshot = new OwnerWorldObservationStore(world).GetSnapshot();
            var projected = Assert.Single(ownerSnapshot.Towns);
            Assert.Equal(populatedTown.Id, projected.Id);
            Assert.Equal(populatedTown.Name, projected.Name);
            Assert.Equal("founding", projected.FoundingState);
            Assert.Equal(populatedTown.ResidentIds, projected.ResidentIds);
            Assert.Equal(populatedTown.BorderTiles.Select(point => (point.X, point.Y)),
                projected.BorderTiles.Select(point => (point.X, point.Y)));
            var godotSnapshot = JsonSerializer.Deserialize<GodotOwnerWorldSnapshot>(
                JsonSerializer.Serialize(ownerSnapshot, GodotJsonOptions), GodotJsonOptions);
            var godotTown = Assert.Single(godotSnapshot!.Towns);
            Assert.Equal(populatedTown.Name, godotTown.Name);
            Assert.Equal(populatedTown.ResidentIds, godotTown.ResidentIds);
            Assert.Equal(populatedTown.BorderTiles.Select(point => (point.X, point.Y)),
                godotTown.BorderTiles.Select(point => (point.X, point.Y)));

            Assert.True(world.RenameAgent(founderIds[0], "credential-secret-token"));
            var logger = new RecordingLogger<PrivateWorldRuntimeService>();
            using (var service = new PrivateWorldRuntimeService(world, file,
                       new OwnerClientPresenceLease(TimeSpan.FromSeconds(30)), logger))
            {
                await service.StartAsync(CancellationToken.None);
                await service.StopAsync(CancellationToken.None);
            }
            Assert.Contains(logger.Messages, message => message.Contains(
                "town_transition tick=0 town=town:first transition=StateLoaded residents=4 buildings=0 border_tiles=",
                StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Messages, message => message.Contains("credential-secret-token", StringComparison.Ordinal));

            file.Save(world);
            using var reloaded = file.LoadOrCreate(geography.Seed);
            var restoredTown = Assert.Single(reloaded.Towns);
            Assert.Equal(populatedTown.Id, restoredTown.Id);
            Assert.Equal(populatedTown.Name, restoredTown.Name);
            Assert.Equal(populatedTown.FoundingState, restoredTown.FoundingState);
            Assert.Equal(populatedTown.ResidentIds, restoredTown.ResidentIds);
            Assert.Equal(populatedTown.BorderTiles, restoredTown.BorderTiles);

            var legacySchema = reloaded.ExportState() with { SchemaVersion = 20, Towns = null };
            using var migrated = PrivateWorldRuntime.Restore(PrivateWorldRuntimeCodec.Decode(
                PrivateWorldRuntimeCodec.Encode(legacySchema)));
            Assert.Equal(PrivateWorldRuntime.StateSchemaVersion, migrated.ExportState().SchemaVersion);
            Assert.Equal(restoredTown.ResidentIds, Assert.Single(migrated.Towns).ResidentIds);
            Assert.Equal(restoredTown.BorderTiles, Assert.Single(migrated.Towns).BorderTiles);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task TownAssignedBuildingExpandsTheBorderAndSurvivesOwnerProjectionAndReload()
    {
        var geography = new GeographyOptions("first-town-growth", WorldSizePreset.Small);
        var directory = Directory.CreateTempSubdirectory("clankerworld-town-growth-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup, newWorldGeography: geography);
            using var world = file.LoadOrCreate(geography.Seed);
            PlaceFourFounders(world);
            world.StartWorld();
            Assert.True(world.StageStarterContent());
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);
            Assert.Empty(world.RoadTiles);

            var definition = world.WorldContent.Buildings.Single(item => item.Tags.Contains("shelter", StringComparer.Ordinal));
            var town = Assert.Single(world.Towns);
            var map = world.ExportState().Map;
            var occupied = map.CampObjects.Select(item => item.Position)
                .Concat(map.Resources.Select(item => item.Position))
                .Concat(world.WorldSimulation.Buildings.SelectMany(building =>
                {
                    var size = world.WorldContent.Buildings.Single(item => item.CanonicalId == building.DefinitionId);
                    return WorldContentSimulationRules.Footprint(size, building.Position);
                }))
                .ToHashSet();
            var position = map.Tiles.Select(tile => tile.Position).First(point =>
                map.IsBuildable(point) && !occupied.Contains(point) && !town.BorderTiles.Contains(point) &&
                TownBorderRules.IsWithinOrAdjacent(town, point, definition.Width, definition.Height) &&
                TownBorderRules.ExpandForBuilding(map, town, point, definition.Width, definition.Height).Count > town.BorderTiles.Count);

            var result = world.PlaceBuilding("town-border-test", definition.CanonicalId, position);
            Assert.True(result.Applied, result.Failure);
            var placed = Assert.Single(world.WorldSimulation.Buildings, item => item.InstanceId == result.InstanceId);
            Assert.Equal(TownBorderRules.FirstTownId, placed.TownId);
            var grownTown = Assert.Single(world.Towns);
            Assert.Contains(placed.InstanceId, grownTown.AssignedBuildingIds);
            Assert.True(grownTown.BorderTiles.Count > town.BorderTiles.Count);
            Assert.Contains(world.ExportState().Events, item => item.Kind == "town_building_assigned");
            Assert.Contains(world.ExportState().Events, item => item.Kind == "town_border_expanded");
            Assert.NotEmpty(world.RoadTiles);
            Assert.Contains(position, world.RoadTiles);
            Assert.All(world.RoadTiles, point => Assert.True(map.IsBuildable(point)));
            Assert.Contains(world.ExportState().Events, item => item.Kind == "town_road_generated");

            var snapshot = new OwnerWorldObservationStore(world).GetSnapshot();
            var projectedTown = Assert.Single(snapshot.Towns);
            Assert.Equal(grownTown.BorderTiles.Select(point => (point.X, point.Y)),
                projectedTown.BorderTiles.Select(point => (point.X, point.Y)));
            Assert.Contains(placed.InstanceId, projectedTown.AssignedBuildingIds);
            Assert.Equal(world.RoadTiles.Select(point => (point.X, point.Y)),
                snapshot.RoadTiles.Select(point => (point.X, point.Y)));
            Assert.Equal(TownBorderRules.FirstTownId,
                Assert.Single(snapshot.PlacedBuildings, item => item.InstanceId == placed.InstanceId).TownId);
            var godotSnapshot = JsonSerializer.Deserialize<GodotOwnerWorldSnapshot>(
                JsonSerializer.Serialize(snapshot, GodotJsonOptions), GodotJsonOptions);
            var godotTown = Assert.Single(godotSnapshot!.Towns);
            Assert.Contains(placed.InstanceId, godotTown.AssignedBuildingIds);
            Assert.Equal(world.RoadTiles.Select(point => (point.X, point.Y)),
                godotSnapshot.RoadTiles.Select(point => (point.X, point.Y)));
            Assert.Equal(TownBorderRules.FirstTownId,
                Assert.Single(godotSnapshot.PlacedBuildings, item => item.InstanceId == placed.InstanceId).TownId);

            file.Save(world);
            using var reloaded = file.LoadOrCreate(geography.Seed);
            var restoredTown = Assert.Single(reloaded.Towns);
            Assert.Equal(grownTown.BorderTiles, restoredTown.BorderTiles);
            Assert.Equal(grownTown.AssignedBuildingIds, restoredTown.AssignedBuildingIds);
            Assert.Equal(world.RoadTiles, reloaded.RoadTiles);
            Assert.Equal(TownBorderRules.FirstTownId,
                Assert.Single(reloaded.WorldSimulation.Buildings, item => item.InstanceId == placed.InstanceId).TownId);
            var invalidRoads = reloaded.ExportState() with
            {
                RoadTiles = reloaded.RoadTiles.Append(reloaded.RoadTiles[0]).ToArray(),
            };
            Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(invalidRoads));
            Assert.Throws<InvalidDataException>(() => PrivateWorldRuntime.Restore(reloaded.ExportState() with
            {
                RoadTiles = null,
            }));
            using var beforeRoadSchema = PrivateWorldRuntime.Restore(reloaded.ExportState() with
            {
                SchemaVersion = 23,
                RoadTiles = null,
            });
            Assert.Empty(beforeRoadSchema.RoadTiles);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void PlaceFourFounders(PrivateWorldRuntime world)
    {
        var map = world.ExportState().Map;
        var positions = FounderPositions(map);
        for (var index = 0; index < positions.Length; index++)
            world.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), positions[index]);
    }

    private static GridPoint[] FounderPositions(SeededMap map)
    {
        var storage = map.GetObject("storage").Position;
        var positions = map.Tiles.Where(tile =>
                Math.Abs(tile.Position.X - storage.X) <= 5 &&
                Math.Abs(tile.Position.Y - storage.Y) <= 5 &&
                map.IsPassable(tile.Position) &&
                !map.CampObjects.Any(item => item.Position == tile.Position) &&
                !map.Resources.Any(item => item.Position == tile.Position))
            .Take(PrivateWorldRuntime.RequiredFounders)
            .Select(tile => tile.Position)
            .ToArray();
        Assert.Equal(PrivateWorldRuntime.RequiredFounders, positions.Length);
        return positions;
    }
}
