namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IDeploymentSettingsService
{
    Task GenerateDeploymentSettingsAsync(string solutionName, string outputPath);
}
