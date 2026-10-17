# Shared Agent Notes

## Purpose

Cross-platform runtime TypeScript code shared by `web` and `mobile`. This file is the single source of truth for the area; the milestone plan lives in `shared/docs/milestones.md`.

## Packages

Both packages use the `@loto/*` scope and live under `shared/`:

- `@loto/api` (`shared/api`)
- `@loto/core` (`shared/core`) — day-one (decided 2026-06-26); web and mobile need BRL/CPF/date formatters and pt-BR enum-label maps immediately

Only the paths in each `exports` map are public. Deep imports such as `@loto/api/src/client` are rejected by TypeScript and Node. Adding an entry is a deliberate change to this file and the milestone plan.

### `@loto/api` — two entries

- `@loto/api` (`.` → `src/index.ts`): contract types and raw API functions. **Server-safe:** it must never import `./queries` or `@tanstack/react-query`, directly or transitively. Next Server Components import from here.
- `@loto/api/queries` (`./queries` → `src/queries/index.ts`): TanStack Query keys, options, and hooks. **Client-only:** web client components and mobile screens import hooks from here.
- `src/client/`: hand-written API client setup.
- `src/generated/`: Orval output (generated request and response types, raw functions, query hooks). The Orval config also lives in this package.

Shared M2 fills `src/client/` and `src/generated/` and adds the automated check for the import boundary. Until then, the boundary holds because the root entry imports nothing.

### `@loto/core` — one entry, pure TS only

- `@loto/core` (`.` → `src/index.ts`)
- `src/format/`, `src/validate/`, `src/labels/`, `src/dates/`: constants, validation schemas, formatters, pt-BR enum-label maps, date helpers, utilities, and other non-UI helpers. Shared M3 fills them.

As of Shared M1, all three entry files are `export {}` placeholders; nothing is exported yet.

## Source-package pattern

- Packages export TypeScript source directly. There is no build step, no `main`, no `dist/`, and no emitted `.js` or `.d.ts`. Web compiles them through Next `transpilePackages`; mobile through Metro. Revisit only if a consumer outside this repo ever appears.
- Every package is ESM only (`"type": "module"`), `"private": true`, and has no `version` field. Apps depend on them with `workspace:*`.
- Shared runtime code is linked through `pnpm workspaces` (`shared/*`, `web`, `mobile`), not Turborepo.
- Each package's `tsconfig.json` extends the root `tsconfig.base.json` and adds `include: ["src"]`.
- Each package defines these scripts, which the root `pnpm typecheck|lint|format|format:check` reaches through `pnpm -r`:
  - `typecheck`: `tsc --noEmit`
  - `lint`: `eslint .`
  - `format` / `format:check`: Prettier with `--ignore-path ../../.gitignore --ignore-path ../../.prettierignore`. Prettier reads ignore files only from its working directory, so these flags are required.
- No package declares dependencies yet; tooling comes from the root `devDependencies`. Runtime libraries such as `@tanstack/react-query` arrive with Shared M2 as `peerDependencies`, so each app owns the instance.

## Toolchain pins

- Node 26: `.nvmrc` is `26`, `engines.node` is `>=26.6.0 <27`, and `.npmrc` sets `engine-strict=true`. Moved from the Node 24 LTS pin on 2026-10-17 by owner decision (Shared M1 amendment note). Node 26 was still "Current" then, with LTS promotion expected in late October 2026; re-verify its LTS status against the official release index.
- pnpm `10.34.6`, set in both `packageManager` and `engines.pnpm`.
- Exact root `devDependencies`:
  - TypeScript `6.0.3`
  - ESLint `9.39.5` and `@eslint/js` `9.39.5`
  - `typescript-eslint` `8.71.2`
  - `eslint-config-prettier` `10.1.8`
  - Prettier `3.9.10`

  The version rationale is in the Shared M1 Phase 2 notes. ESLint 9 is end-of-life but is still required by the Next lint stack; revisit ESLint 10 when Next and Expo accept it.
- `tsconfig.base.json` sets `strict`, `noUncheckedIndexedAccess`, `isolatedModules`, `verbatimModuleSyntax`, `module: "esnext"`, `moduleResolution: "bundler"`, `target: "es2022"`, `skipLibCheck`, and `noEmit`.
- The root `eslint.config.mjs` and `.prettierrc` apply to shared packages unchanged. React, Next, and Expo lint layers belong to the apps.

## Configuration injection

- Shared code never reads the environment: no `process.env`, `import.meta.env`, `NEXT_PUBLIC_*`, or `EXPO_PUBLIC_*`.
- The consuming app shell supplies configuration such as the API base URL and tokens through caller-provided interfaces. Apps own storage: cookies on web, secure store on mobile.
- Shared code never touches storage directly, and importing a module has no side effects beyond defining values.

## App-owned UI and platform code

- `web` and `mobile` own their UI components, styling, routing, navigation, and storage and transport adapters.
- Do not put React UI components in `shared`.
- Do not put Tailwind or NativeWind code, or design tokens, in `shared`. The apps read `design/tokens/*.json` at build time.
- Do not put Next-specific or Expo-specific APIs in `shared`.
- The only React-facing code in `shared` is the TanStack Query hooks behind `@loto/api/queries`.

## Important decisions

- The backend owns the API contract. Nothing in `shared` defines a request or response shape by hand; every contract type is generated from the server OpenAPI document. Hand-written code may wrap generated types, never duplicate them.
- `shared/api` starts with the generated client, raw functions, and TanStack Query helpers.
- Required C# fields must remain required in generated TS contracts.
- `server.Exceptions` stays backend-only; consumers rely on the serialized API error contract instead.
- Keep raw API functions available for Next Server Components.
- Keep TanStack Query wrappers available for web client components and mobile screens.

## Gates

- `pnpm -r typecheck` and `pnpm -r lint` stay green on every commit that touches `shared/`.
- `pnpm format:check` must also stay green, because the web and mobile standing gates run it across the whole workspace.
- Vitest arrives with Shared M2 (decision D1, independently accepted 2026-10-17): Shared M2 item 3.1 installs it for `@loto/api`; Shared M3 adds formatter/validator tests to the existing runner. Installing it and any root `pnpm -r test` fan-out are later Shared/root-tooling changes.
