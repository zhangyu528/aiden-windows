Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'resolve-version-from-tag.ps1' {
    It 'strips v prefix and keeps suffix' {
        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
        $resolveScript = Join-Path $repoRoot '.github\scripts\resolve-version-from-tag.ps1'
        $result = & $resolveScript -Tag 'v1.2.3-rc.1'
        if ($result -ne '1.2.3-rc.1') {
            throw "Expected 1.2.3-rc.1 but got: $result"
        }
    }

    It 'throws on empty tag' {
        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
        $thrown = $false
        try {
            $resolveScript = Join-Path $repoRoot '.github\scripts\resolve-version-from-tag.ps1'
            & $resolveScript -Tag '' | Out-Null
        }
        catch {
            $thrown = $true
        }

        if (-not $thrown) {
            throw 'Expected script to throw for empty tag.'
        }
    }
}

Describe 'check-signpath-readiness.ps1' {
    BeforeAll {
        $script:originalEnv = @{
            SIGNPATH_READY = $env:SIGNPATH_READY
            SIGNPATH_ORGANIZATION_ID = $env:SIGNPATH_ORGANIZATION_ID
            SIGNPATH_PROJECT_SLUG = $env:SIGNPATH_PROJECT_SLUG
            SIGNPATH_SIGNING_POLICY_SLUG = $env:SIGNPATH_SIGNING_POLICY_SLUG
            SIGNPATH_UNSIGNED_ARTIFACT_CFG = $env:SIGNPATH_UNSIGNED_ARTIFACT_CFG
            SIGNPATH_INSTALLER_ARTIFACT_CFG = $env:SIGNPATH_INSTALLER_ARTIFACT_CFG
        }
    }

    AfterAll {
        $env:SIGNPATH_READY = $script:originalEnv.SIGNPATH_READY
        $env:SIGNPATH_ORGANIZATION_ID = $script:originalEnv.SIGNPATH_ORGANIZATION_ID
        $env:SIGNPATH_PROJECT_SLUG = $script:originalEnv.SIGNPATH_PROJECT_SLUG
        $env:SIGNPATH_SIGNING_POLICY_SLUG = $script:originalEnv.SIGNPATH_SIGNING_POLICY_SLUG
        $env:SIGNPATH_UNSIGNED_ARTIFACT_CFG = $script:originalEnv.SIGNPATH_UNSIGNED_ARTIFACT_CFG
        $env:SIGNPATH_INSTALLER_ARTIFACT_CFG = $script:originalEnv.SIGNPATH_INSTALLER_ARTIFACT_CFG
    }

    It 'fails when readiness flag is not true' {
        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
        $env:SIGNPATH_READY = 'false'
        $env:SIGNPATH_ORGANIZATION_ID = 'org'
        $env:SIGNPATH_PROJECT_SLUG = 'proj'
        $env:SIGNPATH_SIGNING_POLICY_SLUG = 'policy'
        $env:SIGNPATH_UNSIGNED_ARTIFACT_CFG = 'unsigned'
        $env:SIGNPATH_INSTALLER_ARTIFACT_CFG = 'installer'

        $thrown = $false
        try {
            $readinessScript = Join-Path $repoRoot '.github\scripts\check-signpath-readiness.ps1'
            & $readinessScript | Out-Null
        }
        catch {
            $thrown = $true
        }

        if (-not $thrown) {
            throw 'Expected readiness script to throw when SIGNPATH_READY is not true.'
        }
    }

    It 'passes when all required variables exist and readiness true' {
        $repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
        $env:SIGNPATH_READY = 'true'
        $env:SIGNPATH_ORGANIZATION_ID = 'org'
        $env:SIGNPATH_PROJECT_SLUG = 'proj'
        $env:SIGNPATH_SIGNING_POLICY_SLUG = 'policy'
        $env:SIGNPATH_UNSIGNED_ARTIFACT_CFG = 'unsigned'
        $env:SIGNPATH_INSTALLER_ARTIFACT_CFG = 'installer'

        $readinessScript = Join-Path $repoRoot '.github\scripts\check-signpath-readiness.ps1'
        & $readinessScript | Out-Null
    }
}
