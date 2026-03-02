$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $staged = @(git diff --cached --name-only --diff-filter=ACMR | ForEach-Object { $_.Replace('\', '/') } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($staged.Count -eq 0) {
        Write-Host "pre-commit: no staged files."
        exit 0
    }

    $testRunner = Join-Path $repoRoot 'automation\tests\Invoke-TestGate.ps1'
    & $testRunner -Scope Staged -StagedFiles $staged

    Write-Host "pre-commit: gate passed."
}
finally {
    Pop-Location
}
