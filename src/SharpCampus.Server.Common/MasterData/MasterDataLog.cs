using Microsoft.Extensions.Logging;
using SharpCampus.Shared.MasterData;
using ZLogger;

namespace SharpCampus.Server.Common.MasterData;

public static partial class MasterDataLog
{
    public static void LogMasterData(this ILogger logger, MemoryDatabase database)
        => MasterDataLoaded(
            logger,
            database.GameConfigTable.All[0].TickRate,
            database.GravityCurveTable.FindByLevel(1).TicksPerCell,
            database.GravityCurveTable.Count,
            database.AttackTableTable.Count,
            database.ComboTableTable.Count,
            database.SkinTable.Count,
            database.MissionTable.Count);

    [ZLoggerMessage(
        LogLevel.Information,
        "Master data loaded: tickRate {tickRate}, gravity Lv1 {gravityLevel1TicksPerCell} ticks/cell, "
        + "{gravityRows} gravity rows, {attackRows} attack rows, {comboRows} combo rows, {skinRows} skins, {missionRows} missions")]
    private static partial void MasterDataLoaded(
        ILogger logger,
        int tickRate,
        int gravityLevel1TicksPerCell,
        int gravityRows,
        int attackRows,
        int comboRows,
        int skinRows,
        int missionRows);
}
