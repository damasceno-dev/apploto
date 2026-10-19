# Server M8 — SV10 + SV11 Phases 1–5 delivery

## Baseline, ownership and selected acceptance

2026-10-18: checkout `/Users/fernandodamasceno/Coding/apploto/Project`, `main`, initial HEAD `d095262488523d949032011ec67e6638013acf5f`, initial `git status --short` empty. That commit includes accepted S06 round-3 fixes and the older Claude design candidate; no visual lock follows from inclusion. Stage 0 closes S06 1.6/5.2 using the actual local clone evidence in Shared's existing delivery note. This delivery owns server code/migrations/tests, local-only SMTP capture, canonical docs and semantic product/screens reconciliation, and generated Shared input/raw/query clients. Root dependency/lockfile changes only if technically necessary; none planned. No staging/commit/push/deploy or branch/worktree mutation.

This opening section records the historical Phases 1–5 delivery at `d095262`; its then-future Phase 6/7 statements describe that baseline, not the current server. Selected items were M8 1.1–1.4, 2.1–2.5, 3.1–3.5, 4.1–4.4 and 5.1–5.5 in `milestones.md`. The current Phase 6/7 candidate and its remaining acceptance gates are recorded below. Independent technical and semantic acceptance of both M8 batches was recorded by the 2026-10-18 repeat review; Phase 8/provider/launch acceptance remains open.

## Phase 1 technical contract (selected before editing runtime code)

All payloads are Communication DTOs with JSON camelCase and named enums. Errors use the existing `{ errorMessages: string[] }` resource envelope, pt-BR. Identity-only means `TokenAuthenticate`; branch scopes use existing token filters and fresh database role/membership checks. No actor, branch or role from caller data substitutes for authenticated authorization. No financial logic changes.

| Route | Auth | Request / response / statuses |
|---|---|---|
| `POST /user/login` | anonymous, existing | Existing request; `ResponseUserLoginJson` gains required `UserId: Guid`; 200/400/401 plus coordination 409 |
| `GET /user/profile` | identity | `ResponseUserProfileJson { UserId, Name, Email }`; 200/401/403 |
| `PUT /user/profile` | identity | `RequestUpdateProfileJson { Name }`; same response; 200/400/401/403/409 |
| `POST /user/change-password` | identity | `RequestChangePasswordJson { CurrentPassword, NewPassword }`; 204; 400 validation, 401 wrong current password, 403 tier, 409 busy |
| `POST /user/logout` | identity | `RequestLogoutJson { RefreshToken }`; 204 idempotent for absent/foreign session; revoke only supplied matching session; 400/401/403/409 |
| `POST /user/forgot-password` | anonymous | `RequestForgotPasswordJson { Email }`; **202 with no body in every valid existing/missing/inactive/throttled/delivery-failed case**; 400 malformed input, 409 technical coordination busy. No delivery promise. |
| `POST /user/reset-password` | anonymous capability | `RequestResetPasswordJson { Token, NewPassword }`; 204; 400 input, 409 invalid/expired/used/missing/inactive token or busy; never echoes capability |
| `POST /operator-link-request` | branch Member | No body; 200 `ResponseOperatorLinkRequestJson`; repeated eligible pending request returns same id; 403 role, 409 already usable setup/busy |
| `GET /operator-link-request/self` | branch Member | `ResponseOwnOperatorLinkRequestJson { Request? }`, containing the latest own request or null (200); 403 managerial role |
| `GET /operator-link-request` | branch Manager/Admin | `RequestListOperatorLinkRequestsJson { Status?, Page = 1, PageSize = 50 }`; bounded 1–100; `ResponseListOperatorLinkRequestsJson { Items, TotalCount, Page, PageSize }`; 200/400/401/403 |
| `POST /operator-link-request/{id}/resolve` | branch Manager/Admin | No body; resolves only when active membership and an active linked operator exist; returns actual `OperatorId`, `HasUsableAccount`, status; 404 foreign/missing/ineligible person, 409 setup still missing/stale/busy; repeated already-resolved request returns current state. Use existing `POST /operator`, `PUT /operator/{id}` then operator-account assignment. No implicit grant. |
| `GET /report/manager-queue` | existing Manager/Admin | Add `PendingOperatorLinkRequestCount`; date-independent active eligible pending requests, no changes to financial totals/bounds/order. Bounded list above feeds Queue/Operators. |
| `POST /invitation` | branch Manager/Admin | `RequestIssueInvitationJson { Email, Role, OperatorId? }`; 201 `ResponseInvitationJson`; Manager cannot grant Admin; only Member may pre-link; 400/403/404/409 |
| `GET /invitation` | branch Manager/Admin | `RequestListInvitationsJson { Status?, Page = 1, PageSize = 50 }`; bounded response; effective expiration projected at read; no token/hash exposure; Manager cannot see Admin invites |
| `POST /invitation/{id}/reissue` | branch Manager/Admin | Optional `RequestReissueInvitationJson { OperatorId? }`: omitted/null body preserves selection; an object replaces it (null field/`{}` clears); Member-only non-null selection. 200 resource, new capability/expiry replaces old even after decline/expiry; accepted invitation cannot reissue; rechecks role and operator eligibility; 400 shape/role, 404 hidden/missing, 409 accepted/conflict/busy |
| `POST /invitation/preview` | anonymous capability | `RequestInvitationTokenJson { Token }`; 200 `ResponseInvitationPreviewJson { BranchName, Role, ExpiresAt, OperatorName? }`, **no target email/user details**; 409 invalid/expired/terminal capability |
| `POST /invitation/decline` | anonymous capability | Same token request; 204; repeated declined capability idempotent, accepted/expired/replaced invalid 409 |
| `POST /invitation/accept` | identity + capability | Same token request; 200 `ResponseAcceptInvitationJson { BranchId, BranchUserId, Role, OperatorId? }`; authenticated active account's normalized email must match; 403 mismatch; 409 duplicate active membership/stale setup/token |
| `POST /invitation/register` | anonymous capability | `RequestRegisterInvitationJson { Token, Name, Password }` (no email/branch/role input); 201 `ResponseUserLoginJson`; refuses existing account, including inactive account, with 409; registration+membership+optional link+consume+refresh one commit |

DTO list statuses are named `Pending`, `Resolved` for requests, and `Pending`, `Accepted`, `Declined`, `Expired` for invitations (expiry is effective state from UTC, not a background scheduler). Request resolution returns live operator/account facts, not a fabricated readiness promise. Pending request reads exclude removed/inactive people; history is retained. Invitation lists retain history, ordered CreatedAt descending then Id, paged after filters; one pending target per branch, expired rows retired before reissue/new issuance. Active membership conflicts instead of changing its role; inactive unique membership reactivates with the invitation role. Pre-link does not edit account assignments or employee history.

## Persistence, concurrency, privacy and delivery

- Add `PasswordResetToken`, `RecoveryThrottle`, `OperatorLinkRequest`, `Invitation`; restricted relationships, unique capability hashes and filtered pending `(BranchId, UserId)` / `(BranchId, Email)` indexes. No raw capability column/outbox. Email canonicalization trims Unicode whitespace and uses the same PostgreSQL lowercase rules as the owning database; migration preflights collisions before normalizing existing Users and preserving uniqueness. No inferred invitation/request backfill.
- Use 32 cryptographically random bytes encoded Base64Url, persist uppercase SHA-256 only. Reset lifetime 30 minutes; invitation 72 hours; per-normalized-email recovery cooldown 60 seconds, configurable with bounded startup validation. All expiry uses injected `TimeProvider` UTC (`now >= ExpiresAt` invalid), never branch/device time. Newly requested recovery invalidates earlier live tokens. Successful password change/reset invalidates outstanding recovery tokens and **all** refresh sessions.
- PostgreSQL transaction-scoped advisory coordination uses SHA-256-derived namespaced 64-bit keys, sorted before acquisition; bounded five-second lock timeout -> resource 409. Account/email key serializes login, register, renewal, profile/password/logout/reset. Branch key serializes membership mutation, operator mutations, request creation/resolution and invitations. Combined invite operations acquire branch+email keys in canonical order and reread mutable data. Authorization is rechecked under branch coordination to avoid cached actor role after a queued demotion/removal. `IUnitOfWork` remains SaveChanges owner; coordination Complete commits transaction; disposal rolls back. Re-read refresh/capability **after** acquisition. No arbitrary single-session revocation assumption.
- Mail boundary is a domain port; pinned MailKit SMTP adapter uses standard SMTP, MIME text and finite timeout, no protocol logging. Default mode disabled; missing SMTP configuration is not fake success. Local capture permits plaintext SMTP **only for loopback**; remote SMTP requires STARTTLS and certificate validation. Application URL is configured, absolute HTTPS (HTTP only loopback), no userinfo/query/fragment. Link token lives in fragment for future client handoff (not server URL query); API consumes body token. Future UI must apply no-referrer and erase capability after consumption; no UI is delivered here.
- Minimal delivery policy: persist hashed capability then send once **after database commit**; no automatic retry or durable plaintext storage. Recovery uses a bounded 100-item volatile queue so SMTP timing stays off the enumeration-safe request path; invitations send synchronously after commit and persist the bounded outcome. Invitation response includes truthful delivery status (`Sent`/`Failed`), meaning SMTP acceptance only, not inbox arrival. Forgotten-password stays enumeration-safe regardless of delivery outcome. Failure leaves hash unusable without capability, retry after cooldown/reissue replaces it; metrics contain only bounded event outcome, never token/email/body/provider exception text. A failed commit sends nothing. A post-commit crash can lose delivery; explicit reissue/request is recovery. Provider acceptance, sender/DNS/credentials, abuse tuning and operator monitoring stay Phase 8.
- OWASP [Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html) informs single-use, entropy, equal response, trusted link and throttling. Microsoft's [.NET SMTP guidance](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient?view=net-10.0) recommends MailKit; [MailKit 4.17.0](https://www.nuget.org/packages/MailKit/4.17.0) provides compatible async SMTP/MIME APIs for .NET 10. No enterprise identity replacement.

## Historical execution checkpoint (superseded by final evidence below)

The first focused run passed 65/66; its multi-key invitation race harness held the wrong first key. The harness was corrected to acquire the first sorted key. A normalization assertion in the first UseCase run was corrected to the canonical identity contract. Both issues were retested and the complete final suites below passed. These early counts are retained as history, not current acceptance evidence.

SMTP image `axllent/mailpit:v1.31.5` was pulled with a disposable empty Docker configuration after a task-owned credential helper blocked; user Docker configuration/credentials were not edited. Image digest: `sha256:94f6b70c1436defae6799d9ac7ff0eeff9540465b842026d5cdb018f1c0234ea`.

