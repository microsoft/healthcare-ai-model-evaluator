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
$clientId = $env:AUTH_CLIENT_ID

Write-Host "Resource Group: $resourceGroupName"
Write-Host "Key Vault: $keyVaultName"
Write-Host "Web App URL: $webAppUrl"
Write-Host "API URL: $apiUrl"
Write-Host "CLIENT_ID: $clientId"

# Check if logged in to Azure CLI
$account = az account show --query "user.name" -o tsv 2>$null
if (-not $account) {
    Write-Error "Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
}

$azureTenantId = az account show --query tenantId -o tsv
Write-Host "Logged in as: $account"
Write-Host "Tenant ID: $azureTenantId"

# Check if we already have a valid CLIENT_ID
if ($clientId -and $clientId -ne "placeholder-will-be-updated-by-script" -and $clientId -ne "00000000-0000-0000-0000-000000000000") {
    Write-Host "App registration already exists with Client ID: $clientId"
    Write-Host "✅ App registration is already configured correctly"
    
    # Update azd environment to ensure consistency
    azd env set AUTH_CLIENT_ID $clientId
    
    # Still update Key Vault with the existing CLIENT_ID
    Write-Host "Updating Key Vault secret with Client ID..."
    try {
        az keyvault secret set `
            --vault-name $keyVaultName `
            --name "auth-client-id" `
            --value $clientId `
            --output none
        Write-Host "✅ Key Vault secret updated successfully"
    } catch {
        Write-Error "❌ Failed to update Key Vault secret: $_"
        exit 1
    }
    
    # Configure managed identity role assignments for data services
    Write-Host "Configuring managed identity access to data services..."
    try {
        # Get Container App's managed identity principal ID
        $containerAppName = "api-$($env:AZURE_ENV_NAME)"
        $principalId = az containerapp show --name $containerAppName --resource-group $resourceGroupName --query "identity.principalId" -o tsv
        
        if ($principalId -and $principalId -ne "null") {
            Write-Host "Container App managed identity principal ID: $principalId"
            
            # Assign Cosmos DB role
            $cosmosAccountName = $env:COSMOS_ACCOUNT_NAME
            if ($cosmosAccountName) {
                Write-Host "Assigning Cosmos DB role to Container App managed identity..."
                $cosmosRoleId = "5bd9cd88-fe45-4216-938b-f97437e15450" # Cosmos DB Account Reader Writer
                az cosmosdb sql role assignment create `
                    --account-name $cosmosAccountName `
                    --resource-group $resourceGroupName `
                    --principal-id $principalId `
                    --role-definition-id $cosmosRoleId `
                    --scope "/subscriptions/$($env:AZURE_SUBSCRIPTION_ID)/resourceGroups/$resourceGroupName/providers/Microsoft.DocumentDB/databaseAccounts/$cosmosAccountName" `
                    --output none 2>$null || Write-Host "Role assignment may already exist"
                Write-Host "✅ Cosmos DB role assignment completed"
            }
            
            # Assign Storage role
            $storageAccountName = $env:STORAGE_ACCOUNT_NAME
            if ($storageAccountName) {
                Write-Host "Assigning Storage role to Container App managed identity..."
                az role assignment create `
                    --assignee $principalId `
                    --role "Storage Blob Data Contributor" `
                    --scope "/subscriptions/$($env:AZURE_SUBSCRIPTION_ID)/resourceGroups/$resourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName" `
                    --output none 2>$null || Write-Host "Role assignment may already exist"
                Write-Host "✅ Storage role assignment completed"
            }
        } else {
            Write-Host "⚠️  Warning: Could not get Container App managed identity principal ID"
        }
    } catch {
        Write-Host "⚠️  Warning: Failed to configure some role assignments: $_"
    }
    
    Write-Host ""
    Write-Host "✅ Post-provision setup completed successfully!"
    Write-Host ""
    Write-Host "Your Healthcare AI Model Evaluator is ready!"
    Write-Host "Application URL: $webAppUrl"
    Write-Host "API URL: $apiUrl"
    Write-Host "Client ID: $clientId" 
    exit 0
} elseif ($clientId -eq "432521be-fddf-45d4-8a9e-f9ff8495db08") {
    Write-Host "Using pre-configured existing app registration with Client ID: $clientId"
    Write-Host "✅ App registration is already configured correctly"
    exit 0
} else {
    Write-Host "Creating Azure AD App Registration as SPA..."
}

