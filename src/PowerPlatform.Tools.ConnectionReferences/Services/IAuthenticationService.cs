namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IAuthenticationService
{
    Task<HttpClient> GetAuthenticatedHttpClientAsync();
}
