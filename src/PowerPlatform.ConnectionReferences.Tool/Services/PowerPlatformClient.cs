using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PowerPlatform.ConnectionReferences.Tool.Models;
using System.Net.Http.Headers;
using System.Text;

namespace PowerPlatform.ConnectionReferences.Tool.Services;

public class PowerPlatformClient : IPowerPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PowerPlatformClient> _logger;
    private readonly PowerPlatformSettings _settings;

    public PowerPlatformClient(ILogger<PowerPlatformClient> logger, IOptions<PowerPlatformSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri($"{_settings.EnvironmentUrl}/api/data/v9.2/");
    }

    public async Task<Solution?> GetSolutionByNameAsync(string solutionName)
    {
        try
        {
            await AuthenticateAsync();
            
            var filter = $"uniquename eq '{solutionName}'";
            var select = "solutionid,uniquename,friendlyname,version";
            var response = await _httpClient.GetAsync($"solutions?$filter={filter}&$select={select}");
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            
            if (result?.value != null && result.value.Count > 0)
            {
                var solutionData = result.value[0];
                return new Solution
                {
                    Id = solutionData.solutionid,
                    Name = solutionData.uniquename,
                    DisplayName = solutionData.friendlyname,
                    Version = solutionData.version
                };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting solution by name: {SolutionName}", solutionName);
            throw;
        }
    }

    public async Task<List<CloudFlow>> GetCloudFlowsInSolutionAsync(string solutionId)
    {
        try
        {
            await AuthenticateAsync();
            
            // Query for workflows (CloudFlows) in the solution
            var filter = $"_solutionid_value eq '{solutionId}' and category eq 5"; // Category 5 = Modern Flow
            var select = "workflowid,name,friendlyname";
            var response = await _httpClient.GetAsync($"workflows?$filter={filter}&$select={select}");
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            
            var cloudFlows = new List<CloudFlow>();
            
            if (result?.value != null)
            {
                foreach (var item in result.value)
                {
                    var cloudFlow = new CloudFlow
                    {
                        Id = item.workflowid,
                        Name = item.name,
                        DisplayName = item.friendlyname ?? item.name
                    };
                    
                    // TODO: Get connection references for this CloudFlow
                    // This would require parsing the CloudFlow definition or using additional API calls
                    
                    cloudFlows.Add(cloudFlow);
                }
            }
            
            return cloudFlows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CloudFlows for solution: {SolutionId}", solutionId);
            throw;
        }
    }

    public async Task<List<ConnectionReference>> GetConnectionReferencesInSolutionAsync(string solutionId)
    {
        try
        {
            await AuthenticateAsync();
            
            var filter = $"_solutionid_value eq '{solutionId}'";
            var select = "connectionreferenceid,connectionreferencelogicalname,connectionreferencedisplayname,connectorid,ismanaged";
            var response = await _httpClient.GetAsync($"connectionreferences?$filter={filter}&$select={select}");
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            
            var connectionReferences = new List<ConnectionReference>();
            
            if (result?.value != null)
            {
                foreach (var item in result.value)
                {
                    connectionReferences.Add(new ConnectionReference
                    {
                        Id = item.connectionreferenceid,
                        Name = item.connectionreferencelogicalname,
                        DisplayName = item.connectionreferencedisplayname,
                        ConnectorId = item.connectorid,
                        IsManaged = item.ismanaged ?? false
                    });
                }
            }
            
            return connectionReferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection references for solution: {SolutionId}", solutionId);
            throw;
        }
    }

    public async Task<ConnectionReference> CreateConnectionReferenceAsync(ConnectionReference connectionReference)
    {
        try
        {
            await AuthenticateAsync();
            
            var createData = new
            {
                connectionreferencelogicalname = connectionReference.Name,
                connectionreferencedisplayname = connectionReference.DisplayName,
                connectorid = connectionReference.ConnectorId
            };
            
            var json = JsonConvert.SerializeObject(createData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("connectionreferences", content);
            response.EnsureSuccessStatusCode();
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            
            connectionReference.Id = result.connectionreferenceid;
            return connectionReference;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating connection reference: {ReferenceName}", connectionReference.Name);
            throw;
        }
    }

    public async Task<List<ConnectionReference>> GetConnectionReferencesByConnectorAsync(string connectorId)
    {
        try
        {
            await AuthenticateAsync();
            
            var filter = $"connectorid eq '{connectorId}'";
            var select = "connectionreferenceid,connectionreferencelogicalname,connectionreferencedisplayname,connectorid,ismanaged";
            var response = await _httpClient.GetAsync($"connectionreferences?$filter={filter}&$select={select}");
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(content);
            
            var connectionReferences = new List<ConnectionReference>();
            
            if (result?.value != null)
            {
                foreach (var item in result.value)
                {
                    connectionReferences.Add(new ConnectionReference
                    {
                        Id = item.connectionreferenceid,
                        Name = item.connectionreferencelogicalname,
                        DisplayName = item.connectionreferencedisplayname,
                        ConnectorId = item.connectorid,
                        IsManaged = item.ismanaged ?? false
                    });
                }
            }
            
            return connectionReferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting connection references by connector: {ConnectorId}", connectorId);
            throw;
        }
    }

    private async Task AuthenticateAsync()
    {
        // TODO: Implement authentication using Azure AD/Service Principal
        // This is a placeholder - you'll need to implement proper OAuth2 flow
        // using Microsoft.Identity.Client or similar
        
        if (!_httpClient.DefaultRequestHeaders.Authorization?.Scheme?.Equals("Bearer") == true)
        {
            // For now, this is a placeholder
            // You'll need to implement proper authentication
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        // TODO: Implement actual OAuth2 token acquisition
        // This should use Microsoft.Identity.Client to get tokens for Dynamics 365
        throw new NotImplementedException("Authentication not yet implemented - please implement OAuth2 flow");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
