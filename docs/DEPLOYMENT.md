# Deployment Guide - Azure Production

Complete guide for deploying the Hive Document Management System to Azure.

## Prerequisites

- Azure Subscription
- Azure CLI installed (`az --version`)
- .NET 8 SDK
- Node.js 18+
- Azure Functions Core Tools v4

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│   Azure Front Door / CDN (Frontend)             │
└──────────────────┬──────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────┐
│   Azure App Service (.NET 8 API)                │
│   - Auto-scaling                                │
│   - Application Insights                        │
└──────┬────────────┬────────────┬────────────────┘
       │            │            │
   ┌───▼───┐   ┌───▼────┐   ┌──▼─────┐
   │Cosmos │   │  Blob  │   │ Queue  │
   │  DB   │   │Storage │   │Storage │
   └───▲────┘   └───▲────┘   └────┬───┘
       │            │             │
       │            │      ┌──────▼────────────┐
       │            │      │  Azure Functions  │
       │            │      │  Consumption Plan │
       └────────────┴──────┴───────────────────┘
```

## Step 1: Resource Group Setup

Create resource group for all resources:

```bash
# Variables
RESOURCE_GROUP="rg-docmanagement-prod"
LOCATION="westeurope"
APP_NAME="docmanagement-app"

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

## Step 2: Azure CosmosDB

Create CosmosDB account and database:

```bash
COSMOS_ACCOUNT="cosmos-$APP_NAME"

# Create CosmosDB account (can take 5-10 minutes)
az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION failoverPriority=0 \
  --default-consistency-level Session \
  --enable-automatic-failover false

# Create database
az cosmosdb sql database create \
  --account-name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --name HiveDb

# Create containers
# Documents container (partition key: /userId)
az cosmosdb sql container create \
  --account-name $COSMOS_ACCOUNT \
  --database-name HiveDb \
  --resource-group $RESOURCE_GROUP \
  --name documents \
  --partition-key-path "/userId" \
  --throughput 400 \
  --idx @indexing-policy-documents.json

# Upload sessions container (partition key: /sessionId, TTL enabled)
az cosmosdb sql container create \
  --account-name $COSMOS_ACCOUNT \
  --database-name HiveDb \
  --resource-group $RESOURCE_GROUP \
  --name upload-sessions \
  --partition-key-path "/sessionId" \
  --throughput 400 \
  --ttl 86400

# Share links container (partition key: /userId)
az cosmosdb sql container create \
  --account-name $COSMOS_ACCOUNT \
  --database-name HiveDb \
  --resource-group $RESOURCE_GROUP \
  --name share-links \
  --partition-key-path "/userId" \
  --throughput 400

# Get connection string
COSMOS_CONNECTION=$(az cosmosdb keys list \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv)

echo "CosmosDB Connection: $COSMOS_CONNECTION"
```

### indexing-policy-documents.json

Create file for documents container indexing policy:

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {
      "path": "/*"
    }
  ],
  "excludedPaths": [
    {
      "path": "/processingInfo/extractedText/*"
    }
  ],
  "compositeIndexes": [
    [
      { "path": "/userId", "order": "ascending" },
      { "path": "/uploadedAt", "order": "descending" }
    ],
    [
      { "path": "/userId", "order": "ascending" },
      { "path": "/metadata/category", "order": "ascending" }
    ]
  ]
}
```

## Step 3: Storage Account

Create storage account for Blob and Queue:

```bash
STORAGE_ACCOUNT="st${APP_NAME//-/}prod"  # Remove dashes, max 24 chars

# Create storage account
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --access-tier Hot

# Create blob container
az storage container create \
  --name documents \
  --account-name $STORAGE_ACCOUNT \
  --auth-mode login

# Create queue
az storage queue create \
  --name document-processing-queue \
  --account-name $STORAGE_ACCOUNT \
  --auth-mode login

# Get connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

echo "Storage Connection: $STORAGE_CONNECTION"
```

## Step 4: Azure Functions

Create Function App for background processing:

```bash
FUNCTION_APP="func-$APP_NAME"

