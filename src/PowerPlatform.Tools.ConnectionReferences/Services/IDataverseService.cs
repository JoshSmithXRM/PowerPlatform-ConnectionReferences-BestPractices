using Newtonsoft.Json.Linq;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IDataverseService
{
    Task<HttpClient> GetAuthenticatedHttpClientAsync();
    Task<List<JObject>> GetAllPagesAsync(HttpClient httpClient, string requestUri);
    string GenerateLogicalName(string provider, string flowId);
    string Sanitize(string input);
}
