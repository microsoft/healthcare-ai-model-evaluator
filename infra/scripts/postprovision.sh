#!/bin/bash
set -euo pipefail

echo "Starting post-provision setup..."

# Detect if we're on Windows and try PowerShell first
if command -v pwsh >/dev/null 2>&1; then
    echo "PowerShell detected, using PowerShell script..."
    pwsh -File "$(dirname "$0")/postprovision.ps1"
    exit $?
elif command -v powershell >/dev/null 2>&1; then
    echo "Windows PowerShell detected, using PowerShell script..."
    powershell -File "$(dirname "$0")/postprovision.ps1"
    exit $?
fi

echo "Using bash script..."

# Get environment variables set by azd
RESOURCE_GROUP_NAME=${AZURE_RESOURCE_GROUP_NAME:-}
KEY_VAULT_NAME=${AZURE_KEY_VAULT_NAME:-}
WEB_APP_URL=${WEB_BASE_URL:-}
API_URL=${API_BASE_URL:-}
CLIENT_ID=${AUTH_CLIENT_ID:-}

echo "Resource Group: $RESOURCE_GROUP_NAME"
echo "Key Vault: $KEY_VAULT_NAME"
echo "Web App URL: $WEB_APP_URL"
echo "API URL: $API_URL"
echo "CLIENT_ID: $CLIENT_ID"

# Check if logged in to Azure CLI
if ! az account show >/dev/null 2>&1; then
    echo "❌ Not logged in to Azure CLI. Please run 'az login' first."
    exit 1
fi

ACCOUNT=$(az account show --query "user.name" -o tsv)
AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Logged in as: $ACCOUNT"
echo "Tenant ID: $AZURE_TENANT_ID"

# Check if we already have a valid CLIENT_ID
if [ -n "$CLIENT_ID" ] && [ "$CLIENT_ID" != "placeholder-will-be-updated-by-script" ] && [ "$CLIENT_ID" != "00000000-0000-0000-0000-000000000000" ]; then
    echo "App registration already exists with Client ID: $CLIENT_ID"
    echo "✅ App registration is already configured correctly"
elif [ "$CLIENT_ID" = "432521be-fddf-45d4-8a9e-f9ff8495db08" ]; then
    echo "Using pre-configured existing app registration with Client ID: $CLIENT_ID"
    echo "✅ App registration is already configured correctly"
else
    echo "Creating Azure AD App Registration..."
    
    # For Microsoft tenant, we need a service management reference
    # This is typically a GUID that identifies the service/application
    SERVICE_MANAGEMENT_REFERENCE=${SERVICE_MANAGEMENT_REFERENCE:-"b31d80b8-b7ef-49da-b3f9-c59ea728cb5f"}
    
    APP_NAME="HealthcareAIModelEvaluator-App-${AZURE_ENV_NAME:-$(date +%s)}"
    
    # Create permissions.json if it doesn't exist
    PERMISSIONS_FILE="$(dirname "$0")/permissions.json"
    if [ ! -f "$PERMISSIONS_FILE" ]; then
        echo "Creating basic permissions.json..."
        cat > "$PERMISSIONS_FILE" << 'EOF'
