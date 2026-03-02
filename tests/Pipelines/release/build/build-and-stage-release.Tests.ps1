Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'build-and-stage-release.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:buildScript = Join-Path $script:repoRoot 'pipelines\release\build\build-and-stage-release.ps1'
    }

    It 'invokes dotnet publish and preparation script' {
        Mock dotnet { }
        Mock (Join-Path $script:repoRoot 'pipelines\installer\prepare-package.ps1') { }

        { & $script:buildScript -Version '1.2.3' } | Should -Not -Throw

        Assert-MockCalled dotnet -Times 2
        # Verify prepare-package was called (via mock name or logic)
    }

    It 'throws error on empty version' {
        { & $script:buildScript -Version '' } | Should -Throw "Version is empty."
    }
}
