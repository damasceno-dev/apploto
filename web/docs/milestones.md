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

Post-MVP report screens (KPI page, upcoming settlements, consolidated monthly report — gap Q) get their own milestone once server M11 is scheduled and the catalog entries exist.

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

Baseline: coordinator-selected worktree, clean at `f424deb`; Node 26.6.0 / pnpm 10.34.6. Implementation checkboxes denote implementer verification; round 3 independently accepted all 15 Web M1 criteria.

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


### M1 implementation evidence (2026-10-21; independently accepted)

All 15 selected Web M1 criteria (1.1–1.5, 2.1–2.4, 3.1–3.4 and 4.1–4.2) are independently accepted by the same reviewer in round 3. All findings are confirmed fixed. **Web M1 passes; combined integration still requires the inherited doc-sync gate to be reconciled by its owners.**

- Versions/compatibility sources and configuration choices are recorded in `../AGENTS.md`; exhaustive exact pins live in `../package.json` and `../../pnpm-lock.yaml`. Next/React `16.4.2` / `19.3.1`, Tailwind `4.3.3`, CVA `0.7.1`; root Node/pnpm/TS pins and package boundaries are unchanged.
- `@loto/api` supplies real runtime values (`Role`, `createApiClient`) and actual `ApiClient` typing in a Server Component. Core is an imported **runtime namespace**, rendered with `Object.keys(core)`, with zero exports as committed. This proves source-module consumption, not a formatter/helper implementation; the empty module can disappear from optimized source maps. The client entry executes a generated hook with `enabled: false` under an app-owned provider and its pure query-key builder. This now verifies query-context identity in SSR and the browser without invoking a request.
- Tailwind's color namespace is reset with `--color-*: initial`. Final client assets contain neither the private environment variable name/test URL nor the default red palette variable. The native Tailwind 3 helper major differs intentionally from Web's Tailwind 4 helper major.
- The launcher uses Next's official `@next/env` to load environment files, then imports the same server-only TS module under Node's server condition **before** spawning Next. Missing and invalid configuration both exit 1 for dev/build/start before binding a port. The controlled valid URL was `http://127.0.0.1:59999`; no backend was started/contacted. `allowImportingTsExtensions` is required by that Node source import; base strictness/module settings stay intact. Automatic framework edits to the canonical guide are disabled with `agentRules: false`.
- Actual browser smoke: root placeholder, metadata/title, `lang="pt-BR"`, missing route and return-to-home passed on production, including the clean copy; dev placeholder also passed. Browser warn/error logs were empty. Loading/error/global-error conventions compile in the production build; they were not fault-injected in a browser and are not claimed as fault-injection evidence.
- Raw-color tests reject hex, RGB/HSL, named colors, arbitrary classes, aliased style values, CSS variable fallback colors and configuration palettes. Ordinary text, labels (including color-label field names), fragments, URLs and dimensions pass. Actual ESLint/CSS CLI negatives and a default re-export negative failed as expected in disposable fixtures; fixtures were removed. The future token-adapter exemption is exactly `src/theme/token-adapter.ts`.

| Check (from workspace root unless stated) | Result |
|---|---|
| `pnpm install`; isolated source copy → `pnpm install --frozen-lockfile` | Passed; no pre-existing node_modules/build/native output used in the initial clean copy |
| `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check` | Passed; both apps and both Shared packages participated |
| `pnpm -r test` | Passed: 103 Shared API tests, 6 Web tooling tests, 7 Mobile tooling tests; no fake empty test runner |
| `LOTO_API_BASE_URL=http://127.0.0.1:59999 pnpm --filter @loto/web build` | Passed here and in isolated installation |
| `pnpm --filter @loto/web dev/start --hostname 127.0.0.1 --port <unused-port>` with explicit URL | Real browser checks passed; only task-owned servers were stopped |
| `pnpm --filter @loto/api generate:check` | Passed: 287 generated files match; no generation/source mutation in Shared |
| Shared root import boundary | Existing gate passed: 274 reachable modules, no React/TanStack imports |
| Node public/private export resolution; raw-color/export-convention negatives | Public entries resolve to TS source; private entries rejected; negative fixtures removed |
| `git diff --check` | Passed |
| `bash docs/check-loto-doc-sync.sh` | **Inherited failure:** canonical v56 versus design v55; no marker/docs edits by this assignment |

Install warnings retained: root-pinned ESLint 9 is deprecated; pnpm blocks esbuild/unrs-resolver install scripts by default. Actual builds/lint/tests succeeded without enabling those scripts or relaxing engine/peer policies. Final installation has no app-induced unmet peer warnings. No server test suite or OpenAPI export was required/run; generation drift was checked against committed input only.

Changed ownership: `web/`, `mobile/`, root app-output ignore additions and the dependency lockfile. No edits under Shared/server/design/infra/canonical docs, no root package/config/toolchain changes, no staged files or commits. Independent review and the server/design-owned semantic reconciliation remain separate from the implemented Web M1 criteria. Lockfile impact: new Web/Mobile importers and framework graphs; the existing Shared API Vite/Vitest resolution also gains the optional Terser peer (`5.51.2`) now present through app tooling. Shared direct dependency specs/pins and source are unchanged; its 103 tests and generation drift check passed with this graph.

### Independent review round 1 — corrections independently confirmed

| Finding | Implementer correction and evidence | Acceptance |
|---|---|---|
| P2-1 | App-relative Turbopack alias selects `_react@19.3.1`. `build` now asserts exactly one app-owned query path in SSR maps and rejects foreign executable React in maps/page trace. A permanently disabled generated hook under Web's own provider passes prerender and real browser hydration in production and development. Metadata-only package traces are distinguished from runtime files; Next's framework React aliases remain intact. | Independently confirmed fixed in re-review |
| P2-2 | Standalone exported hex/function palettes fail; the round-2 refinement below limits ordinary strings/templates to an entire trimmed color value. Named colors stay context-dependent, URLs/fragments remain valid, CSS at-rule params (`@apply`, `@supports`) are checked. The TS gate includes app TS/TSX outside src; CSS scanning covers the app root while skipping only generated/dependency root directories. New negative fixtures pass; actual two-file imported-palette/class/style CLI cases fail at the literal definition. | Independently confirmed fixed in re-review |
| P4 | Duplicate diagnostics removed with range containment; dependency keys sorted. | Independently confirmed fixed in re-review |

A new disposable source copy installed with the frozen lockfile. Root gates pass with 103 Shared + 6 Web + 7 Mobile tests. Fresh Web production build passes its output assertion; browser shows `sem requisição`, `lang="pt-BR"`, and empty warning/error logs. Explicit colors in standalone palettes and CSS `@apply` now fail real CLI checks in both apps. No requests/backend configuration beyond the inert URL are used. No dependency pins or lockfile changes were needed in this correction round. Native blockers and the inherited doc-sync v56/v55 failure remain unresolved external prerequisites; the app correction patch was independently accepted in round 3.

### Independent re-review — round-2 P3 accepted in round 3

