namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class AnalysisResult
{
    public List<FlowAnalysis> Flows { get; set; } = new();
    public string SolutionName { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    public int TotalFlows => Flows.Count;
    public int TotalConnectionReferences => Flows.Sum(f => f.ConnectionReferences.Count);
}
