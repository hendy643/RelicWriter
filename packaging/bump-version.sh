#!/usr/bin/env bash
# Sets the version of a new release everywhere it is written, ready to be committed on a release branch:
#   version.json (app and every component), the release entry of the Flatpak metainfo, the "## Unreleased" heading of CHANGELOG.md (renamed to the version and date),
#   and, through a build, the project files and manifests.
# Usage: packaging/bump-version.sh 0.2.0     (or 0.2.0-beta.1)
set -euo pipefail
version="${1:?Usage: bump-version.sh <x.y.z or x.y.z-pre.release>}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || { echo "'$version' is not x.y.z or x.y.z-pre.release." >&2; exit 1; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."

sed -E -i "s/(\"app\"[[:space:]]*:[[:space:]]*\")[^\"]*\"/\1$version\"/; s/(\"RelicWriter[A-Za-z0-9.]*\"[[:space:]]*:[[:space:]]*\")[^\"]*\"/\1$version\"/" version.json

meta=packaging/flatpak/io.github.hendy643.RelicWriter.metainfo.xml
sed -E -i "s/<release version=\"[^\"]*\" date=\"[^\"]*\" \/>/<release version=\"$version\" date=\"$(date +%F)\" \/>/" "$meta"

# The change log's open section becomes this release's; a release with nothing under "## Unreleased" has no notes, so it is refused.
grep -Eq '^## Unreleased[[:space:]]*$' CHANGELOG.md || { echo 'CHANGELOG.md has no "## Unreleased" section to release.' >&2; exit 1; }
sed -E -i "s/^## Unreleased[[:space:]]*$/## $version · $(LC_ALL=C date '+%-d %B %Y')/" CHANGELOG.md

# The build copies the numbers into the project files and manifests.
dotnet build RelicWriter.slnx -c Release
git status --short
echo "Version $version written. Commit these changes on a release branch and open a pull request to master."
