using System.Security.Claims;
using Gesellschaftsspieler.MCPServer.Enforcement;

namespace Gesellschaftsspieler.MCPServer.Tests;

public class CallerLevelResolverTests
{
    private static ClaimsPrincipal Authenticated(string? sub, params string[] roles)
    {
        var claims = new List<Claim>();
        if (sub is not null) claims.Add(new Claim("sub", sub));
        foreach (var role in roles) claims.Add(new Claim("role", role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    [Fact]
    public void NoPrincipal_IsAnonymous()
    {
        var caller = CallerLevelResolver.Resolve(null);
        Assert.Equal(CallerLevel.Anonymous, caller.Level);
        Assert.Null(caller.Subject);
    }

    [Fact]
    public void Unauthenticated_IsAnonymous()
    {
        var caller = CallerLevelResolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.Equal(CallerLevel.Anonymous, caller.Level);
    }

    [Fact]
    public void AuthenticatedWithoutSub_IsAnonymous()
    {
        var caller = CallerLevelResolver.Resolve(Authenticated(sub: null, "Administrator"));
        Assert.Equal(CallerLevel.Anonymous, caller.Level);
    }

    [Fact]
    public void SubWithoutAdminRole_IsUser()
    {
        var caller = CallerLevelResolver.Resolve(Authenticated("u1"));
        Assert.Equal(CallerLevel.User, caller.Level);
        Assert.Equal("u1", caller.Subject);
    }

    [Fact]
    public void NonAdminRole_IsUser()
    {
        var caller = CallerLevelResolver.Resolve(Authenticated("u1", "PowerUser", "Publisher"));
        Assert.Equal(CallerLevel.User, caller.Level);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Admin")]
    public void AdminRole_IsAdmin(string role)
    {
        var caller = CallerLevelResolver.Resolve(Authenticated("u1", role));
        Assert.Equal(CallerLevel.Admin, caller.Level);
    }

    [Fact]
    public void MultipleRolesIncludingAdmin_IsAdmin()
    {
        var caller = CallerLevelResolver.Resolve(Authenticated("u1", "User", "Administrator"));
        Assert.Equal(CallerLevel.Admin, caller.Level);
    }
}
