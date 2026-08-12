using NSubstitute;
using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class MatchmakingServiceTests
{
    private static readonly UserId _user = new(Guid.NewGuid());

    private readonly IMatchQueue _queue = Substitute.For<IMatchQueue>();
    private readonly ITicketStore _tickets = Substitute.For<ITicketStore>();

    [Fact]
    public async Task Status_IsNoneBeforeAnythingHappens()
    {
        var status = await CreateService().GetStatusAsync();

        Assert.Equal(MatchQueueState.None, status.State);
        Assert.Null(status.Ticket);
    }

    [Fact]
    public async Task Enqueue_PutsTheCallerInTheQueue()
    {
        var status = await CreateService().EnqueueAsync();

        Assert.Equal(MatchQueueState.Queued, status.State);
        await _queue.Received(1).EnqueueAsync(_user);
    }

    [Fact]
    public async Task Status_IsQueuedWhileWaiting()
    {
        _queue.ContainsAsync(_user).Returns(true);

        Assert.Equal(MatchQueueState.Queued, (await CreateService().GetStatusAsync()).State);
    }

    [Fact]
    public async Task Status_CarriesTheTicketOnceTheMatchIsMade()
    {
        var ticket = new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "token");
        _tickets.GetAsync(_user).Returns(ticket);

        var status = await CreateService().GetStatusAsync();

        Assert.Equal(MatchQueueState.Matched, status.State);
        Assert.Equal(ticket, status.Ticket);
    }

    [Fact]
    public async Task Enqueue_AfterAMatchReturnsTheTicketRatherThanRequeueing()
    {
        var ticket = new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "token");
        _tickets.GetAsync(_user).Returns(ticket);

        var status = await CreateService().EnqueueAsync();

        Assert.Equal(MatchQueueState.Matched, status.State);
        Assert.Equal(ticket, status.Ticket);
        await _queue.DidNotReceive().EnqueueAsync(Arg.Any<UserId>());
    }

    [Fact]
    public async Task Enqueue_IsIdempotentWhileQueued()
    {
        var service = CreateService();

        await service.EnqueueAsync();
        var second = await service.EnqueueAsync();

        Assert.Equal(MatchQueueState.Queued, second.State);
        await _queue.Received(2).EnqueueAsync(_user);
    }

    [Fact]
    public async Task Cancel_TakesTheCallerOutOfTheQueue()
    {
        await CreateService().CancelAsync();

        await _queue.Received(1).RemoveAsync(_user);
    }

    private MatchmakingService CreateService()
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(_user);

        return new MatchmakingService(userContext, _queue, _tickets);
    }
}
