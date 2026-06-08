# Pilot Deployment

## Topology
- `postgres`, `redis` and local evidence storage run through `docker-compose`
- `Guardiao.Api` and `Guardiao.Web` run on the pilot host
- `Guardiao.Worker.Edge` runs on the pilot edge notebook or host with camera access
- API and Web share the same PostgreSQL instance for the pilot

## Startup Ordering
1. Provision `.env` from `.env.example`
2. Start infrastructure with `docker compose up -d postgres redis`
3. Apply database migration if needed
4. Start `api`
5. Wait for `GET /ready`
6. Confirm API readiness details:
   - database reachable
   - pending migrations applied
   - object storage writable
7. Start `web`
8. Wait for `GET /ready` on `web`
9. Start `worker`
10. Wait for `GET /ready` on `worker`
11. Run `scripts/post-deploy-smoke.sh`

## Container Images
- `src/Guardiao.Api/Dockerfile`
- `src/Guardiao.Web/Dockerfile`
- `src/Guardiao.Worker.Edge/Dockerfile`

## Pilot Ports
- API: `8080`
- Web: `8081`
- Worker health: `${EDGE_HEALTH_PORT}`
- PostgreSQL: `${DB_PORT}`
- Redis: `6379`

## Readiness Meaning
- API `/ready`
  - PostgreSQL reachable
  - no pending migrations
  - local object storage path writable
- Web `/ready`
  - panel can authenticate against the API using the configured shared secret
- Worker `/ready`
  - at least one enabled camera configured
  - recent gallery refresh success
  - recent successful camera loop activity
