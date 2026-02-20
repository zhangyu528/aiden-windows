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
$installRoot = Join-Path $repoRoot "Aiden.TrayMonitor\runtime\collector"
$targetDir = Join-Path $installRoot $Version

if (Test-Path $targetDir) {
    $existing = Get-ChildItem -Recurse -Path $targetDir -Filter "otelcol*.exe" | Select-Object -First 1
    if ($existing) {
        Write-Host "Collector already exists: $($existing.FullName)"
        exit 0
    }
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$archiveName = Split-Path -Leaf $DownloadUrl
$tempArchive = Join-Path $env:TEMP ("otelcol-" + $Version + "-" + $archiveName)
if (Test-Path $tempArchive) {
    Remove-Item -Force $tempArchive
}

Write-Host "Downloading: $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempArchive

$actualHash = (Get-FileHash -Algorithm SHA256 -Path $tempArchive).Hash.ToLowerInvariant()
$expectedHash = $Sha256.ToLowerInvariant()
if ($actualHash -ne $expectedHash) {
    throw "SHA256 mismatch. expected=$expectedHash actual=$actualHash"
}

$extractDir = Join-Path $env:TEMP ("otelcol-extract-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $extractDir | Out-Null

try {
    if ($tempArchive.ToLowerInvariant().EndsWith(".zip")) {
        Expand-Archive -Path $tempArchive -DestinationPath $extractDir -Force
    } elseif ($tempArchive.ToLowerInvariant().EndsWith(".tar.gz")) {
        $localArchive = Join-Path $extractDir "collector.tar.gz"
        Copy-Item -Path $tempArchive -Destination $localArchive -Force
        Push-Location $extractDir
        try {
            tar -xzf ".\\collector.tar.gz"
        }
        finally {
            Pop-Location
        }
    } else {
        throw "Unsupported archive format: $tempArchive"
    }

    Copy-Item -Path (Join-Path $extractDir "*") -Destination $targetDir -Recurse -Force

    $found = Get-ChildItem -Recurse -Path $targetDir -Filter "otelcol*.exe" | Select-Object -First 1
    if (-not $found) {
        throw "No otelcol executable found after extraction."
    }

    Write-Host "Installed: $($found.FullName)"
}
finally {
    if (Test-Path $extractDir) {
        Remove-Item -Recurse -Force $extractDir
    }
    if (Test-Path $tempArchive) {
        Remove-Item -Force $tempArchive
    }
}
