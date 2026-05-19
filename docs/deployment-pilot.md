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
6. Start `web`
7. Start `worker`
8. Run `scripts/post-deploy-smoke.sh`

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
