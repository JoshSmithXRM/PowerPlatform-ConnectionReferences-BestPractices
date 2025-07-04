using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PowerPlatform.Tools.ConnectionReferences.Models;
using PowerPlatform.Tools.ConnectionReferences.Services;

namespace PowerPlatform.Tools.ConnectionReferences;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Power Platform Connection References Tool");

        var solutionOption = new Option<string>(
            name: "--solution",
            description: "The unique name of the solution to process")
        {
            IsRequired = true
        };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Preview changes without making modifications");
        var outputOption = new Option<string>(
            name: "--output",
            description: "Output file path");

        var formatOption = new Option<string>(
            name: "--format",
            description: "Output format: table, vertical, csv, json")
        {
            IsRequired = false
        };
        formatOption.SetDefaultValue("vertical");

        var analyzeCommand = new Command("analyze", "Analyze solution connection references");
        analyzeCommand.AddOption(solutionOption);
        analyzeCommand.AddOption(formatOption);
        analyzeCommand.AddOption(outputOption);
        rootCommand.AddCommand(analyzeCommand);
        analyzeCommand.SetHandler(async (solution, format, output) => await ExecuteCommand("analyze", solution, false, output, format), solutionOption, formatOption, outputOption);

        var createRefsCommand = new Command("create-refs", "Create new shared connection references");
        createRefsCommand.AddOption(solutionOption);
        createRefsCommand.AddOption(dryRunOption);
        rootCommand.AddCommand(createRefsCommand);
        createRefsCommand.SetHandler(async (solution, dryRun) => await ExecuteCommand("create-refs", solution, dryRun, null), solutionOption, dryRunOption);

        var updateFlowsCommand = new Command("update-flows", "Update flows to use shared connection references");
        updateFlowsCommand.AddOption(solutionOption);
        updateFlowsCommand.AddOption(dryRunOption);
        rootCommand.AddCommand(updateFlowsCommand);
        updateFlowsCommand.SetHandler(async (solution, dryRun) => await ExecuteCommand("update-flows", solution, dryRun, null), solutionOption, dryRunOption);

        var processCommand = new Command("process", "Full process: create connection references and update flows");
        processCommand.AddOption(solutionOption);
        processCommand.AddOption(dryRunOption);
        rootCommand.AddCommand(processCommand);
        processCommand.SetHandler(async (solution, dryRun) => await ExecuteCommand("process", solution, dryRun, null), solutionOption, dryRunOption);

        var generateCommand = new Command("generate-deployment-settings", "Generate deployment settings JSON");
        generateCommand.AddOption(solutionOption);
        generateCommand.AddOption(outputOption);
        rootCommand.AddCommand(generateCommand);
        generateCommand.SetHandler(async (solution, output) => await ExecuteCommand("generate-deployment-settings", solution, false, output), solutionOption, outputOption);
        var cleanupCommand = new Command("cleanup", "Remove old unused connection references");
        cleanupCommand.AddOption(solutionOption);
        cleanupCommand.AddOption(dryRunOption);
        rootCommand.AddCommand(cleanupCommand);
        cleanupCommand.SetHandler(async (solution, dryRun) => await ExecuteCommand("cleanup", solution, dryRun, null), solutionOption, dryRunOption);

        var addExistingRefsCommand = new Command("add-existing-refs", "Add existing connection references used by flows to the solution");
        addExistingRefsCommand.AddOption(solutionOption);
        addExistingRefsCommand.AddOption(dryRunOption);
        rootCommand.AddCommand(addExistingRefsCommand);
        addExistingRefsCommand.SetHandler(async (solution, dryRun) => await ExecuteCommand("add-existing-refs", solution, dryRun, null), solutionOption, dryRunOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task ExecuteCommand(string command, string solution, bool dryRun, string? output, string? format = null)
    {
        try
        {
            var config = LoadConfiguration();
            var serviceProvider = BuildServiceProvider(config);
            var processor = serviceProvider.GetRequiredService<ConnectionReferenceProcessor>();

            Console.WriteLine($"=== Power Platform Connection References Tool ===");
            Console.WriteLine($"Command: {command}");
            Console.WriteLine($"Solution: {solution}");
            if (command != "analyze")
                Console.WriteLine($"Dry Run: {dryRun}");
            if (!string.IsNullOrEmpty(format))
                Console.WriteLine($"Format: {format}");
            if (!string.IsNullOrEmpty(output))
                Console.WriteLine($"Output: {output}");
            Console.WriteLine();

            switch (command)
            {
                case "analyze":
                    if (!string.IsNullOrEmpty(format))
                    {
                        if (!Enum.TryParse<PowerPlatform.Tools.ConnectionReferences.Models.OutputFormat>(format, true, out var outputFormat))
                        {
                            Console.WriteLine($"Invalid format '{format}'. Valid options: table, vertical, csv, json");
                            Environment.Exit(1);
                        }
                        await processor.AnalyzeAsync(solution, outputFormat, output);
                    }
                    else
                    {
                        await processor.AnalyzeAsync(solution);
                    }
                    break;
                case "create-refs":
                    await processor.CreateConnectionReferencesAsync(solution, dryRun);
                    break;
                case "update-flows":
                    await processor.UpdateFlowsAsync(solution, dryRun);
                    break;
                case "process":
                    await processor.ProcessAsync(solution, dryRun);
                    break;
                case "generate-deployment-settings":
                    await processor.GenerateDeploymentSettingsAsync(solution, output ?? "deploymentsettings.json");
                    break;
                case "cleanup":
                    await processor.CleanupAsync(solution, dryRun);
                    break;
                case "add-existing-refs":
                    await processor.AddExistingConnectionReferencesAsync(solution, dryRun);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    static IConfiguration LoadConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
    }

    static IServiceProvider BuildServiceProvider(IConfiguration config)
    {
        var services = new ServiceCollection();

        services.AddSingleton(config);

        services.AddSingleton<AppSettings>(provider =>
        {
            var settings = new AppSettings();
            config.GetSection("PowerPlatform").Bind(settings.PowerPlatform);
            config.GetSection("ConnectionReferences").Bind(settings.ConnectionReferences);
            return settings;
        });

        services.AddSingleton<IAuthenticationService, AuthenticationService>(provider =>
        {
            var settings = provider.GetRequiredService<AppSettings>();
            return new AuthenticationService(settings.PowerPlatform);
        });

        services.AddSingleton<IDataverseService, DataverseService>();
        services.AddSingleton<IFlowService, FlowService>();
        services.AddSingleton<IConnectionReferenceService, ConnectionReferenceService>();
        services.AddSingleton<IAnalysisOutputService, AnalysisOutputService>();
        services.AddSingleton<IDeploymentSettingsService, DeploymentSettingsService>();

        services.AddSingleton<ConnectionReferenceProcessor>();

        return services.BuildServiceProvider();
    }
}
