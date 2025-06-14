# Authentication Methods

This tool supports 4 different authentication methods to connect to your Power Platform environment:

## 1. Service Principal (Recommended for Automation)

**Best for**: CI/CD pipelines, automation, unattended scenarios

**Configuration**:
```json
{
  "PowerPlatform": {
    "AuthenticationMethod": "ServicePrincipal",
    "TenantId": "your-tenant-id",
    "ClientId": "your-app-registration-client-id",
    "ClientSecret": "your-app-registration-client-secret",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/"
  }
}
```

**Setup Requirements**:
1. Create an App Registration in Azure AD
2. Grant appropriate permissions to Dataverse
3. Create a client secret

## 2. Interactive (Recommended for Development)

**Best for**: Developer testing, one-off operations, scenarios requiring MFA

**Configuration**:
```json
{
  "PowerPlatform": {
    "AuthenticationMethod": "Interactive",
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
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

**Configuration**:
```json
{
  "PowerPlatform": {
    "AuthenticationMethod": "UsernamePassword",
    "TenantId": "your-tenant-id",
    "Username": "user@yourdomain.com",
    "Password": "your-password",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**Limitations**:
- Does NOT support MFA
- Being deprecated by Microsoft
- Less secure than other methods

## 4. Device Code

**Best for**: Environments where browser isn't available (headless servers, Docker containers)

**Configuration**:
```json
{
  "PowerPlatform": {
    "AuthenticationMethod": "DeviceCode",
    "TenantId": "your-tenant-id",
    "DataverseUrl": "https://yourorg.crm.dynamics.com/",
    "PublicClientId": "51f81489-12ee-4a9e-aaae-a2591f45987d"
  }
}
```

**How it works**:
1. Tool displays a URL and code
2. You open the URL in a browser (can be on different device)
3. Enter the code and authenticate
4. Tool continues after successful authentication

## Public Client ID

The default `PublicClientId` (`51f81489-12ee-4a9e-aaae-a2591f45987d`) is the official Power Platform CLI client ID provided by Microsoft. You can use your own public client app registration if needed.

## Security Best Practices

1. **Never commit secrets** to source control
2. **Use Service Principal** for automation scenarios
3. **Use Interactive** for development and testing
4. **Consider environment variables** for sensitive values in production
5. **Rotate secrets regularly** for Service Principal authentication
