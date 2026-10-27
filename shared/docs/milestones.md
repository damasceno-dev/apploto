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
- **Standing gates:** `pnpm -r typecheck` and `pnpm -r lint` stay green on every commit touching `shared/`. A test runner (Vitest) arrives with Milestone 2 (D1, below): the client core is the first logic worth testing. *(Amended 2026-10-17; previously "arrives with Milestone 3".)*

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

> **Amendment (2026-10-17, owner decision during server SV22 close-out): Node 24 → Node 26.** The owner moved the project toolchain to Node 26 instead of installing Node 24 next to the machine's Node 26. `.nvmrc` is now `26`, `engines.node` is `>=26.6.0 <27`, and `engine-strict=true` and pnpm `10.34.6` are unchanged.
> - **Recorded deviation:** the official release index (latest entries dated 2026-10-14) still lists Node 26 as **Current** (`v26.12.0`, `lts: false`), while `v24.22.0` is the active LTS. The 1.1 "current LTS floor" rule is therefore knowingly relaxed until Node 26's LTS promotion, which is expected in late October 2026. When that lands, re-check the index and record it here.
> - **The floor is `26.6.0`,** the version installed on the development host (released 2026-08-03), so the standing gates run without a new download. Any later 26.x also satisfies it.
> - **Not yet affected:** `web/` and `mobile/` have no packages yet. When the Next and Expo apps are scaffolded, confirm that their toolchains support Node 26 before pinning their versions.
> - **Verification on the new pin (2026-10-17):**
>   - Node `v26.6.0` and pnpm `10.34.6` ran `pnpm install --frozen-lockfile`: exit 0, and the `pnpm-lock.yaml` sha256 `082dc0a2…d07d` is unchanged.
>   - `pnpm config get engine-strict` → `true`.
>   - `pnpm -r typecheck`, `pnpm -r lint` and `pnpm format:check` → exit 0 for both `@loto/api` and `@loto/core`.
>   - Negative check: temporarily restoring `<25` made pnpm stop with `ERR_PNPM_UNSUPPORTED_ENGINE`, so the gate still enforces the range.
> - The historical notes below stay as recorded: they describe the Node 24 runs of their time.

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

- [x] **3.1** Add `shared/api/package.json`: name `@loto/api`, `"private": true`, `"type": "module"`, a `typecheck` script, and a **two-entry `exports` map** (source package — no build step, no `main`/`dist`): root entry `.` → `./src/index.ts` (contract types + raw fetchers — server-safe, must never transitively import react-query) and `./queries` → `./src/queries/index.ts` (TanStack Query hooks — client-only). Server Components import from `@loto/api`; client components and mobile screens import hooks from `@loto/api/queries`.
- [x] **3.2** Add `shared/api/tsconfig.json` extending `tsconfig.base.json` with the package's `include`.
- [x] **3.3** Add `shared/api/src/index.ts` and `shared/api/src/queries/index.ts` (placeholder exports backing the two `exports` entries) plus the pre-agreed folder structure from `shared/AGENTS.md`: `src/client/` (API client setup — filled by Milestone 2) and `src/generated/` (Orval output target — filled by Milestone 2), each with `.gitkeep`.
- [x] **3.4** Add the same trio for `shared/core`: `package.json` (`@loto/core`), `tsconfig.json`, `src/index.ts` placeholder, plus empty module folders `src/format/`, `src/validate/`, `src/labels/`, `src/dates/` (filled by Milestone 3).
- [x] **3.5** Verify `pnpm install && pnpm -r typecheck && pnpm -r lint` green with both packages in the workspace; confirm no package declares dependencies yet (runtime deps like `@tanstack/react-query` arrive with Milestone 2 as peer dependencies, by design).

**Phase 3 implementation notes — 2026-10-15 (items 3.1–3.5 only):**

- **Package shape.** Both packages are private ESM source packages with no `version`, `main`, `dist/`, or build script.
  - `@loto/api` exports exactly two entries: `.` → `./src/index.ts` and `./queries` → `./src/queries/index.ts`.
  - `@loto/core` exports a single entry, `.` → `./src/index.ts`.
  - Each package has `typecheck` (`tsc --noEmit`), `lint` (`eslint .`), `format` and `format:check` scripts, so the root fan-out scripts reach it. The Prettier scripts pass the root `.gitignore` and `.prettierignore` via `--ignore-path` (see the Phase 2 note).
  - Each `tsconfig.json` extends `../../tsconfig.base.json` with `include: ["src"]` and no overrides.
- **Placeholders.** The three entry files contain only a comment and `export {}`, which keeps them modules under `isolatedModules`. Nothing is exported and nothing is imported yet. `.gitkeep` files hold `api/src/client/`, `api/src/generated/` and `core/src/{format,validate,labels,dates}/`.
- **Import boundary.** For now the boundary holds by structure: the root `@loto/api` entry imports nothing, and only the `./queries` entry is meant to reach TanStack Query. The automated boundary check (a lint rule or an import-graph script) remains Milestone 2 scope, as written there.
- **Dependencies.** No package declares `dependencies`, `devDependencies` or `peerDependencies`. The packages use the root-installed `tsc`, `eslint` and `prettier`. The lockfile gains only two empty importers, `shared/api: {}` and `shared/core: {}`.
- **3.5 results.** All commands ran under temporary Node **v24.21.0** and pnpm **10.34.6**.
  - `pnpm install` and `pnpm install --frozen-lockfile` both exit 0.
  - `pnpm -r list` reports **3 workspace projects**: the root `lotoapp` plus `@loto/api` and `@loto/core`. Recursive runs report `Scope: 2 of 3 workspace projects`, because the root is excluded.
  - `pnpm -r typecheck`, `pnpm -r lint` and `pnpm format:check` exit 0 in both packages. `tsc --listFilesOnly` and ESLint's JSON output show all three entry files checked.
  - A temporary seeded file in each package made every gate fail in that package: TS2322 (`noUncheckedIndexedAccess`), `no-unused-vars`, `no-explicit-any`, and a Prettier warning. The seeds were removed and all gates went green again.
  - No `dist/`, `.js`, `.d.ts` or `.tsbuildinfo` output exists under `shared/`.
- **Consumer resolution (scratch copy, outside the repo; groundwork for 4.3, not the 4.3 check).** A fixture `web/` package depended on both packages via `workspace:*`, which works without a `version` field.
  - Imports of `@loto/api`, `@loto/api/queries` and `@loto/core` pass `tsc`.
  - Deep imports such as `@loto/api/src/client` fail with TS2882.
  - Node's `import.meta.resolve` maps the three entries to their `src` files and rejects unexported paths with `ERR_PACKAGE_PATH_NOT_EXPORTED`.

### Phase 4 — Docs sync + close-out

- [x] **4.1** Update `shared/AGENTS.md`: promote `shared/core` from "later if needed" to day-one (recommended 2026-06-07, confirmed 2026-06-10); document the source-package pattern (no build step; Next `transpilePackages` / Metro), the `@loto/api` / `@loto/api/queries` export split, the `@loto/*` scope, the toolchain pins, and restate the no-env-var-reads rule. Keep it the single source of truth for the area.
- [x] **4.2** Align all agent docs that called `shared/core` optional/later → **day-one**: root `AGENTS.md` (root `CLAUDE.md` is a symlink to it), `shared/AGENTS.md` (core lines), and `mobile/AGENTS.md`. **Done 2026-06-30** in the frontend-foundation commit, to remove the milestone-vs-AGENTS contradiction. The broader `shared/AGENTS.md` rewrite (source-package pattern, export split, toolchain pins) stays in 4.1.
- [x] **4.3** Done criteria: fresh clone → `pnpm install && pnpm -r typecheck && pnpm -r lint` green; both packages resolve in the workspace; no build artifacts or `dist/` anywhere; no env-var reads anywhere under `shared/`.

**Phase 4 implementation notes — 2026-10-15 (items 4.1 and 4.3; 4.2 was already done 2026-06-30):**

- **4.1:** `shared/AGENTS.md` is rewritten as the area's single source of truth. It now covers:
  - the `@loto/*` packages and their exact `exports` maps: the server-safe `@loto/api` root versus client-only `@loto/api/queries`, the single-entry `@loto/core`, and the rejection of deep imports
  - the source-package pattern: no build step, `main`, `dist/` or emitted JS; Next `transpilePackages` and Metro; ESM, private, `workspace:*`
  - per-package scripts, including the Prettier `--ignore-path` requirement
  - the Node, pnpm and exact dev-tool pins
  - configuration injection: no `process.env`, `import.meta.env`, `NEXT_PUBLIC_*` or `EXPO_PUBLIC_*` reads; apps supply config and own storage
  - app-owned UI and platform code

  Every earlier decision and rule is kept. `shared/core` was already day-one in `AGENTS.md`, and its existing "decided 2026-06-26" wording is preserved; that date differs from the 2026-06-07/06-10 dates in 4.1's text, and the discrepancy is left as found. The stated gates add `pnpm format:check`, matching the web and mobile standing gates.
- **4.3:** Verified in a fresh `git clone` of `parallel/shared` at `d3b80e5`, with this delivery's uncommitted doc changes applied. The run used temporary Node **v24.21.0**, pnpm **10.34.6** and a new, empty package store.
  - `pnpm install && pnpm -r typecheck && pnpm -r lint` exits 0, and the install leaves `pnpm-lock.yaml` unchanged. `pnpm format:check` also exits 0. Every recursive run reports `Scope: 2 of 3 workspace projects` (`@loto/api` and `@loto/core`).
  - **Both packages resolve.** Through package self-reference, Node maps `@loto/api`, `@loto/api/queries` and `@loto/core` to their `src` entry files and rejects unexported paths with `ERR_PACKAGE_PATH_NOT_EXPORTED`.
  - **Consumer check.** A disposable `workspace:*` consumer inside the scratch clone type-checks imports of all three entries, and a deep import fails with TS2882.
  - **No dependencies.** Neither package manifest declares any dependency field.
  - **No build output.** After the gates ran, the clone has no `dist/`, `build/`, `bin/` or `obj/` directories and no `.js`, `.d.ts` or `.tsbuildinfo` files. The only `.mjs` file is the root `eslint.config.mjs` source. `git status --ignored` lists only `node_modules/`.
  - **No environment reads.** No source or manifest file under `shared/` contains `process.env`, `import.meta.env`, `NEXT_PUBLIC_*` or `EXPO_PUBLIC_*`. The only matches are the rule statements in `shared/AGENTS.md` and this file.
- **Milestone 1 is complete (2026-10-15):** every Phase 1–4 item is checked and the 4.3 done criteria hold. This unblocks Shared M2 (which still needs backend Milestone 7.6), Web M1 and Mobile M1. The "only Shared M1 Phase 1 has landed" precondition lines in `web/docs/milestones.md` and `mobile/docs/milestones.md` are now stale; those docs belong to their area owners and were not edited here.

