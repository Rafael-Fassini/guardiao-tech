# Backup and Restore

## PostgreSQL
- Perform regular logical backups with `pg_dump`
- Keep one pre-migration snapshot and one daily pilot snapshot
- Store backup metadata with timestamp and schema version

## Evidence Storage
- Snapshot `${OBJECT_STORAGE_ROOT_PATH}` together with PostgreSQL backup metadata
- Keep restore order aligned with DB backup timestamp

## Restore Order
1. Stop API and Worker
2. Restore PostgreSQL
3. Restore evidence storage volume
4. Start API and wait for `/ready`
5. Start Web and Worker
6. Execute post-deploy smoke script
