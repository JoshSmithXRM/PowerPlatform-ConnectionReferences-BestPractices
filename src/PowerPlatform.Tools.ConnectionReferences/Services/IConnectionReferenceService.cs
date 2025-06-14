using PowerPlatform.Tools.ConnectionReferences.Models;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IConnectionReferenceService
{
    Task<string?> CreateConnectionReferenceAsync(HttpClient httpClient, string logicalName, string displayName, string connectionId, string connectorId);
    Task<string?> QueryConnectionReferenceIdAsync(HttpClient httpClient, string logicalName);
    Task<List<ConnectionReferenceInfo>> GetConnectionReferencesInSolutionAsync(HttpClient httpClient, string solutionName);
    Task<bool> DeleteConnectionReferenceAsync(HttpClient httpClient, ConnectionReferenceInfo connectionRef);
    Task<bool> AddConnectionReferenceToSolutionAsync(HttpClient httpClient, string connRefId, string logicalName, string solutionName);
    Task<ConnectionReferenceResult?> ProcessConnectionReferenceForProviderAsync(HttpClient httpClient, FlowInfo flow, string provider, ProcessingStats stats, bool dryRun, string solutionName);
}
