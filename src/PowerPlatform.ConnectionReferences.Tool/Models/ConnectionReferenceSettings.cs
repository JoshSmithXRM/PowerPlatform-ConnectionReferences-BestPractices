namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class ConnectionReferenceSettings
{
    public string Prefix { get; set; } = "shared";
    public Dictionary<string, ProviderMapping> ProviderMappings { get; set; } = new();
}
