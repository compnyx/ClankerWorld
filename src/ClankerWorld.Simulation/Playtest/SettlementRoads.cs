using ClankerWorld.Simulation.Content;
using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private const int MaximumLandRoadSearchTiles = 32_768;

    private int RoadStepCost(GridPoint from, GridPoint to)
    {
        var cost = map.FootStepCost(from, to);
        // One ground tile per tick is already the movement floor. This
        // provisional factor removes diagonal wait ticks and biases routing
        // toward an existing Road without making illness delays disappear.
        return roadTiles.Contains(from) && roadTiles.Contains(to)
            ? Math.Max(1, cost * 70 / 100) : cost;
    }

    private void GenerateRoadToBuilding(PlacedBuilding building)
    {
        if (building.TownId is null) return;
        var target = building.Position;
        var network = roadTiles.Count > 0
            ? roadTiles
            : new HashSet<GridPoint> { map.GetObject("storage").Position };
        var occupied = map.Resources.Select(item => item.Position)
            .Concat(map.CampObjects.Where(item => item.Id != "storage").Select(item => item.Position))
            .Concat(worldSimulation.Buildings.Where(item => item.InstanceId != building.InstanceId)
                .SelectMany(item =>
                {
                    var design = worldContent.Buildings.Single(value => value.CanonicalId == item.DefinitionId);
                    return WorldContentSimulationRules.Footprint(design, item.Position);
                }))
            .ToHashSet();
        var open = new PriorityQueue<GridPoint, (int Cost, int Y, int X, int Order)>();
        var best = new Dictionary<GridPoint, int> { [target] = 0 };
        var predecessor = new Dictionary<GridPoint, GridPoint>();
        var order = 0;
        open.Enqueue(target, (0, target.Y, target.X, order++));

        while (open.TryDequeue(out var current, out var priority) && best.Count <= MaximumLandRoadSearchTiles)
        {
            if (priority.Cost != best[current]) continue;
            if (network.Contains(current))
            {
                var added = 0;
                while (true)
                {
                    if (roadTiles.Add(current)) added++;
                    if (current == target) break;
                    current = predecessor[current];
                }
                if (added > 0)
                    AppendEvent("town_road_generated", $"{building.TownId}:{building.InstanceId}:tiles:{added}");
                return;
            }

            foreach (var next in map.FootNeighbors(current))
            {
                if (!map.IsBuildable(next) || occupied.Contains(next) && !network.Contains(next) ||
                    map.IsDiagonalFootStep(current, next) &&
                    (!map.IsBuildable(new GridPoint(next.X, current.Y)) ||
                     !map.IsBuildable(new GridPoint(current.X, next.Y))))
                    continue;
                var cost = checked(priority.Cost + map.FootStepCost(current, next));
                if (best.TryGetValue(next, out var previous) && previous <= cost) continue;
                best[next] = cost;
                predecessor[next] = current;
                open.Enqueue(next, (cost, next.Y, next.X, order++));
            }
        }
        AppendEvent("town_road_unconnected", $"{building.TownId}:{building.InstanceId}:land_route_unavailable");
    }

    private static void ValidateRoads(IReadOnlyList<GridPoint> roads, SeededMap map, FounderSetupState? setup)
    {
        if (roads.Count == 0) return;
        if (setup is null || roads.Distinct().Count() != roads.Count ||
            roads.Any(point => !map.IsBuildable(point)))
            throw new InvalidDataException("Saved Roads contain duplicate, invalid, or unaffiliated ground tiles.");
    }
}
