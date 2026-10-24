# Makes a release the git flow way (see docs/releasing.md): a release/<version> branch from develop with the version written, a pull request into master
# (merging it starts the Release workflow), then a pull request back into develop. Needs the gh CLI, logged in as the owner or a maintainer.
# Usage: powershell -File packaging/release.ps1 1.0.0     (or 1.0.0-beta.1)
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "'$Version' is not x.y.z or x.y.z-pre.release." }
$branch = "release/$Version"

function Step([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Failed: $Command" }
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
    Step { gh pr checks $branch --watch }
    Step { gh pr merge $branch --merge --admin }

    # Back into develop. Its pull request needs the checks too, and they start a moment after it is opened.
    Step { gh pr create --base develop --head $branch --title "Merge release $Version into develop" --body "Back-merge of the version change." }
    Start-Sleep -Seconds 20
    Step { gh pr checks $branch --watch }
    Step { gh pr merge $branch --merge --admin --delete-branch }

    Step { git checkout develop }
    Step { git pull --ff-only }
    Write-Host "Done. The Release workflow is running: gh run list --workflow release.yml, then gh release view v$Version when it has finished."
}
finally { Pop-Location }
