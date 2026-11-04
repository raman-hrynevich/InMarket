using ProductService.API.Extensions;
using Shouldly;
using Xunit;

namespace ProductService.Tests.Extensions;

public class KeycloakBackchannelTests
{
    private const string PublicAuthority = "http://localhost:8080/realms/inmarket-realm";
    private const string InternalAuthority = "http://keycloak:8080/realms/inmarket-realm";

    [Fact]
    public void Rewrite_ShouldPointMetadataAtTheInternalAuthority()
    {
        var request = new Uri($"{PublicAuthority}/.well-known/openid-configuration");

        var rewritten = KeycloakBackchannel.Rewrite(request, PublicAuthority, InternalAuthority);

        rewritten.ShouldBe(new Uri($"{InternalAuthority}/.well-known/openid-configuration"));
    }

    [Fact]
    public void Rewrite_ShouldPointSigningKeysAtTheInternalAuthority()
    {
        var request = new Uri($"{PublicAuthority}/protocol/openid-connect/certs");

        var rewritten = KeycloakBackchannel.Rewrite(request, PublicAuthority, InternalAuthority);

        rewritten.ShouldBe(new Uri($"{InternalAuthority}/protocol/openid-connect/certs"));
    }

    [Fact]
    public void Rewrite_ShouldLeaveTheRequest_WhenNoInternalAuthorityIsConfigured()
    {
        var request = new Uri($"{PublicAuthority}/protocol/openid-connect/certs");

        KeycloakBackchannel.Rewrite(request, PublicAuthority, "").ShouldBe(request);
    }

    [Fact]
    public void Rewrite_ShouldLeaveADifferentHost()
    {
        var request = new Uri("https://dev-lifelesssoul.eu.auth0.com/.well-known/openid-configuration");

        KeycloakBackchannel.Rewrite(request, PublicAuthority, InternalAuthority).ShouldBe(request);
    }
}
