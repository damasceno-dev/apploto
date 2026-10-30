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
  - **Generation must be explicitly disabled for ordinary builds** (2026-10-15). If `OpenApiGenerateDocuments` is left unset, the package's `Microsoft.Extensions.ApiDescription.Server.targets` defaults it to `true`, and `OpenApiGenerateDocumentsOnBuild` follows it. A plain `dotnet build` with no config then fails with `Missing required configuration: ConnectionStrings:DefaultConnection`; I reproduced this in the scratch copy.
    - With `<OpenApiGenerateDocuments>false</OpenApiGenerateDocuments>` in the project, a no-config `dotnet build --no-incremental` succeeds and writes no document.
    - An export build with `-p:OpenApiGenerateDocuments=true -p:OpenApiDocumentsDirectory=<temp>` plus the inert environment variables still writes the same sha256 `2cec6f9b…` document.
    - A following ordinary build succeeds again.
- **Decision — build-time export is chosen.** The Development-only `MapOpenApi()` endpoint is not an export service: it needs a running server with a database and exists only for local exploration. A `WebApplicationFactory` harness was the fallback, and is not needed because build-time generation works without Docker or PostgreSQL.
- **Contract issues that need a decision before generation** (recorded here so they are not decided silently):
  - **F1 — numeric widening.** .NET's web JSON defaults (`AllowReadingFromString`) emit every number as `["number","string"]` or `["integer","string"]`, response members included. Examples: `ResponseTransactionJson.value` and `.version`, and `ResponseDashboardBacklogJson.*Count`. The contract snapshot pins this (`number|string`, 146 lines). Generated TypeScript would type money and `Version` as `number | string`, while responses actually serialize numbers. Shared must not patch the document. See decision **D2**.
  - **F2 — no auth-tier metadata.** The document has one global `Bearer` requirement and no per-operation `security`. The code has three tiers — `[AllowAnonymous]` (login/register/renew-token), `[TokenAuthenticate]` (identity token: branch create, my-branches, session) and `[TokenAuthenticateBranch]` / `[TokenAuthorize]` (branch token) — but a generated client cannot tell which token an operation needs. See decision **D3**.
- **Design input.** The design 0.4.2 cross-screen patterns (`design/docs/screens.md` on `main` `e4544a8`, not yet merged into `parallel/shared`) set the transport rules below:
  - The caller freezes the `Idempotency-Key` and payload before dispatch.
  - Any 5xx, timeout, lost response, connection loss or **cancellation after dispatch** on a write means **Resultado não confirmado**, with the immutable candidate and key kept. Cancelling the wait cannot prove the server rolled back.
  - Never mint a new key on retry, and never blindly replay unguarded writes.
  - A 401 recovery cycle is bounded, never recursive on `/user/login` or `/user/renew-token`, and owned by the app.

### Decisions before Phase 1 starts

- **D1 — Vitest moves from M3 to M2.** The client core is the first logic in `shared/`, and its no-retry, header and error guarantees need tests. This amends the "Vitest arrives with Milestone 3" standing rule. M3 then adds formatter tests to an existing runner.
- **D2 — numeric fidelity (F1).** *Recommended:* a narrow server handoff (**H3**). A direction-aware schema transformer emits `number` / `integer` (dropping the `string` alternative) on **response** members, while request members keep advertising what binding accepts. Spec §6.15 and the doc-sync group would be bumped and the snapshot updated deliberately. *Alternative:* accept `number | string` in generated types, which leaves every consumer narrowing money and `Version`. Not recommended. Phase 2's numeric gate follows whichever option is chosen.
- **D3 — auth tier per operation (F2).** *Recommended:* a narrow server handoff (**H4**). An operation transformer derives per-operation `security` from the existing filters: `[]` for anonymous, `IdentityBearer`, and `BranchBearer`. Shared then generates an operation→tier map from the document and passes the tier to the app's token provider. *Fallback:* the provider receives `{ method, path }` and each app maps paths to tokens. That duplicates a contract fact by hand in two apps, so it is accepted only if the server declines H4, and is recorded as such.
- **D4 — committed document and generated output stay out of manual formatting and lint.** The committed document lives at `shared/api/openapi/server-api.openapi.json`. Generated code lives under `shared/api/src/generated/`, is formatted by Orval's Prettier hook with the pinned root Prettier, and is excluded from ESLint. The exported JSON is added to `.prettierignore`, so `format:check` never rewrites a contract byte. Both changes touch root tooling and need the root-tooling owner's sign-off.