$appName = "HealthcareAIModelEvaluator-App-$(Get-Random)"
$redirectUris = @(
    "http://localhost:3000",
    "https://localhost:3000"
)

# Add the Container App URLs - API at root and webapp at /webapp
if ($apiUrl) {
    $redirectUris += $apiUrl
    $redirectUris += "$apiUrl/webapp"
}

# Create permissions.json for Microsoft Graph User.Read
$permissionsFile = Join-Path $PSScriptRoot "permissions.json"
if (-not (Test-Path $permissionsFile)) {
    Write-Host "Creating permissions.json for Microsoft Graph access..."
    $permissions = @(
        @{
            resourceAppId = "00000003-0000-0000-c000-000000000000"  # Microsoft Graph
            resourceAccess = @(
                @{
                    id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d"  # User.Read
                    type = "Scope"
                }
            )
        }
    )
    $permissions | ConvertTo-Json -Depth 4 | Out-File -FilePath $permissionsFile -Encoding UTF8
}

# Try creating the app registration as a Web Application (not SPA)
Write-Host "Creating app registration as Web Application for Container App hosting..."
Write-Host "App name: $appName"
Write-Host "Redirect URIs: $($redirectUris -join ', ')"

# Check if service management reference is available
$serviceManagementReference = $env:SERVICE_MANAGEMENT_REFERENCE
Write-Host "Service management reference from env: '$serviceManagementReference'"
if (-not $serviceManagementReference) {
    Write-Host "No SERVICE_MANAGEMENT_REFERENCE found in environment, using default"
    $serviceManagementReference = "b31d80b8-b7ef-49da-b3f9-c59ea728cb5f"
}

Write-Host "Using service management reference: $serviceManagementReference"
$result = az ad app create `
    --display-name $appName `
    --enable-id-token-issuance `
    --required-resource-accesses "@$permissionsFile" `
    --service-management-reference $serviceManagementReference `
    --query "{appId: appId, objectId: id}" -o json 2>&1

Write-Host "App registration creation result:"
Write-Host $result
Write-Host "Exit code: $LASTEXITCODE"

if ($LASTEXITCODE -eq 0) {
    try {
        $appRegistration = $result | ConvertFrom-Json
        $clientId = $appRegistration.appId
        $objectId = $appRegistration.objectId
        Write-Host "✅ Successfully created app registration with Client ID: $clientId"
        Write-Host "Object ID: $objectId"

        # Configure Web platform redirect URIs (not SPA)
        Write-Host "Configuring Web platform redirect URIs..."
        $body = @{
            web = @{
                redirectUris = $redirectUris
                implicitGrantSettings = @{
                    enableIdTokenIssuance = $true
                    enableAccessTokenIssuance = $false
                }
            }
        } | ConvertTo-Json -Depth 4

        $redirectResult = az rest `
            --method PATCH `
            --uri "https://graph.microsoft.com/v1.0/applications/$objectId" `
            --headers "Content-Type=application/json" `
            --body $body `
            --output none 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Successfully configured Web platform redirect URIs"
        } else {
            Write-Host "⚠️  Warning: Failed to configure redirect URIs automatically"
            Write-Host "Please manually add these redirect URIs in the Azure Portal under 'Web' platform:"
            foreach ($uri in $redirectUris) {
                Write-Host "  - $uri"
            }
        }

        } catch {
        Write-Host "❌ Failed to parse app registration JSON response: $_"
        Write-Host "Raw result: $result"
        Write-Host "Using placeholder CLIENT_ID for now..."
        $clientId = "00000000-0000-0000-0000-000000000000"
    }
} else {
    Write-Host "❌ Failed to create Azure AD App Registration. Error:"
    Write-Host $result
    Write-Host "Using placeholder CLIENT_ID for now..."
    $clientId = "00000000-0000-0000-0000-000000000000"
    Write-Host "⚠️  Using placeholder CLIENT_ID: $clientId"
    Write-Host "Please create an app registration manually and run: azd env set AUTH_CLIENT_ID <your-client-id>"
}

