# Server M8 — SV10 + SV11 Phases 1–5 delivery

## Baseline, ownership and selected acceptance

2026-10-18: checkout `/Users/fernandodamasceno/Coding/apploto/Project`, `main`, initial HEAD `d095262488523d949032011ec67e6638013acf5f`, initial `git status --short` empty. That commit includes accepted S06 round-3 fixes and the older Claude design candidate; no visual lock follows from inclusion. Stage 0 closes S06 1.6/5.2 using the actual local clone evidence in Shared's existing delivery note. This delivery owns server code/migrations/tests, local-only SMTP capture, canonical docs and semantic product/screens reconciliation, and generated Shared input/raw/query clients. Root dependency/lockfile changes only if technically necessary; none planned. No staging/commit/push/deploy or branch/worktree mutation.

Selected items are M8 1.1–1.4, 2.1–2.5, 3.1–3.5, 4.1–4.4, 5.1–5.5 in `milestones.md`. Complete implementation and meaningful PostgreSQL/HTTP/SMTP/contract checks are required before implementation ticks; 1.4 stays open until independent Claude acceptance. This note records the implementation delivery and its evidence; it does not claim independent acceptance. Phase 6 closes legacy public register and unrestricted branch create together with operated onboarding; they remain available now. Phase 7 implements real Google validation. Phase 8 owns production provider and full launch close-out. Gap C, Google/platform, uncataloged design 0.4.6 and full M8 remain open.

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

Implementation delivered for **1.1–1.3, 2.1–2.5, 3.1–3.5, 4.1–4.4 and 5.1–5.4**. Item **1.4** remains unchecked because independent Claude plan/implementation/semantic acceptance has not happened. Composite **5.5** remains unchecked only for the actual repository documentary gate that depends on 1.4; its completed runtime/export/HTML/regression subcriteria are checked explicitly. All other selected implementation items have current evidence. Stage 0 S06 **1.6/5.2** are checked on the previously committed actual source clone, with historical review evidence preserved in Shared's existing note. Phases 6–8 and whole M8 completion remain unchecked.

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

Source baseline: `d095262488523d949032011ec67e6638013acf5f` plus this uncommitted M8 candidate. Canonical revision: **v53**. Source/landing hash for this new delivery: **pending future authorized commit**; no self-referential hash is asserted. Actual OpenAPI input SHA-256: `fcfe94155a72246a11c0a103c760524f9f80dd1164f61c75351bef4455de15b9` (**87 paths, 118 operations, 186 schemas**; 8 anonymous, 8 identity and 102 branch operations). Login carries required authenticated `UserId`; request self-status has an explicit nullable wrapper; reissue has its intentional optional DTO body. Existing transport numeric/enum/nullability/required header guarantees remain pinned.

Generated tree SHA-256: `a401811614fe34dbb68ff9da383c929e2fd531b46985aa587f9d8c844c43b4e8` over **255 files**. Recipe: sort **relative path strings** under `shared/api/src/generated/` ordinally, emit UTF-8 `<file-sha256>  <relative-path>\n` for each file, then SHA-256 the complete bytes. Actual export/generation commands produced every generated file; no DTO/client was hand-edited. Header boundary has 110 separated merges and zero lossy merges. The root/query split, injected token provider, no automatic retry, typed errors and abort behavior remain green. A fresh clone of this uncommitted candidate is not claimed; the actual-commit fresh clone belongs to Stage 0 only.

### Remaining acceptance, operational risks and handoff

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


## Proposed repository-style commit message (not executed)

```text
Server M8 Phases 1–5 — feat(auth): account security, recovery and invitations

SV10 + SV11: implement M8 1.1–1.3, 2.1–2.5, 3.1–3.5,
4.1–4.4 and 5.1–5.4; deliver 5.5 runtime/contract evidence.
Keep independent acceptance 1.4 and documentary 5.5 close-out open.

Close Shared S06 1.6/5.2 using a clean local fresh clone of actual
source commit d095262488523d949032011ec67e6638013acf5f, preserving
round-3 review history and exact committed input/generated hashes.

Return authenticated UserId on login. Add safe identity profile/name,
confirmed password change and stable refresh-session logout. Reject
self-membership role/removal separately from last-active-Admin rules.
Serialize mutable branch/account authorization and refresh operations
inside PostgreSQL transactions, rechecking queued actors and tokens.

Add hashed, single-use UTC recovery and invitation capabilities;
normalize email consistently with PostgreSQL and preflight legacy
collisions. Revoke every active refresh session atomically on password
change/reset. Backfill stable refresh-session ids and retain inactive
rotation history so logout also revokes a racing successor.

Provide an explicit MailKit SMTP boundary, bounded volatile recovery
queue, truthful post-commit send outcomes and isolated loopback Mailpit.
Use configured trusted links, target/IP throttles and no token logging,
durable plaintext outbox, automatic mail retry or real recipients.
Pin the newly introduced PKCS dependency to patched 10.0.13.

Persist eligible Member operator-link requests, unique pending state,
bounded Manager/Admin reads/resolution and the Work Queue count.
Return actual Terminal-grant setup state without implicitly assigning
accounts. Add role-scoped invitation issue/list/preview/reissue/decline,
correct-identity acceptance and atomic new-user membership/pre-link.
Allow safe preselection replacement on reissue; invalidate old tokens.

Apply the M8 migration on fresh/upgraded disposable PostgreSQL and
verify collision/commit rollback, single-use and authorization races,
operator competition, removal history and real API-to-local-SMTP flows.
Keep money/FIFO rules and the existing role matrix intact.

Reconcile the canonical three-file group to v53 and product/screens
semantics for shipped N/U/V contracts. Keep design markers at v52
pending independent acceptance; C, operated onboarding, actual public
bootstrap closure, Google and production provider remain future scope.
Regenerate actual OpenAPI/raw/query clients through accepted S06 tools;
preserve server-safe imports, token/header/error/abort/no-retry contracts.

Validation:
- actual d095262 fresh clone: frozen install, recursive checks, 91 tests,
  generation/export from both directories; clean clone after checks
- solution build: zero errors; six pre-existing migration-test warnings
- Validators 344/344; UseCases 1148/1148; WebApi/architecture 1215/1215
- targeted password/session mutations: 13/13 killed, 100%, Stryker 5.0.0
- real PostgreSQL migration/race/rollback and actual local SMTP scenarios
- no pending EF model changes; numeric/auth/enum/nullability snapshots
- frozen pnpm install; recursive typecheck/lint/format; Shared 101/101
- both-directory generation/export checks; default export stays disabled
- input fcfe94155a72246a11c0a103c760524f9f80dd1164f61c75351bef4455de15b9
- 255-file tree a401811614fe34dbb68ff9da383c929e2fd531b46985aa587f9d8c844c43b4e8
- both canonical HTML scripts parse; 34 checker regressions pass in
  disposable metadata-aligned fixtures; git diff --check passes
- actual doc-sync/regression baseline intentionally fails v53/v52
  until independent semantic review and documentary marker close-out

No full-M8, launch, visual, Google or production-provider acceptance.
New landing hash remains pending a separately authorized commit.
```
