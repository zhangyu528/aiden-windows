$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($env:SIGNPATH_READY -ne "true") {
    throw "SignPath onboarding is not marked ready. Set repository variable SIGNPATH_READY=true after SignPath approval."
}

$required = @(
    "SIGNPATH_ORGANIZATION_ID",
    "SIGNPATH_PROJECT_SLUG",
    "SIGNPATH_SIGNING_POLICY_SLUG",
    "SIGNPATH_UNSIGNED_ARTIFACT_CFG",
    "SIGNPATH_INSTALLER_ARTIFACT_CFG"
)

$missing = @()
foreach ($name in $required) {
    $item = Get-Item -Path "Env:$name" -ErrorAction SilentlyContinue
    if ($null -eq $item -or [string]::IsNullOrWhiteSpace($item.Value)) {
        $missing += $name
    }
}

if ($missing.Count -gt 0) {
    throw "Missing SignPath configuration: $($missing -join ', ')"
}
