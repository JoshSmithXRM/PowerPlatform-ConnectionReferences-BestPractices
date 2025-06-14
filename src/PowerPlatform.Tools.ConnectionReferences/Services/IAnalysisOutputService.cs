using PowerPlatform.Tools.ConnectionReferences.Models;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public interface IAnalysisOutputService
{
    Task OutputTableAsync(AnalysisResult analysisResult, string? outputPath);
    Task OutputVerticalAsync(AnalysisResult analysisResult, string? outputPath);
    Task OutputCsvAsync(AnalysisResult analysisResult, string? outputPath);
    Task OutputJsonAsync(AnalysisResult analysisResult, string? outputPath);
    string TruncateString(string input, int maxLength);
}