> **Adoption note (2026-10-17, server SV22; see the landing record below).** D1: Vitest begins in M2; this changes policy only (the line above and `shared/AGENTS.md`), and installing the runner remains item 3.1. D2: the recommended response-only numeric fidelity is adopted and implemented by the server as **H3** (server M7.6 follow-up Phase 4.2); the `number | string` alternative is not taken. D3: the recommended derived per-operation tiers are adopted and implemented as **H4** (Phase 4.3) with schemes `IdentityBearer` and `BranchBearer`, `security: []` for anonymous operations, and `[TokenAuthorize]` role names listed on `BranchBearer` (OpenAPI 3.1 allows role names for non-OAuth schemes); the `{ method, path }` fallback is not needed. Scheme names are metadata labels: both tokens travel in the same `Authorization: Bearer` header. D4: adopted as stated; the `.prettierignore`/ESLint changes stay with the root-tooling owner and are not made by the server track. H1 and H2 shipped earlier and are not reopened.

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
  - One WebApi.Test OpenAPI contract test, and the deliberate snapshot update.
  - A §6.15 note plus a doc-sync revision bump.
- **H4 — per-operation auth tiers (if D3 adopts it).**
  - One new operation transformer in `server/server.API/OpenApi/` and its registration line.
  - One WebApi.Test contract test pinning one anonymous, one identity and one branch operation, and the deliberate snapshot update.
  - A §6.15 note plus a doc-sync revision bump.

> **H3/H4 ownership record (2026-10-17, server SV22; input for item 1.2, which Shared ticks once the landing commit exists).** Owner: the server track (server M7.6 follow-up Phase 4). Baseline `7e8b169`, spec `v52`. Landing commit: **`763edd7`**, spec `v52` (recorded after the implementation commit was created). The delivered file set is wider than the plan above, because the tier derivation is a separate resolver and the evidence needed more than one test:
> - Server: `server/server.API/OpenApi/ResponseNumericFidelityOpenApiDocumentTransformer.cs` (H3), `OperationTokenRequirementResolver.cs` and `OperationSecurityOpenApiOperationTransformer.cs` (H4), and the registration plus the two scheme definitions in `server/server.API/ApiDependencyInjection.cs`.
> - Tests (`server/tests/WebApi.Test/OpenApi/`): `ResponseNumericFidelityTransformerTest.cs`, `ResponseNumericFidelityGeneratorTest.cs`, `ResponseNumericWireFidelityTest.cs`, `ResponseNumericFidelityOpenApiContractTest.cs`, `OperationTokenRequirementResolverTest.cs`, `OperationSecurityOpenApiContractTest.cs`, `OperationSecurityPrecedenceTest.cs`, `AuthTierProbeHost.cs`, the `OpenApiContractSurface.cs` renderer (security section and per-operation `security` line), and the updated `Snapshots/openapi-contract-surface.txt`.
> - Docs: `docs/loto-specs.md` §6.15 and the doc-sync group at `v52`, and `server/AGENTS.md` OpenAPI rules. `design/docs/product.md` / `screens.md` need no semantic change. After the reconciliation, their markers and the `screens.md` source-contracts reference moved to `v52`, and the actual doc-sync checker plus 34/34 regression scenarios pass.
> - Status (2026-10-17): server Phase 4 items 4.1–4.6 are checked, with landing reference `763edd7`. The Shared standing gates pass on the owner-chosen Node 26 pin (`>=26.6.0 <27`; see the Shared M1 amendment): `pnpm -r typecheck`, `pnpm -r lint` and `pnpm format:check` all exit 0. Shared repeats its own integration/input gates in Phase 1.
> The evidence (commands, hashes and parity) is in the server milestone's Phase 4 delivery note. Shared's own export script, committed input and generation (1.3 onward) remain Shared work.
> - **Export constraint for 1.3** (reproduced 2026-10-17): once a Release export is cached, an incremental export build pointed at a *fresh* `OpenApiDocumentsDirectory` returns success but writes no document. A `--no-incremental` export to a fresh directory regenerates it. The export script must therefore force regeneration (`--no-incremental`, or an equivalent target invalidation) and must fail when the document is missing, never treat a skipped target as a fresh export. This is MSBuild/H2 caching behavior, not a transformer change.
> - **Shared standing gates:** SV22 changes only `shared/AGENTS.md` and `shared/docs/milestones.md`, both outside `format:check` by `.prettierignore` (`**/AGENTS.md`, `**/docs/`), and no TypeScript. `pnpm -r typecheck/lint/format:check` could not run on Node `>=24.21.0 <25` at the time. After the owner moved the pin to Node 26 (Shared M1 amendment), all three exit 0.
> - **Generated names:** a component that both a request and a response reach is published twice — the input under its own id, the response as `{id}Output`. Today's contract has none (only string enums are shared), so no `…Output` component exists yet.

