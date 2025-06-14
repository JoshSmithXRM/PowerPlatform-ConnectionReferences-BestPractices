namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class AnalysisResult
{
    public List<FlowAnalysis> Flows { get; set; } = new();
    public string SolutionName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    public int TotalFlows => Flows.Count;
    public int TotalConnectionReferences => Flows.Sum(f => f.ConnectionReferences.Count);
}

public class FlowAnalysis
{
    public string FlowId { get; set; } = string.Empty;
    public string FlowName { get; set; } = string.Empty;
    public List<ConnectionReferenceAnalysis> ConnectionReferences { get; set; } = new();
}

public class ConnectionReferenceAnalysis
{
    public string ConnectionReferenceId { get; set; } = string.Empty;
    public string LogicalName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
}
