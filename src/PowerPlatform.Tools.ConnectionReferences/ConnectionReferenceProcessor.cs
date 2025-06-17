using Microsoft.Extensions.Configuration;
using PowerPlatform.Tools.ConnectionReferences.Models;
using PowerPlatform.Tools.ConnectionReferences.Services;

namespace PowerPlatform.Tools.ConnectionReferences;

public class ConnectionReferenceProcessor
{
    private readonly AppSettings _settings;
    private readonly IDataverseService _dataverseService;
    private readonly IFlowService _flowService;
    private readonly IConnectionReferenceService _connectionReferenceService;
    private readonly IAnalysisOutputService _analysisOutputService;
    private readonly IDeploymentSettingsService _deploymentSettingsService; public ConnectionReferenceProcessor(
        AppSettings settings,
        IDataverseService dataverseService,
        IFlowService flowService,
        IConnectionReferenceService connectionReferenceService,
        IAnalysisOutputService analysisOutputService,
        IDeploymentSettingsService deploymentSettingsService)
    {
        _settings = settings;
        _dataverseService = dataverseService;
        _flowService = flowService;
        _connectionReferenceService = connectionReferenceService;
        _analysisOutputService = analysisOutputService;
        _deploymentSettingsService = deploymentSettingsService;
    }

    private string BuildApiUrl(string endpoint)
    {
        var baseUrl = _settings.PowerPlatform.DataverseUrl.TrimEnd('/');
        return $"{baseUrl}/api/data/v9.2/{endpoint.TrimStart('/')}";
    }

    public async Task AnalyzeAsync(string solutionName)
    {
        // Call the comprehensive overload with default parameters
        await AnalyzeAsync(solutionName, OutputFormat.Table, outputPath: null);
    }

    public async Task AnalyzeAsync(string solutionName, OutputFormat format = OutputFormat.Vertical, string? outputPath = null)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();
        var flows = await _flowService.GetCloudFlowsInSolutionAsync(httpClient, solutionName);

        var analysisResult = new AnalysisResult
        {
            SolutionName = solutionName,
            Flows = new List<FlowAnalysis>()
        };

        foreach (var flow in flows)
        {
            var flowInfo = _flowService.ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = _flowService.GetConnectionReferences(flowInfo.ClientData);
            var flowAnalysis = new FlowAnalysis
            {
                FlowId = flowInfo.Id,
                FlowName = flowInfo.Name,
                ConnectionReferences = new List<ConnectionReferenceAnalysis>()
            };

            foreach (var connRef in connectionRefs)
            {
                var connRefDetails = await GetConnectionReferenceDetailsAsync(httpClient, connRef.LogicalName);
                flowAnalysis.ConnectionReferences.Add(new ConnectionReferenceAnalysis
                {
                    ConnectionReferenceId = connRefDetails?.Id ?? "Unknown",
                    LogicalName = connRef.LogicalName,
                    Provider = connRef.ApiName,
                    ConnectionId = connRefDetails?.ConnectionId ?? "Not Set"
                });
            }

            analysisResult.Flows.Add(flowAnalysis);
        }

