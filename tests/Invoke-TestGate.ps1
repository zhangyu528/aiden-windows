param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Staged', 'PR', 'Nightly')]
    [string]$Scope,

    [switch]$InstallPester,

    [string[]]$StagedFiles = @(),

    [string]$Configuration = 'Debug',
    
    [switch]$NoRestore,

    [string]$AppPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot

try {
    $resultsBaseDir = Join-Path $repoRoot "artifacts\test-results"
    
    # Define test types based on Scope
    $runUnit = $true
    $runPipeline = ($Scope -ne 'PR')
    $runIntegration = ($Scope -eq 'PR' -or $Scope -eq 'Nightly')
    $runUI = ($Scope -eq 'Nightly')

    if ($Scope -eq 'Staged' -and $StagedFiles.Count -gt 0) {
        $hasAidenChanges = $StagedFiles -match 'Aiden\.RuntimeAgent|Aiden\.TrayMonitor|tests/'
        $hasPipelineChanges = $StagedFiles -match 'pipelines/|tests/Pipelines/|tests/Invoke-TestGate\.ps1'
        
        # If we have specific files, we only run what's relevant. 
        if ($hasAidenChanges -or $hasPipelineChanges) {
            $runUnit = [bool]$hasAidenChanges
            $runPipeline = [bool]$hasPipelineChanges
        }
    }

    if ($Scope -eq 'PR' -or $Scope -eq 'Nightly') {
        $Configuration = 'Release'
    }

    Write-Host "--- Aiden Unified Test Gate ---" -ForegroundColor Cyan
    Write-Host "Scope: $Scope"
    Write-Host "Configuration: $Configuration"
    Write-Host "Tests: Unit=$runUnit, Pipeline=$runPipeline, Integration=$runIntegration, UI=$runUI"
    Write-Host "---------------------------------"

    $scriptDir = $PSScriptRoot

    # 1. Unit Tests
    if ($runUnit) {
        $dotnetResultsDir = Join-Path $resultsBaseDir "dotnet"
        Write-Host "Running Aiden Unit Tests..." -ForegroundColor Yellow
        & (Join-Path $scriptDir "drivers/run-unit-tests.ps1") `
            -Configuration $Configuration -NoRestore:$NoRestore -ResultsDirectory $dotnetResultsDir
    }

    # 2. Integration Tests
    if ($runIntegration) {
        $dotnetResultsDir = Join-Path $resultsBaseDir "dotnet"
        Write-Host "Running Aiden Integration Tests..." -ForegroundColor Yellow
        & (Join-Path $scriptDir "drivers/run-integration-tests.ps1") `
            -Configuration $Configuration -NoRestore:$NoRestore -ResultsDirectory $dotnetResultsDir
    }

    # 3. UI Tests
    if ($runUI) {
        $uiResultsDir = Join-Path $resultsBaseDir "ui"
        Write-Host "Running Aiden UI Tests..." -ForegroundColor Yellow
        & (Join-Path $scriptDir "drivers/run-ui-tests.ps1") `
            -Configuration $Configuration -NoRestore:$NoRestore -AppPath $AppPath -ResultsDirectory $uiResultsDir
    }

    # 4. Pipeline Script Tests (Pester)
    if ($runPipeline) {
        Write-Host "Running Pester Tests for Automation Scripts..." -ForegroundColor Yellow
        & (Join-Path $scriptDir "ensure-pester-v5.ps1") -Install:$InstallPester

        $pesterResultsDir = Join-Path $resultsBaseDir "scripts"
        if (-not (Test-Path $pesterResultsDir)) { New-Item -ItemType Directory -Path $pesterResultsDir -Force | Out-Null }
        $resultsFile = Join-Path $pesterResultsDir 'pester-pipeline.xml'

        # Collect all Pester tests in the relevant subfolders
        $runPaths = @("drivers", "Pipelines") | ForEach-Object { Join-Path $scriptDir $_ }

        $config = New-PesterConfiguration
        $config.Run.Path = $runPaths
        $config.Run.PassThru = $true
        $config.Output.CIFormat = 'Auto'
        $config.TestResult.Enabled = $true
        $config.TestResult.OutputPath = $resultsFile
        $config.TestResult.OutputFormat = 'NUnitXml'

        $result = Invoke-Pester -Configuration $config
        if ($result.FailedCount -gt 0 -or $result.FailedBlocksCount -gt 0) {
            throw "Pester script tests failed."
        }
    }

    Write-Host "All requested tests completed successfully." -ForegroundColor Green
}
catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
