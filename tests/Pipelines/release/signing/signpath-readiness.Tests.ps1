Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'check-signpath-readiness.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:readinessScript = Join-Path $script:repoRoot 'pipelines\release\signing\check-signpath-readiness.ps1'

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
        foreach ($key in $script:originalEnv.Keys) {
            Set-Item "Env:$key" $script:originalEnv[$key]
        }
    }

    It 'fails when readiness flag is not true' {
        $env:SIGNPATH_READY = 'false'
        { & $script:readinessScript } | Should -Throw
    }

    It 'passes when all required variables exist and readiness true' {
        $env:SIGNPATH_READY = 'true'
        $env:SIGNPATH_ORGANIZATION_ID = 'org'
        $env:SIGNPATH_PROJECT_SLUG = 'proj'
        $env:SIGNPATH_SIGNING_POLICY_SLUG = 'policy'
        $env:SIGNPATH_UNSIGNED_ARTIFACT_CFG = 'unsigned'
        $env:SIGNPATH_INSTALLER_ARTIFACT_CFG = 'installer'

        & $script:readinessScript | Out-Null
    }
}
