param(
    [Parameter(Mandatory = $false)]
    [string]$InstallDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$vmSpec = @{
    Name = 'VictoriaMetrics'
    Version = 'v1.113.0'
    DownloadUrl = 'https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/v1.113.0/victoria-metrics-windows-amd64-v1.113.0.zip'
    Sha256 = 'ed8f660442a45b260a2c0a0976440ecec863bb75ccb7cec6aad9580364a92de6'
    Destination = 'Aiden.RuntimeAgent\runtime\vm'
    ExecutablePattern = 'victoria-metrics*.exe'
}

$collectorSpec = @{
    Name = 'OpenTelemetry Collector'
    Version = 'v0.146.1'
    DownloadUrl = 'https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol-contrib_0.146.1_windows_amd64.tar.gz'
    Sha256 = '0eaa1ff9d0f5d8009921667368981617641cebb1766fc7b38be95d5dc21a126a'
    Destination = 'Aiden.RuntimeAgent\runtime\collector'
    ExecutablePattern = 'otelcol-contrib.exe'
    ArchiveType = 'tar.gz'
}

function New-DownloadTempFile([string]$prefix) {
    $candidate = Join-Path $env:TEMP ("$prefix-" + [guid]::NewGuid().ToString('N'))
    if (Test-Path $candidate) {
        Remove-Item -Force $candidate
    }
    return $candidate
}

function Invoke-Download([string]$url, [string]$destination) {
    if (Test-Path $destination) {
        Remove-Item -Force $destination
    }
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
}

function Verify-Sha256([string]$file, [string]$expected) {
    if ([string]::IsNullOrWhiteSpace($expected)) {
        Write-Warning "Skipping SHA256 verification for $file"
        return
    }
    $actualHash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash.ToLowerInvariant()
    if ($actualHash -ne $expected.ToLowerInvariant()) {
        throw "SHA256 mismatch for $file. expected=$expected actual=$actualHash"
    }
}

function Ensure-Directory([string]$path) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path -Force | Out-Null
    }
}

function Install-Vm {
    $targetDir = Join-Path $InstallDir ($vmSpec.Destination + '\' + $vmSpec.Version)
    $exePath = Join-Path $targetDir 'victoria-metrics.exe'
    if ((Test-Path $exePath) -and (-not $Force.IsPresent)) {
        Write-Host "VictoriaMetrics already installed at $exePath"
        return
    }

    Ensure-Directory $targetDir
    $archive = New-DownloadTempFile "victoria-metrics-$($vmSpec.Version).zip"
    $extractDir = Join-Path $env:TEMP ("victoria-metrics-unpack-" + [guid]::NewGuid().ToString('N'))
    try {
        Invoke-Download -url $vmSpec.DownloadUrl -destination $archive
        Verify-Sha256 -file $archive -expected $vmSpec.Sha256
        Expand-Archive -Path $archive -DestinationPath $extractDir -Force
        $found = Get-ChildItem -Recurse -Path $extractDir -Filter $vmSpec.ExecutablePattern | Select-Object -First 1
        if (-not $found) {
            throw "VictoriaMetrics executable not found inside the archive."
        }
        Copy-Item -Path $found.FullName -Destination $exePath -Force
        Write-Host "VictoriaMetrics installed to $exePath"
    }
    finally {
        if (Test-Path $extractDir) {
            Remove-Item -Recurse -Force $extractDir
        }
        if (Test-Path $archive) {
            Remove-Item -Force $archive
        }
    }
}

function Install-Collector {
    $targetDir = Join-Path $InstallDir ($collectorSpec.Destination + '\' + $collectorSpec.Version)
    $exePath = Join-Path $targetDir $collectorSpec.ExecutablePattern
    if ((Test-Path $exePath) -and (-not $Force.IsPresent)) {
        Write-Host "OTel Collector already installed at $exePath"
        return
    }

    Ensure-Directory $targetDir
    $archive = New-DownloadTempFile "otelcol-$($collectorSpec.Version)"
    $extractDir = Join-Path $env:TEMP ("otelcol-unpack-" + [guid]::NewGuid().ToString('N'))
    try {
        Invoke-Download -url $collectorSpec.DownloadUrl -destination $archive
        Verify-Sha256 -file $archive -expected $collectorSpec.Sha256
        Ensure-Directory $extractDir
        if ($collectorSpec.ArchiveType -eq 'zip') {
            Expand-Archive -Path $archive -DestinationPath $extractDir -Force
        }
        elseif ($collectorSpec.ArchiveType -eq 'tar.gz') {
            $localTar = Join-Path $extractDir 'collector.tar.gz'
            Copy-Item -Path $archive -Destination $localTar -Force
            Push-Location $extractDir
            try {
                tar -xzf ".\collector.tar.gz"
            }
            finally {
                Pop-Location
            }
        }
        else {
            throw "Unsupported archive type: $($collectorSpec.ArchiveType)"
        }

        Copy-Item -Path (Join-Path $extractDir '*') -Destination $targetDir -Recurse -Force
        $found = Get-ChildItem -Recurse -Path $targetDir -Filter $collectorSpec.ExecutablePattern | Select-Object -First 1
        if (-not $found) {
            throw "otelcol-contrib.exe not found after extraction."
        }

        Write-Host "OTel Collector installed to $($found.FullName)"
    }
    finally {
        if (Test-Path $extractDir) {
            Remove-Item -Recurse -Force $extractDir
        }
        if (Test-Path $archive) {
            Remove-Item -Force $archive
        }
    }
}

Ensure-Directory $InstallDir
Write-Host "Installing runtime dependencies into $InstallDir"
Install-Vm
Install-Collector
Write-Host "Runtime components ready."
