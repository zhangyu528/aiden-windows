param(
    [switch]$PublishApp,
    [string]$AppPath = "",
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64",
    [string]$ResultsDirectory = "",
    [string]$TrxLogFileName = "",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDir = Join-Path $repoRoot "artifacts\ui-test\tray"
$projectPath = Join-Path $repoRoot "Aiden.TrayMonitor\Aiden.TrayMonitor.csproj"
$testProjectPath = Join-Path $repoRoot "tests\Aiden.UI.Tests\Aiden.UI.Tests.csproj"

if ($PublishApp) {
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    dotnet publish $projectPath `
        -c $Configuration `
        -r $Rid `
        --self-contained false `
        -o $publishDir
}

$resolvedAppPath = $AppPath
if ([string]::IsNullOrWhiteSpace($resolvedAppPath)) {
    $candidates = @(
        (Join-Path $publishDir "Aiden.TrayMonitor.exe"),
        (Join-Path $repoRoot "Aiden.TrayMonitor\bin\$Configuration\net8.0-windows\Aiden.TrayMonitor.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $resolvedAppPath = $candidate
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($resolvedAppPath)) {
    throw "Cannot resolve Aiden.TrayMonitor.exe. Run with -PublishApp or pass -AppPath."
}

if (-not (Test-Path $resolvedAppPath)) {
    throw "AIDEN_UI_APP_PATH target does not exist: $resolvedAppPath"
}

$env:AIDEN_UI_APP_PATH = (Resolve-Path $resolvedAppPath).Path
Write-Host "AIDEN_UI_APP_PATH=$env:AIDEN_UI_APP_PATH"

$testArgs = @("test", $testProjectPath, "-c", $Configuration)
if ($NoRestore) {
    $testArgs += "--no-restore"
}

if (-not [string]::IsNullOrWhiteSpace($TrxLogFileName)) {
    $testArgs += "--logger"
    $testArgs += "trx;LogFileName=$TrxLogFileName"
}

if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $testArgs += "--results-directory"
    $testArgs += $ResultsDirectory
}

dotnet @testArgs