MailKit 4.17.0 introduced PKCS 10.0.0; restore reported [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx). This scope pins the patched transitive dependency `System.Security.Cryptography.Pkcs` 10.0.13. Root JS dependencies and the shared lockfile are unchanged.

## Local backend acceptance journey

The executable journey is `server/tests/WebApi.Test/AccountSecurity/M8LocalAcceptanceJourneyTest.cs`:

```bash
DOCKER_CONFIG=/private/tmp/loto-m8-public-dockerconfig dotnet test server/tests/WebApi.Test --no-build --filter FullyQualifiedName~M8LocalAcceptanceJourneyTest -v minimal
```

The Docker config path is a disposable `config.json` containing only `{"auths":{}}` for public image pulls; it avoids the blocked desktop credential helper without reading/copying/modifying user credentials. It is not an application secret/provider configuration. Tests own random-port loopback PostgreSQL/Mailpit resources and dispose them. The existing `loto-postgres-local`/`loto_dev_local` service is never reset or used as a disposable test target.

1. Fixture creates a unique `M8LocalJourney` branch/Admin in the owned PostgreSQL container. A unique `journey-<guid>@example.invalid` fake account uses the retained local `POST /user/register` bootstrap with test password `Password123`, then real `POST /user/login` returns its `UserId` and identity/refresh pair. The fixture is local evidence, not a new production bootstrap endpoint.
2. Real `PUT /user/profile` changes the name. Confirmed `POST /user/change-password` changes the fake password to `ChangedPassword123`; old refresh renewal is immediately 401, while fresh login works. `POST /user/logout` revokes that contracted refresh session.
3. `POST /user/forgot-password` returns empty 202. The actual SMTP adapter delivers the reset link to the isolated Mailpit capture, using the configured trusted application base URL (future app page; this batch supplies no frontend). The test reads the captured fragment capability, consumes it through `POST /user/reset-password` with `ResetPassword123`, and logs in through the real API. No raw capability is written into this report or application logs.
4. The existing authorized `POST /branch/users` fixture adds that account as Member (legacy bootstrap explicitly remains until Phase 6); identity `POST /branch/session` enters the branch. Member creates a link request. Admin uses the existing operator create API and resolves the request: `HasUsableAccount=false` is the immediate observation. Only a separate real operator-account assignment makes the Terminal visible in Member self-context.
5. Admin creates an unlinked operator, issues a Member invitation with its id, and receives truthful SMTP `Sent`. A new fake recipient's captured capability enters `POST /invitation/register`: user, membership, pre-link and consumption commit together. Its branch session/self-context shows the preselected operator and no assigned account — no implicit grant.
6. A different existing password account receives another captured invitation, logs in normally and accepts under the matching identity; the branch membership has the invited Manager role. Its existing password is retained.

Safe reset boundary: dispose only the test-owned PostgreSQL/Mailpit containers. Do not truncate shared databases, delete another Compose project's mail, kill user processes or add a public reset/seed/admin-grant route. For optional standalone local capture, `infra/docker-compose.m8-mail.yml` is an isolated Compose file with loopback bindings, `/dev/shm` mail storage, no relay and no host mail volume. Set `LOTO_M8_SMTP_PORT=0` and `LOTO_M8_MAIL_UI_PORT=0` to allocate free host ports, then inspect only that project's mapped ports. The application opt-in is `SecurityMail__Enabled=true`, `SecurityMail__LocalCapture=true`, `SecurityMail__Host=127.0.0.1`, the mapped `SecurityMail__Port`, `SecurityMail__From=no-reply@loto.invalid`, and a trusted `AccountSecurity__ApplicationUrl`; all defaults/limits are in Phase 1 above. A real provider/sender is not configured or proved by this journey.

### Final technical reconciliation before independent review

