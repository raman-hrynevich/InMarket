using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ProductService.API.Configurations;
using ProductService.BLL.Validators;
using ProductService.Infrastructure;
using Unleash;
using System.Diagnostics.CodeAnalysis;

namespace ProductService.API.Extensions;

[ExcludeFromCodeCoverage]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.AddValidatorsFromAssemblyContaining<CreateCategoryModelValidator>();

        services.AddAutoMapper(cfg =>
        {
            cfg.AddMaps(typeof(Mappings.MappingProfile).Assembly);
        });

        var allowedOrigins = configuration.GetRequiredSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("CORS allowed origins are missing or empty.");
        }
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        services.Configure<UnleashOptions>(configuration.GetSection("Unleash"));

        services.AddSingleton<IUnleash>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<UnleashOptions>>().Value;

            if (string.IsNullOrEmpty(options.ApiUrl) || string.IsNullOrEmpty(options.ApiToken))
            {
                throw new InvalidOperationException("Unleash options (ApiUrl or ApiToken) are not configured properly.");
            }

            var settings = new UnleashSettings
            {
                AppName = "product-service",
                UnleashApi = new Uri(options.ApiUrl),
                CustomHttpHeaders = new Dictionary<string, string>
                {
                    { "Authorization", options.ApiToken }
                }
            };

            return new DefaultUnleash(settings);
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ProductDbContext>(options =>
        {
            options.UseLazyLoadingProxies();
            options.UseSqlServer(connectionString);
        });

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));

        services.AddHangfireServer();

        return services;
    }

    public static IServiceCollection AddSecurityLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WebhookSettings>(configuration.GetSection("Webhooks"));
        services.Configure<Auth0Settings>(configuration.GetSection(Auth0Settings.SectionName));
        services.Configure<KeycloakSettings>(configuration.GetSection(KeycloakSettings.SectionName));

        var auth0Settings = configuration.GetSection(Auth0Settings.SectionName).Get<Auth0Settings>()
            ?? throw new InvalidOperationException("Auth0 settings are missing in Configuration!");

        var keycloakSettings = configuration.GetSection(KeycloakSettings.SectionName).Get<KeycloakSettings>()
            ?? throw new InvalidOperationException("Keycloak settings are missing in Configuration!");

        if (string.IsNullOrWhiteSpace(auth0Settings.RoleClaimType))
        {
            throw new InvalidOperationException("Auth0:RoleClaimType is missing in Configuration!");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = AuthSchemes.Selector;
            options.DefaultChallengeScheme = AuthSchemes.Selector;
        })
        .AddPolicyScheme(AuthSchemes.Selector, AuthSchemes.Selector, options =>
        {
            options.ForwardDefaultSelector = context =>
                AuthSchemeSelector.ForRequest(context, keycloakSettings.Authority);
        })
        .AddJwtBearer(AuthSchemes.Auth0, options =>
        {
            options.Authority = $"https://{auth0Settings.Domain}/";
            options.Audience = auth0Settings.Audience;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                RoleClaimType = auth0Settings.RoleClaimType
            };
        })
        .AddJwtBearer(AuthSchemes.Keycloak, options =>
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
        });

        services.AddTransient<IClaimsTransformation, KeycloakRoleTransformation>();
        services.AddAuthorization();

        return services;
    }
}
