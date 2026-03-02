Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'verify-signatures.ps1 Workflow Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
        $script:verifyScript = Join-Path $script:repoRoot 'pipelines\release\signing\verify-signatures.ps1'
    }

    Context 'When files are missing' {
        It 'throws error if a file is not found' {
            Mock Test-Path { $false }
            { & $script:verifyScript -Version '1.0.0' } | Should -Throw "Missing file for signature verification: *"
        }
    }

    Context 'When files exist but have invalid signatures' {
        It 'throws error if signature status is NotSigned' {
            Mock Test-Path { $true }
            Mock Get-AuthenticodeSignature { return @{ Status = 'NotSigned' } }
            
            { & $script:verifyScript -Version '1.0.0' } | Should -Throw "Invalid signature on *. Status=NotSigned"
        }

        It 'throws error if signature status is HashMismatch' {
            Mock Test-Path { $true }
            Mock Get-AuthenticodeSignature { return @{ Status = 'HashMismatch' } }
            
            { & $script:verifyScript -Version '1.0.0' } | Should -Throw "Invalid signature on *. Status=HashMismatch"
        }
    }

    Context 'When all files are valid' {
        It 'passes without throwing' {
            Mock Test-Path { $true }
            Mock Get-AuthenticodeSignature { return @{ Status = 'Valid' } }
            
            { & $script:verifyScript -Version '1.0.0' } | Should -Not -Throw
        }
    }
}
