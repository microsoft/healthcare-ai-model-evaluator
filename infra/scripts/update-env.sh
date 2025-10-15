#!/bin/bash
set -e

echo "Updating React environment variables for build..."

CLIENT_ID=$(azd env get-value AUTH_CLIENT_ID)
API_BASE_URL=$(azd env get-value API_BASE_URL)
TENANT_ID=$(azd env get-value AZURE_TENANT_ID)

echo "CLIENT_ID: $CLIENT_ID"
echo "API_BASE_URL: $API_BASE_URL"
echo "TENANT_ID: $TENANT_ID"

# Create or update .env.production file
cat > .env.production << EOF
REACT_APP_CLIENT_ID=$CLIENT_ID
REACT_APP_API_BASE_URL=$API_BASE_URL
REACT_APP_TENANT_ID=$TENANT_ID
EOF

echo "✅ Updated .env.production with environment variables"
cat .env.production 