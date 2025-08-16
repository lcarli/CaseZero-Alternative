# 🤖 CaseZero Case Generator AI

## Overview

The CaseZero Case Generator AI is a comprehensive Azure Durable Functions-based system that automatically generates realistic detective investigation cases using artificial intelligence. The system provides real-time progress tracking and produces complete case packages with documents, evidence, and investigation materials.

## 🏗️ Architecture

### Core Components

- **Azure Durable Functions**: Orchestrates the case generation pipeline
- **Azure Storage Account**: Stores generated cases and bundles
- **Azure Key Vault**: Manages secrets and configuration
- **Frontend Interface**: Real-time progress tracking and case management
- **LLM Service**: AI-powered content generation

### Generation Pipeline

The system follows a 10-step pipeline for case generation:

1. **Plan** - Initial case structure and framework
2. **Expand** - Detailed suspects, evidence, and timeline
3. **Design** - Investigation flow and game mechanics
4. **GenDocs** - Generate investigation documents
5. **GenMedia** - Create media assets and image prompts
6. **Normalize** - Standardize content and format
7. **Index** - Create searchable metadata
8. **RuleValidate** - Quality assurance checks
9. **RedTeam** - Security and content validation
10. **Package** - Final assembly and storage

## 🚀 Quick Start

### Prerequisites

- Azure subscription with appropriate permissions
- .NET 8 SDK
- Azure CLI
- Azure Functions Core Tools
- Visual Studio Code (recommended)

### 1. Deploy Infrastructure

First, deploy the required Azure infrastructure:

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "your-subscription-id"

# Deploy infrastructure using GitHub Actions
# Go to: https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure.yml
# Click "Run workflow" and select "development" environment
```

### 2. Configure Secrets

After infrastructure deployment, configure the required secrets in Azure Key Vault:

```bash
# Get Key Vault name from deployment output
KV_NAME="your-keyvault-name"

# Add required secrets
az keyvault secret set --vault-name $KV_NAME --name "OpenAI-ApiKey" --value "your-openai-key"
az keyvault secret set --vault-name $KV_NAME --name "OpenAI-Endpoint" --value "your-openai-endpoint"
```

### 3. Deploy Functions

Deploy the Case Generator Functions:

```bash
# Option 1: Using GitHub Actions (Recommended)
# Go to: https://github.com/lcarli/CaseZero-Alternative/actions/workflows/functions-deploy.yml
# Click "Run workflow" and select "development" environment

