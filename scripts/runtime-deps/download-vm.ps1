param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$DownloadUrl = "",

    [Parameter(Mandatory = $false)]
    [string]$Sha256 = "",

    [Parameter(Mandatory = $false)]
    [string]$InstallRoot = "",

    [Parameter(Mandatory = $false)]
    [switch]$AllowInsecureFallback,

    [Parameter(Mandatory = $false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Version must be provided."
}

if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
    $DownloadUrl = "https://github.com/VictoriaMetrics/VictoriaMetrics/releases/download/$Version/victoria-metrics-windows-amd64-$Version.zip"
}

function Resolve-Sha256FromReleaseAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [Parameter(Mandatory = $true)]
        [string]$ArchiveFileName
    )

    $apiUrl = "https://api.github.com/repos/$Repo/releases/tags/$Version"
    $headers = @{ "User-Agent" = "aiden-runtime-script" }
    $release = Invoke-RestMethod -Uri $apiUrl -Headers $headers
    if (-not $release -or -not $release.assets) {
        throw "Release assets not found for $Repo@$Version"
    }

    $checksumAssets = @($release.assets | Where-Object {
        ($_.name -match '(?i)checksum|sha256') -and ($_.name -match '(?i)\.txt$')
    })
    foreach ($asset in $checksumAssets) {
        try {
            Write-Host "Resolving SHA256 from: $($asset.browser_download_url)"
            $checksumsText = (Invoke-WebRequest -Uri $asset.browser_download_url -Headers $headers -UseBasicParsing).Content
            $escapedName = [regex]::Escape($ArchiveFileName)
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

    throw "Unable to resolve SHA256 for $ArchiveFileName. Provide -Sha256 explicitly."
}

$installBase = if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
    Join-Path $repoRoot "Aiden.RuntimeAgent"
}
else {
    if (-not (Test-Path $InstallRoot)) {
        New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
    }
    (Resolve-Path $InstallRoot).Path
}

$installRoot = Join-Path $installBase "runtime\vm"
$targetDir = Join-Path $installRoot $Version
$exePath = Join-Path $targetDir "victoria-metrics.exe"

if ((Test-Path $exePath) -and (-not $Force.IsPresent)) {
    Write-Host "victoria-metrics already exists: $exePath"
    exit 0
}

New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$tempZip = Join-Path $env:TEMP ("victoria-metrics-" + $Version + ".zip")
if (Test-Path $tempZip) {
    Remove-Item -Force $tempZip
}

if ([string]::IsNullOrWhiteSpace($Sha256)) {
    $archiveName = Split-Path -Leaf $DownloadUrl
    try {
        $Sha256 = Resolve-Sha256FromReleaseAssets -Repo "VictoriaMetrics/VictoriaMetrics" -Version $Version -ArchiveFileName $archiveName
    }
    catch {
        if (-not $AllowInsecureFallback.IsPresent) {
            throw
        }

        Write-Warning "Unable to auto-resolve SHA256 from release assets. Continuing without hash verification because -AllowInsecureFallback is set. Details: $($_.Exception.Message)"
        $Sha256 = ""
    }
}

Write-Host "Downloading: $DownloadUrl"
Invoke-WebRequest -Uri $DownloadUrl -OutFile $tempZip

if (-not [string]::IsNullOrWhiteSpace($Sha256)) {
    $actualHash = (Get-FileHash -Algorithm SHA256 -Path $tempZip).Hash.ToLowerInvariant()
    $expectedHash = $Sha256.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        throw "SHA256 mismatch. expected=$expectedHash actual=$actualHash"
    }
}
else {
    Write-Warning "SHA256 verification skipped (no hash provided or resolvable)."
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