# Create Function App (Consumption plan)
az functionapp create \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --storage-account $STORAGE_ACCOUNT \
  --consumption-plan-location $LOCATION \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Linux

# Configure app settings
az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "CosmosDBConnection=$COSMOS_CONNECTION" \
    "BlobStorageConnection=$STORAGE_CONNECTION" \
    "AzureWebJobsStorage=$STORAGE_CONNECTION"

# Deploy Functions
cd src/Hive.Functions
func azure functionapp publish $FUNCTION_APP
```

## Step 5: App Service (API)

Create App Service for .NET API:

```bash
APP_SERVICE="app-$APP_NAME-api"
APP_SERVICE_PLAN="plan-$APP_NAME"

# Create App Service Plan (B1 or higher for production)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:8.0"

# Configure app settings
az webapp config appsettings set \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "CosmosDb__Endpoint=https://$COSMOS_ACCOUNT.documents.azure.com:443/" \
    "CosmosDb__DatabaseName=HiveDb" \
    "BlobStorage__ConnectionString=$STORAGE_CONNECTION" \
    "AzureQueue__ConnectionString=$STORAGE_CONNECTION" \
    "AzureQueue__QueueName=document-processing-queue"

# Configure CosmosDB key in Key Vault (recommended) or direct
COSMOS_KEY=$(az cosmosdb keys list \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query primaryMasterKey -o tsv)

az webapp config appsettings set \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --settings "CosmosDb__Key=$COSMOS_KEY"

# Enable HTTPS only
az webapp update \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --https-only true

# Deploy API
cd ../../src/Hive.Api
dotnet publish -c Release -o ./publish
cd publish
zip -r ../api.zip .
cd ..
az webapp deployment source config-zip \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --src api.zip
```

## Step 6: Application Insights

Enable monitoring and logging:

```bash
APP_INSIGHTS="appi-$APP_NAME"

# Create Application Insights
az monitor app-insights component create \
  --app $APP_INSIGHTS \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app $APP_INSIGHTS \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# Connect to App Service
az webapp config appsettings set \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$INSTRUMENTATION_KEY"

# Connect to Functions
az functionapp config appsettings set \
  --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$INSTRUMENTATION_KEY"
```

## Step 7: Frontend Deployment

Deploy React frontend to Azure Static Web Apps or Azure Storage with CDN:

### Option A: Azure Static Web Apps (Recommended)

```bash
STATIC_WEB_APP="swa-$APP_NAME"

# Create Static Web App
az staticwebapp create \
  --name $STATIC_WEB_APP \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --source https://github.com/your-org/your-repo \
  --branch main \
  --app-location "/frontend" \
  --output-location "dist"

# Configure API URL
API_URL="https://$APP_SERVICE.azurewebsites.net/api"
echo "VITE_API_URL=$API_URL" > frontend/.env.production

# Build and deploy (via GitHub Actions or manual)
cd frontend
npm install
npm run build

# Manual deployment (if not using GitHub Actions)
az staticwebapp deploy \
  --name $STATIC_WEB_APP \
  --resource-group $RESOURCE_GROUP \
  --app-location dist
```

### Option B: Azure Blob Storage + CDN

```bash
# Enable static website hosting
az storage blob service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --static-website \
  --index-document index.html \
  --404-document index.html

# Build frontend
cd frontend
npm install
API_URL="https://$APP_SERVICE.azurewebsites.net/api" npm run build

# Upload to blob storage
az storage blob upload-batch \
  --account-name $STORAGE_ACCOUNT \
  --destination '$web' \
  --source ./dist \
  --auth-mode login

# Get static website URL
FRONTEND_URL=$(az storage account show \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query "primaryEndpoints.web" -o tsv)

echo "Frontend URL: $FRONTEND_URL"

# Optional: Create CDN for HTTPS and custom domain
CDN_PROFILE="cdn-$APP_NAME"
CDN_ENDPOINT="cdn-endpoint-$APP_NAME"

