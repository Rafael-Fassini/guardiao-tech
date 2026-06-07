#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://127.0.0.1:8080}"
WEB_URL="${WEB_URL:-http://127.0.0.1:8081}"
WORKER_URL="${WORKER_URL:-http://127.0.0.1:${EDGE_HEALTH_PORT:-18081}}"

curl --fail --silent --show-error --max-time 5 "${API_URL}/health" > /dev/null
curl --fail --silent --show-error --max-time 5 "${API_URL}/ready" > /dev/null
curl --fail --silent --show-error --max-time 5 "${WEB_URL}/login" > /dev/null
curl --fail --silent --show-error --max-time 5 "${WORKER_URL}/health" > /dev/null
curl --fail --silent --show-error --max-time 5 "${WORKER_URL}/metrics" > /dev/null

printf '%s\n' "post-deploy smoke passed"
