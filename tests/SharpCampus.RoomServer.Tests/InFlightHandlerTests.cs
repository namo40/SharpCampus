using MessagePipe;
using SharpCampus.RoomServer.Rooms;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class InFlightHandlerTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly GatedHandler _inner = new();

    [Fact]
    public async Task NothingInFlight_IsAlreadyIdle()
    {
        var handler = new InFlightHandler<int>(_inner);

        await handler.WhenIdleAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IdleIsNotReached_UntilTheHandlerHasFinished()
    {
        var handler = new InFlightHandler<int>(_inner);
        var handling = handler.HandleAsync(1, TestContext.Current.CancellationToken);

        var idle = handler.WhenIdleAsync();
        Assert.False(idle.IsCompleted);

        _inner.Release();
        await handling;
        await idle.WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task IdleWaitsForTheLast_OfSeveralOverlappingMessages()
    {
        var handler = new InFlightHandler<int>(_inner);
        var first = handler.HandleAsync(1, TestContext.Current.CancellationToken);
        var second = handler.HandleAsync(2, TestContext.Current.CancellationToken);

        var idle = handler.WhenIdleAsync();
        _inner.Release();
        await first;
        await second;

        await idle.WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AHandlerThatThrows_StillCountsAsFinished()
    {
        var handler = new InFlightHandler<int>(new ThrowingHandler());

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await handler.HandleAsync(1, TestContext.Current.CancellationToken));

        await handler.WhenIdleAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);
    }

    private sealed class GatedHandler : IAsyncMessageHandler<int>
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult();

        public async ValueTask HandleAsync(int message, CancellationToken cancellationToken)
            => await _gate.Task.WaitAsync(_timeout, cancellationToken);
    }

    private sealed class ThrowingHandler : IAsyncMessageHandler<int>
    {
        public ValueTask HandleAsync(int message, CancellationToken cancellationToken)
            => throw new InvalidOperationException("handler failure");
    }
}
