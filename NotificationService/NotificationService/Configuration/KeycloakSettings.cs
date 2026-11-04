namespace NotificationService.Configuration;

public class KeycloakSettings
{
    public const string SectionName = "Keycloak";

    public required string Authority { get; set; }

    public string InternalAuthority { get; set; } = string.Empty;
}
