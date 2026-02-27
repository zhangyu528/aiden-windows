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
if [ "${EVENT_NAME:-}" = "workflow_run" ]; then
  status="${WORKFLOW_CONCLUSION:-completed}"
  run_url="${SOURCE_RUN_URL:-${CURRENT_RUN_URL:-}}"
fi

ts="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
workflow_display="${WORKFLOW_NAME:-}"
if [ -z "$workflow_display" ]; then
  workflow_display="${GITHUB_WORKFLOW:-Feishu Notification}"
fi

status_text="$status"
case "$status" in
  success) status_text="Success" ;;
  failure) status_text="Failure" ;;
  cancelled) status_text="Cancelled" ;;
  timed_out) status_text="Timed Out" ;;
  action_required) status_text="Action Required" ;;
  triggered) status_text="Triggered" ;;
  completed) status_text="Completed" ;;
esac

color="blue"
case "$status" in
  success) color="green" ;;
  failure|cancelled|timed_out|action_required) color="red" ;;
esac

jq -n \
  --arg repository "${REPOSITORY:-}" \
  --arg event_name "${EVENT_NAME:-}" \
  --arg status_text "$status_text" \
  --arg actor "${ACTOR:-}" \
  --arg ref_name "${REF_NAME:-}" \
  --arg workflow_display "$workflow_display" \
  --arg run_url "$run_url" \
  --arg ts "$ts" \
  --arg color "$color" \
  '{
    msg_type: "interactive",
    card: {
      config: { wide_screen_mode: true, enable_forward: true },
      header: {
        template: $color,
        title: { tag: "plain_text", content: "Aiden Windows Notification" }
      },
      elements: [
        {
          tag: "div",
          fields: [
            { is_short: true, text: { tag: "lark_md", content: ("**Repository**\n" + $repository) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Event**\n" + $event_name) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Status**\n" + $status_text) } },
            { is_short: true, text: { tag: "lark_md", content: ("**Actor**\n" + $actor) } },
            { is_short: false, text: { tag: "lark_md", content: ("**Ref**\n" + $ref_name) } },
            { is_short: false, text: { tag: "lark_md", content: ("**Workflow**\n" + $workflow_display) } }
          ]
        },
        {
          tag: "action",
          actions: [
            {
              tag: "button",
              text: { tag: "plain_text", content: "Open Run" },
              type: "primary",
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

echo "status=$status" >> "$GITHUB_OUTPUT"
echo "run_url=$run_url" >> "$GITHUB_OUTPUT"
echo "skipped=false" >> "$GITHUB_OUTPUT"
echo "summary=event=${EVENT_NAME:-}, status=${status}, run_url=${run_url}" >> "$GITHUB_OUTPUT"
echo "::notice::${EVENT_NAME:-} | ${status} | ${run_url}"
