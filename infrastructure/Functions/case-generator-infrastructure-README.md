# 🤖 Case Generator Infrastructure

This directory contains all the necessary files to deploy **ONLY** the Case Generator infrastructure for the CaseZero platform, independent of the main application infrastructure.

## 📋 Overview

The Case Generator infrastructure includes:
- **Azure Function App** - Runs the AI case generation pipeline
- **Storage Account** - Stores generated cases and bundles
- **Key Vault** - Manages secrets for AI services
- **Application Insights** - Monitoring and logging
- **Log Analytics Workspace** - Centralized logging

## 🏗️ Architecture

The Case Generator operates as a standalone component with its own resource group and infrastructure, allowing for:
- Independent deployment and scaling
- Isolated cost management
- Separate monitoring and alerting
- Independent development cycles

### 10-Step AI Pipeline

The Case Generator follows a sophisticated 10-step pipeline:

1. **Plan** - Create initial case structure and framework
2. **Expand** - Generate detailed suspects, evidence, and timeline
3. **Design** - Design investigation flow and game mechanics
4. **GenDocs** - Generate investigation documents
5. **GenMedia** - Create media assets and image prompts
6. **Normalize** - Standardize content and format
7. **Index** - Create searchable metadata
8. **RuleValidate** - Quality assurance checks
9. **RedTeam** - Security and content validation
10. **Package** - Final assembly and storage

## 📁 Files Structure

```
infrastructure/
├── bicep/
│   ├── case-generator.bicep                 # Main Bicep template
│   ├── case-generator-parameters.dev.json   # Development parameters
│   └── case-generator-parameters.prod.json  # Production parameters
├── case-generator-config.yml                # Configuration documentation
└── case-generator-infrastructure-README.md  # This file

.github/workflows/
└── case-generator-infrastructure.yml        # GitHub Actions workflow
```

## 🚀 Quick Start

### 1. Deploy via GitHub Actions (Recommended)

1. Go to **Actions** tab in GitHub
2. Select "🤖 Deploy Case Generator Infrastructure"
3. Choose environment (`development` or `production`)
4. Select action (`deploy`, `validate`, or `destroy`)
5. Click "Run workflow"

### 2. Manual Deployment

```bash
# 1. Login to Azure
az login

# 2. Set subscription (if needed)
az account set --subscription "Your-Subscription-Id"

# 3. Create resource group
az group create \
  --name casezero-casegen-dev-rg \
  --location "East US 2" \
  --tags Environment=development Project=CaseZero Component=CaseGenerator

# 4. Deploy infrastructure
az deployment group create \
  --resource-group casezero-casegen-dev-rg \
  --template-file infrastructure/bicep/case-generator.bicep \
  --parameters @infrastructure/bicep/case-generator-parameters.dev.json \
  --name "casegen-$(date +%Y%m%d%H%M%S)"
```

## 🔧 Configuration

### Environment Variables

The following secrets need to be configured in GitHub repository settings:

```bash
# Azure Credentials (JSON format)
AZURE_CREDENTIALS_DEV   # For development environment
AZURE_CREDENTIALS_PROD  # For production environment

# Optional: Teams notifications
TEAMS_WEBHOOK_URL       # Microsoft Teams webhook for notifications
```

### Parameter Files

**Development (`case-generator-parameters.dev.json`)**:
- Consumption plan for Function App (cost-optimized)
- LRS storage (locally redundant)
- 30-day log retention

**Production (`case-generator-parameters.prod.json`)**:
- Premium plan for Function App (performance-optimized)
- GRS storage (geo-redundant)
- 90-day log retention

## 🏗️ Resource Groups

The Case Generator uses separate resource groups:

| Environment | Resource Group |
|------------|----------------|
| Development | `casezero-casegen-dev-rg` |
| Production | `casezero-casegen-prod-rg` |

## 📊 Cost Estimation

| Environment | Monthly Cost | Key Features |
|------------|-------------|--------------|
| Development | $50-100 | Consumption plan, LRS storage |
| Production | $200-500 | Premium plan, GRS storage, extended retention |

## 🔒 Security Features

- **Managed Identity**: System-assigned identity for secure resource access
- **RBAC**: Role-based access control for Key Vault and Storage
- **HTTPS Only**: All communications encrypted
- **Key Vault**: Centralized secrets management
- **Network Security**: Configurable access controls

## 📈 Monitoring

### Key Metrics
- Function execution count and duration
- Error rates and success rates
- Storage usage and performance
- Cost optimization metrics

### Alerts
- High error rates (>10%)
- Function timeouts (>45 minutes)
- Storage capacity warnings (>80%)

## 🔄 Deployment Options

### GitHub Actions Workflow

The workflow supports three actions:

1. **Validate** - Check template syntax and configuration
2. **Deploy** - Deploy infrastructure to Azure
3. **Destroy** - Remove all infrastructure (requires confirmation)

### Features:
- ✅ Template validation
- 📊 What-if analysis
- 🔒 Environment protection
- 📢 Teams notifications
- 📋 Deployment summaries

## 🛠️ Development

### Prerequisites
- Azure CLI 2.50+
- Bicep CLI
- Azure subscription with contributor access

### Local Testing

```bash
# Validate template locally
az bicep build --file infrastructure/bicep/case-generator.bicep

# Test parameter files
az deployment group validate \
  --resource-group your-test-rg \
  --template-file infrastructure/bicep/case-generator.bicep \
  --parameters @infrastructure/bicep/case-generator-parameters.dev.json
```

## 🚨 Troubleshooting

### Common Issues

1. **Resource name conflicts**: Azure resource names must be globally unique
   - Solution: The template includes `uniqueString()` for storage and Key Vault

2. **Permission errors**: Insufficient Azure permissions
   - Solution: Ensure service principal has Contributor role

3. **Template validation failures**: Bicep syntax or configuration errors
   - Solution: Run `az bicep build` locally to check syntax

### Logs and Monitoring

- **Application Insights**: Function execution logs and metrics
- **Azure Portal**: Resource-level monitoring and diagnostics
- **GitHub Actions**: Deployment logs and summaries

## 🔄 Integration

### With Main Application
The Case Generator integrates with the main CaseZero application through:
- REST API endpoints
- Shared Azure Active Directory authentication
- Event-driven communication patterns

### With AI Services
Configuration for AI services should be stored in Key Vault:
- Azure OpenAI API keys
- Computer Vision service keys
- Other AI service credentials

## 📚 Additional Resources

- [Case Generator Setup Guide](../docs/CASE_GENERATOR_SETUP.md)
- [Azure Infrastructure Documentation](../docs/cicd/azure-setup.md)
- [Function App Documentation](../backend/CaseGen.Functions/README.md)
- [Configuration Examples](../backend/CaseZeroApi/CONFIGURATION_EXAMPLE.md)

## 🤝 Support

For issues with the Case Generator infrastructure:
1. Check GitHub Actions workflow logs
2. Review Azure Portal for resource-specific errors
3. Consult Application Insights for runtime issues
4. Create an issue in the repository with logs and error details

---

**🚀 Ready to generate amazing cases with AI!**