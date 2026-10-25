#!/usr/bin/env bash
# Makes a release the git flow way (see docs/releasing.md): a release/<version> branch from develop with the version written, a pull request into master
# (merging it starts the Release workflow), then, once the release is published, a pull request that merges master back into develop.
# Needs the gh CLI, logged in as the owner or a maintainer.
# Usage: packaging/release.sh 1.0.0     (or 1.0.0-beta.1)
set -euo pipefail
version="${1:?Usage: release.sh <x.y.z or x.y.z-pre.release>}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || { echo "'$version' is not x.y.z or x.y.z-pre.release." >&2; exit 1; }
branch="release/$version"
here="$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"

# The checks start a few seconds after a pull request is opened; wait for them to be reported, then for them to finish.
wait_for_checks() {
    for _ in $(seq 36); do
        gh pr checks "$1" 2>&1 | grep -q 'no checks reported' || break
        sleep 5
    done
    gh pr checks "$1" --watch
}

cd "$here/.."
[ -z "$(git status --porcelain)" ] || { echo 'The working tree has uncommitted changes.' >&2; exit 1; }
git fetch origin --tags
[ -z "$(git tag --list "v$version")" ] || { echo "Tag v$version already exists." >&2; exit 1; }
git checkout develop
git pull --ff-only

git checkout -b "$branch"
"$here/bump-version.sh" "$version"
git add -A
git commit -m "Release $version"
git push -u origin "$branch"

# Into master: merging changes version.json there, which starts the Release workflow.
# --admin is the "bypass rules" choice the ruleset needs from the owner or a maintainer to merge at all.
gh pr create --base master --head "$branch" --title "Release $version" --body "Release $version"
wait_for_checks "$branch"
number="$(gh pr view "$branch" --json number --jq .number)"
gh pr merge "$branch" --merge --admin   # the branch stays until the whole flow has succeeded
sha="$(gh pr view "$number" --json mergeCommit --jq .mergeCommit.oid)"

# Wait for the release to be published (the workflow runs on the merge commit and waits for nothing else).
run=""
for _ in $(seq 36); do
    run="$(gh run list --workflow release.yml --commit "$sha" --json databaseId --jq '.[0].databaseId // empty')"
    [ -n "$run" ] && break
    sleep 5
done
[ -n "$run" ] || { echo "The Release workflow did not start for $sha. Is the version in version.json new? The release branch is merged; finish by hand (docs/releasing.md)." >&2; exit 1; }
gh run watch "$run" --exit-status
gh release view "v$version"

# Merge master back into develop, which gets the version change and the merge commit.
gh pr create --base develop --head master --title "Merge release $version into develop" --body "Merges master back into develop after release $version."
wait_for_checks master
gh pr merge master --merge --admin

git checkout develop
git pull --ff-only

# Everything worked: the release branch has done its job.
git push origin --delete "$branch"
git branch -d "$branch"
echo "Done. Release v$version is published and master is merged back into develop."
