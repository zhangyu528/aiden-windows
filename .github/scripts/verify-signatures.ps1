param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$files = @(
    "artifacts/stage/tray/Aiden.TrayMonitor.exe",
    "artifacts/stage/agent/Aiden.RuntimeAgent.exe",
    "artifacts/installer/Aiden-Setup-$Version-win-x64.exe"
)

foreach ($file in $files) {
    if (-not (Test-Path $file)) {
        throw "Missing file for signature verification: $file"
    }

    $sig = Get-AuthenticodeSignature -FilePath $file
    if ($sig.Status -ne "Valid") {
        throw "Invalid signature on $file. Status=$($sig.Status)"
    }
}
