using System.Security.Claims;
using BioAnalyzer.Research.Api.Infrastructure;

namespace BioAnalyzer.Research.Api.Tests;

public class RequesterIdentityTests
{
    [Fact]
    public void Resolve_NullOrAnonymous_ReturnsNull()
    {
        Assert.Null(RequesterIdentity.Resolve(null));
        Assert.Null(RequesterIdentity.Resolve(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void Resolve_PrefersEntraOid()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("oid", "user-object-id"),
                new Claim("appid", "should-not-win"),
                new Claim(ClaimTypes.Name, "display")
            ],
            authenticationType: "Bearer");
        var result = RequesterIdentity.Resolve(new ClaimsPrincipal(identity));
        Assert.Equal(RequesterIdentity.UserSubjectPrefix + "user-object-id", result);
    }

    [Fact]
    public void Resolve_DaemonAppId_WhenNoOid()
    {
        var identity = new ClaimsIdentity(
            [new Claim("appid", "daemon-client-id")],
            authenticationType: "Bearer");
        var result = RequesterIdentity.Resolve(new ClaimsPrincipal(identity));
        Assert.Equal(RequesterIdentity.AppSubjectPrefix + "daemon-client-id", result);
    }

    [Fact]
    public void Resolve_ApiKeyScheme_UsesPrefixedName()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, "research-api-client"),
                new Claim("auth_type", "api_key")
            ],
            authenticationType: ApiKeyAuthenticationHandler.SchemeName);
        var result = RequesterIdentity.Resolve(new ClaimsPrincipal(identity));
        Assert.Equal(RequesterIdentity.ApiKeySubjectPrefix + "research-api-client", result);
    }

    [Fact]
    public void EqualsRequester_IsOrdinal()
    {
        Assert.True(RequesterIdentity.EqualsRequester("oid:abc", "oid:abc"));
        Assert.False(RequesterIdentity.EqualsRequester("oid:abc", "oid:ABC"));
        Assert.False(RequesterIdentity.EqualsRequester(null, "oid:abc"));
    }
}
