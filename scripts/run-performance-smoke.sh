#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ARTIFACT_DIR="${ROOT_DIR}/artifacts/performance"
mkdir -p "${ARTIFACT_DIR}"

dotnet build "${ROOT_DIR}/Guardiao.sln" -m:1 -nodeReuse:false

dotnet test "${ROOT_DIR}/tests/Guardiao.UnitTests/Guardiao.UnitTests.csproj" \
  --no-build -m:1 -nodeReuse:false \
  --filter "Category=Performance" \
  --logger "trx;LogFileName=performance-unit.trx" \
  --results-directory "${ARTIFACT_DIR}"

dotnet test "${ROOT_DIR}/tests/Guardiao.IntegrationTests/Guardiao.IntegrationTests.csproj" \
  --no-build -m:1 -nodeReuse:false \
  --filter "Category=Replay" \
  --logger "trx;LogFileName=replay-integration.trx" \
  --results-directory "${ARTIFACT_DIR}"

cat > "${ARTIFACT_DIR}/performance-summary.json" <<JSON
{
  "generatedAtUtc": "$(date -u +"%Y-%m-%dT%H:%M:%SZ")",
  "artifacts": {
    "performanceTrx": "artifacts/performance/performance-unit.trx",
    "replayTrx": "artifacts/performance/replay-integration.trx"
  }
}
JSON
