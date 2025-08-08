# 🔐 Variables and Secrets Reference

This document provides a comprehensive reference for all variables, secrets, and configuration required for the CaseZero CI/CD pipeline.

## Table of Contents

1. [GitHub Repository Secrets](#github-repository-secrets)
2. [GitHub Environment Secrets](#github-environment-secrets)
3. [Environment Variables](#environment-variables)
4. [Azure Configuration](#azure-configuration)
5. [Application Settings](#application-settings)
6. [Security Best Practices](#security-best-practices)

## GitHub Repository Secrets

These secrets are accessible across all workflows and environments.

### Azure Authentication

| Secret Name | Description | Required | Example Value |
|-------------|-------------|----------|---------------|
| `AZURE_CREDENTIALS_DEV` | Service principal JSON for development | ✅ | See [Service Principal JSON](#service-principal-json) |
| `AZURE_CREDENTIALS_PROD` | Service principal JSON for production | ✅ | See [Service Principal JSON](#service-principal-json) |

### Azure Static Web Apps

| Secret Name | Description | Required | How to Get |
|-------------|-------------|----------|------------|
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` | Deployment token for dev frontend | ✅ | Azure Portal > Static Web App > Overview |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PROD` | Deployment token for prod frontend | ✅ | Azure Portal > Static Web App > Overview |

### Resource Groups

| Secret Name | Description | Required | Example Value |
|-------------|-------------|----------|---------------|
| `AZURE_RESOURCE_GROUP_DEV` | Development resource group name | ✅ | `casezero-dev-rg` |
| `AZURE_RESOURCE_GROUP_PROD` | Production resource group name | ✅ | `casezero-prod-rg` |

### Notifications

| Secret Name | Description | Required | Example Value |
|-------------|-------------|----------|---------------|
| `TEAMS_WEBHOOK_URL` | Microsoft Teams webhook for notifications | ❌ | `https://outlook.office.com/webhook/...` |

## GitHub Environment Secrets

These secrets are specific to each environment and provide additional security isolation.

### Development Environment

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `DATABASE_CONNECTION_STRING` | Dev database connection | `Server=...;Database=...` |
| `JWT_SECRET_KEY` | JWT signing key | `your-development-jwt-secret` |
| `EMAIL_SMTP_PASSWORD` | Email service password | `smtp-password` |

### Production Environment

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `DATABASE_CONNECTION_STRING` | Prod database connection | `Server=...;Database=...` |
| `JWT_SECRET_KEY` | JWT signing key | `your-production-jwt-secret` |
| `EMAIL_SMTP_PASSWORD` | Email service password | `smtp-password` |
| `SSL_CERTIFICATE_PASSWORD` | SSL certificate password | `cert-password` |

## Environment Variables

### Workflow Environment Variables

These are defined in the workflow files and can be customized:

```yaml
env:
  # .NET Configuration
  DOTNET_VERSION: '8.0.x'
  
  # Node.js Configuration
  NODE_VERSION: '18'
  
  # Azure App Names
  AZURE_WEBAPP_NAME_DEV: 'casezero-dev'
  AZURE_WEBAPP_NAME_PROD: 'casezero-prod'
  
  # Build Configuration
  BUILD_CONFIGURATION: 'Release'
  
  # Test Configuration
  TEST_RESULTS_PATH: '**/TestResults/*.xml'
```

### Application Environment Variables

These are set in the Azure App Service configuration:

#### Development Environment
```yaml
ASPNETCORE_ENVIRONMENT: Development
WEBSITE_RUN_FROM_PACKAGE: '1'
JwtSettings__ExpirationDays: '7'
EmailSettings__SmtpServer: 'smtp.office365.com'
EmailSettings__SmtpPort: '587'
EmailSettings__UseSsl: 'true'
CasesBasePath: 'D:\home\site\wwwroot\cases'
```

#### Production Environment
```yaml
ASPNETCORE_ENVIRONMENT: Production
WEBSITE_RUN_FROM_PACKAGE: '1'
JwtSettings__ExpirationDays: '7'
EmailSettings__SmtpServer: 'smtp.office365.com'
EmailSettings__SmtpPort: '587'
EmailSettings__UseSsl: 'true'
CasesBasePath: 'D:\home\site\wwwroot\cases'
APPINSIGHTS_INSTRUMENTATIONKEY: '${applicationInsights.properties.InstrumentationKey}'
```

## Azure Configuration

### Service Principal JSON

The Azure credentials should be in this format:

```json
{
  "clientId": "12345678-1234-1234-1234-123456789012",
  "clientSecret": "your-client-secret",
  "subscriptionId": "12345678-1234-1234-1234-123456789012",
  "tenantId": "12345678-1234-1234-1234-123456789012",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
```

### Azure Key Vault References

For enhanced security, use Key Vault references in parameter files:

```json
{
  "sqlServerAdminLogin": {
    "reference": {
      "keyVault": {
        "id": "/subscriptions/{subscription-id}/resourceGroups/casezero-shared-rg/providers/Microsoft.KeyVault/vaults/casezero-keyvault"
      },
      "secretName": "sql-admin-login"
    }
  },
  "sqlServerAdminPassword": {
    "reference": {
      "keyVault": {
        "id": "/subscriptions/{subscription-id}/resourceGroups/casezero-shared-rg/providers/Microsoft.KeyVault/vaults/casezero-keyvault"
      },
      "secretName": "sql-admin-password"
    }
  }
}
```

## Application Settings

### Database Configuration

#### Connection Strings
```yaml
# Development
DefaultConnection: "Server=casezero-sql-dev.database.windows.net;Database=casezero-db;User Id=casezero-admin;Password={password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Production
DefaultConnection: "Server=casezero-sql-prod.database.windows.net;Database=casezero-db;User Id=casezero-admin;Password={password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

### JWT Configuration

```yaml
JwtSettings__Key: "YourSuperSecretKeyThatIsAtLeast32CharactersLong!"
JwtSettings__Issuer: "CaseZeroAPI"
JwtSettings__Audience: "CaseZeroUsers"
JwtSettings__ExpirationDays: "7"
```

### Email Configuration

```yaml
EmailSettings__SmtpServer: "smtp.office365.com"
EmailSettings__SmtpPort: "587"
EmailSettings__UseSsl: "true"
EmailSettings__FromEmail: "noreply@casezero.com"
EmailSettings__FromName: "CaseZero System"
```

### Application Insights

```yaml
APPINSIGHTS_INSTRUMENTATIONKEY: "{instrumentation-key}"
APPLICATIONINSIGHTS_CONNECTION_STRING: "InstrumentationKey={key};IngestionEndpoint=https://eastus2-3.in.applicationinsights.azure.com/"
ApplicationInsightsAgent_EXTENSION_VERSION: "~3"
XDT_MicrosoftApplicationInsights_Mode: "Recommended"
```

## Security Best Practices

### Secret Management

1. **Use Environment-Specific Secrets**
   - Different secrets for dev and prod
   - Regular rotation of sensitive credentials
   - Use Azure Key Vault for production secrets

2. **Principle of Least Privilege**
   - Service principals with minimal required permissions
   - Environment-specific access controls
   - Regular access reviews

3. **Secret Rotation**
   ```bash
   # Rotate service principal secret
   az ad sp credential reset --name casezero-prod-sp
   
   # Update GitHub secret with new credential
   # Update any Azure Key Vault references
   ```

### Access Control

#### Required Azure Permissions

**Development Service Principal:**
- Contributor on `casezero-dev-rg`
- Website Contributor (for Static Web Apps)

**Production Service Principal:**
- Contributor on `casezero-prod-rg`
- Website Contributor (for Static Web Apps)
- Key Vault Secrets User (if using Key Vault)

#### GitHub Environment Protection

**Production Environment:**
- Required reviewers: 2 people minimum
- Deployment branches: `main` only
- Environment secrets isolated from development

### Network Security

```yaml
# IP Restrictions (Production App Service)
ipSecurityRestrictions:
  - ipAddress: "CloudFlare"  # Example: Use CDN IPs
    action: "Allow"
    priority: 100
  - ipAddress: "Any"
    action: "Deny"
    priority: 2147483647
```

## Setup Commands

### Create Service Principal

```bash
# Development
az ad sp create-for-rbac --name "casezero-dev-sp" \
  --role "Contributor" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/casezero-dev-rg" \
  --sdk-auth

# Production
az ad sp create-for-rbac --name "casezero-prod-sp" \
  --role "Contributor" \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/casezero-prod-rg" \
  --sdk-auth
```

### Get Static Web App Tokens

```bash
# Development
az staticwebapp secrets list --name casezero-frontend-dev --query "properties.apiKey" -o tsv

# Production
az staticwebapp secrets list --name casezero-frontend-prod --query "properties.apiKey" -o tsv
```

### Configure Key Vault

```bash
# Create Key Vault
az keyvault create --name casezero-keyvault \
  --resource-group casezero-shared-rg \
  --location "East US 2"

# Add secrets
az keyvault secret set --vault-name casezero-keyvault \
  --name "sql-admin-login" --value "casezero-admin"
  
az keyvault secret set --vault-name casezero-keyvault \
  --name "sql-admin-password" --value "$(openssl rand -base64 32)"
  
az keyvault secret set --vault-name casezero-keyvault \
  --name "jwt-secret" --value "$(openssl rand -base64 64)"
```

## Validation Checklist

Use this checklist to verify your configuration:

### Repository Secrets
- [ ] `AZURE_CREDENTIALS_DEV` - Valid JSON format
- [ ] `AZURE_CREDENTIALS_PROD` - Valid JSON format
- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` - 32+ character token
- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN_PROD` - 32+ character token
- [ ] `AZURE_RESOURCE_GROUP_DEV` - Exact resource group name
- [ ] `AZURE_RESOURCE_GROUP_PROD` - Exact resource group name
- [ ] `TEAMS_WEBHOOK_URL` - Valid webhook URL (optional)

### Environment Configuration
- [ ] Development environment created in GitHub
- [ ] Production environment created with protection rules
- [ ] Environment-specific secrets configured
- [ ] Service principals have appropriate permissions

### Azure Resources
- [ ] Resource groups exist
- [ ] Service principals created and configured
- [ ] Key Vault setup (if using)
- [ ] Static Web Apps created
- [ ] App Services configured

### Testing
- [ ] Service principal authentication works
- [ ] GitHub Actions can deploy to Azure
- [ ] Applications start successfully
- [ ] Database connections work
- [ ] Application Insights receiving data

## Troubleshooting

### Common Issues

1. **Authentication Errors**
   - Verify service principal JSON format
   - Check subscription ID matches
   - Ensure service principal has correct permissions

2. **Resource Not Found**
   - Verify resource group names
   - Check resource naming conventions
   - Ensure resources exist in correct subscription

3. **Permission Denied**
   - Review service principal role assignments
   - Check resource group scopes
   - Verify subscription access

### Debugging Commands

```bash
# Test service principal login
az login --service-principal \
  --username "{client-id}" \
  --password "{client-secret}" \
  --tenant "{tenant-id}"

# List role assignments
az role assignment list --assignee "{service-principal-id}"

# Test resource group access
az group show --name "casezero-dev-rg"
```

---

For questions about configuration or issues with secrets management, please refer to the main CI/CD documentation or create an issue in the repository.