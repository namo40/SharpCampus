using MasterMemory;
using MessagePack;
using SharpCampus.Shared.Missions;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// A daily objective: a statistic to reach a goal on, and what reaching it pays.
/// </summary>
[MemoryTable("missions")]
[MessagePackObject(true)]
public sealed record Mission : IValidatable<Mission>
{
    /// <summary>Primary key: the identifier progress is tracked under.</summary>
    [PrimaryKey] public MissionId MissionId { get; init; }

    /// <summary>Localization key of the display name.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Player statistic this mission counts.</summary>
    public MissionMetric Metric { get; init; }

    /// <summary>Value of <see cref="Metric"/> that completes the mission.</summary>
    public int Goal { get; init; }

    /// <summary>Coins paid out on completion.</summary>
    public Coins RewardCoins { get; init; }

    void IValidatable<Mission>.Validate(IValidator<Mission> validator)
    {
        validator.Validate(x => x.NameKey.Length > 0);
        validator.Validate(x => x.Goal >= 1);
        validator.Validate(x => x.RewardCoins.AsPrimitive() >= 0);
    }
}
