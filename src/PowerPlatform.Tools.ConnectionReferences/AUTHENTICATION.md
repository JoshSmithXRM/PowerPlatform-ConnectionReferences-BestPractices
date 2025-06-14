# Authentication Methods

This tool supports 4 different authentication methods to connect to your Power Platform environment. Each method requires different fields to be configured in your `appsettings.json` file.

## 1. Service Principal (Recommended for Automation)

**Best for**: CI/CD pipelines, automation, unattended scenarios

**Required Fields**:
- `TenantId` - Your Azure AD tenant ID
- `AuthenticationMethod` - Set to `"ServicePrincipal"`
- `ClientId` - Your app registration client ID
- `ClientSecret` - Your app registration client secret
- `DataverseUrl` - Your Dataverse environment URL

**Configuration**:
```json
{
  "PowerPlatform": {
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "AuthenticationMethod": "ServicePrincipal",
    "ClientId": "your-app-registration-client-id",
    "ClientSecret": "your-app-registration-client-secret",
    "Username": "",
    "Password": "",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**Setup Requirements**:
1. Create an App Registration in Azure AD
2. Grant appropriate permissions to Dataverse
3. Create a client secret

## 2. Interactive (Recommended for Development)

**Best for**: Developer testing, one-off operations, scenarios requiring MFA

**Required Fields**:
- `TenantId` - Your Azure AD tenant ID
- `AuthenticationMethod` - Set to `"Interactive"`
- `DataverseUrl` - Your Dataverse environment URL
- `PublicClientId` - Public client ID (default provided)

**Optional Fields**: All others can be empty

**Configuration**:
```json
{
  "PowerPlatform": {
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "AuthenticationMethod": "Interactive",
    "ClientId": "",
    "ClientSecret": "",
    "Username": "",
    "Password": "",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**How it works**:
- Opens a browser window for authentication
- Supports MFA automatically
- Uses the Power Platform CLI public client ID by default
- Caches tokens for subsequent runs

## 3. Username/Password (Legacy - Not Recommended)

**Best for**: Legacy scenarios where interactive auth isn't possible

**Required Fields**:
- `TenantId` - Your Azure AD tenant ID
- `AuthenticationMethod` - Set to `"UsernamePassword"`
- `Username` - Your user principal name (email)
- `Password` - Your password
- `DataverseUrl` - Your Dataverse environment URL
- `PublicClientId` - Public client ID (default provided)

**Configuration**:
```json
{
  "PowerPlatform": {
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "AuthenticationMethod": "UsernamePassword",
    "ClientId": "",
    "ClientSecret": "",
    "Username": "user@yourdomain.com",
    "Password": "your-password",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**Limitations**:
- Does NOT support MFA
- Being deprecated by Microsoft
- Less secure than other methods
- Account must not have MFA enabled

## 4. Device Code

**Best for**: Environments where browser isn't available (headless servers, Docker containers)

**Required Fields**:
- `TenantId` - Your Azure AD tenant ID
- `AuthenticationMethod` - Set to `"DeviceCode"`
- `DataverseUrl` - Your Dataverse environment URL
- `PublicClientId` - Public client ID (default provided)

**Optional Fields**: All others can be empty

**Configuration**:
```json
{
  "PowerPlatform": {
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "AuthenticationMethod": "DeviceCode",
    "ClientId": "",
    "ClientSecret": "",
    "Username": "",
    "Password": "",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**How it works**:
1. Tool displays a URL and code
2. You open the URL in a browser (can be on different device)
3. Enter the code and authenticate
4. Tool continues after successful authentication

## Token Caching

For Interactive, Username/Password, and Device Code authentication methods, the tool automatically caches authentication tokens using MSAL token cache. This provides several benefits:

**Features**:
- **First run**: Requires full authentication (browser/credentials)
- **Subsequent runs**: Uses cached tokens automatically (no re-authentication needed)
- **Secure storage**: Cache is stored securely in the user profile using MSAL extensions
- **Cross-session persistence**: Cache persists between application runs and system restarts
- **Automatic refresh**: Tokens are automatically refreshed when possible before expiration
- **Multiple environments**: Separate caches maintained for different tenants/environments

**Cache Locations**:
- **Windows**: `%LOCALAPPDATA%\.IdentityService`
- **macOS**: `~/.IdentityService`
- **Linux**: `~/.IdentityService`

**Cache Behavior**:
- Service Principal authentication does NOT use token caching (always authenticates fresh)
- Interactive authentication caches tokens for the longest possible duration
- Cache is automatically cleared if tokens become invalid or corrupted
- First successful authentication after tool installation will establish the cache

**Troubleshooting Cache Issues**:
- Delete the cache directory to force fresh authentication
- Check that the user has write permissions to the cache location
- Ensure antivirus software isn't blocking cache file creation

## Troubleshooting Authentication Issues

### Service Principal Connection Permission Error

**Problem**: When using Service Principal authentication, you get a 403 Forbidden error or "ConnectionAuthorizationFailed" when trying to access connections, even though the service principal has appropriate Dataverse permissions.

**Error Examples**:
```
HTTP 403 Forbidden
{"error":"ConnectionAuthorizationFailed","message":"The connection is not authorized for the specified principal"}
```

**Root Cause**: Service principals require explicit permission to use connections, even if they have admin-level permissions in Dataverse.

**Solution**: Each connection must be explicitly shared with the service principal:

1. **In Power Platform Admin Center**:
   - Go to your environment → Resources → Connections
   - Find the connection(s) your flows use
   - Click on the connection → Share
   - Add your service principal as a user with "Can use" permission

2. **In Power Automate Portal**:
   - Go to Data → Connections
   - Click the "..." menu on each connection
   - Select "Share" 
   - Add your service principal with "Can use" permission

3. **Using PowerShell** (for bulk operations):
   ```powershell
   # Install Power Platform CLI if not already installed
   pac install latest
   
   # Authenticate as an admin
   pac auth create --url https://yourorg.crm.dynamics.com
   
   # Share connection with service principal
   pac connection share --connection-id "connection-id" --principal "service-principal-object-id" --role CanUse
   ```

**Prevention**: When creating new connections for flows that will be managed by service principals, always share them with the service principal immediately.

**Note**: This requirement exists because connections contain sensitive credential information, so Power Platform requires explicit permission grants rather than inheriting permissions from Dataverse roles.

## Field Reference

| Field | Service Principal | Interactive | Username/Password | Device Code |
|-------|------------------|-------------|-------------------|-------------|
| `TenantId` | ✅ Required | ✅ Required | ✅ Required | ✅ Required |
| `DataverseUrl` | ✅ Required | ✅ Required | ✅ Required | ✅ Required |
| `AuthenticationMethod` | ✅ Required | ✅ Required | ✅ Required | ✅ Required |
| `ClientId` | ✅ Required | ❌ Not used | ❌ Not used | ❌ Not used |
| `ClientSecret` | ✅ Required | ❌ Not used | ❌ Not used | ❌ Not used |
| `Username` | ❌ Not used | ❌ Not used | ✅ Required | ❌ Not used |
| `Password` | ❌ Not used | ❌ Not used | ✅ Required | ❌ Not used |
| `PublicClientId` | ❌ Not used | ⚙️ Optional* | ⚙️ Optional* | ⚙️ Optional* |

*Default value provided - only change if you have your own public client app registration

## Public Client ID

The default `PublicClientId` (`51f81489-12ee-4a9e-aaae-a2591f45987d`) is the official Power Platform CLI client ID provided by Microsoft. You can use your own public client app registration if needed.

## Security Best Practices

1. **Never commit secrets** to source control
2. **Use Service Principal** for automation scenarios
3. **Use Interactive** for development and testing
4. **Consider environment variables** for sensitive values in production
5. **Rotate secrets regularly** for Service Principal authentication
6. **Use .gitignore** to exclude appsettings.json from version control
