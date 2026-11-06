#!/bin/bash
# NapeBar for macOS をビルドし、build/NapeBar-mac.zip を作る。
# 必要なもの: Xcode, XcodeGen (brew install xcodegen)
set -euo pipefail
cd "$(dirname "$0")"

xcodegen generate --quiet

xcodebuild \
  -project NapeBar.xcodeproj \
  -scheme NapeBar \
  -configuration Release \
  -destination 'generic/platform=macOS' \
  -derivedDataPath build/DerivedData \
  -quiet \
  ONLY_ACTIVE_ARCH=NO \
  build

app="build/DerivedData/Build/Products/Release/NapeBar.app"
rm -f build/NapeBar-mac.zip
ditto -c -k --keepParent "$app" build/NapeBar-mac.zip
echo "Built: $(pwd)/$app"
echo "Zip:   $(pwd)/build/NapeBar-mac.zip"
