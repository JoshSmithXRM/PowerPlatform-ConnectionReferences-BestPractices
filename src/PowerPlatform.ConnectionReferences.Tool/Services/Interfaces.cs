using PowerPlatform.ConnectionReferences.Tool.Models;

namespace PowerPlatform.ConnectionReferences.Tool.Services;

public interface IConnectionReferenceService
{
    Task ProcessSolutionAsync(string solutionName);
    Task<Solution> GetSolutionWithCloudFlowsAsync(string solutionName);
    Task<List<ConnectionReference>> AnalyzeConnectionReferencesAsync(Solution solution);
    Task CreateMissingConnectionReferencesAsync(List<ConnectionReference> requiredReferences);
}

public interface IPowerPlatformClient
{
    Task<Solution?> GetSolutionByNameAsync(string solutionName);
    Task<List<CloudFlow>> GetCloudFlowsInSolutionAsync(string solutionId);
    Task<List<ConnectionReference>> GetConnectionReferencesInSolutionAsync(string solutionId);
    Task<ConnectionReference> CreateConnectionReferenceAsync(ConnectionReference connectionReference);
    Task<List<ConnectionReference>> GetConnectionReferencesByConnectorAsync(string connectorId);
}
