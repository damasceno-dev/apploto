# Lotero — Web Milestones

> **Status:** Active
> **Draft started at** 2026-10-14
> **Approach:** Next.js App Router over the backend-owned contract, built screen by screen from the design system

This file is the general milestone list for the `web/` area. Detailed phases with checkboxes are written just-in-time when a milestone starts, following the `server/docs/milestones.md` convention, and executed phase by phase. Cross-area docs reference these milestones as **Web M1**, **Web M2**, …

**Web and mobile share one numbering.** `mobile/docs/milestones.md` uses the same milestone numbers for the same scope, so **Web M5 ∥ Mobile M5** always means the same feature area on both platforms. The foundation (M1–M3) is proven on web first; from M4 on, each same-numbered pair runs in lockstep against one screen blueprint.

### Standing rules

- **The backend owns behavior.** The web app renders what the API returns and calls what the catalog names. It never recomputes balances, variances, due amounts, or permissions the server already decides — preview endpoints exist for exactly that. Money is display-only by default; any unavoidable client arithmetic goes through the integer-cents helpers in `@loto/core`.
- **The screen catalog is the spec.** Every screen is built from its `design/docs/screens.md` entry (data, actions, states, navigation) and its `design/specs/screens/` blueprint (layout). A screen named in the catalog's **gap → screen map** is not built — or is built only outside the gated feature, when the map row carries a feature qualifier — until that gap is resolved and the entry re-synced. The map is authoritative; inline pointers are mirrors.
- **Design system only.** Components follow `design/specs/components/`; styling uses semantic and domain tokens through the web Tailwind preset. Raw hex/rgb literals and primitive-token references fail lint everywhere except the token-adapter module. Light and dark ship together from Web M3 on.
- **Server-first rendering.** Server Components fetch with raw `@loto/api` functions; client components use `@loto/api/queries` hooks. Suspense boundaries and skeletons cover every loading state the catalog names.
- **Thin BFF, auth only.** Route handlers own login, branch session, renewal, logout, and the cookie-to-bearer forwarding of browser calls (see `web/AGENTS.md`). Nothing else goes through them without a recorded, concrete web-only reason.
- **Verbatim errors.** `ResponseErrorJson.ErrorMessages` render as the server sent them, through the shared `ApiError` normalization. No client-side rewording of server errors.
- **Branch clock, not device clock.** Business-day defaults come from `GET /branch/current` and are never passed through device-timezone conversion.
- **Screen definition of done** (every feature milestone, every screen): all catalog states (loading · empty · error · success and the named variants), pt-BR copy per `copy-guidelines.md`, formatting through `@loto/core`, keyboard and screen-reader paths per the design 0.4.2 accessibility patterns, both themes, and an end-to-end test of the screen's **primary job** against a locally running API.
- **Impeccable is a standing refinement step.** After a screen meets its blueprint, run the Impeccable critique/polish pass with the token vocabulary loaded so refinements stay inside the system; changes it suggests outside the tokens or the component contract go back to design, not into code.
- **Standing gates:** `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check`, and the web production build stay green on every commit touching `web/`. Unit tests (Vitest) and end-to-end tests (Playwright) arrive with Web M2 — the first web logic worth testing.

### Area dependency map

- **Shared M1** (workspace, base tsconfig, root lint/format, package skeletons) → Web M1.
- **Shared M2** (generated client, client core with the token-provider interface and `ApiError`) → Web M2. **Shared M3** (`@loto/core` formatters and label maps) → Web M4, the first milestone that renders business data.
- **Design 0.4.2** (cross-screen patterns: session expiry, idempotent submits, uncertain-save outcomes, unsaved input, concurrency, online-only, accessibility) → Web M2 implements the plumbing for those patterns.
- **Design M2** (tokens) → Web M3 Phase 1; **Design M3** (component contract) → the rest of Web M3.
- **Design M4** (per-feature screen blueprints, written just-in-time) → each feature milestone, Web M4–M11.
- **Server gates per feature:** M7.7 Phase 5 (gaps A, H, L, S) gates the fiado and pairing features; server M8 (gaps C, N, U, V) gates Web M11 and the U/V qualified features elsewhere; server M9 aligns with Web M12.
- **Server M11 tripwire delivered.** The manager-queue rename already landed before Shared M2; baseline `f424deb` includes M11 `18012d5`. Web M1 consumes the committed source packages without regeneration.
- **Mobile:** Mobile M1 may run in parallel with Web M1. Mobile M2 and M3 follow the patterns Web M2 and M3 lock. From M4 on, Web MN ∥ Mobile MN.
- **Flow-alignment plan (owner, 2026-10-26; server `milestones.md` *Flow-alignment plan*).** All before release. **Batch B — real cashier day** is Web M4–M5 plus the Batch B increments of M6 (transaction entry: fiado sale, repayment, cofre deposit, inline opening from an operation) and M7 (per-session review and approval) — never Batch C's settlement work — against server M9.6 (sessions, inline opening, blind count/Conferir, handover, close-form entries, separation of duties, fiado boundaries, cofre transfer) and Design M4 Phase 9 (4.9.1–4.9.8); its demonstration is complete seeded journeys in the running app. **Batch C** adds receivables, reconciliation, P&L and shortages to Web M6/M8 against server M9.7 (Design 4.9.9–4.9.12). **Batch D** adds the employee statement, hour bank, special draws, opening count and Access import to Web M8–M10 against server M9.8 and M10 (Design 4.9.13–4.9.15). Ease-of-use principle: the screen supplies known information and asks only for physical observations or decisions the system cannot infer; extra controls appear only on a discrepancy or exception.

### Milestone overview

| #       | Name                                 | Main precondition                                    | Output                                                                |
|---------|--------------------------------------|------------------------------------------------------|-----------------------------------------------------------------------|
| **M1**  | Next.js Scaffold                     | Shared M1                                            | `@loto/web` in the workspace, lint/format/style baseline, compile proof |
| **M2**  | Session & Data Plumbing              | Shared M2, design 0.4.2                              | Auth BFF, API client wiring, query layer, cross-screen pattern plumbing |
| **M3**  | Design System Implementation         | Design M2 + M3                                       | Tailwind token preset, themes, primitive components, `/styleguide`     |
| **M4**  | Entry & Navigation Shell             | Web M2 + M3, Shared M3, Design M4 (shell)            | Login, Branch picker, Gestão ↔ Meu turno shell, route guards           |
| **M5**  | Operator Day                         | Web M4, Design M4 (day close)                        | Operator Day Cockpit, Open day, Close day, Fix & resubmit              |
| **M6**  | Transactions Ledger                  | Web M5, Design M4 (ledger)                           | Create, Edit, list, installment plan, client statement, payment        |
| **M7**  | Manager Work Queue & Approvals       | Web M5 + M6, Design M4 (queue)                       | Manager Work Queue, Daily-close approval                               |
| **M8**  | Reports & Month Lock                 | Web M7, Design M4 (reports)                          | Six management reports including Monthly reconciliation + lock         |
| **M9**  | Time & Self-Service                  | Web M4, Design M4 (time)                             | Clock in / out, Time-entry management, both self-service summaries     |
| **M10** | Admin & Configuration                | Web M4, Design M4 (admin)                            | The nine admin/configuration screens                                   |
| **M11** | Account & Onboarding                 | Server M8, design 0.4.6                              | The auth & account screen set, Plataforma, guided employee flows       |
| **M12** | Release Readiness                    | Server M9                                            | Deployment, CI gates, audits, observability                            |

The M11 report screens (Dashboard, Upcoming settlements, Monthly financial — gap Q) ship in Web M8 now that server M11 shipped and their blueprints exist (Design 4.8.4). The flow-alignment plan adds its screens to M5, M6, M7, M8, M9 and M10 (sections below), all before M12.

---

## Milestone 1 — Next.js Scaffold

**Goal:** Stand up `@loto/web` inside the existing pnpm workspace — Next.js App Router, TypeScript on the shared base config, the web lint/format/styling baseline including the raw-color gate — and prove that the `@loto/api` and `@loto/core` source packages compile in both Server and client components, so Web M2 starts on a working app.

