# Rollout Checklist

- `.env` created from `.env.example`
- Production templates reviewed for API, Web and Worker
- `API_ENABLE_DEBUG_HEADER_AUTHENTICATION=false`
- `WEB_ENABLE_OPERATIONS_DEMO_LOGIN=false`
- Victim registry secrets rotated and validated
- PostgreSQL backup taken before deploy
- `docker compose config` passes
- `/ready`, `/health`, `/login` smoke checks pass
- Worker `/metrics` exposes expected counters and gauges
- Pilot operators have runbook links available
