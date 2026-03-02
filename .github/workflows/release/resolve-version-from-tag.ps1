param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($Tag)) {
    throw "Tag is empty."
}

if ($Tag -match '^v(?<clean>.+)$') {
    $version = $Matches.clean
}
else {
    $version = $Tag
}

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Resolved version is empty for tag '$Tag'."
}

if ($env:GITHUB_OUTPUT) {
    "version=$version" >> $env:GITHUB_OUTPUT
}

Write-Output $version
