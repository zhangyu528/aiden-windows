param(
  [Parameter(Mandatory = $false)]
  [string]$InstallPath = ""
)

$ErrorActionPreference = "Stop"

function Stop-IfRunning {
  param([string]$Name)
  Get-Process -Name $Name -ErrorAction SilentlyContinue | ForEach-Object {
    try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch {}
  }
}

Write-Host "Stopping Aiden runtime processes..."
Stop-IfRunning -Name "Aiden.RuntimeAgent"
Stop-IfRunning -Name "Aiden.TrayMonitor"
Stop-IfRunning -Name "victoria-metrics"
Stop-IfRunning -Name "otelcol"
Stop-IfRunning -Name "otelcol-contrib"

Write-Host "Removing HKCU Run key..."
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "AidenRuntimeAgent" -ErrorAction SilentlyContinue

if (-not [string]::IsNullOrWhiteSpace($InstallPath) -and (Test-Path $InstallPath)) {
  Write-Host "Removing install path: $InstallPath"
  Remove-Item -Path $InstallPath -Recurse -Force
}

Write-Host "Uninstall cleanup completed."
