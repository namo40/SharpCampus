using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Bots;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class MatchmakingWorkerTests
{
    private const int SummonAfterSeconds = 30;

    private static readonly UserId _first = new(Guid.NewGuid());
    private static readonly UserId _second = new(Guid.NewGuid());
    private static readonly UserId _third = new(Guid.NewGuid());
    private static readonly UserId _fourth = new(Guid.NewGuid());

    private static readonly SkinId _classic = new("CLASSIC");
    private static readonly SkinId _mono = new("MONO");

    // The real data rather than a stand-in: which skin an account starts in is a master data answer.
    private static readonly MemoryDatabase _masterData = MasterDataLoader.Load("masterdata");

    private static readonly RoomServerEntry _server =
        new("room-0", "http://localhost:5002", "http://localhost:5002", 0, 100);

    private readonly IPairingLock _pairingLock = Substitute.For<IPairingLock>();
    private readonly IMatchQueue _queue = Substitute.For<IMatchQueue>();
    private readonly ITicketStore _tickets = Substitute.For<ITicketStore>();
    private readonly IActiveRoomStore _activeRooms = Substitute.For<IActiveRoomStore>();
    private readonly IRoomRegistry _registry = Substitute.For<IRoomRegistry>();
    private readonly IRoomControlClient _roomControl = Substitute.For<IRoomControlClient>();
    private readonly IBotSummoner _bots = Substitute.For<IBotSummoner>();
    private readonly IBotSummonCooldown _botCooldown = Substitute.For<IBotSummonCooldown>();
    private readonly IProfileRepository _profiles = Substitute.For<IProfileRepository>();
    private readonly List<UserId> _waiting = [];

    public MatchmakingWorkerTests()
    {
        _pairingLock.TryAcquireAsync().Returns(true);
        _registry.FindLeastLoadedAsync().Returns(_server);
        _roomControl.CreateRoomAsync(Arg.Any<string>(), Arg.Any<CreateRoomRequest>()).Returns(CreateRoomResult.Created);
        _bots.SummonAsync().Returns(SummonBotResult.Deployed);
        _profiles.GetAsync(_first).Returns(new Profile(_first, "alpha") { EquippedSkinId = _mono });
        _profiles.GetAsync(_second).Returns(new Profile(_second, "beta") { EquippedSkinId = _classic });

        // A queue that only gives up its members when the worker confirms the pairing, which is the
        // behaviour the whole peek-then-remove model rests on.
        _queue.PeekPairAsync().Returns(_ => _waiting.Count < 2 ? null : (_waiting[0], _waiting[1]));
        _queue.When(queue => queue.RemoveAsync(Arg.Any<UserId>()))
            .Do(call => _waiting.Remove(call.Arg<UserId>()));
    }

    [Fact]
    public async Task TwoWaitingPlayers_AreGivenARoomAndATicketEach()
    {
        QueueHolds(_first, _second);

        await CreateWorker().PairAsync();

        var request = (CreateRoomRequest)_roomControl.ReceivedCalls().Single().GetArguments()[1]!;
        Assert.Equal([_first, _second], request.Players.Select(player => player.UserId));
        Assert.Equal(["alpha", "beta"], request.Players.Select(player => player.DisplayName));

        // What each player equipped is fixed here, which is what makes it the same in a rematch.
        Assert.Equal([_mono, _classic], request.Players.Select(player => player.EquippedSkinId));

        await _tickets.Received(1).StoreAsync(_first, Arg.Is<MatchTicket>(ticket => ticket.RoomId == request.RoomId));
        await _tickets.Received(1).StoreAsync(_second, Arg.Is<MatchTicket>(ticket => ticket.RoomId == request.RoomId));

        Assert.Empty(_waiting);
    }

    [Fact]
    public async Task PlayerWhoNeverAskedTheApiServerForAnything_IsSeatedWithTheDefaultsAProfileWouldHave()
    {
        QueueHolds(_third, _fourth);

        await CreateWorker().PairAsync();

        var request = (CreateRoomRequest)_roomControl.ReceivedCalls().Single().GetArguments()[1]!;

        Assert.All(request.Players, player => Assert.StartsWith(NicknameRules.InitialPrefix, player.DisplayName));
        Assert.All(request.Players, player => Assert.Equal(_classic, player.EquippedSkinId));
    }

    [Fact]
    public async Task PairedPlayers_KeepTheirPlaceInTheQueueUntilBothTicketsExist()
    {
        QueueHolds(_first, _second);

        await CreateWorker().PairAsync();

        // A client polling between these calls has to find a ticket or a queue place, never neither.
        Received.InOrder(() =>
        {
            _tickets.StoreAsync(_first, Arg.Any<MatchTicket>());
            _tickets.StoreAsync(_second, Arg.Any<MatchTicket>());
            _queue.RemoveAsync(_first);
            _queue.RemoveAsync(_second);
        });
    }

    [Fact]
    public async Task Ticket_CarriesTheChosenServerAndATokenThatServerAccepts()
    {
        QueueHolds(_first, _second);

        await CreateWorker().PairAsync();

        var issued = StoredTickets().ToList();
        Assert.Equal([_first, _second], issued.Select(entry => entry.UserId));

        foreach (var (userId, ticket) in issued)
        {
            Assert.Equal(_server.ClientEndpoint, ticket.Endpoint);
            Assert.True(EntryTokens().TryValidate(ticket.EntryToken, out var signedUser, out var signedRoom));
            Assert.Equal(userId, signedUser);
            Assert.Equal(ticket.RoomId, signedRoom);
        }
    }

    [Fact]
    public async Task PairedPlayers_AreRecordedAsBeingInTheRoomTheyWereSentTo()
    {
        QueueHolds(_first, _second);

        await CreateWorker().PairAsync();

        var roomId = ((CreateRoomRequest)_roomControl.ReceivedCalls().Single().GetArguments()[1]!).RoomId;
        var expected = new ActiveRoom(roomId, _server.ClientEndpoint);

        // This is what a client that died mid-match is answered with when it queues again.
        await _activeRooms.Received(1).StoreAsync(_first, expected);
        await _activeRooms.Received(1).StoreAsync(_second, expected);
    }

    [Fact]
    public async Task EveryWaitingPair_IsPlacedInTheSamePass()
    {
        QueueHolds(_first, _second, _third, _fourth);

        await CreateWorker().PairAsync();

        Assert.Equal(2, _roomControl.ReceivedCalls().Count());
        Assert.Empty(_waiting);
    }

    [Fact]
    public async Task LonePlayer_StaysInTheQueue()
    {
        QueueHolds(_first);

        await CreateWorker().PairAsync();

        Assert.Equal([_first], _waiting);
        await _roomControl.DidNotReceive().CreateRoomAsync(Arg.Any<string>(), Arg.Any<CreateRoomRequest>());
    }

    [Fact]
    public async Task RoomServerThatRefuses_LeavesThePairQueuedForTheNextPass()
    {
        QueueHolds(_first, _second);
        _roomControl.CreateRoomAsync(Arg.Any<string>(), Arg.Any<CreateRoomRequest>())
            .Returns(new CreateRoomResult(CreateRoomOutcome.AtCapacity));

        await CreateWorker().PairAsync();

        Assert.Equal([_first, _second], _waiting);
        await _tickets.DidNotReceive().StoreAsync(Arg.Any<UserId>(), Arg.Any<MatchTicket>());

        // The pass gives up on the pair rather than retrying it: the timer is what paces the retry.
        Assert.Single(_roomControl.ReceivedCalls());
    }

    [Fact]
    public async Task UnreachableRoomServer_LeavesThePairQueuedForTheNextPass()
    {
        QueueHolds(_first, _second);
        _roomControl.CreateRoomAsync(Arg.Any<string>(), Arg.Any<CreateRoomRequest>()).Returns((CreateRoomResult?)null);

        await CreateWorker().PairAsync();

        Assert.Equal([_first, _second], _waiting);
        await _tickets.DidNotReceive().StoreAsync(Arg.Any<UserId>(), Arg.Any<MatchTicket>());
        Assert.Single(_roomControl.ReceivedCalls());
    }

    [Fact]
    public async Task NoRegisteredRoomServer_LeavesThePairQueuedForTheNextPass()
    {
        QueueHolds(_first, _second);
        _registry.FindLeastLoadedAsync().Returns((RoomServerEntry?)null);

        await CreateWorker().PairAsync();

        Assert.Equal([_first, _second], _waiting);
        Assert.Single(_registry.ReceivedCalls());
    }

    [Fact]
    public async Task AnotherInstanceHoldingTheLock_StopsThisOneFromTouchingTheQueue()
    {
        QueueHolds(_first, _second);
        _pairingLock.TryAcquireAsync().Returns(false);

        await CreateWorker().PairAsync();

        await _queue.DidNotReceive().PeekPairAsync();
        await _pairingLock.DidNotReceive().ReleaseAsync();
    }

    [Fact]
    public async Task PairingLock_IsReleasedOnceThePassIsDone()
    {
        QueueHolds(_first, _second);

        await CreateWorker().PairAsync();

        await _pairingLock.Received(1).ReleaseAsync();
    }

    [Fact]
    public async Task LonePlayerWhoHasNotWaitedLongEnough_IsLeftToTheQueue()
    {
        LoneEntry(_first, TimeSpan.FromSeconds(SummonAfterSeconds - 1));

        await CreateWorker().PairAsync();

        await _bots.DidNotReceive().SummonAsync();
    }

    [Fact]
    public async Task LonePlayerPastTheThreshold_DrawsABotAndStartsTheCooldown()
    {
        LoneEntry(_first, TimeSpan.FromSeconds(SummonAfterSeconds + 1));

        await CreateWorker().PairAsync();

        await _bots.Received(1).SummonAsync();
        await _botCooldown.Received(1).StartAsync();
    }

    [Fact]
    public async Task PlayersWhoStillHaveEachOther_DrawNoBot()
    {
        // Two waiting accounts are a pair the pass has already had its chance at, so the queue reports
        // no lone entry at all.
        QueueHolds(_first, _second);
        _roomControl.CreateRoomAsync(Arg.Any<string>(), Arg.Any<CreateRoomRequest>())
            .Returns(new CreateRoomResult(CreateRoomOutcome.AtCapacity));

        await CreateWorker().PairAsync();

        await _bots.DidNotReceive().SummonAsync();
    }

    [Fact]
    public async Task BotThatIsAlreadyOnItsWay_IsNotSentForAgain()
    {
        LoneEntry(_first, TimeSpan.FromSeconds(SummonAfterSeconds + 1));
        _botCooldown.IsActiveAsync().Returns(true);

        await CreateWorker().PairAsync();

        await _bots.DidNotReceive().SummonAsync();
    }

    [Fact]
    public async Task BotServerThatIsNotRunning_StartsTheCooldownAllTheSame()
    {
        LoneEntry(_first, TimeSpan.FromSeconds(SummonAfterSeconds + 1));
        _bots.SummonAsync().Returns((SummonBotResult?)null);

        await CreateWorker().PairAsync();

        // A bot server is optional, so the passes that follow stay quiet instead of asking for one every
        // half second. The pass itself finished all the same: a queue with nobody to pair still works.
        await _bots.Received(1).SummonAsync();
        await _botCooldown.Received(1).StartAsync();
    }

    [Fact]
    public async Task BotServerWithNoAccountLeft_StartsTheCooldownAllTheSame()
    {
        LoneEntry(_first, TimeSpan.FromSeconds(SummonAfterSeconds + 1));
        _bots.SummonAsync().Returns(SummonBotResult.Exhausted);

        await CreateWorker().PairAsync();

        await _botCooldown.Received(1).StartAsync();
    }

    private void QueueHolds(params UserId[] waiting) => _waiting.AddRange(waiting);

    private void LoneEntry(UserId userId, TimeSpan waited)
    {
        QueueHolds(userId);
        _queue.PeekLoneAsync().Returns((userId, waited));
    }

    private IEnumerable<(UserId UserId, MatchTicket Ticket)> StoredTickets() => _tickets.ReceivedCalls()
        .Where(call => call.GetMethodInfo().Name == nameof(ITicketStore.StoreAsync))
        .Select(call => ((UserId)call.GetArguments()[0]!, (MatchTicket)call.GetArguments()[1]!));

    private static EntryTokenService EntryTokens()
        => new(Options.Create(new EntryTokenOptions { Secret = "test-secret" }), TimeProvider.System);

    private MatchmakingWorker CreateWorker()
    {
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IProfileRepository)).Returns(_profiles);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(provider);

        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(scope);

        return new MatchmakingWorker(
            _pairingLock,
            _queue,
            _tickets,
            _activeRooms,
            _registry,
            _roomControl,
            _bots,
            _botCooldown,
            EntryTokens(),
            scopes,
            _masterData,
            Options.Create(new BotFallbackOptions { SummonAfterSeconds = SummonAfterSeconds }),
            NullLogger<MatchmakingWorker>.Instance);
    }
}
