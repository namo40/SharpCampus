namespace SharpCampus.ApiServer.Matchmaking;

public interface IPairingLock
{
    // Only one ApiServer instance may pair at a time, or two of them would hand the same player to two rooms.
    Task<bool> TryAcquireAsync();

    Task ReleaseAsync();
}
