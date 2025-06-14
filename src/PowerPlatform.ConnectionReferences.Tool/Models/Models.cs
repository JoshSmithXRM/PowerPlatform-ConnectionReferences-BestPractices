namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class PowerPlatformSettings
{
    public string EnvironmentUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

public class ConnectionReferenceSettings
{
    public NamingSchemeSettings NamingScheme { get; set; } = new();
}

public class NamingSchemeSettings
{
    public string Prefix { get; set; } = "CR_";
    public string Suffix { get; set; } = "_Shared";
}

public class CloudFlow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<ConnectionReference> ConnectionReferences { get; set; } = new();
}

public class ConnectionReference
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
    public string ConnectorDisplayName { get; set; } = string.Empty;
    public string? ConnectionId { get; set; }
    public bool IsManaged { get; set; }
}

public class Solution
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<CloudFlow> CloudFlows { get; set; } = new();
    public List<ConnectionReference> ConnectionReferences { get; set; } = new();
}
