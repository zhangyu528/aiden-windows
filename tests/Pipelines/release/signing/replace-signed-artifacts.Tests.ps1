Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'replace-signed-artifacts.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:replaceScript = Join-Path $script:repoRoot 'pipelines\release\signing\replace-signed-artifacts.ps1'
    }

    It 'throws error if tray executable is missing' {
        Mock Test-Path { $false }
        { & $script:replaceScript -Version '1.0.0' } | Should -Throw "Signed tray executable not found: *"
    }

    It 'throws error if agent executable is missing' {
        Mock Test-Path { 
            param($path)
            if ($path -match 'tray') { return $true }
            return $false
        }
        { & $script:replaceScript -Version '1.0.0' } | Should -Throw "Signed agent executable not found: *"
    }

    It 'successfully copies signed artifacts' {
        Mock Test-Path { $true }
        Mock Get-ChildItem { return @{ FullName = 'path/to/installer.exe' } }
        Mock Copy-Item { }

        { & $script:replaceScript -Version '1.2.3' } | Should -Not -Throw
        
        Assert-MockCalled Copy-Item -Times 3
    }
}
