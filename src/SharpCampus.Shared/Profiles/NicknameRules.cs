using SharpCampus.Shared.Identity;

namespace SharpCampus.Shared.Profiles;

/// <summary>
/// What a nickname may look like, and the nickname a brand new profile starts with.
/// Lives in the shared assembly so the client can reject bad input without a round trip.
/// </summary>
public static class NicknameRules
{
    /// <summary>Shortest accepted nickname.</summary>
    public const int MinLength = 2;

    /// <summary>Longest accepted nickname.</summary>
    public const int MaxLength = 16;

    /// <summary>Prefix of every server-assigned nickname.</summary>
    public const string InitialPrefix = "player_";

    private const int InitialSuffixLength = 8;

    /// <summary>
    /// Tests a nickname against the accepted shape: <c>^[A-Za-z0-9_]{2,16}$</c>.
    /// ASCII only, because the console board renders in a fixed-width grid.
    /// </summary>
    /// <param name="nickname">Candidate nickname.</param>
    /// <returns><see langword="true"/> when the nickname may be stored as is.</returns>
    public static bool IsValid(string? nickname)
    {
        if (nickname is null || nickname.Length < MinLength || nickname.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in nickname)
        {
            if (!IsAllowed(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the nickname a profile is created with, derived from the account identifier so it is stable.
    /// </summary>
    /// <param name="userId">Account the profile belongs to.</param>
    /// <returns>A nickname that satisfies <see cref="IsValid"/>.</returns>
    public static string CreateInitial(UserId userId) =>
        // Production note: two accounts can share these hex digits, which the unique index would reject on insert.
        // A real service would retry with a longer suffix instead of failing the first profile read.
        InitialPrefix + userId.AsPrimitive().ToString("N").Substring(0, InitialSuffixLength);

    private static bool IsAllowed(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
}
