namespace PowerPlatform.ConnectionReferences.Tool.Models;

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

    public int TotalErrors => CreatedConnRefErrorCount + AddedToSolutionErrorCount + UpdatedFlowErrorCount + DeletedConnRefErrorCount;
}
