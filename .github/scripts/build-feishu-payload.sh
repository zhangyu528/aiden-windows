#!/usr/bin/env bash
set -euo pipefail

if [ -z "${GITHUB_OUTPUT:-}" ]; then
  echo "::error::GITHUB_OUTPUT is not available."
  exit 1
fi

if [ -z "${FEISHU_WEBHOOK:-}" ]; then
  echo "::warning::FEISHU_WEBHOOK is not configured; skip notification."
  echo "skipped=true" >> "$GITHUB_OUTPUT"
  exit 0
fi

status="triggered"
run_url="${CURRENT_RUN_URL:-}"
commit_url="${COMMIT_URL:-}"
commit_sha="${COMMIT_SHA:-}"
commit_message="${COMMIT_MESSAGE:-}"

ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
workflow_display="${WORKFLOW_NAME:-}"
if [ -z "$workflow_display" ]; then
  workflow_display="${GITHUB_WORKFLOW:-Feishu Notification}"
fi

if [ -z "$commit_message" ]; then
  commit_message="(No commit message)"
fi

short_sha="${commit_sha:0:7}"
if [ -z "$short_sha" ]; then
  short_sha="unknown"
fi

jq -n \
  --arg repository "${REPOSITORY:-}" \
  --arg event_name "${EVENT_NAME:-}" \
  --arg actor "${ACTOR:-}" \
  --arg ref_name "${REF_NAME:-}" \
  --arg workflow_display "$workflow_display" \
  --arg run_url "$run_url" \
  --arg commit_url "$commit_url" \
  --arg commit_sha "$commit_sha" \
  --arg short_sha "$short_sha" \
  --arg commit_message "$commit_message" \
  --arg ts "$ts" \
  --arg color "green" \
  '{
    msg_type: "interactive",
    card: {
      config: { wide_screen_mode: true, enable_forward: true },
      header: {
        template: $color,
        title: { tag: "plain_text", content: "Main Branch Updated" }
      },
      elements: [
        {
          tag: "markdown",
          content: ("**Commit Message**\n" + $commit_message)
        },
        {
          tag: "div",
          fields: [
            { is_short: true, text: { tag: "lark_md", content: ("**Repository**\n" + $repository) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Actor**\n" + $actor) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Branch**\n" + $ref_name) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Commit**\n" + $short_sha) } },
            { is_short: false, text: { tag: "lark_md", content: ("**Workflow**\n" + $workflow_display) } },
            { is_short: false, text: { tag: "lark_md", content: ("**Event**\n" + $event_name) } }
          ]
        },
        {
          tag: "action",
          actions: [
            {
              tag: "button",
              text: { tag: "plain_text", content: "Open Commit" },
              type: "primary",
              url: ($commit_url // $run_url)
            },
            {
              tag: "button",
              text: { tag: "plain_text", content: "Open Run" },
              type: "default",
              url: $run_url
            }
          ]
        },
        {
          tag: "note",
          elements: [
            { tag: "plain_text", content: ("Timestamp (UTC): " + $ts) }
          ]
        }
      ]
    }
  }' > payload.json

echo "status=notified" >> "$GITHUB_OUTPUT"
echo "run_url=$run_url" >> "$GITHUB_OUTPUT"
echo "skipped=false" >> "$GITHUB_OUTPUT"
echo "summary=event=${EVENT_NAME:-}, commit=${short_sha}, run_url=${run_url}" >> "$GITHUB_OUTPUT"
echo "::notice::${EVENT_NAME:-} | commit=${short_sha} | ${run_url}"