### Phase 1 — Contract input and deterministic export

- [x] **1.1** Tripwire gate: confirm SV03 is integrated. Record in the milestone notes:
  - the server commit and spec revision
  - that the snapshot and exported document contain `/report/manager-queue` and `ResponseManagerQueue*Json`, and no `/report/dashboard`

  Phases 2–4 commit nothing generated before this item is checked.
- [x] **1.2** Record the agreed ownership of H2, and of H3/H4 if D2/D3 adopt them: exact files, owner and landing commit. The server owner lands H2 before 1.3 runs against the real tree. Record the H2 ordinary-build regression evidence: a no-config `dotnet build server/server.sln --no-incremental` succeeds, and no OpenAPI document is written without the explicit `-p:OpenApiGenerateDocuments=true` override.
  - Server input handoff landed at `763edd7`, spec `v52`; H1/H2 were delivered earlier at `26fb2bb`. Exact H3/H4 files, owner and evidence are recorded above. This item remains for the Shared owner to verify in the actual integrated checkout and repeat the ordinary-build check; the server landing does not implement 1.3–1.6.
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
  - `generate:check` (`shared/api/scripts/check-generated.ts`) never relies on `git diff`. That command ignores untracked additions, and a repo-relative pathspec silently matches nothing when run from `shared/api`; both false passes were reproduced.
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
  - **Numeric fidelity** follows D2: `ResponseTransactionJson.value` and `.version` are `number`, or the chosen widened type is recorded.
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


### Delivered state and verification

**Status:** Complete. Phases 1–5 landed in `d095262` (server input `763edd7`, spec v52); later regenerations follow each landed server contract.

- **Export (1.3–1.5):** `scripts/export-openapi.ts` forces a `--no-incremental` Release build into a fresh temp directory, strips real server configuration case-insensitively (connection-string prefixes, `Token`, `ASPNETCORE_`/`DOTNET_`, security-mail and `GoogleIdentity` aliases), injects inert values and copies the document unmodified. Missing, stale or failing exports exit 1. `export:check` compares bytes and needs the .NET 10 SDK.
- **Generation (2.1–2.8):** Orval 8.39.0 (`fetch` and `react-query`, `tags-split`, `headers: true`, `useDates: false`) over the committed document only. `generate:check` regenerates into a mirror and fails on modified/added/deleted files. Tree hash recipe: sha256 over UTF-8 lines `<file-sha256>  <relative-path>\n` for every file under `src/generated/`, sorted ordinally by relative path. `contract-fidelity.ts` and `query-fidelity.ts` pin enum names, required/nullable fields, numeric directions, required financial headers and `ETag` access.
- **Client core (3.1–3.7):** per-call `createApiClient` clients and a default from `configureApiClient`; the token provider receives `{ method, path, authTier }`; failures normalize to `ApiError` (provider failures are kind `token-provider`, never dispatched). Dispatch starts at the `fetch` invocation; cancellation is rechecked after the provider settles. `scripts/header-boundary.ts` rewrites Orval's header merge at generation time so caller headers stay separate: a conflicting `Idempotency-Key`/`If-Match`/`Content-Type` in any casing and any caller `Authorization` are refused before dispatch; ordinary headers pass through.
- **Queries and boundary (4.1–4.4):** `@tanstack/react-query` is a peer (`^5.104.1`); hooks are reachable only through `@loto/api/queries`, with no generated `retry`. `scripts/check-boundary.ts` (inside `lint`) rejects React/TanStack/query modules reachable from the root entry.
- **1.6 / 5.2:** verified on a local clone of `d095262`: frozen install, recursive typecheck/lint/format, 91/91 tests, `generate:check` from both directories and `export:check` (input `6fd6f244…7ea7`, tree `d2f91f08…20fe7`, 227 files).

