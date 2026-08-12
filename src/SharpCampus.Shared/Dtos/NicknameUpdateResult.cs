namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Outcome of a nickname change.
/// </summary>
public enum NicknameUpdateResult
{
    /// <summary>The profile now carries the requested nickname.</summary>
    Updated,

    /// <summary>Another account already holds the nickname, compared case-insensitively.</summary>
    Duplicate,

    /// <summary>The nickname does not satisfy <see cref="Profiles.NicknameRules"/>.</summary>
    Invalid,
}