**Scope boundary:** Tooling, configuration, and an app skeleton only. No auth, no API calls, no tokens (Web M3), no screens. Touches nothing under `server/`, `design/`, `shared/`, or `infra/`; root files change only where a phase names them.

**Precondition:** **Shared M1 complete** — `tsconfig.base.json`, the root ESLint and Prettier configs, and the `@loto/api` / `@loto/core` skeletons with their two-entry export map. At the selected baseline `f424deb`, Shared M1 and the Shared M2 source client are delivered; core remains `export {}` until Shared M3. Shared acceptance status remains owned by Shared.

**Key behaviors:**

- **Versions are verified at implementation time** — Next.js, React, Tailwind, and helper libraries at their current stable releases, checked against the official release sources and recorded in the phase notes, as Shared M1 did for Node and pnpm.
- **ESM only**, `"type": "module"`, named exports everywhere except the files Next requires to default-export (pages, layouts, route conventions, config).
- **Source packages, no build step:** `transpilePackages` lists `@loto/api` and `@loto/core`; nothing in `shared/` gains a `dist/`.
- **Configuration is injected by the app.** Web reads its environment in one server-only module; `shared/` never reads `NEXT_PUBLIC_*`. Because browser calls go through the route-handler BFF (Web M2), the API base URL is a server-side variable and never needs a `NEXT_PUBLIC_` copy.
- **No color can drift before the tokens exist.** Tailwind's default palette is cleared and raw color literals fail lint from this milestone on; Web M3 adds the token preset and the primitive-reference ban.

### M1 criterion coverage and verification plan (2026-10-21)

Baseline: clean at `f424deb`; Node 26.6.0 / pnpm 10.34.6. All 15 criteria are independently accepted.

| Criteria | Artifact / behavior | Meaningful verification |
|---|---|---|
| 1.1–1.3 | Package, strict tsconfig, Next source transpilation and typed routes | Install, route type generation, typecheck, production build |
| 1.4–1.5 | pt-BR conventions, boundaries, private environment example, ignores | Real browser home/404, inspect boundary modules, missing/invalid env checks |
| 2.1–2.2 | Next/hooks lint plus named exports and raw-color gates | Positive/negative AST and CSS fixtures, lint CLI negative checks |
| 2.3–2.4 | Empty default palette, cn, CVA dependency, root format contract | CSS build, cn resolution, workspace format check |
| 3.1–3.2 | Server API runtime/type imports, core namespace, client hook/key entry | Production build and browser render; disabled hook/provider context proof without requests |
| 3.3 | Server-only validated URL | Validation tests, build failures, browser asset inspection |
| 3.4, 4.2 | Reproducible source install/build and browser | Frozen clean copy, workspace checks, production browser smoke, Shared drift check |
| 4.1 | App guide + owning milestone evidence | Exact pins, official links, overrides and limitations below |

### Phase 1 — App scaffold

- [x] **1.1** Add `web/package.json`: name `@loto/web`, `"private": true`, `"type": "module"`, scripts `dev`, `build`, `start`, `typecheck` (`tsc --noEmit`), `lint`, `format`, `format:check` matching the root fan-out names; dependencies on Next.js, React, and React DOM at the verified versions; `@loto/api` and `@loto/core` as `workspace:*` dependencies.
- [x] **1.2** Add `web/tsconfig.json` extending `../tsconfig.base.json`, with the Next.js plugin, `jsx: "preserve"`, and the `@/*` → `src/*` alias. Override base module settings only where Next requires it, and record each override with its reason in a comment.
- [x] **1.3** Add `web/next.config.ts`: `transpilePackages: ["@loto/api", "@loto/core"]` and `reactStrictMode`; adopt typed routes if stable in the verified version. No rewrites, middleware, or route handlers yet.
- [x] **1.4** Add the App Router skeleton under `web/src/app/`: root `layout.tsx` with `<html lang="pt-BR">` and Lotero metadata, a placeholder `page.tsx`, `loading.tsx`, `error.tsx`, `global-error.tsx`, and `not-found.tsx`. Route groups (`(auth)`, `(branch)`) are created by the milestones that fill them — no empty scaffolding.
- [x] **1.5** Extend the root `.gitignore` with the Next.js block (`.next/`, `next-env.d.ts`, `.env*.local`, `out/`) and add `web/.env.example` documenting every variable the app reads.

### Phase 2 — Lint, format, and styling baseline

- [x] **2.1** Add `web/eslint.config.mjs` layering Next's flat config (core web vitals) and the React Hooks rules over the root config, plus a named-exports rule with the Next file-convention exemptions.
- [x] **2.2** Add the **raw-color gate**: a lint rule that rejects hex, `rgb()`, `hsl()`, and named-color literals in TS/TSX and Tailwind arbitrary color values (`bg-[#…]`, `text-[rgb(…)]`), with a single exemption path for the future token-adapter module. Prove it with a throwaway violation that fails, then remove it.
- [x] **2.3** Install Tailwind at the verified version with its default color palette cleared, `tailwind-merge`, and one variant helper (choose `class-variance-authority` or `tailwind-variants` in-phase and record why). Add the `cn()` helper in `src/lib/`.
- [x] **2.4** Confirm the root Prettier config applies unchanged to `web/`; adopt Tailwind class sorting only if it fits the root config without per-area divergence, and record the outcome.

### Phase 3 — Workspace integration proof

- [x] **3.1** The placeholder page (a Server Component) imports actual API types/runtime values and the empty runtime namespace from `@loto/core` and renders technical evidence through them — proving server-side source-package compilation.
- [x] **3.2** A small `"use client"` component imports from `@loto/api/queries` and renders inside the page — proving the client entry compiles and stays separate from the server-safe root entry.
- [x] **3.3** Add `src/config/server-env.ts` (`server-only`): reads and validates `LOTO_API_BASE_URL` at startup with a clear failure message; documented in `.env.example`.
- [x] **3.4** From the repo root: `pnpm install`, `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check`, and `pnpm --filter @loto/web build` all green; `pnpm --filter @loto/web dev` serves the placeholder page.

### Phase 4 — Docs sync + close-out

- [x] **4.1** Update `web/AGENTS.md` with the verified version pins, the variant-helper choice, and any tsconfig overrides from 1.2. *(The BFF amendment, token rules, and Impeccable note landed with this milestones file on 2026-10-14.)*
- [x] **4.2** Done criteria: fresh clone → install, typecheck, lint, format check, and build green; the placeholder page renders in pt-BR; a raw color literal fails lint; no token, API call, cookie, or auth code exists yet; nothing under `shared/` gained a build artifact.


### M1 delivered state and verification (2026-10-21)

**Status:** Complete and independently accepted (all 15 criteria).

- Versions/compatibility sources and configuration choices are recorded in `../AGENTS.md`; exact pins live in `../package.json` and `../../pnpm-lock.yaml`. Next/React `16.4.2` / `19.3.1`, Tailwind `4.3.3`, CVA `0.7.1`; the native Tailwind 3 helper major differs intentionally.
- Server Components import real `@loto/api` runtime values and typing; the client entry runs a generated hook with `enabled: false` under the app-owned provider, verifying one query context in SSR and the browser without a request. The Turbopack alias pins the app's React and the build rejects foreign executable React or a second query path in SSR maps.
- Tailwind's color namespace is reset (`--color-*: initial`); client assets contain neither the private environment variable/URL nor default palette variables. The launcher loads env files with `@next/env` and validates the server-only URL before spawning Next; missing/invalid configuration exits 1 for dev/build/start.
- Raw-color gate: hex/RGB/HSL/named colors, arbitrary classes, aliased style values, CSS fallbacks, `@apply`/`@supports` params and standalone exported palettes fail. Outside style contexts only a whole trimmed color value fails, so copy such as `Bolão #123 confirmado` passes; named colors outside styles and mixed values under unrecognized style keys are the accepted trade-off. The only exemption is `src/theme/token-adapter.ts`.
- Verification: frozen install in an isolated copy; recursive typecheck/lint/format; 103 Shared + 6 Web + 7 Mobile tests at the time; production build with bundle identity check; real-browser placeholder/404/`lang="pt-BR"` smoke in production and dev with empty console; `generate:check` (287 files). Loading/error conventions compile but were not fault-injected.
- Install warnings retained: root ESLint 9 is deprecated; pnpm blocks esbuild/unrs-resolver install scripts by default without affecting builds.
---

