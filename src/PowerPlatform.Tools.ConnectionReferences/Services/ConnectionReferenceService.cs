using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;
using System.Text;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class ConnectionReferenceService : IConnectionReferenceService
{
    private readonly AppSettings _settings;
    private readonly IDataverseService _dataverseService;

    public ConnectionReferenceService(AppSettings settings, IDataverseService dataverseService)
    {
        _settings = settings;
        _dataverseService = dataverseService;
    }

    public async Task<string?> CreateConnectionReferenceAsync(HttpClient httpClient, string logicalName, string displayName, string connectionId, string connectorId)
    {
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

    public async Task<string?> QueryConnectionReferenceIdAsync(HttpClient httpClient, string logicalName)
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

    public async Task<List<ConnectionReferenceInfo>> GetConnectionReferencesInSolutionAsync(HttpClient httpClient, string solutionName)
    {
        var fetchXml = $@"
        <fetch>
          <entity name='connectionreference'>
            <attribute name='connectionreferenceid'/>
            <attribute name='connectionreferencelogicalname'/>
            <attribute name='connectionreferencedisplayname'/>
            <attribute name='connectionid'/>
            <attribute name='connectorid'/>
            <link-entity name='solutioncomponent' from='objectid' to='connectionreferenceid' link-type='inner'>
                <link-entity name='solution' from='solutionid' to='solutionid' link-type='inner'>
                    <filter>
                        <condition attribute='uniquename' operator='eq' value='{solutionName}'/>
                    </filter>
                </link-entity>
            </link-entity>
          </entity>
        </fetch>";

        var requestUri = $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/connectionreferences?fetchXml={Uri.EscapeDataString(fetchXml)}";
        var results = await _dataverseService.GetAllPagesAsync(httpClient, requestUri);

        return results.Select(cr => new ConnectionReferenceInfo
        {
            Id = cr["connectionreferenceid"]?.ToString() ?? "",
            LogicalName = cr["connectionreferencelogicalname"]?.ToString() ?? "",
            DisplayName = cr["connectionreferencedisplayname"]?.ToString() ?? "",
            ConnectionId = cr["connectionid"]?.ToString() ?? "",
            ConnectorId = cr["connectorid"]?.ToString() ?? ""
        }).ToList();
    }

    public async Task<bool> DeleteConnectionReferenceAsync(HttpClient httpClient, ConnectionReferenceInfo connectionRef)
    {
        try
        {
            var resp = await httpClient.DeleteAsync($"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/connectionreferences({connectionRef.Id})");

            if (!resp.IsSuccessStatusCode)
            {
                var errorBody = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] Failed to delete connection reference '{connectionRef.LogicalName}' (ID: {connectionRef.Id})");
                Console.WriteLine($"[ERROR] Status: {resp.StatusCode}, Response: {errorBody}");
                return false;
            }

            Console.WriteLine($"[DELETE] Successfully deleted connection reference '{connectionRef.LogicalName}' (ID: {connectionRef.Id})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Exception deleting connection reference '{connectionRef.LogicalName}': {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddConnectionReferenceToSolutionAsync(HttpClient httpClient, string connRefId, string logicalName, string solutionName)
    {
        Console.WriteLine($"[DEBUG] Adding connection reference to solution - ID: {connRefId}, LogicalName: {logicalName}");

        if (await TryAddWithComponentType(httpClient, connRefId, logicalName, solutionName, 10132))
            return true;

        Console.WriteLine($"[INFO] Retrying with alternative component type for '{logicalName}'");
        return await TryAddWithComponentType(httpClient, connRefId, logicalName, solutionName, 10469);
    }

    public async Task<ConnectionReferenceResult?> ProcessConnectionReferenceForProviderAsync(HttpClient httpClient, FlowInfo flow, string provider, ProcessingStats stats, bool dryRun, string solutionName)
    {
        Console.WriteLine($"[INFO] {flow.Name}: Processing provider '{provider}'");

        if (!_settings.ConnectionReferences.ProviderMappings.TryGetValue(provider, out var mapping))
        {
            Console.WriteLine($"[WARN] {flow.Name}: No mapping for provider '{provider}', skipping.");
            return null;
        }

        var logicalName = _dataverseService.GenerateLogicalName(provider, flow.Id);
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

    private async Task<bool> TryAddWithComponentType(HttpClient httpClient, string connRefId, string logicalName, string solutionName, int componentType)
    {
        var payload = new JObject
        {
            ["ComponentId"] = connRefId,
            ["ComponentType"] = componentType,
            ["SolutionUniqueName"] = solutionName,
            ["AddRequiredComponents"] = false
        };

        Console.WriteLine($"[DEBUG] Trying component type {componentType} for '{logicalName}'");

        var resp = await httpClient.PostAsync(
            $"{_settings.PowerPlatform.DataverseUrl}/api/data/v9.2/AddSolutionComponent",
            new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
        );

        if (resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[ADD] Successfully added connection reference '{logicalName}' to solution '{solutionName}' using component type {componentType}");
            return true;
        }

        var errorBody = await resp.Content.ReadAsStringAsync();
        Console.WriteLine($"[DEBUG] Component type {componentType} failed for '{logicalName}': {resp.StatusCode} - {errorBody}");
        return false;
    }
}
