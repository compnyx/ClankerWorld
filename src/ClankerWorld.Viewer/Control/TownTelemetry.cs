using Microsoft.Extensions.Logging;

namespace ClankerWorld.Viewer.Control;

internal enum TownTransitionKind
{
    StateLoaded,
    FoundingStarted,
    ResidentJoined,
    ResidentLeft,
    ResidentUnaffiliated,
    Founded,
    BuildingAssigned,
    BuildingUnassigned,
    BorderExpanded,
}

/// <summary>Bounded operational outcomes for authoritative Town state changes.</summary>
internal static partial class TownTelemetry
{
    public static void Transition(ILogger logger, long worldTick, string townId,
        TownTransitionKind transition, int residentCount, int buildingCount, int borderTileCount)
    {
        LogTownTransition(logger, worldTick, townId, transition, residentCount, buildingCount, borderTileCount);
    }

    [LoggerMessage(EventId = 2265, Level = LogLevel.Information,
        Message = "town_transition tick={WorldTick} town={TownId} transition={Transition} residents={ResidentCount} buildings={BuildingCount} border_tiles={BorderTileCount}")]
    private static partial void LogTownTransition(ILogger logger, long worldTick, string townId, TownTransitionKind transition,
        int residentCount, int buildingCount, int borderTileCount);
}