# Option 2: Manual deployment
cd backend/CaseGen.Functions
func azure functionapp publish casezero-func-dev
```

### 4. Configure GitHub Secrets

Set up the following secrets in your GitHub repository:

- `AZURE_CREDENTIALS`: Service principal credentials (JSON format)
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_DEV`: Function app publish profile for dev
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_PROD`: Function app publish profile for prod

## 📋 Step-by-Step Setup Guide

### Phase 1: Azure Resource Setup

1. **Create Resource Groups**
   ```bash
   az group create --name casezero-dev-rg --location "East US 2"
   az group create --name casezero-prod-rg --location "East US 2"
   ```

2. **Deploy Infrastructure**
   - Use the GitHub Actions workflow: `🏗️ Deploy Infrastructure`
   - Select environment: `development` for testing
   - Verify resources are created correctly

3. **Verify Deployed Resources**
   ```bash
   # List deployed resources
   az resource list --resource-group casezero-dev-rg --output table
   ```

### Phase 2: Configure Azure Services

1. **Key Vault Configuration**
   ```bash
   # Set your Key Vault name (from deployment output)
   KV_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.KeyVault/vaults" --query "[0].name" -o tsv)
   
   # Configure secrets
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-ApiKey" --value "your-api-key"
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-Endpoint" --value "https://your-endpoint.openai.azure.com/"
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-DeploymentName" --value "gpt-4"
   ```

2. **Storage Account Setup**
   ```bash
   # Get storage account name
   STORAGE_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.Storage/storageAccounts" --query "[?contains(name, 'genstr')].name" -o tsv)
   
   # Verify containers are created
   az storage container list --account-name $STORAGE_NAME --auth-mode login
   ```

3. **Function App Configuration**
   ```bash
   # Get Function App name
   FUNC_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.Web/sites" --query "[?contains(name, 'func')].name" -o tsv)
   
   # Verify Function App is running
   az functionapp show --name $FUNC_NAME --resource-group casezero-dev-rg --query "state" -o tsv
   ```

### Phase 3: Deploy Functions

1. **Build Functions Locally**
   ```bash
   cd backend/CaseGen.Functions
   dotnet restore
   dotnet build --configuration Release
   dotnet publish --configuration Release --output ./publish
   ```

2. **Deploy to Azure**
   ```bash
   # Using Azure Functions Core Tools
   func azure functionapp publish $FUNC_NAME --dotnet-isolated
   
   # Or use GitHub Actions workflow
   # Navigate to Actions > 🚀 Deploy Case Generator Functions
   ```

3. **Verify Deployment**
   ```bash
   # List deployed functions
   az functionapp function list --name $FUNC_NAME --resource-group casezero-dev-rg --output table
   
   # Test the health endpoint
   curl "https://$FUNC_NAME.azurewebsites.net/api/status"
   ```

### Phase 4: Configure Frontend

1. **Update Environment Variables**
   ```bash
   # In your frontend/.env file
   echo "VITE_FUNCTIONS_BASE_URL=https://$FUNC_NAME.azurewebsites.net" >> frontend/.env
   ```

2. **Build and Deploy Frontend**
   ```bash
   cd frontend
   npm install
   npm run build
   
   # Deploy using existing CI/CD pipeline
   ```

## 🔧 Configuration

### Environment Variables

#### Function App Settings
```
FUNCTIONS_EXTENSION_VERSION=~4
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
AzureWebJobsStorage=<storage-connection-string>
CaseGeneratorStorage__ConnectionString=<storage-connection-string>
CaseGeneratorStorage__CasesContainer=cases
CaseGeneratorStorage__BundlesContainer=bundles
KeyVault__VaultUri=<keyvault-uri>
TaskHub=CaseGeneratorHub
APPLICATIONINSIGHTS_CONNECTION_STRING=<app-insights-connection>
```

#### Key Vault Secrets
- `OpenAI-ApiKey`: Your OpenAI API key
- `OpenAI-Endpoint`: OpenAI service endpoint
- `OpenAI-DeploymentName`: Model deployment name

### CORS Configuration

If accessing from a web application, configure CORS on the Function App:

```bash
az functionapp cors add --name $FUNC_NAME --resource-group casezero-dev-rg --allowed-origins "https://your-domain.com"
```

## 🔌 API Reference

### Start Case Generation

**Endpoint**: `POST /api/StartCaseGeneration`

**Request Body**:
```json
{
  "title": "Robbery at Tech Company",
  "location": "São Paulo, SP",
  "difficulty": "Iniciante",
  "targetDurationMinutes": 60,
  "generateImages": true,
  "constraints": [],
  "timezone": "America/Sao_Paulo"
}
```

**Response**:
```json
{
  "instanceId": "abc123...",
  "status": "Started"
}
```

### Get Generation Status

**Endpoint**: `GET /api/status/{instanceId}`

**Response**:
```json
{
  "instanceId": "abc123...",
  "runtimeStatus": "Running",
  "createdAt": "2024-01-01T10:00:00Z",
  "lastUpdatedAt": "2024-01-01T10:05:00Z",
  "customStatus": {
    "caseId": "CASE-20240101-1000",
    "status": "Running",
    "currentStep": "GenDocs",
    "completedSteps": ["Plan", "Expand", "Design"],
    "progress": 40.0
  }
}
```

## 🧪 Testing

### Unit Tests

```bash
cd backend/CaseGen.Functions
dotnet test
```

### Integration Tests

```bash
# Test individual functions
curl -X POST "https://$FUNC_NAME.azurewebsites.net/api/StartCaseGeneration" \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Case","location":"Test Location","difficulty":"Iniciante"}'
```

### Load Testing

For production environments, perform load testing:

```bash
# Install Artillery
npm install -g artillery

