using Newtonsoft.Json;
using PowerPlatform.Tools.ConnectionReferences.Models;
using System.Text;

namespace PowerPlatform.Tools.ConnectionReferences.Services;

public class AnalysisOutputService : IAnalysisOutputService
{
    public async Task OutputTableAsync(AnalysisResult analysisResult, string? outputPath)
    {
        var output = new StringBuilder();

        output.AppendLine($"=== FLOW AND CONNECTION REFERENCE ANALYSIS FOR '{analysisResult.SolutionName}' ===");
        output.AppendLine();

        if (analysisResult.Flows.Count == 0)
        {
            output.AppendLine("No flows found in the solution.");
        }
        else
        {
            output.AppendLine($"{"Flow ID",-38} | {"Flow Name",-25} | {"Conn Ref ID",-38} | {"Conn Ref Logical Name",-50} | {"Provider",-35} | {"Connection ID",-38}");
            output.AppendLine(new string('-', 235));

            foreach (var flow in analysisResult.Flows)
            {
                if (flow.ConnectionReferences.Count == 0)
                {
                    output.AppendLine($"{flow.FlowId,-38} | {TruncateString(flow.FlowName, 25),-25} | {"(No connection references)",-38} | {"",-50} | {"",-35} | {"",-38}");
                    continue;
                }

                bool firstRef = true;
                foreach (var connRef in flow.ConnectionReferences)
                {
                    string flowIdDisplay = firstRef ? flow.FlowId : "";
                    string flowNameDisplay = firstRef ? TruncateString(flow.FlowName, 25) : "";

                    output.AppendLine($"{flowIdDisplay,-38} | {flowNameDisplay,-25} | {connRef.ConnectionReferenceId,-38} | {connRef.LogicalName,-50} | {TruncateString(connRef.Provider, 35),-35} | {connRef.ConnectionId,-38}");

                    firstRef = false;
                }

                if (flow.ConnectionReferences.Count > 1)
                {
                    output.AppendLine(new string('-', 235));
                }
            }
        }

        output.AppendLine();
        output.AppendLine("=== SUMMARY ===");
        output.AppendLine($"Total Flows: {analysisResult.Flows.Count}");
        output.AppendLine($"Total Connection References: {analysisResult.Flows.Sum(f => f.ConnectionReferences.Count)}");

        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, output.ToString());
            Console.WriteLine($"Table output saved to: {outputPath}");
        }
        else
        {
            Console.Write(output.ToString());
        }
    }

    public async Task OutputVerticalAsync(AnalysisResult analysisResult, string? outputPath)
    {
        var output = new StringBuilder();

        output.AppendLine($"=== FLOW AND CONNECTION REFERENCE ANALYSIS FOR '{analysisResult.SolutionName}' ===");
        output.AppendLine();

        if (analysisResult.Flows.Count == 0)
        {
            output.AppendLine("No flows found in the solution.");
        }
        else
        {
            foreach (var flow in analysisResult.Flows)
            {
                output.AppendLine($"Flow: {flow.FlowName}");
                output.AppendLine($"  ID: {flow.FlowId}");

                if (flow.ConnectionReferences.Count == 0)
                {
                    output.AppendLine($"  Connection References: None");
                }
                else
                {
                    output.AppendLine($"  Connection References ({flow.ConnectionReferences.Count}):");
                    foreach (var connRef in flow.ConnectionReferences)
                    {
                        output.AppendLine($"    - Logical Name: {connRef.LogicalName}");
                        output.AppendLine($"      Connection Reference ID: {connRef.ConnectionReferenceId}");
                        output.AppendLine($"      Provider: {connRef.Provider}");
                        output.AppendLine($"      Connection ID: {connRef.ConnectionId}");
                        output.AppendLine();
                    }
                }
                output.AppendLine(new string('-', 80));
                output.AppendLine();
            }
        }

        output.AppendLine("=== SUMMARY ===");
        output.AppendLine($"Total Flows: {analysisResult.Flows.Count}");
        output.AppendLine($"Total Connection References: {analysisResult.Flows.Sum(f => f.ConnectionReferences.Count)}");

        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, output.ToString());
            Console.WriteLine($"Vertical output saved to: {outputPath}");
        }
        else
        {
            Console.Write(output.ToString());
        }
    }

    public async Task OutputCsvAsync(AnalysisResult analysisResult, string? outputPath)
    {
        var output = new StringBuilder();

        output.AppendLine("FlowId,FlowName,ConnectionReferenceId,LogicalName,Provider,ConnectionId");

        foreach (var flow in analysisResult.Flows)
        {
            if (flow.ConnectionReferences.Count == 0)
            {
                output.AppendLine($"\"{flow.FlowId}\",\"{EscapeCsv(flow.FlowName)}\",\"\",\"\",\"\",\"\"");
            }
            else
            {
                foreach (var connRef in flow.ConnectionReferences)
                {
                    output.AppendLine($"\"{flow.FlowId}\",\"{EscapeCsv(flow.FlowName)}\",\"{connRef.ConnectionReferenceId}\",\"{EscapeCsv(connRef.LogicalName)}\",\"{EscapeCsv(connRef.Provider)}\",\"{connRef.ConnectionId}\"");
                }
            }
        }

        string csvContent = output.ToString();

        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, csvContent);
            Console.WriteLine($"CSV output saved to: {outputPath}");
        }
        else
        {
            Console.Write(csvContent);
        }
    }

    public async Task OutputJsonAsync(AnalysisResult analysisResult, string? outputPath)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Include
        };

        var jsonOutput = new
        {
            SolutionName = analysisResult.SolutionName,
            AnalysisDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Summary = new
            {
                TotalFlows = analysisResult.Flows.Count,
                TotalConnectionReferences = analysisResult.Flows.Sum(f => f.ConnectionReferences.Count),
                FlowsWithConnectionReferences = analysisResult.Flows.Count(f => f.ConnectionReferences.Count > 0),
                FlowsWithoutConnectionReferences = analysisResult.Flows.Count(f => f.ConnectionReferences.Count == 0)
            },
            Flows = analysisResult.Flows.Select(f => new
            {
                FlowId = f.FlowId,
                FlowName = f.FlowName,
                ConnectionReferenceCount = f.ConnectionReferences.Count,
                ConnectionReferences = f.ConnectionReferences.Select(cr => new
                {
                    ConnectionReferenceId = cr.ConnectionReferenceId,
                    LogicalName = cr.LogicalName,
                    Provider = cr.Provider,
                    ConnectionId = cr.ConnectionId
                }).ToArray()
            }).ToArray()
        };

        string jsonContent = JsonConvert.SerializeObject(jsonOutput, jsonSettings);

        if (outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, jsonContent);
            Console.WriteLine($"JSON output saved to: {outputPath}");
        }
        else
        {
            Console.Write(jsonContent);
        }
    }

    public string TruncateString(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        return input.Length <= maxLength ? input : input.Substring(0, maxLength - 3) + "...";
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Replace("\"", "\"\"");
    }
}
