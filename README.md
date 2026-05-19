# Plataforma Guardiao / AEGIS Tech

Operational platform for protected-case synchronization, edge candidate detection, incident review and pilot rollout support.

## Solution
- `src/Guardiao.Api`: transactional API, webhook intake, security and readiness
- `src/Guardiao.Web`: Blazor operations panel
- `src/Guardiao.Worker.Edge`: edge capture and deterministic inference pipeline
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
- Start infrastructure and apps with `docker compose up -d`
- Run `bash scripts/post-deploy-smoke.sh`

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
