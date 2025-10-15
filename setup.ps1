#!/usr/bin/env pwsh

Write-Host "🚀 Healthcare AI Model Evaluator Azure Deployment Setup" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check if Azure CLI is installed
try {
    az --version | Out-Null
    Write-Host "✅ Azure CLI is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI is not installed. Please install it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Red
    exit 1
}

# Check if azd is installed
try {
    azd version | Out-Null
    Write-Host "✅ Azure Developer CLI (azd) is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure Developer CLI (azd) is not installed. Please install it from: https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd" -ForegroundColor Red
    exit 1
}

# Check if Docker is installed
try {
    docker --version | Out-Null
    Write-Host "✅ Docker is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker is not installed. Please install it from: https://docs.docker.com/get-docker/" -ForegroundColor Red
    exit 1
}

# Check if Node.js is installed
try {
    node --version | Out-Null
    Write-Host "✅ Node.js is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Node.js is not installed. Please install it from: https://nodejs.org/" -ForegroundColor Red
    exit 1
}

# Check if .NET is installed
try {
    dotnet --version | Out-Null
    Write-Host "✅ .NET SDK is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET 8 SDK is not installed. Please install it from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

Write-Host "✅ All prerequisites are installed!" -ForegroundColor Green

# Check if user is logged in to Azure
Write-Host "Checking Azure authentication..." -ForegroundColor Yellow
try {
    az account show | Out-Null
    Write-Host "✅ Azure CLI authentication verified" -ForegroundColor Green
} catch {
    Write-Host "🔐 Please log in to Azure CLI:" -ForegroundColor Yellow
    az login
}

try {
    azd auth login --check-status | Out-Null
    Write-Host "✅ Azure Developer CLI authentication verified" -ForegroundColor Green
} catch {
    Write-Host "🔐 Please log in to Azure Developer CLI:" -ForegroundColor Yellow
    azd auth login
}

# Create environment file if it doesn't exist
if (-not (Test-Path ".env")) {
    Write-Host "📝 Creating .env file from template..." -ForegroundColor Yellow
    Copy-Item ".env.example" ".env"
    Write-Host "✅ Created .env file. Please edit it with your preferred settings." -ForegroundColor Green
    Write-Host "   You can set AZURE_ENV_NAME and AZURE_LOCATION, or use the defaults." -ForegroundColor Cyan
} else {
    Write-Host "✅ .env file already exists." -ForegroundColor Green
}

Write-Host ""
Write-Host "🎉 Setup complete! You can now deploy your application:" -ForegroundColor Green
Write-Host ""
Write-Host "1. Review and edit .env file if needed:" -ForegroundColor Cyan
Write-Host "   notepad .env" -ForegroundColor White
Write-Host ""
Write-Host "2. Initialize azd (if not already done):" -ForegroundColor Cyan
Write-Host "   azd init" -ForegroundColor White
Write-Host ""
Write-Host "3. Deploy the application:" -ForegroundColor Cyan
Write-Host "   azd up" -ForegroundColor White
Write-Host ""
Write-Host "For more detailed instructions, see DEPLOYMENT.md" -ForegroundColor Cyan 