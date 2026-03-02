Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'resolve-version-from-tag.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:resolveScript = Join-Path $script:repoRoot 'pipelines\release\versioning\resolve-version-from-tag.ps1'
    }

    It "resolves 'v1.2.3' to '1.2.3'" {
        $result = & $script:resolveScript -Tag 'v1.2.3'
        $result | Should -Be '1.2.3'
    }

    It "resolves '2.0.0' to '2.0.0'" {
        $result = & $script:resolveScript -Tag '2.0.0'
        $result | Should -Be '2.0.0'
    }

    It "throws error on empty tag" {
        { & $script:resolveScript -Tag '' } | Should -Throw
    }
}
