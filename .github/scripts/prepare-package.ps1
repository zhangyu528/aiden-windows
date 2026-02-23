param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$stageRoot = Join-Path $artifactsRoot 'stage'
$trayPublish = Join-Path $stageRoot 'tray'
$agentPublish = Join-Path $stageRoot 'agent'
$packageDir = Join-Path $stageRoot 'package'
$installerDir = Join-Path $artifactsRoot 'installer'

foreach ($dir in @($packageDir, $installerDir)) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force -ErrorAction Stop $dir
    }
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

foreach ($path in @($trayPublish, $agentPublish)) {
    if (-not (Test-Path $path)) {
        throw "Publish directory not found: $path"
    }
}

Write-Host "Staging install payload for version $Version"
Copy-Item -Path (Join-Path $trayPublish '*') -Destination $packageDir -Recurse -Force
Copy-Item -Path (Join-Path $agentPublish '*') -Destination $packageDir -Recurse -Force
Write-Host "Copying installer icon"
$iconSource = Join-Path $repoRoot 'Aiden.TrayMonitor\Assets\aiden.ico'
if (Test-Path $iconSource) {
    Copy-Item -Path $iconSource -Destination $packageDir -Force
} else {
    Write-Warning "Icon not found: $iconSource"
}
Write-Host "Copying installer runtime helper"
$helperScriptSource = Join-Path $repoRoot '.github\installer\install-runtime-deps.ps1'
if (-not (Test-Path $helperScriptSource)) {
    throw "Helper script not found: $helperScriptSource"
}
$helperDestDir = Join-Path $packageDir 'scripts'
if (Test-Path $helperDestDir) {
    Remove-Item -Recurse -Force -ErrorAction Stop $helperDestDir
}
New-Item -ItemType Directory -Path $helperDestDir -Force | Out-Null
Copy-Item -Path $helperScriptSource -Destination $helperDestDir -Force
$packagedHelper = Join-Path $helperDestDir 'install-runtime-deps.ps1'
if (-not (Test-Path $packagedHelper)) {
    throw "Packaged helper script not found after copy: $packagedHelper"
}

Write-Host "Payload staged in $packageDir"
$info = Get-ChildItem -Path $packageDir -Recurse -File | Measure-Object -Property Length -Sum
Write-Host "  files: $($info.Count), size: $([math]::Round($info.Sum / 1MB, 2)) MB"
