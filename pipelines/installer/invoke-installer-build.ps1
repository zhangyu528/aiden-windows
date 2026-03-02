param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# 1. Stage signed binaries
Write-Host "Step 1: Staging signed binaries..." -ForegroundColor Cyan
& "$PSScriptRoot/stage-signed-binaries.ps1"

# 2. Build Inno Setup installer
Write-Host "Step 2: Building Inno Setup installer..." -ForegroundColor Cyan
& "$PSScriptRoot/build-inno-setup.ps1" -Version $Version
