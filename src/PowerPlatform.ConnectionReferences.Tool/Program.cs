using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerPlatform.ConnectionReferences.Tool.Services;

namespace PowerPlatform.ConnectionReferences.Tool;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var connectionReferenceService = host.Services.GetRequiredService<IConnectionReferenceService>();
        
        try
        {
            logger.LogInformation("Starting Power Platform Connection References Tool");
            
            // TODO: Parse command line arguments for solution name/ID
            var solutionName = "YourSolutionName"; // This will come from args
            
            await connectionReferenceService.ProcessSolutionAsync(solutionName);
            
            logger.LogInformation("Tool execution completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during tool execution");
            Environment.Exit(1);
        }
    }
    
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddUserSecrets<Program>();
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<PowerPlatformSettings>(context.Configuration.GetSection("PowerPlatform"));
                services.AddScoped<IConnectionReferenceService, ConnectionReferenceService>();
                services.AddScoped<IPowerPlatformClient, PowerPlatformClient>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
}
