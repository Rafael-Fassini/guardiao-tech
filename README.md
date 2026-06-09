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
- Configure the pilot camera through `.env`:
  - `EDGE_CAMERA_0_SOURCE=webcam://0` for a local USB/notebook camera
  - `EDGE_CAMERA_0_SOURCE=rtsp://usuario:senha@host:554/stream` for an IP camera
  - keep `EDGE_CAMERA_0_ID`, `EDGE_CAMERA_0_SITE_ID` and `EDGE_CAMERA_0_PROTECTED_CASE_ID` aligned with the records created for the pilot case
- Treat `/health` as process liveness only.
- Treat `/ready` as dependency readiness:
  - API checks database reachability, pending migrations and local object storage writability.
  - Web checks authenticated reachability to the operations API.
  - Worker checks recent gallery refresh success and recent camera loop success.

## Camera Connectivity Notes
- `webcam://<indice>` now captures a real frame through OpenCV `VideoCapture`.
- `rtsp://...` now opens the stream directly in the worker and publishes real candidate events to the API.
- If the local OpenCvSharp `videoio` runtime is unavailable on the host, the worker falls back to `ffmpeg` for single-frame capture while keeping OpenCV/ONNX in the detection and embedding stages.
- For notebook/USB cameras during development:
  - prefer running `Guardiao.Worker.Edge` locally with `dotnet run --project src/Guardiao.Worker.Edge`
  - or expose `/dev/video*` manually to the worker container if you need webcam capture inside Docker
- For RTSP/IP cameras, `docker compose` is the simplest path because no host device mapping is required.

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
- The API also runs a periodic eligibility scan that reports artifacts past their configured retention window. This scan reports candidates; it does not delete evidence automatically in this phase.

## Operational Notifications
- Incident creation now triggers backend operational notifications through configurable channels.
- Supported pilot channels:
  - HTTP webhook with optional HMAC signature header
  - SMTP e-mail using a simple local or on-prem relay
- Configure notification delivery through `OperationalNotifications` in `src/Guardiao.Api/appsettings.*` or the `.env` variables consumed by `docker compose`.
- Minimum notification payload fields:
  - `incidentId`
  - `protectedCaseId`
  - `candidateEventId`
  - `createdAtUtc`
  - `status`
  - `hasEvidence`
- Notification delivery failures do not roll back incident creation.
- The API records notification history and exposes it to the operations panel under each incident detail page.

## Pending Incident Escalation
- The API runs a lightweight background scan for incidents still in `PendingReview`.
- When `OperationalNotifications:EscalationWindowMinutes` expires:
  - the incident receives `EscalatedAtUtc`
  - an `incident.escalated` audit entry is written
  - the API sends a second operational notification
- Current pilot limitation:
  - escalation marks the incident as escalated for operators without changing the domain status away from `PendingReview`
  - this preserves the existing human review flow while still surfacing SLA breach state

## Notification Test Tips
- Webhook:
  - point `NOTIFY_WEBHOOK_URL` to a local receiver such as `http://host.docker.internal:9000/guardiao/incidents`
  - inspect `X-Guardiao-Event`, `X-Guardiao-Timestamp` and optional `X-Guardiao-Signature`
- SMTP:
  - for local tests, use a simple SMTP sink such as MailHog or another lab relay
  - configure `NOTIFY_SMTP_HOST`, `NOTIFY_SMTP_PORT`, `NOTIFY_SMTP_SENDER` and `NOTIFY_SMTP_RECIPIENT`
- Failure behavior:
  - the API retries according to `OperationalNotifications:RetryAttempts` and `InitialRetryDelayMilliseconds`
  - failures are visible in `/metrics`, audit entries and incident notification history

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

## Operational Validation
- `bash scripts/post-deploy-smoke.sh`
  - validates API `/health` and `/ready`
  - validates Web `/health` and `/ready`
  - validates Worker `/health`, `/ready` and `/metrics`
  - validates panel technical authentication against the API
  - validates worker candidate-event ingestion against the API
  - validates incident/evidence route reachability
- `bash scripts/validate-deployment-config.sh`
  - checks required deployment artifacts and pilot-safe flags
- `bash scripts/run-verification.sh`
  - runs build plus unit and integration test suites

## Failure Recovery
- If API `/ready` fails:
  - inspect PostgreSQL connectivity, pending migrations and object storage path permissions
- If Web `/ready` fails:
  - inspect `PanelApi` base URL, shared secret and API availability
- If Worker `/ready` fails:
  - inspect gallery refresh counters, camera loop failures and model file presence
- If evidence persistence fails:
  - inspect `${OBJECT_STORAGE_ROOT_PATH}`, disk space and API logs for `evidence_artifact.created` or upload failure entries

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
- API: `/health`, `/ready`, `/metrics`
- Web: `/health`, `/ready`, `/login`
- Worker: `/health`, `/ready`, `/metrics`
