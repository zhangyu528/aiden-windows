param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$DownloadUrl,

    [Parameter(Mandatory = $true)]
    [string]$Sha256
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Version) -or [string]::IsNullOrWhiteSpace($DownloadUrl) -or [string]::IsNullOrWhiteSpace($Sha256)) {
    throw "Version, DownloadUrl, Sha256 must all be provided."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$installRoot = Join-Path $repoRoot "Aiden.TrayMonitor\runtime\vm"
$targetDir = Join-Path $installRoot $Version
$exePath = Join-Path $targetDir "victoria-metrics.exe"

if (Test-Path $exePath) {
    Write-Host "victoria-metrics already exists: $exePath"
    exit 0
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$tempZip = Join-Path $env:TEMP ("victoria-metrics-" + $Version + ".zip")
if (Test-Path $tempZip) {
    Remove-Item -Force $tempZip
}

Write-Host "Downloading: $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempZip

$actualHash = (Get-FileHash -Algorithm SHA256 -Path $tempZip).Hash.ToLowerInvariant()
$expectedHash = $Sha256.ToLowerInvariant()
if ($actualHash -ne $expectedHash) {
    throw "SHA256 mismatch. expected=$expectedHash actual=$actualHash"
}

$extractDir = Join-Path $env:TEMP ("victoria-metrics-extract-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

try {
    Expand-Archive -Path $tempZip -DestinationPath $extractDir -Force
    $foundExe = Get-ChildItem -Recurse -Path $extractDir -Filter "victoria-metrics*.exe" | Select-Object -First 1
    if (-not $foundExe) {
        throw "victoria-metrics executable was not found in archive."
    }

    Copy-Item -Path $foundExe.FullName -Destination $exePath -Force
    Write-Host "Installed: $exePath"
}
finally {
    if (Test-Path $extractDir) {
        Remove-Item -Recurse -Force $extractDir
    }
    if (Test-Path $tempZip) {
        Remove-Item -Force $tempZip
    }
}