# Run load test
artillery run load-test-config.yml
```

## 📊 Monitoring

### Application Insights

Monitor your Functions using Application Insights:

1. **View in Azure Portal**
   - Navigate to your Function App
   - Click on "Application Insights"
   - Monitor performance, failures, and dependencies

2. **Key Metrics to Monitor**
   - Function execution count
   - Function execution duration
   - Function failures
   - Storage operations
   - Key Vault access

### Alerts

Set up alerts for critical scenarios:

```bash
# Create alert for function failures
az monitor metrics alert create \
  --name "CaseGenerator-Failures" \
  --resource-group casezero-dev-rg \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/casezero-dev-rg/providers/Microsoft.Web/sites/$FUNC_NAME" \
  --condition "count Microsoft.Web/sites/functions/requests" \
  --threshold 10
```

## 🚨 Troubleshooting

### Common Issues

1. **Function App Not Starting**
   ```bash
   # Check logs
   az functionapp log tail --name $FUNC_NAME --resource-group casezero-dev-rg
   
   # Restart the app
   az functionapp restart --name $FUNC_NAME --resource-group casezero-dev-rg
   ```

2. **Storage Connection Issues**
   ```bash
   # Verify storage account access
   az storage account show --name $STORAGE_NAME --resource-group casezero-dev-rg
   
   # Check connection string
   az functionapp config appsettings list --name $FUNC_NAME --resource-group casezero-dev-rg
   ```

3. **Key Vault Access Issues**
   ```bash
   # Check Function App identity
   az functionapp identity show --name $FUNC_NAME --resource-group casezero-dev-rg
   
   # Verify Key Vault access policies
   az keyvault show --name $KV_NAME --resource-group casezero-dev-rg
   ```

### Debug Mode

Enable debug logging:

```bash
az functionapp config appsettings set \
  --name $FUNC_NAME \
  --resource-group casezero-dev-rg \
  --settings "AZURE_FUNCTIONS_ENVIRONMENT=Development"
```

## 🔄 CI/CD Pipeline

### Automated Deployment

The system includes comprehensive CI/CD pipelines:

1. **Infrastructure Pipeline**: `infrastructure.yml`
   - Deploys all Azure resources
   - Validates BICEP templates
   - Supports multiple environments

2. **Functions Pipeline**: `functions-deploy.yml`
   - Builds and tests Functions
   - Deploys to dev/prod environments
   - Includes health checks

3. **Frontend Pipeline**: `cd-dev.yml` / `cd-prod.yml`
   - Builds and deploys frontend
   - Updates API endpoints

### Manual Deployment

For emergency deployments:

```bash
# Quick function deployment
cd backend/CaseGen.Functions
func azure functionapp publish $FUNC_NAME --force
```

## 📈 Scaling

### Performance Optimization

1. **Function App Scaling**
   ```bash
   # Configure premium plan for production
   az functionapp plan update \
     --name casezero-func-asp-prod \
     --resource-group casezero-prod-rg \
     --max-burst 20
   ```

2. **Storage Optimization**
   - Use premium storage for high IOPS
   - Enable CDN for generated content
   - Implement blob lifecycle policies

### Cost Optimization

1. **Development Environment**
   - Use consumption plan
   - Implement auto-pause policies
   - Regular cleanup of test data

2. **Production Environment**
   - Monitor usage patterns
   - Implement cost alerts
   - Use reserved instances for predictable workloads

## 🔒 Security

### Best Practices

1. **Access Control**
   - Use managed identities
   - Implement RBAC properly
   - Regular access reviews

2. **Data Protection**
   - Encrypt data at rest
   - Use HTTPS for all communications
   - Implement data retention policies

3. **Monitoring**
   - Enable Azure Security Center
   - Monitor for suspicious activities
   - Regular security assessments

## 📚 Additional Resources

- [Azure Durable Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/durable/)
- [Azure Functions .NET Isolated Guide](https://docs.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Azure Key Vault Integration](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-key-vault)
- [Application Insights for Functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-monitoring)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.