| Regeneration (source) | Input SHA-256 | Paths / operations / schemas | Generated files | Tests |
|---|---|---|---|---|
| S06 landing `d095262` (server `763edd7`, v52) | `6fd6f244…7ea7` | 73 / 101 / 166 | 227 | 91 |
| Server M8 Phases 1–5 (`9ea777c`, v53) | `fcfe9415…15b9` | 87 / 118 / 186 | 255 | 101 |
| Server M8 Phases 6–7 (`b96c5d3`, v54) | `e903b801…6d48` | 90 / 120 / 186 | 257 | 103 |
| Server M11 Phases 4–7 (`18012d5`, v56) | `b2f7dcf0…23c2` | 93 / 123 / 214 | 287 | 103 |
| Server M8 Phase 8 local subset (v57) | `8f420211…abbd` | 93 / 123 / 214 | 287 (tree `5e9f08bd…b536`) | 103 |
| Server M9.6 Phases 2–3 (v59; current document) | `39a273db…401a` | 107 / 138 / 253 | 340 (tree `8dc2a132…e55f`) | 103 |

The current document replaces `/dailyclose` with the `/session` routes (start, opening proposal, handover, finish, custody exception, declare, rearrange, opening-discrepancy attribution, opening count) and adds `/terminal`; `daily-close` endpoints and `requestOpenDailyCloseJson` are gone, transaction creates gain `SessionId`/`HistoricalReason`/`OpeningAcknowledgment`, and the generated-header merge count is 132. `@loto/core` labels the eight new session enums (33 maps, matching copy §5). The Web BFF and Mobile auth-policy generated files were regenerated by their own generators. Earlier documents added the 429 `Retry-After` header declarations to login and the abuse-protected anonymous flows; consumers read `Retry-After` from `ApiError.headers`, and nothing retries automatically. `ResponseOpenChequeAgingGroupJson.originTransactionId` is optional/nullable since the M11 regeneration. No generated file is hand-edited and no `dashboard` path exists.

