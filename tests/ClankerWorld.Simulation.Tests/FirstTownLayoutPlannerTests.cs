using ClankerWorld.Simulation.Harness;
using ClankerWorld.Simulation.Playtest;
using ClankerWorld.Simulation.World;

namespace ClankerWorld.Simulation.Tests;

public sealed class FirstTownLayoutPlannerTests
{
    [Theory]
    [InlineData("starter-layout-one")]
    [InlineData("starter-layout-two")]
    public void StartingPlanHasFiveLegalBuildingsAndAConnectedRoadNetwork(string seed)
    {
        var map = GeneratedCampMapGenerator.Generate(new GeographyOptions(seed, WorldSizePreset.Small));
        var roughSite = map.GetObject("storage").Position;
        var plan = FirstTownLayoutPlanner.Plan(map, roughSite);
        Assert.NotNull(plan);
        var repeated = FirstTownLayoutPlanner.Plan(map, roughSite);
        Assert.NotNull(repeated);
        Assert.Equal(plan.Buildings, repeated.Buildings);
        Assert.Equal(plan.RoadTiles, repeated.RoadTiles);
        Assert.Equal(["warehouse", "house-a", "house-b", "farmhouse", "blacksmith"],
            plan.Buildings.Select(building => building.Role).ToArray());

        var occupied = map.CampObjects.Select(item => item.Position)
            .Concat(map.Resources.Select(item => item.Position)).ToHashSet();
        foreach (var building in plan.Buildings)
        {
            for (var dy = 0; dy < building.Height; dy++)
            for (var dx = 0; dx < building.Width; dx++)
            {
                var tile = new GridPoint(building.Position.X + dx, building.Position.Y + dy);
                Assert.True(map.IsBuildable(tile));
                Assert.True(occupied.Add(tile));
            }
        }

        var roads = plan.RoadTiles.ToHashSet();
        var reachable = new HashSet<GridPoint> { plan.Buildings[0].Position };
        var pending = new Queue<GridPoint>();
        pending.Enqueue(plan.Buildings[0].Position);
        while (pending.TryDequeue(out var current))
        {
            foreach (var next in map.FootNeighbors(current).Where(roads.Contains))
                if (reachable.Add(next)) pending.Enqueue(next);
        }
        Assert.All(plan.Buildings, building => Assert.Contains(building.Position, reachable));
        Assert.All(plan.RoadTiles, point => Assert.True(map.IsBuildable(point)));
    }
}
