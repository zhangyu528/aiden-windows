param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$installer = "artifacts/installer/Aiden-Setup-$Version-win-x64.exe"
if (-not (Test-Path $installer)) {
    throw "Installer not found: $installer"
}

$hash = (Get-FileHash -Algorithm SHA256 -Path $installer).Hash.ToLowerInvariant()
"$hash  $(Split-Path -Leaf $installer)" | Out-File -FilePath "artifacts/installer/SHA256SUMS.txt" -Encoding ascii
