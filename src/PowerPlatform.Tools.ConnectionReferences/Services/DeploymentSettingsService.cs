using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class DeploymentSettingsService : IDeploymentSettingsService
{
    private readonly AppSettings _settings;
    private readonly IConnectionReferenceService _connectionReferenceService;
    private readonly IDataverseService _dataverseService;

    public DeploymentSettingsService(AppSettings settings, IConnectionReferenceService connectionReferenceService, IDataverseService dataverseService)
    {
        _settings = settings;
        _connectionReferenceService = connectionReferenceService;
        _dataverseService = dataverseService;
    }

    public async Task GenerateDeploymentSettingsAsync(string solutionName, string outputPath)
    {
        var httpClient = await _dataverseService.GetAuthenticatedHttpClientAsync();

        var connectionReferences = await _connectionReferenceService.GetConnectionReferencesInSolutionAsync(httpClient, solutionName);

        var deploymentConnectionReferences = new List<JObject>();

        foreach (var connectionRef in connectionReferences)
        {

            deploymentConnectionReferences.Add(new JObject
            {
                ["LogicalName"] = connectionRef.LogicalName,
                ["ConnectionId"] = connectionRef.ConnectionId,
                ["ConnectorId"] = connectionRef.ConnectorId
            });
        }

        deploymentConnectionReferences = deploymentConnectionReferences
            .OrderBy(cr => cr["LogicalName"]?.ToString())
            .ToList();

        var deploymentSettings = new JObject
        {
            ["EnvironmentVariables"] = new JArray(),
            ["ConnectionReferences"] = new JArray(deploymentConnectionReferences)
        };

        await File.WriteAllTextAsync(outputPath, deploymentSettings.ToString(Formatting.Indented));
        Console.WriteLine($"[INFO] Generated deployment settings with {deploymentConnectionReferences.Count} connection references: {outputPath}");
    }

    private string ExtractConnectorNameFromId(string connectorId)
    {
        if (string.IsNullOrEmpty(connectorId))
            return "UNKNOWN";

        var parts = connectorId.Split('/');
        var connectorName = parts.LastOrDefault() ?? "unknown";

        if (connectorName.StartsWith("shared_"))
            connectorName = connectorName.Substring(7);

        return connectorName;
    }
}
