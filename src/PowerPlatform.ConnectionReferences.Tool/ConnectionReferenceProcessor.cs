using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPlatform.ConnectionReferences.Tool.Models;
using PowerPlatform.ConnectionReferences.Tool.Services;
using System.Text;

namespace PowerPlatform.ConnectionReferences.Tool;

public class ConnectionReferenceProcessor
{
    private readonly AppSettings _settings;
    private readonly AuthenticationService _authService;
    private HttpClient? _httpClient;

    public ConnectionReferenceProcessor(IConfiguration config)
    {
        _settings = new AppSettings();
        config.GetSection("PowerPlatform").Bind(_settings.PowerPlatform);
        config.GetSection("ConnectionReferences").Bind(_settings.ConnectionReferences);
        
        _authService = new AuthenticationService(_settings.PowerPlatform);
    }    public async Task AnalyzeAsync(string solutionName)
    {
        var context = await InitializeContextAsync();
        var flows = await GetCloudFlowsInSolutionAsync(context, solutionName);
        
        Console.WriteLine($"[INFO] Found {flows.Count} cloud flows in solution '{solutionName}'");
        Console.WriteLine();
        
        if (flows.Count == 0)
        {
            Console.WriteLine("No flows found in the solution.");
            return;
        }

        // Print header
        Console.WriteLine("=== FLOW AND CONNECTION REFERENCE ANALYSIS ===");
        Console.WriteLine();        Console.WriteLine($"{"Flow ID",-38} | {"Flow Name",-25} | {"Conn Ref ID",-38} | {"Conn Ref Logical Name",-50} | {"Provider",-35} | {"Connection ID",-38}");
        Console.WriteLine(new string('-', 235));

        foreach (var flow in flows)
        {
            var flowInfo = ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = GetConnectionReferences(flowInfo.ClientData);
            
            if (connectionRefs.Count == 0)
            {
                Console.WriteLine($"{flowInfo.Id,-38} | {TruncateString(flowInfo.Name, 25),-25} | {"(No connection references)",-38} | {"",-50} | {"",-35} | {"",-38}");
                continue;
            }

            bool firstRef = true;
            foreach (var connRef in connectionRefs)
            {
                // Get detailed connection reference information
                var connRefDetails = await GetConnectionReferenceDetailsAsync(context, connRef.LogicalName);
                
                string flowIdDisplay = firstRef ? flowInfo.Id : "";                string flowNameDisplay = firstRef ? TruncateString(flowInfo.Name, 25) : "";
                
                Console.WriteLine($"{flowIdDisplay,-38} | {flowNameDisplay,-25} | {connRefDetails?.Id ?? "Unknown",-38} | {connRef.LogicalName,-50} | {TruncateString(connRef.ApiName, 35),-35} | {connRefDetails?.ConnectionId ?? "Not Set",-38}");
                
                firstRef = false;
            }
              if (connectionRefs.Count > 1)
            {
                Console.WriteLine(new string('-', 235));
            }
        }
        
        Console.WriteLine();
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Total Flows: {flows.Count}");
        Console.WriteLine($"Total Connection References: {flows.Sum(f => {
            var flowInfo = ExtractFlowInfo(f);
            return flowInfo != null ? GetConnectionReferences(flowInfo.ClientData).Count : 0;
        })}");
    }

    public async Task CreateConnectionReferencesAsync(string solutionName, bool dryRun)
    {
        var context = await InitializeContextAsync();
        var flows = await GetCloudFlowsInSolutionAsync(context, solutionName);
        var stats = new ProcessingStats();

        foreach (var flow in flows)
        {
            var flowInfo = ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = GetConnectionReferences(flowInfo.ClientData);
            
            foreach (var providerGroup in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName)).GroupBy(cr => cr.ApiName))
            {
                await ProcessConnectionReferenceForProviderAsync(context, flowInfo, providerGroup.Key, stats, dryRun, solutionName);
            }
        }

        PrintSummary(stats);
    }

    public async Task UpdateFlowsAsync(string solutionName, bool dryRun)
    {
        var context = await InitializeContextAsync();
        var flows = await GetCloudFlowsInSolutionAsync(context, solutionName);
        var stats = new ProcessingStats();

        foreach (var flow in flows)
        {
            var flowInfo = ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = GetConnectionReferences(flowInfo.ClientData);
            var newConnRefLogicalNames = new Dictionary<string, string>();

            foreach (var providerGroup in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName)).GroupBy(cr => cr.ApiName))
            {
                var logicalName = GenerateLogicalName(providerGroup.Key, flowInfo.Id);
                newConnRefLogicalNames[providerGroup.Key] = logicalName;
            }

            if (newConnRefLogicalNames.Any())
            {
                await UpdateFlowConnectionReferencesAsync(context, flowInfo, newConnRefLogicalNames, stats, dryRun);
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
        var context = await InitializeContextAsync();
        var flows = await GetCloudFlowsInSolutionAsync(context, solutionName);
        
        var deploymentSettings = new JObject
        {
            ["ConnectionReferences"] = new JObject()
        };

        foreach (var flow in flows)
        {
            var flowInfo = ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = GetConnectionReferences(flowInfo.ClientData);
            
            foreach (var connRef in connectionRefs.Where(cr => !string.IsNullOrEmpty(cr.ApiName)))
            {
                var logicalName = GenerateLogicalName(connRef.ApiName, flowInfo.Id);
                if (!deploymentSettings["ConnectionReferences"]!.HasValues || 
                    deploymentSettings["ConnectionReferences"]![logicalName] == null)
                {
                    deploymentSettings["ConnectionReferences"]![logicalName] = new JObject
                    {
                        ["LogicalName"] = logicalName,
                        ["ConnectionId"] = "",
                        ["ConnectorId"] = _settings.ConnectionReferences.ProviderMappings.GetValueOrDefault(connRef.ApiName)?.ConnectorId ?? ""
                    };
                }
            }
        }

        await File.WriteAllTextAsync(outputPath, deploymentSettings.ToString(Formatting.Indented));
        Console.WriteLine($"[INFO] Deployment settings written to {outputPath}");
    }

    public async Task CleanupAsync(string solutionName, bool dryRun)
    {
        Console.WriteLine("[INFO] Cleanup functionality not yet implemented");
        await Task.CompletedTask;
    }    private async Task<HttpClient> InitializeContextAsync()
    {
        if (_httpClient == null)
        {
            _httpClient = await _authService.GetAuthenticatedHttpClientAsync();
        }
        return _httpClient;
    }    private async Task<List<JObject>> GetCloudFlowsInSolutionAsync(HttpClient httpClient, string solutionName)
    {
        var fetchXml = $@"
        <fetch>
          <entity name='workflow'>
            <attribute name='name'/>
            <attribute name='workflowid'/>
            <attribute name='clientdata'/>
            <filter>
              <condition attribute='category' operator='eq' value='5'/>
            </filter>
            <link-entity name='solutioncomponent' from='objectid' to='workflowid' link-type='inner'>
                <link-entity name='solution' from='solutionid' to='solutionid' link-type='inner'>
                    <filter>
                        <condition attribute='uniquename' operator='eq' value='{solutionName}'/>
                    </filter>
                </link-entity>
            </link-entity>
        </entity></fetch>";

        var requestUri = $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/workflows?fetchXml={Uri.EscapeDataString(fetchXml)}";
        return await GetAllPagesAsync(httpClient, requestUri);
    }

    private async Task<List<JObject>> GetAllPagesAsync(HttpClient httpClient, string requestUri)
    {
        var results = new List<JObject>();

        while (!string.IsNullOrEmpty(requestUri))
        {
            var response = await httpClient.GetAsync(requestUri);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(content);
            results.AddRange(json["value"]!.Select(f => (JObject)f));
            requestUri = json["@odata.nextLink"]?.ToString() ?? string.Empty;
        }        return results;
    }

    private FlowInfo? ExtractFlowInfo(JObject flow)
    {
        var flowName = flow["name"]?.ToString();
        var flowId = flow["workflowid"]?.ToString();
        var clientDataJson = flow["clientdata"]?.ToString();

        if (string.IsNullOrEmpty(clientDataJson) || string.IsNullOrEmpty(flowName) || string.IsNullOrEmpty(flowId))
        {
            Console.WriteLine($"[SKIP] {flowName}: No clientdata found or missing required fields.");
            return null;
        }

        return new FlowInfo
        {
            Id = flowId,
            Name = flowName,
            ClientData = clientDataJson
        };
    }

    private List<ConnectionReference> GetConnectionReferences(string clientDataJson)
    {
        var clientData = JObject.Parse(clientDataJson);
        var connRefs = clientData["properties"]?["connectionReferences"] as JObject;

        if (connRefs == null)
            return new List<ConnectionReference>();

        return connRefs.Properties().Select(p => new ConnectionReference
        {
            ReferenceKey = p.Name,
            LogicalName = p.Value["connection"]?["connectionReferenceLogicalName"]?.ToString() ?? "",
            ApiName = p.Value["api"]?["name"]?.ToString() ?? ""
        }).ToList();
    }

    private async Task<ConnectionReferenceResult?> ProcessConnectionReferenceForProviderAsync(
        HttpClient httpClient, FlowInfo flow, string provider, ProcessingStats stats, bool dryRun, string solutionName)
    {
        Console.WriteLine($"[INFO] {flow.Name}: Processing provider '{provider}'");

        if (!_settings.ConnectionReferences.ProviderMappings.TryGetValue(provider, out var mapping))
        {
            Console.WriteLine($"[WARN] {flow.Name}: No mapping for provider '{provider}', skipping.");
            return null;
        }

        var logicalName = GenerateLogicalName(provider, flow.Id);
        var displayName = $"{_settings.ConnectionReferences.Prefix}_{provider}_{flow.Id}";

        Console.WriteLine($"[INFO] {flow.Name}: Creating connection reference with logicalName='{logicalName}', displayName='{displayName}'");

        if (dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would create connection reference '{logicalName}' for provider '{provider}' for flow '{flow.Name}'.");
            stats.CreatedConnRefCount++;
            return new ConnectionReferenceResult { Id = Guid.NewGuid().ToString(), LogicalName = logicalName };
        }

        try
        {
            var connRefId = await CreateConnectionReferenceAsync(httpClient, logicalName, displayName, mapping.ConnectionId, mapping.ConnectorId);
            
            if (string.IsNullOrEmpty(connRefId))
            {
                stats.CreatedConnRefErrorCount++;
                return null;
            }

            var added = await AddConnectionReferenceToSolutionAsync(httpClient, connRefId, logicalName, solutionName);
            if (added)
            {
                stats.AddedToSolutionCount++;
            }
            else
            {
                stats.AddedToSolutionErrorCount++;
            }

            stats.CreatedConnRefCount++;
            Console.WriteLine($"[CREATE] {flow.Name}: Successfully created connection reference '{logicalName}' (ID: {connRefId}) for provider '{provider}'");
            
            return new ConnectionReferenceResult { Id = connRefId, LogicalName = logicalName };
        }
        catch (Exception ex)
        {
            stats.CreatedConnRefErrorCount++;
            Console.WriteLine($"[ERROR] {flow.Name}: Exception creating connection reference '{logicalName}' for provider '{provider}'");
            Console.WriteLine($"[ERROR] Exception: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> CreateConnectionReferenceAsync(HttpClient httpClient, string logicalName, string displayName, string connectionId, string connectorId)
    {
        // First check if it already exists
        var existingId = await QueryConnectionReferenceIdAsync(httpClient, logicalName);
        if (!string.IsNullOrEmpty(existingId))
        {
            Console.WriteLine($"[EXISTS] Connection reference '{logicalName}' already exists with ID: {existingId}");
            return existingId;
        }

        var payload = new Dictionary<string, object>
        {
            ["connectionid"] = connectionId,
            ["connectorid"] = connectorId,
            ["connectionreferencedisplayname"] = displayName,
            ["connectionreferencelogicalname"] = logicalName
        };

        var resp = await httpClient.PostAsync(
            $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/connectionreferences",
            new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
        );

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR] Failed to create connection reference '{logicalName}'");
            Console.WriteLine($"[ERROR] Status: {resp.StatusCode}, Response: {errorBody}");
            return null;
        }

        // Extract ID from response
        if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            if (resp.Headers.TryGetValues("OData-EntityId", out var entityIdValues))
            {
                var entityId = entityIdValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(entityId))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(entityId, @"connectionreferences\(([a-f0-9\-]+)\)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
        }

        var content = await resp.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                var created = JObject.Parse(content);
                return created["connectionreferenceid"]?.ToString();
            }
            catch (JsonReaderException) { }
        }

        return null;
    }

    private async Task<bool> AddConnectionReferenceToSolutionAsync(HttpClient httpClient, string connRefId, string logicalName, string solutionName)
    {
        var payload = new JObject
        {
            ["ComponentId"] = connRefId,
            ["ComponentType"] = 10469,
            ["SolutionUniqueName"] = solutionName,
            ["AddRequiredComponents"] = false
        };

        var resp = await httpClient.PostAsync(
            $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/AddSolutionComponent",
            new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
        );

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[ERROR] Failed to add connection reference '{logicalName}' to solution");
            Console.WriteLine($"[ERROR] Status: {resp.StatusCode}, Response: {errorBody}");
            return false;
        }

        Console.WriteLine($"[ADD] Successfully added connection reference '{logicalName}' to solution '{solutionName}'");
        return true;
    }

    private async Task<List<string>> UpdateFlowConnectionReferencesAsync(HttpClient httpClient, FlowInfo flow, Dictionary<string, string> newConnRefLogicalNames, ProcessingStats stats, bool dryRun)
    {
        Console.WriteLine($"[INFO] {flow.Name}: Updating flow to use {newConnRefLogicalNames.Count} new connection references");

        var oldConnectionRefs = new List<string>();
        var clientData = JObject.Parse(flow.ClientData);
        var connRefsObj = clientData["properties"]?["connectionReferences"] as JObject;

        if (connRefsObj == null)
            return oldConnectionRefs;

        foreach (var prop in connRefsObj.Properties())
        {
            var apiName = prop.Value["api"]?["name"]?.ToString();
            if (apiName != null && newConnRefLogicalNames.ContainsKey(apiName))
            {
                var oldLogicalName = prop.Value["connection"]?["connectionReferenceLogicalName"]?.ToString();
                if (!string.IsNullOrEmpty(oldLogicalName))
                {
                    oldConnectionRefs.Add(oldLogicalName);
                    Console.WriteLine($"[INFO] {flow.Name}: Marking old connection reference '{oldLogicalName}' for deletion");
                }

                prop.Value["connection"]!["connectionReferenceLogicalName"] = newConnRefLogicalNames[apiName];
                Console.WriteLine($"[INFO] {flow.Name}: Updated connection reference from '{oldLogicalName}' to '{newConnRefLogicalNames[apiName]}'");
            }
        }

        if (dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would update flow '{flow.Name}' to use new connection references: {string.Join(", ", newConnRefLogicalNames.Values)}");
            stats.UpdatedFlowCount++;
            return oldConnectionRefs;
        }

        try
        {
            var updatePayload = new JObject
            {
                ["clientdata"] = clientData.ToString(Formatting.None)
            };

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/workflows({flow.Id})")
            {
                Content = new StringContent(updatePayload.ToString(), Encoding.UTF8, "application/json")
            };

            var resp = await httpClient.SendAsync(request);

            if (!resp.IsSuccessStatusCode)
            {
                stats.UpdatedFlowErrorCount++;
                var errorBody = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] {flow.Name}: Failed to update flow");
                Console.WriteLine($"[ERROR] Status: {resp.StatusCode}, Response: {errorBody}");
            }
            else
            {
                stats.UpdatedFlowCount++;
                Console.WriteLine($"[UPDATE] {flow.Name}: Successfully updated flow to use new connection references");
            }
        }
        catch (Exception ex)
        {
            stats.UpdatedFlowErrorCount++;
            Console.WriteLine($"[ERROR] {flow.Name}: Exception updating flow");
            Console.WriteLine($"[ERROR] Exception: {ex.GetType().Name}: {ex.Message}");
        }

        return oldConnectionRefs;
    }

    private async Task<string?> QueryConnectionReferenceIdAsync(HttpClient httpClient, string logicalName)
    {
        var queryUrl = $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/connectionreferences?$select=connectionreferenceid&$filter=connectionreferencelogicalname eq '{logicalName}'";
        var resp = await httpClient.GetAsync(queryUrl);

        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[ERROR] Failed to query for connection reference '{logicalName}'. Status: {resp.StatusCode}");
            return null;
        }

        var content = await resp.Content.ReadAsStringAsync();
        var result = JObject.Parse(content);
        var connRefs = result["value"] as JArray;

        if (connRefs == null || connRefs.Count == 0)
            return null;

        return connRefs[0]["connectionreferenceid"]?.ToString();
    }

    private string GenerateLogicalName(string provider, string flowId)
    {
        return $"{_settings.ConnectionReferences.Prefix}_{provider}_{Sanitize(flowId)}".ToLowerInvariant();
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
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

    private async Task<ConnectionReferenceDetails?> GetConnectionReferenceDetailsAsync(HttpClient httpClient, string logicalName)
    {
        try
        {
            var queryUrl = $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/connectionreferences?$select=connectionreferenceid,connectionreferencelogicalname,connectionid&$filter=connectionreferencelogicalname eq '{logicalName}'";
            var resp = await httpClient.GetAsync(queryUrl);

            if (!resp.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ERROR] Failed to query connection reference '{logicalName}'. Status: {resp.StatusCode}");
                return null;
            }

            var content = await resp.Content.ReadAsStringAsync();
            var result = JObject.Parse(content);
            var connRefs = result["value"] as JArray;

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

    private static string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return "";
        
        return input.Length <= maxLength ? input : input.Substring(0, maxLength - 3) + "...";
    }

    private class ConnectionReferenceDetails
    {
        public string Id { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }

    // ...existing code...
}
