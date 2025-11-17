#!/usr/bin/env pwsh

param(
    [string]$resourceGroupName,
    [string]$keyVaultName,
    [string]$webAppUrl,
    [string]$apiUrl
)

Write-Host "Starting post-provision setup..."

# Get environment variables set by azd
$resourceGroupName = $env:AZURE_RESOURCE_GROUP_NAME
$keyVaultName = $env:AZURE_KEY_VAULT_NAME
$webAppUrl = $env:WEB_BASE_URL
$apiUrl = $env:API_BASE_URL

Write-Host "Resource Group: $resourceGroupName"
Write-Host "Key Vault: $keyVaultName"
Write-Host "Web App URL: $webAppUrl"
Write-Host "API URL: $apiUrl"

# Check if logged in to Azure CLI
$account = az account show --query "user.name" -o tsv 2>$null
if (-not $account) {
    Write-Error "Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
}

Write-Host "Logged in as: $account"

# Create Azure AD App Registration
Write-Host "Creating Azure AD App Registration as SPA..."

$appName = "HealthcareAIModelEvaluator-App-$(Get-Random)"
$redirectUris = @(
    "http://localhost:3000",
    "https://localhost:3000",
    "http://localhost:3000/webapp/",
    "https://localhost:3000/webapp/"
)

if ($webAppUrl) {
    $redirectUris += "$webAppUrl/webapp/"
}

$redirectUrisJson = $redirectUris | ConvertTo-Json -Compress

# Create the app registration as SPA
$appRegistration = az ad app create `
    --display-name $appName `
    --sign-in-audience "AzureADMyOrg" `
    --spa-redirect-uris $redirectUrisJson `
    --enable-id-token-issuance true `
    --query "{appId: appId, objectId: id}" -o json | ConvertFrom-Json

if (-not $appRegistration) {
    Write-Error "Failed to create Azure AD App Registration"
    exit 1
}

$clientId = $appRegistration.appId
Write-Host "Created SPA App Registration with Client ID: $clientId"

# Update Key Vault secret with the actual client ID
Write-Host "Updating Key Vault secret with Client ID..."
az keyvault secret set `
    --vault-name $keyVaultName `
    --name "auth-client-id" `
    --value $clientId `
    --output none

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Successfully updated auth-client-id in Key Vault"
} else {
    Write-Error "❌ Failed to update Key Vault secret"
    exit 1
}

# Update Static Web App configuration

Write-Host ""
Write-Host "🎉 Post-provision setup completed successfully!"
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Update your Vite app's MSAL configuration to use the new Client ID: $clientId"
Write-Host "2. Add your production URLs to the app registration redirect URIs if needed"
Write-Host "3. Grant any additional API permissions if required"
Write-Host ""
Write-Host "Your application URLs:"
Write-Host "  Frontend: $webAppUrl"
Write-Host "  API: $apiUrl"
Write-Host "  Client ID: $clientId" 