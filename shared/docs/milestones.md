# Lotero — Shared Milestones

> **Status:** Active
> **Draft started at** 2026-06-10
> **Planned start:** 2026-06-30
> **Approach:** Backend-owned API contract, generated TypeScript client, source packages linked via pnpm workspaces

This file is the general milestone list for the `shared/` area. Detailed phases with checkboxes are written just-in-time when a milestone starts, following the `server/docs/milestones.md` convention. Cross-area docs may reference these milestones as **M1**, **M2**, **M3** (qualified as "Shared M1" etc. across areas).

### Standing rules

- **The backend owns the API contract.** Nothing in `shared/` defines a request/response shape by hand — every contract type is generated from the server OpenAPI document (Milestone 2 onward). Hand-written code may *wrap* generated types, never duplicate them.
- **Cross-platform runtime TS only:** no React components, no Tailwind/NativeWind, no Next-specific or Expo-specific APIs, and no direct reads of `NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` — configuration is injected by the consuming app shell (per `shared/AGENTS.md`).
- **Source-package pattern:** packages export TypeScript source directly (no build step, no `dist/`). Web compiles them via Next `transpilePackages`; mobile via Metro. Revisit only if a consumer outside this repo ever appears.
- **pnpm workspaces only — no Turborepo** (root `AGENTS.md`).
- **Standing gates:** `pnpm -r typecheck` and `pnpm -r lint` stay green on every commit touching `shared/`. A test runner (Vitest) arrives with Milestone 3 — the first pure logic worth testing.

### Area dependency map

