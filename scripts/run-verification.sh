#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="${ROOT_DIR}/artifacts/test-results"
mkdir -p "${ARTIFACT_DIR}"

bash "${ROOT_DIR}/scripts/validate-deployment-config.sh"

dotnet build "${ROOT_DIR}/Guardiao.sln" -m:1 -nodeReuse:false

dotnet test "${ROOT_DIR}/tests/Guardiao.UnitTests/Guardiao.UnitTests.csproj" \
  --no-build -m:1 -nodeReuse:false \
  --logger "trx;LogFileName=unit.trx" \
  --results-directory "${ARTIFACT_DIR}"

dotnet test "${ROOT_DIR}/tests/Guardiao.IntegrationTests/Guardiao.IntegrationTests.csproj" \
  --no-build -m:1 -nodeReuse:false \
  --logger "trx;LogFileName=integration.trx" \
  --results-directory "${ARTIFACT_DIR}"

cat > "${ARTIFACT_DIR}/verification-summary.json" <<JSON
{
  "generatedAtUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "artifacts": {
    "unitTrx": "artifacts/test-results/unit.trx",
    "integrationTrx": "artifacts/test-results/integration.trx"
  }
}
JSON
