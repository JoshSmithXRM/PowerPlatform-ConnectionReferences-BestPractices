namespace PowerPlatform.ConnectionReferences.Tool;

public class AnalysisResult
{
    public string SolutionName { get; set; } = string.Empty;
    public List<FlowAnalysis> Flows { get; set; } = new List<FlowAnalysis>();
}
