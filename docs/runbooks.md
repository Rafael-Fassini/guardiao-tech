# Runbooks

## Camera Unavailable
1. Check worker `/health` and `/ready`
2. Inspect `reconnect_count`, `camera_loop_failures_total` and `fps_in`
3. Validate camera source and local network reachability
4. Restart only the worker if the issue is isolated to capture

## Repeated Webhook Signature Failure
1. Check API logs for signature rejection count
2. Confirm the shared secret rotation state on both sides
3. Validate timestamp skew and proxy/body rewriting
4. Keep generic API responses; do not expose secret details externally

## Sync Backlog Growth
1. Inspect reconciliation interval and webhook worker failures
2. Check PostgreSQL and external registry latency
3. Measure backlog growth window before scaling retries or pausing ingest

## Object Storage Unavailable
1. Confirm local volume mount and disk availability
2. Check write permission on `${OBJECT_STORAGE_ROOT_PATH}`
3. Inspect API `/ready` for `objectStorageWritable=false`
4. Pause evidence-ingesting flows if persistence failure repeats

## Database Migration Rollback
1. Stop write traffic to API
2. Restore latest backup
3. Re-run smoke checks on `/ready`, `/health`, `/login`
4. Resume worker only after API and DB are stable

## Worker Latency Spike
1. Inspect `queue_depth`, `frames_dropped`, `match_latency_ms`
2. Reduce camera count or target FPS for the pilot
3. Confirm CPU contention on the edge host

## Queue Pressure Increase
1. Inspect `frames_dropped` and `queue_depth`
2. Reduce ingress or processing FPS mismatch
3. Disable non-critical pilot cameras before raising queue size

## Worker Not Ready
1. Check worker `/ready` payload for stale cameras or missing gallery refresh
2. Inspect `gallery_refresh_failures_total` and `camera_loop_failures_total`
3. Validate worker/API shared secret, API base URL and camera reachability
4. Confirm model files exist at the configured paths

## Web Not Ready
1. Check web `/ready`
2. Validate `PanelApi:BaseUrl` and `PanelApi:SharedSecret`
3. Confirm API `/ready` is healthy before restarting the web host
