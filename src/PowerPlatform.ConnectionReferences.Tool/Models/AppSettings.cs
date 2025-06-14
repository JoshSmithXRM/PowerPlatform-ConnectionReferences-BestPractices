namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class AppSettings
{
    public PowerPlatformSettings PowerPlatform { get; set; } = new();
    public ConnectionReferenceSettings ConnectionReferences { get; set; } = new();
}
