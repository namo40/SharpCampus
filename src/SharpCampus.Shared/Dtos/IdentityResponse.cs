using MessagePack;
using SharpCampus.Shared.Identity;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// The account the server resolved from the bearer token on the current call.
/// </summary>
/// <param name="UserId">Identifier of the authenticated account.</param>
/// <param name="Email">Email address the account signed up with.</param>
[MessagePackObject]
public sealed record IdentityResponse(
    [property: Key(0)] UserId UserId,
    [property: Key(1)] string Email);
