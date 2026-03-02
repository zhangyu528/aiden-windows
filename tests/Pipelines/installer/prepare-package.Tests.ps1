Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'prepare-package.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
        $script:prepareScript = Join-Path $script:repoRoot 'pipelines\installer\prepare-package.ps1'
    }

    It 'stages package components correctly' {
        Mock Test-Path { $true }
        Mock Remove-Item { }
        Mock New-Item { }
        Mock Copy-Item { }
        Mock Measure-Object { return @{ Count = 10; Sum = 1024 * 1024 } }

        { & $script:prepareScript -Version '1.0.0' } | Should -Not -Throw

        Assert-MockCalled Copy-Item -AtLeast 4
    }

    It 'throws error if publish directories are missing' {
        Mock Test-Path { $false }
        Mock New-Item { }
        { & $script:prepareScript -Version '1.0.0' } | Should -Throw "Publish directory not found: *"
    }
}