- M1 → M2 → M3 (strictly ordered within this area).
- M1 also unblocks Web Milestone 1 and Mobile Milestone 1 (apps join the workspace M1 creates).
- M2 consumes the server OpenAPI document at the current spec revision; regeneration after each backend milestone phase is a cheap `pnpm generate`, so the pipeline never blocks on backend M7/M8 finishing. The one hard backend dependency is the **OpenAPI Contract Hardening** milestone (see M2's precondition), owned and planned by the server track.
- Design milestones run fully parallel — no dependency in either direction until M3 implements `design/specs/formatting-ptbr.md`.

---

## Milestone 1 — Workspace Bootstrap

**Goal:** Stand up the pnpm monorepo plumbing — root workspace, TypeScript base config, lint/format baseline, and empty-but-wired `@loto/api` + `@loto/core` packages — so that Milestone 2 codegen and the web/mobile scaffolds land into an already-working workspace.

**Scope boundary:** Tooling and package skeletons only. No generated client (Milestone 2), no helpers (Milestone 3), no app scaffolds (Web/Mobile Milestone 1). Touches nothing under `server/`, `design/`, or `infra/`.

**Precondition:** None on the backend — runs fully parallel to server M7 review. Locally: current Node LTS and pnpm installed.

**Key behaviors:**

- **Package scope is `@loto/*`:** `@loto/api` and `@loto/core`. Short, unambiguous, matches the project naming.
- **ESM only** (`"type": "module"`) across all workspace packages.
- **TypeScript strict** with `moduleResolution: "bundler"` and `noEmit` — apps bundle package source directly (source-package pattern from the standing rules).
- **Pinned toolchain:** exact pnpm version via the root `packageManager` field; Node version via `engines` + `.nvmrc`. Exact versions chosen at implementation time, recorded in the checklist notes.
- **Lint/format:** ESLint 9 flat config + Prettier configured once at the root; packages inherit. React/Next/Expo-specific lint layers are added later inside `web/` and `mobile/`, not here.
- **`web` and `mobile` are listed in `pnpm-workspace.yaml` from day one** — pnpm tolerates globs that match nothing, so the app scaffolds later join the workspace without touching root plumbing.

### Phase 1 — Workspace root

- [ ] **1.1** Add root `package.json`: `"name": "lotoapp"`, `"private": true`, `"type": "module"`, `packageManager` pinned to the chosen exact pnpm version, `engines.node` set to the current LTS floor, and fan-out scripts `typecheck`, `lint`, `format`, `format:check` implemented via `pnpm -r ...`.
- [ ] **1.2** Add `pnpm-workspace.yaml` with packages: `shared/*`, `web`, `mobile`.
- [ ] **1.3** Add `.nvmrc` with the pinned Node LTS major.
- [ ] **1.4** Extend the root `.gitignore` (currently dotnet-flavored) with the Node/pnpm block: `node_modules/`, `*.tsbuildinfo`, `.eslintcache`, pnpm debug logs.
- [ ] **1.5** Add root `.npmrc` with `engine-strict=true` (and nothing else until a concrete need appears).
- [ ] **1.6** Run `pnpm install` — succeeds on the empty workspace; commit `pnpm-lock.yaml`.

### Phase 2 — TypeScript + lint/format baseline

- [ ] **2.1** Add `tsconfig.base.json` at the repo root: `strict`, `noUncheckedIndexedAccess`, `isolatedModules`, `verbatimModuleSyntax`, `module: "esnext"`, `moduleResolution: "bundler"`, `target` at a current evergreen baseline, `skipLibCheck: true`, `noEmit: true`. Record in a comment that app tsconfigs (Next/Expo) extend this and may override module settings their frameworks require.
- [ ] **2.2** Add root `eslint.config.mjs`: ESLint 9 flat config with `typescript-eslint` recommended rules plus `eslint-config-prettier` last. Scope it to `shared/**` plus future `web/**`/`mobile/**` TS files; keep framework-specific plugins out of the root config.
- [ ] **2.3** Add Prettier config (`.prettierrc` + `.prettierignore`). Pick formatting options once at implementation time; they apply unchanged to `web` and `mobile` later.
- [ ] **2.4** Verify root scripts run green on the so-far-empty workspace: `pnpm typecheck`, `pnpm lint`, `pnpm format:check`.

### Phase 3 — Package skeletons

- [ ] **3.1** Add `shared/api/package.json`: name `@loto/api`, `"private": true`, `"type": "module"`, a `typecheck` script, and a **two-entry `exports` map** (source package — no build step, no `main`/`dist`): root entry `.` → `./src/index.ts` (contract types + raw fetchers — server-safe, must never transitively import react-query) and `./queries` → `./src/queries/index.ts` (TanStack Query hooks — client-only). Server Components import from `@loto/api`; client components and mobile screens import hooks from `@loto/api/queries`.
- [ ] **3.2** Add `shared/api/tsconfig.json` extending `tsconfig.base.json` with the package's `include`.
- [ ] **3.3** Add `shared/api/src/index.ts` and `shared/api/src/queries/index.ts` (placeholder exports backing the two `exports` entries) plus the pre-agreed folder structure from `shared/AGENTS.md`: `src/client/` (API client setup — filled by Milestone 2) and `src/generated/` (Orval output target — filled by Milestone 2), each with `.gitkeep`.
- [ ] **3.4** Add the same trio for `shared/core`: `package.json` (`@loto/core`), `tsconfig.json`, `src/index.ts` placeholder, plus empty module folders `src/format/`, `src/validate/`, `src/labels/`, `src/dates/` (filled by Milestone 3).
- [ ] **3.5** Verify `pnpm install && pnpm -r typecheck && pnpm -r lint` green with both packages in the workspace; confirm no package declares dependencies yet (runtime deps like `@tanstack/react-query` arrive with Milestone 2 as peer dependencies, by design).

### Phase 4 — Docs sync + close-out

- [ ] **4.1** Update `shared/AGENTS.md`: promote `shared/core` from "later if needed" to day-one (recommended 2026-06-07, confirmed 2026-06-10); document the source-package pattern (no build step; Next `transpilePackages` / Metro), the `@loto/api` / `@loto/api/queries` export split, the `@loto/*` scope, the toolchain pins, and restate the no-env-var-reads rule. Keep it the single source of truth for the area.
- [x] **4.2** Align all agent docs that called `shared/core` optional/later → **day-one**: root `AGENTS.md` (root `CLAUDE.md` is a symlink to it), `shared/AGENTS.md` (core lines), and `mobile/AGENTS.md`. **Done 2026-06-30** in the frontend-foundation commit, to remove the milestone-vs-AGENTS contradiction. The broader `shared/AGENTS.md` rewrite (source-package pattern, export split, toolchain pins) stays in 4.1.
- [ ] **4.3** Done criteria: fresh clone → `pnpm install && pnpm -r typecheck && pnpm -r lint` green; both packages resolve in the workspace; no build artifacts or `dist/` anywhere; no env-var reads anywhere under `shared/`.

> Note: the companion `web/AGENTS.md` amendment (route-handler BFF expected for auth/cookies) belongs to the web area and lands with the web milestones file, not here.

---

## Milestone 2 — API Contract Pipeline

**Goal:** A deterministic OpenAPI → TypeScript pipeline: export the server OpenAPI document without hand edits, generate types + raw fetch functions (Server-Component-safe) + TanStack Query hooks via Orval, and ship the hand-written client core that everything generated plugs into.

**Scope boundary / key behaviors (detailed phases written when this milestone starts):**

- Deterministic OpenAPI export from `server.API` into the repo (script-driven; build-time document generation preferred over scraping a running server — decide in-phase and record it). The committed document is the generation input, so contract drift is always visible in diffs.
- Orval config producing three layers: contract types, raw `fetch` functions usable from Next Server Components, and TanStack Query hooks for client components and mobile screens. Output is mapped so hooks land behind the `./queries` entry while the root entry stays hook-free (exact Orval output layout decided in-phase).
- **Export hygiene invariant:** the root `@loto/api` entry exposes types + raw fetchers only and must never transitively import `@tanstack/react-query`; hooks are reachable only via `@loto/api/queries`. Pin it with a lightweight check (import-boundary lint rule or a small script walking the import graph of `src/index.ts`).
- **Enum-name gate (consumer side):** after generation, assert the generated TS exposes enum *names* (string unions / named enum objects), never bare numeric unions like `0 | 1 | 2`. The server-side half of this guarantee is the backend OpenAPI Contract Hardening milestone (see this milestone's precondition) — the server emits a named-enum contract; this gate proves codegen preserved it.
- Hand-written client core in `src/client/`: base-URL + token injection via a caller-provided provider interface (apps own storage — cookies on web, secure store on mobile), aware of the **two-stage token model** (identity token from `POST /user/login` vs branch-scoped token from `POST /branch/session`; refresh via `POST /user/renew-token`), and `ResponseErrorJson` normalization into one typed `ApiError`.
- Required-field fidelity gate: a static assertion that required C# fields are non-optional in generated TS, pinned against known DTOs (the promise `server/AGENTS.md` already makes).
- `pnpm generate` regeneration script + a drift check (regenerate and fail on `git diff`) that M9's CI can adopt later. Regenerating after each backend phase lands is routine, not an event.

**Precondition:** Milestone 1, plus the backend **OpenAPI Contract Hardening** milestone landed on the server track, so generation consumes a named-enum contract. That milestone now exists as **Milestone 7.6 — Backend OpenAPI Contract Hardening** in `server/docs/milestones.md` (planned during the M7 Phase 12 close-out, where the project-wide `required`-metadata gap was relocated to it); it must land before this milestone starts. Its contents: public contract enums serialize as *names* on responses (integer input still accepted during transition); the OpenAPI document lists named enum values (`Draft`, `Active`, `Cancelled`, …); WebApi/OpenAPI tests pin at least one request enum and one response enum; required/nullability fidelity audited on a sample of DTOs; spec-sync revision bump per the server doc-sync convention. Otherwise this milestone tracks the server OpenAPI at whatever spec revision is current — it does not wait for M7 to close.

---

## Milestone 3 — Core Helpers

**Goal:** Fill `@loto/core` with the day-one pure-TS helpers both apps need immediately: BRL format/parse, CPF/CNPJ format + validate, date/time formatters (`dd/MM/yyyy`, `HH:mm`, datetime), signed-variance display, and pt-BR label maps for every contract enum (`TransactionStatus`, `DailyCloseStatus`, `Direction`, `AgingBucket`, `TimeEntryStatus`, `Role`, `AccountType`, …).

**Scope boundary / key behaviors (detailed phases written when this milestone starts):**

- **Money precision strategy (locked 2026-06-10):** the wire format stays JSON `number` — the backend contract is untouched, and C# `decimal` / `numeric(14,2)` values round-trip exactly at 2 decimal places. Client code treats money as **display-only by default**: the backend computes every financial outcome (the preview endpoints exist for exactly this), so clients never recompute balances, variances, or due amounts. Where client-side arithmetic is unavoidable (input masking, optimistic column sums), it goes through integer-cents helpers in this package: `parseBrl` returns integer **cents**, `formatBrl` accepts cents or a wire `number`, and no client code performs float arithmetic on money.
- Implements `design/specs/formatting-ptbr.md` (Design M0, Phase 2) — formatters cite the spec; the spec is the contract, the helpers are the implementation.
- Enum label maps key off the **generated** enum types from `@loto/api`, so a backend enum change breaks the build here instead of silently desyncing labels.
- Vitest lands with this milestone; every formatter and validator gets unit tests (the first frontend tests in the repo).
- Pure TS only — no Intl-locale surprises across platforms without a pinned strategy (decide `Intl` vs hand-rolled per formatter in-phase, considering Hermes/Expo `Intl` support, and record the decision).

**Precondition:** Milestone 2 (generated enum types) and Design M0 Phase 2 (the formatting spec).
