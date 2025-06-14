using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IFlowService
{
    Task<List<JObject>> GetCloudFlowsInSolutionAsync(HttpClient httpClient, string solutionName);
    FlowInfo? ExtractFlowInfo(JObject flow);
    List<ConnectionReference> GetConnectionReferences(string clientDataJson);
    Task<List<string>> UpdateFlowConnectionReferencesAsync(HttpClient httpClient, FlowInfo flow, Dictionary<string, string> newConnRefLogicalNames, ProcessingStats stats, bool dryRun);
    Dictionary<string, List<string>> BuildConnectionReferenceDependencyMap(List<JObject> flows);
}
