using Newtonsoft.Json.Linq;
using PowerPlatform.Tools.ConnectionReferences.Models;
using System.Text;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class DataverseService : IDataverseService
{
    private readonly IAuthenticationService _authService;
    private readonly AppSettings _settings;
    private HttpClient? _httpClient;

    public DataverseService(IAuthenticationService authService, AppSettings settings)
    {
        _authService = authService;
        _settings = settings;
    }

    public async Task<HttpClient> GetAuthenticatedHttpClientAsync()
    {
        if (_httpClient == null)
        {
            _httpClient = await _authService.GetAuthenticatedHttpClientAsync();
        }
        return _httpClient;
    }

    public async Task<List<JObject>> GetAllPagesAsync(HttpClient httpClient, string requestUri)
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
        }
        return results;
    }

    public string GenerateLogicalName(string provider, string flowId)
    {
        return $"{_settings.ConnectionReferences.Prefix}_{provider}_{Sanitize(flowId)}".ToLowerInvariant();
    }

    public string Sanitize(string input)
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
}
