#!/bin/sh
# Builds the Flatpak: publishes the app, then packs it with flatpak-builder. Run on Linux from anywhere.
# Needs the .NET 10 SDK, flatpak-builder and the org.freedesktop.Platform/Sdk 25.08 runtimes (flatpak install flathub org.freedesktop.Platform//25.08 org.freedesktop.Sdk//25.08).
set -eu
here=$(cd "$(dirname "$0")" && pwd)
root=$here/../..
app=io.github.hendy643.RelicWriter

# Self-contained linux-x64, trimmed (partial: the .NET libraries only, see docs/installer.md) and ReadyToRun.
props="-c Release -r linux-x64 -p:PublishTrimmed=true -p:TrimMode=partial -p:PublishReadyToRun=true -p:InvariantGlobalization=true -p:DebugType=None -p:DebugSymbols=false"
rm -rf "$here/publish"
dotnet restore "$root/RelicWriter.Desktop/RelicWriter.Desktop.csproj" -r linux-x64 -p:Configuration=Release -p:PublishTrimmed=true -p:PublishReadyToRun=true
dotnet publish "$root/RelicWriter.Desktop/RelicWriter.Desktop.csproj" --no-restore --self-contained true $props -o "$here/publish"
# Native symbol files are large and of no use to users.
find "$here/publish" -name '*.pdb' -delete

cd "$here"
flatpak-builder --force-clean --user --install-deps-from=flathub --repo=repo --install build-dir "$app.yml"
flatpak build-bundle repo "$here/RelicWriter.flatpak" "$app"
echo "Built $here/RelicWriter.flatpak; run it with: flatpak run $app"
