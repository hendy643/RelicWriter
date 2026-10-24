# Makes a release the git flow way (see docs/releasing.md): a release/<version> branch from develop with the version written, a pull request into master
# (merging it starts the Release workflow), then, once the release is published, a pull request that merges master back into develop.
# Needs the gh CLI, logged in as the owner or a maintainer.
# Usage: powershell -File packaging/release.ps1 1.0.0     (or 1.0.0-beta.1)
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "'$Version' is not x.y.z or x.y.z-pre.release." }
$branch = "release/$Version"

function Step([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Failed: $Command" }
}

# The checks start a few seconds after a pull request is opened; wait for them to be reported, then for them to finish.
function WaitForChecks([string]$head) {
    for ($i = 0; $i -lt 36; $i++) {
        if ("$(gh pr checks $head 2>&1)" -notmatch 'no checks reported') { break }
        Start-Sleep -Seconds 5
    }
    Step { gh pr checks $head --watch }
}

Push-Location (Resolve-Path (Join-Path $PSScriptRoot '..'))
try {
    if (git status --porcelain) { throw 'The working tree has uncommitted changes.' }
    Step { git fetch origin --tags }
    if (git tag --list "v$Version") { throw "Tag v$Version already exists." }
    Step { git checkout develop }
    Step { git pull --ff-only }

    Step { git checkout -b $branch }
    Step { & "$PSScriptRoot/bump-version.ps1" $Version }
    Step { git add -A }
    Step { git commit -m "Release $Version" }
    Step { git push -u origin $branch }

    # Into master: merging changes version.json there, which starts the Release workflow.
    # --admin is the "bypass rules" choice the ruleset needs from the owner or a maintainer to merge at all.
    Step { gh pr create --base master --head $branch --title "Release $Version" --body "Release $Version" }
    WaitForChecks $branch
    $merge = gh pr view $branch --json number,mergeCommit --jq .number
    Step { gh pr merge $branch --merge --admin }   # the branch stays until the whole flow has succeeded
    $sha = gh pr view $merge --json mergeCommit --jq .mergeCommit.oid

    # Wait for the release to be published (the workflow runs on the merge commit and waits for nothing else).
    $run = $null
    for ($i = 0; $i -lt 36 -and -not $run; $i++) {
        $run = gh run list --workflow release.yml --commit $sha --json databaseId --jq '.[0].databaseId'
        if (-not $run) { Start-Sleep -Seconds 5 }
    }
    if (-not $run) { throw "The Release workflow did not start for $sha. Is the version in version.json new? The release branch is merged; finish by hand (docs/releasing.md)." }
    Step { gh run watch $run --exit-status }
    Step { gh release view "v$Version" }

    # Merge master back into develop, which gets the version change and the merge commit.
    Step { gh pr create --base develop --head master --title "Merge release $Version into develop" --body "Merges master back into develop after release $Version." }
    WaitForChecks master
    Step { gh pr merge master --merge --admin }

    Step { git checkout develop }
    Step { git pull --ff-only }

    # Everything worked: the release branch has done its job.
    Step { git push origin --delete $branch }
    Step { git branch -d $branch }
    Write-Host "Done. Release v$Version is published and master is merged back into develop."
}
finally { Pop-Location }
