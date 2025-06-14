namespace PowerPlatform.ConnectionReferences.Tool.Models;

public class FlowAnalysis
{
    public string FlowId { get; set; } = string.Empty;
    public string FlowName { get; set; } = string.Empty;
    public List<ConnectionReferenceAnalysis> ConnectionReferences { get; set; } = new();
}
