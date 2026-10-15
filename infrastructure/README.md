# CaseZero Infrastructure - 3-Tier Architecture (Azure Verified Modules)

## 📋 Overview

Infrastructure as Code (IaC) for the complete CaseZero application stack using **100% Azure Verified Modules (AVM)** and Azure Bicep with a **3-tier modular architecture**:

- **Shared Layer**: Key Vault, Monitoring (Application Insights + Log Analytics), Optional SQL Database
- **API Layer**: Backend API (.NET 8.0) on App Service
- **Functions Layer**: Case Generator (.NET 9.0) on Azure Functions
- **Frontend Layer**: Static Web App (React + Vite)

## � Azure Verified Modules (AVM)

This infrastructure uses **official Microsoft Azure Verified Modules** ensuring:
- ✅ **Best Practices**: Microsoft-vetted patterns and configurations
- ✅ **Security**: Built-in security features and compliance
- ✅ **Maintainability**: Regular updates from Microsoft
- ✅ **Consistency**: Standardized modules across the organization

**Reference**: https://azure.github.io/Azure-Verified-Modules/

## �🏗️ Architecture

```
infrastructure/
├── main.bicep                      # Main orchestrator (subscription-level)
├── parameters.dev.json             # Development parameters
├── parameters.prod.json            # Production parameters
│
├── shared/                         # Shared infrastructure layer (100% AVM)
│   └── main.bicep                  # Key Vault (0.12.0), Log Analytics (0.9.1)
│                                   # App Insights (0.4.2), SQL Server (0.20.3)
│
├── api/                            # Backend API layer (.NET 8.0) (100% AVM)
│   └── main.bicep                  # App Service Plan (0.4.1), App Service (0.14.0)
│
├── functions/                      # Case Generator layer (.NET 9.0) (100% AVM)
│   └── main.bicep                  # Storage (0.17.1), Service Plan (0.4.1)
│                                   # Function App (0.14.0)
│
└── frontend/                       # Frontend layer (React + Vite) (Native Resource)
    └── main.bicep                  # Azure Static Web App (2023-12-01 API)
```

## 📦 Azure Verified Modules Used

| Layer | Module | Version | Purpose |
|-------|--------|---------|---------|
| **Shared** | `avm/res/key-vault/vault` | 0.12.0 | Secrets management |
| **Shared** | `avm/res/operational-insights/workspace` | 0.9.1 | Log Analytics |
| **Shared** | `avm/res/insights/component` | 0.4.2 | Application Insights |
| **Shared** | `avm/res/sql/server` | 0.20.3 | SQL Server + Database |
| **API** | `avm/res/web/serverfarm` | 0.4.1 | App Service Plan |
| **API** | `avm/res/web/site` | 0.14.0 | App Service (.NET 8.0) |
| **Functions** | `avm/res/storage/storage-account` | 0.17.1 | Storage Account |
| **Functions** | `avm/res/web/serverfarm` | 0.4.1 | Function App Plan |
| **Functions** | `avm/res/web/site` | 0.14.0 | Function App (.NET 9.0) |
| **Frontend** | Native `Microsoft.Web/staticSites` | 2023-12-01 | Static Web App |

## 🚀 Deployment

### Prerequisites

- Azure CLI installed (`az --version`)
- Azure subscription with appropriate permissions
- Logged in to Azure (`az login`)

### Quick Deploy

#### Development Environment

```bash
# Deploy to development
az deployment sub create \
  --name casezero-dev-deployment \
  --location canadaeast \
  --template-file infrastructure/main.bicep \
  --parameters @infrastructure/parameters.dev.json
```

#### Production Environment

```bash
# Deploy to production
az deployment sub create \
  --name casezero-prod-deployment \
  --location canadaeast \
  --template-file infrastructure/main.bicep \
  --parameters @infrastructure/parameters.prod.json
```

### Validate Before Deployment

```bash
# Validate Bicep syntax
az bicep build --file infrastructure/main.bicep

# What-if analysis (dev)
az deployment sub what-if \
  --location canadaeast \
  --template-file infrastructure/main.bicep \
  --parameters @infrastructure/parameters.dev.json
```

## 📦 Resources Provisioned

### Shared Layer (4 Resource Groups)

