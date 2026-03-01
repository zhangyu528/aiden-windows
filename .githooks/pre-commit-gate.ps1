$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function StartsWithAny([string]$value, [string[]]$prefixes) {
    foreach ($prefix in $prefixes) {
        if ($value.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    return $false
}

function IsDocOnlyPath([string]$path) {
    $docPrefixes = @('docs/', '.github/ISSUE_TEMPLATE/', '.github/PULL_REQUEST_TEMPLATE')
    $docExtensions = @('.md', '.txt')

    if (StartsWithAny $path $docPrefixes) {
        return $true
    }

    foreach ($ext in $docExtensions) {
        if ($path.EndsWith($ext, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $staged = @(git diff --cached --name-only --diff-filter=ACMR | ForEach-Object { $_.Replace('\', '/') } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($staged.Count -eq 0) {
        Write-Host "pre-commit: no staged files, skipping gate."
        exit 0
    }

    $allDocOnly = $true
    foreach ($path in $staged) {
        if (-not (IsDocOnlyPath $path)) {
            $allDocOnly = $false
            break
        }
    }

    if ($allDocOnly) {
        Write-Host "pre-commit: docs-only change detected, skipping test gate."
        exit 0
    }

    $runtimePrefixes = @('Aiden.RuntimeAgent/', 'tests/Aiden.RuntimeAgent.UnitTests/')
    $trayPrefixes = @('Aiden.TrayMonitor/', 'tests/Aiden.TrayMonitor.UnitTests/')
    $scriptPrefixes = @('.github/scripts/', 'Aiden.RuntimeAgent/scripts/', 'tests/Aiden.Scripts.Tests/', '.github/workflows/', '.githooks/', 'scripts/setup-githooks.ps1', 'scripts/ensure-pester-v5.ps1', 'scripts/run-script-tests.ps1')

    $needsRuntime = $false
    $needsTray = $false
    $needsScripts = $false
    $needsRestore = $false

    foreach ($path in $staged) {
        if (StartsWithAny $path $runtimePrefixes) { $needsRuntime = $true; $needsRestore = $true }
        if (StartsWithAny $path $trayPrefixes) { $needsTray = $true; $needsRestore = $true }
        if (StartsWithAny $path $scriptPrefixes) { $needsScripts = $true }
        if ($path -eq 'Aiden.sln' -or $path -eq 'Directory.Build.props' -or $path.EndsWith('.csproj', [System.StringComparison]::OrdinalIgnoreCase)) {
            $needsRuntime = $true
            $needsTray = $true
            $needsRestore = $true
        }
    }

    if ($needsRestore) {
        Write-Host "pre-commit: restoring solution..."
        dotnet restore Aiden.sln --verbosity quiet
    }

    if ($needsRuntime) {
        Write-Host "pre-commit: runtime build + unit tests..."
        dotnet build Aiden.RuntimeAgent/Aiden.RuntimeAgent.csproj -c Debug --no-restore -nologo
        dotnet test tests/Aiden.RuntimeAgent.UnitTests/Aiden.RuntimeAgent.UnitTests.csproj -c Debug --no-build --no-restore -nologo
    }

    if ($needsTray) {
        Write-Host "pre-commit: tray build + unit tests..."
        dotnet build Aiden.TrayMonitor/Aiden.TrayMonitor.csproj -c Debug --no-restore -nologo
        dotnet test tests/Aiden.TrayMonitor.UnitTests/Aiden.TrayMonitor.UnitTests.csproj -c Debug --no-build --no-restore -nologo
    }

    if ($needsScripts) {
        Write-Host "pre-commit: pester v5 scripts tests..."
        & (Join-Path $repoRoot 'scripts\run-script-tests.ps1')
    }

    Write-Host "pre-commit: gate passed."
}
finally {
    Pop-Location
}
