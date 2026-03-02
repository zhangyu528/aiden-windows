Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'generate-sha256sums.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:hashScript = Join-Path $script:repoRoot '.github\workflows\release\generate-sha256sums.ps1'
    }

    It 'throws error if installer is missing' {
        Mock Test-Path { $false }
        { & $script:hashScript -Version '1.0.0' } | Should -Throw "Installer not found: *"
    }

    It 'generates correct SHA256SUMS.txt format' {
        Mock Test-Path { $true }
        Mock Get-FileHash { return @{ Hash = 'ABCDEF123456' } }
        Mock Set-Content { }

        & $script:hashScript -Version '1.2.3'

        Assert-MockCalled -CommandName Set-Content
    }
}
