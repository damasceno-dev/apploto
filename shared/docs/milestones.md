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

- [x] **1.1** Add root `package.json`: `"name": "lotoapp"`, `"private": true`, `"type": "module"`, `packageManager` pinned to the chosen exact pnpm version, `engines.node` set to the current LTS floor, and fan-out scripts `typecheck`, `lint`, `format`, `format:check` implemented via `pnpm -r ...`.
- [x] **1.2** Add `pnpm-workspace.yaml` with packages: `shared/*`, `web`, `mobile`.
- [x] **1.3** Add `.nvmrc` with the pinned Node LTS major.
- [x] **1.4** Extend the root `.gitignore` (currently dotnet-flavored) with the Node/pnpm block: `node_modules/`, `*.tsbuildinfo`, `.eslintcache`, pnpm debug logs.
- [x] **1.5** Add root `.npmrc` with `engine-strict=true` (and nothing else until a concrete need appears).
- [x] **1.6** Run `pnpm install` — succeeds on the empty workspace; commit `pnpm-lock.yaml`. **Install complete; lockfile retained uncommitted as requested for this Phase 1 slice.**

**Phase 1 implementation notes — 2026-10-13 (items 1.1–1.6 only):**

- Toolchain verified at implementation time against the [official Node release index](https://nodejs.org/dist/index.json) and [release status](https://nodejs.org/en/about/previous-releases): Node **24.21.0**, Krypton LTS; Node 26 is still Current. `.nvmrc` pins major `24`, with `engines.node` set to `>=24.21.0 <25` to enforce the verified LTS floor and stay on that major.
- pnpm **10.34.6** is the registry's [`latest-10`](https://registry.npmjs.org/-/package/pnpm/dist-tags) release; both `packageManager` and `engines.pnpm` pin that exact version. Its [release metadata](https://registry.npmjs.org/pnpm/10.34.6) requires Node `>=18.12`, and the [pnpm 10 compatibility table](https://pnpm.io/10.x/installation#compatibility) supports Node 24. Selected the maintained 10.x line because it honors the required `engine-strict=true` in `.npmrc`; [pnpm 11 moved non-registry settings out of `.npmrc`](https://pnpm.io/blog/releases/11.0).
- Existing root content was preserved. The existing `node_modules/` ignore rule remains; the appended Node/pnpm block adds `*.tsbuildinfo`, `.eslintcache`, and `pnpm-debug.log*`. No prototypes or files in `server/`, `design/`, or `infra/` were changed.
- Actual install: temporary Node **v24.21.0** and pnpm **10.34.6** executables, with archive checksums verified, ran `pnpm install --store-dir /private/tmp/lotoapp-shared-m1-toolchain/store`. **Exit 0**: `Already up to date`, `Done in 215ms using pnpm v10.34.6`. The store override keeps installation state temporary; global tool installations were unchanged. `pnpm config get engine-strict` returned `true`. Generated `pnpm-lock.yaml` is retained with only the empty root importer and no dependencies.
- Fan-out scripts are configured only. No package typecheck, lint, or format checks were claimed or run: child packages do not exist yet, and Phase 2 supplies tool configs. Phases 2–4 remain pending (apart from the previously completed 4.2); no package skeletons, generated clients, helpers, app scaffolds, or Turborepo were added. No commit was made.

### Phase 2 — TypeScript + lint/format baseline

- [x] **2.1** Add `tsconfig.base.json` at the repo root: `strict`, `noUncheckedIndexedAccess`, `isolatedModules`, `verbatimModuleSyntax`, `module: "esnext"`, `moduleResolution: "bundler"`, `target` at a current evergreen baseline, `skipLibCheck: true`, `noEmit: true`. Record in a comment that app tsconfigs (Next/Expo) extend this and may override module settings their frameworks require.
- [x] **2.2** Add root `eslint.config.mjs`: ESLint 9 flat config with `typescript-eslint` recommended rules plus `eslint-config-prettier` last. Scope it to `shared/**` plus future `web/**`/`mobile/**` TS files; keep framework-specific plugins out of the root config.
- [x] **2.3** Add Prettier config (`.prettierrc` + `.prettierignore`). Pick formatting options once at implementation time; they apply unchanged to `web` and `mobile` later.
- [x] **2.4** Verify root scripts run green on the so-far-empty workspace: `pnpm typecheck`, `pnpm lint`, `pnpm format:check`.

**Phase 2 implementation notes — 2026-10-15 (items 2.1–2.4 only):**

- Dev tools are exact-pinned as root `devDependencies` (versions checked against the npm registry on 2026-10-15). Node/pnpm pins and the root fan-out scripts are unchanged.
  - **TypeScript 6.0.3.** It is the newest release inside `typescript-eslint`'s supported range (`>=4.8.4 <6.1.0`), and it matches the Expo SDK 57 template pin (`~6.0.3`). TypeScript 7.0.2 (`latest`) is outside that range.
  - **ESLint 9.39.5 and `@eslint/js` 9.39.5** (the last 9.x release). ESLint 9 reached end-of-life on 2026-08-06, so pnpm reports it as deprecated. It is still the milestone's line on purpose: `eslint-config-next` 16.4.1 depends on `eslint-plugin-react` 7.37.5, `eslint-plugin-import` 2.32.0 and `eslint-plugin-jsx-a11y` 6.10.2, and all three cap their ESLint peer range at `^9`. **Revisit ESLint 10 once the Next and Expo lint stacks accept it.**
  - **`typescript-eslint` 8.71.2** (it supports ESLint `^9 || ^10`), **`eslint-config-prettier` 10.1.8** and **Prettier 3.9.10**.
- **2.1:** `target` is `es2022`. `lib` is left at the target default; packages that need other libs override it. The base has no `include`, so `tsc -p tsconfig.base.json` correctly reports TS18003 ("no inputs"). Each package adds its own `include`.
- **2.2:** `eslint.config.mjs` uses `defineConfig`, and its TypeScript layer stacks `@eslint/js` recommended under `typescript-eslint` recommended. The layer uses the `**/*.{ts,tsx,mts,cts}` glob instead of per-area paths, because `files` globs resolve relative to the config that applies them. Per-area paths would match nothing once `web/` or `mobile/` import the root config into their own configs. `server/`, `design/`, `docs/` and `infra/` are covered by global ignores. `eslint-config-prettier/flat` comes last. The rules are not type-aware (no `parserOptions.project`).
- **2.3:** `.prettierrc` sets `printWidth: 100`, `singleQuote: true`, `trailingComma: "all"` and `endOfLine: "lf"`; everything else stays at Prettier defaults. `.prettierignore` excludes the lockfile, the non-TS areas, hand-maintained docs (`**/docs/`, `**/AGENTS.md`, `CLAUDE.md`) and build output. **For Phase 3 and the app scaffolds:** Prettier 3 reads ignore files only from its working directory. Each package's `format` and `format:check` scripts must therefore pass the root ignore files explicitly, for example `prettier --check . --ignore-path ../../.gitignore --ignore-path ../../.prettierignore` (use `../` from `web/` and `mobile/`). Patterns resolve relative to the ignore file. Without these flags, package-level checks pick up files the root ignores (verified in the fixture below).
- **2.4:** All commands ran under checksum-verified temporary Node **v24.21.0** and pnpm **10.34.6**, with a temporary `--store-dir`.
  - Results: `pnpm install --frozen-lockfile` exits 0. `pnpm typecheck`, `pnpm lint` and `pnpm format:check` each exit 0 with `No projects matched the filters`.
  - That result means the scope is empty, not that a package passed. The workspace has no child packages yet, and `pnpm -r` excludes the root project.
  - The new root config files pass `prettier --check`. `eslint --print-config` confirms that the root config applies to `shared/**` TS paths and ignores `server/**`.
- **Config proof, not package validation.** A throwaway copy of the workspace was built outside the repo with one `shared/*` fixture package and a `web/` fixture whose `eslint.config.mjs` spreads the root config. It checked three things:
  - The root scripts reach both packages, and the packages run the root-installed `tsc`, `eslint` and `prettier` binaries.
  - Clean code passes all three gates.
  - Seeded violations fail each gate: TS1484 (`verbatimModuleSyntax`), TS2322 (`noUncheckedIndexedAccess`), `@typescript-eslint/no-unused-vars` in both packages, `@typescript-eslint/no-explicit-any`, and a Prettier style warning.

  Real package validation is Phase 3.5.

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

> Note: the companion `web/AGENTS.md` amendment (route-handler BFF expected for auth/cookies) belongs to the web area; it landed with `web/docs/milestones.md` on 2026-10-14.

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
- Pure TS only — no Intl-locale surprises across platforms without a pinned strategy (decide `Intl` vs hand-rolled per formatter in-phase, using the Hermes `Intl` behavior measured in Mobile M1 Phase 4, and record the decision).

**Precondition:** Milestone 2 (generated enum types) and Design M0 Phase 2 (the formatting spec).
