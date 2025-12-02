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
    "https://localhost:3000"
)

if ($apiUrl) {
    $redirectUris += "$apiUrl/webapp"
}

# For Microsoft tenant, we need a service management reference
$serviceManagementReference = if ($env:SERVICE_MANAGEMENT_REFERENCE) { 
    $env:SERVICE_MANAGEMENT_REFERENCE 
} else { 
    "b31d80b8-b7ef-49da-b3f9-c59ea728cb5f" 
}

# Try creating the app registration with service management reference first
Write-Host "Attempting to create app registration with service management reference..."

# Create permissions.json if it doesn't exist
$permissionsFile = Join-Path $PSScriptRoot "permissions.json"
if (-not (Test-Path $permissionsFile)) {
    Write-Host "Creating basic permissions.json..."
    $permissions = @(
        @{
            resourceAppId = "00000003-0000-0000-c000-000000000000"
            resourceAccess = @(
                @{
                    id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"
                    type = "Scope"
                }
            )
        }
    )
    $permissions | ConvertTo-Json -Depth 4 | Out-File -FilePath $permissionsFile -Encoding UTF8
}

$appRegistration = $null
$appCreationFailed = $false

# First attempt with service management reference
$result = az ad app create `
    --display-name $appName `
    --enable-id-token-issuance `
    --required-resource-accesses "@$permissionsFile" `
    --service-management-reference $serviceManagementReference `
    --query "{appId: appId, objectId: id}" -o json 2>&1

if ($LASTEXITCODE -eq 0) {
    $appRegistration = $result | ConvertFrom-Json
    Write-Host "✅ Successfully created app registration with service management reference"
} else {
    Write-Host "⚠️  Failed to create app with service management reference. Error:"
    Write-Host $result
    Write-Host "Retrying without service management reference..."
    
    # Second attempt without service management reference
    $result = az ad app create `
        --display-name $appName `
        --enable-id-token-issuance `
        --required-resource-accesses "@$permissionsFile" `
        --query "{appId: appId, objectId: id}" -o json 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $appRegistration = $result | ConvertFrom-Json
        Write-Host "✅ Successfully created app registration without service management reference"
    } else {
        Write-Host "❌ Failed to create Azure AD App Registration. Error:"
        Write-Host $result
        $appCreationFailed = $true
    }
}

if ($appCreationFailed -or -not $appRegistration) {
    Write-Host "Using placeholder CLIENT_ID for now..."
    $clientId = "00000000-0000-0000-0000-000000000000"
    Write-Host "⚠️  Using placeholder CLIENT_ID: $clientId"
    Write-Host "Please create an app registration manually and run: azd env set AUTH_CLIENT_ID <your-client-id>"
} else {

} else {
    $clientId = $appRegistration.appId
    $objectId = $appRegistration.objectId
    Write-Host "Created App Registration with Client ID: $clientId"

    # Configure SPA redirect URIs using Microsoft Graph API
    Write-Host "Configuring SPA redirect URIs..."
    $body = @{
        spa = @{
            redirectUris = $redirectUris
        }
    } | ConvertTo-Json -Depth 3

    $result = az rest `
        --method PATCH `
        --uri "https://graph.microsoft.com/v1.0/applications/$objectId" `
        --headers "Content-Type=application/json" `
        --body $body `
        --output none

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Successfully configured SPA redirect URIs"
    } else {
        Write-Host "⚠️  Warning: Failed to configure SPA redirect URIs, but app registration was created"
        Write-Host "Please manually add the following redirect URIs in the Azure Portal:"
        foreach ($uri in $redirectUris) {
            Write-Host "  - $uri"
        }
    }
}

# Update azd environment with the CLIENT_ID (real or placeholder)
az env set AUTH_CLIENT_ID $clientId

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