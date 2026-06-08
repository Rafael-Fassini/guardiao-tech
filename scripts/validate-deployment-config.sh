#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

test -f "${ROOT_DIR}/.env.example"
test -f "${ROOT_DIR}/docker-compose.yml"
test -f "${ROOT_DIR}/src/Guardiao.Api/Dockerfile"
test -f "${ROOT_DIR}/src/Guardiao.Web/Dockerfile"
test -f "${ROOT_DIR}/src/Guardiao.Worker.Edge/Dockerfile"

grep -q "api:" "${ROOT_DIR}/docker-compose.yml"
grep -q "web:" "${ROOT_DIR}/docker-compose.yml"
grep -q "worker:" "${ROOT_DIR}/docker-compose.yml"
grep -q "postgres:" "${ROOT_DIR}/docker-compose.yml"
grep -q "API_ENABLE_DEBUG_HEADER_AUTHENTICATION=false" "${ROOT_DIR}/.env.example"
grep -q "WEB_ENABLE_OPERATIONS_DEMO_LOGIN=false" "${ROOT_DIR}/.env.example"
grep -q "API_MAX_REQUEST_BODY_BYTES=1048576" "${ROOT_DIR}/.env.example"
grep -q "/ready" "${ROOT_DIR}/scripts/post-deploy-smoke.sh"
grep -q "/api/candidate-events" "${ROOT_DIR}/scripts/post-deploy-smoke.sh"
grep -q "/api/incidents" "${ROOT_DIR}/scripts/post-deploy-smoke.sh"

printf '%s\n' "deployment config validation passed"
