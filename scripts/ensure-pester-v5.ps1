param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-HighestPesterModule {
    return Get-Module -ListAvailable -Name Pester |
        Sort-Object Version -Descending |
        Select-Object -First 1
}

$installed = Get-HighestPesterModule

if (-not $installed -or $installed.Version.Major -lt 5) {
    if (-not $Install) {
        throw "Pester v5 is required. Run: pwsh -ExecutionPolicy Bypass -File .\scripts\ensure-pester-v5.ps1 -Install"
    }

    Write-Host 'Installing Pester v5 to CurrentUser scope...'
    Set-PSRepository PSGallery -InstallationPolicy Trusted
    Install-Module Pester -Scope CurrentUser -MinimumVersion 5.0.0 -Force -SkipPublisherCheck
    $installed = Get-HighestPesterModule
}

if (-not $installed -or $installed.Version.Major -lt 5) {
    throw 'Failed to locate Pester v5 after installation attempt.'
}

Import-Module Pester -MinimumVersion 5.0.0 -Force
Write-Host "Using Pester version: $($installed.Version)"
