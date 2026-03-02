param(
    [Parameter(Mandatory = $true)]
    [string]$Tag,
    [Parameter(Mandatory = $true)]
    [string]$ReleaseUrl
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Write-Host "--- Pre-release Summary ---" -ForegroundColor Cyan
Write-Host "Computed tag: $Tag"
Write-Host "Pre-release URL: $ReleaseUrl"
Write-Host "---------------------------"
