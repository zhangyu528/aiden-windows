$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$signedTray = "artifacts/signed/apps/Aiden.TrayMonitor.exe"
$signedAgent = "artifacts/signed/apps/Aiden.RuntimeAgent.exe"

$stageTray = "artifacts/stage/tray/Aiden.TrayMonitor.exe"
$stageAgent = "artifacts/stage/agent/Aiden.RuntimeAgent.exe"

if (-not (Test-Path $signedTray)) { throw "Signed Tray executable not found: $signedTray" }
if (-not (Test-Path $signedAgent)) { throw "Signed Agent executable not found: $signedAgent" }

Write-Host "Staging signed binaries..."
Copy-Item -Path $signedTray -Destination $stageTray -Force
Copy-Item -Path $signedAgent -Destination $stageAgent -Force
