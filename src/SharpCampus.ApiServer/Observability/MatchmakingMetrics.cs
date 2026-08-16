using System.Diagnostics.Metrics;

namespace SharpCampus.ApiServer.Observability;

public sealed class MatchmakingMetrics
{
    public const string MeterName = "SharpCampus.ApiServer";

    public const string QueueDepthName = "sharpcampus.matchmaking.queue.depth";

    private long _queueDepth;

    public MatchmakingMetrics(IMeterFactory meters)
        => meters.Create(MeterName).CreateObservableGauge(QueueDepthName, () => Volatile.Read(ref _queueDepth));

    // The gauge is read by whoever scrapes, and a callback that went to Redis would put that caller's
    // latency inside a metric. The pairing worker leaves the last depth it saw here instead.
    public void ReportQueueDepth(long depth) => Volatile.Write(ref _queueDepth, depth);
}