[
  {
    "resourceAppId": "00000003-0000-0000-c000-000000000000",
    "resourceAccess": [
      {
        "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
        "type": "Scope"
      }
    ]
  }
]
EOF
    fi
    
    # Try creating the app registration - first attempt with service management reference for Microsoft tenant
    echo "Attempting to create SPA app registration with service management reference..."
    APP=""
    APP_CREATION_FAILED=false
    
    # Capture both stdout and stderr for better error reporting
    if APP_OUTPUT=$(az ad app create \
        --display-name "$APP_NAME" \
        --enable-id-token-issuance \
        --required-resource-accesses "@$PERMISSIONS_FILE" \
        --service-management-reference "$SERVICE_MANAGEMENT_REFERENCE" \
        --query "{appId: appId, objectId: id}" -o json 2>&1); then
        APP="$APP_OUTPUT"
        echo "✅ Successfully created app registration with service management reference"
    else
        echo "⚠️  Failed to create app with service management reference. Error:"
        echo "$APP_OUTPUT"
        echo "Retrying without service management reference..."
        
        # If that fails, try without service management reference (for non-Microsoft tenants)
        if APP_OUTPUT=$(az ad app create \
            --display-name "$APP_NAME" \
            --enable-id-token-issuance \
            --required-resource-accesses "@$PERMISSIONS_FILE" \
            --query "{appId: appId, objectId: id}" -o json 2>&1); then
            APP="$APP_OUTPUT"
            echo "✅ Successfully created app registration without service management reference"
        else
            echo "❌ Failed to create Azure AD App Registration. Error:"
            echo "$APP_OUTPUT"
            echo ""
            echo "This might be due to:"
            echo "1. Insufficient permissions to create app registrations"
            echo "2. Tenant policy restrictions"
            echo "3. Invalid service management reference"
            echo ""
            echo "⚠️  Continuing deployment with placeholder CLIENT_ID"
            echo "You can manually create an app registration later and update the AUTH_CLIENT_ID environment variable."
            APP_CREATION_FAILED=true
        fi
    fi

    if [ "$APP_CREATION_FAILED" = true ] || [ -z "$APP" ] || [ "$APP" = "null" ]; then
        echo "Using placeholder CLIENT_ID for now..."
        CLIENT_ID="00000000-0000-0000-0000-000000000000"
        echo "⚠️  Using placeholder CLIENT_ID: $CLIENT_ID"
        echo "Please create an app registration manually and run: azd env set AUTH_CLIENT_ID <your-client-id>"
    else
        CLIENT_ID=$(echo "$APP" | jq -r '.appId')
        APP_OBJECT_ID=$(echo "$APP" | jq -r '.objectId')
        
        if [ -z "$CLIENT_ID" ] || [ "$CLIENT_ID" = "null" ]; then
            echo "❌ Failed to extract Client ID from app registration response"
            echo "Response: $APP"
            echo "Using placeholder CLIENT_ID..."
            CLIENT_ID="00000000-0000-0000-0000-000000000000"
        else
            echo "Created SPA App Registration with Client ID: $CLIENT_ID"
            
            # Configure SPA redirect URIs using Microsoft Graph API
            echo "Configuring SPA redirect URIs..."
            # Build redirect URIs JSON robustly with jq to avoid quoting issues
            SPA_REDIRECTS=("http://localhost:3000" "https://localhost:3000")
            if [ -n "${API_URL:-}" ]; then
                SPA_REDIRECTS+=("${API_URL}/webapp")
            fi
            SPA_REDIRECT_URIS_JSON=$(printf '%s\n' "${SPA_REDIRECTS[@]}" | jq -R . | jq -s .)
            BODY=$(jq -n --argjson uris "$SPA_REDIRECT_URIS_JSON" '{spa: {redirectUris: $uris}}')

            if [ -z "${APP_OBJECT_ID:-}" ] || [ "$APP_OBJECT_ID" = "null" ]; then
                echo "❌ Failed to determine application object ID; cannot set redirect URIs"
            elif az rest \
                --method PATCH \
                --uri "https://graph.microsoft.com/v1.0/applications/$APP_OBJECT_ID" \
                --headers "Content-Type=application/json" \
                --body "$BODY" \
                --output none; then
                echo "✅ Successfully configured SPA redirect URIs"
            else
                echo "⚠️  Warning: Failed to configure SPA redirect URIs, but app registration was created"
                echo "Please manually add the following redirect URIs in the Azure Portal:"
                for uri in "${SPA_REDIRECTS[@]}"; do echo "  - $uri"; done
                echo "Request body was: $BODY"
            fi
            
            echo "✅ Successfully configured app registration as SPA with redirect URIs"
        fi
    fi
    
    # Update azd environment with the CLIENT_ID (real or placeholder)
    azd env set AUTH_CLIENT_ID "$CLIENT_ID"
fi

# Update Key Vault secret with the actual client ID
echo "Updating Key Vault secret with Client ID..."
if az keyvault secret set \
    --vault-name "$KEY_VAULT_NAME" \
    --name "auth-client-id" \
    --value "$CLIENT_ID" \
    --output none; then
    echo "✅ Successfully updated auth-client-id in Key Vault"
else
    echo "❌ Failed to update Key Vault secret"
    exit 1
fi

echo ""
if [ "$CLIENT_ID" = "00000000-0000-0000-0000-000000000000" ]; then
    echo "⚠️  Post-provision setup completed with placeholder CLIENT_ID!"
    echo ""
    echo "IMPORTANT: Please manually create an Azure AD App Registration and update the CLIENT_ID:"
    echo "1. Go to Azure Portal > Azure Active Directory > App registrations"
    echo "2. Create a new registration with these settings:"
    echo "   - Name: HealthcareAIModelEvaluator-App-${AZURE_ENV_NAME}"
    echo "   - Supported account types: Single tenant"
    echo "   - Redirect URI: SPA, ${API_URL}/webapp"
    echo "3. Copy the Application (client) ID"
    echo "4. Run: azd env set AUTH_CLIENT_ID <your-client-id>"
    echo "5. Run: azd deploy to update the configuration"
else
    echo "🎉 Post-provision setup completed successfully!"
fi
echo ""
echo "Your application URLs:"
echo "  Application: ${API_URL}/webapp"
echo "  API: $API_URL"
echo "  Client ID: $CLIENT_ID"