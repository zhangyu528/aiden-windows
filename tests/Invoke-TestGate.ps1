param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Staged', 'PR', 'Nightly')]
    [string]$Scope,

    [string[]]$StagedFiles = @(),
    
    [string]$ChangedFilesText = '',

    [string]$Configuration = 'Debug',
    
    [switch]$NoRestore,

    [string]$AppPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot

try {
    $resultsBaseDir = Join-Path $repoRoot "artifacts\test-results"

    function Test-AnyPathMatch {
        param(
            [Parameter(Mandatory = $true)]
            [string[]]$Paths,
            [Parameter(Mandatory = $true)]
            [string[]]$Patterns
        )

        foreach ($path in $Paths) {
            $normalized = ($path -replace '\\', '/').Trim()
            if ([string]::IsNullOrWhiteSpace($normalized)) {
                continue
            }
            foreach ($pattern in $Patterns) {
                if ($normalized -match $pattern) {
                    return $true
                }
            }
        }
        return $false
    }

    $changedFiles = @()
    if ($Scope -eq 'Staged') {
        $changedFiles = @($StagedFiles)
    }
    elseif (($Scope -eq 'PR' -or $Scope -eq 'Nightly') -and -not [string]::IsNullOrWhiteSpace($ChangedFilesText)) {
        $changedFiles = @($ChangedFilesText -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    # Default behavior: Nightly runs full matrix; PR/Staged are change-aware when file list is available.
    $runUnit = $true
    $runIntegration = ($Scope -eq 'PR' -or $Scope -eq 'Nightly')
    $runUI = ($Scope -eq 'Nightly')

    if (($Scope -eq 'PR' -or $Scope -eq 'Staged') -and $changedFiles.Count -gt 0) {
        $unitPatterns = @(
            '^Aiden\.RuntimeAgent/',
            '^Aiden\.TrayMonitor/',
            '^tests/Aiden\..*UnitTests/',
            '^tests/Common/'
        )
        $integrationPatterns = @(
            '^Aiden\.RuntimeAgent/',
            '^Aiden\.TrayMonitor/',
            '^tests/Aiden\.IntegrationTests/',
            '^tests/Common/'
        )
        $uiPatterns = @(
            '^Aiden\.TrayMonitor/',
            '^tests/Aiden\.UI\.Tests/',
            '^tests/Common/'
        )

        $runUnit = Test-AnyPathMatch -Paths $changedFiles -Patterns $unitPatterns
        $runIntegration = Test-AnyPathMatch -Paths $changedFiles -Patterns $integrationPatterns
        $runUI = if ($Scope -eq 'Nightly') { $true } else { Test-AnyPathMatch -Paths $changedFiles -Patterns $uiPatterns }
    }

    if ($Scope -eq 'PR' -or $Scope -eq 'Nightly') {
        $Configuration = 'Release'
    }

    Write-Host "--- Aiden Unified Test Gate ---" -ForegroundColor Cyan
    Write-Host "Scope: $Scope"
    Write-Host "Configuration: $Configuration"
    if ($changedFiles.Count -gt 0) {
        Write-Host "ChangedFiles count: $($changedFiles.Count)"
    }
    Write-Host "Tests: Unit=$runUnit, Integration=$runIntegration, UI=$runUI"
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
    
    if (-not $runUnit -and -not $runIntegration -and -not $runUI) {
        Write-Host "No relevant code changes detected for test scopes; all tests skipped." -ForegroundColor DarkYellow
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
