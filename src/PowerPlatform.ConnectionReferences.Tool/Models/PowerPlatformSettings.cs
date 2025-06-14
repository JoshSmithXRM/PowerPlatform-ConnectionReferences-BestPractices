namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class PowerPlatformSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string DataverseUrl { get; set; } = string.Empty;
    public AuthenticationMethod AuthenticationMethod { get; set; } = AuthenticationMethod.ServicePrincipal;
    
    // Service Principal Authentication
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    
    // Username/Password Authentication (Legacy)
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
    // Interactive/DeviceCode Authentication (uses well-known public client)
    public string PublicClientId { get; set; } = "51f81489-12ee-4a9e-aaae-a2591f45987d"; // Default Power Platform CLI client ID
}
