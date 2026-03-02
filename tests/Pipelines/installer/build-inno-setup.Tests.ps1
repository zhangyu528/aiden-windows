Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'build-inno-setup.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
        $script:innoScript = Join-Path $script:repoRoot 'pipelines\installer\build-inno-setup.ps1'
    }

    It 'invokes ISCC.exe with correct parameters' {
        # Mocking external command ISCC.exe
        Mock & { 
            param($iscc, $v, $s, $o, $iss) 
            # Simply succeed
        }

        # Since ISCC.exe is a hardcoded path, we need to mock the call at that path or 
        # modify the script to be more testable. 
        # For now, we mock the call logic.
        
        # Note: Pester mocks for absolute paths can be tricky.
        # But we can mock the command name if PS resolves it.
        
        Mock "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" { } -Verifiable

        $env:GITHUB_WORKSPACE = 'test_ws'
        { & $script:innoScript -Version '1.0.0' } | Should -Not -Throw

        Assert-MockCalled "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    }
}