az cdn profile create \
  --name $CDN_PROFILE \
  --resource-group $RESOURCE_GROUP \
  --sku Standard_Microsoft

az cdn endpoint create \
  --name $CDN_ENDPOINT \
  --profile-name $CDN_PROFILE \
  --resource-group $RESOURCE_GROUP \
  --origin $STORAGE_ACCOUNT.z6.web.core.windows.net \
  --origin-host-header $STORAGE_ACCOUNT.z6.web.core.windows.net
```

## Step 8: Security Configuration

### Enable CORS on API

```bash
# Allow frontend domain
az webapp cors add \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "https://$STATIC_WEB_APP.azurestaticapps.net"
```

### Configure Authentication (Azure AD)

```bash
# Create Azure AD app registration
az ad app create \
  --display-name "$APP_NAME-api" \
  --sign-in-audience AzureADMyOrg

# Get Application ID
APP_ID=$(az ad app list --display-name "$APP_NAME-api" --query "[0].appId" -o tsv)

# Configure API authentication
az webapp auth update \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --enabled true \
  --action LoginWithAzureActiveDirectory \
  --aad-client-id $APP_ID \
  --aad-token-issuer-url "https://sts.windows.net/$(az account show --query tenantId -o tsv)/"
```

### Use Azure Key Vault for Secrets

```bash
KEY_VAULT="kv-$APP_NAME"

# Create Key Vault
az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION

# Store secrets
az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name "CosmosDbKey" \
  --value "$COSMOS_KEY"

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name "StorageConnectionString" \
  --value "$STORAGE_CONNECTION"

# Enable managed identity for App Service
az webapp identity assign \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP

# Get identity principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

# Grant access to Key Vault
az keyvault set-policy \
  --name $KEY_VAULT \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list

# Update app settings to use Key Vault references
az webapp config appsettings set \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "CosmosDb__Key=@Microsoft.KeyVault(VaultName=$KEY_VAULT;SecretName=CosmosDbKey)" \
    "BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$KEY_VAULT;SecretName=StorageConnectionString)"
```

## Step 9: Scaling Configuration

### Auto-scaling for App Service

```bash
# Enable autoscale
az monitor autoscale create \
  --resource-group $RESOURCE_GROUP \
  --resource $APP_SERVICE \
  --resource-type Microsoft.Web/serverfarms \
  --name autoscale-$APP_NAME \
  --min-count 1 \
  --max-count 5 \
  --count 2

# Scale on CPU > 70%
az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name autoscale-$APP_NAME \
  --condition "CpuPercentage > 70 avg 5m" \
  --scale out 1

# Scale in when CPU < 30%
az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name autoscale-$APP_NAME \
  --condition "CpuPercentage < 30 avg 10m" \
  --scale in 1
```

### CosmosDB Autoscale

```bash
# Update to autoscale throughput
az cosmosdb sql container throughput update \
  --account-name $COSMOS_ACCOUNT \
  --database-name HiveDb \
  --resource-group $RESOURCE_GROUP \
  --name documents \
  --max-throughput 4000
```

## Step 10: Monitoring and Alerts

### Create Alerts

```bash
# Alert on high response time
az monitor metrics alert create \
  --name "High Response Time" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$APP_SERVICE" \
  --condition "avg ResponseTime > 3000" \
  --description "Alert when avg response time > 3 seconds" \
  --evaluation-frequency 5m \
  --window-size 15m \
  --action <action-group-id>

# Alert on failed requests
az monitor metrics alert create \
  --name "Failed Requests" \
  --resource-group $RESOURCE_GROUP \
  --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Web/sites/$APP_SERVICE" \
  --condition "total Http5xx > 10" \
  --description "Alert when more than 10 5xx errors" \
  --evaluation-frequency 5m \
  --window-size 15m
```

## Step 11: Backup and Disaster Recovery

### CosmosDB Backup

CosmosDB has automatic continuous backup. Configure restore settings:

```bash
# Enable continuous backup (7 days)
az cosmosdb update \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --backup-policy-type Continuous

