using UnitGenerator;

namespace SharpCampus.Shared.Identity;

/// <summary>
/// Identifies a SharpCampus account. Carries the value of the authentication provider's subject claim.
/// </summary>
[UnitOf(typeof(Guid), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.ParseMethod)]
public readonly partial struct UserId;