| Resource | Environment | SKU | Purpose |
|----------|-------------|-----|---------|
| **Key Vault** | All | Standard | Secrets management (JWT keys, Azure OpenAI credentials) |
| **Log Analytics** | All | PerGB2018 | Centralized logging |
| **App Insights** | All | - | Application monitoring and telemetry |
| **SQL Server** | Optional | Basic/S1 | Relational database (optional, defaults to SQLite) |

### API Layer

| Resource | Dev SKU | Prod SKU | Purpose |
|----------|---------|----------|---------|
| **App Service Plan** | B1 (Basic) | P1v3 (Premium) | Compute for API |
| **App Service** | Linux | Linux | .NET 8.0 Backend API |

### Functions Layer

| Resource | Dev SKU | Prod SKU | Purpose |
|----------|---------|----------|---------|
| **Function App Plan** | Y1 (Consumption) | EP1 (Elastic Premium) | Serverless compute |
| **Function App** | Linux | Linux | .NET 9.0 Case Generator |
| **Storage Account** | Standard_LRS | Standard_GRS | Blob storage (cases, bundles, logs) |

### Frontend Layer

| Resource | Dev SKU | Prod SKU | Purpose |
|----------|---------|----------|---------|
| **Static Web App** | Free | Standard | React SPA with CDN |

## 🔐 Security Features

### Managed Identity & RBAC

- ✅ **System-Assigned Managed Identity** for all apps
- ✅ **Key Vault Secrets User** role for API and Functions
- ✅ **Storage Blob Data Contributor** role for Functions

### Network Security

- ✅ **HTTPS Only** enforced on all endpoints
- ✅ **TLS 1.2 minimum** enforced
- ✅ **Blob Public Access** disabled
- ✅ **FTPS Only** for deployment

### Secrets Management

- ✅ **No hardcoded secrets** in templates
- ✅ **Key Vault references** for sensitive config
- ✅ **Soft delete** enabled on Key Vault
- ✅ **Purge protection** in production

## 🎛️ Configuration

### Environment Variables

The infrastructure automatically configures:

#### API Backend (.NET 8.0)
```
ASPNETCORE_ENVIRONMENT=Production|Development
ConnectionStrings__DefaultConnection=Data Source=casezero.db (SQLite) or SQL Server
JwtSettings__SecretKey=@Microsoft.KeyVault(SecretUri=...)
APPINSIGHTS_INSTRUMENTATIONKEY=<auto>
APPLICATIONINSIGHTS_CONNECTION_STRING=<auto>
CaseGenerator__FunctionBaseUrl=https://<func-app>.azurewebsites.net
CaseGeneratorStorage__AccountName=<storage-account>           # managed identity
CaseGeneratorStorage__BundlesContainer=bundles
CaseGeneratorStorage__CasesContainer=cases
```

The API uses System-Assigned Managed Identity to read case bundles
(`Storage Blob Data Reader`) and to enqueue forensic requests
(`Storage Queue Data Message Sender`). The `forensic-requests` queue is
pre-created by the functions layer so the API runs with least-privilege RBAC.

#### Functions (.NET 9.0)
```
FUNCTIONS_EXTENSION_VERSION=~4
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
CaseGeneratorStorage__AccountName=<storage-account>           # managed identity
CaseGeneratorStorage__BundlesContainer=bundles
CaseGeneratorStorage__CasesContainer=cases
LLM__UseAzureFoundry=true
AzureFoundry__Endpoint=@Microsoft.KeyVault(SecretUri=...)
AzureFoundry__ApiKey=@Microsoft.KeyVault(SecretUri=...)
AzureFoundry__ModelName=@Microsoft.KeyVault(SecretUri=...)
AzureFoundry__ImageDeploymentName=@Microsoft.KeyVault(SecretUri=...)
KeyVault__VaultUri=<auto>
```

#### Frontend (React + Vite)
```
VITE_API_BASE_URL=https://<api-url>/api
VITE_APP_TITLE=CaseZero
VITE_ENV=prod|dev
```

### Required Key Vault Secrets

Before running the application, add these secrets to Key Vault:

```bash
# JWT signing key for API
az keyvault secret set \
  --vault-name <keyvault-name> \
  --name jwt-signing-key \
  --value "<generate-secure-key-32-chars>"

# Azure AI Foundry credentials (used by the Case Generator)
az keyvault secret set \
  --vault-name <keyvault-name> \
  --name azure-foundry-endpoint \
  --value "https://<your-foundry-resource>.cognitiveservices.azure.com/"

az keyvault secret set \
  --vault-name <keyvault-name> \
  --name azure-foundry-api-key \
  --value "<your-api-key>"

az keyvault secret set \
  --vault-name <keyvault-name> \
  --name azure-foundry-model-name \
  --value "gpt-5.2"

az keyvault secret set \
  --vault-name <keyvault-name> \
  --name azure-foundry-image-deployment-name \
  --value "gpt-image-2"
```

