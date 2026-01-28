#!/bin/bash
set -euo pipefail

# Admin User Creation Script for MedBench (Cosmos DB SQL API)
#
# This deployment uses Cosmos DB SQL API with local auth disabled.
# Admin user creation therefore uses Microsoft Entra ID (DefaultAzureCredential)
# and requires data-plane RBAC for the caller.

echo "Setting up first admin user (Cosmos DB SQL API, Entra ID auth)..."

if ! command -v python3 >/dev/null 2>&1; then
    echo "❌ python3 is required. Install Python 3 and retry."
    exit 1
fi

COSMOS_ENDPOINT=$(azd env get-value COSMOSDB_ENDPOINT 2>/dev/null || echo "")
COSMOS_DATABASE=$(azd env get-value COSMOSDB_DATABASE 2>/dev/null || echo "")

if [ -z "$COSMOS_ENDPOINT" ] || [ -z "$COSMOS_DATABASE" ]; then
    echo "❌ Missing azd env values. Ensure you've run 'azd up' and that these are set:"
    echo "   COSMOSDB_ENDPOINT"
    echo "   COSMOSDB_DATABASE"
    exit 1
fi

read -p "Enter admin email: " ADMIN_EMAIL
while [[ ! "$ADMIN_EMAIL" =~ ^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$ ]]; do
    echo "❌ Please enter a valid email address"
    read -p "Enter admin email: " ADMIN_EMAIL
done

read -p "Enter admin display name: " ADMIN_NAME
if [ -z "$ADMIN_NAME" ]; then
    echo "❌ Name is required"
    exit 1
fi

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"

echo ""
echo "Installing Python deps (azure-cosmos, azure-identity)..."
python3 -m pip install -r "$ROOT_DIR/tools/cosmos-migration/requirements.txt" >/dev/null

echo ""
echo "Creating admin user via Cosmos SQL API (requires 'az login' and Cosmos RBAC)..."
python3 "$ROOT_DIR/tools/cosmos-migration/create_admin_user.py" \
    --endpoint "$COSMOS_ENDPOINT" \
    --database "$COSMOS_DATABASE" \
    --email "$ADMIN_EMAIL" \
    --name "$ADMIN_NAME"

echo "✅ Done."

exit 0