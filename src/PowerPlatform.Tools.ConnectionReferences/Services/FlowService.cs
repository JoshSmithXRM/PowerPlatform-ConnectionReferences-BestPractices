using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;
using System.Text;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class FlowService : IFlowService
{
    private readonly AppSettings _settings;
    private readonly IDataverseService _dataverseService;

    public FlowService(AppSettings settings, IDataverseService dataverseService)
    {
        _settings = settings;
        _dataverseService = dataverseService;
    }

    private string BuildApiUrl(string endpoint)
    {
        var baseUrl = _settings.PowerPlatform.DataverseUrl.TrimEnd('/');
        return $"{baseUrl}/api/data/v9.2/{endpoint.TrimStart('/')}";
    }

    public async Task<List<JObject>> GetCloudFlowsInSolutionAsync(HttpClient httpClient, string solutionName)
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
            </link-entity>        </entity></fetch>"; var requestUri = BuildApiUrl($"workflows?fetchXml={Uri.EscapeDataString(fetchXml)}");
        return await _dataverseService.GetAllPagesAsync(httpClient, requestUri);
    }

    public FlowInfo? ExtractFlowInfo(JObject flow)
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

    public List<ConnectionReference> GetConnectionReferences(string clientDataJson)
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

    public async Task<List<string>> UpdateFlowConnectionReferencesAsync(HttpClient httpClient, FlowInfo flow, Dictionary<string, string> newConnRefLogicalNames, ProcessingStats stats, bool dryRun)
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

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), BuildApiUrl($"workflows({flow.Id})"))
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

    public Dictionary<string, List<string>> BuildConnectionReferenceDependencyMap(List<JObject> flows)
    {
        var dependencyMap = new Dictionary<string, List<string>>();

        foreach (var flow in flows)
        {
            var flowInfo = ExtractFlowInfo(flow);
            if (flowInfo == null) continue;

            var connectionRefs = GetConnectionReferences(flowInfo.ClientData);

            foreach (var connRef in connectionRefs)
            {
                if (!string.IsNullOrEmpty(connRef.LogicalName))
                {
                    if (!dependencyMap.ContainsKey(connRef.LogicalName))
                    {
                        dependencyMap[connRef.LogicalName] = new List<string>();
                    }
                    dependencyMap[connRef.LogicalName].Add(flowInfo.Name);
                }
            }
        }

        return dependencyMap;
    }
}
