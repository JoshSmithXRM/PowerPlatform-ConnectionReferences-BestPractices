namespace PowerPlatform.Tools.ConnectionReferences.Models;

public class PowerPlatformSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string DataverseUrl { get; set; } = string.Empty;
    public AuthenticationMethod AuthenticationMethod { get; set; } = AuthenticationMethod.ServicePrincipal;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PublicClientId { get; set; } = "51f81489-12ee-4a9e-aaae-a2591f45987d";
}
