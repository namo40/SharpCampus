using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Shared.Identity;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class UserContextTests
{
    [Fact]
    public void UserId_ComesFromTheSubjectClaim()
    {
        var userId = Guid.NewGuid();
        var context = CreateContext(("sub", userId.ToString()), ("email", "player@example.com"));

        Assert.Equal(new UserId(userId), context.UserId);
    }

    [Fact]
    public void Email_ComesFromTheEmailClaim()
    {
        var context = CreateContext(("sub", Guid.NewGuid().ToString()), ("email", "player@example.com"));

        Assert.Equal("player@example.com", context.Email);
    }

    [Fact]
    public void UserId_ThrowsWhenTheSubjectClaimIsMissing()
    {
        var context = CreateContext(("email", "player@example.com"));

        Assert.Throws<InvalidOperationException>(() => context.UserId);
    }

    [Fact]
    public void Email_ThrowsOutsideOfAnHttpRequest()
    {
        var context = new UserContext(new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() => context.Email);
    }

    private static UserContext CreateContext(params (string Type, string Value)[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                claims.Select(claim => new Claim(claim.Type, claim.Value)),
                authenticationType: "Test")),
        };

        return new UserContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
