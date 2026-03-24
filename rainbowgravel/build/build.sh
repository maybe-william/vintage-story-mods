#!/usr/bin/env bash
set -euo pipefail

# Ensure we run from the directory where this script lives, regardless of
# the current working directory when invoked.
cd "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Running build from: $(pwd)"

#TODO: Call the patch-building python script with correct configuration 
#- in order to generate the correct panning probabilities.
#python3 ./build_patch.py ../src/rainbowgravel/patches/patch_file.json

VERSION=$(cat ../src/rainbowgravel/modinfo.json | grep '"Version"' | grep -oE '[0-9\.]+')
MODID=$(cat ../src/rainbowgravel/modinfo.json | grep '"ModID"' | cut -f2 -d':' | tr -d '"' | tr -d ' ' | tr -d ',')
GAMEVERSION=$(cat ../src/rainbowgravel/gameversion | tr -d ' ')
echo "Building $MODID version $VERSION for Game Version $GAMEVERSION"

rm ../dist/$MODID-$VERSION-$GAMEVERSION.zip || true
cd ../src/rainbowgravel
zip -r ../../dist/$MODID-$VERSION-$GAMEVERSION.zip ./* -x "*.DS_Store"

echo "Build complete: for file dist/$MODID-$VERSION-$GAMEVERSION.zip"