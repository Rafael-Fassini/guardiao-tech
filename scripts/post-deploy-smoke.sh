#!/usr/bin/env bash
set -euo pipefail

API_URL="${API_URL:-http://127.0.0.1:8080}"
WEB_URL="${WEB_URL:-http://127.0.0.1:8081}"
WORKER_URL="${WORKER_URL:-http://127.0.0.1:${EDGE_HEALTH_PORT:-18081}}"

curl --fail --silent "${API_URL}/health" > /dev/null
curl --fail --silent "${API_URL}/ready" > /dev/null
curl --fail --silent "${WEB_URL}/login" > /dev/null
curl --fail --silent "${WORKER_URL}/health" > /dev/null
curl --fail --silent "${WORKER_URL}/metrics" > /dev/null

printf '%s\n' "post-deploy smoke passed"