- The new operations are split into operation-owned use cases and validators per `server/AGENTS.md`; shared password/capability/request-state helpers remain application services, and every service/use case is explicitly registered. Controllers retain their published routes/filters/DTO metadata.
- A real .NET 10 probe retained U+0130 under invariant lowercase while the isolated PostgreSQL probe mapped it to U+0069. PostgreSQL default `btrim` also retained NBSP/narrow NBSP. Runtime canonicalization therefore uses `IUsersRepository.NormalizeEmail`: .NET Unicode trim followed by a parameterized scalar database `lower`, with exact canonical lookup afterward. Login/register/forgot/issue, actor lock keys, throttle hashes and acceptance use that same persisted canonical identity. Migration uses an explicit Unicode trim set and the same database lowercase before collision preflight/update. Real HTTP tests pin Unicode login/duplicate/recovery/invitation parity and migration backfill; no shop data was inspected or guessed. Database locale/collation must remain stable across deployments. Primary references: [PostgreSQL lower/btrim](https://www.postgresql.org/docs/16/functions-string.html), [EF scalar SQL queries](https://learn.microsoft.com/en-us/ef/core/querying/sql-queries#querying-scalar-non-entity-types).
- Reissue can recover an unavailable/retired preselection: no body (or a JSON null body) preserves it; a DTO object replaces it, with `OperatorId=null`/`{}` clearing it or a valid eligible id replacing it. Email/branch/role remain immutable. Shape/role violations are 400; eligibility conflict 409; every successful reissue replaces the capability hash. This prevents a stale operator from forcing a 72-hour wait while preserving atomic acceptance and no implicit assignment.
- New registration prepares the pure identity token before commit; generation failure rolls back user/membership/pre-link/refresh/consume. SMTP remains after commit. Invitation list filters and effective statuses use one captured server UTC instant.
- The intermediate focused run passed **82/82**, including local SMTP, Unicode/reissue/migration cases, architecture and H4. The final complete WebApi run below also includes the later five-second coordination-timeout regression and reviewed optional-body snapshot.


## Final delivery and validation — 2026-10-18

Implementation delivered for **1.1–1.3, 2.1–2.5, 3.1–3.5, 4.1–4.4 and 5.1–5.4**. At that first delivery, item **1.4** and composite **5.5** remained unchecked pending independent review and the actual repository documentary gate. The later accepted repeat review and v54 doc-sync close-out are recorded below. All other selected implementation items have current evidence. Stage 0 S06 **1.6/5.2** are checked on the previously committed actual source clone, with historical review evidence preserved in Shared's existing note. Phases 6–8 and whole M8 completion remain unchecked.

### Current command results

Run from repository root unless another directory is stated. Docker-dependent commands used `DOCKER_CONFIG=/private/tmp/loto-m8-public-dockerconfig` (disposable public-pull configuration). No production credentials, mail recipients or database were used. All counts below are current completed executions, not predecessor counts.

| Exact command / recipe | Result and local evidence |
|---|---|
| `dotnet build server/server.sln --no-restore --no-incremental -v quiet` | Exit 0, 0 errors; six existing EF1002 warnings in older migration tests. `/private/tmp/loto-m8-build-final.log` |
| `dotnet test server/tests/Validators.Test --no-build -v minimal` | Exit 0, **344 passed**, zero failed/skipped. `/private/tmp/loto-m8-validators-final.log` |
| `dotnet test server/tests/UseCases.Test -v minimal` | Exit 0, **1148 passed**, zero failed/skipped. `/private/tmp/loto-m8-usecases-final.log` |
| `DOCKER_CONFIG=/private/tmp/loto-m8-public-dockerconfig dotnet test server/tests/WebApi.Test --no-build -v minimal` | Exit 0, **1215 passed**, zero failed/skipped, 4m01s. Includes architecture, H4 real credential-tier probes, numeric/enum/nullability/header/contract snapshots and all new PostgreSQL/SMTP cases. `/private/tmp/loto-m8-webapi-final.log` |
| In `server`: `dotnet ef migrations has-pending-model-changes --project server.Infrastructure --startup-project server.API --no-build` | Exit 0, no pending model changes. `/private/tmp/loto-m8-model-check.log` |
| `pnpm install --frozen-lockfile` | Exit 0; shared lockfile unchanged. |
| `pnpm -r typecheck`; `pnpm -r lint`; `pnpm format:check`; `pnpm -r test` | All exit 0, **101 Shared tests**; server-safe import graph: 243 modules, no hook imports/retries. `/private/tmp/loto-m8-shared-final.log` |
| `pnpm --filter @loto/api generate:check`; `pnpm --dir shared/api generate:check` | Both exit 0; all **255** generated files match fresh generation. Same transcript. |
| `pnpm --filter @loto/api export:check`; `pnpm --dir shared/api export:check` | Both exit 0; independent non-incremental opt-in Release exports match the committed candidate input exactly using inert configuration. Same transcript. |
| `node /private/tmp/loto-m8-default-export-check.mjs` | Exit 0; removes every `isServerConfiguration` environment key, then runs `dotnet build server/server.API/server.API.csproj -c Release --no-restore --no-incremental -nologo -v:quiet -p:OpenApiDocumentsDirectory=<fresh-temp-directory>` **without** the opt-in property; zero JSON documents. Build log `/private/tmp/loto-m8-default-export-build.log`. |
| Extract both canonical HTML files' inline `<script>` blocks to disposable `.js` files; `node --check <each-file>` | Both documents pass after final narrative edits. |
| `bash docs/check-loto-doc-sync.sh` | **Expected exit 1**: first diagnostic product v52 versus canonical v53; screens also deliberately v52. `/private/tmp/loto-m8-doc-sync-final.log` |
| `bash docs/test-loto-doc-sync.sh` | **Expected exit 1 at its current-doc baseline**, for the same pending marker gate. `/private/tmp/loto-m8-doc-regression-actual-final.log` |
| Copy only the two checker scripts, canonical three-file group and product/screens into `/private/tmp/loto-m8-doc-regression-final`; align markers **in that disposable copy only**, then `bash /private/tmp/loto-m8-doc-regression-final/docs/test-loto-doc-sync.sh` | Exit 0, **34/34 checker scenarios**. `/private/tmp/loto-m8-doc-regression-final.log`. This proves checker behavior, not accepted source markers or semantic approval. Actual files and checker code were not weakened. |
| `git diff --check`; `git diff --cached --name-only`; `git branch --show-current`; `git rev-parse HEAD` | Whitespace clean, index empty, branch still `main`, HEAD still `d095262488523d949032011ec67e6638013acf5f`. |

The final UseCase run rebuilt its test project after three mutation-driven cases were added; runtime code did not change in that final test-only step. The final Shared pipeline was repeated once with durable output after an earlier successful process's long output could not be recovered from the tool session. Green runtime suites were not otherwise repeated without a changed input or identified concern. The final contract snapshot was updated only after reviewing the intended additions and optional reissue-body delta; no old path/schema removal or weakened numeric/auth gate was accepted.

### Targeted mutation verification

Following `server/AGENTS.md`, installed **Stryker.NET 5.0.0** only in disposable `/private/tmp/loto-m8-stryker-tools` with `dotnet tool install dotnet-stryker --tool-path /private/tmp/loto-m8-stryker-tools`; no global tool, project package or lockfile changed. The tool's [official configuration](https://stryker-mutator.io/docs/stryker-net/configuration/) and [runtime migration guidance](https://stryker-mutator.io/docs/stryker-net/migration-guide/) informed the targeted run. The installed CLI rejected the documented `--test-case-filter` option, so the final run uses the complete UseCase test project. Initial relative project discovery also failed; explicit absolute project/test paths resolved discovery. Neither setup failure changed runtime code or test gates.

From `server/server.Application`, final command:

```bash
/private/tmp/loto-m8-stryker-tools/dotnet-stryker \
  --project /Users/fernandodamasceno/Coding/apploto/Project/server/server.Application/server.Application.csproj \
  --test-project /Users/fernandodamasceno/Coding/apploto/Project/server/tests/UseCases.Test/UseCases.Test.csproj \
  --mutate 'Services/Security/AccountPasswordUpdater.cs' \
  --mutate 'UseCases/Users/ChangePassword/ChangeUserPasswordUseCase.cs' \
  --mutate 'UseCases/Users/Logout/LogoutUserUseCase.cs' \
  --reporter cleartext --reporter json \
  --output /private/tmp/loto-m8-stryker-final-output
```

**Exit 0; all 13 selected mutants killed, 100.00%, no selected survivors/no-coverage/compile errors.** `/private/tmp/loto-m8-stryker-final.log`, JSON `/private/tmp/loto-m8-stryker-final-output/reports/mutation-report.json`. This is a scoped mutation result, not a whole-application score: the tool initially enumerates the larger project and skips other files under the explicit mutation filter; safe-mode compile diagnostics in unrelated existing OperatorAccounts/TimeEntries methods are outside the selected files. The first targeted run killed 7/13 and exposed missing unit assertions for validation, fresh-account checking and transaction completion. Strengthened the two success tests and added three meaningful rejection cases; the final complete UseCase suite passed **1148/1148** and the repeated targeted mutations all died. Existing real HTTP/rollback/race tests independently cover persistence and authorization. Runtime source was unchanged in this final test-only step. A complete ordinary solution build follows mutation execution to restore unmutated artifacts.

### Real scenario evidence and migration impact

- `AccountSecurityHttpTest`, `AccountSecurityRollbackHttpTest` and `RecoveryRateLimitHttpTest`: safe profile DTO, current password requirement, all-session revocation, natural identity expiry, stable-session logout, both ordering variants of renewal/change/reset/logout races, simultaneous single-use reset, exact UTC boundary, invalid/used/inactive/missing targets, equal empty 202 responses, persisted target/IP throttles, trusted-host isolation, disabled/failed delivery, and trigger-induced rollback including a waiting renewal. A held account key produces the dedicated 409 after the bounded five-second timeout without mutation.
- `MembershipSecurityHttpTest`: dedicated self error, real mutually queued Admin removals/demotions revalidate the actor and preserve one active Admin; unlink retains employee/operator history and account grants, removed memberships invalidate branch authorization. Existing cross-tenant/role suites remain green.
- `OperatorLinkRequestHttpTest`: unique pending index also rejects bypassing application coordination, concurrent Members/managers, eligible active Terminal assignments only, date-independent pending queue count, Member managerial-data denial, removed/inactive/foreign records, deterministic repeats and resolution without implicit account grant.
- `InvitationHttpTest`, `InvitationEdgeCasesHttpTest`, `InvitationRollbackHttpTest`: correct-identity acceptance, no password takeover/inactive bypass, immutable target/branch/role, Manager cannot invite Admin, safe preview, atomic new registration or permitted membership reactivation/pre-link/consume/refresh, competing accept/operator races, retired/changed/existing links, replacement capability invalidation, decline/expiry/replay, clear/replace stale preselection, failed commit and token-generation rollback, truthful post-commit send failure/reissue.
- `EmailNormalizationHttpTest` and `M8SecurityMigrationTest`: actual .NET/PostgreSQL Unicode-case/whitespace parity, canonical login/duplicate/recovery/invitation identity, upgrade from pre-M8 schema, normalized existing emails, stable session backfill, hash-only capability storage and collision preflight rollback. Fresh test databases apply the complete migration chain. The upgrade creates no inferred invitations or requests.
- Migration `20261018040013_M8AccountSecurityAndInvitations`: four new tables and restricted FKs; capability/hash and filtered pending uniqueness indexes; canonical email collision preflight aborts with `M8_EMAIL_NORMALIZATION_COLLISION`; legacy refresh values longer than 128 abort before narrowing. Existing refresh rows backfill `SessionId=Id`, remove the temporary default, retain historical rotated values inactive, and use user/session lookup plus unique value. All schema/data changes are transactional. **Down removes the M8 schema/session changes but cannot undo email normalization.** A collision requires an explicit later data decision; migration never chooses an account or resets a database.
- `LocalSmtpDeliveryTest` uses the real MailKit adapter against isolated loopback Mailpit separately from fake-sender tests; it covers real MIME/recipient/content acceptance, disabled/unreachable paths, STARTTLS requirements and rejection of non-loopback plaintext. `M8LocalAcceptanceJourneyTest` exercises real APIs → PostgreSQL → captured mail for profile/password/logout/reset, Member request → manager setup/resolve, and both new/existing invitation acceptance paths. The journey above gives fixture and immediate observations. No real mail was sent.

### Contract source and reproducibility

The historical Phases 1–5 candidate began at `d095262488523d949032011ec67e6638013acf5f` and subsequently landed in `9ea777cdb5376f5f6396de65bfba39db41ae91ee`; that commit also carried separate M9.5 evidence, not independent M8 acceptance. Its canonical revision was **v53**. Its OpenAPI input SHA-256 was `fcfe94155a72246a11c0a103c760524f9f80dd1164f61c75351bef4455de15b9` (**87 paths, 118 operations, 186 schemas**; 8 anonymous, 8 identity and 102 branch operations). Login carried required authenticated `UserId`; request self-status had an explicit nullable wrapper; reissue had its intentional optional DTO body. Existing transport numeric/enum/nullability/required header guarantees were pinned.

Generated tree SHA-256 at that delivery: `a401811614fe34dbb68ff9da383c929e2fd531b46985aa587f9d8c844c43b4e8` over **255 files**. Recipe: sort **relative path strings** under `shared/api/src/generated/` ordinally, emit UTF-8 `<file-sha256>  <relative-path>\n` for each file, then SHA-256 the complete bytes. Actual export/generation commands produced every generated file; no DTO/client was hand-edited. Header boundary had 110 separated merges and zero lossy merges. The root/query split, injected token provider, no automatic retry, typed errors and abort behavior were green. The original delivery did not claim a fresh clone of its then-uncommitted candidate; the actual-commit fresh clone belongs to S06 Stage 0 only.

### Phases 1–5 handoff recorded before Phase 6–7 work

1. Claude independently inspects the checkout/diff, implementation and semantic v53 reconciliation. **1.4 remains open.** Fix findings and repeat review before acceptance; no review transcript/approval is fabricated.
2. Only after that acceptance, update both product/screens markers from v52 to v53, run the **actual** `bash docs/check-loto-doc-sync.sh` and `bash docs/test-loto-doc-sync.sh` to green, then record accepted 1.4 and complete 5.5 evidence in a small documentary close-out diff for review. Current mismatch is exactly the two design markers; the checker reports product first. No visual approval/lock follows from the older committed Claude candidate.
3. Phases 6–8 remain future scope: coherent platform/operated onboarding/shared CLI and closure of actual public register/create/add-member paths; verified Google sign-in/linking; genuine production sender/domain/DNS/credentials/delivery, abuse/monitoring tuning and complete M8 security/launch acceptance. **Gap C and full M8/launch readiness remain open.** Design 0.4.6 uncataloged screens remain uncataloged. N/U and V's shipped backend request/pre-link surfaces are reconciled; UI work comes later.
4. Delivery intentionally has no durable plaintext outbox or automatic retry. The bounded volatile recovery queue can lose mail on crash or overflow; a later request after cooldown/reissue replaces the capability. SMTP `Sent` means observed server acceptance, not inbox arrival. `Failed` means acceptance was not confirmed; an ambiguous transport failure can occur after the SMTP server accepted a message. No fake/local delivery proves production sender integration.
5. Email canonicalization depends on stable owning PostgreSQL locale/collation, and cannot be reversed by migration Down. Account/branch advisory coordination is a backend contract for writers; new writers must preserve it. Refresh history remains inactive to preserve stable-session logout semantics; retention/cleanup is later operational work, with no arbitrary deletion introduced here. Existing `Scalar.AspNetCore Version="*"` restore reproducibility risk is unchanged.
6. No web/mobile/Hermes/Next UI acceptance, visual lock, production-provider acceptance or full-M8 completion is claimed. No platform tool restriction was bypassed; no staging, commit, merge, push, history rewrite, worktree/branch mutation, publishing or deployment occurred. The existing user PostgreSQL service was not reset. Root tooling/lockfile and Domain framework-dependency boundaries remain unchanged.

Dependent ready work: independent combined review of this complete batch, then documentary marker/checklist close-out; Phase 6 planning/implementation can use the shipped account/invitation/mail boundaries for operated onboarding. Web/Mobile can consume the regenerated contracts after acceptance without client token decoding or readiness guesses. Shared M3, design tokens and future reports were not started.


## Complete changed/new-file inventory

Initial working status was empty: every path below belongs to this delivery, with no pre-existing changes mixed in. `M` means modified relative to the initial HEAD; `A` means a new, unstaged file. Generated clients are identified by their actual paths, not omitted from the inventory. Ignored build outputs, disposable fixtures, local logs and received snapshot artifacts are not source changes. Root package/lockfile, web/mobile runtime and shared/core are unchanged.

**207 paths: 72 modified, 135 new.**

```text
M design/docs/product.md
M design/docs/screens.md
M docs/loto-specs.md
M docs/loto_entity_relationship_diagram.html
M docs/loto_presentation.html
A infra/docker-compose.m8-mail.yml
A server/docs/m8-delivery.md
M server/docs/milestones.md
A server/server.API/Configuration/AccountSecurityConfiguration.cs
A server/server.API/Configuration/SecurityRateLimits.cs
A server/server.API/Controllers/AccountSecurityController.cs
A server/server.API/Controllers/InvitationController.cs
A server/server.API/Controllers/OperatorLinkRequestController.cs
M server/server.API/Controllers/UserController.cs
M server/server.API/ExceptionHandling/PostgresExceptionHandler.cs
M server/server.API/Program.cs
M server/server.Application/AppDependencyInjection.cs
A server/server.Application/Services/Security/AccountPasswordUpdater.cs
A server/server.Application/Services/Security/AccountSecurityOptions.cs
A server/server.Application/Services/Security/InvitationWorkflow.cs
A server/server.Application/Services/Security/OperatorLinkRequestReader.cs
A server/server.Application/Services/Security/SecurityRules.cs
A server/server.Application/Services/Security/SecurityValidation.cs
M server/server.Application/Services/SharedValidators.cs
M server/server.Application/UseCases/BranchUsers/Add/AddBranchUserUseCase.cs
M server/server.Application/UseCases/BranchUsers/Remove/RemoveBranchUserUseCase.cs
M server/server.Application/UseCases/BranchUsers/UpdateRole/UpdateBranchUserRoleUseCase.cs
A server/server.Application/UseCases/Invitations/Accept/AcceptInvitationUseCase.cs
A server/server.Application/UseCases/Invitations/Decline/DeclineInvitationUseCase.cs
A server/server.Application/UseCases/Invitations/InvitationTokenFluentValidation.cs
A server/server.Application/UseCases/Invitations/Issue/IssueInvitationFluentValidation.cs
A server/server.Application/UseCases/Invitations/Issue/IssueInvitationUseCase.cs
A server/server.Application/UseCases/Invitations/List/ListInvitationsFluentValidation.cs
A server/server.Application/UseCases/Invitations/List/ListInvitationsUseCase.cs
A server/server.Application/UseCases/Invitations/Preview/PreviewInvitationUseCase.cs
A server/server.Application/UseCases/Invitations/Register/RegisterInvitationFluentValidation.cs
A server/server.Application/UseCases/Invitations/Register/RegisterInvitationUseCase.cs
A server/server.Application/UseCases/Invitations/Reissue/ReissueInvitationFluentValidation.cs
A server/server.Application/UseCases/Invitations/Reissue/ReissueInvitationUseCase.cs
A server/server.Application/UseCases/OperatorLinkRequests/Create/CreateOperatorLinkRequestUseCase.cs
A server/server.Application/UseCases/OperatorLinkRequests/List/ListOperatorLinkRequestsFluentValidation.cs
A server/server.Application/UseCases/OperatorLinkRequests/List/ListOperatorLinkRequestsUseCase.cs
A server/server.Application/UseCases/OperatorLinkRequests/Own/GetOwnOperatorLinkRequestUseCase.cs
A server/server.Application/UseCases/OperatorLinkRequests/Resolve/ResolveOperatorLinkRequestUseCase.cs
M server/server.Application/UseCases/Operators/Create/CreateOperatorUseCase.cs
M server/server.Application/UseCases/Operators/Deactivate/DeactivateOperatorUseCase.cs
M server/server.Application/UseCases/Operators/Update/UpdateOperatorUseCase.cs
M server/server.Application/UseCases/Reports/ManagerQueue/GetManagerQueueUseCase.cs
A server/server.Application/UseCases/Users/ChangePassword/ChangePasswordFluentValidation.cs
A server/server.Application/UseCases/Users/ChangePassword/ChangeUserPasswordUseCase.cs
A server/server.Application/UseCases/Users/ForgotPassword/ForgotPasswordFluentValidation.cs
A server/server.Application/UseCases/Users/ForgotPassword/ForgotUserPasswordUseCase.cs
A server/server.Application/UseCases/Users/GetProfile/GetUserProfileUseCase.cs
M server/server.Application/UseCases/Users/Login/UserLoginMapper.cs
M server/server.Application/UseCases/Users/Login/UserLoginUseCase.cs
A server/server.Application/UseCases/Users/Logout/LogoutFluentValidation.cs
A server/server.Application/UseCases/Users/Logout/LogoutUserUseCase.cs
M server/server.Application/UseCases/Users/Register/UserRegisterMapper.cs
M server/server.Application/UseCases/Users/Register/UserRegisterUseCase.cs
M server/server.Application/UseCases/Users/RenewToken/UserRenewTokenUseCase.cs
A server/server.Application/UseCases/Users/ResetPassword/ResetPasswordFluentValidation.cs
A server/server.Application/UseCases/Users/ResetPassword/ResetUserPasswordUseCase.cs
A server/server.Application/UseCases/Users/UpdateProfile/UpdateProfileFluentValidation.cs
A server/server.Application/UseCases/Users/UpdateProfile/UpdateUserProfileUseCase.cs
A server/server.Application/UseCases/Users/UserProfileMapper.cs
M server/server.Application/UseCases/Users/UserValidationExtensions.cs
A server/server.Communication/Requests/RequestChangePasswordJson.cs
A server/server.Communication/Requests/RequestForgotPasswordJson.cs
A server/server.Communication/Requests/RequestInvitationTokenJson.cs
A server/server.Communication/Requests/RequestIssueInvitationJson.cs
A server/server.Communication/Requests/RequestListInvitationsJson.cs
A server/server.Communication/Requests/RequestListOperatorLinkRequestsJson.cs
A server/server.Communication/Requests/RequestLogoutJson.cs
A server/server.Communication/Requests/RequestRegisterInvitationJson.cs
A server/server.Communication/Requests/RequestReissueInvitationJson.cs
A server/server.Communication/Requests/RequestResetPasswordJson.cs
A server/server.Communication/Requests/RequestUpdateProfileJson.cs
A server/server.Communication/Responses/ResponseAcceptInvitationJson.cs
A server/server.Communication/Responses/ResponseInvitationJson.cs
A server/server.Communication/Responses/ResponseInvitationPreviewJson.cs
A server/server.Communication/Responses/ResponseListInvitationsJson.cs
A server/server.Communication/Responses/ResponseListOperatorLinkRequestsJson.cs
M server/server.Communication/Responses/ResponseManagerQueueJson.cs
A server/server.Communication/Responses/ResponseOperatorLinkRequestJson.cs
A server/server.Communication/Responses/ResponseOwnOperatorLinkRequestJson.cs
M server/server.Communication/Responses/ResponseUserLoginJson.cs
A server/server.Communication/Responses/ResponseUserProfileJson.cs
A server/server.Domain/Entities/Enums/InvitationStatus.cs
A server/server.Domain/Entities/Enums/MailDeliveryStatus.cs
A server/server.Domain/Entities/Enums/OperatorLinkRequestStatus.cs
A server/server.Domain/Entities/Invitation.cs
A server/server.Domain/Entities/OperatorLinkRequest.cs
A server/server.Domain/Entities/PasswordResetToken.cs
A server/server.Domain/Entities/RecoveryThrottle.cs
M server/server.Domain/Entities/RefreshToken.cs
M server/server.Domain/Entities/User.cs
A server/server.Domain/Interfaces/IAccountSecurityRepository.cs
A server/server.Domain/Interfaces/IOnboardingRepository.cs
A server/server.Domain/Interfaces/ISecurityCoordination.cs
A server/server.Domain/Interfaces/ISecurityMailSender.cs
M server/server.Domain/Interfaces/IUsersRepository.cs
M server/server.Exceptions/ResourcesErrorMessages.Designer.cs
M server/server.Exceptions/ResourcesErrorMessages.resx
A server/server.Infrastructure/Configurations/InvitationConfiguration.cs
A server/server.Infrastructure/Configurations/OperatorLinkRequestConfiguration.cs
A server/server.Infrastructure/Configurations/PasswordResetTokenConfiguration.cs
A server/server.Infrastructure/Configurations/RecoveryThrottleConfiguration.cs
M server/server.Infrastructure/Configurations/RefreshTokenConfiguration.cs
M server/server.Infrastructure/InfraDependencyInjection.cs
A server/server.Infrastructure/Migrations/20261018040013_M8AccountSecurityAndInvitations.Designer.cs
A server/server.Infrastructure/Migrations/20261018040013_M8AccountSecurityAndInvitations.cs
M server/server.Infrastructure/Migrations/ServerDbContextModelSnapshot.cs
A server/server.Infrastructure/Repositories/AccountSecurityRepository.cs
M server/server.Infrastructure/Repositories/BranchUsersRepository.cs
A server/server.Infrastructure/Repositories/OnboardingRepository.cs
M server/server.Infrastructure/Repositories/RefreshTokenRepository.cs
M server/server.Infrastructure/Repositories/UsersRepository.cs
M server/server.Infrastructure/ServerDbContext.cs
A server/server.Infrastructure/Services/RecoveryMailQueue.cs
A server/server.Infrastructure/Services/SecurityCoordination.cs
A server/server.Infrastructure/Services/SecurityMailOptions.cs
A server/server.Infrastructure/Services/SmtpSecurityMailSender.cs
M server/server.Infrastructure/server.Infrastructure.csproj
M server/tests/CommonTestUtilities/Repositories/BranchUsersRepositoryBuilder.cs
M server/tests/CommonTestUtilities/Repositories/UsersRepositoryBuilder.cs
A server/tests/CommonTestUtilities/Services/FixedUtcTimeProvider.cs
A server/tests/CommonTestUtilities/Services/SecurityCoordinationBuilder.cs
M server/tests/CommonTestUtilities/Services/TokenServicesBuilder.cs
A server/tests/UseCases.Test/UseCases/AccountSecurity/AccountSecurityUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/BranchUsers/Add/AddBranchUserUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/BranchUsers/Remove/RemoveBranchUserUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/BranchUsers/UpdateRole/UpdateBranchUserRoleUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Operators/Create/CreateOperatorUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Operators/Deactivate/DeactivateOperatorUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Operators/Update/UpdateOperatorUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Reports/ManagerQueue/GetManagerQueueUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Users/Login/UserLoginUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Users/Register/UserRegisterUseCaseTest.cs
M server/tests/UseCases.Test/UseCases/Users/RenewToken/UserRenewTokenUseCaseTest.cs
A server/tests/Validators.Test/AccountSecurity/AccountSecurityValidatorsTest.cs
A server/tests/WebApi.Test/AccountSecurity/AccountSecurityHttpTest.cs
A server/tests/WebApi.Test/AccountSecurity/AccountSecurityRollbackHttpTest.cs
A server/tests/WebApi.Test/AccountSecurity/EmailNormalizationHttpTest.cs
A server/tests/WebApi.Test/AccountSecurity/LocalSmtpDeliveryTest.cs
A server/tests/WebApi.Test/AccountSecurity/M8LocalAcceptanceJourneyTest.cs
A server/tests/WebApi.Test/AccountSecurity/MembershipSecurityHttpTest.cs
A server/tests/WebApi.Test/AccountSecurity/RecoveryRateLimitHttpTest.cs
M server/tests/WebApi.Test/Architecture/ArchitectureTest.cs
M server/tests/WebApi.Test/Branches/BranchScopedControllerUnhappyPathTest.cs
A server/tests/WebApi.Test/Infrastructure/LocalMailpitCapture.cs
A server/tests/WebApi.Test/Infrastructure/M8Scenario.cs
A server/tests/WebApi.Test/Infrastructure/M8SecurityMigrationTest.cs
A server/tests/WebApi.Test/Infrastructure/M8WriteFailure.cs
A server/tests/WebApi.Test/Infrastructure/SecurityMailCapture.cs
M server/tests/WebApi.Test/Infrastructure/ServerWebApplicationFactory.cs
M server/tests/WebApi.Test/Infrastructure/TestSeeder.cs
A server/tests/WebApi.Test/Invitations/InvitationEdgeCasesHttpTest.cs
A server/tests/WebApi.Test/Invitations/InvitationHttpTest.cs
A server/tests/WebApi.Test/Invitations/InvitationRollbackHttpTest.cs
A server/tests/WebApi.Test/OpenApi/M8AccountSecurityOpenApiContractTest.cs
M server/tests/WebApi.Test/OpenApi/OperationSecurityOpenApiContractTest.cs
M server/tests/WebApi.Test/OpenApi/Snapshots/openapi-contract-surface.txt
A server/tests/WebApi.Test/OperatorLinkRequests/OperatorLinkRequestHttpTest.cs
M shared/AGENTS.md
M shared/api/openapi/server-api.openapi.json
M shared/api/scripts/export-openapi.ts
A shared/api/src/contract-checks/account-security-fidelity.ts
A shared/api/src/generated/endpoints/account-security/account-security.ts
M shared/api/src/generated/endpoints/index.ts
A shared/api/src/generated/endpoints/invitation/invitation.ts
A shared/api/src/generated/endpoints/operator-link-request/operator-link-request.ts
M shared/api/src/generated/endpoints/user/user.ts
A shared/api/src/generated/model/getInvitationParams.ts
A shared/api/src/generated/model/getOperatorLinkRequestParams.ts
M shared/api/src/generated/model/index.ts
A shared/api/src/generated/model/invitationStatus.ts
A shared/api/src/generated/model/mailDeliveryStatus.ts
A shared/api/src/generated/model/operatorLinkRequestStatus.ts
A shared/api/src/generated/model/requestChangePasswordJson.ts
A shared/api/src/generated/model/requestForgotPasswordJson.ts
A shared/api/src/generated/model/requestInvitationTokenJson.ts
A shared/api/src/generated/model/requestIssueInvitationJson.ts
A shared/api/src/generated/model/requestLogoutJson.ts
A shared/api/src/generated/model/requestRegisterInvitationJson.ts
A shared/api/src/generated/model/requestReissueInvitationJson.ts
A shared/api/src/generated/model/requestResetPasswordJson.ts
A shared/api/src/generated/model/requestUpdateProfileJson.ts
A shared/api/src/generated/model/responseAcceptInvitationJson.ts
A shared/api/src/generated/model/responseInvitationJson.ts
A shared/api/src/generated/model/responseInvitationPreviewJson.ts
A shared/api/src/generated/model/responseListInvitationsJson.ts
A shared/api/src/generated/model/responseListOperatorLinkRequestsJson.ts
M shared/api/src/generated/model/responseManagerQueueJson.ts
A shared/api/src/generated/model/responseOperatorLinkRequestJson.ts
A shared/api/src/generated/model/responseOwnOperatorLinkRequestJson.ts
M shared/api/src/generated/model/responseUserLoginJson.ts
A shared/api/src/generated/model/responseUserProfileJson.ts
M shared/api/src/generated/operations/operations.ts
A shared/api/src/generated/queries/account-security/account-security.ts
M shared/api/src/generated/queries/index.ts
A shared/api/src/generated/queries/invitation/invitation.ts
A shared/api/src/generated/queries/operator-link-request/operator-link-request.ts
M shared/api/src/generated/queries/user/user.ts
A shared/api/test/account-security.test.ts
M shared/api/test/export-environment.test.ts
M shared/api/test/header-boundary.test.ts
M shared/docs/milestones.md
```


## SV11 Phase 6–7 technical plan — source baseline 9ea777c (2026-10-18)

Initial checkout: `Project`, branch `main`, HEAD `9ea777cdb5376f5f6396de65bfba39db41ae91ee`, clean. This commit carries the Phase 1–5 implementation and the separate M9.5 evidence update. S06 actual-commit closure remains recorded; no new independent M8 acceptance was supplied by that commit or the M9.5 review. Items 1.4/5.5 and both v52 design markers remain pending combined Claude review. Selected implementation: 6.1–6.3, 7.1 and the technical scenarios within 7.2; genuine provider acceptance and Phase 8 remain separate gates. M9.5 evidence and shop-answer gates are preserved.

Ownership/files: Domain user/platform/provider/challenge/onboarding records and pure ports; Communication DTOs; Application operation use cases/validators and shared seed/invitation primitives; Infrastructure repositories/configuration/verifier/migration; API platform guard, thin controllers and derived OpenAPI metadata; server-owned executable CLI; real PostgreSQL/HTTP/SMTP/CLI and isolated protocol tests; canonical three-file v54 plus semantic product/screens reconciliation; actual S06 export/generated clients. No app UI, Shared M3, design tokens, reporting or financial formula changes.

Contract/acceptance selected before runtime edits:

- Platform flag defaults false; profile exposes read-only capability. `PlatformOperator` is an authoritative identity-tier guard with explicit OpenAPI platform restriction metadata, never a new bearer tier or tenant role. `POST /platform/onboarding` accepts a required request UUID, branch/owner fields and bounded optional setup; 201 on first create, 200 repeat for identical actor/request/content, 409 changed repeat. `GET /platform/onboarding/{requestId}` and `POST .../reissue-owner-invitation` expose only the operated record and owner invitation metadata; platform authority grants no branch membership/session/data. Missing/false/revoked authority 403; unknown record 404. All mutation conflicts use pt-BR resources.
- One application onboarding use case atomically creates the branch, existing default seed factory output, explicit optional terminals/paired Tabs/additional products/operators/assignments and pending Admin invitation. No initial member is required. A persisted request identity/hash prevents duplicate branches after an uncertain response; mail is post-commit with existing truthful delivery policy. Reissue replaces the owner capability, preserving the branch. Advisory keys include request, caller email, owner email and branch, all globally sorted; fresh authority is checked under the caller account key.
- CLI supports explicit `Development`/`Staging`, an explicit PostgreSQL target and JSON request file, read-only inspect/dry-run, separately explicit platform grant/revoke, onboarding and owner reissue. It uses the same Application use cases and real configured infrastructure, never an API seed route or fake branch Admin. `--demo-data` is CLI-only, requires an `@example.invalid` owner and otherwise empty optional setup, and supplies only fake terminal/paired Tab/product/operator structure. Normal stdout contains ids/bounded statuses, no passwords/capabilities/connection strings. Tests use only owned disposable databases/capture.
- Remove actual `POST /user/register`, `POST /branch/create` and `POST /branch/users` actions; unsupported URLs return normal routing 404/405. Other reads/membership/session contracts stay. Refactor fixture bootstrap to explicit test-owned seeds/application seams. Generated functions disappear through actual export/generation.
- `POST /user/google/challenge` is anonymous and returns a five-minute UUID/nonce/state challenge. Persist only nonce/state hashes, server UTC expiry and consumption. `POST /user/google/sign-in` accepts challenge UUID/state, ID token, optional invitation capability/name/current password; returns ordinary `ResponseUserLoginJson`. JSON/body transport, client-held state and Google-bound nonce, single consumption and default no cross-origin/cookie authentication prevent callback/token replay; future app adapters must retain their own challenge and pass nonce to Google. No redirect-cookie GIS endpoint is introduced. Both routes use existing bounded anonymous rate limiting.
- Real verifier uses Google's `Google.Apis.Auth` ID-token validation library with its trusted Google certificates, issuer/audience/signature/lifetime checks, then requires verified email and a challenge-bound nonce. Only configured client audiences are variable; missing audiences disable with resource 503, invalid assertions 401, provider outage 503. An injected verifier supports isolated application tests; configuration never selects a test verifier or arbitrary issuer/JWKS endpoint. No live Google login is performed.
- Unique stable `(issuer, subject)` provider identity and one Google identity per user; safe-null local `EmailVerifiedAt`. Legacy users are not backfilled verified. Successful invitation acceptance/recovery proves local email. First link to an existing unverified password account requires current password; non-Google-authoritative email also requires password. New Google account creation requires a matching valid invitation; existing linked users use stable subject even if provider email changes. Inactive accounts/collisions/mismatched invites fail without partial membership/link/session. Google-only users have no usable password until secure recovery establishes one. Session issue, provider link, challenge consumption and optional invitation join commit atomically under shared account/branch/provider/challenge coordination.
- Verify fresh/upgrade/backfill/Down limitations, concurrent repeats/acceptance/provider links/reset/renewal, trigger rollback and no-tenant authority; run full existing suites and systemic auth/snapshot/generated transport gates. At this planning stage, the canonical v54/design v52 mismatch remained explicit pending independent combined semantic acceptance; the later accepted review and marker close-out are recorded below, without a full-M8/provider readiness claim.

Primary protocol references: [Google server verification](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token), [Google nonce/state client contract](https://developers.google.com/identity/gsi/web/reference/js-reference), [Google .NET ID-token validator](https://cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.GoogleJsonWebSignature). These justify trusted JWT validation, stable subject and explicit proof for legacy/non-authoritative email rather than decoding a caller assertion.

## SV11 Phase 6–7 implementation and local acceptance (2026-10-18)

The source baseline was clean `main` at `9ea777cdb5376f5f6396de65bfba39db41ae91ee`. During this work the coordinator landed `a326b4d` for Design M1, changing only `design/docs/decisions.md`, `design/docs/milestones.md` and the Claude reference manifest/readme. It did not change the M8 source; this delivery did not stage, commit, switch branch, merge or alter those four paths. The preceding M8 Phases 1–5 candidate is in `9ea777c` but 1.4/5.5 independent acceptance was not recorded by that commit. S06 1.6/5.2 real-commit clone evidence remains in the Shared note. M9.5 legacy evidence and shop-answer gates were left intact.

**Behavior and security boundary.** `IsPlatformOperator` upgrades false for every legacy account and cannot be set by an API DTO. Platform HTTP and the CLI call the same `OperateBranchUseCase`; a request UUID + actor + canonical content hash gives 201 once, 200 on identical repeat, 409 on changed repeat. The single database transaction writes branch/default seeds/explicit optional setup/pending hashed owner Admin invitation and the idempotency record. Local SMTP acceptance is attempted after commit; failed or uncertain delivery is visible and owner reissue replaces the capability without cloning the branch. The platform caller receives no membership or branch token. The three actual public bootstrap POST actions are removed; `POST /branch/users` is 405 because its GET sibling remains, while register/create POSTs are 404. There is no development-only public escape route.

`POST /user/google/challenge` and `/sign-in` keep state and nonce hashes, server-UTC five-minute expiry and single use. The production verifier uses `Google.Apis.Auth` with fixed Google trust and configured audiences; no test verifier is selected by configuration. An injected verifier drives local application/HTTP scenarios only. Existing password accounts require current-password proof unless both recorded local mailbox proof and Google Gmail/Workspace authority exist. A stable linked subject signs in through the ordinary identity/refresh envelope; unknown subjects create accounts only through a matching invitation. Passwordless Google-created accounts cannot use password login or current-password change until the existing recovery path establishes a local password. All changed membership and refresh-session authority remains database-backed.

**Disposable local journey.** The `OperatedOnboardingHttpTest` fixture starts a private PostgreSQL Testcontainer, applies the real migration, creates a fake platform identity and fake `@example.invalid` owner, then calls `POST /platform/onboarding`. Immediate observations are a single seeded branch with zero memberships, Pending owner Admin invitation and captured local capability; the platform identity's `/branch/my-branches` is empty and `/branch/session` for that branch is denied. Over-posted platform/role/membership fields cannot change that result. `POST /invitation/register` with the captured capability creates the owner's identity/membership, and a new branch session succeeds. The same fixture launches `dotnet server/server.Cli/bin/Debug/net10.0/server.Cli.dll onboard --environment Development --connection <test-container-connection> --actor-user-id <fake-platform-user-id> --request-file <test-owned-json> --execute`, then repeats that UUID by HTTP and observes the identical branch. `set-platform` without `--execute` writes nothing; grant enables the route and revoke denies it immediately. `--demo-data` with an `@example.invalid` owner first runs without `--execute` (zero writes), then with it (one fake Terminal/paired Tab/product/operator, no financial ledger or payment-method claim). A concurrent duplicate request creates one branch/invitation; a database trigger rejecting the invitation insert rolls back branch/seeds/request identity, and a retry after removing the test trigger succeeds. Expired owner-invite reissue invalidates the prior capability. The test fixture disposes only its own database/capture; no shared database reset or real SMTP delivery occurs.

`GoogleSignInHttpTest` is explicitly **TEST-ONLY** at the verifier boundary: it exercises actual HTTP/PostgreSQL transaction behavior for password proof, stable subject, identity/refresh renewal, invitation-only new account, replay, unverified email, wrong state/nonce, invitation mismatch and two subjects racing to link one password account. `GoogleVerifierBoundaryTest` directly exercises the production adapter's no-audience fail-closed path and malformed/unsigned rejection without contacting Google. These scenarios do **not** prove a real Google project, consent screen, client audience, rotating certificate fetch or live login. The local mail capture likewise proves no production sender/domain/DNS acceptance.

**Migration and compatibility.** `20261018165353_M8OperatedOnboardingAndGoogleIdentity` adds nullable `Users.EmailVerifiedAt`, false-default `Users.IsPlatformOperator`, unique `(Issuer,Subject)` and `UserId` Google link indexes, unique hashed challenge state/nonce indexes and unique operated request/branch/invitation indexes with restricted FKs. Upgrade from the pre-M8 schema preserves the old normalized account/refresh session and leaves legacy email proof null/platform authority false; fresh Testcontainer migrations and EF model check pass. A disposable database test applies the new migration and downgrades to `20261018040013_M8AccountSecurityAndInvitations`, confirming the three new tables/two columns disappear. `Down` therefore loses newly recorded provider/onboarding/proof state; it is a schema rollback, not a reversible identity-data migration. No financial schema, variance or FIFO rule changes.

**Shared/document ownership.** Canonical v54 is in all three project-level files. Independent repeat review accepted both M8 batches technically and semantically; product/screens reconciliation and both `Synced against loto-specs` markers now align at v54. The actual `bash docs/check-loto-doc-sync.sh` exits 0 and `bash docs/test-loto-doc-sync.sh` passes all 34 scenarios. Items 1.4 and composite 5.5 are checked in the owning milestone; this documentary close-out diff remains available for its separate review. This candidate closes gap C's backend escape paths and implements N/U/V backend contracts, but uncataloged design 0.4.6, production providers, owner visual direction and launch readiness remain open.

**Phase 8 operator/provider acceptance checklist (all open):** configure a real trusted application URL, sender address and SMTP transport only under later deployment authorization; verify sender domain SPF/DKIM/DMARC and received-link behavior with approved recipient fixtures; define retry/monitoring for uncertain SMTP acceptance and bounded in-memory queue loss; create a real Google OAuth client/consent project with exact web/mobile audiences and origin policy, configure `GoogleIdentity:ClientIds` through secrets and verify a live signed ID token and account link/renewal without logging tokens; review token/session/key rotation, rate limits, retained challenge/refresh data and audit/alerting; complete the 0.4.6 invite/profile/recovery/Google/Plataforma/owner-checklist UI catalog and real app acceptance; perform the remaining Phase 8 security/provider and launch review. None of these is inferred from local adapters or checked as complete here.

## SV11 validation ledger and remaining gates

| Exact command or focused fixture | Result on this candidate |
|---|---|
| `dotnet build server/server.sln --no-restore -v:q` | Exit 0; zero errors and six pre-existing EF1002 warnings in older migration tests. |
| `dotnet test server/tests/Validators.Test --no-build -v:q`; `dotnet test server/tests/UseCases.Test --no-build -v:q` | 344/344 and 1148/1148 passed after the final server build. |
| `dotnet test server/tests/WebApi.Test --no-build -v:n > /private/tmp/loto-m8-final-webapi.log 2>&1` | Final exact-tree run exits 0: **1231/1231** WebApi/architecture tests in 4m22s. One preceding quiet run had 1230/1231 with an older `CloseReviewEventsMigrationTest` failure; that test passed 1/1 when isolated, and the full diagnostic rerun passed. The quiet output did not retain the first failure detail, so no cause is asserted. |
| Focused `OperatedOnboardingHttpTest`, `GoogleSignInHttpTest`, `GoogleVerifierBoundaryTest`, `M8SecurityMigrationTest` filters | Real PostgreSQL/HTTP/CLI/capture scenarios passed, including concurrent duplicate, trigger rollback/retry, CLI parity/demo/grant/revoke, Google link races and removed membership, adapter malformed/unsigned fail-closed, upgrade/backfill and Down. The final over-post and verifier pair passed 2/2. |
| `dotnet ef migrations has-pending-model-changes --project server.Infrastructure --startup-project server.API --no-build` in `server/` | Exit 0; no pending model changes. Fresh migration application is exercised by the full disposable HTTP fixture. |
| `CI=true pnpm install --frozen-lockfile`; `pnpm typecheck`; `pnpm lint`; `pnpm format:check`; `pnpm test` | All exit 0 with the exact lockfile; 103/103 Shared tests, 244 hook-free root modules and no generated retry. Initial sandboxed offline install could not find a cached tarball; the allowed frozen install succeeded without lockfile changes. |
| `pnpm --filter @loto/api export:openapi`; `pnpm --filter @loto/api generate`; root/package `export:check` and `generate:check` | Exit 0. Candidate input SHA-256 `e903b801be1db602d6e02ec505c4ae2a48888d869f58af7c3af50b26f5e11d48` (90 paths, 120 operations, 186 schemas); 257-file generated tree SHA-256 `115efe12e51f58f0ae3decd7a4d9b9059386130d076a35320dee07f637dadad6`. Landing hash awaits an authorized commit. |
| Inert Release build without `OpenApiGenerateDocuments=true`; CLI `--help` and refused `Production` target | Zero exported documents by default; help exits 0; Production request exits 1 before any database contact. |
| `node --check` on extracted scripts from both canonical HTML files; `git diff --check` | All scripts parse; whitespace clean. |
| `bash docs/check-loto-doc-sync.sh`; `bash docs/test-loto-doc-sync.sh` | After independent semantic acceptance, actual repository markers align at v54: checker exit 0 and all 34 baseline regression scenarios pass. The prior v52-marker failure is historical pre-review evidence. |

Items **1.4** and composite **5.5** are now closed by the accepted combined review and actual v54 doc-sync checks. The remaining selected gate is **7.2/7.2.2** genuine Google project/consent/client/live-provider acceptance. Phase 8 sender/DNS/monitoring, 0.4.6 UI and whole-M8/launch acceptance remain open. No real credentials, outbound messages, device login, deployment, stage or commit are claimed.

## SV11 repeat-review correction and first-operator procedure

The technical repeat review inspected the Phase 6–7 candidate on `main` at `a326b4d937543e0a243bd1c5aac8204090cee24f` and found two blocking issues: an empty migrated database had no way to acquire its first platform operator after public registration closed, and password login disclosed whether an account existed. The correction adds no public bootstrap or grant route and changes no financial rules or existing M8 migration.

`bootstrap-platform --name --email` now checks the database under a global PostgreSQL advisory transaction lock. Only zero users, or exactly one active platform user with the same normalized email/name and still no password or recorded email proof, are eligible. It creates a passwordless platform user and a hashed 256-bit, UTC-expiring password-reset capability in one commit. The existing SMTP adapter then sends a trusted application link; a send failure is reported and the command can replace the token only for that still-unactivated sole account. The normal `/user/reset-password` path consumes the capability, sets the password, records email proof and revokes sessions. The CLI never accepts or prints a password or capability. Dry-run validates the payload and reads current database eligibility without writing. Operated-onboarding dry-run validates the full request and the actor's fresh platform flag. Both CLI and API use the same bounded `AccountSecurity` URL/lifetime parser. CLI process tests resolve the actual built CLI project reference, including from a separate output tree.

For Development/Staging, an operator sets `LOTO_CLI_AccountSecurity__ApplicationUrl` and the local SMTP capture settings, migrates an owned database, then runs `dotnet run --project server/server.Cli/server.Cli.csproj -- bootstrap-platform --environment Development --connection <owned-disposable-connection> --name <operator-name> --email <fake-address@example.invalid>` to preview; `--execute` commits and attempts delivery. The captured `reset-password#token=` capability is sent to `/user/reset-password` with a new password. The operator can then log in and use the ordinary platform onboarding workflow. The disposable `FirstPlatformOperatorCliTest` performs this full journey, including a failed-delivery retry and a configured seven-minute lifetime. No production sender is inferred from local Mailpit.

Production bootstrap uses the **same** command only after Phase 8 approves the sender, trusted HTTPS application URL, target database and recipient. Its exact gated invocation is `dotnet run --project server/server.Cli/server.Cli.csproj -- bootstrap-platform --environment Production --allow-production-bootstrap --connection-env LOTO_BOOTSTRAP_DB --name "$LOTO_OPERATOR_NAME" --email "$LOTO_OPERATOR_EMAIL" --execute`, with the connection string supplied only through the named environment variable, plus approved `LOTO_CLI_AccountSecurity__ApplicationUrl`, `LOTO_CLI_SecurityMail__Enabled=true`, TLS SMTP host/port/from/auth settings and `LOTO_CLI_SecurityMail__LocalCapture=false`. The command rejects ordinary Production operations, a Production connection on the command line, disabled/plaintext mail and non-HTTPS application links. This is a documented procedure, **not** evidence that a real sender, recipient or production environment was configured or exercised.

Password login now returns the same pt-BR 401 resource for missing, inactive, wrong-password and passwordless accounts, with one BCrypt verification on each path to reduce the obvious timing distinction. The HTTP test compares their exact serialized error bodies. Login throttling is explicitly still Phase 8.1. Retired `UserRegisterUseCase`, `CreateBranchUseCase` and `AddBranchUserUseCase`, their validators/mappers/unused DTOs and tests are gone; the internal `BranchBootstrap` and its seed fixture remain. Google sign-in has a dedicated validator and tested post-library identity policy, while challenge creation purges expired rows. No live Google provider acceptance is claimed.

The prior SV11 generated-tree entry used path-object sorting and was wrong. Recomputing all 257 generated files by ordinal **relative path string**, hashing each file and then hashing the UTF-8 `<sha256>  <relative-path>\n` lines yields `115efe12e51f58f0ae3decd7a4d9b9059386130d076a35320dee07f637dadad6`. This correction is recorded in both this note and Shared's milestone note. The OpenAPI input/clients are unchanged by the review fixes; the landing commit hash still awaits a separately authorized commit.

Canonical v54 and the derived presentation/ERD describe the first-operator path and generic login behavior; product/screens reconcile those backend facts. The clean independent repeat review accepted both M8 batches technically and semantically. Both design markers now read **v54**; the actual doc-sync checker and all 34 regressions pass. Items 1.4 and 5.5 are checked, while 7.2/7.2.2 and 8.1–8.2 stay open for genuine provider, UI and launch evidence.

### Repeat-review correction validation

This correction started from `main` at `a326b4d937543e0a243bd1c5aac8204090cee24f` with the prior Phase 6–7 candidate already staged (109 paths) and no unstaged files. The index was preserved; every correction below remains unstaged. `dotnet build server/server.sln --no-restore -m:1 -nr:false -v q` passes with zero errors and six existing EF1002 warnings in older integration fixtures. After that ordinary build, `dotnet test server/tests/Validators.Test/Validators.Test.csproj --no-build --no-restore -m:1 -nr:false -v q` passes **328/328** and the corresponding `UseCases.Test.csproj` command passes **1144/1144**. A real disposable PostgreSQL/Mailpit first-operator test covers an empty migrated database, no-write preview, failed-delivery retry, hashed capability with the configured seven-minute lifetime, reset/login and successful platform onboarding by the activated operator. Its separately built CLI path is resolved from the project reference, not a hard-coded in-tree binary.

Targeted Stryker mutation testing of `BootstrapPlatformOperatorUseCase` and `UserLoginUseCase` killed **56/56 runnable selected mutants**, with no survivors or no-coverage mutants; one block-removal mutant could not compile and seven mutants were ignored by the scope/filter. This is targeted evidence, not a whole-server score. Tests strengthened the single-account retry guard, validation rejection and transaction completion assertions; the unavailable-account dummy BCrypt hash was made non-equivalent to an ordinary hard-coded password. The ordinary build above restored unmutated outputs after Stryker.

`dotnet test server/tests/WebApi.Test/WebApi.Test.csproj --no-build --no-restore -m:1 -nr:false -v q` passes **1237/1237** in 4m46s on the final code, including architecture, real PostgreSQL migrations/HTTP, CLI process and local Mailpit capture. The separate-output CLI project-reference regression also passes 1/1 with `BaseOutputPath=/private/tmp/loto-sv13-cli-scratch/`. The frozen workspace install, recursive typecheck/lint/format/test (103 Shared tests), both-directory `generate:check` and `export:check`, default export-disabled build, migration model check, HTML script parse and 34 disposable doc-checker regressions passed earlier in this same correction after the API/Shared source changes; the later mutation-driven source change was confined to the login dummy BCrypt hash and unit assertions, with the final server build and three suites repeated above. `git diff --check` and `git diff --cached --check` pass. At the correction stage before independent review, the repository doc-sync checker failed only for the deliberate canonical v54/design v52 marker gate; after the accepted semantic review and marker update it exits 0, with 34/34 regression scenarios passing. No live SMTP sender or Google provider was exercised.

## M8 repeat-review acceptance and documentary close-out (2026-10-18)

The independent repeat review inspected a disposable clone of `a326b4d` plus the unchanged 109-path staged candidate, the 58-path unstaged correction and nine new files. It reproduced and accepted every P2/P3 finding and the three earlier P4 suggestions, with build, 328 Validator, 1144 UseCase, 1237 WebApi/architecture, separate-output CLI, EF-model, Shared generation/export and HTML checks passing. The reviewer explicitly accepted **both M8 batches, Phases 1–5 and 6–7, technically and semantically**, without claiming business-owner, provider, UI or launch acceptance. The review's optional P4 suggestions are non-blocking future refinements and were not mixed into this documentary close-out.

The reviewed canonical v54 spec, presentation and ERD and the semantically reconciled product/screens now share v54 markers. On this checkout, `bash docs/check-loto-doc-sync.sh` exits **0** and `bash docs/test-loto-doc-sync.sh` passes the baseline plus **34/34** disposable checker scenarios. Owning milestone items **1.4** and **5.5**, including its actual-repository doc-sync sub-item, are checked on that evidence. The Phase 6–7 landing commit hash remains pending a separately authorized commit; no self-reference is recorded here. **7.2/7.2.2, 8.1 and 8.2 remain unchecked** for live Google, production sender/abuse controls, UI handoff and whole-M8/launch acceptance. The documentary close-out is left as a diff for independent review, without staging or committing it.

## SV11 exact changed/new-file inventory (Phase 6–7 candidate at first delivery)

At inventory capture: 69 changed tracked paths (including deletions) and 41 new paths; `git diff --cached --name-only` is empty. `M`, `D` and `A` denote modified, deleted and new. Generated OpenAPI/client paths are actual generator output, not hand edits. The later Design M1 commit is HEAD-only and absent from this working-tree inventory.

Changed tracked paths:
```text
M	design/docs/product.md
M	design/docs/screens.md
M	docs/loto-specs.md
M	docs/loto_entity_relationship_diagram.html
M	docs/loto_presentation.html
M	server/docs/m8-delivery.md
M	server/docs/milestones.md
M	server/server.API/ApiDependencyInjection.cs
M	server/server.API/Controllers/BranchController.cs
M	server/server.API/Controllers/UserController.cs
M	server/server.API/OpenApi/OperationSecurityOpenApiOperationTransformer.cs
M	server/server.API/OpenApi/OperationTokenRequirementResolver.cs
M	server/server.Application/AppDependencyInjection.cs
M	server/server.Application/Services/Security/InvitationWorkflow.cs
M	server/server.Application/UseCases/Branches/Create/CreateBranchUseCase.cs
M	server/server.Application/UseCases/Invitations/Accept/AcceptInvitationUseCase.cs
M	server/server.Application/UseCases/Invitations/Register/RegisterInvitationUseCase.cs
M	server/server.Application/UseCases/Users/ChangePassword/ChangeUserPasswordUseCase.cs
M	server/server.Application/UseCases/Users/Login/UserLoginUseCase.cs
M	server/server.Application/UseCases/Users/ResetPassword/ResetUserPasswordUseCase.cs
M	server/server.Application/UseCases/Users/UserProfileMapper.cs
M	server/server.Communication/Responses/ResponseUserProfileJson.cs
M	server/server.Domain/Entities/User.cs
M	server/server.Exceptions/ResourcesErrorMessages.Designer.cs
M	server/server.Exceptions/ResourcesErrorMessages.resx
M	server/server.Infrastructure/Configurations/UserConfiguration.cs
M	server/server.Infrastructure/InfraDependencyInjection.cs
M	server/server.Infrastructure/Migrations/ServerDbContextModelSnapshot.cs
M	server/server.Infrastructure/ServerDbContext.cs
M	server/server.Infrastructure/server.Infrastructure.csproj
M	server/server.sln
M	server/tests/WebApi.Test/AccountSecurity/AccountSecurityHttpTest.cs
M	server/tests/WebApi.Test/AccountSecurity/EmailNormalizationHttpTest.cs
M	server/tests/WebApi.Test/AccountSecurity/M8LocalAcceptanceJourneyTest.cs
M	server/tests/WebApi.Test/Architecture/ArchitectureTest.cs
M	server/tests/WebApi.Test/Branches/BranchGlobalControllerHappyPathTest.cs
M	server/tests/WebApi.Test/Branches/BranchScopedControllerHappyPathTest.cs
M	server/tests/WebApi.Test/Branches/BranchScopedControllerUnhappyPathTest.cs
M	server/tests/WebApi.Test/Branches/BranchTokenScopeTest.cs
M	server/tests/WebApi.Test/Branches/Create/CreateBranchSeedPersistenceTest.cs
M	server/tests/WebApi.Test/Infrastructure/M8SecurityMigrationTest.cs
M	server/tests/WebApi.Test/Infrastructure/ServerWebApplicationFactory.cs
M	server/tests/WebApi.Test/Infrastructure/TestSeeder.cs
M	server/tests/WebApi.Test/OpenApi/M8AccountSecurityOpenApiContractTest.cs
M	server/tests/WebApi.Test/OpenApi/OperationSecurityOpenApiContractTest.cs
M	server/tests/WebApi.Test/OpenApi/Snapshots/openapi-contract-surface.txt
M	server/tests/WebApi.Test/Serialization/NamedEnumRequestValidationTest.cs
M	server/tests/WebApi.Test/Users/UserControllerHappyPathTest.cs
M	shared/api/openapi/server-api.openapi.json
M	shared/api/scripts/export-openapi.ts
M	shared/api/src/contract-checks/account-security-fidelity.ts
M	shared/api/src/generated/endpoints/branch/branch.ts
M	shared/api/src/generated/endpoints/index.ts
M	shared/api/src/generated/endpoints/user/user.ts
M	shared/api/src/generated/model/index.ts
D	shared/api/src/generated/model/requestAddBranchUserJson.ts
D	shared/api/src/generated/model/requestCreateBranchJson.ts
D	shared/api/src/generated/model/requestUserRegisterJson.ts
D	shared/api/src/generated/model/responseAddBranchUserJson.ts
D	shared/api/src/generated/model/responseCreateBranchJson.ts
M	shared/api/src/generated/model/responseUserProfileJson.ts
D	shared/api/src/generated/model/responseUserRegisterJson.ts
M	shared/api/src/generated/operations/operations.ts
M	shared/api/src/generated/queries/branch/branch.ts
M	shared/api/src/generated/queries/index.ts
M	shared/api/src/generated/queries/user/user.ts
M	shared/api/test/export-environment.test.ts
M	shared/api/test/header-boundary.test.ts
M	shared/docs/milestones.md
```

New paths:
```text
A	server/server.API/Controllers/PlatformController.cs
A	server/server.API/Filters/TokenPlatformOperatorFilter.cs
A	server/server.Application/Services/Security/BranchBootstrap.cs
A	server/server.Application/UseCases/Platform/OperateBranch/OperateBranchFluentValidation.cs
A	server/server.Application/UseCases/Platform/OperateBranch/OperateBranchUseCase.cs
A	server/server.Application/UseCases/Platform/ReadOnboarding/ReadOperatedOnboardingUseCase.cs
A	server/server.Application/UseCases/Platform/ReissueOwnerInvitation/ReissueOwnerInvitationUseCase.cs
A	server/server.Application/UseCases/Users/Google/CreateGoogleChallengeUseCase.cs
A	server/server.Application/UseCases/Users/Google/GoogleSignInUseCase.cs
A	server/server.Cli/Program.cs
A	server/server.Cli/server.Cli.csproj
A	server/server.Communication/Requests/RequestGoogleSignInJson.cs
A	server/server.Communication/Requests/RequestOperateBranchJson.cs
A	server/server.Communication/Responses/ResponseGoogleChallengeJson.cs
A	server/server.Communication/Responses/ResponseOperatedOnboardingJson.cs
A	server/server.Domain/Entities/GoogleIdentity.cs
A	server/server.Domain/Entities/GoogleLoginChallenge.cs
A	server/server.Domain/Entities/OperatedOnboarding.cs
A	server/server.Domain/Interfaces/IGoogleIdentityRepository.cs
A	server/server.Domain/Interfaces/IGoogleIdentityVerifier.cs
A	server/server.Domain/Interfaces/IOperatedOnboardingRepository.cs
A	server/server.Exceptions/Exceptions/GoogleProviderUnavailableException.cs
A	server/server.Infrastructure/Configurations/GoogleIdentityConfiguration.cs
A	server/server.Infrastructure/Configurations/GoogleLoginChallengeConfiguration.cs
A	server/server.Infrastructure/Configurations/OperatedOnboardingConfiguration.cs
A	server/server.Infrastructure/Migrations/20261018165353_M8OperatedOnboardingAndGoogleIdentity.Designer.cs
A	server/server.Infrastructure/Migrations/20261018165353_M8OperatedOnboardingAndGoogleIdentity.cs
A	server/server.Infrastructure/Repositories/GoogleIdentityRepository.cs
A	server/server.Infrastructure/Repositories/OperatedOnboardingRepository.cs
A	server/server.Infrastructure/Services/GoogleIdentityVerifier.cs
A	server/tests/WebApi.Test/AccountSecurity/GoogleSignInHttpTest.cs
A	server/tests/WebApi.Test/AccountSecurity/GoogleVerifierBoundaryTest.cs
A	server/tests/WebApi.Test/Platform/OperatedOnboardingHttpTest.cs
A	shared/api/src/generated/endpoints/platform/platform.ts
A	shared/api/src/generated/model/requestGoogleSignInJson.ts
A	shared/api/src/generated/model/requestOnboardingOperatorJson.ts
A	shared/api/src/generated/model/requestOnboardingTerminalJson.ts
A	shared/api/src/generated/model/requestOperateBranchJson.ts
A	shared/api/src/generated/model/responseGoogleChallengeJson.ts
A	shared/api/src/generated/model/responseOperatedOnboardingJson.ts
A	shared/api/src/generated/queries/platform/platform.ts
```

## SV11 repeat-review correction file inventory

These paths are the review-fix delta relative to the earlier Phase 6–7 candidate. `M` means edited, `D` removed and `A` newly added. The preceding SV11 inventory records the first candidate separately.

```text
M	design/docs/product.md
M	design/docs/screens.md
M	docs/loto-specs.md
M	docs/loto_entity_relationship_diagram.html
M	docs/loto_presentation.html
M	server/docs/m8-delivery.md
M	server/docs/milestones.md
M	server/server.API/Configuration/AccountSecurityConfiguration.cs
M	server/server.API/Controllers/PlatformController.cs
M	server/server.API/Controllers/UserController.cs
M	server/server.Application/AppDependencyInjection.cs
A	server/server.Application/Services/Security/AccountSecurityOptionsFactory.cs
M	server/server.Application/Services/Security/BranchBootstrap.cs
D	server/server.Application/UseCases/BranchUsers/Add/AddBranchUserFluentValidation.cs
D	server/server.Application/UseCases/BranchUsers/Add/AddBranchUserMapper.cs
D	server/server.Application/UseCases/BranchUsers/Add/AddBranchUserUseCase.cs
D	server/server.Application/UseCases/Branches/Create/CreateBranchFluentValidation.cs
D	server/server.Application/UseCases/Branches/Create/CreateBranchMapper.cs
D	server/server.Application/UseCases/Branches/Create/CreateBranchUseCase.cs
A	server/server.Application/UseCases/Platform/Bootstrap/BootstrapPlatformOperatorUseCase.cs
M	server/server.Application/UseCases/Platform/OperateBranch/OperateBranchFluentValidation.cs
M	server/server.Application/UseCases/Users/Google/CreateGoogleChallengeUseCase.cs
A	server/server.Application/UseCases/Users/Google/GoogleSignInFluentValidation.cs
M	server/server.Application/UseCases/Users/Google/GoogleSignInUseCase.cs
M	server/server.Application/UseCases/Users/Login/UserLoginUseCase.cs
D	server/server.Application/UseCases/Users/Register/UserRegisterFluentValidation.cs
D	server/server.Application/UseCases/Users/Register/UserRegisterMapper.cs
D	server/server.Application/UseCases/Users/Register/UserRegisterUseCase.cs
M	server/server.Cli/Program.cs
D	server/server.Communication/Requests/RequestAddBranchUserJson.cs
D	server/server.Communication/Requests/RequestUserRegisterJson.cs
D	server/server.Communication/Responses/ResponseAddBranchUserJson.cs
D	server/server.Communication/Responses/ResponseCreateBranchJson.cs
D	server/server.Communication/Responses/ResponseUserRegisterJson.cs
M	server/server.Domain/Interfaces/IGoogleIdentityRepository.cs
M	server/server.Domain/Interfaces/IUsersRepository.cs
M	server/server.Exceptions/ResourcesErrorMessages.Designer.cs
M	server/server.Exceptions/ResourcesErrorMessages.resx
M	server/server.Infrastructure/Repositories/GoogleIdentityRepository.cs
M	server/server.Infrastructure/Repositories/UsersRepository.cs
A	server/server.Infrastructure/Services/GoogleIdentityPolicy.cs
M	server/server.Infrastructure/Services/GoogleIdentityVerifier.cs
D	server/tests/CommonTestUtilities/Requests/RequestAddBranchUserJsonBuilder.cs
D	server/tests/CommonTestUtilities/Requests/RequestUserRegisterJsonBuilder.cs
A	server/tests/UseCases.Test/UseCases/AccountSecurity/AccountSecurityOptionsFactoryTest.cs
A	server/tests/UseCases.Test/UseCases/AccountSecurity/BootstrapPlatformOperatorUseCaseTest.cs
A	server/tests/UseCases.Test/UseCases/AccountSecurity/M8PlatformAndGoogleUseCaseTest.cs
D	server/tests/UseCases.Test/UseCases/BranchUsers/Add/AddBranchUserUseCaseTest.cs
D	server/tests/UseCases.Test/UseCases/Branches/Create/CreateBranchUseCaseTest.cs
M	server/tests/UseCases.Test/UseCases/Users/Login/UserLoginUseCaseTest.cs
D	server/tests/UseCases.Test/UseCases/Users/Register/UserRegisterUseCaseTest.cs
A	server/tests/Validators.Test/AccountSecurity/M8PlatformAndGoogleValidatorsTest.cs
D	server/tests/Validators.Test/BranchUsers/Add/AddBranchUserFluentValidationTest.cs
D	server/tests/Validators.Test/Branches/Create/CreateBranchFluentValidationTest.cs
D	server/tests/Validators.Test/Users/Register/UserRegisterFluentValidationTest.cs
M	server/tests/WebApi.Test/AccountSecurity/AccountSecurityHttpTest.cs
M	server/tests/WebApi.Test/AccountSecurity/GoogleSignInHttpTest.cs
M	server/tests/WebApi.Test/AccountSecurity/GoogleVerifierBoundaryTest.cs
M	server/tests/WebApi.Test/Branches/BranchScopedControllerHappyPathTest.cs
M	server/tests/WebApi.Test/Branches/BranchScopedControllerUnhappyPathTest.cs
M	server/tests/WebApi.Test/Branches/BranchTokenScopeTest.cs
M	server/tests/WebApi.Test/Infrastructure/ServerWebApplicationFactory.cs
A	server/tests/WebApi.Test/Platform/FirstPlatformOperatorCliTest.cs
M	server/tests/WebApi.Test/Platform/OperatedOnboardingHttpTest.cs
M	server/tests/WebApi.Test/Users/UserControllerHappyPathTest.cs
M	server/tests/WebApi.Test/WebApi.Test.csproj
M	shared/docs/milestones.md
```
