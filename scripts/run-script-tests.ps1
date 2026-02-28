param(
    [switch]$InstallPester
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    & (Join-Path $repoRoot 'scripts\ensure-pester-v5.ps1') -Install:$InstallPester

    $resultsDir = Join-Path $repoRoot 'artifacts\test-results\scripts'
    $resultsFile = Join-Path $resultsDir 'pester-scripts.xml'
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

    $config = New-PesterConfiguration
    $config.Run.Path = 'tests/Aiden.Scripts.Tests'
    $config.Run.PassThru = $true
    $config.Output.CIFormat = 'Auto'
    $config.TestResult.Enabled = $true
    $config.TestResult.OutputPath = $resultsFile
    $config.TestResult.OutputFormat = 'NUnitXml'

    $result = Invoke-Pester -Configuration $config
    if ($result.FailedCount -gt 0 -or $result.FailedBlocksCount -gt 0) {
        throw "Script tests failed. FailedCount=$($result.FailedCount), FailedBlocksCount=$($result.FailedBlocksCount)"
    }
}
finally {
    Pop-Location
}