Server delivery and essential migration/SMTP/setup guidance for the M8 regenerations are in [Server M8](../../server/docs/milestones.md#m8-accepted-delivery-and-essential-operations).

**Not claimed here:** Hermes/Metro and Next behavior (proved by Web/Mobile M1/M2). `Scalar.AspNetCore` floats (`Version="*"`) on the server — a restore-reproducibility risk owned by the server.
---

## Milestone 3 — Core Helpers

**Goal:** Deliver the pure TypeScript formatting, parsing, identifier validation and enum-label helpers shared by web and mobile.

**Preconditions:** Shared M2 generated enum types and Design M0 formatting/copy contracts.

**Status:** Complete. Implementation and Web/native verification are done; the final formatter strategies are verified on Android 16/API 36 and iOS 26.5.

**Boundaries:**

- `@loto/core` exposes only its public root; runtime source uses no platform storage, environment, device clock/locale or DOM/Node API. Generated API imports are type-only.
- Backend financial outcomes remain authoritative. Helpers format values and parse input; they do not calculate balances, variance, FIFO, fees or credit.
- Wire money remains JSON `number`. Branded integer `Cents` and explicit `…Wire` entry points prevent unit ambiguity; values outside `numeric(14,2)` or wire values with more than two decimals are rejected.
- Manual display rules implement `design/specs/formatting-ptbr.md`; label maps follow the generated enums and `copy-guidelines.md`. The parser uses exact BigInt rational arithmetic, with bounded complexity and one final rounding step.

### Phase 1 — Contract inventory and API map

- [x] **1.1** Every formatting/copy row mapped to one public helper with its input/output units, invalid/empty handling, test and native-evidence state (table below).
- [x] **1.2** Public entry: `@loto/core` (`.` only). No deep import, no build layer; `@loto/api` is a type-only workspace dependency.

| Contract row              | Helper (input → output)                                                                                              | Invalid / empty                                                         | Tests                                                                   | Native state                           |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- | ----------------------------------------------------------------------- | -------------------------------------- |
| §2 money display          | `formatBrl(Cents)`, `formatBrlWire(number)`, `formatBrlAmount` → `R$ 1.234,56`                                       | negative, >2 decimals, non-finite, out of range → `RangeError`          | `brl.test.ts` (code points, 12-digit max, wire round-trip ×2000)        | Accepted: Android 16/API 36 + iOS 26.5 |
| §2 input / paste          | `parseBrl(text)` → cents; entry digits (`normalizeMoneyDigits` …)                                                    | `empty` / `malformed` / `signed` / `out-of-range`; 15th digit → null    | `brl.test.ts` (38 cases, round-trip ×2000)                              | Accepted: Android 16/API 36 + iOS 26.5 |
| §3 signed variance/deltas | `formatSignedBrl(Cents)`, `…Wire` → `+R$`/`−R$`/`R$ 0,00`                                                            | `-0` → unsigned zero                                                    | `brl.test.ts`                                                           | Accepted: Android 16/API 36 + iOS 26.5 |
| §3 fiado balance          | `presentFiadoBalance(Cents)`, `…Wire` → `{ kind, signedCents, amount, label, text }`                                 | as money                                                                | `spoken-fiado.test.ts`                                                  | Accepted: Android 16/API 36 + iOS 26.5 |
| MoneyInput spoken value   | `spellBrl(Cents)`, `spellSignedBrl`                                                                                  | negative → `RangeError` (unsigned)                                      | 32 hand-written sentences up to the maximum                             | Accepted: Android 16/API 36 + iOS 26.5 |
| A9 arithmetic input       | `evaluateMoneyExpression(text, options)` → typed outcome, cents                                                      | 9 error codes, `incomplete`, `empty`                                    | `money-expression.test.ts` (fixed + integer oracle ×1500 + adversarial) | Accepted: Android 16/API 36 + iOS 26.5 |
| §4 date-only              | `CalendarDate`, `parseCalendarDate`, `formatDate`, `parseDateInput`, long/weekday/month names, `addDays`/`addMonths` | null (parse) / `RangeError` (display)                                   | `dates.test.ts` (UTC-calendar oracle ×3000, leap/month/year edges)      | Accepted: Android 16/API 36 + iOS 26.5 |
| §4 wall clock             | `formatWallTime`, `formatWallDateTime`                                                                               | offset/`Z`/impossible → `RangeError`                                    | `dates.test.ts`                                                         | Accepted: Android 16/API 36 + iOS 26.5 |
| §4 UTC instant            | `formatInstant*(value, timeZone)`, `parseInstant`                                                                    | offset-free/impossible → null/`RangeError`; unknown zone → `RangeError` | 15 zone cases incl. DST start/end, +05:30, +12:45; both calendar ends   | Accepted: Android 16/API 36 + iOS 26.5 |
| §5 durations              | `formatDuration(h)`, `formatSignedDuration(h)`                                                                       | negative unsigned / non-finite → `RangeError`                           | half-minute edges, integer oracle ×2000                                 | Accepted: Android 16/API 36 + iOS 26.5 |
| §6 masks/validation       | `formatIdentifier(kind, v)`, `maskIdentifierInput(kind, text)`, `identifierDigits`, `isValidCpf`/`isValidCnpj`       | verbatim passthrough; repeated digits false                             | `numbers-identifiers.test.ts`                                           | Accepted: Android 16/API 36 + iOS 26.5 |
| §7–§8 numbers/absent      | `formatCount`, `formatDecimal`, `formatPercent`, `formatInstallment`, `EM_DASH`, `formatOptional`                    | non-integer count / bad position → `RangeError`                         | `numbers-identifiers.test.ts`                                           | Accepted: Android 16/API 36 + iOS 26.5 |
| copy §5 (25 maps)         | `…Labels`, `enumLabels`, `enumLabel`                                                                                 | unknown → `{ known: false, label: raw }`                                | `labels.test.ts` (generated keys, copy text)                            | Accepted: Android 16/API 36 + iOS 26.5 |

### Phase 2 — BRL, spoken money and fiado presentation

- [x] **2.1** `Cents` brand and wire boundary: `asCents`, `centsFromWire` (exact, rejects non-finite, >2 decimals, out of range, `-0` → 0), `wireFromCents` (the JSON number whose shortest text has ≤2 decimals).
- [x] **2.2** Displays (formatting §2–§3): `formatBrl`/`formatBrlWire` (unsigned; a negative value throws rather than hiding its sign), `formatBrlAmount` (no unit, for MoneyInput), `formatSignedBrl`/`formatSignedBrlWire` (`+R$`, `−R$` U+2212, unsigned zero); U+00A0 after `R$`.
- [x] **2.3** Input: `parseBrl` (paste: optional `R$`, surrounding spaces, strict dot grouping, 1–2 decimals; signs, other symbols and >12 integer digits rejected by category) and the cents-digit model (`normalizeMoneyDigits`, `centsFromMoneyDigits`, `moneyDigitsFromCents`, `formatMoneyDigits`, 14-digit cap).
- [x] **2.4** `presentFiadoBalance`/`…Wire`: `> 0` debt unsigned · **em aberto**; `0` **quitado**; `< 0` absolute value with explicit `+` · **crédito do cliente**; the signed API value is preserved and nothing is summed.
- [x] **2.5** Spoken money `spellBrl` (and `spellSignedBrl`): pt-BR cardinal words, _real/reais_, _centavo(s)_, _de reais_ after exact millions/billions, up to 999 999 999 999,99; hand-rolled (one contract for both platforms).

### Phase 2b — Restricted arithmetic money input (server M9.6 Phase 8 / plan A9)

- [x] **2b.1** `evaluateMoneyExpression(text, { allowNegative })`: tokenizer + recursive descent, nothing executed. Grammar, aliases, whitespace, precedence, unary signs, exact rational arithmetic, final half-away-from-zero rounding to cents and limits documented in `shared/AGENTS.md`.
- [x] **2b.2** Typed results: `empty`, `incomplete` (in-progress text), `invalid` with an error code and position, `ok` with `cents`, `rounded` and `expression`; the app owns the pt-BR messages and sends only the resulting number.
- [x] **2b.3** Tests with independent expected values: sums, grouping/decimal commas, precedence/parentheses, unary signs, division and rounding edges, maximum magnitude, malicious and overlong input.

### Phase 3 — Calendar, wall clock, instants, durations and numbers

- [x] **3.1** `CalendarDate` brand (`YYYY-MM-DD`, years 0001–9999, real calendar validation), parts, leap years, weekday, `addDays`/`addMonths` (clamped), compare — integer arithmetic, no `Date`.
- [x] **3.2** Displays: `formatDate`, `parseDateInput` (`dd/MM/yyyy`), long date, weekday long/short, month name, `formatMonthYear`, `formatMonthField`, `formatDateWithWeekday` (calendar cell name).
- [x] **3.3** Wall clock (rendered exactly as returned, offsets rejected): `formatWallTime`, `formatWallDateTime`.
- [x] **3.4** UTC instants: `formatInstant`/`formatInstantDate`/`formatInstantTime(value, timeZone)` — the caller supplies the device zone (formatting §4); an offset-free value is rejected (it is a wall clock), and so is a conversion whose local date leaves 0001–9999 AD.
- [x] **3.5** Durations `formatDuration`/`formatSignedDuration` from the decimal hours' exact decimal text: minutes = round(|h| × 60) half away from zero, sign reapplied.
- [x] **3.6** `formatCount`, `formatDecimal`, `formatPercent`, `formatInstallment`, `EM_DASH`/`formatOptional` (absent ≠ zero).

### Phase 4 — Identifiers and enum labels

- [x] **4.1** `formatIdentifier`/`formatCpf`/`formatCnpj`/`formatPhone`/`formatCep` with verbatim passthrough, `identifierDigits` (strips mask punctuation only).
- [x] **4.2** `isValidCpf`/`isValidCnpj` (check digits; repeated-digit sequences rejected). Numeric CNPJ only — see the open note.
- [x] **4.3** Progressive input mask `maskIdentifierInput(kind, text)` (formatting §6 "input mask auto-punctuates"): punctuation added as digits arrive, phone landline shape up to 10 digits and mobile at 11, a complete value equal to the display mask, unsupported or overlong text verbatim.
- [x] **4.4** 25 exhaustive maps keyed by the generated enums (`Record<Enum, string>`; a new value fails typecheck and the coverage test) plus `enumLabel` (unknown → raw value, `known: false`) and the **Automática** marker.

### Phase 5 — Tests, boundary and docs

- [x] **5.1** Vitest (same pinned versions as `@loto/api`) with fixed contract examples, boundaries, seeded property checks with independent oracles, and code-point assertions.
- [x] **5.2** Boundary test (imports, globals, `Intl` use limited to the instant module, no clock/locale reads) and a public-export snapshot.
- [x] **5.3** `shared/AGENTS.md` and this milestone record the API, the strategy table and the native evidence state.

### Phase 6 — Web and native verification

- [x] **6.1** Verify the Web adapter through Next SSR and Chrome, including Unicode, hydration and explicit time-zone cases; integration is recorded in Web M3 7.4.
- [x] **6.2** Execute all 99 public exports through 338 portable assertions on native Hermes for both OSs, including BigInt, calendar/era boundaries, DST and historical/non-hour offsets.
- [x] **6.3** Record the measured per-family strategies and verify them natively on both OSs.

### Verification and strategy

**296 core tests** pass, including independent/property oracles, boundary/purity checks, progressive identifier masks and four native-derived instant regressions. Workspace checks, API drift/export, Web build/gallery/session regressions and both-platform source-map checks pass. Both OSs passed five cold-start/probe/scroll/background-resume cycles with 338/338 assertions. Native verification also covered DST, Chatham, historical New York, AD boundaries and the rational 1/3 case.

Money, spoken values, calendar/wall-clock display, durations, numbers, identifiers and labels are hand-rolled; arithmetic parsing uses BigInt. UTC instants use only a validated numeric `Intl` IANA `longOffset`, including historical seconds, then derive a proleptic-Gregorian calendar from epoch + offset. This avoids the reproduced hybrid Julian/Gregorian calendar fields and rejects local dates outside AD 0001–9999. Offset parsing supports iOS split parts and Android complete prefix parts; missing/unrecognized offsets fail closed. The caller supplies the zone.

Raw Intl display differences do not change the NBSP/minus/unsigned-zero/copy contract. Native probes are mobile-owned and excluded from release bundles. Shared stays pure TypeScript.

### Remaining scope and dependencies

- Alphanumeric CNPJ is outside the current numeric-only formatting contract: it renders verbatim and fails `isValidCnpj`; supporting it requires a product/design decision.
- Mobile M1 retains an unexplained 2026-10-27 iOS SIGABRT in `objc_storeWeak` during UIKit keyboard-shortcut enumeration. Later clean runs bound observation only. See [Mobile M1/M2](../../mobile/docs/milestones.md#milestone-1--expo-scaffold).
- Server M9.6 Phase 8 records parser integration separately; design owns the universal money-field interaction pattern. No future session/custody enum or financial API is introduced here.
