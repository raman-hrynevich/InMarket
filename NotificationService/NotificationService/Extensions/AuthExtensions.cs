using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NotificationService.API.Configuration;
using NotificationService.Configuration;

namespace NotificationService.API.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Auth0Settings>(configuration.GetSection(Auth0Settings.SectionName));
        services.Configure<KeycloakSettings>(configuration.GetSection(KeycloakSettings.SectionName));

        var auth0Settings = configuration.GetSection(Auth0Settings.SectionName).Get<Auth0Settings>()
            ?? throw new InvalidOperationException("Auth0 settings are missing in appsettings.json");

        var keycloakSettings = configuration.GetSection(KeycloakSettings.SectionName).Get<KeycloakSettings>()
            ?? throw new InvalidOperationException("Keycloak settings are missing in appsettings.json");

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = AuthConstants.SelectorScheme;
            options.DefaultChallengeScheme = AuthConstants.SelectorScheme;
        })
        .AddPolicyScheme(AuthConstants.SelectorScheme, AuthConstants.SelectorScheme, options =>
        {
            options.ForwardDefaultSelector = context =>
                AuthSchemeSelector.ForRequest(context, keycloakSettings.Authority);
        })
        .AddJwtBearer(AuthConstants.Auth0Scheme, options =>
        {
            options.Authority = $"https://{auth0Settings.Domain}/";
            options.Audience = auth0Settings.Audience;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                RoleClaimType = auth0Settings.RoleClaimType
            };

            options.Events = CreateHubTokenEvents();
        })
        .AddJwtBearer(AuthConstants.KeycloakScheme, options =>
        {
            options.Authority = keycloakSettings.Authority;
            options.RequireHttpsMetadata = false;
            if (!string.IsNullOrWhiteSpace(keycloakSettings.InternalAuthority))
            {
                options.BackchannelHttpHandler = new KeycloakBackchannelHandler(
                    keycloakSettings.Authority,
                    keycloakSettings.InternalAuthority);
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidIssuer = keycloakSettings.Authority
            };

            options.Events = CreateHubTokenEvents();
        });

        services.AddTransient<IClaimsTransformation, KeycloakRoleTransformation>();

        return services;
    }

    private static JwtBearerEvents CreateHubTokenEvents()
    {
        return new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var hubToken = AuthSchemeSelector.ReadHubToken(context.HttpContext);

                if (hubToken is not null)
                {
                    context.Token = hubToken;
                }

                return Task.CompletedTask;
            }
        };
    }
}
