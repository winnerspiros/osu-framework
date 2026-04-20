<#
.SYNOPSIS
    One-click NuGet pack script for osu-framework.
.DESCRIPTION
    Packs all osu-framework NuGet packages into the ./artifacts/ directory.
.PARAMETER Version
    Package version (default: 0.0.0-local).
.PARAMETER Publish
    If set, publishes packages to a NuGet source (requires NUGET_API_KEY env var).
.EXAMPLE
    .\pack.ps1
    .\pack.ps1 -Version 2026.420.1
    .\pack.ps1 -Version 2026.420.1 -Publish
#>
[CmdletBinding()]
param(
    [string]$Version = "0.0.0-local",
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

$ArtifactsDir = Join-Path $PSScriptRoot "artifacts"
$CommonArgs = @("-c", "Release", "/p:Version=$Version", "/p:GenerateDocumentationFile=true")

Write-Host "============================================="
Write-Host " osu-framework NuGet Pack"
Write-Host " Version : $Version"
Write-Host " Output  : $ArtifactsDir"
Write-Host "============================================="

# Clean artifacts
if (Test-Path $ArtifactsDir) { Remove-Item $ArtifactsDir -Recurse -Force }
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

Write-Host ""
Write-Host ">>> Packing osu.Framework (Desktop)..."
dotnet pack @CommonArgs /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg `
    osu.Framework -o $ArtifactsDir
if ($LASTEXITCODE -ne 0) { throw "Failed to pack osu.Framework" }

Write-Host ""
Write-Host ">>> Packing osu.Framework.Android..."
$workloads = dotnet workload list 2>$null
if ($workloads -match "android") {
    dotnet pack @CommonArgs `
        osu.Framework.Android -o $ArtifactsDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to pack osu.Framework.Android" }
} else {
    Write-Host "    [SKIP] Android workload not installed. Run 'dotnet workload install android' first."
}

Write-Host ""
Write-Host ">>> Packing osu.Framework.iOS..."
$workloads = dotnet workload list 2>$null
if ($workloads -match "ios") {
    dotnet pack @CommonArgs `
        osu.Framework.iOS -o $ArtifactsDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to pack osu.Framework.iOS" }
} else {
    Write-Host "    [SKIP] iOS workload not installed. Run 'dotnet workload install ios' first."
}

Write-Host ""
Write-Host "============================================="
Write-Host " Packages created in ${ArtifactsDir}:"
Get-ChildItem "$ArtifactsDir\*.nupkg" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
Get-ChildItem "$ArtifactsDir\*.snupkg" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $_" }
Write-Host "============================================="

if ($Publish) {
    Write-Host ""
    Write-Host ">>> Publishing packages..."

    $apiKey = $env:NUGET_API_KEY
    if (-not $apiKey) {
        throw "NUGET_API_KEY environment variable is not set. Set it to your GitHub PAT or NuGet API key."
    }

    $source = $env:NUGET_SOURCE
    if (-not $source) {
        throw "NUGET_SOURCE environment variable is not set. Example: https://nuget.pkg.github.com/<owner>/index.json"
    }

    Get-ChildItem "$ArtifactsDir\*.nupkg" | ForEach-Object {
        Write-Host "    Publishing $($_.Name)..."
        dotnet nuget push $_.FullName `
            --api-key $apiKey `
            --source $source `
            --skip-duplicate
        if ($LASTEXITCODE -ne 0) { throw "Failed to publish $($_.Name)" }
    }

    Write-Host ""
    Write-Host ">>> All packages published to $source"
}
