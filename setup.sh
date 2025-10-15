#!/bin/bash
set -euo pipefail

echo "🚀 Healthcare AI Model Evaluator Azure Deployment Setup"
echo "=================================="

# Check prerequisites
echo "Checking prerequisites..."

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI is not installed. Please install it from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check if azd is installed
if ! command -v azd &> /dev/null; then
    echo "❌ Azure Developer CLI (azd) is not installed. Please install it from: https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install it from: https://docs.docker.com/get-docker/"
    exit 1
fi

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    echo "❌ Node.js is not installed. Please install it from: https://nodejs.org/"
    exit 1
fi

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET 8 SDK is not installed. Please install it from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

echo "✅ All prerequisites are installed!"

# Check if user is logged in to Azure
echo "Checking Azure authentication..."
if ! az account show &> /dev/null; then
    echo "🔐 Please log in to Azure CLI:"
    az login
fi

if ! azd auth login --check-status &> /dev/null; then
    echo "🔐 Please log in to Azure Developer CLI:"
    azd auth login
fi

echo "✅ Azure authentication verified!"

# Create environment file if it doesn't exist
if [ ! -f ".env" ]; then
    echo "📝 Creating .env file from template..."
    cp .env.example .env
    echo "✅ Created .env file. Please edit it with your preferred settings."
    echo "   You can set AZURE_ENV_NAME and AZURE_LOCATION, or use the defaults."
else
    echo "✅ .env file already exists."
fi

echo ""
echo "🎉 Setup complete! You can now deploy your application:"
echo ""
echo "1. Review and edit .env file if needed:"
echo "   nano .env"
echo ""
echo "2. Initialize azd (if not already done):"
echo "   azd init"
echo ""
echo "3. Deploy the application:"
echo "   azd up"
echo ""
echo "For more detailed instructions, see DEPLOYMENT.md" 