# Power Platform Connection References Best Practices Tool

A console application for managing connection references in Power Platform solutions. This tool helps standardize connection references across Cloud Flows by creating shared connection references with consistent naming conventions.

## Features

- **Analyze** solutions to identify connection reference usage
- **Create** standardized connection references with configurable naming schemes
- **Update** Cloud Flows to use shared connection references  
- **Clean up** old connection references
- **Generate** deployment settings JSON for ALM processes
- **Dry-run mode** for safe testing

## Quick Start

1. Configure your environment in `appsettings.json`
2. Run commands to manage your solution's connection references

## Commands

### Analyze Solution
Analyze a solution to see what connection references are used and what would be created:
```bash
dotnet run -- analyze --solution "YourSolutionName" [--format table|vertical|csv|json] [--output "filename"]
```

**Output Formats:**
- `table` (default) - Tabular format in terminal
- `vertical` - Tree-like format for better readability
- `csv` - Comma-separated values for Excel/analysis
- `json` - Structured JSON for automation/scripting

**Examples:**
```bash
# Default table format
dotnet run -- analyze --solution "MyFlows"

# Vertical format for readability
dotnet run -- analyze --solution "MyFlows" --format vertical

# Export to CSV for analysis
dotnet run -- analyze --solution "MyFlows" --format csv --output "analysis.csv"

# JSON for automation
dotnet run -- analyze --solution "MyFlows" --format json --output "analysis.json"
```

**Analyze Output Details**:
The analyze command provides comprehensive information about each flow's connection references:
- **Flow ID**: Unique identifier of the Power Automate flow
- **Flow Name**: Display name of the flow
- **Connection Reference ID**: Unique ID of the connection reference
- **Logical Name**: The logical name used to reference the connection in the flow
- **Provider**: The connector type (e.g., `shared_commondataserviceforapps`, `shared_azuread`)
- **Connection ID**: The actual connection being used

This information helps identify which flows need to be updated and what standardized connection references should be created.

### Create Connection References
Create new shared connection references for all connectors found in the solution:
```bash
dotnet run -- create-refs --solution "YourSolutionName" [--dry-run]
```

### Update Flows
Update Cloud Flows to use the new shared connection references:
```bash
dotnet run -- update-flows --solution "YourSolutionName" [--dry-run]
```

### Full Process
Run the complete process (create connection references + update flows):
```bash
dotnet run -- process --solution "YourSolutionName" [--dry-run]
```

### Generate Deployment Settings
Create a deployment settings JSON file for the solution's connection references:
```bash
dotnet run -- generate-deployment-settings --solution "YourSolutionName" --output "deploymentsettings.json"
```

## Deployment Settings Format

The generated deployment settings file includes descriptive placeholders for easy find-and-replace operations in ALM processes:

```json
{
  "EnvironmentVariables": [],
  "ConnectionReferences": [
    {
      "LogicalName": "prefix_connector_flowid",
      "ConnectionId": "{{REPLACE_WITH_CONNECTOR_CONNECTION_ID}}",
      "ConnectorId": "/providers/Microsoft.PowerApps/apis/shared_connector"
    }
  ]
}
```

**Placeholder Format**: Connection ID placeholders use the format `{{REPLACE_WITH_[CONNECTOR]_CONNECTION_ID}}` where `[CONNECTOR]` is the connector name in uppercase (e.g., `COMMONDATASERVICEFORAPPS`, `AZUREAD`, `SHAREPOINTONLINE`).

**ALM Usage Examples**:
- **PowerShell**: `$content -replace "{{REPLACE_WITH_DATAVERSE_CONNECTION_ID}}", $connectionId`
- **Azure DevOps**: Use File Transform task with variable replacement
- **Find/Replace**: Search for `{{REPLACE_WITH_` pattern and replace with actual connection IDs

These placeholders make automated deployment processes much easier by providing clear, searchable tokens for connection ID replacement.

### Clean Up
Remove old unused connection references (dependency-aware - only deletes connection references not used by any flows):
```bash
dotnet run -- cleanup --solution "YourSolutionName" [--dry-run]
```

**Safety Features**:
- Analyzes all flows in the solution to identify connection reference dependencies
- Only removes connection references that are not referenced by any flow
- Provides detailed logging of what will be kept vs. deleted
- Shows which flows are using each connection reference before deletion

## Configuration

Configure the tool by editing `appsettings.json`:

```json
{
  "PowerPlatform": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id", 
    "ClientSecret": "your-client-secret",
    "DataverseUrl": "https://yourorg.crm.dynamics.com"
  },
  "ConnectionReferences": {
    "Prefix": "shared",
    "ProviderMappings": {
      "shared_azuread": {
        "connectionId": "your-connection-id",
        "connectorId": "/providers/Microsoft.PowerApps/apis/shared_azuread"
      },
      "shared_commondataservice": {
        "connectionId": "your-connection-id",
        "connectorId": "/providers/Microsoft.PowerApps/apis/shared_commondataservice"
      }
    }
  }
}
```

## Provider Mappings

The `ProviderMappings` section maps connector API names to their corresponding connection and connector IDs in your environment. You'll need to:

1. **connectionId**: The GUID of the existing connection you want to reference
2. **connectorId**: The full connector API path (usually starts with `/providers/Microsoft.PowerApps/apis/`)

To find these values, you can use the Power Platform admin center or query the Dataverse API directly.

## Common Options

- `--solution`: The unique name of the solution to process
- `--dry-run`: Preview changes without making modifications
- `--format`: Output format (table, vertical, csv, json) - analyze command only
- `--output`: Specify output file path

## Examples

```bash
# Safe analysis of a solution
dotnet run -- analyze --solution "MyCloudFlows"

# Analyze with vertical format for better readability
dotnet run -- analyze --solution "MyCloudFlows" --format vertical

# Export analysis to CSV
dotnet run -- analyze --solution "MyCloudFlows" --format csv --output "flow-analysis.csv"

# Test what would be created (no changes made)
dotnet run -- process --solution "MyCloudFlows" --dry-run

# Actually create and update everything
dotnet run -- process --solution "MyCloudFlows"

# Generate deployment settings for ALM
dotnet run -- generate-deployment-settings --solution "MyCloudFlows" --output "release/deploymentsettings.json"
```

## Troubleshooting

### Component Type Compatibility
The tool automatically handles different Dataverse environment versions by trying multiple component types when adding connection references to solutions:
- First attempts with component type 10132 (newer environments)
- Falls back to component type 10469 (older environments) if the first fails
- This ensures compatibility across different Power Platform environment versions

### Common Issues
- **"Invalid component type" warnings**: These are handled automatically and don't prevent successful operation
- **Authentication token expiry**: The tool uses token caching and will prompt for re-authentication when needed
- **Connection reference already exists**: The tool detects existing connection references and reuses them instead of creating duplicates

## Best Practices

- Always run with `--dry-run` first to preview changes
- Use consistent naming conventions for your connection reference prefix
- Keep your provider mappings up to date with your environment
- Generate deployment settings files for promoted solutions
- Regular cleanup helps maintain a tidy environment