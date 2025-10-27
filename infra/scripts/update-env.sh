#!/bin/bash
set -e

echo "Updating Vite environment variables for build..."

CLIENT_ID=$(azd env get-value AUTH_CLIENT_ID)
API_BASE_URL=$(azd env get-value API_BASE_URL)
TENANT_ID=$(azd env get-value AZURE_TENANT_ID)

echo "CLIENT_ID: $CLIENT_ID"
echo "API_BASE_URL: $API_BASE_URL"
echo "TENANT_ID: $TENANT_ID"

# Create or update .env.production file with Vite variables
cat > .env.production << EOF
VITE_CLIENT_ID=$CLIENT_ID
VITE_API_BASE_URL=$API_BASE_URL
VITE_TENANT_ID=$TENANT_ID
EOF

echo "✅ Updated .env.production with Vite environment variables"
cat .env.production 