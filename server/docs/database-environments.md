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

For an existing database with branches, first follow the [M95 reviewed mapping and transition procedure](#m95-opening-side-inflow-upgrade). A bare database update cannot supply its required identity map and will fail. For a fresh empty database, apply migrations and run the API from `server/`:

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

## M8 local security operations

This is the reproducible local path for account security (server M8 Phase 8 local subset). It never touches
`loto-postgres-local`, a shared database or a real mailbox, and it proves nothing about a production sender, domain,
Google project or Production bootstrap.

**1. Own a disposable environment.** Run every command from the repository root. Generate the local-only database
password into the shell, never into a file or the command history of another tool, and keep the ports on loopback:

```bash
export LOTO_M8_DB_PASSWORD="$(openssl rand -hex 16)"
docker run -d --rm --name loto-m8-local-db -p 127.0.0.1:55432:5432 \
  -e POSTGRES_DB=loto_dev_m8 -e POSTGRES_PASSWORD="$LOTO_M8_DB_PASSWORD" postgres:16-alpine
docker compose -f infra/docker-compose.m8-mail.yml -p loto-m8-mail up -d   # SMTP 127.0.0.1:11025, UI http://127.0.0.1:18025
export LOTO_M8_CONNECTION="Host=127.0.0.1;Port=55432;Database=loto_dev_m8;Username=postgres;Password=$LOTO_M8_DB_PASSWORD"
dotnet ef database update --project server/server.Infrastructure --startup-project server/server.API --connection "$LOTO_M8_CONNECTION"
```

**2. Inject configuration explicitly.** Every value below comes from the environment of the process that needs it; the
API and the CLI read the same keys (the CLI with the `LOTO_CLI_` prefix). Invalid security options stop the process at
startup: the application URL must be HTTPS or loopback HTTP ending in `/`, and every `AccountSecurity:Login*` value must
lie in the range in the table below.

```bash
export LOTO_CLI_AccountSecurity__ApplicationUrl=http://127.0.0.1:3000/
export LOTO_CLI_SecurityMail__Enabled=true LOTO_CLI_SecurityMail__Host=127.0.0.1
export LOTO_CLI_SecurityMail__Port=11025 LOTO_CLI_SecurityMail__LocalCapture=true
```

**3. First platform operator.** Preview eligibility first; it writes nothing. Repeat with `--execute` to create the
passwordless account and send its single-use setup link to the local capture. The CLI never accepts or prints a password
or capability, and it refuses `Production` and `loto_prod`/`production` database names.

```bash
dotnet run --project server/server.Cli -- bootstrap-platform --environment Development \
  --connection-env LOTO_M8_CONNECTION --name "Operador local" --email operador@example.invalid
```

**4. Activate and sign in through the ordinary API.** Start the API with the same injected values (it keeps the
terminal). In a second shell, open the captured message in the Mailpit UI, copy the value after `#token=` into
`LOTO_M8_SETUP_TOKEN` and choose `LOTO_M8_OPERATOR_PASSWORD` (8 characters to 72 UTF-8 bytes). Activation is the normal
`POST /user/reset-password` (204); then `POST /user/login` returns the identity session (200).

```bash
ConnectionStrings__DefaultConnection="$LOTO_M8_CONNECTION" AccountSecurity__ApplicationUrl=http://127.0.0.1:3000/ \
SecurityMail__Enabled=true SecurityMail__Host=127.0.0.1 SecurityMail__Port=11025 SecurityMail__LocalCapture=true \
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5180 \
dotnet run --project server/server.API --no-launch-profile
```

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://127.0.0.1:5180/user/reset-password \
  -H 'Content-Type: application/json' -d "{\"token\":\"$LOTO_M8_SETUP_TOKEN\",\"newPassword\":\"$LOTO_M8_OPERATOR_PASSWORD\"}"
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://127.0.0.1:5180/user/login \
  -H 'Content-Type: application/json' -d "{\"email\":\"operador@example.invalid\",\"password\":\"$LOTO_M8_OPERATOR_PASSWORD\"}"
```

Operated onboarding (`POST /platform/onboarding` or the CLI `onboard`) and invitations then follow the M8 milestone notes.
`FirstPlatformOperatorCliTest`, `M8LocalAcceptanceJourneyTest` and `OperatedOnboardingHttpTest` replay the same
journeys on disposable containers.

**5. Clean up only what step 1 created.**

```bash
docker rm -f loto-m8-local-db
docker compose -f infra/docker-compose.m8-mail.yml -p loto-m8-mail down
unset LOTO_M8_DB_PASSWORD LOTO_M8_CONNECTION LOTO_M8_SETUP_TOKEN LOTO_M8_OPERATOR_PASSWORD
```

### Login abuse protection

`POST /user/login` passes three budgets. A refusal is always 429 `SECURITY_RATE_LIMITED` with `Retry-After` in whole
seconds, identical for missing, inactive, passwordless and existing accounts, and it happens before the account is read,
BCrypt runs or a token is minted.

| Setting (`AccountSecurity:`) | Default | Allowed | Scope |
|---|---|---|---|
| `LoginRequestsPerMinute` | 30 | 1–1000 | requests per client network per minute, **per API process** (restart resets it; N instances allow N×) |
| `LoginClientFailureLimit` | 5 | 1–100 | failed checks per account from one client network, PostgreSQL (all instances, survives restart) |
| `LoginAccountFailureLimit` | 20 | client limit–1000 | failed checks per account from all networks, PostgreSQL; set it explicitly whenever `LoginClientFailureLimit` exceeds 20, or startup stops |
| `LoginFailureWindowMinutes` | 15 | 1–1440 | window in which failures accumulate |
| `LoginCooldownMinutes` | 15 | 1–1440 | block length once a failure limit is reached |

A failed check counts for a wrong password on `POST /user/login` (401) and for a wrong `CurrentPassword` on the Google
first-link proof, which answers 409 `GOOGLE_LINK_PROOF_REQUIRED`. Validation 400s, coordination 409s
(`SECURITY_COORDINATION_BUSY`), a Google proof request without a password and 429 refusals never count or extend a
block. A successful login clears only that network's budget. A successful password reset or change deletes every
persistent budget of the account (all its `LoginThrottles` rows), so **a locked-out person recovers through Esqueci
minha senha**; the process-local request limit of their network is separate and still runs out its current minute, so
an immediate login may get one more 429 with a `Retry-After` of at most 60 seconds. Do not delete `LoginThrottles`
rows by hand. A sustained attack from more networks than
`LoginAccountFailureLimit / LoginClientFailureLimit` can keep re-imposing the account cooldown: it is never permanent,
and an already-linked Google sign-in is unaffected.

**Client network and proxies.** The client network is the connection's peer address: an IPv4 address, or the /64 of an
IPv6 address. `X-Forwarded-For` is ignored unless the peer is listed in `ForwardedHeaders:KnownProxies` (addresses) or
`ForwardedHeaders:KnownNetworks` (CIDR); then the framework unwraps trusted hops only and the first untrusted address is
the client. An invalid entry stops startup. Behind an unconfigured reverse proxy every client shares the proxy's
partition, so the request limit becomes global and the network budget becomes per account. Never set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED`, which trusts every sender. The forgot-password (60/min) and capability (120/min)
limiters use the same partition; forgot-password still answers its suppressed request with the same empty 202.

**Retention.** `SecurityThrottleCleanupHostedService` deletes expired `LoginThrottles` rows and elapsed
`RecoveryThrottles` rows every `SecurityThrottleCleanup:SweepIntervalMinutes` (15) in batches of
`SecurityThrottleCleanup:BatchSize` (500), both positive integers. It uses `FOR UPDATE SKIP LOCKED`, so it never touches
a row a request is holding, and it is safe with several instances. Rows hold only SHA-256 key hashes, counts and UTC
instants. Inspect without mutating:

```sql
SELECT COUNT(*) AS login_rows,
       COUNT(*) FILTER (WHERE "BlockedUntil" > now()) AS blocked_rows,
       COUNT(*) FILTER (WHERE "ExpiresAt" <= now()) AS awaiting_sweep
FROM "LoginThrottles";
```

`infra/scripts/render-server-appsettings.sh` does not yet render any `AccountSecurity`, `SecurityMail`,
`ForwardedHeaders` or `SecurityThrottleCleanup` value; hosted environments use the defaults until M9 adds them. Real
sender/DNS/TLS, a cross-instance request limiter and the Production first-operator rollout remain M8 Phase 8 launch work.

## M95 opening-side inflow upgrade

`20261020104851_M95OpeningSideInflows` changes data only. Back up the owned database and stop application writers before the reviewed upgrade; do not reset development or staging. A fresh empty database needs no mapping. An existing database **always** needs an explicit identity review for every branch, including inactive branches. Earlier seeds used random IDs and editable names, so neither a matching name, display order, creation time nor a complete-looking catalog proves which product was seeded. Never bulk-select by `Name = 'Tarifa Bolão'` and call that provenance.

Inspect branch/product/category/type IDs (including inactive rows), their origin evidence and close usage read-only. These queries are an inventory, not an automatic mapping:

```sql
SELECT "Id", "Name", "Active" FROM "Branches" ORDER BY "Id";
SELECT "BranchId", "Id", "Name", "DisplayOrder", "Active" FROM "Products" ORDER BY "BranchId", "Id";
SELECT "BranchId", "Id", "Name", "DefaultDirection", "Active" FROM "Categories" ORDER BY "BranchId", "Id";
SELECT c."BranchId", t."Id", t."CategoryId", t."Name", t."SettlementRule",
       t."RequiresTabAccountAndClient", t."SystemRole", t."Active"
FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId";
SELECT c."BranchId", c."Id" AS "CloseId", c."Date", c."Status", c.xmin,
       i."Id" AS "ItemId", i."ProductId", i."Value", i."Active", s."LockDate"
FROM "DailyCloseItems" i JOIN "DailyCloses" c ON c."Id" = i."DailyCloseId"
LEFT JOIN "Settings" s ON s."BranchId" = c."BranchId" AND s."Active";
```

Build a JSON array with exactly one object per branch. Each has `branchId`, `productId`, `receitaCategoryId`, `entradasCategoryId`. IDs must be reviewed UUIDs. Explicit `productId: null` attests that there is no identified old seeded fee product to retire; it is **not** a way to skip an unresolved identity. If provenance cannot distinguish a renamed seed from a custom product, leave the upgrade blocked until the owner supplies that identification. A null category ID authorizes creation of Receita/Entradas; an active name collision aborts. An existing category ID must be active, In, ordinary (no system-counterpart types), and belong to that branch. Renamed categories work by ID; retired or Out categories are never reactivated or repurposed. Select another reviewed active In category or explicitly create one through existing admin commands before the maintenance window.

Optional `receitaTypeName` and `entradasTypeName` select unused editable labels (defaults Tarifa Bolão and Operador). Any same-name type anywhere in the branch, including other categories and active **or retired** types/categories, aborts; its settlement/Tab flags are never rewritten. Review the collision and choose an unused label rather than adopting a custom object. The migration creates two ordinary SameDay types requiring neither Tab nor client. No classification marker, schema, DTO or OpenAPI change is involved; M11 can later classify the reviewed resulting IDs, independent of labels.

Supply the map with `SELECT set_config('loto.m95_seed_map', <JSON parameter>, false)` on the **same PostgreSQL connection** that executes the EF-generated upgrade SQL. For an operational SQL session, use `SET loto.m95_seed_map = '<reviewed JSON array>';` before including the generated script; placeholders must be replaced with reviewed IDs, not copied literally. Keep credentials outside command text. Generate only the intended range:

```bash
dotnet ef migrations script 20261018165353_M8OperatedOnboardingAndGoogleIdentity 20261020104851_M95OpeningSideInflows \
  --project server/server.Infrastructure --startup-project server/server.API --idempotent --output /tmp/loto-m95-upgrade.sql
```

The migration locks relevant tables, validates the map and runs atomically inside the EF migration transaction. `M95_PREFLIGHT` means no changes were committed: correct the identity/category/label issue, then retry the same upgrade. The EF history entry prevents duplicates on subsequent migrate calls and reruns of this **idempotent** script. A plain non-idempotent script rerun would re-enter the body and report a misleading type collision; do not use it for this procedure. Do not execute the raw migration body outside EF's transaction/history wrapper. Tests exercise actual prior-schema upgrade, failure rollback, fresh database and repeated migrate.

**Gap AA boundary.** `M95_GAP_AA` refuses retirement whenever the selected fee product has an active saved item on an active **unlocked** close, regardless of status. Do not omit that product, delete its count, artificially lock the month, or reactivate rows to evade the check. The unresolved DailyClose/Product recovery policy must be decided and implemented under its own scope; this branch's upgrade remains blocked meanwhile. Other independent release slices can be reviewed. An atomic database upgrade cannot partially migrate other branches after one fails.

Locked historical values remain active DailyCloseItems with the same IDs, amounts and close/item versions; only the identified active Product is retired. Previously retired products are not updated again. Official variances, approvals, ledger values and opening chains are untouched. The prior saved fee still enters the next opening under the existing calculator, even when a review omits a prior-only retired product. The first transition from old representation is therefore not certified exact-count parity; assess it explicitly, and do not rewrite locked history to force zero. M10's opening/import work is still pending.

**Already-retired fee products.** Keep the identified retired product ID in `productId`; do not replace it with null. Its active unlocked counts still trigger `M95_GAP_AA`, even though retirement would not change Active again. That branch remains blocked pending the AA policy. Null means the owner established there is no identified old seed, not that its existing retirement makes it safe to skip review.

**Hidden opening amount and first-close review.** Retirement does not remove prior saved fee items from the calculator. If the fee appears only in the prior counted close, the current review omits the entire retired-product row: its opening amount is **invisible** in both the current Close day and approval review. An exact count can therefore preview and submit −100 when the locked prior close contains a fee of 100, while the visible opening rows alone suggest zero. The migration does not detect or acknowledge this transition risk automatically. Completing migration does not authorize operational close-out; item 3.7 remains open.

Before executing the upgrade, run the following read-only inventory with the reviewed map in the same session and `loto.m95_transition_date` set to the intended first new close date (ISO `YYYY-MM-DD`). It selects each Terminal's actual latest prior active counted close, then lists only non-zero active mapped fee items in that opening. Do not search for an older fee-bearing close if a newer counted close exists: that older close is not the calculator's opening. Include inactive Terminals in the inventory and review whether they will resume operation. If Terminals cut over on different dates, rerun for each intended date and retain the relevant account rows.

```sql
WITH reviewed AS (
    SELECT * FROM jsonb_to_recordset(current_setting('loto.m95_seed_map')::jsonb)
      AS m("branchId" uuid, "productId" uuid)
)
SELECT a."BranchId", a."Id" AS "TerminalId", a."Name", a."Active" AS "TerminalActive",
       prior."Id" AS "PriorCloseId", prior."Date", prior."Status",
       i."ProductId", i."Id" AS "FeeItemId", i."Value" AS "HiddenOpeningFee",
       -i."Value" AS "TransitionVarianceContribution"
FROM reviewed m
JOIN "Accounts" a ON a."BranchId" = m."branchId" AND a."Type" = 0
JOIN LATERAL (
    SELECT c.* FROM "DailyCloses" c
    WHERE c."BranchId" = a."BranchId" AND c."AccountId" = a."Id"
      AND c."Active" AND c."ItemsFirstRecordedAt" IS NOT NULL
      AND c."Date" < current_setting('loto.m95_transition_date')::date
    ORDER BY c."Date" DESC, c."CreatedAt" DESC, c."Id" DESC LIMIT 1
) prior ON true
JOIN "DailyCloseItems" i ON i."DailyCloseId" = prior."Id"
    AND i."ProductId" = m."productId" AND i."Active" AND i."Value" <> 0
ORDER BY a."BranchId", a."Id";
```

For each result, the manager/reviewer must open the identified prior close through its ordinary read/review route, confirm the saved fee amount, and reconcile the first new close's preview against the physical count, actual Active ledger and this opening contribution. Record the prior close ID, amount and explanation in ordinary Notes before submission; retain the inventory with the deployment review outside the repository. Compare the persisted official variance after submit with that preview. Never call the difference a cash shortage solely from the visible rows, automatically approve it, alter locked counts, or add a synthetic offsetting transaction. If any difference is unexplained, stop submit/approval and escalate under 3.7; this procedure characterizes the limitation and does not settle the AA recovery policy or promise a new UI field.

**Rollback.** Down fails with `M95_FORWARD_ONLY`: the new editable types may already own transactions, and reactivating a counted fee would restore the bad representation. Recover from a verified pre-upgrade backup only under a separately authorized restoration plan, or use a reviewed forward correction. This batch neither performs nor authorizes a restore.

**Operator use (working Q3/Q9/Q5, pending the named shop answers).** Enter actual Tarifa Bolão and Operador amounts on the Terminal/day through ordinary transactions before submit; no amount or rate is generated. Count the physical cash, not a separate fee/Operador product. Ordinary cash product sales use count-only (stock down/cash up, no extra Out row). Fiado still uses the existing counted-stock flag and linked Terminal leg; non-cash repayments add no cash leg. Revisit only affected consumers if the shop answer or a real M10 dry run contradicts these defaults.
