using MessagePack;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public class DuelContractSerializationTests
{
    [Fact]
    public void JoinRoomRequest_RoundTrips()
        => AssertRoundTrip(new JoinRoomRequest(new RoomId(Ulid.NewUlid()), "payload.signature"));

    [Fact]
    public void JoinRoomResult_RoundTrips() => AssertRoundTrip(new JoinRoomResult(true, new PlayerIndex(1), false));

    [Fact]
    public void JoinRoomResult_RoundTripsWithoutASeat() => AssertRoundTrip(JoinRoomResult.Rejected);

    [Fact]
    public void MatchStartInfo_RoundTrips()
    {
        var info = new MatchStartInfo(
            [
                new MatchPlayerInfo(new PlayerIndex(0), "a", MasterDataSample.NewSkin("CLASSIC", 0)),
                new MatchPlayerInfo(new PlayerIndex(1), "b", MasterDataSample.NewSkin("MONO", 200)),
            ],
            60,
            5);

        var restored = Roundtrip(info);

        Assert.Equal(info.Players, restored.Players);
        Assert.Equal(info.CountdownTicks, restored.CountdownTicks);
        Assert.Equal(info.NextCount, restored.NextCount);
    }

    [Fact]
    public void MatchResult_RoundTrips()
        => AssertRoundTrip(new MatchResult(DuelOutcome.Player1Wins, new PlayerIndex(0), MatchEndReason.Disconnect));

    [Fact]
    public void MatchResult_RoundTripsADrawWithoutAWinner()
        => AssertRoundTrip(new MatchResult(DuelOutcome.Draw, null, MatchEndReason.TopOut));

    [Fact]
    public void TickDelta_RoundTrips()
    {
        var delta = new TickDelta(
            new TickNumber(412),
            [
                new PlayerDelta(new PlayerIndex(0), true, PieceKind.S, Rotation.Left, -1, 20, true, 3, 2, 4),
                new PlayerDelta(new PlayerIndex(1), false, default, default, 0, 0, false, 0, 0, 4),
            ],
            [
                DuelEvent.From(TickEvent.PieceLocked(0, PieceKind.I, Rotation.Right, -2, 3)),
                DuelEvent.From(TickEvent.LinesCleared(0, 2, 3)),
            ]);

        var restored = Roundtrip(delta);

        Assert.Equal(delta.Tick, restored.Tick);
        Assert.Equal(delta.Players, restored.Players);
        Assert.Equal(delta.Events, restored.Events);
        Assert.Equal(-2, restored.Events[0].ToTickEvent().LockedX);
    }

    [Fact]
    public void DuelSnapshot_RoundTrips()
    {
        var cells = new byte[DuelReplicaBoard.Width * DuelReplicaBoard.Height];
        cells[0] = (byte)CellKind.Garbage;

        var snapshot = new DuelSnapshot(
            new TickNumber(9),
            [
                new PlayerSnapshot(new PlayerIndex(0), cells, [PieceKind.I, PieceKind.J], true, PieceKind.O, false, true, PieceKind.T, Rotation.Half, 3, 18, false, 4, 1, 2, 180, false),
                new PlayerSnapshot(new PlayerIndex(1), new byte[cells.Length], [], false, default, true, false, default, default, 0, 0, false, 0, 0, 2, 180, true),
            ]);

        var restored = Roundtrip(snapshot);

        Assert.Equal(snapshot.Tick, restored.Tick);
        Assert.Equal(snapshot.Players[0].Cells, restored.Players[0].Cells);
        Assert.Equal(snapshot.Players[0].Next, restored.Players[0].Next);
        Assert.Equal(snapshot.Players[1].ToppedOut, restored.Players[1].ToppedOut);
    }

    private static void AssertRoundTrip<T>(T value) => Assert.Equal(value, Roundtrip(value));

    // The contract options, not the standard ones: RoomId wraps a Ulid the standard resolver cannot format.
    private static T Roundtrip<T>(T value) => MessagePackSerializer.Deserialize<T>(
        MessagePackSerializer.Serialize(value, ContractSerialization.Options),
        ContractSerialization.Options);
}
