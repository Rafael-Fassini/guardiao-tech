#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -f "${ROOT_DIR}/.env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source "${ROOT_DIR}/.env"
  set +a
fi

API_URL="${API_URL:-http://127.0.0.1:8080}"
WEB_URL="${WEB_URL:-http://127.0.0.1:8081}"
WORKER_URL="${WORKER_URL:-http://127.0.0.1:${EDGE_HEALTH_PORT:-18081}}"
PANEL_SECRET="${PANEL_API_SHARED_SECRET:-}"
WORKER_SECRET="${WORKER_API_SHARED_SECRET:-}"
WORKER_ID="${WORKER_ID:-edge-smoke-worker}"

if [[ -z "${PANEL_SECRET}" || -z "${WORKER_SECRET}" ]]; then
  printf '%s\n' "PANEL_API_SHARED_SECRET and WORKER_API_SHARED_SECRET must be configured in .env or the shell environment." >&2
  exit 1
fi

check_url() {
  local label="$1"
  local url="$2"
  printf '[check] %s -> %s\n' "${label}" "${url}"
  curl --fail --silent --show-error --max-time 5 "${url}" > /dev/null
}

check_panel_api() {
  local label="$1"
  local path="$2"
  printf '[check] %s -> %s%s\n' "${label}" "${API_URL}" "${path}"
  curl --fail --silent --show-error --max-time 5 \
    -H "X-Panel-User: smoke-operator" \
    -H "X-Panel-Role: admin" \
    -H "X-Panel-Auth: ${PANEL_SECRET}" \
    "${API_URL}${path}" > /dev/null
}

worker_ingest_check() {
  local request_file response_file incident_id

  request_file="$(mktemp)"
  response_file="$(mktemp)"
  cat > "${request_file}" <<JSON
{
  "eventId": "$(cat /proc/sys/kernel/random/uuid)",
  "protectedCaseId": "$(cat /proc/sys/kernel/random/uuid)",
  "siteId": "$(cat /proc/sys/kernel/random/uuid)",
  "cameraId": "$(cat /proc/sys/kernel/random/uuid)",
  "matchScore": 0.91,
  "occurredAtUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "evidences": [
    {
      "artifactType": "Snapshot",
      "fileName": "snapshot.jpg",
      "contentType": "image/jpeg",
      "content": "AQIDBA=="
    }
  ]
}
JSON

  printf '[check] worker candidate-event ingest\n'
  curl --fail --silent --show-error --max-time 5 \
    -H "Content-Type: application/json" \
    -H "X-Worker-Id: ${WORKER_ID}" \
    -H "X-Worker-Auth: ${WORKER_SECRET}" \
    -d @"${request_file}" \
    "${API_URL}/api/candidate-events" > "${response_file}"

  if ! grep -q '"candidateEventId"' "${response_file}"; then
    printf '%s\n' "Candidate event ingest did not return a candidateEventId." >&2
    rm -f "${request_file}" "${response_file}"
    exit 1
  fi

  incident_id="$(grep -oE '"incidentId":"?[0-9a-fA-F-]+' "${response_file}" | head -n 1 | cut -d '"' -f4 || true)"
  rm -f "${request_file}" "${response_file}"

  if [[ -n "${incident_id}" ]]; then
    printf '[check] incident evidences -> %s\n' "${incident_id}"
    curl --fail --silent --show-error --max-time 5 \
      -H "X-Panel-User: smoke-operator" \
      -H "X-Panel-Role: admin" \
      -H "X-Panel-Auth: ${PANEL_SECRET}" \
      "${API_URL}/api/incidents/${incident_id}/evidences" > /dev/null
  else
    printf '[info] ingest returned no incidentId; validating evidence route with a not-found probe\n'
    curl --silent --show-error --max-time 5 \
      -H "X-Panel-User: smoke-operator" \
      -H "X-Panel-Role: admin" \
      -H "X-Panel-Auth: ${PANEL_SECRET}" \
      -o /dev/null \
      -w '%{http_code}' \
      "${API_URL}/api/incidents/00000000-0000-0000-0000-000000000000/evidences" | grep -q '^404$'
  fi
}

check_url "api health" "${API_URL}/health"
check_url "api ready" "${API_URL}/ready"
check_url "web health" "${WEB_URL}/health"
check_url "web ready" "${WEB_URL}/ready"
check_url "worker health" "${WORKER_URL}/health"
check_url "worker ready" "${WORKER_URL}/ready"
check_url "worker metrics" "${WORKER_URL}/metrics"
check_panel_api "operations summary" "/api/operations/summary"
check_panel_api "incident list" "/api/incidents"
worker_ingest_check

printf '%s\n' "post-deploy smoke passed"