## 📊 Cost Estimation

### Development Environment (~$50-80/month)

- App Service Plan B1: ~$13/month
- Function App Consumption: ~$0-5/month (free tier)
- Static Web App Free: $0
- Storage Standard_LRS: ~$1-5/month
- Application Insights: ~$5-10/month
- Key Vault: ~$0.03/month

### Production Environment (~$250-350/month)

- App Service Plan P1v3: ~$100/month
- Function App EP1: ~$70/month
- Static Web App Standard: ~$9/month
- Storage Standard_GRS: ~$10-20/month
- Application Insights: ~$50-100/month
- SQL Database S1: ~$30/month (optional)
- Key Vault: ~$0.03/month

## 🔧 Maintenance

### Update a Single Layer

```bash
# Update only the API layer
az deployment group create \
  --resource-group casezero-api-dev-rg \
  --template-file infrastructure/api/main.bicep \
  --parameters environment=dev namePrefix=casezero \
    keyVaultUri=<vault-uri> \
    appInsightsConnectionString=<connection-string>
```

### Enable SQL Database

```bash
# Update parameters.dev.json or parameters.prod.json
"enableSqlDatabase": {
  "value": true
},
"sqlAdminLogin": {
  "value": "sqladmin"
},
"sqlAdminPassword": {
  "value": "<secure-password>"
}

# Redeploy
az deployment sub create \
  --name casezero-dev-deployment \
  --location canadaeast \
  --template-file infrastructure/main.bicep \
  --parameters @infrastructure/parameters.dev.json
```

### Scale Production

Edit `parameters.prod.json`:

```json
{
  "appServicePlanSku": {
    "value": {
      "name": "P2v3",
      "tier": "PremiumV3",
      "capacity": 3
    }
  }
}
```

## 📝 Best Practices

### Implemented

- ✅ **Separation of Concerns**: Each layer is independently deployable
- ✅ **Environment Parity**: Same templates for dev/staging/prod
- ✅ **Managed Identity**: No connection strings in app code
- ✅ **Zone Redundancy**: Enabled for production SQL and App Service Plan
- ✅ **Geo-Redundant Storage**: Enabled for production
- ✅ **Consistent Tagging**: All resources tagged with Environment, Project, Layer
- ✅ **Health Checks**: Configured for API and Functions
- ✅ **Auto-scaling**: Ready (App Service Plan supports scaling)

### Recommendations for Production

- [ ] **Custom Domain**: Configure custom domain for Static Web App
- [ ] **Azure Front Door**: Add CDN and WAF for global distribution
- [ ] **Private Endpoints**: Enable for Storage and SQL in VNet
- [ ] **Azure Monitor Alerts**: Configure alerts for failures and latency
- [ ] **Backup Strategy**: Enable automated backups for SQL Database
- [ ] **Disaster Recovery**: Configure geo-replication for critical data

## 🐛 Troubleshooting

### Deployment Fails with "Resource Not Found"

**Cause**: Bicep references a resource that doesn't exist yet.
**Solution**: Ensure all `dependsOn` are correctly configured (already handled in templates).

### Function App Shows "Storage Account Not Accessible"

**Cause**: RBAC permissions not yet propagated.
**Solution**: Wait 5-10 minutes for Azure AD to propagate role assignments.

### Static Web App Shows 404

**Cause**: Build not completed or app settings not configured.
**Solution**: Check GitHub Actions workflow or manually deploy using Azure CLI.

## 🔗 Related Documentation

- [Azure Bicep Documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [BACKEND_ARCHITECTURE.md](../docs/BACKEND_ARCHITECTURE.md)
- [DEPLOYMENT.md](../docs/DEPLOYMENT.md)
- [API Documentation](../docs/API_COMPLETE.md)

## 📞 Support

For issues related to infrastructure:
1. Check deployment logs: `az deployment sub show --name <deployment-name>`
2. Review Activity Log in Azure Portal
3. Open an issue on GitHub with deployment output

---

**Version**: 2.0  
**Last Updated**: October 30, 2025  
**Maintained By**: Infrastructure Team
