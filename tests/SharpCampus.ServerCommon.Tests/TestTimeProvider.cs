namespace SharpCampus.ServerCommon.Tests;

// A clock the test moves by hand, so token expiry can be reached without waiting for it.
internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
