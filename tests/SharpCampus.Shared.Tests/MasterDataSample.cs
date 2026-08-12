using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Missions;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Tests;

// A small but complete dataset the validation tests break in one place at a time.
internal sealed class MasterDataSample
{
    public GameConfig GameConfig { get; set; } = new()
    {
        Id = GameConfig.SingleRowId,
        TickRate = 20,
        LockDelayTicks = 10,
        LockResetMax = 15,
        NextCount = 5,
        InputPerTickMax = 10,
        GarbageCapPerLock = 8,
        GraceTicks = 200,
        CountdownSec = 3,
        RematchTimeoutSec = 10,
        JoinTimeoutSec = 30,
        LevelUpIntervalSec = 30,
        MaxLevel = 3,
    };

    public List<GravityCurve> GravityCurve { get; set; } =
    [
        new() { Level = 1, TicksPerCell = 20 },
        new() { Level = 2, TicksPerCell = 18 },
        new() { Level = 3, TicksPerCell = 15 },
    ];

    public List<AttackTable> AttackTable { get; set; } =
    [
        new() { LinesCleared = 1, Garbage = 0 },
        new() { LinesCleared = 2, Garbage = 1 },
        new() { LinesCleared = 3, Garbage = 2 },
        new() { LinesCleared = 4, Garbage = 4 },
    ];

    public List<ComboTable> ComboTable { get; set; } =
    [
        new() { ComboMin = 2, ComboMax = 3, Bonus = 1 },
        new() { ComboMin = 4, ComboMax = 5, Bonus = 2 },
        new() { ComboMin = 6, ComboMax = 99, Bonus = 3 },
    ];

    public List<Skin> Skins { get; set; } =
    [
        NewSkin("CLASSIC", 0),
        NewSkin("MONO", 200),
        NewSkin("RETRO", 400),
    ];

    public List<Mission> Missions { get; set; } =
    [
        new()
        {
            MissionId = new MissionId("PLAY_3"),
            NameKey = "mission.play_3.name",
            Metric = MissionMetric.MatchesPlayed,
            Goal = 3,
            RewardCoins = new Coins(30),
        },
        new()
        {
            MissionId = new MissionId("WIN_1"),
            NameKey = "mission.win_1.name",
            Metric = MissionMetric.Wins,
            Goal = 1,
            RewardCoins = new Coins(50),
        },
    ];

    public List<Economy> Economy { get; set; } =
    [
        new()
        {
            Id = GameConfig.SingleRowId,
            WinCoins = new Coins(100),
            LoseCoins = new Coins(30),
            RatingK = 32,
            RatingInitial = new Rating(1000),
        },
    ];

    public static Skin NewSkin(string skinId, int price) => new()
    {
        SkinId = new SkinId(skinId),
        NameKey = $"skin.{skinId.ToLowerInvariant()}.name",
        Price = new Coins(price),
        BlockGlyph = "[]",
        ColorI = "Cyan",
        ColorO = "Yellow",
        ColorT = "Magenta",
        ColorS = "Green",
        ColorZ = "Red",
        ColorJ = "Blue",
        ColorL = "DarkYellow",
        ColorGarbage = "DarkGray",
    };

    public byte[] Build()
    {
        var builder = new DatabaseBuilder();
        builder.Append([GameConfig]);
        builder.Append(GravityCurve);
        builder.Append(AttackTable);
        builder.Append(ComboTable);
        builder.Append(Skins);
        builder.Append(Missions);
        builder.Append(Economy);
        return builder.Build();
    }

    public MemoryDatabase ToDatabase() => new(Build());
}
