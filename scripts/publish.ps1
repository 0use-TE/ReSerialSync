param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packageName = "ReSerialSync-$Runtime"
$packageRoot = Join-Path $repoRoot "artifacts/$packageName"
if (Test-Path -LiteralPath $packageRoot) { throw "Output exists: $packageRoot. Choose a clean artifacts directory before publishing." }
dotnet publish (Join-Path $repoRoot 'ReSerialSync/ReSerialSync.Desktop/ReSerialSync.Desktop.csproj') -c Release -r $Runtime -p:PublishAot=true -p:SelfContained=true -p:PublishSingleFile=true -p:StripSymbols=true -o $packageRoot
if ($LASTEXITCODE -ne 0) { throw 'Native AOT publish failed.' }
Get-ChildItem -LiteralPath $packageRoot -File | Where-Object { $_.Extension -in '.pdb','.dbg' } | Remove-Item
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/releases.md') -Destination (Join-Path $packageRoot 'RUNNING.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/third-party-notices.md') -Destination (Join-Path $packageRoot 'THIRD-PARTY-NOTICES.md')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/licenses') -Destination (Join-Path $packageRoot 'licenses') -Recurse
$archive = Join-Path $repoRoot "artifacts/$packageName.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
