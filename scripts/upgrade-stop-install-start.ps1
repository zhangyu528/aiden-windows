param(
  [Parameter(Mandatory = $true)]
  [string]$NewPackagePath,
  [Parameter(Mandatory = $true)]
  [string]$InstallPath
)

$ErrorActionPreference = "Stop"

function Stop-IfRunning {
  param([string]$Name)
  Get-Process -Name $Name -ErrorAction SilentlyContinue | ForEach-Object {
    try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {}
  }
}

Write-Host "Stopping running processes..."
Stop-IfRunning -Name "Aiden.RuntimeAgent"
Stop-IfRunning -Name "Aiden.TrayMonitor"
Stop-IfRunning -Name "victoria-metrics"
Stop-IfRunning -Name "otelcol"
Stop-IfRunning -Name "otelcol-contrib"

if (Test-Path $InstallPath) {
  Write-Host "Cleaning old install path: $InstallPath"
  Remove-Item -Path $InstallPath -Recurse -Force
}

Write-Host "Installing new package from $NewPackagePath"
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
Copy-Item -Path (Join-Path $NewPackagePath "*") -Destination $InstallPath -Recurse -Force

$agentExe = Join-Path $InstallPath "Aiden.RuntimeAgent.exe"
if (Test-Path $agentExe) {
  Write-Host "Updating HKCU Run key for RuntimeAgent"
  New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Force | Out-Null
  Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "AidenRuntimeAgent" -Value "`"$agentExe`""

  Write-Host "Starting RuntimeAgent"
  Start-Process -FilePath $agentExe -WorkingDirectory $InstallPath
}

$trayExe = Join-Path $InstallPath "Aiden.TrayMonitor.exe"
if (Test-Path $trayExe) {
  Write-Host "Starting Tray UI"
  Start-Process -FilePath $trayExe -WorkingDirectory $InstallPath
}

Write-Host "Upgrade completed."