The reviewer confirmed all round-1 findings fixed, then found hex-like reference numbers rejected inside ordinary copy. Outside styles/arbitrary classes, the gate now requires the entire trimmed string to be one explicit hex color or one color function. Style contexts and CSS retain substring detection. Both app suites include passing `Bolão #123 confirmado` and `aria-label="Excluir lançamento #abc"` fixtures, plus template/function-copy cases; standalone/trimmed palettes and composite style colors still fail. Named colors outside styles and imported values retain the documented trade-off: forbidden explicit palette literals fail at their definitions without import evaluation.

Round-2 validation: root `pnpm typecheck`, `pnpm lint`, `pnpm format:check` and `pnpm test` passed (103 Shared + 6 Web + 7 Mobile tests). Actual ESLint CLI checks using app configuration accepted both copy examples and rejected standalone/trimmed hex, color functions, composite style values and arbitrary color classes in both apps. `git diff --check` passed. Runtime/build evidence above was independently rechecked before this tooling-only correction; unchanged browser/native bundles were not rerun. The same reviewer independently confirmed this correction in round 3. No native evidence, dependency pins, lockfile or inherited doc-sync markers changed in this round.

### Round-3 acceptance and commit readiness

The same reviewer verified the correction in a fresh isolated copy: frozen install without unmet peers; root typecheck/lint/format and all 116 tests; Web production build with app-owned bundle identity and missing-URL failure; iOS/Android exports with bundle assertions; Shared generation matching all 287 files; clean diff checks and no personal paths. No native execution or Hermes readings were substituted by these checks.

All findings are fixed. The suggested broader detection of mixed values under custom style-key names is optional and non-blocking; whole-value detection outside recognized styles is the accepted trade-off. This app foundation is ready to commit with native criteria explicitly open. Combined integration remains blocked by inherited spec v56/design v55 doc-sync reconciliation; it is not implied by app acceptance. No staging or commit was performed.

---

## Milestone 2 — Session & Data Plumbing

**Goal:** Wire the web app to the API end to end — the route-handler BFF for the two-stage token model, the generated client running on the server and through the cookie-to-bearer layer in the browser, the TanStack Query provider, and the plumbing behind the design 0.4.2 cross-screen patterns — with no product screens yet.

**Scope boundary:** session/data foundation only. No finished Login, Branch picker, shell or business screen (Web M4), no token/component gallery (Web M3), no new IA destination. The gate redirects to the destinations fixed by the [shell blueprint route map](../../design/specs/screens/shell-and-identity.md) (`/entrar`, `/filiais`, role homes `/gestao/fila` and `/turno/hoje`); those screens are built by M4. Minimal technical fixtures exist only in the end-to-end build (below). Touches only `web/` and the root lockfile.

**Precondition:** Web M1 (accepted), Shared M2 (accepted, below), design 0.4.2 authored. Shared M3 is not required: M2 renders no business data, so no local formatter package is created.

### Stale assumptions corrected at the start of M2 (2026-10-21)

- **Logout revocation is shipped.** The earlier text said server-side revocation would arrive with server M8 and that logout would only clear cookies. Server M8 Phases 1–5 (accepted 2026-10-18) ship `POST /user/logout` (identity tier, body `{refreshToken}`, 204, idempotent): it revokes every active refresh token of that token's `SessionId`, which renewal preserves. Logout therefore calls it with the stored refresh token. Already-issued identity and branch JWTs are stateless and stay valid until their `exp` (60 minutes by default); the BFF drops them from the browser at once.
- **Shared M2 is accepted.** The handoff note below said it awaited review; its round-3 review accepted Phases 1–5 on 2026-10-18 and the close-out landed in `d095262`. This checkout consumes the later committed regeneration (M8 Phase 8 local subset, canonical v57): 123 operations — 10 anonymous, 10 identity, 103 branch.
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

### M2 implementation evidence (2026-10-21; review rounds 1–2 corrected and D-M2-1 implemented 2026-10-22; accepted locally, single instance)

Baseline: worktree `Project-app`, branch `paralell/app`, clean at `93e7d04` (Web M1 `05ed70a`, Server M8 Phase 8 local subset `452eef8`); Node 26.6.0 / pnpm 10.34.6, .NET SDK 10.0.201, Docker, Google Chrome. Ticks above mean **implemented and checked by the implementer**; none is independently accepted yet. Criterion 3.3 was blocked by decision D-M2-1 until the owner decided it on 2026-10-22; it is now implemented (see *D-M2-1 resolution* below). No criterion is blocked; none is accepted until the independent re-review.

**What the checks prove (and what they do not).**

- *Session authority (2.x, 3.x, 5.x).* Unit suites drive the real session core against a programmable fake API: 25-request renewal bursts make one rotation; two users renewing at once never share a result; a late holder of a rotated token gets the peer result for 15 s and afterwards a `stale` (not a logout); a definitive 401 is final with verbatim messages; an unknown outcome allows exactly one verification retry, then pauses; a coordination-busy 409 is retried once and never treated as loss. Race tests hold a renewal in flight and run logout, another login or a branch switch against it: logout shares the rotation and revokes it, and the late renewal writes nothing; a switch keeps only the identity rotation; a request with an ended generation is refused unrenewed. Route-handler tests through real `NextRequest`/`NextResponse` check every cookie's `__Host-`/`HttpOnly`/`Secure`/`SameSite=Lax`/`Path=/` attributes, deletion attributes, round-tripping of a `+/=` refresh token, verbatim 429 + `Retry-After`, CSRF/415 refusals before any API call, and JSON without token or password.
- *Forwarder (4.x).* Unknown operations (including every BFF-owned one), unsafe/encoded segments, protocol-relative and dot-segment escapes, a browser `Authorization`, a stale scope and a body over 1 MiB are refused before dispatch. Fidelity: tier bearer, `Idempotency-Key`/`If-Match` only where declared and byte for byte, no cookie/forwarding/hop-by-hop request headers, status and body bytes with duplicated `ErrorMessages`, `ETag`/`Retry-After` relayed, backend `Set-Cookie`/`Server` dropped, `no-store, private`. A GET is recovered once after a 401; a write is dispatched exactly once and its 401 returned verbatim with `x-loto-session: renewed`; a second 401 ends at the branch gate; twelve concurrent 401s make one renewal and one branch-session recreation; a lost upstream answer to a write is 502 **without** the not-forwarded marker; cancellation reaches the API request.
- *Browser layer (6.x, 7.x).* jsdom tests: a late response from user A/branch 1 never appears after the switch to user B/branch 2 (the old request is aborted and carried the old scope; no `Authorization` header, `credentials: same-origin`); a burst of session signals makes one `/bff/session` re-read; device input is locked without a branch session, unread until authority is rechecked, restored for the same returned e-mail after re-login and invisible to another person or branch. Pure suites cover key lifetime (one key per intent; same key and bytes through uncertainty, retry and reload; `pending` after reload → uncertain; changed payload while unresolved refused; new key only after a known rejection; 409 keeps the original; resend only while the window is provable from server time; paused session sends/stores nothing; unavailable storage reported), outcome classification, guarded writes (exact `If-Match`, stale 409 verbatim with reload path, no invented version, a BFF scope refusal is not a stale version), draft privacy/corruption/partition clearing, and the branch calendar (no `Date`/device clock in its sources).
- *Real stack (8.2).* `e2e/stack.ts` starts disposable PostgreSQL (`postgres:16-alpine`) and Mailpit (`axllent/mailpit:v1.31.5`) containers on free loopback ports, builds and migrates this checkout's API, starts it with 1-minute tokens, seeds only through supported flows with `@example.invalid` identities (first-operator CLI → captured setup link → reset-password; operated onboarding of three branches; invitation register/accept; an Admin-issued Member invitation; a bank account), builds the end-to-end Next output and serves it on `http://localhost`. Chrome checks: the gate redirects before render with no forwarded request and keeps the deep link; login yields host-only `HttpOnly`/`Secure`/`SameSite=Lax` cookies that `document.cookie`, storage, HTML and the login JSON never expose; zero/one/several branches; identity read before a branch exists; server and browser read the same branch clock without a browser bearer; a refused branch token recovers (`renewed`); proxy and forwarder renew after the margin; 15 concurrent reads after expiry all succeed and the replaced refresh token is then refused by the API; a server-revoked session plus expired tokens ends at Login with cookies cleared and the intent kept, bound to Alfa (not resumed in Beta); **Sair** is confirmed and the API then refuses its refresh token; two browsers stay isolated, and a user change in one browser drops the old scope (a request with it gets `409 session-scope-changed`); a same-user branch switch drops the branch cookie and gates branch routes; cross-origin/header-less BFF calls and unknown targets are refused while the session survives; `PUT /setting` round-trips `ETag`→`If-Match`, and a stale save shows exactly the API's own 409 messages with input kept and a working reload path; a `POST /transaction` whose response is lost after commit keeps key and bytes across a reload, the explicit verification replays it (same key and body, one row), and the next intent uses a new key; device input survives reload and re-login for the same person only, and storage holds no token or raw e-mail.
- *Not covered by checks:* multi-instance deployment (see boundary below), real HTTPS hosting, a browser other than Chrome, and the visual/assistive behavior of screens that do not exist yet (M4).

