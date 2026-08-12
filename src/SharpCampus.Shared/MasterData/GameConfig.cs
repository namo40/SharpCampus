using MasterMemory;
using MessagePack;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// The single row of match-wide constants the simulation runs on.
/// </summary>
// The MasterMemory generator reads [PrimaryKey] off property declarations, so the table records
// spell their columns out instead of taking a positional parameter list.
[MemoryTable("game_config")]
[MessagePackObject(true)]
public sealed record GameConfig : IValidatable<GameConfig>
{
    // MasterMemory indexes every table, so the single-row tables carry a constant key.
    /// <summary>Primary key every single-row table uses.</summary>
    public const int SingleRowId = 1;

    /// <summary>Primary key, always <see cref="SingleRowId"/>.</summary>
    [PrimaryKey] public int Id { get; init; }

    /// <summary>Simulation ticks per second.</summary>
    public int TickRate { get; init; }

    /// <summary>Ticks a piece rests on the stack before it locks.</summary>
    public int LockDelayTicks { get; init; }

    /// <summary>How often a move may restart the lock delay before it stops resetting.</summary>
    public int LockResetMax { get; init; }

    /// <summary>Upcoming pieces shown to the player.</summary>
    public int NextCount { get; init; }

    /// <summary>Inputs a client may have applied within one tick.</summary>
    public int InputPerTickMax { get; init; }

    /// <summary>Garbage rows a single lock can send.</summary>
    public int GarbageCapPerLock { get; init; }

    /// <summary>Ticks a disconnected player keeps their seat.</summary>
    public int GraceTicks { get; init; }

    /// <summary>Seconds counted down before a match starts.</summary>
    public int CountdownSec { get; init; }

    /// <summary>Seconds a rematch offer stays open.</summary>
    public int RematchTimeoutSec { get; init; }

    /// <summary>Seconds a matched player has to join their room.</summary>
    public int JoinTimeoutSec { get; init; }

    /// <summary>Seconds of play between level increases.</summary>
    public int LevelUpIntervalSec { get; init; }

    /// <summary>Highest level the gravity curve has to cover.</summary>
    public int MaxLevel { get; init; }

    void IValidatable<GameConfig>.Validate(IValidator<GameConfig> validator)
    {
        validator.Validate(x => x.Id == SingleRowId);
        validator.Validate(x => x.TickRate > 0);
        validator.Validate(x => x.LockDelayTicks > 0);
        validator.Validate(x => x.LockResetMax > 0);
        validator.Validate(x => x.NextCount > 0);
        validator.Validate(x => x.InputPerTickMax > 0);
        validator.Validate(x => x.GarbageCapPerLock > 0);
        validator.Validate(x => x.GraceTicks > 0);
        validator.Validate(x => x.CountdownSec > 0);
        validator.Validate(x => x.RematchTimeoutSec > 0);
        validator.Validate(x => x.JoinTimeoutSec > 0);
        validator.Validate(x => x.LevelUpIntervalSec > 0);
        validator.Validate(x => x.MaxLevel > 0);

        if (!validator.CallOnce())
        {
            return;
        }

        var rows = validator.GetTableSet().TableData.Count;
        if (rows != 1)
        {
            validator.Fail($"expected exactly one row, found {rows}.");
        }
    }
}