## Milestone 2 — Session & Data Plumbing

**Goal:** Wire the web app to the API end to end — the route-handler BFF for the two-stage token model, the generated client running on the server and through the cookie-to-bearer layer in the browser, the TanStack Query provider, and the plumbing behind the design 0.4.2 cross-screen patterns — with no product screens yet.

**Scope boundary:** session/data foundation only. No finished Login, Branch picker, shell or business screen (Web M4), no token/component gallery (Web M3), no new IA destination. The gate redirects to the destinations fixed by the [shell blueprint route map](../../design/specs/screens/shell-and-identity.md) (`/entrar`, `/filiais`, role homes `/gestao/fila` and `/turno/hoje`); those screens are built by M4. Minimal technical fixtures exist only in the end-to-end build (below). Touches only `web/` and the root lockfile.

**Precondition:** Web M1 (accepted), Shared M2 (accepted, below), design 0.4.2 authored. Shared M3 is not required: M2 renders no business data, so no local formatter package is created.

### Stale assumptions corrected at the start of M2 (2026-10-21)

- **Logout revocation is shipped.** The earlier text said server-side revocation would arrive with server M8 and that logout would only clear cookies. Server M8 Phases 1–5 (accepted 2026-10-18) ship `POST /user/logout` (identity tier, body `{refreshToken}`, 204, idempotent): it revokes every active refresh token of that token's `SessionId`, which renewal preserves. Logout therefore calls it with the stored refresh token. Already-issued identity and branch JWTs are stateless and stay valid until their `exp` (60 minutes by default); the BFF drops them from the browser at once.
- **Shared M2 is accepted.** Phases 1–5 were independently accepted on 2026-10-18 and landed in `d095262`. This checkout consumes the later committed regeneration (M8 Phase 8 local subset, canonical v57): 123 operations — 10 anonymous, 10 identity, 103 branch.
- **Branch switch keeps device input in its old scope.** "Cleared on branch switch" applies to active state (queries, forms, versions, selections, origin, mode). The catalog (Branch switching; Durable scoped input) says input is persisted in its isolated scope before the old session is dropped and is restorable only after that branch is reselected and its grants rechecked; it is never shown in, or transplanted into, the new branch. M2 follows the catalog.
- **Forwarding design decided:** a same-origin catch-all route handler (`/bff/api/…`) as the client core's configured base URL. The browser's token provider returns `null`.

> **Shared M2 handoff (2026-10-17, S06; independently accepted 2026-10-18, landed `d095262`).** The client contract this milestone consumes, as delivered in `shared/api` (details in `shared/AGENTS.md`):
> - **Server side, per request:** create a client per request with `createApiClient({ baseUrl, getAccessToken })` and pass it as `{ client }` in a raw function's last argument. The process-wide `configureApiClient()` default suits the browser runtime. Each call snapshots its client, so concurrent requests never share a token provider.
> - **Browser through the BFF:** `baseUrl` may be a same-origin prefix such as `/api`, and a provider that returns `null` sends no `Authorization` header. Both forwarding designs named above therefore work without Shared changes; the choice stays in-phase here.
> - **The provider receives `{ method, path, authTier }`** on every call. `authTier` is `anonymous`, `identity` or `branch`, generated from each operation's published `security`. Role names are not part of it.
> - **Failures reject with one `ApiError`:** `kind`, `status`, `method`, `path`, the verbatim `errorMessages`, and `dispatched`. `isOutcomeUncertain(error)` marks the design 0.4.2 "Resultado não confirmado" cases.
> - **Per-call headers:** ordinary headers (for example tracing) may be passed in options. A caller value that conflicts with a generated `Idempotency-Key`, `If-Match` or `Content-Type`, and any caller `Authorization`, is refused before dispatch, so the BFF attaches the bearer itself.
> - **Shared never retries, renews, mints or replaces anything.** `Idempotency-Key` and `If-Match` are required arguments passed through unchanged, `ETag` is readable on raw results, and `toIfMatch`/`parseETag` implement §6.11. Generated hooks set no `retry`; keep mutation retries at 0.
> - `@tanstack/react-query` is a peer dependency (`^5.104.1`), so the app owns the instance and the `QueryClient`.

### Contract inventory (actual code at baseline `93e7d04`)

| Fact | Source | Consequence |
|---|---|---|
| Login → `{UserId, Name, Email, ResponseToken{Token, RefreshToken}}`; renew → new global token + refresh token; branch session → `{Token, Branch{Id, Name, …, Role}}` | `UserController`, `UserLoginUseCase`, `UserRenewTokenUseCase`, `CreateBranchSessionUseCase` | BFF keeps all three tokens; renewal never yields branch authority, so the branch session is recreated with `POST /branch/session` |
| One active refresh token **per user**: every login/renewal deactivates the user's other active tokens; renewal keeps `SessionId` | `RefreshTokenRepository.SaveRefreshToken`, `RefreshToken` | Rotation races lose definitively; a second login (any device/browser) ends this one's renewal; concurrent renewals must be coalesced |
| Refresh lifetime 7 days; JWT lifetime `Token:ExpirationTimeInMinutes` (60); zero clock skew | `SharedValidators`, `JwtTokenService`, appsettings | Cookie max-age 7 days; proactive renewal 30 s before `exp` |
| 401 sources: auth filters (`TOKEN_EMPTY/INVALID/EXPIRED/WITHOUT_USER`), in-action `FreshActor`/`RequireUser` (`TOKEN_INVALID`/`TOKEN_WITHOUT_USER`), wrong current password (`PASSWORD_WRONG`), renew (`REFRESHTOKEN_*`) | `AuthenticationService`, `SecurityRules`, use cases | A 401 is a definite answer thrown before `Commit` in every authenticated use case, but the catalog forbids identifying expiry by message text: reads retry once after recovery, writes are never replayed automatically |
| Wrong token scope → 403 `TOKEN_WITHOUT_PERMISSION`; branch session for a missing/inactive membership → 404 `BRANCH_NOT_FOUND` | `AuthenticationService`, `CreateBranchSessionUseCase` | No renewal for 403/404; 404 on recreation → branch gate |
| Declared headers: `Idempotency-Key` on `POST /transaction`, `/transaction/installment`, `/transaction/fiado-payment`; `If-Match` on transaction update/cancel/finalize, daily-close items, setting update, lock-month | generated `operations.ts` | Forwarded byte for byte; `ETag` returned |
| Credential-bearing or session-ending operations: login, Google challenge/sign-in, renew-token, logout, branch session, invitation register, change password | generated endpoints | Never forwarded: BFF-owned now (login, renew, logout, branch session) or BFF-owned in Web M11 |

### Phase 1 — Plan, inventory and test toolchain