**Decisions and limitations to review.**

- **Per-tab branch context (D-M2-1, decided 2026-10-22).** Each tab's branch is its `/f/{branchId}` address; the earlier one-branch-per-browser model and its single branch cookie are gone (a leftover `__Host-loto-br` is cleared and never authorizes).
- **Process boundary.** Single flight, peer results, rotation memory and generation endings are in-process. Two Next instances behind a load balancer would race rotations and could resurrect ended generations; M12 must choose sticky sessions or a shared store.
- **Forwarded client address.** The BFF sends no `X-Forwarded-For` (a route handler has no trustworthy peer address, and passing the incoming header would let clients spoof it). Behind the BFF every login shares the BFF's network partition in the API's abuse budgets. M12 hosting must forward the real client address and list the BFF in the API's `ForwardedHeaders:KnownProxies`.
- **Server rendering never renews** (it cannot set cookies); the proxy renews first. A 401 in a server read redirects to the gate without clearing cookies; the proxy settles them on the next request.
- **Backend facts observed:** logout returns 204 for an unknown token (the first harness run sent a percent-encoded cookie value and "revoked" nothing — the helper now decodes); API JWTs carry no unique id, so a token re-minted within the same second is byte-identical; already-issued identity/branch JWTs stay valid until `exp` after logout. A login over an existing session drops the old credentials from the browser without revoking them server-side.
- **Deferred to Web M11:** BFF routes for Google sign-in, invitation registration and password change (their responses carry or end credentials, so the forwarder refuses them).

| Check (from the repository root unless stated) | Result |
|---|---|
| `pnpm install --frozen-lockfile` at baseline; then `pnpm --filter @loto/web add -D -E …` | Passed; Web devDependencies added; the lockfile also records optional-peer variants for Next (`@playwright/test`, `@babel/core`) and Vitest (`jsdom`) — one version of each package |
| `pnpm -r typecheck` (Web also checks `tsconfig.test.json`: tests, e2e, configs) | Passed |
| `pnpm -r lint` (Web: ESLint, CSS raw-color gate, BFF allowlist drift) | Passed |
| `pnpm format:check` | Passed |
| `pnpm -r test` | Passed: Shared 103; Web 7 tooling + 109 Vitest (11 files; 83 before review round 1, 104 after it); Mobile 7 |
| `LOTO_API_BASE_URL=http://127.0.0.1:59999 pnpm --filter @loto/web build` (fresh `.next`) | Passed with the bundle check (one app-owned TanStack/React); routes `/bff/session{,/login,/branch,/logout}`, `/bff/api/[...path]`, Proxy; no `/e2e` route; client assets contain neither the API variable/URL nor a cookie name |
| `pnpm --filter @loto/web test:e2e` (own disposable stack, Chrome) | Passed: 13/13 in Chrome after review round 2 (round 1 added restored pre-Sair cookies; round 2 a session read held across a login); containers and processes removed afterwards |
| `pnpm --filter @loto/api generate:check` | Passed: 287 generated files match; Shared untouched |
| `git diff --check`; personal-path scan of changed files | Passed |
| `bash docs/check-loto-doc-sync.sh` | **Inherited failure** (lane B): design markers v55 against canonical v57; no marker or canonical doc edited here |

### Independent review round 1 (2026-10-22) — six fixes confirmed in round 2; two corrected again below

The reviewer reproduced all gates and the 11 browser tests, then found nine defects with probes the suite lacked. Each fix below has a regression test of the reproduced scenario; none is self-certified as accepted.