> Note: the companion `web/AGENTS.md` amendment (route-handler BFF expected for auth/cookies) belongs to the web area; it landed with `web/docs/milestones.md` on 2026-10-14.

---

## Milestone 2 — API Contract Pipeline

**Goal:** A deterministic OpenAPI → TypeScript pipeline: export the server OpenAPI document without hand edits, generate types + raw fetch functions (Server-Component-safe) + TanStack Query hooks via Orval, and ship the hand-written client core that everything generated plugs into.

**Scope boundary / key behaviors (phases planned 2026-10-15 — see below):**

- Deterministic OpenAPI export from `server.API` into the repo (script-driven; build-time document generation preferred over scraping a running server — decide in-phase and record it). The committed document is the generation input, so contract drift is always visible in diffs.
- Orval config producing three layers: contract types, raw `fetch` functions usable from Next Server Components, and TanStack Query hooks for client components and mobile screens. Output is mapped so hooks land behind the `./queries` entry while the root entry stays hook-free (exact Orval output layout decided in-phase).
- **Export hygiene invariant:** the root `@loto/api` entry exposes types + raw fetchers only and must never transitively import `@tanstack/react-query`; hooks are reachable only via `@loto/api/queries`. Pin it with a lightweight check (import-boundary lint rule or a small script walking the import graph of `src/index.ts`).
- **Enum-name gate (consumer side):** after generation, assert the generated TS exposes enum *names* (string unions / named enum objects), never bare numeric unions like `0 | 1 | 2`. The server-side half of this guarantee is the backend OpenAPI Contract Hardening milestone (see this milestone's precondition) — the server emits a named-enum contract; this gate proves codegen preserved it.
- Hand-written client core in `src/client/`: base-URL + token injection via a caller-provided provider interface (apps own storage — cookies on web, secure store on mobile), aware of the **two-stage token model** (identity token from `POST /user/login` vs branch-scoped token from `POST /branch/session`; refresh via `POST /user/renew-token`), and `ResponseErrorJson` normalization into one typed `ApiError`.
- Required-field fidelity gate: a static assertion that required C# fields are non-optional in generated TS, pinned against known DTOs (the promise `server/AGENTS.md` already makes).
- `pnpm generate` regeneration script + a drift check (regenerate and fail on `git diff`) that M9's CI can adopt later. Regenerating after each backend phase lands is routine, not an event.

**Precondition:** Milestone 1, plus the backend **OpenAPI Contract Hardening** milestone landed on the server track, so generation consumes a named-enum contract. That milestone now exists as **Milestone 7.6 — Backend OpenAPI Contract Hardening** in `server/docs/milestones.md` (planned during the M7 Phase 12 close-out, where the project-wide `required`-metadata gap was relocated to it); it must land before this milestone starts. Its contents: public contract enums serialize as *names* on responses (integer input still accepted during transition); the OpenAPI document lists named enum values (`Draft`, `Active`, `Cancelled`, …); WebApi/OpenAPI tests pin at least one request enum and one response enum; required/nullability fidelity audited on a sample of DTOs; spec-sync revision bump per the server doc-sync convention. Otherwise this milestone tracks the server OpenAPI at whatever spec revision is current — it does not wait for M7 to close.

**Precondition status (2026-10-15):** Shared M1 is complete (`40f8ac2`). M7.6 is complete (`6fc1fda`, spec `v41`). Generation is still blocked by the **SV03 tripwire** below.

**Tripwire — SV03 (manager-queue rename).** Server M11 and design gap Q say the rename moves into the server milestone that precedes the first generated TypeScript client if the web build starts before M11 is scheduled. Shared M1's close-out unblocked Web M1, so this plan treats the tripwire as **tripped**.

- The server track owns the rename (coordination label **SV03**): `GET /report/dashboard` → `GET /report/manager-queue`, and `ResponseDashboard*Json` → `ResponseManagerQueue*Json` with field names unchanged, together with the snapshot and the doc-sync group.
- No generated TypeScript may be committed from a document that still contains `/report/dashboard`. The obsolete dashboard surface is never generated.
- As of this plan, SV03 has not landed. The exported document below still has `/report/dashboard`.

### Planning record (2026-10-15)

- **Backend input inspected.** The last server code change is `b860103` (M7.7 Phase 7.5), at spec **`v43`** (`7620ffb`, docs-only).
  - `server/` and `docs/` are identical on `parallel/shared` `40f8ac2` and `main` `e4544a8`.
  - M7.7 is still open: Phase 5 items, Phase 7.6 and Phase 8 remain. The pipeline regenerates as those phases land.
  - The first committed generation input is the **SV03-integrated** server commit. It is recorded in item 1.1, not here.
- **Export prototype (throwaway copy, nothing committed).** I added `Microsoft.Extensions.ApiDescription.Server` 10.0.11 (matching `Microsoft.AspNetCore.OpenApi` 10.0.11) to a scratch copy of `server.API` with `OpenApiGenerateDocuments=true`, then ran `dotnet build -c Release`.
  - **Without config, the build fails.** `InfraDependencyInjection.AddDbContext` throws `Missing required configuration: ConnectionStrings:DefaultConnection` during host construction.
  - **With inert placeholders, the build succeeds.** I set process-scoped environment variables `ConnectionStrings__DefaultConnection=Host=openapi-export.invalid;…` and `Token__SigningKey=<non-secret placeholder>`, with no `ASPNETCORE_ENVIRONMENT`. The generator builds the host without starting it, so no database or hosted service runs.
  - Two builds (the second `--no-incremental`) produced byte-identical documents: sha256 `2cec6f9b625e1439a3b1a72fe209520bc3014672089e76f036a31d4bb2c6a0d3`.
  - Contents: OpenAPI 3.1.1, no `servers`, 68 paths, 96 operations, 150 component schemas (14 enum components, all `type: string`, none numeric).
  - `Idempotency-Key` and `If-Match` are required header parameters, and `ETag` is declared on 200/201 versioned responses.
  - `ResponseErrorJson.errorMessages` is required with type `["null","array"]` of `string`.
  - **Generation must be explicitly disabled for ordinary builds** (review finding, 2026-10-15). If `OpenApiGenerateDocuments` is left unset, the package's `Microsoft.Extensions.ApiDescription.Server.targets` defaults it to `true`, and `OpenApiGenerateDocumentsOnBuild` follows it. A plain `dotnet build` with no config then fails with `Missing required configuration: ConnectionStrings:DefaultConnection`; I reproduced this in the scratch copy.
    - With `<OpenApiGenerateDocuments>false</OpenApiGenerateDocuments>` in the project, a no-config `dotnet build --no-incremental` succeeds and writes no document.
    - An export build with `-p:OpenApiGenerateDocuments=true -p:OpenApiDocumentsDirectory=<temp>` plus the inert environment variables still writes the same sha256 `2cec6f9b…` document.
    - A following ordinary build succeeds again.
- **Decision — build-time export is chosen.** The Development-only `MapOpenApi()` endpoint is not an export service: it needs a running server with a database and exists only for local exploration. A `WebApplicationFactory` harness was the fallback, and is not needed because build-time generation works without Docker or PostgreSQL.
- **Findings that need a decision before generation** (recorded here so they are not decided silently):
  - **F1 — numeric widening.** .NET's web JSON defaults (`AllowReadingFromString`) emit every number as `["number","string"]` or `["integer","string"]`, response members included. Examples: `ResponseTransactionJson.value` and `.version`, and `ResponseDashboardBacklogJson.*Count`. The contract snapshot pins this (`number|string`, 146 lines). Generated TypeScript would type money and `Version` as `number | string`, while responses actually serialize numbers. Shared must not patch the document. See decision **D2**.
  - **F2 — no auth-tier metadata.** The document has one global `Bearer` requirement and no per-operation `security`. The code has three tiers — `[AllowAnonymous]` (login/register/renew-token), `[TokenAuthenticate]` (identity token: branch create, my-branches, session) and `[TokenAuthenticateBranch]` / `[TokenAuthorize]` (branch token) — but a generated client cannot tell which token an operation needs. See decision **D3**.
- **Design input.** The design 0.4.2 cross-screen patterns (`design/docs/screens.md` on `main` `e4544a8`, not yet merged into `parallel/shared`) set the transport rules below:
  - The caller freezes the `Idempotency-Key` and payload before dispatch.
  - Any 5xx, timeout, lost response, connection loss or **cancellation after dispatch** on a write means **Resultado não confirmado**, with the immutable candidate and key kept. Cancelling the wait cannot prove the server rolled back.
  - Never mint a new key on retry, and never blindly replay unguarded writes.
  - A 401 recovery cycle is bounded, never recursive on `/user/login` or `/user/renew-token`, and owned by the app.

### Decisions for review before Phase 1 starts

- **D1 — Vitest moves from M3 to M2.** The client core is the first logic in `shared/`, and its no-retry, header and error guarantees need tests. This amends the "Vitest arrives with Milestone 3" standing rule. M3 then adds formatter tests to an existing runner.
- **D2 — numeric fidelity (F1).** *Recommended:* a narrow server handoff (**H3**). A direction-aware schema transformer emits `number` / `integer` (dropping the `string` alternative) on **response** members, while request members keep advertising what binding accepts. Spec §6.15 and the doc-sync group would be bumped and the snapshot updated deliberately. *Alternative:* accept `number | string` in generated types, which leaves every consumer narrowing money and `Version`. Not recommended. Phase 2's numeric gate follows whichever option is chosen.
- **D3 — auth tier per operation (F2).** *Recommended:* a narrow server handoff (**H4**). An operation transformer derives per-operation `security` from the existing filters: `[]` for anonymous, `IdentityBearer`, and `BranchBearer`. Shared then generates an operation→tier map from the document and passes the tier to the app's token provider. *Fallback:* the provider receives `{ method, path }` and each app maps paths to tokens. That duplicates a contract fact by hand in two apps, so it is accepted only if the server declines H4, and is recorded as such.
- **D4 — committed document and generated output stay out of manual formatting and lint.** The committed document lives at `shared/api/openapi/server-api.openapi.json`. Generated code lives under `shared/api/src/generated/`, is formatted by Orval's Prettier hook with the pinned root Prettier, and is excluded from ESLint. The exported JSON is added to `.prettierignore`, so `format:check` never rewrites a contract byte. Both changes touch root tooling and need the root-tooling owner's sign-off.

> **Adoption note (2026-10-17, server SV22 — technical choices independently accepted; see the final acceptance/landing record below).** D1: Vitest begins in M2; this changes policy only (the line above and `shared/AGENTS.md`), and installing the runner remains item 3.1. D2: the recommended response-only numeric fidelity is adopted and implemented by the server as **H3** (server M7.6 follow-up Phase 4.2); the `number | string` alternative is not taken. D3: the recommended derived per-operation tiers are adopted and implemented as **H4** (Phase 4.3) with schemes `IdentityBearer` and `BranchBearer`, `security: []` for anonymous operations, and `[TokenAuthorize]` role names listed on `BranchBearer` (OpenAPI 3.1 allows role names for non-OAuth schemes); the `{ method, path }` fallback is not needed. Scheme names are metadata labels: both tokens travel in the same `Authorization: Bearer` header. D4: adopted as stated; the `.prettierignore`/ESLint changes stay with the root-tooling owner and are not made by the server track. H1 and H2 shipped earlier and are not reopened.

### Server-file handoffs (agree ownership before any edit; shared never edits `server/` unless the agreement assigns it)

- **H1 — SV03 rename.** Owned by the server track and already coordinated. It is the tripwire gate above. Files: the server track's choice, plus `tests/WebApi.Test/OpenApi/Snapshots/openapi-contract-surface.txt` and the `docs/` doc-sync group.
- **H2 — opt-in build-time export.** One file: `server/server.API/server.API.csproj`.
  - Add `Microsoft.Extensions.ApiDescription.Server` with an exact version matching `Microsoft.AspNetCore.OpenApi` (10.0.11 today) and `PrivateAssets=all`.
  - Set `<OpenApiGenerateDocuments>false</OpenApiGenerateDocuments>` **explicitly**. Leaving it unset makes the package default it to `true`, which breaks every ordinary build and `dotnet test`. Only the shared export script overrides it, as a global property: `-p:OpenApiGenerateDocuments=true -p:OpenApiDocumentsDirectory=<temp dir>`.
  - **Ordinary-build regression check**, run by the server owner when H2 lands and repeated in 1.2:
    - With no `ConnectionStrings__*` or `Token__*` variables and no Development environment, `dotnet build server/server.sln --no-incremental` succeeds and writes no OpenAPI document.
    - The existing `dotnet test` suites build unchanged.
  - Not required, but recommended to the server owner: pin `Scalar.AspNetCore` (currently `Version="*"`) so export builds restore reproducibly.
- **H3 — response numeric narrowing (if D2 adopts it).**
  - One new transformer in `server/server.API/OpenApi/` and its registration line in `ApiDependencyInjection.cs`.
  - One WebApi.Test OpenAPI contract test, and the reviewed snapshot update.
  - A §6.15 note plus a doc-sync revision bump.
- **H4 — per-operation auth tiers (if D3 adopts it).**
  - One new operation transformer in `server/server.API/OpenApi/` and its registration line.
  - One WebApi.Test contract test pinning one anonymous, one identity and one branch operation, and the reviewed snapshot update.
  - A §6.15 note plus a doc-sync revision bump.

> **H3/H4 ownership record (2026-10-17, server SV22; input for item 1.2, which Shared ticks once the landing commit exists).** Owner: the server track (server M7.6 follow-up Phase 4). Baseline `7e8b169`, spec `v52`. Landing commit: **`763edd7`**, spec `v52` (recorded after the accepted implementation commit was created). The delivered file set is wider than the plan above, because the tier derivation is a separate resolver and the evidence needed more than one test:
> - Server: `server/server.API/OpenApi/ResponseNumericFidelityOpenApiDocumentTransformer.cs` (H3), `OperationTokenRequirementResolver.cs` and `OperationSecurityOpenApiOperationTransformer.cs` (H4), and the registration plus the two scheme definitions in `server/server.API/ApiDependencyInjection.cs`.
> - Tests (`server/tests/WebApi.Test/OpenApi/`): `ResponseNumericFidelityTransformerTest.cs`, `ResponseNumericFidelityGeneratorTest.cs`, `ResponseNumericWireFidelityTest.cs`, `ResponseNumericFidelityOpenApiContractTest.cs`, `OperationTokenRequirementResolverTest.cs`, `OperationSecurityOpenApiContractTest.cs`, `OperationSecurityPrecedenceTest.cs`, `AuthTierProbeHost.cs`, the `OpenApiContractSurface.cs` renderer (security section and per-operation `security` line), and the reviewed `Snapshots/openapi-contract-surface.txt`.
> - Docs: `docs/loto-specs.md` §6.15 and the doc-sync group at `v52`, and `server/AGENTS.md` OpenAPI rules. `design/docs/product.md` / `screens.md` need no semantic change. After the round-3 review accepted the reconciliation, their markers and the `screens.md` source-contracts reference moved to `v52`, and the actual doc-sync checker plus 34/34 regression scenarios pass.
> - Acceptance: technical/code and semantic review PASS (round 3, 2026-10-17); server Phase 4 items 4.1–4.5 are checked. The Shared standing gates now pass on the owner-chosen Node 26 pin (`>=26.6.0 <27`; see the Shared M1 amendment): `pnpm -r typecheck`, `pnpm -r lint` and `pnpm format:check` all exit 0. Final documentary/toolchain review also passed with no findings; the actual landing reference is `763edd7`. Server Phase 4 item 4.6 is now closed. Shared repeats its own integration/input gates in Phase 1.
> The evidence (commands, hashes and parity) is in the server milestone's Phase 4 delivery note. Shared's own export script, committed input and generation (1.3 onward) remain Shared work.
> - **Export constraint for 1.3** (found by the SV22 independent review and reproduced): once a Release export is cached, an incremental export build pointed at a *fresh* `OpenApiDocumentsDirectory` returns success but writes no document. A `--no-incremental` export to a fresh directory regenerates it. The export script must therefore force regeneration (`--no-incremental`, or an equivalent target invalidation) and must fail when the document is missing, never treat a skipped target as a fresh export. This is MSBuild/H2 caching behavior, not a transformer change.
> - **Shared standing gates:** SV22 changes only `shared/AGENTS.md` and `shared/docs/milestones.md`, both outside `format:check` by `.prettierignore` (`**/AGENTS.md`, `**/docs/`), and no TypeScript. `pnpm -r typecheck/lint/format:check` could not run on Node `>=24.21.0 <25` during review. After the owner moved the pin to Node 26 (Shared M1 amendment), all three exit 0.
> - **Generated names:** a component that both a request and a response reach is published twice — the input under its own id, the response as `{id}Output`. Today's contract has none (only string enums are shared), so no `…Output` component exists yet.

### Phase 1 — Contract input and deterministic export

- [x] **1.1** Tripwire gate: confirm SV03 is integrated. Record in the milestone notes:
  - the server commit and spec revision
  - that the snapshot and exported document contain `/report/manager-queue` and `ResponseManagerQueue*Json`, and no `/report/dashboard`

  Phases 2–4 commit nothing generated before this item is checked.
- [x] **1.2** Record the agreed ownership of H2, and of H3/H4 if D2/D3 adopt them: exact files, owner and landing commit. The server owner lands H2 before 1.3 runs against the real tree. Record the H2 ordinary-build regression evidence: a no-config `dotnet build server/server.sln --no-incremental` succeeds, and no OpenAPI document is written without the explicit `-p:OpenApiGenerateDocuments=true` override.
  - Server input handoff is accepted at `763edd7`, spec `v52`; H1/H2 were delivered earlier at `26fb2bb`. Exact H3/H4 files, owner and evidence are recorded above. This item remains for the Shared owner to verify in the actual integrated checkout and repeat the ordinary-build check; the server landing does not implement 1.3–1.6.
- [x] **1.3** Add the export script `shared/api/scripts/export-openapi.ts`, run with Node's built-in type stripping (Node 26 per the toolchain pin), and the package script `export:openapi`.
  - It runs `dotnet build server/server.API/server.API.csproj -c Release -p:OpenApiGenerateDocuments=true -p:OpenApiDocumentsDirectory=<temp>`.
  - Inert placeholder config is passed only in the child process environment (an `.invalid` host and a non-secret signing-key placeholder, never Development credentials or a reachable database).
  - The resulting `server.API.json` is copied unmodified to `shared/api/openapi/server-api.openapi.json`.
  - The script fails if the build fails or the document is missing.
- [x] **1.4** Determinism and parity evidence. Export twice; the outputs must be byte-identical (record the sha256).
  - Prove parity with the runtime contract once: the exported document must equal the Development `/openapi/v1.json` from the WebApi.Test host, excluding `servers`. Equivalently, it must render to the checked-in `openapi-contract-surface.txt` through `OpenApiContractSurface.Render`.
  - Record the method, the result, and the path, operation and schema counts.
- [x] **1.5** Add `export:check`: re-export to a temp file and fail on any byte difference from the committed document. It requires the .NET 10 SDK; record that requirement and that server M9 CI can adopt it.
- [x] **1.6** Commit the first document only after 1.1 passes. The milestone notes record the input server commit, spec revision and document sha256. Every later regeneration repeats the same record.

### Phase 2 — Generated contract types and raw functions (root entry)

- [x] **2.1** Pin Orval at an exact 8.x release published at least 14 days before the pin. Today's `latest` is 8.43.0, published 2026-10-14, so it doesn't qualify yet. Add it as an `@loto/api` `devDependency`, coordinating the lockfile with its single owner, and record the version, date and engine (`node >=22.18.0`).
- [x] **2.2** Add `shared/api/orval.config.ts` (input: the committed document only) with these settings:
  - the `fetch` client
  - models in `src/generated/model/`
  - raw functions in `src/generated/endpoints/`, split by tag
  - a mutator pointing at the hand-written transport in `src/client/` (2.3)
  - **`headers: true`.** Orval normalizes it to `outputOptions.headers ?? false` (verified in the published 8.38.0 and 8.43.0 sources), so without it the declared `Idempotency-Key` and `If-Match` header parameters drop out of generated signatures and the 2.8 gate cannot pass. Confirm support in the exact pinned release; 2.8 proves it.
  - **`clean: true`** on the output directories, so a removed endpoint or model never survives as a stale file
  - `useDates` off, so `date` and `date-time` values, including the offset-free `BranchLocalDateTime`, stay strings and never become JavaScript instants
  - Prettier formatting through the pinned root config

  Generated files are never hand-edited.
- [x] **2.3** Add the minimal transport the mutator needs in `src/client/`. It builds the URL from the injected base URL, attaches the injected token, and passes request and response through with headers intact. The full contract is completed in Phase 3. `generate` is the package script that runs Orval.
- [x] **2.4** The root entry `src/index.ts` exports generated models, raw functions and the client-configuration API, and nothing from `src/generated/queries/` or `@tanstack/*`.
- [x] **2.5** Codegen determinism and drift. Running `generate` twice produces a byte-identical `src/generated/` tree (record the hash). It needs no .NET.
  - `generate:check` (`shared/api/scripts/check-generated.ts`) never relies on `git diff`. That command ignores untracked additions, and a repo-relative pathspec silently matches nothing when run from `shared/api`; review reproduced both false passes.
  - Instead, the script resolves every path from its own location (`import.meta.dirname`), so the working directory never matters. It generates cleanly from the committed document into a fresh temporary directory, with the same config and output layout, and compares that tree recursively with the committed `src/generated/`, byte for byte.
  - It fails on any **modified**, **added** or **deleted** file and lists each difference.
  - Prove it with three seeded cases (one tracked file modified, one extra generated file added, one committed file deleted), each run from the repository root **and** from `shared/api`. All six runs must fail, and a clean tree must pass from both directories.
- [x] **2.6** **Enum-name gate.** Compile-time assertions in `shared/api/src/contract-checks/` run under `pnpm -r typecheck`, against `TransactionStatus`, `DailyCloseStatus` and `Role`. Declared names are assignable, `// @ts-expect-error` proves numeric literals are not, and a check confirms no generated enum type is a numeric union.
- [x] **2.7** **Required/nullable fidelity gate.** Compile-time assertions against known DTOs, taken from the document as inspected:
  - `ResponseTokenJson.token` and `.refreshToken` are required `string`.
  - `ResponseCreateBranchSessionJson.token` and `.branch` are required.
  - `ResponseErrorJson.errorMessages` is a required key whose type includes `null`.
  - `RequestCreateTransactionJson.clientId` is optional and nullable.
  - `ResponseGetCurrentBranchSummaryJson.branchLocalDate` is a required `string`.
  - **Numeric fidelity** follows D2: `ResponseTransactionJson.value` and `.version` are `number`, or the accepted widened type is recorded.
- [x] **2.8** **Financial-header fidelity gate.** Compile-time assertions:
  - The generated `POST /transaction` and `POST /transaction/installment` functions **require** an `Idempotency-Key` header argument.
  - `PUT /transaction/{transactionId}` and `POST /transaction/{transactionId}/finalize` require `If-Match`.
  - The `ETag` response header is readable from the raw function's result.

### Phase 3 — Client core (`src/client/`)

- [x] **3.1** Introduce Vitest, per D1. Pin an exact version as an `@loto/api` `devDependency` and add a `test` script. A root `test` fan-out (`pnpm -r test`) is a root-tooling change for its owner. Tests use an injected fake `fetch` and never touch the network.
- [x] **3.2** **Configuration and injection.** Apps configure the client explicitly, for example `configureApiClient({ baseUrl, getAccessToken, fetch? })`.
  - Nothing happens at import time, and nothing reads `process.env`, `import.meta.env`, `NEXT_PUBLIC_*` or `EXPO_PUBLIC_*`.
  - Using the client before it is configured throws a typed configuration error.
  - The token provider is called on **every** request, never captured, so web's per-request cookie reads and mobile's secure-store reads stay correct.
  - Each call can pass its own `RequestInit`, including an `AbortSignal`.
- [x] **3.3** **Token-provider contract and two-stage sessions.**
  - The provider receives `{ method, path, authTier }` (tier per D3; `authTier` is absent under the D3 fallback). It returns a bearer token or `null`. `null` sends no `Authorization` header, which lets web's browser runtime use a base URL for the cookie-to-bearer BFF route.
  - The generated `POST /user/login`, `POST /branch/session` and `POST /user/renew-token` functions are exposed unchanged.
  - Shared never stores, caches or renews tokens, never reads cookies or the secure store, never orchestrates sessions, and contains no BFF or Expo code. Web M2 and Mobile M2 own those.
- [x] **3.4** **Typed `ApiError`.** It normalizes a `ResponseErrorJson` body, using the generated type only, and carries `status`, method, path and `errorMessages`.
  - The message list is kept verbatim, in order, without deduplication; a `null` or missing list becomes an empty list. Whether an error body was present is recorded.
  - Failures without a response (network, timeout, abort) become a distinct kind with no status. Each records whether the request was **dispatched**, meaning the transport had already invoked `fetch`.
  - An abort **before dispatch** (the signal was already aborted, or aborted before `fetch` was invoked) sends nothing and is not uncertain.
  - An abort **after dispatch** is treated like a timeout or lost response. Cancelling the wait cannot prove the server rolled back, so the transport assumes the request may have reached it.
  - Pure predicates `isApiError` and `isOutcomeUncertain(error)` are exported. `isOutcomeUncertain` is true for a dispatched non-GET that ended in 5xx, timeout, connection loss, lost response or cancellation after dispatch. It is false for any GET, any non-5xx HTTP response and an abort before dispatch, matching the design 0.4.2 recovery table. The caller keeps its immutable candidate and key; shared never discards or replaces them.
  - Shared contains no pt-BR UI copy; screens render the messages.
- [x] **3.5** **Transparent financial headers.**
  - Caller-supplied `Idempotency-Key` and `If-Match` are sent byte-for-byte. The transport never mints, rewrites, strips or caches a key.
  - Response headers, including `ETag`, are returned to the caller.
  - Pure helpers `toIfMatch(version)` and `parseETag(header)` implement the §6.11 representation: a strong, quoted, positive decimal `xmin`.
- [x] **3.6** **No-retry invariant.** Each invocation makes exactly one network request, with no automatic retry on any status (401, 409, 5xx) or on a network error, and no automatic 401 renewal. Apps decide retries; a retried financial create reuses the caller's frozen key and payload.
- [x] **3.7** Vitest coverage for 3.2–3.6:
  - injection and per-request provider calls
  - `null` provider → no `Authorization` header
  - `ApiError` normalization of a multi-message body, a `null` list, a non-JSON body and a network failure
  - `isOutcomeUncertain` for GET versus POST, 409 versus 500, and a POST aborted **before** dispatch (`fetch` never called, not uncertain) versus **after** dispatch (abort while the fake `fetch` is pending, uncertain)
  - header pass-through and `ETag` exposure
  - exactly one `fetch` call for 401, 409, 500 and network errors

### Phase 4 — TanStack Query hooks (`./queries`) and the import boundary

- [x] **4.1** Pin `@tanstack/react-query` exactly as an `@loto/api` `devDependency`, for typechecking, and declare a v5 `peerDependency` range, so each app owns the instance and its `QueryClient`. Shared never creates a `QueryClient`.
- [x] **4.2** Add a second Orval output, `react-query` over `fetch`, into `src/generated/queries/`. It uses the same model directory and the same mutator, with **`headers: true`** and `clean: true` as in 2.2. Without `headers: true`, mutation variables lose the required header arguments that 4.3 asserts. A spike decides whether the hooks reuse the raw functions or carry internal generated fetchers, and the decision is recorded. Either way, the root graph stays hook-free and nothing is hand-edited.
- [x] **4.3** `src/queries/index.ts` is the only entry exporting hooks and query keys or options.
  - Generated mutation hooks set no `retry`. A check over the generated tree fails if any `retry` option appears.
  - The notes record that apps keep mutation retries at 0 and never retry a write automatically.
  - Header arguments stay **required** in mutation variable types (compile-time assertion for `POST /transaction`).
- [x] **4.4** **Import-boundary check.** `shared/api/scripts/check-boundary.ts` walks the transitive import graph of `src/index.ts` and fails if it reaches `@tanstack/*`, `react`, `src/queries/**` or `src/generated/queries/**`. It runs inside `@loto/api`'s `lint` script, so the standing `pnpm -r lint` gate enforces it. Prove it with a seeded violation that fails, then remove the violation.

### Phase 5 — Docs sync and close-out

- [x] **5.1** Update `shared/AGENTS.md` with:
  - the generated layout and the regeneration procedure (export → `generate` → review the diff → commit with the server commit, spec revision and document hash)
  - the client-core API and the no-retry and header rules
  - the D2/D3 outcomes
  - the new dependencies, keeping peer versus dev roles

  Notify the web and mobile owners of the configured-client contract their M2 milestones consume.
- [x] **5.2** Done criteria (all from a fresh clone):
  - `pnpm install && pnpm -r typecheck && pnpm -r lint && pnpm format:check && pnpm -r test` pass. These include the enum, fidelity and header compile-time gates and the import-boundary check.
  - `generate:check` passes from both the repository root and `shared/api`.
  - `export:check` passes on a machine with the .NET 10 SDK.
  - The committed document matches the recorded post-SV03 server commit and spec revision, and contains no `/report/dashboard`.
  - There is no hand-authored DTO and no function for an unshipped endpoint.
  - There are no environment reads under `shared/`.
  - There is no `dist/`.


> **S06 delivery note (2026-10-17; accepted 2026-10-18): Phases 1–5 implemented, validated and independently accepted.** Checkout `Project`, branch `main`, baseline `ba63552`. That commit integrates the server input `763edd7` (SV22 H3/H4) and `940bca7` (its accepted landing record), spec `v52`; `git diff 940bca7 HEAD -- server docs` is empty. Pre-existing design WIP (`design/docs/milestones.md`, `design/docs/decisions.md`, `design/references/claude/`) is untouched and outside this delivery. Nothing is staged or committed.
>
> **Still unchecked, with reasons:**
> - Every item reopened in review is closed: 1.3, 3.3 and 3.4 by the round-2 review, and 2.3, 3.2, 3.5 and 3.7 by the round-3 acceptance (below).
> - **1.6** needs an authorized commit. The document's commit must record its real hash; none is invented here.
> - **5.2** is defined "from a fresh clone", which needs that commit. Its checks have already passed in a disposable fresh-clone equivalent (below), and they are repeated once the commit exists.
>
> **Phase 1 — contract input**
> - **1.1 tripwire passed** (verified before anything was generated). The snapshot and the exported document carry `/report/manager-queue` and `ResponseManagerQueue{,Backlog,Close,NotSubmitted}Json`, and have 0 `dashboard` mentions.
> - The document itself shows:
>   - response numbers are `number`/`integer`, while request numbers stay `number|string` (for example `RequestCreateTransactionJson.value`);
>   - per-operation `security`: `[]` ×3, `IdentityBearer` ×3, `BranchBearer` ×95 (46 any role, 49 with role names), with no document-wide requirement;
>   - `Idempotency-Key` is required on `POST /transaction`, `/transaction/installment` and `/transaction/fiado-payment`; `If-Match` is required on guarded mutations; `ETag` is declared on versioned responses.
> - The spec group and both design markers are at `v52`, and `bash docs/check-loto-doc-sync.sh` is green.
> - **1.2.** The H1/H2 landing (`26fb2bb`) and the H3/H4 landing (`763edd7`) are recorded above.
> - **1.2 H2 regression:** with no `ConnectionStrings__*`, `Token__*` or `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`, `dotnet build server/server.sln --no-incremental` exits 0 and writes no OpenAPI document. User configuration was not changed.
> - **1.3** `scripts/export-openapi.ts` (Node 26 type stripping) runs on **every** export:
>   - `dotnet build … -c Release --no-incremental -p:OpenApiGenerateDocuments=true -p:OpenApiDocumentsDirectory=<fresh mkdtemp dir>`;
>   - the child gets an environment with the real `ConnectionStrings__*`, `Token__*`, `ASPNETCORE_*` and `DOTNET_ENVIRONMENT` stripped, and only inert values injected (`Host=openapi-export.invalid`, a non-secret signing key);
>   - the `server.API.json` bytes are copied unmodified, and only the owned temp directory is removed.
>   - A fake `dotnet` recorded the child environment while the parent held a "real" connection string, signing key and `Development`; the child saw only the inert values.
>   - Failure proofs (each exit 1):
>     - a build that exits 0 but writes no document (the committed file stays untouched);
>     - a failing build;
>     - a missing SDK;
>     - a drifted document;
>     - a missing committed document.
> - **1.4** Exports from `shared/api` and from the repository root are byte-identical: sha256 `6fd6f2441c38fdc76b3289afd147b2d7c04170213af368e3d7d9a00545d37ea7` (the same as the server's SV22 export). The document is OpenAPI 3.1.1, 73 paths, 101 operations, 166 schemas, with no `servers`.
>   - Parity: the document equals this checkout's WebApi.Test host Development `/openapi/v1.json` once the runtime-only `servers` (`http://localhost/`) is excluded. A disposable harness outside the repo dumped the runtime document.
> - **1.5** `export:check` re-exports to a temp file and compares bytes. It passes from both directories and needs the .NET 10 SDK (here 10.0.201); server M9 CI can adopt it.
>
> **Phase 2 — generated models and raw functions**
> - **2.1 Orval `8.39.0`**, published 2026-09-30, engines `node >=22.18.0`. It is the newest 8.x at least 14 days old on 2026-10-17: `8.40.0` (2026-10-04) and the plan's `8.43.0` (2026-10-14) do not qualify.
> - Behavior verified in the installed 8.39.0 source:
>   - it normalizes `headers: outputOptions.headers ?? false`;
>   - it formats only through `output.formatter` (the `prettier` flag is not read);
>   - the fetch client's `forceSuccessResponse` and `includeHttpResponseReturnType` type only the 2xx variants as `{ data, status, headers }`;
>   - a mutator file that exports `ErrorType` sets the hooks' `TError`.
> - Lockfile audit: only additions. No pre-existing package was upgraded or removed (`ajv@8`, `path-key@4` and `json-schema-traverse@1` sit beside the existing versions), and ESLint's lockfile key now records Orval's optional `jiti` peer. Orval's optional `zod` peer was resolved automatically to `4.6.7`.
> - **2.2** `orval.config.ts` takes the committed document as its only input:
>   - the `fetch` client, `tags-split` mode, models in `src/generated/model/` and raw functions in `src/generated/endpoints/`;
>   - the `src/client/fetch.ts#apiFetch` mutator, `headers: true`, `clean: true` and `useDates: false`;
>   - `formatter: 'prettier'` with the root `.prettierrc`.
>   - No operation has an `operationId`, so names come from method and path (for example `postTransaction`, `putTransactionTransactionId`).
> - **2.3/2.4.** The mutator builds the URL from the injected base, attaches the provider's token and keeps headers. The root entry exports models, raw functions and the client API.
> - **2.5** `generate` is deterministic: two runs give tree hash `0ba7c11047f220393164af5d8ac74a2a5864abf552c5bb3d7f1240e5c8ec8459` (sha256 of the sorted per-file sha256 list) over 227 files: 194 model, 16 endpoint, 16 query and 1 auth map. *(Superseded by review round 1: the tree and its hash changed, and the hash recipe is now stated explicitly.)*
>   - `generate:check` mirrors the inputs into a fresh temp directory, symlinks the installed tools, generates, and compares all paths and bytes; no git is involved.
>   - It passes from the repository root and from `shared/api`. **All six seeded cases fail** with a precise list: `modified` / `added` (an untracked stale file) / `deleted`, each from both directories. Each was restored, and the restore verified by tree hash.
> - **2.6–2.8** `src/contract-checks/contract-fidelity.ts` is compiled by `typecheck`:
>   - `TransactionStatus`, `DailyCloseStatus` and `Role` equal their name unions and reject numeric literals, and no generated enum object has a non-string value;
>   - required `token`/`refreshToken` and session `token`/`branch`;
>   - `errorMessages` is required `string[] | null`, and `clientId` is optional and nullable;
>   - `branchLocalDate`/`branchLocalDateTime` are required strings;
>   - response `value`/`version` are `number`, and request `value` is `number | string`;
>   - `Idempotency-Key` is required on `POST /transaction` and `/transaction/installment`, `If-Match` on `PUT /transaction/{transactionId}` and `…/finalize`, and `headers: Headers` (ETag) is on raw results.
>   - Sabotage proofs (restored afterwards): generating with `headers: false` and with `useDates: true` each fails typecheck.
>
> **Phase 3 — client core**
> - **3.1** Vitest `5.0.3` (2026-09-30, engines include `>=26.0.0`), with its required peer `vite` `8.3.2` (2026-10-01). Root `test` is `pnpm -r test`.
> - **3.2–3.6.** Request-scoped clients come from `createApiClient` and pass per call as `{ client }`; `configureApiClient` sets a default, and each call snapshots its client. The rest is as documented in `shared/AGENTS.md`:
>   - typed `ApiConfigurationError`;
>   - the token provider called per request with a generated `authTier`;
>   - `ApiError`, `isOutcomeUncertain`, `toIfMatch`/`parseETag`;
>   - exactly one `fetch` call per invocation.
>   - `src` compiles with `types: []`: seeded `process.env` and `import.meta.env` reads fail to compile.
> - **Choice recorded:** dispatch starts at the `fetch` invocation, so even a synchronous throw from `fetch` counts as dispatched (the conservative reading). An abort while the token provider is pending settles the call at once without dispatch.
> - **3.7** 55 tests pass, driving the real generated functions with an injected fake `fetch`. They cover:
>   - provider calls with method/path/tier, the `null` token and URL joining;
>   - concurrent requests with different clients, and a reconfiguration during an in-flight request;
>   - multi-message, `null`, non-JSON and empty error bodies, network failure, malformed 2xx, and a body lost after the status line;
>   - the uncertainty matrix (GET vs POST, 409/401 vs 5xx, abort before dispatch, during the provider and after dispatch, timeout);
>   - header pass-through and `ETag`, and one `fetch` call for 401/409/500/network;
>   - `toIfMatch`/`parseETag` boundaries.
>
> **Phase 4 — queries entry and boundary**
> - **4.1** `@tanstack/react-query` `5.104.1` (2026-10-02) is pinned exactly as a devDependency, with peer range `^5.104.1`. `react`/`@types/react` `19.3.0` are pinned for typechecking only. No `QueryClient` is created.
> - **4.2** A second Orval project (`react-query` over `fetch`) writes to `src/generated/queries/`, with the same models and mutator and `headers: true`, `clean: true`.
>   - Spike result: the hooks carry internal generated fetchers that call the same `apiFetch`; they do not import the raw functions.
>   - A first attempt with `useQuery: true` turned writes into query hooks. It was reverted to Orval's defaults (GET → query, others → mutation): 101 hooks, one per operation.
> - **4.3** Only `@loto/api/queries` exports hooks. `query-fidelity.ts` asserts required header variables for POST `/transaction`, installment and PUT, and that the hooks' default `TError` is `ApiError`. No generated file contains `retry`, and apps keep mutation retries at 0.
> - **4.4** `scripts/check-boundary.ts` runs inside `lint`. It walks the TypeScript-resolved transitive import graph of `src/index.ts` (218 modules, type-only imports and re-exports included) and fails on React, `@tanstack/*`, `src/queries/**` or `src/generated/queries/**`, plus any generated `retry`.
>   - Seeded proofs, all failing with the import chain and then removed:
>     - a direct type import of `@tanstack/react-query` in a client module;
>     - a re-export of `../queries` hidden behind an innocent intermediate module;
>     - `retry` in generated output.
>
> **Phase 5 — docs and verification**
> - **5.1** `shared/AGENTS.md` carries the layout, commands, pins and client contract. The web and mobile M2 plans carry in-repo handoff notes; no external message was sent.
> - **Fresh-clone-equivalent verification:** HEAD's tracked files from `git archive`, plus only this task's 261 changed/new paths (no `design/` WIP), in a disposable directory that is not a git repository. There:
>   - `pnpm install --frozen-lockfile`;
>   - `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check` and `pnpm -r test` (55/55);
>   - `generate:check` from the root and from `shared/api`, and `export:check` (`6fd6f244…`).
>
>   All exit 0. There is no `dist/` and no environment read under `shared/*/src`. Node `v26.6.0`, pnpm `10.34.6`, .NET SDK `10.0.201`.
> - **Review round 1 (2026-10-17): needs fixes — corrected here, awaiting repeat review.** The independent review reproduced every gate and found five transport/export defects that the 55 submitted tests did not exercise.
>   1. **[P2] Required financial headers could be overridden.** Orval 8.39.0 always merges `options.headers` after the generated header arguments (raw and query outputs). Its only switch, `requestOptions: false`, would also remove `signal` and the per-call `client`. Fixed at the wrapper boundary, with no edit to generated code:
>      - the caller-facing options type is `ApiRequestOptions` (`headers?: never`), exposed as the mutator's last overload, so `options.headers` is a compile-time error in both outputs (`contract-fidelity.ts`, `query-fidelity.ts`);
>      - codegen now emits each operation's declared header parameters in `src/generated/operations/operations.ts` (which replaces `auth/operation-auth-tiers.ts`);
>      - before dispatch, the mutator refuses (`ApiError` kind `invalid-request`) any header the operation does not declare (besides `Content-Type` with a body) and any name repeated in another casing, so `If-Match: "7", "99"` can no longer be produced.
>      - **Residual:** a caller that bypasses the types and repeats a declared header in exactly the same casing still replaces the value. Orval erases that before the mutator sees it, and only editing generated output could change it.
>   2. **[P2] A `null` token did not guarantee no `Authorization`.** The provider is now the only source: any caller-supplied `Authorization` (record, `Headers` or tuple form) is refused before the provider runs.
>   3. **[P2] The export's environment filter was case-sensitive.** `isServerConfiguration` now matches case-insensitively, treats `:` like `__`, and covers `DOTNET_`/`ASPNETCORE_`-prefixed keys and the `SQLCONNSTR_`/`SQLAZURECONNSTR_`/`MYSQLCONNSTR_`/`POSTGRESQLCONNSTR_`/`CUSTOMCONNSTR_` prefixes. The canonical inert values are injected afterwards. A fake SDK given mixed-case "real" markers received only the inert values.
>   4. **[P2] An abort between the provider resolving and `fetch` was reported as dispatched.** Cancellation is now rechecked after the provider settles; everything from that check to the `fetch` invocation is synchronous.
>   5. **[P3] Provider failures escaped untyped.** A synchronous throw and an asynchronous rejection both become `ApiError` kind `token-provider` (`cause` kept, `dispatched: false`, never uncertain). Hooks' `TError` (`ApiError`) is now true for them.
>   - **Regressions:** 30 new tests (85 in total):
>     - case collisions through record, `Headers` and tuple options, in the raw and query outputs;
>     - undeclared headers and `Authorization`;
>     - exact header bytes;
>     - the provider-resolution/abort window;
>     - sync and async provider failures;
>     - 16 server-configuration aliases plus 6 preserved names, and the exact child environment.
>   - **Sabotage proof (sources restored):**
>     - removing header validation fails 3 tests;
>     - removing the final abort check fails 1;
>     - removing provider normalization fails the rejecting-provider case (the sync path has its own guard);
>     - a case-sensitive filter fails 12.
>   - **Refreshed evidence:**
>     - Tree hash, recipe: sha256 over the UTF-8 lines `<file-sha256>  <relative-path>\n` for every file under `src/generated/`, sorted by relative path. Result `c9311da46da88554b4fca9bb0d6d2f2ce403e4fe506be9986ccba109d3ed0433` over 227 files (194 model, 16 endpoint, 16 query, 1 operation map), identical after regeneration.
>     - The six `generate:check` negatives fail again from both directories.
>     - `export:check` passes at `6fd6f244…7ea7` (the input bytes are unchanged).
>     - Frozen install, `pnpm -r typecheck/lint/test` (85/85) and `pnpm format:check` pass. The boundary check walks 218 modules.
> - **Review round 2 (2026-10-18): needs fixes — corrected here, awaiting repeat review.** The reviewer accepted the null-token, export-environment, abort-window and provider-error fixes and closed **1.3, 3.3 and 3.4**. Two header findings remained, and **3.2** was reopened.
>   1. **[P2] Exact-case options headers still replaced financial arguments.** Round 1's type-only restriction could not stop JavaScript or type-asserted callers, because Orval's merge erased the generated value before the mutator ran.
>      - The fix moves the boundary into generation: `scripts/header-boundary.ts` wraps Orval's own `fetch` and `react-query` builders through the documented function-valued `client` option, passing the built-in client name back so the query module still emits React hooks.
>      - It rewrites only the header merge: `headers: { …generated, ...getHeaders(options?.headers) }` becomes `headers: { …generated }, callerHeaders: options?.headers`. That covers all 88 sites (70 body-only, 16 body + declared headers, 2 declared headers only).
>      - Diffing the regenerated tree against the round-1 tree shows exactly those 88 rewrites and nothing else. Any unrecognized merge fails generation.
>      - With both sources intact, the mutator refuses (`invalid-request`, before dispatch, never uncertain) a caller header that names a generated or declared header (`Idempotency-Key`, `If-Match`, `Content-Type`) with a different value in **any** casing. An equal value is allowed.
>      - Round 1's residual is gone. Without the boundary the mutator refuses every financial request (fail-safe; proved by sabotage) rather than allowing overrides.
>   2. **[P2] Ordinary headers were refused.** `ApiRequestOptions` is `RequestInit` without `method`/`body` again, so `headers` is allowed, and ordinary caller headers pass through unchanged in record, `Headers` and tuple forms, in both outputs. A caller `Authorization` and case-duplicated names are still refused.
>   - **Supersedes in the round-1 note:** "options cannot carry headers", the undeclared-header refusal and the residual.
>   - **Regressions:** 91 tests in total:
>     - exact- and other-case conflicts for `Idempotency-Key`, `If-Match` and `Content-Type` in the raw and query outputs;
>     - ordinary pass-through (record, `Headers`, tuple, raw, query and GET) with the exact header list sent;
>     - equal-value non-conflict;
>     - caller `Authorization` and case duplicates;
>     - `header-boundary.test.ts`: the three rewrite shapes, a loud failure on an unknown shape, and the committed tree has 0 lossy merges and 88 separated ones.
>   - The compile-time gates now assert that ordinary `headers` compile and a `body` does not, in both outputs.
>   - **Sabotage (restored):**
>     - plain Orval clients make every financial request refuse (fail-safe, 8+ failures);
>     - ignoring conflicts in the mutator fails 2 tests;
>     - dropping ordinary headers fails 1.
>   - The same sabotage exposed a real gap, now fixed: `generate:check`'s mirror did not copy `scripts/header-boundary.ts`.
>   - **Refreshed evidence:**
>     - tree hash (same recipe) `d2f91f08604268fd4d67539fce3eadd88611ec307f375312cd0ed7138da20fe7` over 227 files, identical after regeneration;
>     - the six `generate:check` negatives fail from both directories;
>     - `export:check` passes at `6fd6f244…7ea7`;
>     - frozen install, `pnpm -r typecheck/lint/test` (91/91) and `pnpm format:check` pass, and the boundary walk covers 218 modules.
> - **Review round 3 (2026-10-18): accepted.** The independent repeat review accepted the round-2 header fixes: the generation-time header boundary, ordinary caller-header pass-through, and refusal of conflicting financial headers and caller `Authorization`. **2.3, 3.2, 3.5 and 3.7 are closed**, so every implementation item 1.1–5.1 is checked.
>   - **Remaining:** 1.6 needs the authorized source commit, recorded with its real hash in a follow-up (never in the same commit). 5.2 needs the done criteria repeated from an actual fresh clone of that commit.
>   - **Accepted evidence:**
>     - input sha256 `6fd6f2441c38fdc76b3289afd147b2d7c04170213af368e3d7d9a00545d37ea7` (server `763edd7`, spec `v52`);
>     - generated tree `d2f91f08604268fd4d67539fce3eadd88611ec307f375312cd0ed7138da20fe7` (227 files);
>     - 91/91 tests, recursive typecheck/lint/format, `generate:check` from both directories, and `export:check`.
> - **Not claimed:** behavior under Hermes/Metro or Next is untested here (Web/Mobile M1/M2 prove it). `Scalar.AspNetCore` floats (`Version="*"`) on the server, a restore-reproducibility risk the server owner already knows about.


> **S06 actual-commit close-out (2026-10-18; SV10/SV11 Stage 0).** Source/landing commit `d095262488523d949032011ec67e6638013acf5f` includes the accepted round-3 implementation and the older design candidate (the latter is committed, with no implied visual approval). A disposable **local Git clone of that exact commit**, `/private/tmp/loto-s06-d095262-acceptance`, was checked out detached; no remote fetch/push, user branch change or copy of uncommitted source participated. `pnpm install --frozen-lockfile`, `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check`, `pnpm -r test` (**91/91**), root `pnpm --filter @loto/api generate:check`, package `pnpm --dir shared/api generate:check`, and `export:check` from both locations all exited 0. Export uses the accepted inert configuration and fresh build recipe. Both generations match all 227 committed files; both exports match input sha256 `6fd6f2441c38fdc76b3289afd147b2d7c04170213af368e3d7d9a00545d37ea7` (server `763edd7`, spec v52). The exact documented hash recipe (sort the **relative path strings** ordinally, then hash UTF-8 `<sha256>  <relative-path>\n` lines relative to `src/generated/`) reproduces `d2f91f08604268fd4d67539fce3eadd88611ec307f375312cd0ed7138da20fe7`. Clone `git status --porcelain` is empty after checks; no dashboard endpoint, runtime environment reads or dist output exists. Thus 1.6 and 5.2 close on the real previously authorized commit, not a self-referential hash for this documentary diff. Historical pre-commit/review notes above remain historical evidence.

> **M8 candidate regeneration (2026-10-18; SV10 + SV11 Phases 1–5).** Actual source baseline remains `d095262488523d949032011ec67e6638013acf5f`, and the later Phases 1–5 candidate, which landed in `9ea777cdb5376f5f6396de65bfba39db41ae91ee` with canonical spec v53. That commit is separate from the S06 landing and does not establish independent M8 acceptance. The accepted opt-in export and generator produced input SHA-256 `fcfe94155a72246a11c0a103c760524f9f80dd1164f61c75351bef4455de15b9` (87 paths, 118 operations, 186 schemas), and generated tree SHA-256 `a401811614fe34dbb68ff9da383c929e2fd531b46985aa587f9d8c844c43b4e8` over 255 files using the same ordinal relative-path-string recipe above. No generated DTO/client was edited by hand. Required `UserId`, profile/recovery/request/invitation DTOs, nullable own-request wrapper and optional reissue-body fidelity compile against both outputs. The S06 wrapper separates all 110 generated header merges; token isolation, no retry, typed error, abort and import-boundary tests remain intact. Export configuration scrubbing now also removes AccountSecurity/SecurityMail aliases and injects disabled mail/trusted inert defaults. Frozen install, recursive typecheck/lint/format/test (**101/101**), root and `shared/api` `generate:check` and `export:check` all exited 0 on the final candidate; both fresh exports match the exact input hash, both generations match all 255 files. Root boundary traversal covers 243 hook-free modules. Default Release build with server configuration removed exports zero documents. Server acceptance and essential migration/SMTP/setup guidance are in [Server M8](../../server/docs/milestones.md#m8-accepted-delivery-and-essential-operations). At this historical Phases 1–5 candidate point, canonical v53/design markers v52 intentionally kept the actual doc-sync gate open; the later combined M8 review and v54 close-out are recorded below. No Web/Mobile UI or Shared M3 implementation is implied.

> **M8 Phase 6–7 candidate regeneration (2026-10-18; SV11).** Source baseline `9ea777cdb5376f5f6396de65bfba39db41ae91ee` contains the preceding M8 input and an unrelated M9.5 evidence edit; the later `a326b4d` design M1 commit does not alter this generation source. This candidate and its repeat-review corrections landed in `b96c5d3`. The accepted opt-in export produced input SHA-256 `e903b801be1db602d6e02ec505c4ae2a48888d869f58af7c3af50b26f5e11d48` (**90 paths, 120 operations, 186 schemas**); clean generation produced **257 files**, ordinal relative-path tree SHA-256 `115efe12e51f58f0ae3decd7a4d9b9059386130d076a35320dee07f637dadad6`. Public register, unrestricted branch create and direct member-add operations disappear; platform onboarding/read/reissue, Google challenge/sign-in and the read-only profile capability fields flow from server OpenAPI, never hand-edited clients. The compile-time profile fidelity assertion and exact header-boundary assertion now match the new surface: **108 separated merges**, zero lossy merges. Export child configuration also strips all `GoogleIdentity__` aliases and injects only an inert client id. Frozen install, recursive typecheck/lint/format/test (**103/103**), root/package generation and export checks pass; boundary traversal finds **244** hook-free root modules. A default inert Release build emits zero documents. [Server M8](../../server/docs/milestones.md#m8-accepted-delivery-and-essential-operations) records the accepted backend scope and essential security/migration/setup guidance; neither live Google acceptance nor a new S06 landing is claimed. The 2026-10-18 independent repeat review subsequently accepted both M8 batches technically and semantically. Both design markers now match canonical v54; the actual doc-sync checker exits 0 and its 34 regression scenarios pass. Server items 1.4/5.5 close on that evidence, while 7.2/7.2.2 and Phase 8 provider/launch gates remain open.

> **Server M11 Phases 4–7 candidate regeneration (2026-10-20; uncommitted working tree on `main` over `c5dec58`, canonical v56; independent review pending).** The accepted opt-in export produced input SHA-256 `b2f7dcf089f6007a9f964ffdf399723fe3111161d695de77e14d035b177123c2` (**93 paths, 123 operations, 214 schemas**; previously `e903b801…`, 90/120/186). Clean generation produced **287 files**, ordinal relative-path tree SHA-256 `ce104dd469ec88c9b5b4e0e0ca9ce45bc0598a3d15862f36231e55aeaf0234e9`. New operations: `GET /report/upcoming-settlements`, `GET /report/monthly-financial/{year}/{month}` and `GET /report/management-overview` (all BranchBearer Admin/Manager); new enums `SettlementInstrument`, `MonthlyFinancialRole` and `OverviewSectionStatus`; TransactionType requests gain optional `settlementInstrument`/`monthlyFinancialRole` and its response requires both; `ResponseOpenChequeAgingGroupJson.originTransactionId` becomes optional/nullable (a standalone cheque has no plan) — a deliberate typed change consumers must handle. No generated file was edited by hand and no `dashboard` path or schema exists. `generate`, `generate:check`, `export:check`, `typecheck`, `lint` (boundary: generated code sets no retry, hook-free root), `test` (**103/103**) and `format:check` all exited 0. Recording only: this does not land or accept the server contracts.

> **Server M8 Phase 8 local subset candidate regeneration (2026-10-21; uncommitted working tree on `main` over `f424deb`, which contains the M11 input above as `18012d5`; canonical v57; independent review pending).** The opt-in export produced input SHA-256 `8f420211324373556b330be8bd03beb4e787df2b8d0e4e928cf4ed9653d2abbd` (**93 paths, 123 operations, 214 schemas**, unchanged counts; previously `b2f7dcf0…`). The document diff is additive only: `POST /user/login` declares 429 `ResponseErrorJson`, and every 429 response (login, Google challenge/sign-in, reset-password, invitation preview/decline/register) declares a required integer `Retry-After` header (minimum 1). Clean generation produced **287 files**, tree SHA-256 `5e9f08bdb49d5ddd7e83f9adb942700d8873192d74191e5cfd4c13ed7c7fb536` (same recipe; it reproduces `ce104dd4…` on the committed tree). Only `endpoints/user/user.ts` and `queries/user/user.ts` changed: `postUserLoginResponseError` gains the 429 variants. Response headers generate no types; `Retry-After` stays a header consumers read from `ApiError.headers`, and nothing retries automatically. `generate`, `generate:check`, `export:check`, `typecheck`, `lint`, `test` (**103/103**) and `format:check` exited 0; no Node manifest or lockfile changed. Recording only: the landing commit is recorded after authorized landing.

---

## Milestone 3 — Core Helpers

**Goal:** Fill `@loto/core` with the day-one pure-TS helpers both apps need immediately: BRL format/parse, CPF/CNPJ format + validate, date/time formatters (`dd/MM/yyyy`, `HH:mm`, datetime), signed-variance display, and pt-BR label maps for every contract enum (`TransactionStatus`, `DailyCloseStatus`, `Direction`, `AgingBucket`, `TimeEntryStatus`, `Role`, `AccountType`, …).

**Scope boundary / key behaviors (detailed phases written when this milestone starts):**

- **Money precision strategy (locked 2026-06-10):** the wire format stays JSON `number` — the backend contract is untouched, and C# `decimal` / `numeric(14,2)` values round-trip exactly at 2 decimal places. Client code treats money as **display-only by default**: the backend computes every financial outcome (the preview endpoints exist for exactly this), so clients never recompute balances, variances, or due amounts. Where client-side arithmetic is unavoidable (input masking, optimistic column sums), it goes through integer-cents helpers in this package: `parseBrl` returns integer **cents**, `formatBrl` accepts cents or a wire `number`, and no client code performs float arithmetic on money.
- Implements `design/specs/formatting-ptbr.md` (Design M0, Phase 2) — formatters cite the spec; the spec is the contract, the helpers are the implementation.
- Enum label maps key off the **generated** enum types from `@loto/api`, so a backend enum change breaks the build here instead of silently desyncing labels.
- Vitest already exists from Shared M2 (D1); every formatter and validator gets unit tests on it.
- Pure TS only — no Intl-locale surprises across platforms without a pinned strategy (decide `Intl` vs hand-rolled per formatter in-phase, using the Hermes `Intl` behavior measured in Mobile M1 Phase 4, and record the decision).

**Precondition:** Milestone 2 (generated enum types) and Design M0 Phase 2 (the formatting spec).

### Plan, gate and boundaries (written before code, 2026-10-26)

Baseline: `Project-app` on `paralell/app` at `e746caa`, clean; approved flow-alignment plan read read-only from `Project/main` at `a34438a` (server M9.6 Phase 8 = plan A9, the restricted arithmetic parser, which lives here). Inputs read in full: `design/specs/formatting-ptbr.md`, `design/specs/copy-guidelines.md` (§5: 25 enum maps), the 25 generated enum types, the MoneyInput/Input/DatePicker/Badge/SaveStatus/Pagination specs, Web M3 (phase map, 7.4, round-1 fixes) and every `PresentationFormat` consumer, and Mobile M1's actual status.

- **Native gate (unchanged).** Mobile M1 4.1/4.2 has **no measured Hermes output** on either platform. This milestone therefore separates three states per helper: **implemented** (code + Node tests), **web-verified candidate** (also exercised in Next SSR and Chrome through the Web adapter), and **native strategy accepted** (needs the Hermes readings and an independent decision). Nothing here reaches the third state. Helpers that use no engine locale facility are hand-rolled and deterministic; their native *execution* is still unproven. The only engine-dependent formatter is the UTC-instant conversion (IANA zone data via `Intl.DateTimeFormat` with an explicit `timeZone`), recorded as a **web-verified candidate, provisional for native**.
- **No financial outcome on the client.** Helpers format, parse input and validate identifiers. They never sum or net balances, compute variance, FIFO, fees or credit, and never round an API figure: an API amount with more than two decimals is a contract violation and throws instead of being rounded.
- **Money inputs are explicit.** Integer cents are the branded `Cents` type; a JSON wire number is passed only to the `…Wire` functions. An untagged number never decides between the two. The range is `numeric(14,2)`: |cents| ≤ 99 999 999 999 999 (12 integer digits), exact in a JS safe integer. *(Refines the 2026-06-10 line "`formatBrl` accepts cents or a wire `number`": it does, through two named entries, `formatBrl(cents)` and `formatBrlWire(value)`.)*
- **Purity boundary.** `src` compiles with `lib: ["es2022"]` and `types: []` (no DOM, no Node, no `process`), imports only relative modules plus `import type` from `@loto/api`'s root entry, reads no clock, environment, storage, device locale or time zone, and keeps no mutable locale state. A boundary test enforces this on the source text.
- **Out of scope.** Web M4 screens, any cashier/handover/reconciliation page, future `Session`/`Safe` enum values (labels follow the **current** generated enums only) (progressive identifier punctuation, first listed here, was implemented in review round 1 — see 4.3), and the universal money-field UX (Design 4.9.7).

### Phase 1 — Contract inventory and API map

- [x] **1.1** Every formatting/copy row mapped to one public helper with its input/output units, invalid/empty handling, test and native-evidence state (table below).
- [x] **1.2** Public entry: `@loto/core` (`.` only). No deep import, no build layer; `@loto/api` is a type-only workspace dependency.

| Contract row | Helper (input → output) | Invalid / empty | Tests | Native state |
|---|---|---|---|---|
| §2 money display | `formatBrl(Cents)`, `formatBrlWire(number)`, `formatBrlAmount` → `R$ 1.234,56` | negative, >2 decimals, non-finite, out of range → `RangeError` | `brl.test.ts` (code points, 12-digit max, wire round-trip ×2000) | hand-rolled; Hermes not executed |
| §2 input / paste | `parseBrl(text)` → cents; entry digits (`normalizeMoneyDigits` …) | `empty` / `malformed` / `signed` / `out-of-range`; 15th digit → null | `brl.test.ts` (38 cases, round-trip ×2000) | hand-rolled; Hermes not executed |
| §3 signed variance/deltas | `formatSignedBrl(Cents)`, `…Wire` → `+R$`/`−R$`/`R$ 0,00` | `-0` → unsigned zero | `brl.test.ts` | hand-rolled; Hermes not executed |
| §3 fiado balance | `presentFiadoBalance(Cents)`, `…Wire` → `{ kind, signedCents, amount, label, text }` | as money | `spoken-fiado.test.ts` | hand-rolled; Hermes not executed |
| MoneyInput spoken value | `spellBrl(Cents)`, `spellSignedBrl` | negative → `RangeError` (unsigned) | 32 hand-written sentences up to the maximum | hand-rolled; Hermes not executed |
| A9 arithmetic input | `evaluateMoneyExpression(text, options)` → typed outcome, cents | 9 error codes, `incomplete`, `empty` | `money-expression.test.ts` (fixed + integer oracle ×1500 + adversarial) | BigInt; Hermes not executed |
| §4 date-only | `CalendarDate`, `parseCalendarDate`, `formatDate`, `parseDateInput`, long/weekday/month names, `addDays`/`addMonths` | null (parse) / `RangeError` (display) | `dates.test.ts` (UTC-calendar oracle ×3000, leap/month/year edges) | hand-rolled; Hermes not executed |
| §4 wall clock | `formatWallTime`, `formatWallDateTime` | offset/`Z`/impossible → `RangeError` | `dates.test.ts` | hand-rolled; Hermes not executed |
| §4 UTC instant | `formatInstant*(value, timeZone)`, `parseInstant` | offset-free/impossible → null/`RangeError`; unknown zone → `RangeError` | 15 zone cases incl. DST start/end, +05:30, +12:45; both calendar ends | **`Intl` with explicit zone: web-verified candidate (Node, Chrome), provisional for native** |
| §5 durations | `formatDuration(h)`, `formatSignedDuration(h)` | negative unsigned / non-finite → `RangeError` | half-minute edges, integer oracle ×2000 | hand-rolled; Hermes not executed |
| §6 masks/validation | `formatIdentifier(kind, v)`, `maskIdentifierInput(kind, text)`, `identifierDigits`, `isValidCpf`/`isValidCnpj` | verbatim passthrough; repeated digits false | `numbers-identifiers.test.ts` | hand-rolled; Hermes not executed |
| §7–§8 numbers/absent | `formatCount`, `formatDecimal`, `formatPercent`, `formatInstallment`, `EM_DASH`, `formatOptional` | non-integer count / bad position → `RangeError` | `numbers-identifiers.test.ts` | hand-rolled; Hermes not executed |
| copy §5 (25 maps) | `…Labels`, `enumLabels`, `enumLabel` | unknown → `{ known: false, label: raw }` | `labels.test.ts` (generated keys, copy text) | data only |

### Phase 2 — BRL, spoken money and fiado presentation

- [x] **2.1** `Cents` brand and wire boundary: `asCents`, `centsFromWire` (exact, rejects non-finite, >2 decimals, out of range, `-0` → 0), `wireFromCents` (the JSON number whose shortest text has ≤2 decimals).
- [x] **2.2** Displays (formatting §2–§3): `formatBrl`/`formatBrlWire` (unsigned; a negative value throws rather than hiding its sign), `formatBrlAmount` (no unit, for MoneyInput), `formatSignedBrl`/`formatSignedBrlWire` (`+R$`, `−R$` U+2212, unsigned zero); U+00A0 after `R$`.
- [x] **2.3** Input: `parseBrl` (paste: optional `R$`, surrounding spaces, strict dot grouping, 1–2 decimals; signs, other symbols and >12 integer digits rejected by category) and the cents-digit model (`normalizeMoneyDigits`, `centsFromMoneyDigits`, `moneyDigitsFromCents`, `formatMoneyDigits`, 14-digit cap).
- [x] **2.4** `presentFiadoBalance`/`…Wire`: `> 0` debt unsigned · **em aberto**; `0` **quitado**; `< 0` absolute value with explicit `+` · **crédito do cliente**; the signed API value is preserved and nothing is summed.
- [x] **2.5** Spoken money `spellBrl` (and `spellSignedBrl`): pt-BR cardinal words, *real/reais*, *centavo(s)*, *de reais* after exact millions/billions, up to 999 999 999 999,99; hand-rolled (one contract for both platforms).

### Phase 2b — Restricted arithmetic money input (server M9.6 Phase 8 / plan A9)

- [x] **2b.1** `evaluateMoneyExpression(text, { allowNegative })`: tokenizer + recursive descent, nothing executed. Grammar, aliases, whitespace, precedence, unary signs, exact rational arithmetic, final half-away-from-zero rounding to cents and limits documented in `shared/AGENTS.md`.
- [x] **2b.2** Typed results: `empty`, `incomplete` (in-progress text), `invalid` with an error code and position, `ok` with `cents`, `rounded` and `operators`; the app owns the pt-BR messages and sends only the resulting number.
- [x] **2b.3** Tests with independent expected values: sums, grouping/decimal commas, precedence/parentheses, unary signs, division and rounding edges, maximum magnitude, malicious and overlong input.

### Phase 3 — Calendar, wall clock, instants, durations and numbers

- [x] **3.1** `CalendarDate` brand (`YYYY-MM-DD`, years 0001–9999, real calendar validation), parts, leap years, weekday, `addDays`/`addMonths` (clamped), compare — integer arithmetic, no `Date`.
- [x] **3.2** Displays: `formatDate`, `parseDateInput` (`dd/MM/yyyy`), long date, weekday long/short, month name, `formatMonthYear`, `formatMonthField`, `formatDateWithWeekday` (calendar cell name).
- [x] **3.3** Wall clock (rendered exactly as returned, offsets rejected): `formatWallTime`, `formatWallDateTime`.
- [x] **3.4** UTC instants: `formatInstant`/`formatInstantDate`/`formatInstantTime(value, timeZone)` — the caller supplies the device zone (formatting §4); an offset-free value is rejected (it is a wall clock), and so is a conversion whose local date leaves 0001–9999 AD (review round 1).
- [x] **3.5** Durations `formatDuration`/`formatSignedDuration` from the decimal hours' exact decimal text: minutes = round(|h| × 60) half away from zero, sign reapplied.
- [x] **3.6** `formatCount`, `formatDecimal`, `formatPercent`, `formatInstallment`, `EM_DASH`/`formatOptional` (absent ≠ zero).

### Phase 4 — Identifiers and enum labels

- [x] **4.1** `formatIdentifier`/`formatCpf`/`formatCnpj`/`formatPhone`/`formatCep` with verbatim passthrough, `identifierDigits` (strips mask punctuation only).
- [x] **4.2** `isValidCpf`/`isValidCnpj` (check digits; repeated-digit sequences rejected). Numeric CNPJ only — see the open note.
- [x] **4.3** Progressive input mask `maskIdentifierInput(kind, text)` (formatting §6 "input mask auto-punctuates"): punctuation added as digits arrive, phone landline shape up to 10 digits and mobile at 11, a complete value equal to the display mask, unsupported or overlong text verbatim. *(Planned as open; implemented in review round 1.)*
- [x] **4.4** 25 exhaustive maps keyed by the generated enums (`Record<Enum, string>`; a new value fails typecheck and the coverage test) plus `enumLabel` (unknown → raw value, `known: false`) and the **Automática** marker.

### Phase 5 — Tests, boundary and docs

- [x] **5.1** Vitest (same pinned versions as `@loto/api`) with fixed contract examples, boundaries, seeded property checks with independent oracles, and code-point assertions.
- [x] **5.2** Boundary test (imports, globals, `Intl` use limited to the instant module, no clock/locale reads) and a public-export snapshot.
- [x] **5.3** `shared/AGENTS.md` and this milestone record the API, the strategy table and the native evidence state.

### Phase 6 — Web verification (Web M3 7.4, owned and recorded by Web)

- [x] **6.1** Web adapter over core, real gallery data outside the old fixtures, Node/SSR/Chrome runs — evidence in Web M3. Hermes execution remains open (Mobile M1 4.1/4.2).

### M3 implementation evidence (2026-10-26; implementer-checked, awaiting independent review)

Ticks mean **implemented and checked by the implementer**. Nothing here is independently accepted, and no native strategy is accepted: Shared M3's native acceptance stays open until Mobile M1 4.1/4.2 measure Hermes and an owner decides.

- **Baseline/final.** Start: `paralell/app` at `e746caa`, clean. End: same HEAD with this batch uncommitted. Plan reference `Project/main` `a34438a` read only; no other checkout edited.
- **Server M9.6 Phase 8 (plan A9) parser** is implemented here as `evaluateMoneyExpression` (contract in `shared/AGENTS.md`); the server owner records its own criteria 8.1/8.2 after independent acceptance and integration. The "design pattern recorded for Batch B" part of 8.1 is Design 4.9.7's, not this milestone's; the Web gallery carries only a labelled demonstration.
- **Package changes.** `@loto/core` gains `dependencies: { "@loto/api": "workspace:*" }` (type-only use) and devDependencies `vitest 5.0.3`, `vite 8.3.2`, `@types/node 26.6.4` (the versions `@loto/api` already pins; no new resolution). The lockfile adds that importer; pnpm 10.34.6 also re-resolved two optional-peer annotations on reinstall (`next`/`styled-jsx` lose the optional `@babel/core` peer key, `@react-native/metro-config` lists `bufferutil`/`utf-8-validate` as transitive peers) — no package version changed, a second install is a no-op and `pnpm install --frozen-lockfile` passes.
- **Checks (2026-10-26, Node v26.6.0, pnpm 10.34.6).** `pnpm install --frozen-lockfile`; `pnpm -r typecheck`; `pnpm -r lint` (one new **warning**, see open items); `pnpm format:check`; `pnpm -r test`: Shared api 103, **core 265**, Web 9 + 232 Vitest, Mobile 7 — all passed. `@loto/api` `generate:check` (287 files) and `export:check` (sha256 `8f420211…2abbd`, 93 paths, 123 operations, 214 schemas) passed: no regeneration, no contract change. Doc sync green at v57 (34/34 regressions), design token/contrast (1,164 pairs, 40 negatives) and design-doc checks (25 enums labelled, 14 negatives) passed read-only.
- **Runtime evidence by environment.** Node (Vitest): every helper. Next SSR + Chrome: through the Web adapter and gallery (Web M3 7.4 evidence) — identical server/browser text with U+00A0/U+2212, no hydration warning, instants in an explicit zone server-side and in the device zone (São Paulo, Kolkata, New York) after hydration. **Metro + Hermes compiler:** `expo export --platform all` into a scratch directory compiled all 13 core modules into the iOS and Android `.hbc` bundles (source maps list them) and Mobile's bundle check passed — a **compile** proof only. **Hermes execution: not run.** Host recheck (read-only): Xcode is selected with an iOS 17.2 simulator runtime but no booted device; no `adb`/emulator on PATH; only `hermesc` (compiler) is installed, no Hermes VM. Nothing was installed or reconfigured.
- **Engineering choices recorded.** Unsigned helpers throw on a negative instead of dropping its sign; wire values with >2 decimals throw rather than round; `catorze` (not *quatorze*); long dates keep the contract's `1 de outubro` (no ordinal); durations are not dot-grouped (`1000h 00min`); paste accepts no sign; expression literals share the paste grammar (≤ 2 decimals, dot never decimal).

**Independent review round 1 (2026-10-27) — six findings, all accepted and corrected; awaiting the same reviewer's recheck.** The reviewer reproduced the gates above independently and checked 500 further expressions against an exact-fraction oracle. Core-owned findings (the four Web-owned ones are in Web M3 7.4):

| Finding | Correction | Regression |
|---|---|---|
| P3-5 — progressive CPF punctuation unimplemented (formatting §6) | `maskIdentifierInput` (4.3) for CPF, CNPJ, CEP and phone; display formatters keep exact-count passthrough. Web's IdentifierInput uses it with caret preservation | 24 typed-prefix cases, verbatim/overlong cases, seeded idempotence and "complete = display mask" (×400 per kind) |
| P3-6 — a converted instant lost its era (`0001-01-01T00:00:00Z` in `Etc/GMT+1` printed `31/12/0001 23:00`, really 1 BC) | `Intl` parts now include the era; a local date outside 0001–9999 AD throws `RangeError` | both ends: 1 BC and year 10000 rejected, 01/01/0001 and 31/12/9999 accepted in UTC and an adjacent zone |

Core after the round: 292 tests (was 265).

**Open items (not implemented, explicit):**
- Alphanumeric CNPJ: renders verbatim and fails `isValidCnpj`; formatting §6 defines numeric CNPJ only — a product/design decision if the shop's clients present the new format.
- Native: Hermes execution of every helper, the `Intl` time-zone path and BigInt; the final per-formatter native strategy decision (input: Mobile M1 4.1/4.2).
- Mobile follow-ups for its owner (mobile is read-only in this batch): `mobile/src/app/index.tsx` keeps an `eslint-disable import/namespace` comment that is now unused (lint warning, not an error) and `tooling/check-bundles.mjs` still prints "Core is an empty runtime namespace" although the bundles now contain core.
