# Rollback Checklist

- Freeze incoming pilot changes
- Stop Worker before rollback
- Roll back API/Web image or restore previous publish output
- Restore PostgreSQL backup if schema/data rollback is required
- Restore evidence storage snapshot if object writes must be reversed
- Re-run smoke checks against previous version
- Confirm webhook signature failures and queue pressure returned to baseline
