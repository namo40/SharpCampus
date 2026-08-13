namespace SharpCampus.Cli.Duel;

// Connection facts the boards do not carry. Written by the hub's receive loop and its heartbeat
// callback, read by the render loop, so every field crosses a thread boundary on its own.
internal sealed class DuelStatus
{
    private readonly int[] _connected = [1, 1];

    private volatile int _pingMilliseconds = -1;

    // Negative until the first heartbeat comes back.
    public int PingMilliseconds => _pingMilliseconds;

    public void Measure(TimeSpan roundTrip) => _pingMilliseconds = (int)roundTrip.TotalMilliseconds;

    public bool IsConnected(int seat) => Volatile.Read(ref _connected[seat]) == 1;

    public void SetConnected(int seat, bool connected) => Volatile.Write(ref _connected[seat], connected ? 1 : 0);
}
