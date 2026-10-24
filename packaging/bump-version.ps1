# Sets the version of a new release everywhere it is written, ready to be committed on a release branch:
#   version.json (app and every component), the release entry of the Flatpak metainfo, and, through a build, the project files and manifests.
# Usage: powershell -File packaging/bump-version.ps1 0.2.0     (or 0.2.0-beta.1)
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw "'$Version' is not x.y.z or x.y.z-pre.release." }
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $root
try {
    $json = Get-Content version.json -Raw
    $json = $json -replace '("app"\s*:\s*")[^"]*"', "`${1}$Version`""
    $json = $json -replace '("RelicWriter[A-Za-z0-9.]*"\s*:\s*")[^"]*"', "`${1}$Version`""
    [IO.File]::WriteAllText("$PWD/version.json", $json)

    $meta = 'packaging/flatpak/io.github.hendy643.RelicWriter.metainfo.xml'
    $date = Get-Date -Format 'yyyy-MM-dd'
    (Get-Content $meta -Raw) -replace '<release version="[^"]*" date="[^"]*" />', "<release version=`"$Version`" date=`"$date`" />" | Set-Content $meta -NoNewline

    # The build copies the numbers into the project files and manifests.
    dotnet build RelicWriter.slnx -c Release
    if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }
    git status --short
    Write-Host "Version $Version written. Commit these changes on a release branch and open a pull request to master."
}
finally { Pop-Location }
