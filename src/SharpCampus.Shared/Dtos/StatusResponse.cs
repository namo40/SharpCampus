using MessagePack;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Identity and liveness snapshot of a single SharpCampus server process.
/// </summary>
/// <param name="ServerName">Configured display name of the server that produced the response.</param>
/// <param name="Version">Informational version of the server assembly.</param>
/// <param name="TimestampUtc">UTC time at which the server produced the response.</param>
[MessagePackObject]
public sealed record StatusResponse(
    [property: Key(0)] string ServerName,
    [property: Key(1)] string Version,
    [property: Key(2)] DateTime TimestampUtc);
