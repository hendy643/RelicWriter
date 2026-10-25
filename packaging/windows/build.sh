#!/usr/bin/env bash
# Publishes the Windows app as one self-contained exe: packaging/windows/publish/RelicWriter.exe. Run from anywhere, on Windows (Git Bash) or Linux; needs the .NET 10 SDK.
# Single file (native libraries inside, compressed), ReadyToRun (precompiled native code), partially trimmed (the .NET libraries only), no pdbs. See docs/windows.md.
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$here/../../RelicWriter.Desktop/RelicWriter.Desktop.csproj"
out="$here/publish"

rm -rf "$out"
dotnet restore "$project" -r win-x64 -p:Configuration=Release -p:PublishReadyToRun=true -p:PublishTrimmed=true -p:PublishSingleFile=true
dotnet publish "$project" -c Release -r win-x64 --no-restore --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true \
    -p:PublishReadyToRun=true -p:PublishTrimmed=true -p:TrimMode=partial \
    -p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o "$out"
# The native Skia and HarfBuzz symbol files are copied from their NuGet packages whatever the switches say.
find "$out" -name '*.pdb' -delete
mv "$out/RelicWriter.Desktop.exe" "$out/RelicWriter.exe"
echo "Built $out/RelicWriter.exe"
