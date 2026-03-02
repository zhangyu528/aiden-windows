param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Write-Host "Creating GitHub pre-release for tag: $Tag" -ForegroundColor Cyan

# Create the release
# --generate-notes: Automatically generate release notes from commits
# --prerelease: Mark as a pre-release
$releaseUrl = gh release create $Tag --prerelease --target main --generate-notes

if (-not $releaseUrl) {
    throw "Failed to create GitHub release for tag $Tag"
}

Write-Host "Release created: $releaseUrl"

# Get the upload URL for artifacts
# We need this for the subsequent upload steps
$uploadUrl = gh release view $Tag --json uploadUrl --jq .uploadUrl

if ($env:GITHUB_OUTPUT) {
    "release_url=$releaseUrl" >> $env:GITHUB_OUTPUT
    "release_upload_url=$uploadUrl" >> $env:GITHUB_OUTPUT
}
