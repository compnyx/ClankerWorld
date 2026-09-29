using ClankerWorld.Simulation.Harness;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    /// <summary>Accept or replace the starter footprint before any founder is placed.</summary>
    public FirstTownLayout AcceptFirstTownLayout(GridPoint roughSite)
    {
        gate.Wait();
        try
        {
            if (geographyOptions is null || founderSetup is not { Started: false, FounderIds.Count: 0 } ||
                !society.Checkpoint.IsPaused || WorldTick != 0 ||
                worldSimulation.ProductionJobs.Count != 0 || (worldSimulation.CropBuilds?.Count ?? 0) != 0)
                throw new InvalidOperationException("Choose the first Town layout during paused setup before placing founders.");
            var existing = worldSimulation.Buildings;
            if (existing.Any(building => !building.InstanceId.StartsWith("first-town-", StringComparison.Ordinal)) ||
                existing.Count is not (0 or 5))
                throw new InvalidOperationException("Other building work prevents replacing the initial layout.");
            var plan = FirstTownLayoutPlanner.Plan(map, roughSite)
                ?? throw new ArgumentException("No connected five-building layout fits near this rough site.", nameof(roughSite));
            var definitions = worldContent.Buildings.ToDictionary(item => item.CanonicalId, StringComparer.Ordinal);
            if (plan.Buildings.Any(building => !definitions.ContainsKey(building.DefinitionId)))
                throw new InvalidOperationException("The initial building definitions are not active.");

            var placed = plan.Buildings.Select(building => new PlacedBuilding(
                "first-town-" + building.Role, building.DefinitionId, building.Position, 0,
                TownBorderRules.FirstTownId,
                building.Role switch
                {
                    "house-a" => HouseholdId,
                    "house-b" => SecondHouseholdId,
                    _ => null,
                })).OrderBy(building => building.InstanceId, StringComparer.Ordinal).ToArray();
            var town = TownBorderRules.CreateFirstTown(map, originSite: roughSite);
            foreach (var building in placed)
            {
                var definition = definitions[building.DefinitionId];
                town = town with
                {
                    AssignedBuildingIds = town.AssignedBuildingIds.Append(building.InstanceId)
                        .Order(StringComparer.Ordinal).ToArray(),
                    BorderTiles = TownBorderRules.ExpandForBuilding(map, town, building.Position,
                        definition.Width, definition.Height),
                };
            }
            worldSimulation = WorldContentSimulationState.Empty with { Buildings = placed };
            towns = [town];
            roadTiles = plan.RoadTiles.ToHashSet();
            checkpointSchemaVersion = StateSchemaVersion;
            AppendEvent(existing.Count == 0 ? "first_town_layout_accepted" : "first_town_layout_redone",
                $"{roughSite.X},{roughSite.Y}:buildings:{placed.Length}:roads:{roadTiles.Count}");
            return plan;
        }
        finally { gate.Release(); }
    }
}
