# Plataforma Guardiao / AEGIS Tech

Operational platform for protected-case synchronization, edge candidate detection, incident review and pilot rollout support.

## Solution
- `src/Guardiao.Api`: transactional API, webhook intake, security and readiness
- `src/Guardiao.Web`: Blazor operations panel
- `src/Guardiao.Worker.Edge`: edge capture and OpenCV/ONNX inference pipeline
- `src/Guardiao.Application`: use cases, orchestration and ports
- `src/Guardiao.Domain`: entities, value objects and invariants
- `src/Guardiao.Infrastructure`: persistence, adapters, storage, options and security helpers

## Local Development
```bash
dotnet restore
dotnet build Guardiao.sln -m:1 -nodeReuse:false
dotnet run --project src/Guardiao.Api
dotnet run --project src/Guardiao.Web
dotnet run --project src/Guardiao.Worker.Edge
```

## Pilot Deployment
- Copy `.env.example` to `.env`
- Review `src/*/appsettings.Production.template.json`
- Place edge model files under `models/` as described in `models/README.md`
- Start infrastructure and apps with `docker compose up -d`
- Run `bash scripts/post-deploy-smoke.sh`

## Edge Inference Models
- Detection model path:
  - local default: `models/haarcascade_frontalface_default.xml`
  - docker default: `/app/models/haarcascade_frontalface_default.xml`
- Embedding model path:
  - local default: `models/face-embedding.onnx`
  - docker default: `/app/models/face-embedding.onnx`
- The worker uses OpenCV for face detection and ONNX Runtime for embedding generation.
- The repository does not include binary model artifacts; supply them before starting `Guardiao.Worker.Edge`.

## Operations Artifacts
- Deployment topology: `docs/deployment-pilot.md`
- Dashboards: `docs/dashboard-definitions.md`
- Runbooks: `docs/runbooks.md`
- Backup/restore: `docs/backup-restore.md`
- Rollout checklist: `docs/rollout-checklist.md`
- Rollback checklist: `docs/rollback-checklist.md`

## Verification
- Regression: `bash scripts/run-verification.sh`
- Performance smoke: `bash scripts/run-performance-smoke.sh`
- Deployment config validation: `bash scripts/validate-deployment-config.sh`

## Health Endpoints
- API: `/health`, `/ready`
- Web: `/login`
- Worker: `/health`, `/metrics`
