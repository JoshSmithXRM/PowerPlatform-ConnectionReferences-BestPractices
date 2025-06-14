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
    "Prefix": "new",
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
- **Service Principal 403/ConnectionAuthorizationFailed errors**: Service principals need explicit "Can use" permission on connections - see [AUTHENTICATION.md](src/PowerPlatform.Tools.ConnectionReferences/AUTHENTICATION.md#service-principal-connection-permission-error) for detailed solution

## Best Practices

### Understanding Connection References Architecture

Connection references create a layer of abstraction between your flows and actual connections:

```
Flow → Connection Reference → Connection
```

**Key concepts:**
- **Flow**: Your Power Automate workflow that needs to connect to external services
- **Connection Reference**: A logical pointer that can be redirected to different connections
- **Connection**: The actual authenticated connection to a service (SharePoint, SQL, etc.)

**Benefits of this architecture:**
- **Environment portability**: Change which connection a flow uses without modifying the flow
- **Deployment flexibility**: Point to different connections in Dev/Test/Prod environments
- **Throttling management**: Distribute load across multiple connections for the same service

### Security & Access Management

#### Environment Strategy for ALM
**Recommended environment strategy:**

```
Development (Unmanaged) → Test (Managed) → Production (Managed)
     ↑                        ↓                    ↓
Use this tool here     Deploy via pipeline  Deploy via pipeline
```

**Development Environment:**
- Use **unmanaged solutions** for flexibility
- Run this tool to standardize connection references
- Develop and test all functionality
- Generate deployment settings for promotion

**Test/Production Environments:**
- Receive **managed solutions only**
- Use deployment settings to configure connection references
- Never run development tools directly
- Maintain environment isolation and traceability

#### Use Least Privilege Principles
- **Service Principals**: Grant only the minimum Dataverse permissions needed for your automation scenarios
- **User Accounts**: Avoid using high-privilege admin accounts for day-to-day connection management
- **Connection Sharing**: Explicitly grant "Can use" permission rather than relying on broad access

#### Implement Shared Administrative Approach
**Recommendation**: Use a dedicated, shared administrator account for connection management.

**Why this matters:**
- Connections are **not automatically shared** with team members
- Individual user connections become **invisible and unusable** by other team members
- Personal connections create **deployment bottlenecks** and **knowledge silos**

**Implementation:**
1. Create a shared service account (e.g., `powerplatform-admin@yourorg.com`)
2. Use this account to create all shared connections
3. Share this account's credentials securely with the team (using tools like Azure Key Vault)
4. Document which connections belong to shared vs. personal use

### Operational Considerations

#### One Connection Reference Per Flow
**Recommendation**: Create a unique connection reference for each flow, even if they use the same service.

**Benefits:**
- **Granular throttling control**: If one flow gets throttled, others continue working
- **Independent scaling**: Point different flows to different connections as load increases
- **Easier troubleshooting**: Isolate connection issues to specific flows
- **Flexible deployment**: Different flows can use different connections in different environments

**Example scenario:**
```
❌ Bad: Multiple flows sharing one connection reference
Flow A ──┐
Flow B ──┼── Shared Connection Reference ── Connection
Flow C ──┘

✅ Good: Each flow has its own connection reference
Flow A ── Connection Reference A ── Connection 1
Flow B ── Connection Reference B ── Connection 2  
Flow C ── Connection Reference C ── Connection 3
```

#### Plan for Throttling and Scale
- **Monitor connection usage** and prepare to distribute load across multiple connections
- **Use connection references** to easily redirect flows to less-utilized connections
- **Consider peak usage times** when planning connection capacity
- **Document your connection topology** for operational teams

### Tool Usage Guidelines

#### Development and Testing Environment Focus
**⚠️ Important**: This tool is designed for **development and testing environments only**. 

**Recommended ALM workflow:**
1. **Development Environment**: Use this tool to standardize connection references in your dev environment
2. **Export as Managed Solution**: Export your solution as a managed solution for deployment
3. **Generate Deployment Settings**: Use the `generate-deployment-settings` command to create configuration files
4. **Deploy via Pipelines**: Use Power Platform pipelines or Azure DevOps with deployment settings to deploy to higher environments
5. **Target Environment Setup**: Ensure connections exist and are properly shared in target environments before deployment

#### Never Run Directly in Production
**❌ Do NOT run this tool directly in production environments**

**Why this matters:**
- **Breaks ALM traceability**: Direct changes in production can't be tracked back to source environments
- **Creates environment drift**: Production becomes out of sync with your development environments  
- **Deployment conflicts**: Future deployments may fail or overwrite manual production changes
- **No rollback capability**: Manual changes are harder to undo if issues arise

**The only exception**: If you're using unmanaged solutions (not recommended), but this creates significant ALM risks and environment synchronization challenges.

#### Proper Production Deployment Process
1. **Develop in Dev**: Use this tool in development environment to create standardized connection references
2. **Test in Test**: Deploy the managed solution to test environment using deployment settings
3. **Validate in Test**: Ensure all flows work correctly with the new connection references
4. **Deploy to Production**: Use the same managed solution and deployment settings for production
5. **Monitor**: Verify all flows are functioning correctly in production

#### Always Test First
- **Run with `--dry-run`** to preview all changes before applying them
- **Test in development environments** before deploying to higher environments
- **Verify authentication** works before running bulk operations
- **Validate deployment settings** in test environments before production deployment

#### Maintain Consistency
- **Use consistent naming conventions** for your connection reference prefix across environments
- **Keep provider mappings up to date** with your environment configuration
- **Document your naming standards** for the team
- **Version control your configuration** files (excluding secrets)

#### Integrate with ALM Processes
- **Generate deployment settings** files for solutions being promoted (`generate-deployment-settings` command)
- **Use managed solutions** for all deployments to test and production environments
- **Leverage Power Platform pipelines** or Azure DevOps for automated deployments
- **Use Service Principal authentication** for CI/CD pipelines
- **Store deployment settings** in source control alongside your solution
- **Environment-specific configuration**: Maintain separate deployment settings for each target environment
- **Regular cleanup** in development helps maintain a tidy environment (use `cleanup` command)

### Known Limitations

#### Multi-Environment Connection Scenarios
**Limitation**: If a flow connects to multiple Dataverse environments, this tool will currently point both connection references to the same connection.

**Example problematic scenario:**
```
Flow connects to:
├── Environment A (Dev)
└── Environment B (Prod)

Current behavior: Both connection references → Same connection
Desired behavior: Each connection reference → Different connection
```

**Workarounds:**
1. **Manual adjustment**: After running the tool, manually update connection references for multi-environment flows
2. **Separate solutions**: Keep flows that span environments in separate solutions
3. **Flow redesign**: Consider if cross-environment flows can be split into single-environment flows with alternative integration patterns

**When this matters:**
- Cross-environment data synchronization flows
- Flows that read from one environment and write to another
- Multi-tenant scenarios where flows span different Dataverse instances

#### Provider Detection Edge Cases
- **Custom connectors**: May require manual provider mapping configuration
- **Premium connectors**: Ensure proper licensing before creating connection references
- **Regional connectors**: Some connectors have region-specific providers that may need manual mapping

### Troubleshooting Common Scenarios

#### "Connection not found" Errors
- Verify the connection exists and is shared with the appropriate accounts
- Check that the connection is in the same environment as your solution
- Ensure service principals have "Can use" permission on connections

#### Deployment Failures
- Validate that target environment has the required connectors enabled
- Confirm that connection references in deployment settings point to existing connections
- Check that the deploying account has permission to create connection references

#### Performance Issues
- Monitor for connection throttling in high-usage scenarios
- Consider distributing flows across multiple connections for the same service
- Review connection reference topology for optimization opportunities