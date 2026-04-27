# Creates a copyable .NET Framework 4.8 deployment folder.
# Note: .NET Framework projects cannot be published as truly self-contained like .NET 8.
# The target PC must have .NET Framework 4.8 installed, or run Run-ApexHMI.cmd
# with ndp48-x86-x64-allos-enu.exe placed next to it for offline installation.
# Usage (from repo root or from deploy/):
#   powershell -ExecutionPolicy Bypass -File deploy\publish-offline.ps1
#   powershell -ExecutionPolicy Bypass -File .\publish-offline.ps1  # if cwd is deploy

param(
    [string] $Configuration = "Release",
    [string] $OutputRelative = "dist\ApexHMI-net48"
)

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptDir)) { $scriptDir = (Get-Location).Path }
$root = (Resolve-Path (Join-Path $scriptDir "..")).Path
$csproj = Join-Path $root "ApexHMI.csproj"
$out = Join-Path $root $OutputRelative

if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Error "Project not found: $csproj"
    exit 1
}

Write-Host "Project: $csproj"
Write-Host "Output:  $out"
Write-Host ""

$buildArgs = @(
    "build", $csproj
    "-c", $Configuration
    "-o", $out
)

& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out "ApexHMI.exe"
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Error "Expected output missing: $exe"
    exit 1
}

Write-Host ""
Write-Host "Done. Deployment folder is ready at:"
Write-Host "  $out"
Write-Host ""
Write-Host "On the target machine:"
Write-Host "  Install .NET Framework 4.8 if missing."
Write-Host "  Copy the whole folder, then run ApexHMI.exe"
Write-Host "  (or Run-ApexHMI.cmd to check/install .NET Framework 4.8)"
Write-Host ""
$size = (Get-ChildItem -LiteralPath $out -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host ("Approx. size: {0:N1} MB" -f ($size / 1MB))
