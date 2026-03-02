Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'upload-release-assets.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:uploadScript = Join-Path $script:repoRoot 'pipelines\release\publishing\upload-release-assets.ps1'
    }

    It 'successfully invokes web requests for uploading assets' {
        Mock Test-Path { $true }
        Mock Invoke-WebRequest { return @{ StatusCode = 200 } }

        { 
            & $script:uploadScript -UploadUrl "https://upload.github.com/..." -Version '1.0.0' -GitHubToken 'secret' 
        } | Should -Not -Throw

        Assert-MockCalled Invoke-WebRequest -Times 2
    }

    It 'throws error if file is missing' {
        Mock Test-Path { $false }
        # The script defines the function then calls it. 
        # Since it calls it twice, failure at the first one is expected.
        { 
            & $script:uploadScript -UploadUrl 'url' -Version '1.0.0' -GitHubToken 'tok' 
        } | Should -Throw "Asset file not found: *"
    }
}
