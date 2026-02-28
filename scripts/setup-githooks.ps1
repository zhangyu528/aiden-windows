$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    git config core.hooksPath .githooks
    $hooksPath = git config --get core.hooksPath
    if ($hooksPath -ne '.githooks') {
        throw "Failed to set core.hooksPath to .githooks (current: $hooksPath)"
    }

    Write-Host "Git hooks enabled. core.hooksPath=$hooksPath"
}
finally {
    Pop-Location
}
