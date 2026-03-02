param(
    [string]$WebhookUrl = $env:FEISHU_WEBHOOK,
    [string]$PayloadFile = "payload.json",
    [bool]$FailOnError = ($env:FAIL_ON_ERROR -eq 'true')
)

$ErrorActionPreference = 'Stop'

function Write-Notify {
    param([string]$Message, [string]$Level = "warning")
    if ($FailOnError -and $Level -eq "error") {
        throw $Message
    }
    Write-Host "::$Level::$Message"
}

if (-not $WebhookUrl) {
    Write-Notify "FEISHU_WEBHOOK is not configured." "error"
    exit 0
}

if (-not (Test-Path $PayloadFile)) {
    Write-Notify "Payload file not found: $PayloadFile" "error"
    exit 1
}

try {
    $response = Invoke-RestMethod -Uri $WebhookUrl -Method Post -ContentType "application/json" -InFile $PayloadFile
    
    if ($response.code -and $response.code -ne 0) {
        Write-Notify "Feishu API returned non-zero code: $($response.code)" "error"
    } else {
        Write-Host "::notice::Feishu notification delivered successfully."
    }
} catch {
    Write-Notify "Failed to send Feishu notification: $($_.Exception.Message)" "error"
}
