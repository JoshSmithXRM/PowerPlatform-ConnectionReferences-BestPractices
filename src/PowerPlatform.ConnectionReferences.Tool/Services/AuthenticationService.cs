using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using PowerPlatform.ConnectionReferences.Tool.Models;
using System.Net.Http.Headers;

namespace PowerPlatform.ConnectionReferences.Tool.Services;

public class AuthenticationService
{
    private readonly PowerPlatformSettings _settings;

    public AuthenticationService(PowerPlatformSettings settings)
    {
        _settings = settings;
    }

    public async Task<HttpClient> GetAuthenticatedHttpClientAsync()
    {
        var token = await GetAccessTokenAsync();

        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");

        return httpClient;
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var scopes = new[] { $"{_settings.DataverseUrl}/.default" };

        return _settings.AuthenticationMethod switch
        {
            AuthenticationMethod.ServicePrincipal => await GetServicePrincipalTokenAsync(scopes),
            AuthenticationMethod.Interactive => await GetInteractiveTokenAsync(scopes),
            AuthenticationMethod.UsernamePassword => await GetUsernamePasswordTokenAsync(scopes),
            AuthenticationMethod.DeviceCode => await GetDeviceCodeTokenAsync(scopes),
            _ => throw new NotSupportedException($"Authentication method {_settings.AuthenticationMethod} is not supported")
        };
    }

    private async Task<string> GetServicePrincipalTokenAsync(string[] scopes)
    {
        if (string.IsNullOrEmpty(_settings.ClientId) || string.IsNullOrEmpty(_settings.ClientSecret))
        {
            throw new InvalidOperationException("ClientId and ClientSecret are required for Service Principal authentication");
        }

        var app = ConfidentialClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithClientSecret(_settings.ClientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();

        var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
        return result.AccessToken;
    }

    private async Task<string> GetInteractiveTokenAsync(string[] scopes)
    {
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PowerPlatformConnectionRefTool");
        Directory.CreateDirectory(cacheDirectory);
        var cacheFile = Path.Combine(cacheDirectory, "msal_cache.dat");

        var app = PublicClientApplicationBuilder
            .Create(_settings.PublicClientId)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .WithRedirectUri("http://localhost")
            .Build();        // Enable token cache persistence
        var storageProperties = new StorageCreationPropertiesBuilder("msal_cache.dat", cacheDirectory)
            .Build();
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(app.UserTokenCache);

        try
        {

            var accounts = await app.GetAccountsAsync();
            if (accounts.Any())
            {
                Console.WriteLine("Using cached authentication...");
                var result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
                return result.AccessToken;
            }
        }
        catch (MsalUiRequiredException)
        {

        }


        Console.WriteLine("Opening browser for authentication...");
        var interactiveResult = await app.AcquireTokenInteractive(scopes)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync();
        return interactiveResult.AccessToken;
    }

    private async Task<string> GetUsernamePasswordTokenAsync(string[] scopes)
    {
        if (string.IsNullOrEmpty(_settings.Username) || string.IsNullOrEmpty(_settings.Password))
        {
            throw new InvalidOperationException("Username and Password are required for Username/Password authentication");
        }

        var app = PublicClientApplicationBuilder
            .Create(_settings.PublicClientId)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();

        var result = await app.AcquireTokenByUsernamePassword(scopes, _settings.Username, _settings.Password)
            .ExecuteAsync();

        return result.AccessToken;
    }

    private async Task<string> GetDeviceCodeTokenAsync(string[] scopes)
    {
        var app = PublicClientApplicationBuilder
            .Create(_settings.PublicClientId)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.TenantId}")
            .Build();

        var result = await app.AcquireTokenWithDeviceCode(scopes, deviceCodeResult =>
        {
            Console.WriteLine("=== DEVICE CODE AUTHENTICATION ===");
            Console.WriteLine($"Go to: {deviceCodeResult.VerificationUrl}");
            Console.WriteLine($"Enter code: {deviceCodeResult.UserCode}");
            Console.WriteLine("Waiting for authentication...");
            return Task.FromResult(0);
        }).ExecuteAsync();

        return result.AccessToken;
    }
}
