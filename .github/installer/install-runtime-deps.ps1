param(
    [Parameter(Mandatory = $false)]
    [string]$InstallDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$logPath = Join-Path $InstallDir 'install-runtime-deps.log'
function Write-Log {
    param([string]$Message)
    Ensure-Directory $InstallDir
    $timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    "$timestamp $Message" | Out-File -FilePath $logPath -Encoding UTF8 -Append
}

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

function New-DownloadTempFile([string]$prefix, [string]$extension = '') {
    $suffix = ''
    if (-not [string]::IsNullOrWhiteSpace($extension)) {
        $suffix = if ($extension.StartsWith('.')) { $extension } else { ".$extension" }
    }
    $candidate = Join-Path $env:TEMP ("$prefix-" + [guid]::NewGuid().ToString('N') + $suffix)
    if (Test-Path $candidate) {
        Remove-Item -Force $candidate
    }
    return $candidate
}

function Invoke-Download([string]$url, [string]$destination) {
    if (Test-Path $destination) {
        Remove-Item -Force $destination
    }
    Write-Log "Downloading $url"
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $destination -UseBasicParsing
    Write-Log "Downloaded to $destination"
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
    Write-Log "[VM] target: $exePath"
    if ((Test-Path $exePath) -and (-not $Force.IsPresent)) {
        Write-Log "VictoriaMetrics already installed at $exePath"
        Write-Host "VictoriaMetrics already installed at $exePath"
        return
    }

    Ensure-Directory $targetDir
    $archive = New-DownloadTempFile -prefix "victoria-metrics-$($vmSpec.Version)" -extension ".zip"
    $extractDir = Join-Path $env:TEMP ("victoria-metrics-unpack-" + [guid]::NewGuid().ToString('N'))
    try {
        Write-Log "[VM] download start"
        Invoke-Download -url $vmSpec.DownloadUrl -destination $archive
        Verify-Sha256 -file $archive -expected $vmSpec.Sha256
        Write-Log "[VM] sha256 verified"
        Expand-Archive -Path $archive -DestinationPath $extractDir -Force
        Write-Log "[VM] archive extracted"
        $found = Get-ChildItem -Recurse -Path $extractDir -Filter $vmSpec.ExecutablePattern | Select-Object -First 1
        if (-not $found) {
            Write-Log "VictoriaMetrics executable not found inside the archive."
            throw "VictoriaMetrics executable not found inside the archive."
        }
        Copy-Item -Path $found.FullName -Destination $exePath -Force
        Write-Log "VictoriaMetrics installed to $exePath"
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
    Write-Log "[OTEL] target: $exePath"
    if ((Test-Path $exePath) -and (-not $Force.IsPresent)) {
        Write-Log "OTel Collector already installed at $exePath"
        Write-Host "OTel Collector already installed at $exePath"
        return
    }

    Ensure-Directory $targetDir
    $archive = New-DownloadTempFile -prefix "otelcol-$($collectorSpec.Version)" -extension ".tar.gz"
    $extractDir = Join-Path $env:TEMP ("otelcol-unpack-" + [guid]::NewGuid().ToString('N'))
    try {
        Write-Log "[OTEL] download start"
        Invoke-Download -url $collectorSpec.DownloadUrl -destination $archive
        Verify-Sha256 -file $archive -expected $collectorSpec.Sha256
        Write-Log "[OTEL] sha256 verified"
        Ensure-Directory $extractDir
        if ($collectorSpec.ArchiveType -eq 'zip') {
            Expand-Archive -Path $archive -DestinationPath $extractDir -Force
            Write-Log "[OTEL] zip extracted"
        }
        elseif ($collectorSpec.ArchiveType -eq 'tar.gz') {
            $tarCmd = Get-Command tar -ErrorAction SilentlyContinue
            if (-not $tarCmd) {
                Write-Log "[OTEL] tar command not found"
                throw "tar command is required to extract .tar.gz archives but was not found."
            }
            $localTar = Join-Path $extractDir 'collector.tar.gz'
            Copy-Item -Path $archive -Destination $localTar -Force
            Push-Location $extractDir
            try {
                tar -xzf ".\collector.tar.gz"
                Write-Log "[OTEL] tar.gz extracted"
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
            Write-Log "otelcol-contrib.exe not found after extraction."
            throw "otelcol-contrib.exe not found after extraction."
        }

        Write-Log "OTel Collector installed to $($found.FullName)"
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
Write-Log "Installing runtime dependencies into $InstallDir"
try {
    Install-Vm
    Install-Collector
    Write-Log "Runtime components ready."
}
catch {
    Write-Log "Runtime dependency installation failed: $($_.Exception.Message)"
    throw
}
