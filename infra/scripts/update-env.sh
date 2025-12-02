#!/bin/bash
set -e

echo "Updating Vite environment variables for build..."

# Get environment variables with fallback values for initial deployment
CLIENT_ID=$(azd env get-value AUTH_CLIENT_ID 2>/dev/null || echo "")
API_BASE_URL=$(azd env get-value API_BASE_URL 2>/dev/null || echo "")
TENANT_ID=$(azd env get-value AZURE_TENANT_ID 2>/dev/null || echo "")

echo "CLIENT_ID: $CLIENT_ID"
echo "API_BASE_URL: $API_BASE_URL"
echo "TENANT_ID: $TENANT_ID"

# Use placeholder values if environment variables are not set (during initial deployment)
if [ -z "$CLIENT_ID" ]; then
    CLIENT_ID="placeholder-client-id"
fi
if [ -z "$API_BASE_URL" ]; then
    API_BASE_URL="placeholder-api-url"
fi
if [ -z "$TENANT_ID" ]; then
    TENANT_ID="placeholder-tenant-id"
fi

# Create or update .env.production file with Vite variables
cat > .env.production << EOF
VITE_CLIENT_ID=$CLIENT_ID
VITE_API_BASE_URL=$API_BASE_URL
VITE_TENANT_ID=$TENANT_ID
EOF

echo "✅ Updated .env.production with Vite environment variables"
cat .env.production 