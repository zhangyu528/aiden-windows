param(
    [Parameter(Mandatory = $true)]
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $ReportPath)) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "## Script Tests`nNo Pester report file found at \`$ReportPath\`."
    exit 0
}

[xml]$report = Get-Content $ReportPath -Raw
$root = $report.'test-results'
$total = [int]$root.total
$passed = $total - [int]$root.failures - [int]$root.errors - [int]$root.skipped - [int]$root.'not-run' - [int]$root.inconclusive
$failed = [int]$root.failures + [int]$root.errors
$skipped = [int]$root.skipped + [int]$root.'not-run' + [int]$root.inconclusive

Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "## Script Tests"
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value ""
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "| Total | Passed | Failed | Skipped |"
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "|---:|---:|---:|---:|"
Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "| $total | $passed | $failed | $skipped |"
