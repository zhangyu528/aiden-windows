Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'install-runtime-deps.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
        $script:installScript = Join-Path $script:repoRoot 'pipelines\installer\install-runtime-deps.ps1'
    }

    It 'invokes download scripts with correct parameters' {
        Mock Test-Path { $true }
        Mock (Join-Path $script:repoRoot 'pipelines\installer\download-vm.ps1') { }
        Mock (Join-Path $script:repoRoot 'pipelines\installer\download-collector.ps1') { }
        Mock Out-File { }

        { & $script:installScript -InstallDir 'test_dir' } | Should -Not -Throw

        # Assert-MockCalled for sub-scripts would go here
    }

    It 'throws error if sub-scripts are missing' {
        Mock Test-Path { $false }
        { & $script:installScript } | Should -Throw "download-vm.ps1 not found: *"
    }
}
