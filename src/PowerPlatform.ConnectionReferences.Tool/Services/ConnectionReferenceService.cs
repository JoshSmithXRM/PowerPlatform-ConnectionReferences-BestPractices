using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PowerPlatform.ConnectionReferences.Tool.Models;

namespace PowerPlatform.ConnectionReferences.Tool.Services;

public class ConnectionReferenceService : IConnectionReferenceService
{
    private readonly IPowerPlatformClient _powerPlatformClient;
    private readonly ILogger<ConnectionReferenceService> _logger;
    private readonly PowerPlatformSettings _settings;
    private readonly ConnectionReferenceSettings _connectionReferenceSettings;

    public ConnectionReferenceService(
        IPowerPlatformClient powerPlatformClient,
        ILogger<ConnectionReferenceService> logger,
        IOptions<PowerPlatformSettings> settings,
        IOptions<ConnectionReferenceSettings> connectionReferenceSettings)
    {
        _powerPlatformClient = powerPlatformClient;
        _logger = logger;
        _settings = settings.Value;
        _connectionReferenceSettings = connectionReferenceSettings.Value;
    }

    public async Task ProcessSolutionAsync(string solutionName)
    {
        _logger.LogInformation("Processing solution: {SolutionName}", solutionName);

        // Get solution with all CloudFlows
        var solution = await GetSolutionWithCloudFlowsAsync(solutionName);
        
        if (solution == null)
        {
            _logger.LogError("Solution {SolutionName} not found", solutionName);
            return;
        }

        _logger.LogInformation("Found solution {SolutionName} with {CloudFlowCount} CloudFlows", 
            solution.DisplayName, solution.CloudFlows.Count);

        // Analyze connection references and determine what's needed
        var requiredReferences = await AnalyzeConnectionReferencesAsync(solution);
        
        _logger.LogInformation("Analysis complete. {RequiredCount} connection references need to be created", 
            requiredReferences.Count);

        // Create missing connection references
        if (requiredReferences.Any())
        {
            await CreateMissingConnectionReferencesAsync(requiredReferences);
        }
        else
        {
            _logger.LogInformation("No new connection references need to be created");
        }
    }

    public async Task<Solution> GetSolutionWithCloudFlowsAsync(string solutionName)
    {
        var solution = await _powerPlatformClient.GetSolutionByNameAsync(solutionName);
        if (solution == null)
        {
            throw new InvalidOperationException($"Solution '{solutionName}' not found");
        }

        // Get all CloudFlows in the solution
        solution.CloudFlows = await _powerPlatformClient.GetCloudFlowsInSolutionAsync(solution.Id);
        
        // Get existing connection references in the solution
        solution.ConnectionReferences = await _powerPlatformClient.GetConnectionReferencesInSolutionAsync(solution.Id);

        return solution;
    }

    public async Task<List<ConnectionReference>> AnalyzeConnectionReferencesAsync(Solution solution)
    {
        var requiredReferences = new List<ConnectionReference>();
        var connectorGroups = new Dictionary<string, List<ConnectionReference>>();

        // Group all connection references by connector
        foreach (var cloudFlow in solution.CloudFlows)
        {
            foreach (var connectionRef in cloudFlow.ConnectionReferences)
            {
                if (!connectorGroups.ContainsKey(connectionRef.ConnectorId))
                {
                    connectorGroups[connectionRef.ConnectorId] = new List<ConnectionReference>();
                }
                connectorGroups[connectionRef.ConnectorId].Add(connectionRef);
            }
        }

        foreach (var connectorGroup in connectorGroups)
        {
            var connectorId = connectorGroup.Key;
            var references = connectorGroup.Value;
            
            _logger.LogInformation("Analyzing connector {ConnectorId} with {ReferenceCount} references", 
                connectorId, references.Count);

            // Check if we already have a shared connection reference for this connector
            var existingSharedRef = solution.ConnectionReferences
                .FirstOrDefault(cr => cr.ConnectorId == connectorId && 
                                     cr.Name.Contains(_connectionReferenceSettings.NamingScheme.Suffix));

            if (existingSharedRef == null)
            {
                // Create a new shared connection reference for this connector
                var sharedReference = CreateSharedConnectionReference(references.First());
                requiredReferences.Add(sharedReference);
                
                _logger.LogInformation("Will create shared connection reference: {ReferenceName}", 
                    sharedReference.Name);
            }
            else
            {
                _logger.LogInformation("Shared connection reference already exists: {ReferenceName}", 
                    existingSharedRef.Name);
            }
        }

        return requiredReferences;
    }

    public async Task CreateMissingConnectionReferencesAsync(List<ConnectionReference> requiredReferences)
    {
        foreach (var reference in requiredReferences)
        {
            try
            {
                _logger.LogInformation("Creating connection reference: {ReferenceName}", reference.Name);
                
                var createdReference = await _powerPlatformClient.CreateConnectionReferenceAsync(reference);
                
                _logger.LogInformation("Successfully created connection reference: {ReferenceName} with ID: {ReferenceId}", 
                    createdReference.Name, createdReference.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create connection reference: {ReferenceName}", reference.Name);
            }
        }
    }

    private ConnectionReference CreateSharedConnectionReference(ConnectionReference templateReference)
    {
        var namingScheme = _connectionReferenceSettings.NamingScheme;
        var connectorName = templateReference.ConnectorDisplayName.Replace(" ", "");
        
        return new ConnectionReference
        {
            Name = $"{namingScheme.Prefix}{connectorName}{namingScheme.Suffix}",
            DisplayName = $"{templateReference.ConnectorDisplayName} - Shared Connection",
            ConnectorId = templateReference.ConnectorId,
            ConnectorDisplayName = templateReference.ConnectorDisplayName,
            IsManaged = false
        };
    }
}
