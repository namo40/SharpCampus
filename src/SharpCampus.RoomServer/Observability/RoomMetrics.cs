using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace SharpCampus.RoomServer.Observability;

public sealed class RoomMetrics
{
    public const string MeterName = "SharpCampus.RoomServer";

    public const string TickDurationName = "sharpcampus.room.tick.duration";

    public const string ActiveRoomsName = "sharpcampus.rooms.active";

    private readonly Histogram<double> _tickDuration;

    // Rooms exist only once a match is placed here, so the gauge has to have an answer before anything
    // registers one.
    private Func<int>? _roomCount;

    public RoomMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _tickDuration = meter.CreateHistogram<double>(TickDurationName, "ms");
        meter.CreateObservableGauge(ActiveRoomsName, () => _roomCount?.Invoke() ?? 0);
    }

    // Called on a loop thread once per room per tick, so it must not allocate or wait for anything.
    public void RecordTick(double milliseconds) => _tickDuration.Record(milliseconds);

    public void TrackRooms(Func<int> count) => _roomCount = count;

    // A tick that fits its budget is the normal case, and the default buckets put every one of those in
    // the same bucket: at 20 ticks a second the budget is 50 ms and a room spends well under 1 ms of it.
    public static void ConfigureViews(MeterProviderBuilder metrics) => metrics.AddView(
        TickDurationName,
        new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 25, 50, 100],
        });
}
