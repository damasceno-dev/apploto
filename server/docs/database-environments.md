# Lotero — Database Environments

## Environment mapping

- `Development` -> local developer machine -> `loto_dev_local`
- `Staging` -> published non-production environment -> `loto_staging`
- `Production` -> live environment -> `loto_prod`

## Appsettings policy

- Tracked in git:
  - `server/server.API/appsettings.json`
  - `server/server.API/appsettings.Development.json`
- Generated outside git:
  - `server/server.API/appsettings.Staging.json`
  - `server/server.API/appsettings.Production.json`

`appsettings.json` should contain only shared non-secret defaults.

`appsettings.Development.json` may contain fixed local-only bootstrap settings for `loto_dev_local`.

`appsettings.Staging.json` and `appsettings.Production.json` must be generated from a non-tracked server-side `.env` on the host before app restart.

Do not commit hosted secrets and do not bake hosted secrets into build artifacts or container images.

## Local workflow

Start PostgreSQL from `infra/`:

```bash
cd infra
docker compose up -d
```

Apply migrations and run the API from `server/`:

```bash
cd server
dotnet ef database update --project server.Infrastructure --startup-project server.API
dotnet run --project server.API
```

## Hosted workflow

1. Keep a non-tracked `.env` on the target host.
2. Set `ASPNETCORE_ENVIRONMENT` to `Staging` or `Production`.
3. Fill the required `SERVER_*` variables from `infra/.env.example`.
4. Generate the hosted appsettings file on the host:

```bash
./infra/scripts/render-server-appsettings.sh /path/to/.env
```

5. Restart the API after the correct `appsettings.{Environment}.json` is generated.

No local workflow may point to the online `loto_prod`.

## Idempotency replay-store cleanup

The replay-first idempotency advisory lock uses a transaction-local PostgreSQL `lock_timeout`
of five seconds by default. This prevents a retry from falling through to Npgsql's longer
command timeout while an in-flight request owns the same key; an expired lock wait returns retryable 409
`IDEMPOTENCY_COORDINATION_BUSY`. Hosted deployments may tune the positive timeout independently:

```text
SERVER_IDEMPOTENCY_COORDINATION_LOCK_TIMEOUT_SECONDS=5
```

The API runs `IdempotencyRequestCleanupHostedService` in every environment. Replay envelopes
remain valid for 24 hours; after `ExpiresAt`, a row is eligible for physical deletion on the
next sweep. The shared cadence is once every 24 hours. Each deletion transaction processes at
most 500 candidates, and that daily run drains successive bounded batches until no expired
candidates remain. With uninterrupted service, normal deletion therefore occurs between 24
and 48 hours after creation.

The candidate query is ordered by `ExpiresAt, Id` and uses
`IX_IdempotencyRequests_ExpiresAt`. Before deletion, the sweep takes the row's normal
endpoint/branch/user/key advisory lock and rechecks `ExpiresAt` in the deletion transaction.
That makes parallel application instances safe and prevents cleanup from deleting a key that
an expired-key reuse just refreshed. Do not run an ad-hoc bulk `DELETE` while the API is live;
it would bypass this coordination.

Hosted settings are generated from these optional `.env` values:

```text
SERVER_IDEMPOTENCY_CLEANUP_INTERVAL_HOURS=24
SERVER_IDEMPOTENCY_CLEANUP_BATCH_SIZE=500
```

Both values must be positive integers; invalid values stop API startup. The service logs event
`IdempotencyRequestCleanupCompleted` when a candidate batch is processed and
`IdempotencyRequestCleanupFailed` when a sweep fails; failures are retried at the next interval.
Operators can inspect backlog and table size without mutating data:

```sql
SELECT
    COUNT(*) AS total_rows,
    COUNT(*) FILTER (WHERE "ExpiresAt" <= now()) AS expired_rows,
    pg_size_pretty(pg_total_relation_size('"IdempotencyRequests"')) AS total_size
FROM "IdempotencyRequests";
```
