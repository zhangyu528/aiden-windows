param(
    [string]$Configuration = 'Release',
    [switch]$NoRestore,
    [string]$AppPath,
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# Resolve AppPath if not provided
if (-not $AppPath) {
    $publishDir = Join-Path $repoRoot "artifacts\ui-test\tray"
    $candidates = @(
        (Join-Path $publishDir "Aiden.TrayMonitor.exe"),
        (Join-Path $repoRoot "Aiden.TrayMonitor\bin\$Configuration\net8.0-windows\Aiden.TrayMonitor.exe")
    )
    
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { $AppPath = $candidate; break }
    }

    # Build if still not found
    if (-not $AppPath) {
        Write-Host "Building tray app for UI smoke..." -ForegroundColor Yellow
        dotnet publish (Join-Path $repoRoot "Aiden.TrayMonitor/Aiden.TrayMonitor.csproj") `
            -c $Configuration -r win-x64 --self-contained false -o $publishDir --nologo
        $AppPath = Join-Path $publishDir "Aiden.TrayMonitor.exe"
    }
}

if (-not $AppPath -or -not (Test-Path $AppPath)) {
    throw "UI tests requested but Aiden.TrayMonitor.exe not found. Pass -AppPath."
}

$env:AIDEN_UI_APP_PATH = (Resolve-Path $AppPath).Path
Write-Host "Running UI tests (DotNet) with app: $env:AIDEN_UI_APP_PATH" -ForegroundColor Yellow

$testProjectPath = Join-Path $repoRoot "tests/Aiden.UI.Tests/Aiden.UI.Tests.csproj"
$restoreFlag = if ($NoRestore) { "--no-restore" } else { "" }

dotnet test $testProjectPath -c $Configuration $restoreFlag `
    --logger "trx;LogFileName=ui-smoke.trx" --results-directory $ResultsDirectory --nologo
