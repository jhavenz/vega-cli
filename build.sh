#!/bin/bash

set -e

echo "Building Vega Development CLI..."

if ! command -v dotnet &> /dev/null; then
    echo ".NET SDK is required but not installed"
    echo "Install from: https://dotnet.microsoft.com/download"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "Using .NET version: $DOTNET_VERSION"

echo "Restoring packages..."
dotnet restore

echo "Building release version..."
dotnet build -c Release --no-restore

echo "Publishing self-contained executable..."
dotnet publish -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true --output ../bin

VEGA_EXECUTABLE="../bin/vega"

chmod +x "$VEGA_EXECUTABLE"

ln -sf "bin/vega" "../vega" 2>/dev/null || true

echo "Build completed successfully!"
echo ""
echo "Installation:"
echo "  The 'vega' executable is now available at: $(pwd)/../bin/vega"
echo "  Convenience symlink created: $(pwd)/../vega"
echo ""
echo "To use globally, add to your PATH or create a symlink:"
echo "  ln -sf $(pwd)/../bin/vega /usr/local/bin/vega"
echo ""
echo "Quick test:"
echo "  ../vega --version"
echo "  ../vega examples"
echo "  ../vega system status"