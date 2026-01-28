#!/usr/bin/env pwsh

param(
    [string]$adminEmail,
    [string]$adminPassword,
    [string]$adminName
)

Write-Host "Setting up first admin user (Cosmos DB SQL API, Entra ID auth)..."

if (-not (Get-Command python3 -ErrorAction SilentlyContinue)) {
    Write-Error "❌ python3 is required. Install Python 3 and retry."
    exit 1
}

$cosmosEndpoint = azd env get-value COSMOSDB_ENDPOINT 2>$null
$cosmosDatabase = azd env get-value COSMOSDB_DATABASE 2>$null

if (-not $cosmosEndpoint -or -not $cosmosDatabase) {
    Write-Error "❌ Missing azd env values. Ensure you've run 'azd up' and that these are set: COSMOSDB_ENDPOINT, COSMOSDB_DATABASE"
    exit 1
}

if (-not $adminEmail) {
    do {
        $adminEmail = Read-Host "Enter admin email"
        if ($adminEmail -notmatch "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$") {
            Write-Host "❌ Please enter a valid email address"
            $adminEmail = $null
        }
    } while (-not $adminEmail)
}

if (-not $adminName) {
    $adminName = Read-Host "Enter admin display name"
}

if (-not $adminName) {
    Write-Error "❌ Name is required"
    exit 1
}

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")

Write-Host "Installing Python deps (azure-cosmos, azure-identity)..."
python3 -m pip install -r (Join-Path $rootDir "tools/cosmos-migration/requirements.txt") | Out-Null

Write-Host "Creating admin user via Cosmos SQL API (requires 'az login' and Cosmos RBAC)..."
python3 (Join-Path $rootDir "tools/cosmos-migration/create_admin_user.py") `
    --endpoint $cosmosEndpoint `
    --database $cosmosDatabase `
    --email $adminEmail `
    --name $adminName

Write-Host "✅ Done."
exit 0