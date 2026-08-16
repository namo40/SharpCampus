using Cysharp.Threading;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using SharpCampus.RoomServer.Observability;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class RoomMetricsTests
{
    [Fact]
    public void MeasuredTicks_LandInTheHistogram()
    {
        var meters = RoomFixture.Meters();
        var metrics = new RoomMetrics(meters);
        using var collector = new MetricCollector<double>(meters, RoomMetrics.MeterName, RoomMetrics.TickDurationName);

        metrics.RecordTick(0.4);
        metrics.RecordTick(1.25);

        Assert.Equal("ms", collector.Instrument?.Unit);
        Assert.Equal([0.4, 1.25], collector.GetMeasurementSnapshot().Select(measurement => measurement.Value));
    }

    [Fact]
    public async Task EveryTickOfARegisteredRoom_IsMeasured()
    {
        using var pool = new ManualLogicLooperPool(20);
        var meters = RoomFixture.Meters();
        var metrics = new RoomMetrics(meters);
        using var collector = new MetricCollector<double>(meters, RoomMetrics.MeterName, RoomMetrics.TickDurationName);

        await CreateManager(pool, metrics).CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());
        pool.Tick(2);

        Assert.Equal(2, collector.GetMeasurementSnapshot().Count);
        Assert.All(collector.GetMeasurementSnapshot(), measurement => Assert.True(measurement.Value >= 0));
    }

    [Fact]
    public void RoomsGauge_ReadsZeroUntilSomethingCountsRooms()
    {
        var meters = RoomFixture.Meters();

        // The meter is built before the room manager is, so the gauge has to answer with nothing yet.
        _ = new RoomMetrics(meters);
        using var collector = new MetricCollector<int>(meters, RoomMetrics.MeterName, RoomMetrics.ActiveRoomsName);

        collector.RecordObservableInstruments();

        Assert.Equal(0, collector.LastMeasurement?.Value);
    }

    [Fact]
    public async Task RoomsGauge_ReadsWhoeverKeepsTheRooms()
    {
        using var pool = new ManualLogicLooperPool(20);
        var meters = RoomFixture.Meters();
        var metrics = new RoomMetrics(meters);
        var manager = CreateManager(pool, metrics);
        using var collector = new MetricCollector<int>(meters, RoomMetrics.MeterName, RoomMetrics.ActiveRoomsName);

        metrics.TrackRooms(() => manager.RoomCount);
        await manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());
        collector.RecordObservableInstruments();

        Assert.Equal(1, collector.LastMeasurement?.Value);
    }

    private static RoomManager CreateManager(ILogicLooperPool pool, RoomMetrics metrics) => new(
        pool,
        RoomFixture.Rules(),
        RoomFixture.MasterData(),
        RoomFixture.ActiveRooms(),
        RoomFixture.Locations(),
        RoomFixture.Options(),
        RoomFixture.Publisher(),
        metrics,
        NullLogger<RoomManager>.Instance);
}
