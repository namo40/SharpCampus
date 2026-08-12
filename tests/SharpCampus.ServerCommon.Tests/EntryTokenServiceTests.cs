using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class EntryTokenServiceTests
{
    private static readonly UserId _user = new(Guid.NewGuid());
    private static readonly RoomId _room = new(Ulid.NewUlid());

    private readonly TestTimeProvider _time = new(new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void IssuedToken_ValidatesBackToTheUserAndRoom()
    {
        var tokens = CreateService("shared-secret");

        Assert.True(tokens.TryValidate(tokens.Issue(_user, _room), out var userId, out var roomId));
        Assert.Equal(_user, userId);
        Assert.Equal(_room, roomId);
    }

    [Fact]
    public void TamperedSignature_IsRejected()
    {
        var tokens = CreateService("shared-secret");
        var token = tokens.Issue(_user, _room);

        // Flip the last character of the signature, keeping the payload intact.
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.False(tokens.TryValidate(tampered, out _, out _));
    }

    [Fact]
    public void PayloadFromAnotherToken_IsRejected()
    {
        var tokens = CreateService("shared-secret");
        var mine = tokens.Issue(_user, _room);
        var other = tokens.Issue(_user, new RoomId(Ulid.NewUlid()));

        Assert.False(tokens.TryValidate(
            $"{other[..other.IndexOf('.')]}.{mine[(mine.IndexOf('.') + 1)..]}",
            out _,
            out _));
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var tokens = CreateService("shared-secret");
        var token = tokens.Issue(_user, _room);

        _time.Advance(TimeSpan.FromSeconds(61));

        Assert.False(tokens.TryValidate(token, out _, out _));
    }

    [Fact]
    public void TokenFromAnotherSecret_IsRejected()
    {
        var token = CreateService("the-other-server").Issue(_user, _room);

        Assert.False(CreateService("shared-secret").TryValidate(token, out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("not.base64url!")]
    public void MalformedToken_IsRejected(string token)
        => Assert.False(CreateService("shared-secret").TryValidate(token, out _, out _));

    [Fact]
    public void NullToken_IsRejected()
        => Assert.False(CreateService("shared-secret").TryValidate(null, out _, out _));

    private EntryTokenService CreateService(string secret)
        => new(Options.Create(new EntryTokenOptions { Secret = secret }), _time);
}