# Restore to point in time (if needed)
az cosmosdb sql database restore \
  --account-name $COSMOS_ACCOUNT \
  --database-name HiveDb \
  --resource-group $RESOURCE_GROUP \
  --restore-timestamp "2024-12-17T10:00:00Z"
```

### Blob Storage Backup

```bash
# Enable blob versioning
az storage account blob-service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --enable-versioning true

# Enable soft delete (30 days)
az storage account blob-service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --enable-delete-retention true \
  --delete-retention-days 30
```

## Step 12: CI/CD Pipeline

### GitHub Actions Example

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy-api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Build API
        run: |
          cd src/Hive.Api
          dotnet publish -c Release -o ./publish

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v2
        with:
          app-name: ${{ secrets.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: src/Hive.Api/publish

  build-and-deploy-functions:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Build Functions
        run: |
          cd src/Hive.Functions
          dotnet publish -c Release -o ./publish

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ secrets.AZURE_FUNCTION_APP_NAME }}
          package: src/Hive.Functions/publish
          publish-profile: ${{ secrets.AZURE_FUNCTION_PUBLISH_PROFILE }}

  build-and-deploy-frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Node.js
        uses: actions/setup-node@v3
        with:
          node-version: '18'

      - name: Build Frontend
        run: |
          cd frontend
          npm install
          npm run build
        env:
          VITE_API_URL: ${{ secrets.API_URL }}

      - name: Deploy to Static Web App
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "frontend"
          output_location: "dist"
```

## Cost Estimation (Monthly)

Based on moderate usage:

- **CosmosDB**: $24/month (400 RU/s per container × 3 containers)
- **App Service B1**: $13/month
- **Storage Account**: $5/month (100 GB storage + operations)
- **Functions Consumption**: $5/month (1M executions)
- **Application Insights**: $5/month (5 GB data)
- **Static Web App**: Free tier
- **CDN (optional)**: $10/month

**Total: ~$60-70/month**

## Production Checklist

- [ ] All secrets in Key Vault
- [ ] HTTPS enforced everywhere
- [ ] CORS configured correctly
- [ ] Authentication enabled (Azure AD)
- [ ] Auto-scaling configured
- [ ] Monitoring and alerts set up
- [ ] Backup and disaster recovery configured
- [ ] CI/CD pipeline working
- [ ] Custom domain and SSL certificate
- [ ] Rate limiting implemented
- [ ] Log retention configured
- [ ] Security scan completed
- [ ] Load testing performed
- [ ] Documentation updated

## Maintenance

### View Logs

```bash
# API logs
az webapp log tail --name $APP_SERVICE --resource-group $RESOURCE_GROUP

# Function logs
func azure functionapp logstream $FUNCTION_APP

# Application Insights queries
az monitor app-insights query \
  --app $APP_INSIGHTS \
  --analytics-query "requests | where timestamp > ago(1h) | summarize count() by resultCode"
```

### Update Application

```bash
# Update API
cd src/Hive.Api
dotnet publish -c Release -o ./publish
az webapp deployment source config-zip \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --src publish.zip

# Update Functions
cd ../Hive.Functions
func azure functionapp publish $FUNCTION_APP
```

## Troubleshooting

### Issue: High latency

- Check Application Insights for slow requests
- Review CosmosDB RU consumption
- Enable query metrics
- Consider increasing throughput

### Issue: 5xx errors

- Check App Service logs
- Verify all connection strings
- Check Key Vault access
- Review Application Insights exceptions

### Issue: Storage costs high

- Review blob lifecycle management
- Enable cool/archive tiers for old documents
- Check if soft-delete retention is too long

## Resources

- [Azure CosmosDB Best Practices](https://docs.microsoft.com/azure/cosmos-db/best-practices)
- [Azure Functions Best Practices](https://docs.microsoft.com/azure/azure-functions/functions-best-practices)
- [Azure App Service Best Practices](https://docs.microsoft.com/azure/app-service/app-service-best-practices)