| Finding | Correction | Regression check |
|---|---|---|
| P1 — an old financial intent could dispatch under the replacement scope | `OperationController` sends through the scope-bound client it was created with (`options.client`) and its own abort signal; disposal and pause are rechecked after every await; `useIdempotentOperation` disposes the controller when the scope ends or the component unmounts. Raw writes use `useScopedApiClient()` for the same binding. | jsdom: Ana/Alfa submit held at the server-time read, scope replaced by Bia/Beta, released → `cancelled`, no `/bff/api/transaction` request; controller: send gets the scoped client and a signal, disposal mid-await sends/stores nothing, disposal aborts an in-flight request (record stays uncertain in its partition), pause during verification does not resend |
| P2 — late login/logout bypassed generation fencing | Session commands are serialized per browser (non-secret `__Host-loto-device` id, minted on the first page/BFF response) and applied by compare-and-swap: the scope the browser declares (`x-loto-scope`) must equal the scope its last committed command left (or, unseen, its cookies' scope); otherwise the command is superseded (409 `session-scope-changed`, no backend call, no cookie). `GET /bff/session` and the proxy are serialized too. Scope changes outside commands (a branch found gone) update the device record. | Route tests with a real cookie jar: login held at the API while Sair is pressed → Sair waits and is superseded, no revocation, Bia stays signed in; Sair first then the older login → login superseded, no cookie; a login carrying pre-Sair cookies/scope → superseded; first-time browser gets an `HttpOnly; Secure` device cookie |
| P2 — ended generations passed the page gate | A `stale` credential refusal never authorizes: ended identity → `anonymous`; ended scope → `choose-branch` with no scope or branch; superseded rotation → `renewal-unconfirmed`; nothing is written. | Service: after Sair the old cookies resolve to `anonymous` and `decideGate` redirects to `/entrar`; ended scope carries no branch; rotation-stale reads as unconfirmed |
| P2 — a late session read restored logged-out client state | `SessionProvider` bumps a command epoch on every session command, aborts the pending re-read, and applies a read or command result only if no newer command started; commands send the scope they were issued against. | jsdom: read held, Sair completes, old `branch` response released → stays `anonymous` |
| P2 — unconfirmed renewal still allowed writes on an unexpired bearer | `ensureCredentials(…, { write })`: after an unknown renewal (or branch-session recreation) outcome, a state-changing request is refused before dispatch (`renewal-unconfirmed`) even when the old token is still valid; reads may use it. | Service and forwarder: identity 10 s from expiry, renewal response lost → GET forwarded, POST refused with no dispatch |
| P2 — unreadable operation storage could replace an unresolved key | `unavailable`/`corrupt` storage quarantines the controller: submit and verification return `storage-unreadable` and mint nothing. Recovery is explicit: `retryStorage`, `discardCorrupt` (after reconciling) or `continueWithoutDeviceRecord` (memory-only, `durable: false`). | Uncertain record, reads failing → same payload refused, no new key; storage back → verification resends the original key; corrupt record blocks until discarded; memory-only path is explicit and non-durable |
| P2 — per-tab branch context replaced without approval | **Not implemented; blocked by decision D-M2-1** (below). Criterion 3.3 is unticked for the per-tab rule. | — |
| P2 — navigation intents retained secret query values | `validateIntentPath` rejects the whole intent when a parameter name could carry a credential (`token`, `secret`, `password`/`senha`, `credential`, `signature`, `auth…`, `code`, `state`, `nonce`, `key`, …) or any value is JWT-shaped; ordinary filters pass. | Route policy: `?token=`, `access_token`, `code`+`state`, `senha`, JWT value, `X-Api-Key` rejected; date/account/status/page filters kept |
| P3 — the forwarder dropped the backend `Date` | `Date` joins the relayed headers (replay windows now use the API's clock). | Forwarder and real route handler: the API's `Date` arrives unchanged |

Round-1 validation (2026-10-22, from the repository root): `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check`, `pnpm -r test` (Shared 103, Web 7 + 104, Mobile 7), fresh Web production build with the bundle check and the client-asset scan, `pnpm --filter @loto/api generate:check` (287 files), `git diff --check` and `pnpm --filter @loto/web test:e2e` (12/12, own stack) all passed. Two browser-test assertions changed with the fixes, not to hide them: the session-cookie helper excludes the intended device id (its attributes are asserted separately), and tests wait for Sair's result before the next command, since a command issued against the pre-Sair scope is now correctly superseded.

### Independent review round 2 (2026-10-22) — corrections accepted in the re-review below

The reviewer confirmed six round-1 findings fixed (cross-scope financial dispatch, login/logout command fencing, stale page-gate authority, writes after unknown renewal, credential-bearing intents, backend `Date`), confirmed that storage read failures and invalid JSON block correctly, and kept D-M2-1 open. Two findings were only partly fixed:

| Finding | Correction | Regression check |
|---|---|---|
| P2 — a session read started *during* a command could overwrite its result | `SessionProvider` now moves an authority boundary when a command starts **and** when it settles: reads in flight at either point are aborted and ignored. Only the newest command applies its own result; a command whose result is not applied (refused, superseded) reads the session afresh after settling. | jsdom: read started while a login is at the API, login settles (`branch`), the older read then answers `anonymous` → UI stays `branch`; a superseded login triggers a fresh read that shows the current state. Chrome (real stack): a `GET /bff/session` that reached the BFF while signed out, delivered only after a successful login, leaves the UI signed in |
| P2 — a parseable but incomplete operation record bypassed recovery | `isOperationRecord` accepts a stored record only with every recovery field present, typed and consistent (`v`, `operation`, a 1–128 visible-ASCII `key`, parseable `payloadJson`, a known `status`, finite-or-null `firstDispatchAfterMs`, integer `dispatches ≥ 1`, boolean `everUncertain`, string `messages`, and `everUncertain` set for an uncertain status). Anything else is `corrupt` and quarantined until a deliberate discard. | The reviewer's probe (30 h old, `status`/`everUncertain` missing) → `storage-unreadable`/`corrupt`, nothing sent; 13 malformed or inconsistent variants rejected; a complete record past the window → `reconcile`, nothing sent |

Round-2 validation (2026-10-22): `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check`, `pnpm -r test` (Shared 103, Web 7 + 109, Mobile 7), fresh Web production build with the bundle check and the client-asset scan, `pnpm --filter @loto/api generate:check` (287 files), `git diff --check`, and `pnpm --filter @loto/web test:e2e` (13/13 in Chrome, own disposable stack, containers and processes removed) all passed. The inherited doc-sync failure (canonical v57, design v55) is unchanged.

**D-M2-1 — decided by the owner on 2026-10-22 (option 2, implemented below); the original analysis is kept for the record.** The catalog (*Across-tab single flight*) requires each tab to keep its own branch context. With httpOnly cookies, one cookie jar is shared by every tab of a browser profile, and page navigations and React Server Component requests cannot carry a per-tab identifier. Implementable options, each changing something already fixed elsewhere:
1. **Per-tab branch slots selected by a request header** (`x-loto-tab` from tab-scoped storage) for every BFF call, with branch-scoped pages rendering branch data only on the client (the server renders the shell). Cost: drops the "Server Components fetch branch data" rule for branch screens; a duplicated tab inherits its slot.
2. **A tab/branch segment in branch URLs** (for example `/f/{slot}/gestao/fila`) so navigations select the slot. Cost: changes the shell blueprint route map.
3. **Accept one branch context per browser profile** for web, keeping the implemented safety (no request under another tab's branch; other tabs re-read and drop their scope). Cost: a switch in one tab moves every tab.
Until the decision M2 kept option 3's behavior. The owner chose **option 2** with one namespace for every branch destination, `/f/{branchId}/…`, applied to both lanes.

### D-M2-1 resolution and round-2 re-inspection (2026-10-22) — accepted in the independent re-review

**Round-2 re-inspection.** The two round-2 corrections were re-read against their reproductions: reads crossing a session command are voided at both the command's start and its settlement, and the record validator requires every recovery field. One remaining defect was found and fixed: a stored operation record frozen for **another endpoint** (a reused context, a tampered entry) was accepted and could be re-sent through this controller's operation. `OperationController` now quarantines it like a corrupt record (deliberate `discardCorrupt` only). Regression: the record is refused, no key is sent, and after the discard a new intent starts cleanly. All retained financial/session fences (scope-bound clients and disposal, write pause on unknown renewal, `Idempotency-Key`/`If-Match`/`Date`/`Retry-After` handling, CSRF) are unchanged and re-run.

**What the owner's decision changed (Web-only; no .NET contract change).**

| Area        | Before                                        | Now                                                                                                                                                                                                                                                                                           |
| ----------- | --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Routes      | branch destinations at the root (`/gestao/…`) | every branch destination under `/f/{branchId}/…` (`src/lib/branch-route.ts`); identity/account/global routes unchanged; unscoped branch paths wait for a chosen branch and resume inside its namespace                                                                                        |
| Credentials | one branch cookie per browser                 | identity cookies plus one `__Host-loto-b-{branchId}` slot per branch (token, summary, branch generation), bound to the `sid` that opened it, at most 4 (oldest evicted; an evicted tab's slot reopens under the generation the tab declares)                                                  |
| Selection   | "the current branch"                          | the request's route branch (page, layout, server client) or `x-loto-branch` (BFF calls); a slot of another login never counts; a missing slot is opened through `POST /branch/session`, which verifies membership; the API re-verifies membership and role on every call                      |
| Scope       | one browser scope                             | `{identity scope}.{branch generation}` per tab; commands compare the identity part; the forwarder refuses an ended or superseded branch generation (409)                                                                                                                                      |
| Refusal     | redirect to the picker                        | a branch the identity cannot open is a 403 page (verbatim messages, link to `/filiais`) and a not-forwarded `403 branch-unavailable` for BFF calls; no existence oracle (missing and foreign read alike)                                                                                      |
| Client      | one session view                              | identity `SessionProvider` at the root; a `BranchSessionProvider` + nested query scope per `/f/[branchId]` subtree (re-reads only its branch; re-reads when the identity changes under it; new generation remounts the subtree); the deepest mounted scope is the default for generated hooks |

**Boundary.** Slots live in the browser's cookie jar (no server store); single flights, rotation memory and ended generations remain in-process (one Next instance; M12 chooses sticky sessions or a shared store). A cookie-header bound of 4 slots means a fifth branch evicts the oldest; the evicted tab's next request reopens it.

**Checks.** Vitest (session core, routes, forwarder, gate, browser layer) adds: two branches held at once with each request using only its own slot; opening a third branch leaves the others; a slot of another login is never used; stale/ended declared generations refused without opening anything; eviction and reopening under the tab's generation; role change → new generation that ends the old one for every tab; login drops every previous slot; namespaced intents resume only for the same e-mail and never into another branch; real route handlers with one cookie jar: tab A opens Alfa, tab B Beta, each re-read stays on its own branch, a command from a branch tab is compared on its identity part, Sair ends both, and another person signed in on the same browser gets a refusal (not the old authority) for the old address; jsdom: queries carry the tab's branch and scope, moving the tab aborts the old branch's request and drops its late answer, leaving the branch subtree returns hooks to the identity scope.

Real stack (`e2e/branch-tabs.spec.ts`, **two or more pages of one browser context**): simultaneous server-rendered and browser reads in Alfa and Beta (each forwarded call names its own branch and scope); switching one tab leaves the other's address, filter, page instance and scope untouched; back/forward, reload, a duplicated tab and a bookmark open the branch their address names; a foreign branch is a 403 with the API's verbatim message, refused unforwarded by the BFF, while other tabs keep working; Sair in one tab ends every tab and a renewal answer written back after Sair authorizes nothing (both branches gate to Login; the rotated refresh token is refused by the API); an uncertain write stays in its branch — the other tab and the same tab after moving to Beta cannot verify or replay it, and back in Alfa it is verified with the same key, bytes and branch (one row in Alfa, none in Beta); another person signing in on the browser re-reads the open tab with their own role (Member), never the previous Admin authority. The M2 regression suite was migrated to the namespace (session, renewal, intent, CSRF, writes). Run on 2026-10-22 against the owned stack: **20/20** (18 on the first run; the two that failed were harness timing — a click waiting on a tab that had already reached its 403, and a held route cancelled by the gate's navigation — corrected and re-run green). After the M3 work and its round-1 corrections the full suite was rerun on a fresh owned stack: **20/20**.

### Independent re-review (2026-10-22) — selected M2 corrections and D-M2-1 accepted

The same reviewer inspected the candidate (the committed round-2 corrections, session/BFF/scopes and the per-tab implementation) and **accepted the selected Web M2 corrections and D-M2-1 in the local single-instance environment, including the proof with tabs that share one cookie jar**. The reviewer's own runs passed: the 20 real-stack tests, the workspace tests, typecheck, lint, formatting, build/bundle checks, a frozen install in a disposable copy, OpenAPI/generation drift, contrast and doc sync v57. Scope of the acceptance: one Next instance on localhost — multi-instance hosting (shared registry or sticky sessions) and real HTTPS stay with M12; no real screen reader or touch device was used.

Iterating locally: `node e2e/stack-cli.ts start` (from `web/`) prints a state file; export it as `LOTO_WEB_E2E_STATE` to run `npx playwright test` repeatedly against that stack, then `node e2e/stack-cli.ts stop`. Without the variable, `test:e2e` starts and removes its own stack; it never touches `loto-postgres-local`, another worktree's database or a real mailbox.

Changed ownership: `web/` (runtime, tests, fixtures, tooling, AGENTS, this milestone) and `pnpm-lock.yaml`. Nothing under `shared/`, `mobile/`, `server/` (build output only, ignored), `design/`, `infra/` or `docs/`. Nothing staged or committed.

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
- [x] **7.4** MoneyInput, DatePicker and masked-identifier wiring to the `@loto/core` formatters and the spoken money value; Badge labels from core's maps. *(Implemented 2026-10-26 against the Shared M3 helpers, which are implementer-checked and await independent review; web-verified only — see the 7.4 evidence below.)*


### M3 criterion map, evidence and limits (2026-10-22; review round 1 corrected, awaiting re-verification)

Baseline as for the D-M2-1 work (`paralell/app` at `e541a32`, design snapshot markers v57, `@loto/core` still `export {}`). Ticks mean **implemented and checked by the implementer**; nothing here is independently accepted, and Web M3 is not complete until an independent review accepts the round-1 corrections and 7.4 *(7.4 was open when this was written; it is implemented in the 7.4 evidence section below)*.

| Criteria | Artifact | Meaningful check |
|---|---|---|
| 1.1–1.3 | `src/theme/token-adapter.ts`, `tooling/token-css.mjs` (PostCSS, before Tailwind), `src/app/globals.css` marker, `src/theme/tokens.generated.ts` (names only; `generate:tokens`, `lint` drift check) | Vitest against the committed tokens (both themes, references followed, every enum value, units, cleared Tailwind defaults, no primitive name in output) and 16 rejection cases on disposable copies (missing/unexpected file, invalid JSON, bad group/key, embedded/missing/group reference, cycle, semantic literal, cross-theme, wrong layer ×3, theme asymmetry ×2, invalid values ×3); built CSS inspected: both themes present, no default palette, no primitive |
| 1.4 | `tooling/colors.mjs` (`hasPrimitiveReference`), `tooling/check-css.mjs` | Node tests: ramp classes with variants/opacity, `color.primitive.*`, primitive and palette CSS variables, `@apply`, `@theme`, selectors all fail; semantic/domain names and copy pass; the adapter's own negative fixtures are assembled at run time so the gate keeps covering test sources; unit test bans root-resolved `var(--color-…)` in `src/` |
| 2.1–2.3 | `src/theme/theme.ts`, `theme-provider.tsx`, `theme-control.tsx`, `src/app/layout.tsx`, `globals.css` base layer, `[data-theme-scope]` blocks | jsdom: default Sistema, cookie attributes, live attribute change; Chrome: Sistema follows the device live without reload, the first HTML carries the chosen theme and renders it **with JavaScript disabled**, Escuro persists on reload and reaches a second tab, Sistema restores |
| 3.1–3.7 | `src/components/ui/{button,form-field,input,money-input,select,date-picker,calendar,choice,format-port}.tsx` | jsdom: variant token mapping, disabled-but-focusable with reason and suppressed activation incl. implicit form submit, width-stable loading; label/helper/error wiring, fieldset, read-only, error summary focus and field links; password toggle, e-mail attributes, counter, search clear/announcement, port-masked identifier; MoneyInput blank≠zero, right-to-left digits, deletion back to blank, leading zero, port paste, spoken value, source free of number parsing; combobox keyboard, groups, type-ahead, missing/inactive markers, loading/failed/empty, native, searchable filter/count/Enter, compact bottom sheet; calendar math without `Date` (leap years, month/year edges) and a source scan; date field parse on blur, invalid text kept, calendar keyboard grid, bounds, no calendar before the branch date; day stepper; checkbox mixed, radio no default, switch. Chrome: real keystrokes in MoneyInput; calendar keyboard inside a dialog |
| 4.1–4.4 | `badge.tsx` (enum exhaustiveness at compile time and run time), `card`, `summary-strip`, `detail-list`, `table`, `filter-bar`, `pagination`, `page-header` | jsdom: every enum value ↔ token, domain variables, unknown value neutral and reported, markers; region card, unavailable figure never zero, em dash; semantic table, named scroll region, row click activates the row link with modifiers, nested control and text selection do not; staged filters, compact sheet; envelope-driven pagination through the port; h1 focus, breadcrumb, **Voltar para**. Chrome: row vs nested action vs text drag; 375px table scrolls in its region |
| 5.1–5.3 | `dialog`, `popover`, `menu`, `tabs`, `disclosure`, `stepper`, `notice`, `toast`, `save-status`, `skeleton`, `empty-state` | jsdom: focus in/return, scroll lock, Escape/backdrop/close one path, busy blocks all, destructive confirm; menu keyboard, type-ahead, unavailable with reason, sheet on compact; tabs/segmented arrows; disclosure; stepper statuses; verbatim messages with duplicates, uncertain notice copy, toast timing/pause/replace, every SaveStatus text, skeleton busy region, neutral all-clear. Chrome: modal `<dialog>` focus containment, listbox and calendar **in the top layer above the dialog**, Escape layering, focus return |
| 6.1 | `src/app/styleguide/*` (development-only; fixture port of finite tables) | Chrome: all 25 families and 5 token categories render; no request leaves the page; both themes side by side; 375px, 200% zoom and doubled root text without page scroll; reduced motion zeroes durations |
| 6.2 | refinement fixes | Manual pass against the specs and handoff in the browser pane: select labels wrapped instead of truncated, badges wrap at words, pagination icon kept with its label, scoped regions now use per-theme variables (found by the axe scan: `--color-*` resolved at the root). **Impeccable was not executed** (not installed; no guessed package or remote script run) |
| 7.1–7.3 | tests, `test:styleguide`, docs | Commands below |

| Check (2026-10-22) | Result |
|---|---|
| `pnpm install --frozen-lockfile`; `pnpm -r typecheck`; `pnpm -r lint`; `pnpm format:check` | Passed |
| `pnpm -r test` | Passed: Shared 103; Web 9 tooling + 219 Vitest (17 files, round-1 regressions included); Mobile 7 |
| `LOTO_API_BASE_URL=http://127.0.0.1:59999 pnpm --filter @loto/web build` (fresh `.next`) | Passed with the bundle check (one app-owned TanStack/React; **no `/styleguide` or `/e2e` route** in an ordinary build); client assets contain no API variable/URL, session cookie name or gallery fixture |
| `pnpm --filter @loto/web test:styleguide` (fresh `.next-styleguide`, Chrome) | Passed 15/15 (round 1 adds the 375 px nested-overlay check), including axe-core 4.14.0 WCAG 2.0/2.1/2.2 A/AA in Claro and Escuro with no violations |
| `pnpm --filter @loto/api generate:check`; `bash docs/check-loto-doc-sync.sh` | Passed: 287 files match; doc sync aligned at v57 (read-only) |
| `git diff --check`; personal-path scan of changed files | Passed |
| `pnpm --filter @loto/web test:e2e` after the M3 changes and the round-1 corrections (own disposable stack, Chrome) | Passed 20/20 (M2 regression + per-tab spec); containers and processes removed |

**Independent review round 1 (2026-10-22) — corrected, awaiting re-verification.** The reviewer accepted M2/D-M2-1 (above) and confirmed five M3 defects, each with a reproduction; every correction has a regression test of that reproduction (`test/unit/ui/review-round-1.test.tsx`; the first was also shown to fail against the old merge).

| Finding | Correction | Regression check |
|---|---|---|
| P2 — `useControlProps` replaced a control's own `disabled`, `required` and `aria-invalid` | The field context only adds: either source disables or requires; an explicit consumer `aria-invalid` wins over the field's | Disabled+required+invalid inputs, textarea and native select keep their props inside and outside a field; the field still adds its own state when the control sets none |
| P2 — a disabled Select / compact SearchableSelect still opened and changed value | `disabled` prop and the field's disabled state make the control unavailable; opening, every key, pointer choice and the compact sheet trigger are gated, and the visible open state is derived from availability | Pointer and keys (`ArrowDown`, `Enter`, Space, `Home`, type-ahead) open nothing, `onValueChange` never fires, by field or prop; searchable wide (input disabled) and compact (no sheet) |
| P2 — a disabled DateField's calendar still chose dates | `disabled` covers the composite: calendar button natively disabled, surface and selection gated; DateRangeField, DayStepper and MonthField gain `disabled` (MonthField also follows its field). MonthField's hardcoded fallback year is gone: without a value or `max` it waits (no guessed year) | No calendar or new date in a disabled field; range, stepper and month emit nothing when disabled; the month picker waits without a known month |
| P2 — replacing a toast charged the old budget to the new one; closing while focused left a pause that never ended | Unread time is per message id (a message that leaves charges nothing); pause (hover/focus) belongs to a message id and leaves with it | Replacement after 4 s: the next stays 5 s; close while focused, then a new toast expires on time; a hovered toast resumes with its unread time |
| P3 — at 375 px a picker inside a dialog opened a second modal | Dialog provides an in-dialog context; below a dialog the calendar surfaces, SearchableSelect, Menu and FilterBar use the popover layer / inline fields instead of a sheet | jsdom at compact width: calendar, searchable list and menu inside a dialog leave one `<dialog>`; Chrome at 375 px: one `dialog:modal` and one `:popover-open` for the calendar and the listbox |

**Not covered / open (2026-10-22).** 7.4 (core wiring) was blocked by Shared M3 then; it is implemented in the section below. No real screen-reader pass was executed (axe is a supplement); the browser's default-font-size setting was not emulated (200% zoom via half-width viewport and a doubled root size were); only Chrome was driven; native/mobile is out of scope; touch was checked as coarse-pointer sizing in code, not on a device. The gallery's month/day labels and amounts were fixture tables then; since 7.4 they come from core.

**Contract/dependency impact.** No API, Shared, design or canonical-doc change. New devDependency `axe-core 4.14.0` (already resolved in the workspace; lockfile adds only the importer entry). Cross-area suggestion for the design lane, not acted on: the Badge spec could say how a scoped (pinned-theme) region references domain colors, and the shell blueprint's route map could show the `/f/{branchId}` prefix the owner decided.

### 7.4 core wiring evidence (2026-10-26; implementer-checked, awaiting independent review)

Baseline `paralell/app` at `e746caa` (clean); Shared M3 helpers from the same batch. The five round-1 corrections are unchanged and their regressions (`test/unit/ui/review-round-1.test.tsx`) pass on the new code; Web M2/D-M2-1 acceptance is not reopened.

| Area | Change | Check |
|---|---|---|
| Port | `src/lib/core-format.ts` (`coreFormat`, frozen) implements all 13 callbacks with core; `AppProviders` provides it for every route. `galleryFixtureFormat` deleted; the gallery uses the root provider | `core-wiring.test.tsx`: every callback on non-fixture values; provider wired; fixture file gone |
| Calendar | `CalendarDate`/`asCalendarDate`/`daysInMonth` and `calendar.ts` arithmetic re-exported from core (same names and branded type); `branch-clock.ts` validates through core | existing calendar/picker/branch-clock suites unchanged and green; jsdom with core: typed `29/02/2028` accepted, `29/02/2027` invalid and kept, pt-BR headings/weekdays/day names across 31/12/2030 → 01/2031 with the branch-clock today and bound |
| MoneyInput | 14-digit cap from core on typing, fallback and paste; display, spoken value and paste from core | jsdom and Chrome keystrokes: `4815162342` → `48.151.623,42` with the full spoken sentence, typed zero → **zero reais**, leading zero, 15th digit refused, Backspace to blank, `R$ 31.415,92` paste, signed/`1.5`/13-digit pastes ignored, read-only amount |
| Identifier, counts | CPF masks at 11 digits, verbatim otherwise; Pagination/Stepper counts dot-grouped | jsdom and Chrome |
| Badge | `domainLabels` (13 domains, typed against the generated enums); default label from core | every value of every domain renders its copy §5 label; unknown stays raw and uncolored |
| Instants | `AuditInstant`/`useDeviceTimeZone` (device zone after hydration, or explicit zone in SSR) | Chrome: server HTML carries the explicit-zone time and an empty device-zone `<time>`; after hydration São Paulo 14:30, Kolkata 23:00, New York 13:30 for one instant; no console/hydration error |
| Gallery | **Formatação** section (money, signed, fiado, spoken, dates, wall clock, instants, durations, counts, identifiers, absent vs zero, the 25 maps) and the arithmetic demonstration; example rows/figures formatted by core | `e2e-styleguide/core-format.spec.ts` (11 tests) |

| Check (2026-10-26) | Result |
|---|---|
| `pnpm --filter @loto/web test` | 9 tooling + 232 Vitest (18 files; 13 new in `core-wiring.test.tsx`) — before review round 1 |
| `LOTO_API_BASE_URL=http://127.0.0.1:59999 pnpm --filter @loto/web build` (fresh `.next`) | Passed with the bundle check; no `/styleguide` or `/e2e` route; client assets contain no API variable/URL, `__Host-loto-b`/`-rt` cookie names, gallery text or fixture |
| `pnpm --filter @loto/web test:styleguide` (fresh `.next-styleguide`, Chrome) | 26/26 (15 existing + 11 core-format), axe-core 4.14.0 clean in Claro and Escuro; the core-format spec also passed 3× repeated |
| `pnpm --filter @loto/web test:e2e` (own disposable PostgreSQL/Mailpit/API, Chrome) | 20/20; owned containers removed. Docker Desktop was started for the run and quit afterwards |

**Corrections made while verifying.** The axe scan and the new interaction tests now wait for hydration: with the larger page, a scan could run before the indeterminate checkbox's `indeterminate` property is set, and axe then reports `aria-conditional-attr` on its server-rendered `aria-checked="mixed"`. That pre-hydration state is inherent to a native checkbox (no HTML attribute exists for `indeterminate`); recorded for review, not changed. The gallery's example amounts previously used an ASCII space after `R$`; they are now formatted by core (U+00A0), and the two existing table assertions name that code point.

**Independent review round 1 of 7.4 (2026-10-27) — all four Web findings accepted and corrected; awaiting the same reviewer's recheck.** (The two core findings are in Shared M3.) Each regression in `test/unit/ui/review-round-2.test.tsx` was run against the pre-fix components and failed (8 of 12; the extreme-navigation ones once they record jsdom's swallowed handler errors), then passed.

| Finding | Correction | Regression |
|---|---|---|
| P2-1 — a read-only MoneyInput changed on paste | `locked` (read-only or disabled) guards `beforeinput`, the `onChange` fallback and paste | jsdom: paste, typed digit, deletion and change emit nothing for `readOnly` and for `disabled`, editable control still pastes; Chrome: real paste and keys on a read-only gallery field leave `12,34` and zero changes |
| P2-2 — a read-only DateField's calendar chose dates | read-only covers the composite: the calendar button is disabled, `commit` and selection are gated | jsdom: no grid opens, no value emitted; Chrome: gallery **Data registrada** |
| P2-3 — reselecting the current date left invalid text with no error | every calendar choice rewrites the text explicitly | jsdom and Chrome: 20/02/2024 → `99/99/9999` → 20/02/2024 again shows `20/02/2024` and no error |
| P3-4 — navigation past 0001-01-01 threw `RangeError` | calendar keys and month buttons stay put at either end (`withinCalendar`); DayStepper's previous/next and MonthField's year buttons are disabled at 0001/9999 (same defect class, found while fixing) | jsdom: all backward moves at 0001-01-01 and forward moves at 9999-12-31 stay with no uncaught error, stepper and month field bounded; Chrome: 01/01/0001 + ArrowLeft/Up/PageUp/Home with no page error |
| (Shared P3-5) progressive CPF | IdentifierInput uses `identifierInput` with caret restore; deleting a generated mask separator deletes the neighbouring digit (round 2: only when the previous text was exactly the mask of its digits) | jsdom: step-by-step CPF, Backspace/Delete over separators, mid-value insert keeps the caret, phone shape change at 11 digits; Chrome: real typing and Backspace |

**Recheck round 2 (2026-10-27) — two findings, both accepted and corrected.** P2: deleting an invalid character from `529.98a` also removed the `8`, because any removed non-digit was treated as mask punctuation (an alphanumeric CNPJ and an overlong CPF were affected the same way). The separator-aware deletion now applies only when exactly one character was removed, that character is one of the kind's generated separators, and the previous text equals the mask of its own digits; otherwise the edit is kept as typed. Regressions (jsdom, both shown failing on the round-1 handler, and Chrome): `529.98a` + Backspace → `529.98`, a letter removed with Delete mid-text, Backspace over an alphanumeric CNPJ's slash removes only the slash, and removing a dot from an overlong CPF keeps all 12 digits. P4: the stale "progressive CPF" open item in this milestone's "Not covered / open" paragraph is removed; alphanumeric CNPJ and the native limitations stay. Checks after round 2: workspace frozen install/typecheck/lint/format (the one mobile warning), tests Shared api 103, core 292, Web 9 + 246 Vitest, Mobile 7; production build with bundle check; `test:styleguide` 31/31 (the new and existing CPF scenarios also 3× repeated). The real-stack e2e (20/20 after round 1) was not re-run: the change is confined to IdentifierInput's change handler, which no e2e flow uses.

Checks after the round-1 corrections (2026-10-27): workspace frozen install/typecheck/lint/format (still the one mobile warning); tests Shared api 103, core 292, Web 9 + 244 Vitest (19 files), Mobile 7; production build with bundle check (no gallery/e2e route, no API URL or cookie names in client assets); `test:styleguide` 30/30 (15 existing + 15 core-format; the round-2 and CPF scenarios also 3× repeated); real-stack `test:e2e` 20/20 on a disposable stack (Docker started for it and quit after). Not re-run because untouched: `generate:check`/`export:check`, design and doc-sync checks, the mobile export.

**Not covered / open.** No real screen-reader or physical-touch pass; only Chrome; the restricted-arithmetic demonstration is gallery-only (no screen adopts it before Design 4.9.7); native execution (Hermes) is Mobile's. The Shared M3 open item on alphanumeric CNPJ (verbatim display, fails validation) applies here too.

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

---

## Milestone 6 — Transactions Ledger

**Goal:** **Transaction — Create (fast entry)** and **Transaction — Edit (correction / audit)** (spines), Transactions list, Installment (pre-dated cheque) plan, Client statement, and Receber pagamento.

**Scope boundary / key behaviors:**

- Fast entry: type-driven form, automatic non-blocking impact preview, idempotent create, Draft save, and the separate finalize action.
- Edit limited to the contract's editable fields, with the full impact preview, `If-Match` concurrency, and the installment confirm-and-cancel flow.
- Member scope follows linked accounts (`mine` default in Meu turno); return targets follow the route-ownership table, including the three Draft-blocker origins.
- **Gated:** fiado types and their linked movement on Create, `PaidAt` and generated movements on Edit, the **Automática** label on the list, Client statement, and Receber pagamento (gap H); account/Tab routing on Create (gap L) — all server M7.7 Phase 5. The list's type filter (gap Z) and the plan's atomic **Finalizar plano** (gap Y) wait on server M7.7 Phase 7.6.

**Precondition:** Web M5 (Terminal activity needs an open close) and the Design M4 ledger blueprints.

---

## Milestone 7 — Manager Work Queue & Approvals

**Goal:** **Manager Work Queue** (spine, exception-first) and **Daily-close approval** (spine, comparison framing).

**Scope boundary / key behaviors:**

- Work Queue from one `GET /report/dashboard` call (or its renamed successor, per the tripwire above): fixed group order, the earlier-days backlog banner with **Ver dia mais antigo**, today literal by branch clock, and status-routed deep links into close flows and the Draft-filtered Transactions list.
- Approval shows opening → closing → variance from the review context, approve / reject with reason, and returns to its origin.
- **Gated:** the self-recorded marker on approval (gap U) and the link-request group on the queue (gap V) — both server M8; the rest proceeds.

**Precondition:** Web M5 and M6 (the flows the queue routes into) and the Design M4 queue/approval blueprints.

---

## Milestone 8 — Reports & Month Lock

**Goal:** Daily ledger, Fiado balance, Fiado aging, Open-cheque aging, Cash-variance summary, and **Monthly reconciliation + lock** (`POST /setting/lock-month`).

**Scope boundary / key behaviors:** report defaults from the catalog, server-side aggregation only, drill-downs that return to the source report, structured lock blockers that deep-link into their resolution flows. **Gated:** Fiado balance and Fiado aging (gap H, server M7.7 Phase 5).

**Precondition:** Web M7 and the Design M4 report blueprints.

---

## Milestone 9 — Time & Self-Service

**Goal:** Clock in / out (Member), Time-entry management (Manager/Admin, including their own hours), My transaction summary, and My time-entry balance — own context in Meu turno, manager-selected context from Gestão.

**Scope boundary / key behaviors:** forgotten-punch triage through the `IsInProgress` filter, My transaction summary's automatic share (gated on gap H, server M7.7 Phase 5), the *Sem registro* day labelling derived on the client from the window minus entries, and no Clock in / out tap for Manager/Admin.

**Precondition:** Web M4 and the Design M4 time blueprints. Independent of M5–M8, so it can be scheduled earlier if useful.

---

## Milestone 10 — Admin & Configuration

**Goal:** Operators, Account assignment, Accounts, Clients (also counter work), Categories & Transaction Types, Products, Holidays, Settings, and Branch members.

**Scope boundary / key behaviors:** Clients keeps the entry mode and stays available to any branch role except deactivate. **Gated:** Account assignment's Tab pairing (gap L) and Accounts' deactivation rule (gap S), both server M7.7 Phase 5; Branch members' own-row guard (gap U) and member addition by invitation (gap C), both server M8. Products' deactivation warning/recovery only (gap AA, server DailyClose/Product; scheduling pending M7.7 close-out triage) is also gated; ordinary Product CRUD and reads proceed.

**Precondition:** Web M4 and the Design M4 admin blueprints. Until this lands, local and staging data come from the backend seeds and the server M8 command-line onboarding.

---

## Milestone 11 — Account & Onboarding

**Goal:** The auth & account screen set that design 0.4.6 catalogs: Invite accept (Criar senha or Continuar com Google), registration via invite, Forgot / Reset password, Profile, Change password, the session menu with server-side Logout, Continuar com Google on Login, the internal **Plataforma** area with the Nova lotérica wizard, the owner setup checklist on Manager Work Queue, the guided Add / Offboard employee flows, and the operator-link requests (gap V) across Cockpit, Work Queue, and Operators.

**Precondition:** Server M8 shipped and design 0.4.6 cataloged; Design M4 blueprints for the set. Closes the C/N/U/V qualified features left open in Web M4, M5, M7, and M10.

---

## Milestone 12 — Release Readiness

**Goal:** Ship the web app: hosting target and deployment pipeline aligned with server M9, CI running the standing gates plus the Playwright suite, an accessibility audit and performance budgets across the spine screens, error tracking, and security headers for the cookie session.

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires. The hosting decision is shared with `infra/` and recorded there.
