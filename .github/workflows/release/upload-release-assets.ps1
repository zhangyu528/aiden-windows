param(
    [Parameter(Mandatory = $true)]
    [string]$UploadUrl,
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$GitHubToken
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Upload-ReleaseAsset {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUploadUrl,
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string]$AssetName,
        [Parameter(Mandatory = $true)]
        [string]$ContentType,
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    if (-not (Test-Path $FilePath)) {
        throw "Asset file not found: $FilePath"
    }

    $cleanUrl = $BaseUploadUrl -replace '\{\?name,label\}$', ''
    $encodedName = [uri]::EscapeDataString($AssetName)
    $assetUrl = "$cleanUrl?name=$encodedName"

    $headers = @{
        Authorization = "Bearer $Token"
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }

    Invoke-WebRequest `
      -Uri $assetUrl `
      -Method Post `
      -Headers $headers `
      -InFile $FilePath `
      -ContentType $ContentType | Out-Null
}

$installerName = "Aiden-Setup-$Version-win-x64.exe"
$installerPath = "artifacts/installer/$installerName"
$checksumsName = "SHA256SUMS.txt"
$checksumsPath = "artifacts/installer/$checksumsName"

Upload-ReleaseAsset -BaseUploadUrl $UploadUrl -FilePath $installerPath -AssetName $installerName -ContentType "application/octet-stream" -Token $GitHubToken
Upload-ReleaseAsset -BaseUploadUrl $UploadUrl -FilePath $checksumsPath -AssetName $checksumsName -ContentType "text/plain" -Token $GitHubToken
