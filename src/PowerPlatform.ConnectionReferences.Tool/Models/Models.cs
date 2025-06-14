using Newtonsoft.Json.Linq;

namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class AppSettings
{
    public PowerPlatformSettings PowerPlatform { get; set; } = new();
    public ConnectionReferenceSettings ConnectionReferences { get; set; } = new();
}

public class PowerPlatformSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string DataverseUrl { get; set; } = string.Empty;
}

public class ConnectionReferenceSettings
{
    public string Prefix { get; set; } = "shared";
    public Dictionary<string, ProviderMapping> ProviderMappings { get; set; } = new();
}

public class ProviderMapping
{
    public string ConnectionId { get; set; } = string.Empty;
    public string ConnectorId { get; set; } = string.Empty;
}

public class FlowInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ClientData { get; set; } = string.Empty;
}

public class ConnectionReference
{
    public string ReferenceKey { get; set; } = string.Empty;
    public string LogicalName { get; set; } = string.Empty;
    public string ApiName { get; set; } = string.Empty;
}

public class ProcessingStats
{
    public int CreatedConnRefCount { get; set; }
    public int CreatedConnRefErrorCount { get; set; }
    public int AddedToSolutionCount { get; set; }
    public int AddedToSolutionErrorCount { get; set; }
    public int UpdatedFlowCount { get; set; }
    public int UpdatedFlowErrorCount { get; set; }
    public int DeletedConnRefCount { get; set; }
    public int DeletedConnRefErrorCount { get; set; }
    
    public int TotalErrors => CreatedConnRefErrorCount + AddedToSolutionErrorCount + 
                              UpdatedFlowErrorCount + DeletedConnRefErrorCount;
}

public class ConnectionReferenceResult
{
    public string Id { get; set; } = string.Empty;
    public string LogicalName { get; set; } = string.Empty;
}