# Update azd environment with the CLIENT_ID (real or placeholder)
azd env set AUTH_CLIENT_ID $clientId

# Update Key Vault secret with the client ID
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

# Configure managed identity role assignments for data services
Write-Host "Configuring managed identity access to data services..."
try {
    # Get Container App's managed identity principal ID
    $containerAppName = "api-$($env:AZURE_ENV_NAME)"
    $principalId = az containerapp show --name $containerAppName --resource-group $resourceGroupName --query "identity.principalId" -o tsv
    
    if ($principalId -and $principalId -ne "null") {
        Write-Host "Container App managed identity principal ID: $principalId"
        
        # Assign Cosmos DB role
        $cosmosAccountName = $env:COSMOS_ACCOUNT_NAME
        if ($cosmosAccountName) {
            Write-Host "Assigning Cosmos DB role to Container App managed identity..."
            $cosmosRoleId = "5bd9cd88-fe45-4216-938b-f97437e15450" # Cosmos DB Account Reader Writer
            az cosmosdb sql role assignment create `
                --account-name $cosmosAccountName `
                --resource-group $resourceGroupName `
                --principal-id $principalId `
                --role-definition-id $cosmosRoleId `
                --scope "/subscriptions/$($env:AZURE_SUBSCRIPTION_ID)/resourceGroups/$resourceGroupName/providers/Microsoft.DocumentDB/databaseAccounts/$cosmosAccountName" `
                --output none 2>$null || Write-Host "Role assignment may already exist"
            Write-Host "✅ Cosmos DB role assignment completed"
        }
        
        # Assign Storage role
        $storageAccountName = $env:STORAGE_ACCOUNT_NAME
        if ($storageAccountName) {
            Write-Host "Assigning Storage role to Container App managed identity..."
            az role assignment create `
                --assignee $principalId `
                --role "Storage Blob Data Contributor" `
                --scope "/subscriptions/$($env:AZURE_SUBSCRIPTION_ID)/resourceGroups/$resourceGroupName/providers/Microsoft.Storage/storageAccounts/$storageAccountName" `
                --output none 2>$null || Write-Host "Role assignment may already exist"
            Write-Host "✅ Storage role assignment completed"
        }
    } else {
        Write-Host "⚠️  Warning: Could not get Container App managed identity principal ID"
    }
} catch {
    Write-Host "⚠️  Warning: Failed to configure some role assignments: $_"
}

Write-Host ""
Write-Host "🎉 Post-provision setup completed successfully!"
Write-Host ""
Write-Host "Your Healthcare AI Model Evaluator is ready!"
Write-Host ""
Write-Host "API URL: $apiUrl"
Write-Host "Frontend URL: $apiUrl/webapp"
Write-Host "Client ID: $clientId"
Write-Host ""
if ($clientId -eq "00000000-0000-0000-0000-000000000000") {
    Write-Host "⚠️  NEXT STEP REQUIRED: Manual app registration setup"
    Write-Host "1. Go to Azure Portal → Azure Active Directory → App registrations"
    Write-Host "2. Create a new Web application (not SPA)"
    Write-Host "3. Add these redirect URIs under 'Web' platform:"
    foreach ($uri in $redirectUris) {
        Write-Host "   - $uri"
    }
    Write-Host "4. Enable 'ID tokens' under Authentication → Implicit grant and hybrid flows"
    Write-Host "5. Note the Application (client) ID"
    Write-Host "6. Run: azd env set AUTH_CLIENT_ID <your-client-id>"
    Write-Host "7. Run: azd up --skip-build"
} else {
    Write-Host "✅ App registration configured automatically as Web application!"
    Write-Host "Your Container App is serving both the API and React frontend."
} 