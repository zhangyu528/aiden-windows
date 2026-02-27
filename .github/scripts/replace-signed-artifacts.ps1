param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$signedTray = "artifacts/signed/apps/Aiden.TrayMonitor.exe"
$signedAgent = "artifacts/signed/apps/Aiden.RuntimeAgent.exe"
$signedInstaller = Get-ChildItem -Path "artifacts/signed/installer" -Filter "Aiden-Setup-*.exe" -Recurse -ErrorAction Stop | Select-Object -First 1

if (-not (Test-Path $signedTray)) {
    throw "Signed tray executable not found: $signedTray"
}
if (-not (Test-Path $signedAgent)) {
    throw "Signed agent executable not found: $signedAgent"
}
if (-not $signedInstaller) {
    throw "Signed installer not found in artifacts/signed/installer"
}

Copy-Item -Path $signedTray -Destination "artifacts/stage/tray/Aiden.TrayMonitor.exe" -Force
Copy-Item -Path $signedAgent -Destination "artifacts/stage/agent/Aiden.RuntimeAgent.exe" -Force
Copy-Item -Path $signedInstaller.FullName -Destination "artifacts/installer/Aiden-Setup-$Version-win-x64.exe" -Force
