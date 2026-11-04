using System.Diagnostics.CodeAnalysis;

namespace ProductService.API.Configurations;

[ExcludeFromCodeCoverage]
public record Auth0Settings
{
    public const string SectionName = AuthSchemes.Auth0;

    public string Domain { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string Scopes { get; init; } = "openid profile email";
    public string RoleClaimType { get; init; } = string.Empty;
}

[ExcludeFromCodeCoverage]
public record KeycloakSettings
{
    public const string SectionName = AuthSchemes.Keycloak;

    public string Authority { get; init; } = string.Empty;
    public string InternalAuthority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string Scopes { get; init; } = "openid profile email";
}
