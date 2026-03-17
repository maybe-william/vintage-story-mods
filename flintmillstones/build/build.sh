#!/usr/bin/env bash
set -euo pipefail

# Ensure we run from the directory where this script lives, regardless of
# the current working directory when invoked.
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running build from: $(pwd)"

VERSION=$(cat ../src/flintmillstones/modinfo.json | grep '"Version"' | grep -oE '[0-9\.]+')
MODID=$(cat ../src/flintmillstones/modinfo.json | grep '"ModID"' | cut -f2 -d':' | tr -d '"' | tr -d ' ' | tr -d ',')
echo "Building $MODID version $VERSION"

rm ../dist/$MODID-$VERSION.zip || true
cd ../src/flintmillstones
zip -r ../../dist/$MODID-$VERSION.zip ./* -x "*.DS_Store"

echo "Build complete: for file dist/$MODID-$VERSION.zip"