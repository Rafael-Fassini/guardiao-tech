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

## Biometric Enrollment Flow
- Open the operations panel and navigate to a case detail page.
- Upload one supported biometric image (`.jpg`, `.jpeg`, `.png`, `.webp`) for the protected person.
- The API validates the image, detects a single face, generates an embedding and stores:
  - the original image in object storage
  - the normalized biometric template in the database
- The worker refreshes its gallery from the API and uses active templates in the main matching path.

## Incident Evidence Flow
- When the worker produces an eligible candidate event, it generates two pilot-oriented visual artifacts whenever possible:
  - a reduced frame snapshot from the detection moment
  - the face crop used for embedding generation
- The worker sends these artifacts with the candidate event payload to the API.
- The API stores the files in object storage and persists `EvidenceArtifact` metadata linked to the created incident.
- The operations panel loads incident evidence from the API only and renders snapshots/crops on the incident detail page.
- Retention currently follows `RetentionMode.CaseBound` for incident evidence created in the pilot.

## Edge Inference Models
- Detection model path:
  - local default: `models/haarcascade_frontalface_default.xml`
  - docker default: `/app/models/haarcascade_frontalface_default.xml`
- Embedding model path:
  - local default: `models/face-embedding.onnx`
  - docker default: `/app/models/face-embedding.onnx`
- The worker uses OpenCV for face detection and ONNX Runtime for embedding generation.
- The API uses the same model family for enrollment-time extraction.
- The repository does not include binary model artifacts; supply them before starting `Guardiao.Api` and `Guardiao.Worker.Edge`.

## Evidence Storage Notes
- Object storage defaults to the configured `ObjectStorage:RootPath`.
- Evidence download is exposed through authenticated API endpoints under `/api/incidents/{incidentId}/evidences`.
- The browser does not access raw storage paths directly.
- Current pilot limitation:
  - evidence is stored only for candidate events that reach the publish path and become associated with an incident
  - evidence rendering in the web panel is optimized for small pilot snapshots/crops, not bulk media review

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
