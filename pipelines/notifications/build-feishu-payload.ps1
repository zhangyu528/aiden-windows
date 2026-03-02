$ErrorActionPreference = 'Stop'

if (-not $env:GITHUB_OUTPUT) {
    Write-Error "GITHUB_OUTPUT is not available."
    exit 1
}

if (-not $env:FEISHU_WEBHOOK) {
    Write-Warning "FEISHU_WEBHOOK is not configured; skip notification."
    "skipped=true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    exit 0
}

$runUrl = $env:CURRENT_RUN_URL
$commitUrl = $env:COMMIT_URL
$commitSha = $env:COMMIT_SHA
$commitMessage = if ($env:COMMIT_MESSAGE) { $env:COMMIT_MESSAGE } else { "(No commit message)" }
$workflowDisplay = if ($env:WORKFLOW_NAME) { $env:WORKFLOW_NAME } elseif ($env:GITHUB_WORKFLOW) { $env:GITHUB_WORKFLOW } else { "Feishu Notification" }
$ts = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$shortSha = if ($commitSha) { $commitSha.SubString(0, [Math]::Min(7, $commitSha.Length)) } else { "unknown" }

$payload = @{
    msg_type = "interactive"
    card = @{
        config = @{ wide_screen_mode = $true; enable_forward = $true }
        header = @{
            template = "green"
            title = @{ tag = "plain_text"; content = "Main Branch Updated" }
        }
        elements = @(
            @{
                tag = "markdown"
                content = "**Commit Message**`n$commitMessage"
            },
            @{
                tag = "div"
                fields = @(
                    @{ is_short = $true; text = @{ tag = "lark_md"; content = "**Repository**`n$($env:REPOSITORY)" } },
                    @{ is_short = $true; text = @{ tag = "lark_md"; content = "**Actor**`n$($env:ACTOR)" } },
                    @{ is_short = $true; text = @{ tag = "lark_md"; content = "**Branch**`n$($env:REF_NAME)" } },
                    @{ is_short = $true; text = @{ tag = "lark_md"; content = "**Commit**`n$shortSha" } },
                    @{ is_short = $false; text = @{ tag = "lark_md"; content = "**Workflow**`n$workflowDisplay" } },
                    @{ is_short = $false; text = @{ tag = "lark_md"; content = "**Event**`n$($env:EVENT_NAME)" } }
                )
            },
            @{
                tag = "action"
                actions = @(
                    @{
                        tag = "button"
                        text = @{ tag = "plain_text"; content = "Open Commit" }
                        type = "primary"
                        url = if ($commitUrl) { $commitUrl } else { $runUrl }
                    },
                    @{
                        tag = "button"
                        text = @{ tag = "plain_text"; content = "Open Run" }
                        type = "default"
                        url = $runUrl
                    }
                )
            },
            @{
                tag = "note"
                elements = @(
                    @{ tag = "plain_text"; content = "Timestamp (UTC): $ts" }
                )
            }
        )
    }
}

$payload | ConvertTo-Json -Depth 10 | Out-File -FilePath "payload.json" -Encoding utf8

"status=notified" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
"run_url=$runUrl" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
"skipped=false" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
"summary=event=$($env:EVENT_NAME), commit=$shortSha, run_url=$runUrl" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8

Write-Host "::notice::$($env:EVENT_NAME) | commit=$shortSha | $runUrl"
