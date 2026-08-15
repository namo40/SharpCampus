using MessagePack;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

// RoomId wraps a Ulid, which the standard resolver has no formatter for, so everything carrying one
// only travels under the shared contract options.
public class MatchmakingSerializationTests
{
    [Fact]
    public void RoomId_RoundTrips()
    {
        var roomId = new RoomId(Ulid.NewUlid());

        Assert.Equal(roomId, Roundtrip(roomId));
    }

    [Fact]
    public void MatchTicket_RoundTrips()
    {
        var ticket = new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "payload.signature");

        Assert.Equal(ticket, Roundtrip(ticket));
    }

    [Fact]
    public void MatchStatusResponse_RoundTripsWithoutATicket()
        => Assert.Equal(MatchStatusResponse.Waiting, Roundtrip(MatchStatusResponse.Waiting));

    [Fact]
    public void MatchStatusResponse_RoundTripsWithATicket()
    {
        var status = new MatchStatusResponse(
            MatchQueueState.Matched,
            new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "token"));

        Assert.Equal(status, Roundtrip(status));
    }

    [Fact]
    public void CreateRoomRequest_RoundTrips()
    {
        var request = new CreateRoomRequest(
            new RoomId(Ulid.NewUlid()),
            [
                new RoomPlayer(new UserId(Guid.NewGuid()), "alpha", new SkinId("CLASSIC")),
                new RoomPlayer(new UserId(Guid.NewGuid()), "beta", new SkinId("MONO")),
            ]);

        var restored = Roundtrip(request);

        Assert.Equal(request.RoomId, restored.RoomId);
        Assert.Equal(request.Players, restored.Players);
    }

    [Fact]
    public void CreateRoomResult_RoundTrips()
        => Assert.Equal(CreateRoomResult.Created, Roundtrip(CreateRoomResult.Created));

    private static T Roundtrip<T>(T value)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var bytes = MessagePackSerializer.Serialize(value, ContractSerialization.Options, cancellationToken);

        return MessagePackSerializer.Deserialize<T>(bytes, ContractSerialization.Options, cancellationToken);
    }
}
