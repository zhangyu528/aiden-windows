param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$source = "$env:GITHUB_WORKSPACE\artifacts\stage\package"
$output = "$env:GITHUB_WORKSPACE\artifacts\installer"

& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
  "/DAppVersion=$Version" `
  "/DSourceDir=$source" `
  "/DOutputDir=$output" `
  ".github\workflows\installer\aiden.iss"