        switch (format)
        {
            case OutputFormat.Table:
                await _analysisOutputService.OutputTableAsync(analysisResult, outputPath);
                break;
            case OutputFormat.Vertical:
                await _analysisOutputService.OutputVerticalAsync(analysisResult, outputPath);
                break;
            case OutputFormat.Csv:
                await _analysisOutputService.OutputCsvAsync(analysisResult, outputPath);
                break;
            case OutputFormat.Json:
                await _analysisOutputService.OutputJsonAsync(analysisResult, outputPath);
                break;
        }
    }
    public async Task CreateConnectionReferencesAsync(string solutionName, bool dryRun)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();
        var flows = await _flowService.GetCloudFlowsInSolutionAsync(httpClient, solutionName);
        var stats = new ProcessingStats();

        foreach (var flow in flows)
        {
            var flowInfo = _flowService.ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = _flowService.GetConnectionReferences(flowInfo.ClientData);
            foreach (var providerGroup in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName)
            && _settings.ConnectionReferences.ProviderMappings.ContainsKey(cr.ApiName))
            .GroupBy(cr => cr.ApiName))
            {
                var expectedLogicalName = _dataverseService.GenerateLogicalName(providerGroup.Key, flowInfo.Id);
                var needsNewConnectionRef = providerGroup.Any(cr =>
                    string.IsNullOrEmpty(cr.LogicalName) ||
                    !cr.LogicalName.StartsWith($"{_settings.ConnectionReferences.Prefix}_", StringComparison.OrdinalIgnoreCase));

                if (needsNewConnectionRef)
                {
                    Console.WriteLine($"[INFO] {flowInfo.Name}: Processing provider '{providerGroup.Key}'");
                    await _connectionReferenceService.ProcessConnectionReferenceForProviderAsync(httpClient, flowInfo, providerGroup.Key, stats, dryRun, solutionName);
                }
                else
                {
                    Console.WriteLine($"[INFO] {flowInfo.Name}: Skipping provider '{providerGroup.Key}' - already follows naming pattern");
                }
            }
        }

        PrintSummary(stats);
    }
    public async Task UpdateFlowsAsync(string solutionName, bool dryRun)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();
        var flows = await _flowService.GetCloudFlowsInSolutionAsync(httpClient, solutionName);
        var stats = new ProcessingStats();

        foreach (var flow in flows)
        {
            var flowInfo = _flowService.ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = _flowService.GetConnectionReferences(flowInfo.ClientData);
            var newConnRefLogicalNames = new Dictionary<string, string>();

            foreach (var providerGroup in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName) && _settings.ConnectionReferences.ProviderMappings.ContainsKey(cr.ApiName)).GroupBy(cr => cr.ApiName))
            {
                // Check if any connection reference for this provider needs updating
                var expectedLogicalName = _dataverseService.GenerateLogicalName(providerGroup.Key, flowInfo.Id);
                var needsUpdate = providerGroup.Any(cr =>
                    string.IsNullOrEmpty(cr.LogicalName) ||
                    !cr.LogicalName.StartsWith($"{_settings.ConnectionReferences.Prefix}_", StringComparison.OrdinalIgnoreCase));

                if (needsUpdate)
                {
                    newConnRefLogicalNames[providerGroup.Key] = expectedLogicalName;
                }
            }

            if (newConnRefLogicalNames.Any())
            {
                await _flowService.UpdateFlowConnectionReferencesAsync(httpClient, flowInfo, newConnRefLogicalNames, stats, dryRun);
            }
        }

        PrintSummary(stats);
    }

    public async Task ProcessAsync(string solutionName, bool dryRun)
    {
        Console.WriteLine("=== CREATING CONNECTION REFERENCES ===");
        await CreateConnectionReferencesAsync(solutionName, dryRun);

        Console.WriteLine("\n=== UPDATING FLOWS ===");
        await UpdateFlowsAsync(solutionName, dryRun);
    }

    public async Task GenerateDeploymentSettingsAsync(string solutionName, string outputPath)
    {
        await _deploymentSettingsService.GenerateDeploymentSettingsAsync(solutionName, outputPath);
    }

    public async Task CleanupAsync(string solutionName, bool dryRun)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();

        Console.WriteLine($"[INFO] Starting cleanup for solution '{solutionName}'");

        var connectionRefs = await _connectionReferenceService.GetConnectionReferencesInSolutionAsync(httpClient, solutionName);
        Console.WriteLine($"[INFO] Found {connectionRefs.Count} connection references in solution");

        var flows = await _flowService.GetCloudFlowsInSolutionAsync(httpClient, solutionName);
        Console.WriteLine($"[INFO] Found {flows.Count} flows in solution");

        var dependencyMap = _flowService.BuildConnectionReferenceDependencyMap(flows);
        Console.WriteLine($"[INFO] Found {dependencyMap.Count} connection references in use by flows");

        var unusedConnRefs = connectionRefs.Where(cr => !dependencyMap.ContainsKey(cr.LogicalName)).ToList();
        var inUseConnRefs = connectionRefs.Where(cr => dependencyMap.ContainsKey(cr.LogicalName)).ToList();

        Console.WriteLine($"[INFO] Connection references in use: {inUseConnRefs.Count}");
        Console.WriteLine($"[INFO] Connection references unused: {unusedConnRefs.Count}");

        foreach (var inUseRef in inUseConnRefs)
        {
            var flowCount = dependencyMap[inUseRef.LogicalName].Count;
            Console.WriteLine($"[IN USE] '{inUseRef.LogicalName}' used by {flowCount} flow(s)");
        }

        var stats = new ProcessingStats();
        foreach (var unusedConnRef in unusedConnRefs)
        {
            if (dryRun)
            {
                Console.WriteLine($"[DRY RUN] Would delete unused connection reference '{unusedConnRef.LogicalName}' (ID: {unusedConnRef.Id})");
                stats.DeletedConnRefCount++;
            }
            else
            {
                var success = await _connectionReferenceService.DeleteConnectionReferenceAsync(httpClient, unusedConnRef);
                if (success)
                {
                    stats.DeletedConnRefCount++;
                }
                else
                {
                    stats.DeletedConnRefErrorCount++;
                }
            }
        }

        Console.WriteLine("\n--- CLEANUP SUMMARY ---");
        Console.WriteLine($"Connection References Deleted: {stats.DeletedConnRefCount} (Errors: {stats.DeletedConnRefErrorCount})");
        Console.WriteLine($"Connection References Kept (In Use): {inUseConnRefs.Count}");
        Console.WriteLine($"Total Errors: {stats.DeletedConnRefErrorCount}");
    }

    private async Task<ConnectionReferenceDetails?> GetConnectionReferenceDetailsAsync(HttpClient httpClient, string logicalName)
    {
        try
        {
            var queryUrl = BuildApiUrl($"connectionreferences?$select=connectionreferenceid,connectionreferencelogicalname,connectionid&$filter=connectionreferencelogicalname eq '{logicalName}'");
            var resp = await httpClient.GetAsync(queryUrl);

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR] Failed to query connection reference '{logicalName}'. Status: {resp.StatusCode}");
                return null;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var result = Newtonsoft.Json.Linq.JObject.Parse(content);
            var connRefs = result["value"] as Newtonsoft.Json.Linq.JArray;

            if (connRefs == null || connRefs.Count == 0)
                return new ConnectionReferenceDetails { Id = "Not Found", ConnectionId = "Not Found" };

            var connRef = connRefs[0];
            return new ConnectionReferenceDetails
            {
                Id = connRef["connectionreferenceid"]?.ToString() ?? "Unknown",
                ConnectionId = connRef["connectionid"]?.ToString() ?? "Not Set"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception querying connection reference '{logicalName}': {ex.Message}");
            return new ConnectionReferenceDetails { Id = "Error", ConnectionId = "Error" };
        }
    }

    private static void PrintSummary(ProcessingStats stats)
    {
        Console.WriteLine($"\n--- SUMMARY ---");
        Console.WriteLine($"Connection References Created: {stats.CreatedConnRefCount} (Errors: {stats.CreatedConnRefErrorCount})");
        Console.WriteLine($"Connection References Added to Solution: {stats.AddedToSolutionCount} (Errors: {stats.AddedToSolutionErrorCount})");
        Console.WriteLine($"Flows Updated: {stats.UpdatedFlowCount} (Errors: {stats.UpdatedFlowErrorCount})");
        Console.WriteLine($"Connection References Deleted: {stats.DeletedConnRefCount} (Errors: {stats.DeletedConnRefErrorCount})");
        Console.WriteLine($"Total Errors: {stats.TotalErrors}");
    }

    private class ConnectionReferenceDetails
    {
        public string Id { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }
}
