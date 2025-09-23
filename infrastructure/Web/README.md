# CaseZero Web Infrastructure

This folder contains Bicep templates for deploying the CaseZero application to Azure, including both the .NET API backend and React frontend.

## Architecture

The infrastructure consists of:

- **App Service Plan**: Linux-based hosting plan for both applications
- **Backend API**: .NET 8.0 web application with SQLite database
- **Frontend Web App**: React/Vite application served via Node.js
- **Application Insights**: Monitoring and telemetry for both applications

## Files

- `main.bicep`: Main template that orchestrates all resources
- `main.parameters.json`: Parameter values for deployment
- `app-backend.bicep`: Backend API App Service configuration
- `app-frontend.bicep`: Frontend web app configuration
- `../abbreviations.json`: Resource naming conventions

## Prerequisites

1. Azure CLI installed and logged in
2. Resource group created in Azure
3. Appropriate permissions to create resources

## Deployment

### Using Azure CLI

```bash
# Create a resource group (if not exists)
az group create --name rg-casezero-dev --location "Canada East"

# Deploy the infrastructure
az deployment group create \
  --resource-group rg-casezero-dev \
  --template-file main.bicep \
  --parameters main.parameters.json
```

### Using PowerShell

```powershell
# Create a resource group (if not exists)
New-AzResourceGroup -Name "rg-casezero-dev" -Location "Canada East"

# Deploy the infrastructure
New-AzResourceGroupDeployment `
  -ResourceGroupName "rg-casezero-dev" `
  -TemplateFile "main.bicep" `
  -TemplateParameterFile "main.parameters.json"
```

## Configuration

### Backend Configuration

The backend App Service is configured with:

- .NET 8.0 runtime
- SQLite database (default)
- JWT authentication settings
- Rate limiting configuration
- CORS settings
- Application Insights integration

### Frontend Configuration

The frontend App Service is configured with:

- Node.js 18 LTS runtime
- Vite build process
- Environment variables for API connection
- Application Insights integration
- SPA routing support

## Environment Variables

### Backend
- `ASPNETCORE_ENVIRONMENT`: Development/Production
- `ConnectionStrings__DefaultConnection`: Database connection
- `JwtSettings__*`: JWT configuration
- `IpRateLimiting__*`: Rate limiting settings

### Frontend
- `VITE_API_BASE_URL`: Backend API URL
- `VITE_APP_TITLE`: Application title
- `NODE_ENV`: development/production
- `VITE_APPINSIGHTS_*`: Application Insights settings

## Deployment Process

1. **Build Phase**: 
   - Backend: .NET application is built and published
   - Frontend: npm install → npm run build → static files

2. **Runtime**:
   - Backend: Kestrel server serves the API
   - Frontend: pm2 serves the built static files as SPA

## Scaling

The infrastructure uses B1 (Basic) App Service Plan by default. For production:

- Consider upgrading to S1 or P1v2 for better performance
- Enable autoscaling based on CPU/memory metrics
- Configure custom domains and SSL certificates

## Monitoring

Both applications are configured with Application Insights for:
- Request/response monitoring
- Error tracking
- Performance metrics
- Custom telemetry

## Security

- HTTPS only enforced
- CORS configured appropriately
- JWT authentication for API
- Rate limiting enabled
- Secure key management for production deployments