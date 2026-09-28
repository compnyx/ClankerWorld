using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Viewer.Observation;

namespace ClankerWorld.Simulation.Tests;

public sealed class FounderSetupTests
{
    [Fact]
    public async Task AddedAdultOnRecordedHouseholdPropertyJoinsThatHouseholdAndEnclosingTown()
    {
        using var world = new PrivateWorldRuntime("new-agent-house-property", startPace: WorldStartPace.FounderSetup);
        foreach (var position in new[]
                 {
                     new GridPoint(0, 0), new GridPoint(1, 2), new GridPoint(2, 2), new GridPoint(3, 2),
                 })
            world.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), position);
        world.StartWorld();
        Assert.True(world.StageStarterContent());
        for (var tick = 0; tick < 8; tick++)
            Assert.True((await world.AdvanceOneTickAsync()).Advanced);

        var state = world.ExportState();
        var house = world.WorldContent.Buildings.Single(building => building.LocalId == "house-1x1");
        var site = state.Map.Tiles.Select(tile => tile.Position).First(point =>
            state.Map.IsBuildable(point) &&
            TownBorderRules.IsWithinOrAdjacent(world.Towns.Single(), point, house.Width, house.Height) &&
            !state.Map.CampObjects.Any(item => item.Position == point) &&
            !state.Map.Resources.Any(item => item.Position == point) &&
            !state.Inhabitants.Any(person => person.Position == point));
        var placed = world.PlaceBuilding("starter-house-alpha", house.CanonicalId, site, "household:camp-alpha");
        Assert.True(placed.Applied, placed.Failure);

        var agentId = "agent:" + Guid.NewGuid().ToString("N");
        var householdCount = world.Society.Households.Count;
        Assert.Equal("household:camp-alpha", world.AddAgent(agentId, site));
        Assert.Equal(householdCount, world.Society.Households.Count);
        Assert.Contains(agentId, world.Society.GetHousehold("household:camp-alpha").MemberIds);
        Assert.Contains(agentId, world.Towns.Single().ResidentIds);
        var secondAgentId = "agent:" + Guid.NewGuid().ToString("N");
        Assert.Equal("household:camp-alpha", world.AddAgent(secondAgentId, site));
        Assert.Equal(householdCount, world.Society.Households.Count);
        Assert.Equal(site, world.Inhabitants.Single(item => item.InhabitantId == secondAgentId).Position);

        using var restored = PrivateWorldRuntime.Restore(
            PrivateWorldRuntimeCodec.Decode(PrivateWorldRuntimeCodec.Encode(world.ExportState())));
        Assert.Equal("household:camp-alpha", restored.Society.GetInhabitant(agentId).HouseholdId);
        Assert.Equal("household:camp-alpha", restored.Society.GetInhabitant(secondAgentId).HouseholdId);
        Assert.Contains(agentId, restored.Towns.Single().ResidentIds);
        Assert.Contains(secondAgentId, restored.Towns.Single().ResidentIds);
    }

    [Fact]
    public async Task EmptyBaseCampPersistsFounderProgressAndOnlyStartsOnExplicitCommand()
    {
        var directory = Directory.CreateTempSubdirectory("clankerworld-founders-");
        try
        {
            var file = new PrivateWorldStateFile(Path.Combine(directory.FullName, "world.json"),
                newWorldPace: WorldStartPace.FounderSetup);
            using (var created = file.LoadOrCreate("new-camp"))
            {
                Assert.Empty(created.Inhabitants);
                Assert.Empty(created.Society.Inhabitants);
                Assert.Equal(2, created.Society.Households.Count);
                Assert.False(created.FounderSetup!.Started);
                Assert.True(created.Society.IsPaused);
                Assert.Equal(2, created.ExportState().Map.CampObjects.Count(item => item.Kind == "shelter"));
                Assert.DoesNotContain(created.ExportState().Map.CampObjects, item => item.Kind == "founder");
                Assert.Throws<InvalidOperationException>(created.Resume);
                Assert.Throws<InvalidOperationException>(created.StartWorld);
                Assert.Throws<InvalidOperationException>(() => created.AddAgent(
                    "agent:" + Guid.NewGuid().ToString("N"), new GridPoint(4, 2)));
                created.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(0, 0));
                created.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(1, 2));
                file.Save(created);
            }

            using var resumedSetup = file.LoadOrCreate("new-camp");
            Assert.Equal(2, resumedSetup.FounderSetup!.FounderIds.Count);
            Assert.True(resumedSetup.Society.IsPaused);
            Assert.Equal(2, resumedSetup.Society.Households.Single(item => item.Id == "household:camp-alpha").MemberIds.Count);
            resumedSetup.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(2, 2));
            resumedSetup.PlaceFounder("founder:" + Guid.NewGuid().ToString("N"), new GridPoint(3, 2));
            Assert.Equal(2, resumedSetup.Society.Households.Single(item => item.Id == "household:camp-beta").MemberIds.Count);
            Assert.True(resumedSetup.Society.IsPaused);
            resumedSetup.StartWorld();
            Assert.False(resumedSetup.Society.IsPaused);
            Assert.True(resumedSetup.FounderSetup.Started);
            var tick = await resumedSetup.AdvanceOneTickAsync();
            Assert.True(tick.Advanced);
            var map = resumedSetup.ExportState().Map;
            var position = map.Tiles.Select(tile => tile.Position).First(point =>
                map.IsPassable(point) &&
                !map.CampObjects.Any(item => item.Position == point) &&
                !map.Resources.Any(item => item.Position == point) &&
                !resumedSetup.Inhabitants.Any(item => item.Position == point));
            var agentId = "agent:" + Guid.NewGuid().ToString("N");
            var householdId = resumedSetup.AddAgent(agentId, position);
            Assert.Equal("household:" + agentId, householdId);
            Assert.Equal(agentId, resumedSetup.Society.Households.Single(item => item.Id == householdId).MemberIds.Single());
            Assert.Equal(4, resumedSetup.Society.Inhabitants.Count(item => item.Id.StartsWith("founder:", StringComparison.Ordinal)));
            Assert.Throws<ArgumentException>(() => resumedSetup.AddAgent(agentId, new GridPoint(5, 2)));
            Assert.True(resumedSetup.RenameAgent(agentId, "Nova"));
            Assert.False(resumedSetup.RenameAgent(agentId, "Nova"));
            Assert.Throws<ArgumentException>(() => resumedSetup.RenameAgent(agentId, "  "));
            file.Save(resumedSetup);
            using var reloaded = file.LoadOrCreate("new-camp");
            Assert.Equal(householdId, reloaded.Society.GetInhabitant(agentId).HouseholdId);
            Assert.Equal("Nova", reloaded.Society.GetInhabitant(agentId).Name);
            Assert.Equal(position, reloaded.Inhabitants.Single(item => item.InhabitantId == agentId).Position);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
