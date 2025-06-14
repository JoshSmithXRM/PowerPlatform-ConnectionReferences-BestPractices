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
dotnet run -- analyze --solution "YourSolutionName"
```

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

### Clean Up
Remove old unused connection references:
```bash
dotnet run -- cleanup --solution "YourSolutionName" [--dry-run]
```

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
- `--output`: Specify output file path (for deployment settings)

## Examples

```bash
# Safe analysis of a solution
dotnet run -- analyze --solution "MyCloudFlows"

# Test what would be created (no changes made)
dotnet run -- process --solution "MyCloudFlows" --dry-run

# Actually create and update everything
dotnet run -- process --solution "MyCloudFlows"

# Generate deployment settings for ALM
dotnet run -- generate-deployment-settings --solution "MyCloudFlows" --output "release/deploymentsettings.json"
```

## Best Practices

- Always run with `--dry-run` first to preview changes
- Use consistent naming conventions for your connection reference prefix
- Keep your provider mappings up to date with your environment
- Generate deployment settings files for promoted solutions
- Regular cleanup helps maintain a tidy environment