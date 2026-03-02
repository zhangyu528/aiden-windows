Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Describe 'Feishu Notification Pipeline Tests' {
    BeforeAll {
        $script:repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
        $script:buildScript = Join-Path $script:repoRoot 'pipelines\notifications\build-feishu-payload.ps1'
        $script:sendScript = Join-Path $script:repoRoot 'pipelines\notifications\send-feishu-notification.ps1'
    }

    Context 'Payload Builder' {
        It 'skips if webhook is missing' {
            $env:FEISHU_WEBHOOK = ''
            $env:GITHUB_OUTPUT = [System.IO.Path]::GetTempFileName()
            
            try {
                & $script:buildScript
                $output = Get-Content $env:GITHUB_OUTPUT
                $output | Should -Contain 'skipped=true'
            } finally {
                Remove-Item $env:GITHUB_OUTPUT -ErrorAction SilentlyContinue
            }
        }
    }

    Context 'Notification Sender' {
        It 'throws error if payload file is missing' {
            $env:FEISHU_WEBHOOK = 'http://dummy-webhook'
            Mock Write-Host { }
            { & $script:sendScript -PayloadFile 'non-existent.json' -FailOnError $true } | Should -Throw "Payload file not found: *"
        }
    }
}
