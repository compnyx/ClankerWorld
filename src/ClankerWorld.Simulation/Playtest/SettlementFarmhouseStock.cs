using ClankerWorld.Simulation.Cognition;
using ClankerWorld.Simulation.Kernel;

namespace ClankerWorld.Simulation.Playtest;

public sealed partial class PrivateWorldRuntime
{
    private PlacedBuilding? FarmhouseForHousehold(string householdId) => worldSimulation.Buildings
        .Where(building => building.HouseholdId == householdId &&
            worldContent.Buildings.Any(definition => definition.CanonicalId == building.DefinitionId &&
                definition.Tags.Contains("farmhouse", StringComparer.Ordinal)))
        .OrderBy(building => building.InstanceId, StringComparer.Ordinal).FirstOrDefault();

    private InventoryLot? FarmGrainForDelivery(string householdId, string farmhouseId) =>
        society.Checkpoint.Inventory.Lots
            .Where(lot => lot.OwnerId == householdId && lot.ItemKind == "grain" &&
                lot.StorageBuildingId != farmhouseId && AvailableLotQuantity(lot) > 0)
            .OrderBy(lot => lot.Id, StringComparer.Ordinal).FirstOrDefault();

    private void AddFarmGrainCandidate(List<CognitionCandidate> candidates, string actor,
        PlaytestInhabitantState state)
    {
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null || CarriedHouseDelivery(actor) is not null ||
            FarmhouseForHousehold(householdId) is not { } farmhouse ||
            FarmGrainForDelivery(householdId, farmhouse.InstanceId) is not { } grain)
            return;
        var source = HouseholdStockPosition(grain);
        var range = HouseholdStockInteractionRange(grain);
        if ((!IsWithinInteractionRange(state.Position, source, range) &&
             FindUnoccupiedRoute(actor, state.Position, source, range).Count == 0) ||
            FindUnoccupiedRoute(actor, source, farmhouse.Position, 0).Count == 0)
            return;
        candidates.Add(new CognitionCandidate("haul_farm_grain",
            "Carry household grain to its Farmhouse for on-site processing.", 24, farmhouse.InstanceId));
    }

    private void HaulFarmGrain(string actor, PlaytestInhabitantState state)
    {
        var householdId = society.Checkpoint.GetInhabitant(actor).HouseholdId;
        if (!AdultResident(actor) || householdId is null ||
            FarmhouseForHousehold(householdId) is not { } farmhouse ||
            FarmGrainForDelivery(householdId, farmhouse.InstanceId) is not { } grain)
            return;
        var source = HouseholdStockPosition(grain);
        var range = HouseholdStockInteractionRange(grain);
        if (!IsWithinInteractionRange(state.Position, source, range))
        {
            MoveToward(actor, state, source, "farm_grain", range);
            return;
        }
        var quantity = Math.Min(HouseHaulLoadQuantity, AvailableLotQuantity(grain));
        ApplyInventoryTransition(inventory => InventoryFixture.Transfer(inventory,
            $"farm-grain-pickup:{WorldTick}:{actor}", householdId, actor, grain.Id,
            quantity, "farm_grain_picked_up", destinationDeliveryBuildingId: farmhouse.InstanceId));
        AppendEvent("farm_grain_picked_up", $"{actor}:{grain.Id}:{quantity}:{farmhouse.InstanceId}");
    }
}
