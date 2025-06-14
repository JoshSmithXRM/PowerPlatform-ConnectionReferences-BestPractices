using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class DeploymentSettingsService : IDeploymentSettingsService
{
    private readonly AppSettings _settings;
    private readonly IFlowService _flowService;
    private readonly IDataverseService _dataverseService;

    public DeploymentSettingsService(AppSettings settings, IFlowService flowService, IDataverseService dataverseService)
    {
        _settings = settings;
        _flowService = flowService;
        _dataverseService = dataverseService;
    }

    public async Task GenerateDeploymentSettingsAsync(string solutionName, string outputPath)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();
        var flows = await _flowService.GetCloudFlowsInSolutionAsync(httpClient, solutionName);

        var connectionReferences = new List<JObject>();
        var processedLogicalNames = new HashSet<string>();

        foreach (var flow in flows)
        {
            var flowInfo = _flowService.ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = _flowService.GetConnectionReferences(flowInfo.ClientData);

            foreach (var connRef in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName)))
            {
                var logicalName = _dataverseService.GenerateLogicalName(connRef.ApiName, flowInfo.Id);

                if (!processedLogicalNames.Contains(logicalName))
                {
                    var connectorName = connRef.ApiName.Replace("shared_", "");
                    var connectionIdPlaceholder = $"{{{{REPLACE_WITH_{connectorName.ToUpper()}_CONNECTION_ID}}}}";

                    connectionReferences.Add(new JObject
                    {
                        ["LogicalName"] = logicalName,
                        ["ConnectionId"] = connectionIdPlaceholder,
                        ["ConnectorId"] = _settings.ConnectionReferences.ProviderMappings.GetValueOrDefault(connRef.ApiName)?.ConnectorId ?? ""
                    });
                    processedLogicalNames.Add(logicalName);
                }
            }
        }

        var deploymentSettings = new JObject
        {
            ["EnvironmentVariables"] = new JArray(),
            ["ConnectionReferences"] = new JArray(connectionReferences)
        };

        await File.WriteAllTextAsync(outputPath, deploymentSettings.ToString(Formatting.Indented));
        Console.WriteLine($"[INFO] Deployment settings written to {outputPath}");
    }
}
