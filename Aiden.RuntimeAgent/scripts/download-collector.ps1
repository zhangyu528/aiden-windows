param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$DownloadUrl = "",

    [Parameter(Mandatory = $false)]
    [string]$Sha256 = "",

    [Parameter(Mandatory = $false)]
    [bool]$VerifyComponents = $true
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version must be provided."
}

if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
    $DownloadUrl = "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/$Version/otelcol-contrib_$($Version.TrimStart('v'))_windows_amd64.tar.gz"
}

$expectedExeName = "otelcol-contrib.exe"

function Resolve-Sha256FromChecksums {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$ArchiveFileName
    )

    $checksumsUrl = "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/$Version/checksums.txt"
    Write-Host "Resolving SHA256 from: $checksumsUrl"
    $checksumsText = (Invoke-WebRequest -Uri $checksumsUrl -UseBasicParsing).Content
    $match = [regex]::Match($checksumsText, "(?im)^([a-f0-9]{64})\s+\*?$([regex]::Escape($ArchiveFileName))\s*$")
    if (-not $match.Success) {
        throw "Unable to resolve SHA256 for $ArchiveFileName from checksums.txt"
    }

    return $match.Groups[1].Value.ToLowerInvariant()
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$installRoot = Join-Path $repoRoot "Aiden.RuntimeAgent\runtime\collector"
$targetDir = Join-Path $installRoot $Version

if (Test-Path $targetDir) {
    $existing = Get-ChildItem -Recurse -Path $targetDir -Filter $expectedExeName | Select-Object -First 1
    if ($existing) {
        Write-Host "Collector already exists: $($existing.FullName)"
        exit 0
    }
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$archiveName = Split-Path -Leaf $DownloadUrl
$resolvedSha256 = if ([string]::IsNullOrWhiteSpace($Sha256)) {
    try {
        Resolve-Sha256FromChecksums -Version $Version -ArchiveFileName $archiveName
    }
    catch {
        Write-Warning "Unable to auto-resolve SHA256 from release checksums. Continuing without hash verification. Details: $($_.Exception.Message)"
        ""
    }
}
else {
    $Sha256.ToLowerInvariant()
}

$tempArchive = Join-Path $env:TEMP ("otelcol-" + $Version + "-" + $archiveName)
if (Test-Path $tempArchive) {
    Remove-Item -Force $tempArchive
}

Write-Host "Downloading: $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempArchive

if (-not [string]::IsNullOrWhiteSpace($resolvedSha256)) {
    $actualHash = (Get-FileHash -Algorithm SHA256 -Path $tempArchive).Hash.ToLowerInvariant()
    $expectedHash = $resolvedSha256
    if ($actualHash -ne $expectedHash) {
        throw "SHA256 mismatch. expected=$expectedHash actual=$actualHash"
    }
}
else {
    Write-Warning "SHA256 verification skipped (no hash provided or resolvable)."
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

    $found = Get-ChildItem -Recurse -Path $targetDir -Filter $expectedExeName | Select-Object -First 1
    if (-not $found) {
        throw "No $expectedExeName found after extraction. This project requires otelcol-contrib."
    }

    if ($VerifyComponents) {
        $requiredComponents = @("count", "spanmetrics", "transform", "filter", "otlphttp")
        $componentsOutput = & $found.FullName components 2>&1 | Out-String
        $missing = @()
        foreach ($component in $requiredComponents) {
            if ($componentsOutput -notmatch ("- name:\s*" + [regex]::Escape($component))) {
                $missing += $component
            }
        }

        if ($missing.Count -gt 0) {
            throw "Installed collector is missing required components: $($missing -join ', ')"
        }
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
