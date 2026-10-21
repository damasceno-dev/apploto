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

**Scope boundary / key behaviors (detailed phases written when this milestone starts):**

- **Cookie session.** Route handlers implement login (`POST /user/login`), branch session (`POST /branch/session`), renewal (`POST /user/renew-token`), and local logout; the identity, branch, and refresh tokens live in `httpOnly`, `Secure`, `SameSite=Lax` cookies. Server-side logout revocation arrives with server M8; until then logout clears the cookies, and the milestone notes record that the refresh token stays valid server-side.
- **One renewal path.** Server-side fetches and the browser forwarding layer share a single-flight renewal on `401`; a failed renewal hands control to the session gate (branch picker or login) per the design 0.4.2 session-expiry pattern, preserving the intended route as an unexecuted intent.
- **Browser calls through the BFF.** `@loto/api/queries` hooks reach the API through a catch-all route handler that attaches the bearer from the cookie and forwards `Idempotency-Key`, `If-Match`, and `ETag` unchanged. The token-provider interface from Shared M2 is satisfied on the server from cookies; the browser never sees a token. Decide in-phase whether the forwarding route is a catch-all proxy or a configured base URL on the client core, and record it.
- **Session gate.** Middleware and layout guards resolve the four authority states from `screens.md` (anonymous → Login; identity only → Branch picker; branch session → role home) before any branch-scoped fetch; deep links are held as intent until the gate passes.
- **Branch clock.** A cached `GET /branch/current` read exposes `BranchLocalDate` / `BranchLocalDateTime` as calendar values, never JavaScript instants.
- **Mutation plumbing.** Helpers that mint and persist an `Idempotency-Key` per user intent (so a retry replays instead of duplicating), carry the `ETag` → `If-Match` round trip, and surface a stale `409` verbatim with a reload path. Uncertain-outcome handling follows design 0.4.2.
- **Unsaved input kept on the device** behind one form-draft helper, keyed by branch, user, and route, cleared on successful save and on branch switch.
- **Test baseline:** Vitest + Testing Library for the plumbing (renewal single-flight, gate resolution, idempotency-key lifetime); Playwright with a local stack (`infra/docker-compose.yml` PostgreSQL + the running API) for login → branch session → gate.

> **Shared M2 handoff (2026-10-17, S06; Shared M2 awaits independent review).** The client contract this milestone consumes, as delivered in `shared/api` (details in `shared/AGENTS.md`):
> - **Server side, per request:** create a client per request with `createApiClient({ baseUrl, getAccessToken })` and pass it as `{ client }` in a raw function's last argument. The process-wide `configureApiClient()` default suits the browser runtime. Each call snapshots its client, so concurrent requests never share a token provider.
> - **Browser through the BFF:** `baseUrl` may be a same-origin prefix such as `/api`, and a provider that returns `null` sends no `Authorization` header. Both forwarding designs named above therefore work without Shared changes; the choice stays in-phase here.
> - **The provider receives `{ method, path, authTier }`** on every call. `authTier` is `anonymous`, `identity` or `branch`, generated from each operation's published `security`. Role names are not part of it.
> - **Failures reject with one `ApiError`:** `kind`, `status`, `method`, `path`, the verbatim `errorMessages`, and `dispatched`. `isOutcomeUncertain(error)` marks the design 0.4.2 "Resultado não confirmado" cases.
> - **Per-call headers:** ordinary headers (for example tracing) may be passed in options. A caller value that conflicts with a generated `Idempotency-Key`, `If-Match` or `Content-Type`, and any caller `Authorization`, is refused before dispatch, so the BFF attaches the bearer itself.
> - **Shared never retries, renews, mints or replaces anything.** `Idempotency-Key` and `If-Match` are required arguments passed through unchanged, `ETag` is readable on raw results, and `toIfMatch`/`parseETag` implement §6.11. Generated hooks set no `retry`; keep mutation retries at 0.
> - `@tanstack/react-query` is a peer dependency (`^5.104.1`), so the app owns the instance and the `QueryClient`.

**Precondition:** Web M1, Shared M2, and design 0.4.2 authored (the patterns this milestone wires).

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
