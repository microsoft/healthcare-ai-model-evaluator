#!/bin/bash

# Generic Add-on Deployment Script
# This script packages any addon with the MedBench package for Azure Functions deployment
# Usage: ./package_addon.sh <addon_name>

set -e

# Check if addon name is provided
if [ $# -lt 1 ]; then
    echo "Usage: $0 <addon_name>"
    echo "Available addons:"
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    for addon_dir in "$SCRIPT_DIR"/*/; do
        if [ -f "${addon_dir}function_app.py" ]; then
            addon_name=$(basename "$addon_dir")
            echo "  - $addon_name"
        fi
    done
    exit 1
fi

ADDON_NAME="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADDON_DIR="$SCRIPT_DIR/$ADDON_NAME"
MEDBENCH_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$ADDON_DIR/build"

# Validate addon exists
if [ ! -d "$ADDON_DIR" ]; then
    echo "❌ Error: Addon '$ADDON_NAME' not found in $SCRIPT_DIR"
    exit 1
fi

if [ ! -f "$ADDON_DIR/function_app.py" ]; then
    echo "❌ Error: function_app.py not found in addon '$ADDON_NAME'"
    exit 1
fi

echo "🚀 Building $ADDON_NAME Add-on Package"
echo "Addon directory: $ADDON_DIR"
echo "MedBench root: $MEDBENCH_ROOT"

# Clean and create build directory
echo "📦 Preparing build directory..."
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Copy addon files
echo "📋 Copying addon files..."
cp "$ADDON_DIR/function_app.py" "$BUILD_DIR/"
cp "$ADDON_DIR/host.json" "$BUILD_DIR/"
cp "$ADDON_DIR/local.settings.json" "$BUILD_DIR/" 2>/dev/null || echo "⚠️  local.settings.json not found, skipping..."

# Copy MedBench package
echo "📚 Copying MedBench package..."
cp -r "$MEDBENCH_ROOT/medbench" "$BUILD_DIR/"

# Copy requirements structure and override addon.txt
echo "📦 Setting up requirements..."
cp -r "$MEDBENCH_ROOT/requirements" "$BUILD_DIR/"
cp "$MEDBENCH_ROOT/requirements.txt" "$BUILD_DIR/"
# Override addon.txt with this addon's requirements
if [ -f "$ADDON_DIR/requirements.txt" ]; then
    cp "$ADDON_DIR/requirements.txt" "$BUILD_DIR/requirements/addon.txt"
else
    echo "⚠️  requirements.txt not found for addon '$ADDON_NAME', using empty addon.txt"
    touch "$BUILD_DIR/requirements/addon.txt"
fi

# Create deployment package
echo "✅ Package prepared: $BUILD_DIR/"
echo "📁 Contents ready for Azure Developer CLI packaging"
echo ""
echo "To deploy this addon:"
echo "  cd $BUILD_DIR"
echo "  azd deploy"
