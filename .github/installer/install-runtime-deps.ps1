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
    Sha256 = ''
    Destination = 'runtime\vm'
    ExecutablePattern = 'victoria-metrics*.exe'
}

$collectorSpec = @{
    Name = 'OpenTelemetry Collector'
    Version = 'v0.146.1'
    DownloadUrl = 'https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.146.1/otelcol-contrib_0.146.1_windows_amd64.tar.gz'
    Sha256 = ''
    Destination = 'runtime\collector'
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

function Resolve-Sha256FromReleaseAssets([string]$repo, [string]$version, [string]$archiveFileName) {
    $apiUrl = "https://api.github.com/repos/$repo/releases/tags/$version"
    Write-Log "Resolving SHA256 via release API: $apiUrl"
    $headers = @{ "User-Agent" = "aiden-installer" }
    $release = Invoke-RestMethod -Uri $apiUrl -Headers $headers
    if (-not $release -or -not $release.assets) {
        throw "Release assets not found for $repo@$version"
    }

    $checksumAssets = @($release.assets | Where-Object {
        ($_.name -match '(?i)checksum|sha256') -and ($_.name -match '(?i)\.txt$')
    })
    if ($checksumAssets.Count -eq 0) {
        throw "No checksum text asset found in $repo@$version release assets"
    }

    foreach ($asset in $checksumAssets) {
        $checksumUrl = $asset.browser_download_url
        Write-Log "Trying checksum asset: $($asset.name)"
        try {
            $checksumsText = (Invoke-WebRequest -Uri $checksumUrl -Headers $headers -UseBasicParsing).Content
            $escapedName = [regex]::Escape($archiveFileName)
            $pattern = "(?im)^([a-f0-9]{64})\s+\*?(?:.+/)?$escapedName\s*$"
            $match = [regex]::Match($checksumsText, $pattern)
            if ($match.Success) {
                return $match.Groups[1].Value.ToLowerInvariant()
            }
        }
        catch {
            continue
        }
    }

    throw "Unable to resolve SHA256 for $archiveFileName from release checksum assets."
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
        $vmArchiveName = Split-Path -Leaf $vmSpec.DownloadUrl
        $expectedVmSha = if ([string]::IsNullOrWhiteSpace($vmSpec.Sha256)) {
            try {
                Resolve-Sha256FromReleaseAssets -repo "VictoriaMetrics/VictoriaMetrics" -version $vmSpec.Version -archiveFileName $vmArchiveName
            }
            catch {
                Write-Log "[VM] unable to resolve sha256 from checksums, continue without hash verification: $($_.Exception.Message)"
                Write-Warning "Unable to resolve VM SHA256 from release checksums. Continuing without hash verification. Details: $($_.Exception.Message)"
                ""
            }
        }
        else {
            $vmSpec.Sha256
        }
        Write-Log "[VM] expected sha256: $expectedVmSha"
        Write-Log "[VM] download start"
        Invoke-Download -url $vmSpec.DownloadUrl -destination $archive
        if (-not [string]::IsNullOrWhiteSpace($expectedVmSha)) {
            Verify-Sha256 -file $archive -expected $expectedVmSha
            Write-Log "[VM] sha256 verified"
        }
        else {
            Write-Log "[VM] sha256 verification skipped"
        }
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
        $archiveName = Split-Path -Leaf $collectorSpec.DownloadUrl
        $expectedCollectorSha = if ([string]::IsNullOrWhiteSpace($collectorSpec.Sha256)) {
            try {
                Resolve-Sha256FromReleaseAssets -repo "open-telemetry/opentelemetry-collector-releases" -version $collectorSpec.Version -archiveFileName $archiveName
            }
            catch {
                Write-Log "[OTEL] unable to resolve sha256 from checksums, continue without hash verification: $($_.Exception.Message)"
                Write-Warning "Unable to resolve OTel SHA256 from release checksums. Continuing without hash verification. Details: $($_.Exception.Message)"
                ""
            }
        }
        else {
            $collectorSpec.Sha256
        }
        Write-Log "[OTEL] expected sha256: $expectedCollectorSha"
        Write-Log "[OTEL] download start"
        Invoke-Download -url $collectorSpec.DownloadUrl -destination $archive
        if (-not [string]::IsNullOrWhiteSpace($expectedCollectorSha)) {
            Verify-Sha256 -file $archive -expected $expectedCollectorSha
            Write-Log "[OTEL] sha256 verified"
        }
        else {
            Write-Log "[OTEL] sha256 verification skipped"
        }
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
        Get-ChildItem -Path $targetDir -Recurse -File -Include *.tar.gz,*.zip -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
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