- [x] **1.1** Phases and the acceptance map below are written before code; the stale claims above are corrected.
- [x] **1.2** Exact test pins compatible with the workspace (each published ≥ 14 days before 2026-10-21): Vitest `5.0.3` with Vite `8.3.2` (Shared's pair), jsdom `30.1.1`, Testing Library React `16.3.3` / DOM `10.4.2`, Playwright Test `1.64.0` driving the installed Google Chrome (`channel: 'chrome'`, no browser download). `test` runs the M1 Node tooling tests **and** Vitest; `test:e2e` needs the real stack and is separate.

### Phase 2 — Server session core (`src/server/session/`, server-only)

- [x] **2.1** Cookie model: identity, refresh and branch tokens plus session metadata in `__Host-` cookies, `HttpOnly; Secure; SameSite=Lax; Path=/`, 7-day max-age. Metadata (identity generation, scope generation, returned `UserId`/`Name`/`Email`, branch summary and role from the session response) holds no token and is not authority.
- [x] **2.2** Expiry reading uses only the JWT `exp` claim, server-side, to renew 30 s early; a token is never decoded for identity, role or permission.
- [x] **2.3** One renewal path: a process-wide single flight keyed by the SHA-256 of the refresh token, shared by the proxy and every route handler. Concurrent 401s/expiries coalesce; different users never share a flight; a successful rotation is reused by late holders of the same old token for 15 s (the across-tab peer result); a definitive 401 is final; an unknown outcome (transport/5xx) allows exactly one verification retry, then pauses. Branch-session recreation has its own single flight per identity token and branch.
- [x] **2.4** Generations fence late results: login, logout and branch selection/leave mint or end a generation and tombstone the previous one in-process; a renewal or session response that finishes for a tombstoned generation never writes cookies, so an old credential cannot resurrect a session.
- [x] **2.5** Server data access creates one `createApiClient` per request from that request's cookies (tier → cookie), uses the server-safe `@loto/api` entry and `cache: 'no-store'`; nothing user-specific is placed in the process-wide default.

### Phase 3 — Auth route handlers (`src/app/bff/session/…`)

- [x] **3.1** `POST /bff/session/login`: calls `POST /user/login`; on success sets identity/refresh cookies and resolves memberships (`GET /branch/my-branches`): zero → `no-memberships`, one → auto-open branch session, several → `choose-branch` with the list. Errors pass status, verbatim `ErrorMessages` and `Retry-After` through; the password is never stored, logged or echoed; JSON never carries a token.
- [x] **3.2** `GET /bff/session` returns the snapshot (`anonymous`, `no-memberships`, `choose-branch`, `branch`, `branch-unavailable`, `renewal-unconfirmed`) after proactive renewal and, when needed, branch-session recreation for the same branch.
- [x] **3.3** `POST /bff/session/branch {branchId}` opens a branch for this identity (fresh `POST /branch/session`; that branch's slot only; the branch generation survives an unchanged role; intent resumed only for the same e-mail and a matching branch); switching is navigation into another `/f/{branchId}` namespace — no command ends another tab's branch. *Per-tab context per owner decision D-M2-1 (2026-10-22, below); independently accepted 2026-10-22 for the local single-instance environment (see Independent re-review, below).*
- [x] **3.4** `POST /bff/session/logout` revokes the refresh session through `POST /user/logout` (renewing the identity first when it is expired), clears every session cookie whatever the outcome and reports `serverConfirmed: false` when revocation was not confirmed.
- [x] **3.5** Every BFF request requires the `x-loto-bff` header; state changes also require a same-origin `Origin` (and `Sec-Fetch-Site: same-origin` when sent). All BFF responses are `Cache-Control: no-store, private` and never contain a token.

### Phase 4 — Cookie-to-bearer forwarder (`src/app/bff/api/[...path]`)

- [x] **4.1** The allowlist is generated from Shared's committed operation map (method, template, tier, declared headers) into `src/server/bff/operations.generated.ts`, minus the credential/session operations above; `lint` fails on drift.
- [x] **4.2** Only that table reaches only `LOTO_API_BASE_URL`: strict path segments (no dot, encoded slash/backslash or empty segment), unknown method/path → 404 without dispatch, browser `Authorization` refused, request body capped at 1 MiB, redirects not followed.
- [x] **4.3** Fidelity: forwards `Content-Type`, `Accept`, `Idempotency-Key`, `If-Match` unchanged; returns status, body bytes (every `ErrorMessages` entry) and `Content-Type`, `ETag`, `Retry-After`, `Date`. No hop-by-hop headers, backend `Set-Cookie` or token reaches the browser.
- [x] **4.4** Renewal policy: proactive before dispatch; a forwarded 401 starts one recovery cycle; GET is re-sent once after a successful recovery; a non-GET is **never** replayed — its 401 returns verbatim with `x-loto-session: renewed|lost`. A request refused before forwarding (scope changed, session lost, renewal unconfirmed, CSRF, unknown operation) carries `x-loto-bff-error` and is provably not executed.
- [x] **4.5** Every forwarded call carries `x-loto-scope`; a mismatch with the cookie scope (another tab switched branch or user) is refused before dispatch. Browser cancellation aborts the backend request.

### Phase 5 — Session gate and intent

- [x] **5.1** Route policy from the blueprint route map: anonymous (`/entrar`, `/reset-password`, `/invitation`), identity (`/filiais`, `/plataforma`, `/conta`) and branch families; role home Member → `/turno/hoje`, Manager/Admin → `/gestao/fila`. Mode never changes role; role comes from the branch session (initially) and the live `GET /branch/current`.
- [x] **5.2** `src/proxy.ts` renews proactively and resolves the gate for every gated page request **before** render: anonymous → Login, identity with zero/several branches or a lost branch → Branch picker, one branch → auto-open; refreshed cookies reach the render and the browser.
- [x] **5.3** Server guards (`requireBranchSession`, `requireIdentity`) re-check the gate in layouts before any branch-scoped fetch.
- [x] **5.4** A gated branch deep link is stored as an unexecuted navigation intent (httpOnly cookie, validated same-origin path, bound to the known branch/email), cleared on logout and branch leave, and resumed only after a matching branch session; it is never an API request.

### Phase 6 — Browser data layer

- [x] **6.1** `SessionProvider` is seeded from the server snapshot (no token), re-reads `/bff/session` on BFF session signals, and notifies other tabs through `BroadcastChannel`.
- [x] **6.2** One scoped browser client (`/bff/api`, `null` token provider, scope header) and one `QueryClient` per session scope; ending a scope cancels and clears it and remounts the subtree, so a late response cannot populate another user/branch. Mutation retry 0; reads are not retried on 4xx.
- [x] **6.3** Branch clock: `GET /branch/current` exposes `BranchLocalDate`/`BranchLocalDateTime` as calendar strings and parts, never `Date`; refreshed on scope start, focus, reconnect and after 60 s; reports a branch-day change and the live role.
- [x] **6.4** A not-forwarded `session-lost`/`branch-unavailable`/`renewal-unconfirmed` answer re-reads the session and pauses writes; the gate takes over.

### Phase 7 — Mutation and device-input plumbing

- [x] **7.1** Operation records: `crypto.randomUUID()` key minted once per user intent, payload frozen before dispatch, persisted in the identity/branch partition so retry and reload reuse the same key and bytes; a new intent or a changed payload after a known rejection gets a new key.
- [x] **7.2** Outcome classification: not sent, auth refused, rejected, conflict, rate limited, uncertain (**Resultado não confirmado**) or confirmed. Uncertain keeps key and payload; same-key verification is offered only while the 24-hour window is provable from server `Date` headers (1-hour margin), otherwise reconciliation. No new key or unconditional resend.
- [x] **7.3** `ETag` → `If-Match` round trip with Shared's `parseETag`/`toIfMatch`; a guarded 409 keeps input, shows messages verbatim and offers **Recarregar e conferir**; versions stay in memory only.
- [x] **7.4** Device drafts keyed by hashed returned e-mail + branch + context; refuse passwords, tokens, Google proof and capability values; corruption/unavailability reported, never thrown away silently; clearing touches only its own partition; restoring requires the current verified session and a caller-confirmed fresh read.

### Phase 8 — Verification and close-out

- [x] **8.1** Vitest suites for renewal single flight (bursts, rotation, two users, unknown outcomes), generations/races, gate resolution, forwarder fidelity/CSRF/unknown destinations/no token exposure, scope isolation, idempotency lifetime, stale versions, uncertain outcomes and draft privacy/restoration.
- [x] **8.2** Playwright against a real local .NET API + disposable PostgreSQL + local mail capture from this checkout: login → branch session → guarded read → renewal and renewal failure → logout, two-user isolation, cookies inspected in Chrome.
- [x] **8.3** Workspace typecheck/lint/format/test, Web production build and bundle check, Shared generation drift check; inherited doc-sync result reported separately.
- [x] **8.4** `web/AGENTS.md` records durable decisions; the evidence below records commands and results.

### M2 acceptance map

| Criteria | Behavior / artifact | Meaningful check |
|---|---|---|
| 1.1–1.2 | This plan; `package.json`, `vitest.config.ts`, `playwright.config.ts` | Install, `pnpm --filter @loto/web test`, `test:e2e` |
| 2.1–2.5 | `src/server/session/*`, `src/server/api.ts` | Unit: cookie attributes, single flight, tombstones; e2e: cookie flags in Chrome |
| 3.1–3.5 | `src/app/bff/session/**` | Unit: CSRF, no token in JSON; e2e: login/branch/logout journeys and revocation |
| 4.1–4.5 | `src/app/bff/api/[...path]`, `src/server/bff/*`, `tooling/bff-operations.mjs` | Unit: fidelity, unknown targets, renewal policy; e2e: guarded read, renewal |
| 5.1–5.4 | `src/server/gate/*`, `src/proxy.ts` | Unit: gate matrix; e2e: redirects before render, intent |
| 6.1–6.4 | `src/client/session/*`, `src/client/query/*`, `src/client/clock/*` | RTL: scope isolation, late responses, clock |
| 7.1–7.4 | `src/client/mutations/*`, `src/client/drafts/*` | Unit/RTL: key lifetime, classification, stale 409, draft privacy |
| 8.1–8.4 | Tests, docs | Commands and results below |

### M2 delivered state and verification (2026-10-22)

**Status:** Complete. All criteria, including 3.3 under decision D-M2-1, are independently accepted for the local single-instance environment (one Next instance on localhost, tabs sharing one cookie jar). Multi-instance hosting and real HTTPS stay with M12; no screen reader or touch device was used.

**What the implementation guarantees.**

- *Session authority (2.x, 3.x, 5.x).* One renewal per rotation burst; two users never share a result; a late holder of a rotated token gets the peer result for 15 s, then `stale` (not logout); a definitive 401 is final with verbatim messages; an unknown outcome allows one verification retry, then pauses; a coordination-busy 409 retries once. Session commands are serialized per browser (non-secret `__Host-loto-device` id) and applied by compare-and-swap on the declared scope; superseded commands return 409 `session-scope-changed` without a backend call. A `stale` credential refusal never authorizes. After an unknown renewal or branch-session recreation outcome, writes are refused before dispatch (`renewal-unconfirmed`) while reads may continue. Cookies are `__Host-`, `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`; JSON never carries a token or password; CSRF/415 refusals precede any API call.
- *Forwarder (4.x).* Unknown/BFF-owned operations, unsafe or encoded segments, browser `Authorization`, stale scopes and bodies over 1 MiB are refused before dispatch. Tier bearer, `Idempotency-Key`/`If-Match` only where declared and byte for byte; status/body bytes, `ETag`, `Retry-After` and `Date` relayed; backend `Set-Cookie`/`Server` dropped; `no-store, private`. A GET is recovered once after a 401; a write is dispatched once and its 401 returned with `x-loto-session: renewed`; concurrent 401s make one renewal and one branch-session recreation; a lost upstream write answer is 502 without the not-forwarded marker.
- *Browser layer (6.x, 7.x).* Late responses from an old scope are aborted and dropped; `SessionProvider` moves an authority boundary when a command starts and settles, so older reads never overwrite newer results. `OperationController` sends through its scope-bound client and signal and rechecks disposal/pause after every await. Operation records are accepted only when every recovery field is present, typed, consistent and bound to the same endpoint; otherwise they are quarantined until a deliberate discard. Unreadable storage mints nothing; recovery is explicit (`retryStorage`, `discardCorrupt`, `continueWithoutDeviceRecord`). Navigation intents with credential-like parameter names or JWT-shaped values are rejected. The branch calendar uses no `Date`/device clock.

**Per-tab branch context (D-M2-1, owner decision 2026-10-22: option 2).** Each tab's branch is its address under one namespace for every branch destination; no .NET contract changed.

| Area        | Implementation |
| ----------- | -------------- |
| Routes      | every branch destination under `/f/{branchId}/…` (`src/lib/branch-route.ts`); identity/account/global routes unchanged; unscoped branch paths wait for a chosen branch and resume inside its namespace |
| Credentials | identity cookies plus one `__Host-loto-b-{branchId}` slot per branch (token, summary, branch generation), bound to the opening `sid`, at most 4 (oldest evicted; an evicted tab's slot reopens under the generation it declares); a leftover single branch cookie never authorizes |
| Selection   | the request's route branch (page, layout, server client) or `x-loto-branch` (BFF calls); a slot from another login never counts; a missing slot opens through `POST /branch/session`; the API re-verifies membership and role on every call |
| Scope       | `{identity scope}.{branch generation}` per tab; commands compare the identity part; the forwarder refuses an ended or superseded branch generation (409) |
| Refusal     | a branch the identity cannot open is a 403 page (verbatim messages, link to `/filiais`) and a not-forwarded `403 branch-unavailable` for BFF calls; missing and foreign read alike |
| Client      | identity `SessionProvider` at the root; a `BranchSessionProvider` + nested query scope per `/f/[branchId]` subtree; the deepest mounted scope is the default for generated hooks |

**Boundaries and decisions.**

- Single flights, rotation memory and ended generations are in-process: M12 must choose sticky sessions or a shared store for more than one Next instance.
- The BFF sends no `X-Forwarded-For`; M12 hosting must forward the real client address and list the BFF in `ForwardedHeaders:KnownProxies`.
- Server rendering never renews (it cannot set cookies); the proxy renews first.
- Backend facts: logout returns 204 for an unknown token; API JWTs carry no unique id; issued identity/branch JWTs stay valid until `exp` after logout; a login over an existing session drops the old credentials from the browser without revoking them.
- Deferred to Web M11: BFF routes for Google sign-in, invitation registration and password change.

**Verification summary.** Recursive typecheck/lint/format; Shared 103 + Web 7 tooling + 109 Vitest + Mobile 7 at the time; fresh production build with bundle and client-asset checks; `generate:check` (287 files); real-stack Playwright (`e2e/stack.ts`: disposable PostgreSQL `postgres:16-alpine`, Mailpit `axllent/mailpit:v1.31.5`, this checkout's API with 1-minute tokens, `@example.invalid` identities seeded through supported flows) **20/20** in Chrome, including `e2e/branch-tabs.spec.ts` with several pages of one browser context and the M2 regression suite migrated to the namespace.

Iterating locally: `node e2e/stack-cli.ts start` (from `web/`) prints a state file; export it as `LOTO_WEB_E2E_STATE` to run `npx playwright test` repeatedly against that stack, then `node e2e/stack-cli.ts stop`. Without the variable, `test:e2e` starts and removes its own stack; it never touches `loto-postgres-local`, another worktree's database or a real mailbox.
---

## Milestone 3 — Design System Implementation

**Goal:** Turn the design system into running web code — the Tailwind token preset (light + dark), theme switching, every primitive in the component contract, and the browsable `/styleguide` gallery that `design/docs/milestones.md` (Design M3 note) assigns to the web build.

**Scope boundary / key behaviors:**

- **Token adapter:** a small preset module reads `design/tokens/*.json` at build time, resolves `{path.to.token}` references, and exposes only semantic and domain tokens to Tailwind (CSS variables per theme). It is the single exemption from the color lint gate; the primitive-reference ban lands here.
- **Themes:** light and dark from the same semantic names; system preference by default with an explicit override; no flash of the wrong theme on first paint.
- **Primitives per `design/specs/components/`:** Button, Input, MoneyInput, Select, DatePicker, Checkbox/Switch, Badge (status, domain-token driven), Card, Table/List, Modal/Sheet, Toast, Skeleton, EmptyState, FormField, PageHeader — anatomy, variants, states, and accessibility exactly as specified, plus the `ResponseErrorJson` rendering contract.
- **`/styleguide`:** renders every token and every component state in both themes. Development-only or access-gated in production — decide in-phase.
- **First Impeccable pass** across the primitives, with any out-of-system suggestion returned to design.

**Precondition:** Web M1; Design M2 (tokens) for the adapter and themes; Design M3 (component contract) for the primitives.

### Plan, decisions and boundaries (written before code, 2026-10-22)

Inputs read: tokens and their README (all eight files), the 25-spec component index and every spec, copy and formatting contracts, the shell blueprint and the Claude implementation handoff and measurements, in the committed design snapshot (markers v57). The scope grew with Design M3: the index has 25 families (the 15 above plus SummaryStrip, DetailList, FilterBar, Pagination, Menu, Tabs, Disclosure, Stepper, Notice and SaveStatus), and M3 builds all of them.

- **Tailwind 4 has no JS preset.** The adapter is `src/theme/token-adapter.ts` (pure TS, erasable syntax only). At build time a PostCSS plugin (`tooling/token-css.mjs`, listed before Tailwind's) replaces the single `@loto-tokens;` marker in `globals.css` with the generated CSS: theme variable blocks, an `@theme inline` block naming semantic/domain tokens only, and `@utility` roles. Raw values therefore never exist in a source CSS file (the CSS gate stays exemption-free); they exist only in the adapter's input JSON and in build output. A names-only `src/theme/tokens.generated.ts` (no values) types components and the gallery; `lint` fails on drift.
- **Shared/core boundary.** `@loto/core` is empty (Shared M3: native formatter strategy unmeasured). Formatting stays out of Web: MoneyInput, DatePicker and the masked-identifier Input take a narrow **presentation port** (`src/components/ui/format-port.ts`) with no production fallback; Badge takes its pt-BR label from the caller. Tests use explicit doubles; the gallery uses labelled fixture tables of reference strings, not a formatter.
- **Theme preference** is a non-secret device cookie (`__Host-loto-theme`: `light`/`dark`/`system`), read by the server render to set `data-theme-preference` on `<html>` before first paint; Sistema resolves through CSS `prefers-color-scheme`, so no script decides the first paint. It is never cleared with the session.
- **`/styleguide`** is compiled only in development or with `LOTO_WEB_STYLEGUIDE=1` (`page.styleguide.tsx`, like the e2e fixtures): an ordinary production build contains no gallery route at all.
- **Impeccable** is not installed on this machine (no CLI, package, plugin or skill). No guessed package or remote script is run; Phase 6 does a manual contract-based refinement and records the named-tool pass as not executed.

### Phase 1 — Token adapter and enforcement

- [x] **1.1** The adapter loads exactly the eight files, each with its one expected top-level group, ignoring `$` metadata; it rejects malformed JSON, missing/extra files, bad keys, embedded or broken references, references to groups, cycles, literals in the semantic/domain layers, cross-theme references, theme asymmetry and non-color files reaching color except through a semantic token — each with a named error.
- [x] **1.2** Units map as the README says: type, spacing, sizes and radii px → rem; breakpoints → em; letter spacing em; shadows px with their theme color; motion ms and `cubic-bezier`; layers integers; backdrop opacity per theme; `tabular-lining` → `tabular-nums lining-nums`.
- [x] **1.3** Generated CSS exposes **only** semantic and domain colors (light under `:root`/`light`, dark under `dark`, Sistema through `prefers-color-scheme`), reduced-motion swaps, and Tailwind 4 theme names; Tailwind's default palettes and scales are cleared. No primitive name or value is reachable from app code.
- [x] **1.4** The lint gate also rejects primitive references — token paths, `--…primitive…` variables and palette-ramp classes (`bg-blue-600`, `text-red-700`, …) — in app TS/TSX, CSS and class strings, with negative fixtures; the only exemption stays `src/theme/token-adapter.ts`. Contrast and thresholds stay with the design gate, untouched.

### Phase 2 — Themes and foundation

- [x] **2.1** Claro / Escuro / Sistema, Sistema by default; the explicit choice persists on the device only and syncs across tabs; logout and branch changes never touch it.
- [x] **2.2** First paint is right in server rendering and hydration: the server writes the preference attribute; a system change re-themes live with no reload; no wrong-theme flash and no hydration mismatch.
- [x] **2.3** Foundation: base typography (system stack), focus ring (`sem.focus.ring`, 2px outline/2px offset), selection, `color-scheme`, skip-link layer, tabular money, reduced motion and rem/em scaling; the M1/M2 placeholder pages use tokens only.

### Phase 3 — Input components

- [x] **3.1** Button (primary, CTA, secondary, text/link, danger outline, danger solid; default/small; loading; disabled-but-focusable with reason) and real-link variant.
- [x] **3.2** FormField (stacked, grid, fieldset; helper, inline error, optional marker, read-only reason, unsaved marker) with correct label/description/`aria-invalid` wiring and error-summary focus contract.
- [x] **3.3** Input: text, e-mail, password (Mostrar/Ocultar senha, correct autocomplete), textarea with counter, search with clear and polite count; masked identifier through the port only.
- [x] **3.4** MoneyInput presentation/control contract: blank ≠ zero, digits-only state, right-to-left cents entry and paste through the port, `R$` adornment hidden from AT, numeric keypad, row actions outside the field, read-only amount. **Wiring to `@loto/core` is criterion 7.4.**
- [x] **3.5** Select: native, listbox (combobox pattern), searchable (local over a complete list), no-default; loading/failed/empty options; out-of-options and inactive markers.
- [x] **3.6** DatePicker: date text field plus calendar dialog grid (keyboard), range, day stepper, month, time; calendar values stay `YYYY-MM-DD` strings (no `Date`), today only from the branch clock prop; display names through the port.
- [x] **3.7** Checkbox (incl. indeterminate), Radio group with no default, Switch (immediate device preferences only).

### Phase 4 — Data and layout components

- [x] **4.1** Badge: every domain group and value from the tokens, exhaustively typed against the generated API enums; unknown values neutral; provenance, feedback and mode variants.
- [x] **4.2** Card (section, next action, checklist, sheet), SummaryStrip (totals, explanatory, signed, unavailable), DetailList (identity, provenance, restriction).
- [x] **4.3** Table/List: semantic table with caption and scoped headers, labelled scroll region, whole-row pointer target that ignores text selection and nested controls, explicit row link, grouped list, selection, loading/empty/error/updating.
- [x] **4.4** FilterBar (apply-on-submit, scope summary, Limpar filtros), Pagination (envelope-driven, range text, `aria-current`), PageHeader (breadcrumb / Voltar para, h1 focus on entry, wrapping actions). Navigation props take real hrefs from the app; no permission or total is invented.

### Phase 5 — Overlays, navigation and feedback

- [x] **5.1** Modal/Sheet on the native `<dialog>` top layer: dialog, confirmation, drawer, bottom sheet; focus first-meaningful/contain/return, Escape/backdrop/close through one path, busy not dismissible, inert background, scroll lock; one level, popovers above.
- [x] **5.2** Menu (menu button pattern, roving focus, type-ahead, mode `menuitemradio`, unavailable focusable with reason), Tabs (tabs pattern and segmented radio group), Disclosure, Stepper (confirmed statuses only).
- [x] **5.3** Notice (kinds, error summary with field links, verbatim `ErrorMessages` in order, uncertain result, no-body fallback, assertive/polite), Toast (success only, ≥5 s, pause on hover/focus, one at a time), SaveStatus (every state; real timestamps passed in), Skeleton (busy region, reduced motion) and EmptyState (four kinds).

### Phase 6 — Running `/styleguide` and refinement

- [x] **6.1** Development-only gallery rendering all 25 families with their variants and states, every exposed token in both themes, and keyboard/touch/375px/enlarged-text/reduced-motion/nested-overlay examples; fixture data and controlled scenarios are labelled; no request and no financial mutation.
- [x] **6.2** Refinement pass against the specs and the Claude handoff measurements, with the token vocabulary; any out-of-system suggestion is returned to design. Named-tool (Impeccable) execution recorded as not performed.

### Phase 7 — Integration, tests and close-out

- [x] **7.1** Vitest/Testing Library: adapter resolution and every rejection, the lint negatives, theme, and each family's variants, states, focus and keyboard behavior.
- [x] **7.2** Playwright (no API needed): gallery in both themes, first paint/reload/system change, 375px, 200% text, reduced motion, dialog/menu/tabs/listbox/calendar keyboard flows, nested overlays, row link versus nested actions, plus an automated accessibility scan as a supplement.
- [x] **7.3** Docs: consumption rules, commands and evidence in `web/AGENTS.md` and below; build/bundle checks green.
- [x] **7.4** MoneyInput, DatePicker and masked-identifier wiring to the `@loto/core` formatters and the spoken money value; Badge labels from core's maps. *(Implemented 2026-10-26 over Shared M3; see the criterion map below.)*


### M3 criterion map, verification and status

**Status:** Implementation of 1.1–7.4 is complete and implementer-verified. **Independent acceptance is pending:** the corrected component behavior (field/prop inheritance, disabled and read-only composites, toast budgets, in-dialog pickers at 375 px) and the 7.4 core-wiring corrections (read-only MoneyInput/DateField, calendar reselection, 0001/9999 bounds, progressive identifier deletion) await the same independent reviewer's recheck. Their regressions live in `test/unit/ui/review-round-1.test.tsx` and `review-round-2.test.tsx`.

| Criteria | Artifact | Meaningful check |
|---|---|---|
| 1.1–1.3 | `src/theme/token-adapter.ts`, `tooling/token-css.mjs` (PostCSS, before Tailwind), `src/app/globals.css` marker, `src/theme/tokens.generated.ts` (names only; `generate:tokens`, `lint` drift check) | Vitest against the committed tokens (both themes, references followed, every enum value, units, cleared Tailwind defaults, no primitive in output) and 16 rejection cases on disposable copies; built CSS inspected |
| 1.4 | `tooling/colors.mjs` (`hasPrimitiveReference`), `tooling/check-css.mjs` | Ramp classes, `color.primitive.*`, primitive/palette CSS variables, `@apply`, `@theme`, selectors fail; semantic/domain names and copy pass; root-resolved `var(--color-…)` banned in `src/` |
| 2.1–2.3 | `src/theme/theme.ts`, `theme-provider.tsx`, `theme-control.tsx`, `src/app/layout.tsx`, `globals.css`, `[data-theme-scope]` blocks | jsdom: default Sistema, cookie attributes; Chrome: live system change, correct first HTML with JavaScript disabled, Escuro persists and reaches a second tab |
| 3.1–3.7 | `src/components/ui/{button,form-field,input,money-input,select,date-picker,calendar,choice,format-port}.tsx` | Field context only adds disabled/required/invalid; disabled or read-only composites never open or emit; MoneyInput blank≠zero, digit/paste semantics and spoken value; combobox/searchable keyboard; calendar math without `Date`; Chrome keystrokes |
| 4.1–4.4 | `badge`, `card`, `summary-strip`, `detail-list`, `table`, `filter-bar`, `pagination`, `page-header` | Enum ↔ token exhaustiveness, unknown neutral; unavailable figure never zero; row click vs nested control vs text selection; envelope-driven pagination; h1 focus |
| 5.1–5.3 | `dialog`, `popover`, `menu`, `tabs`, `disclosure`, `stepper`, `notice`, `toast`, `save-status`, `skeleton`, `empty-state` | Focus in/return, one dismissal path, busy blocks; pickers inside a dialog use the popover layer (one `dialog:modal`); toast unread time per message id; verbatim messages in order |
| 6.1–6.2 | `src/app/styleguide/*` (development or `LOTO_WEB_STYLEGUIDE=1` only) | All 25 families and token categories in both themes, 375 px, 200% zoom, reduced motion; manual refinement pass. **Impeccable was not executed** (not installed) |
| 7.1–7.3 | tests, `test:styleguide`, docs | Commands below |
| 7.4 | `src/lib/core-format.ts` (`coreFormat` port for all 13 callbacks), calendar re-exports from core, `AuditInstant`/`useDeviceTimeZone`, gallery **Formatação** section | `core-wiring.test.tsx`; `e2e-styleguide/core-format.spec.ts`; Chrome SSR/hydration with explicit and device zones |

**Latest verification (2026-10-27):** frozen install, recursive typecheck/lint/format; Shared api 103 + core 292, Web 9 tooling + 246 Vitest; production build with bundle check (no `/styleguide` or `/e2e` route, no API URL, cookie names or gallery text in client assets); `test:styleguide` 31/31 with axe-core 4.14.0 WCAG A/AA clean in Claro and Escuro; real-stack `test:e2e` 20/20 on a disposable stack (last run after the first 7.4 corrections; the later IdentifierInput-only change is not used by any e2e flow).

**Not covered / open.** No real screen-reader or physical-touch pass; only Chrome; the browser default-font-size setting was not emulated (200% zoom and a doubled root size were). The indeterminate checkbox carries `aria-checked="mixed"` before hydration (inherent to the native control). The restricted-arithmetic demonstration is gallery-only until Design 4.9.7. Alphanumeric CNPJ stays verbatim and fails validation (Shared M3 open item). Cross-area suggestions for design, not acted on: the Badge spec could describe scoped (pinned-theme) regions, and the shell route map could show the `/f/{branchId}` prefix.
---

## Milestone 4 — Entry & Navigation Shell

**Goal:** The first product screens and the frame every later screen lives in: Login, Branch picker / session, and the navigation shell with the **Gestão ↔ Meu turno** mode switch, Switch branch, and role-based home resolution.

**Scope boundary / key behaviors:**

- Implements `screens.md` → *Navigation & information architecture* verbatim: home resolution by role, `GET /operator/self-context` deciding only whether Meu turno is offered, the one-shot mode switch, the full-page setup explanation for an unlinked Manager/Admin, Switch branch clearing all branch-scoped state, and the route guard that never gates Open day / Close day / Fix & resubmit behind an operator link in Gestão.
- The single-branch auto-open and the zero-branch empty state on Branch picker.
- Login and Branch picker carry gaps C and N (map); this milestone builds their current catalog entries, and the gated additions (recovery, invitation, Google, session menu) land in Web M11.
- Home destinations render as placeholders until their feature milestones land.

**Precondition:** Web M2 + M3, Shared M3, and the Design M4 shell blueprint.

---

## Milestone 5 — Operator Day

**Goal:** The operator's working day: **Operator Day Cockpit** (spine), **Open day**, **Close day**, and **Fix & resubmit** — dual-mode, own work in Meu turno and exceptions entered from Gestão.

**Scope boundary / key behaviors:**

- Workstation-first opening: one terminal binds automatically, several ask once per device and branch-local day, and the multi-terminal strip keeps other open closes visible.
- Close day consumes the review context and `variance-preview` for live variance, autosaves to the server on today's plain Draft, keeps unsaved counts on the device, and routes Submit blockers to the Draft transactions with a return path.
- The cockpit's setup-needed states for an unlinked Member and for a link without a usable account; **Solicitar vínculo** waits for gap V (Web M11).
- **Gated:** Close day's linked fiado movements (gap H, server M7.7 Phase 5) and the dated review events with **Ver o que mudou** on Fix & resubmit, Close day, and the cockpit's P1 (gap X, server M7.7 Phase 7.6). Retained-retired-product edit/preview/save recovery on Close day and Fix & resubmit is also gated (gap AA, server DailyClose/Product; scheduling pending M7.7 close-out triage); the remaining screen behavior and historical reads proceed. Gap A no longer gates: the close shows no Tab balance.

**Precondition:** Web M4 and the Design M4 day-close blueprints. The cockpit's "record a transaction" entry links into Web M6.

**Flow-alignment plan — Batch B (server M9.6; Design 4.9.2–4.9.7):** sessions on shared and separate cashboxes; **inline opening** (*Conferir abertura e iniciar* committed atomically with the first operation; *Abertura diferente*; explicit zero opening); **blind count** — values entered, **Conferir**, then the calculation; **Recontar**; outdated and unconfirmed states; the extra confirmation only when the ledger changed before submit; autosave that never reveals the difference — replacing the live `variance-preview` behavior above; the three close-form fields (*Relatório do terminal*, signed; *Tarifa Bolão*; *Tarifa Especial*); **Passar o caixa** and the incoming acknowledgment with the handover discrepancy; the restricted arithmetic money input (from `@loto/core`, server M9.6 Phase 8). **Demonstration:** seeded journeys — a single-person day, a matching handover, Abertura diferente, separate cashboxes, a ledger change between Conferir and submit — each with cash and card/PIX sales, fiado with a cash and a PIX repayment, a cofre deposit, an expense and a small difference, running against the real API.

---

## Milestone 6 — Transactions Ledger

**Goal:** **Transaction — Create (fast entry)** and **Transaction — Edit (correction / audit)** (spines), Transactions list, Installment (pre-dated cheque) plan, Client statement, and Receber pagamento.

**Scope boundary / key behaviors:**

- Fast entry: type-driven form, automatic non-blocking impact preview, idempotent create, Draft save, and the separate finalize action.
- Edit limited to the contract's editable fields, with the full impact preview, `If-Match` concurrency, and the installment confirm-and-cancel flow.
- Member scope follows linked accounts (`mine` default in Meu turno); return targets follow the route-ownership table, including the three Draft-blocker origins.
- **Gated:** fiado types and their linked movement on Create, `PaidAt` and generated movements on Edit, the **Automática** label on the list, Client statement, and Receber pagamento (gap H); account/Tab routing on Create (gap L) — all server M7.7 Phase 5. The list's type filter (gap Z) and the plan's atomic **Finalizar plano** (gap Y) wait on server M7.7 Phase 7.6.

**Precondition:** Web M5 (Terminal activity needs an open close) and the Design M4 ledger blueprints.

**Flow-alignment plan.** *Batch B (server M9.6; Design 4.9.6):* the fiado sale writes its cashbox movement automatically — the counted-stock choice disappears; repayment method without default (Dinheiro, PIX, configured others); **Depósito Dinheiro** as the two-leg cofre transfer; operations started without an active session go through the inline opening. *Batch C (server M9.7; Design 4.9.9–4.9.10):* cheque custody into the cofre, batch **Depositar**, **Cheque devolvido** → **Reapresentar** / **Converter em fiado**, card settlement batches and **Recolhimento**, with occurrence versus posting dates explained.

---

## Milestone 7 — Manager Work Queue & Approvals

**Goal:** **Manager Work Queue** (spine, exception-first) and **Daily-close approval** (spine, comparison framing).

**Scope boundary / key behaviors:**

- Work Queue from one `GET /report/dashboard` call (or its renamed successor, per the tripwire above): fixed group order, the earlier-days backlog banner with **Ver dia mais antigo**, today literal by branch clock, and status-routed deep links into close flows and the Draft-filtered Transactions list.
- Approval shows opening → closing → variance from the review context, approve / reject with reason, and returns to its origin.
- **Gated:** the self-recorded marker on approval (gap U) and the link-request group on the queue (gap V) — both server M8; the rest proceeds.

**Precondition:** Web M5 and M6 (the flows the queue routes into) and the Design M4 queue/approval blueprints.

**Flow-alignment plan — Batch B (server M9.6 Phases 2, 4, 5; Design 4.9.5):** review per session — sessions side by side, never summed counts; **Aprovar dia do caixa** applying every check to each session; **Aprovar como único gestor** (reason, audited); handover discrepancies — unattributed until the manager attributes them (outgoing, incoming or neither, with a reason) — open custody and the Conferir history in the queue and on approval.

---

## Milestone 8 — Reports & Month Lock

**Goal:** Daily ledger, Fiado balance, Fiado aging, Open-cheque aging, Cash-variance summary, and **Monthly reconciliation + lock** (`POST /setting/lock-month`).

**Scope boundary / key behaviors:** report defaults from the catalog, server-side aggregation only, drill-downs that return to the source report, structured lock blockers that deep-link into their resolution flows. **Gated:** Fiado balance and Fiado aging (gap H, server M7.7 Phase 5).

**Precondition:** Web M7 and the Design M4 report blueprints.

**Also in this milestone:** the M11 Dashboard, Upcoming settlements and Monthly financial report (Design 4.8.4). **Flow-alignment plan.** *Batch C (server M9.7; Design 4.9.11–4.9.12):* statement reconciliation (OFX import, proposed matches, confirm displayed matches only), P&L by financial class and posting date with frozen locked months, shortage tracking, and the per-session/settlement-event revisions of the existing reports. *Batch D (server M9.8; Design 4.9.13):* the employee statement and special-draws catalog.

---

## Milestone 9 — Time & Self-Service

**Goal:** Clock in / out (Member), Time-entry management (Manager/Admin, including their own hours), My transaction summary, and My time-entry balance — own context in Meu turno, manager-selected context from Gestão.

**Scope boundary / key behaviors:** forgotten-punch triage through the `IsInProgress` filter, My transaction summary's automatic share (gated on gap H, server M7.7 Phase 5), the *Sem registro* day labelling derived on the client from the window minus entries, and no Clock in / out tap for Manager/Admin.

**Precondition:** Web M4 and the Design M4 time blueprints. Independent of M5–M8, so it can be scheduled earlier if useful.

**Flow-alignment plan — Batch D (server M9.8 Phase 2; Design 4.9.14):** hour bank — carry-over balance and manager *Pagamento/Ajuste de horas* entries with a reason, kept apart from attendance.

---

## Milestone 10 — Admin & Configuration

**Goal:** Operators, Account assignment, Accounts, Clients (also counter work), Categories & Transaction Types, Products, Holidays, Settings, and Branch members.

**Scope boundary / key behaviors:** Clients keeps the entry mode and stays available to any branch role except deactivate. **Gated:** Account assignment's Tab pairing (gap L) and Accounts' deactivation rule (gap S), both server M7.7 Phase 5; Branch members' own-row guard (gap U) and member addition by invitation (gap C), both server M8. Products' deactivation warning/recovery only (gap AA, server DailyClose/Product; scheduling pending M7.7 close-out triage) is also gated; ordinary Product CRUD and reads proceed.

**Precondition:** Web M4 and the Design M4 admin blueprints. Until this lands, local and staging data come from the backend seeds and the server M8 command-line onboarding.

**Flow-alignment plan.** *Batch B (server M9.6; Design 4.9.8):* cashbox with its paired fiado account by default and its Terminal reference, cofre account type, seeded expense types and repayment methods, Tarifa Especial role, system-owned close-form types, owner setup checklist items. *Batch D (server M9.6 Phase 3, M9.8, M10; Design 4.9.13, 4.9.15):* pay items and effective-dated commission rules; the go-live screens — **Opening count** (server M9.6 Phase 3), **opening fiado balances and opening receivables** (M10 Part A) and **Access import** (M10 Part B: guide, mapping, dry run, confirmation). Batch B journeys use seeded openings.

---

## Milestone 11 — Account & Onboarding

**Goal:** The auth & account screen set that design 0.4.6 catalogs: Invite accept (Criar senha or Continuar com Google), registration via invite, Forgot / Reset password, Profile, Change password, the session menu with server-side Logout, Continuar com Google on Login, the internal **Plataforma** area with the Nova lotérica wizard, the owner setup checklist on Manager Work Queue, the guided Add / Offboard employee flows, and the operator-link requests (gap V) across Cockpit, Work Queue, and Operators.

**Precondition:** Server M8 shipped and design 0.4.6 cataloged; Design M4 blueprints for the set. Closes the C/N/U/V qualified features left open in Web M4, M5, M7, and M10.

---

## Milestone 12 — Release Readiness

**Goal:** Ship the web app: hosting target and deployment pipeline aligned with server M9, CI running the standing gates plus the Playwright suite, an accessibility audit and performance budgets across the spine screens, error tracking, and security headers for the cookie session.

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires — including every flow-alignment batch (B, C, D) and its demonstration. The hosting decision is shared with `infra/` and recorded there.
