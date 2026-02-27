#!/usr/bin/env bash
set -euo pipefail

FAIL_ON_ERROR="${FAIL_ON_ERROR:-false}"

fail() {
  local msg="$1"
  if [ "${FAIL_ON_ERROR}" = "true" ]; then
    echo "::error::${msg}"
    exit 1
  fi
  echo "::warning::${msg}"
}

http_code="$(curl -sS -o feishu-response.json -w "%{http_code}" \
  --retry 3 \
  --retry-delay 2 \
  --retry-all-errors \
  --max-time 20 \
  -H 'Content-Type: application/json' \
  -d @payload.json \
  "${FEISHU_WEBHOOK}")"

if [ "${http_code}" -lt 200 ] || [ "${http_code}" -ge 300 ]; then
  fail "Feishu webhook returned HTTP ${http_code}"
  exit 0
fi

api_code=""
if command -v jq >/dev/null 2>&1; then
  api_code="$(jq -r '.code // empty' feishu-response.json 2>/dev/null || true)"
else
  api_code="$(grep -oE '"code"[[:space:]]*:[[:space:]]*-?[0-9]+' feishu-response.json | head -n 1 | sed -E 's/.*:[[:space:]]*(-?[0-9]+).*/\1/' || true)"
fi

if [ -n "${api_code}" ] && [ "${api_code}" != "0" ]; then
  fail "Feishu API returned non-zero code: ${api_code}"
  exit 0
fi

echo "::notice::Feishu notification delivered successfully."
