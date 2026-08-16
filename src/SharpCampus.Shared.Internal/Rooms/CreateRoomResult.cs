using MessagePack;

namespace SharpCampus.Shared.Internal.Rooms;

/// <summary>
/// Whether a room server took the room.
/// </summary>
public enum CreateRoomOutcome : byte
{
    /// <summary>The room exists and is waiting for both players.</summary>
    Created = 0,

    /// <summary>The server is already running as many rooms as it accepts.</summary>
    AtCapacity = 1,

    /// <summary>The server already has a room under that identifier.</summary>
    AlreadyExists = 2,

    /// <summary>The server is shutting down and takes no further rooms.</summary>
    Draining = 3,
}

/// <summary>
/// Answer to a room creation request.
/// </summary>
/// <param name="Outcome">Whether the room was created, and why not when it was not.</param>
[MessagePackObject]
public sealed record CreateRoomResult([property: Key(0)] CreateRoomOutcome Outcome)
{
    /// <summary>The answer for a room that was stood up.</summary>
    public static CreateRoomResult Created { get; } = new(CreateRoomOutcome.Created);
}
