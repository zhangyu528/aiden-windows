param(
    [Parameter(Mandatory = $false)]
    [string]$InstallDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory = $false)]
    [switch]$Force,
    [Parameter(Mandatory = $false)]
    [switch]$AllowInsecureFallback
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$logPath = Join-Path $InstallDir 'install-runtime-deps.log'
function Write-Log {
    param([string]$Message)
    $timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    "$timestamp $Message" | Out-File -FilePath $logPath -Encoding UTF8 -Append
}

$vmScript = Join-Path $PSScriptRoot 'download-vm.ps1'
$collectorScript = Join-Path $PSScriptRoot 'download-collector.ps1'

if (-not (Test-Path $vmScript)) {
    throw "download-vm.ps1 not found: $vmScript"
}
if (-not (Test-Path $collectorScript)) {
    throw "download-collector.ps1 not found: $collectorScript"
}

$vmVersion = 'v1.113.0'
$vmUrl = 'https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.113.0/victoria-metrics-windows-amd64-v1.113.0.zip'
$collectorVersion = 'v0.146.1'
$collectorUrl = 'https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol-contrib_0.146.1_windows_amd64.tar.gz'

Write-Log "Installing runtime dependencies into $InstallDir"

$vmArgs = @{
    Version = $vmVersion
    DownloadUrl = $vmUrl
    InstallRoot = $InstallDir
}
$collectorArgs = @{
    Version = $collectorVersion
    DownloadUrl = $collectorUrl
    InstallRoot = $InstallDir
    VerifyComponents = $true
}

if ($AllowInsecureFallback.IsPresent) {
    $vmArgs.AllowInsecureFallback = $true
    $collectorArgs.AllowInsecureFallback = $true
}
if ($Force.IsPresent) {
    $vmArgs.Force = $true
    $collectorArgs.Force = $true
}

try {
    Write-Log '[VM] installation start'
    & $vmScript @vmArgs
    Write-Log '[VM] installation complete'

    Write-Log '[OTEL] installation start'
    & $collectorScript @collectorArgs
    Write-Log '[OTEL] installation complete'

    Write-Log 'Runtime components ready.'
}
catch {
    Write-Log "Runtime dependency installation failed: $($_.Exception.Message)"
    throw
}
