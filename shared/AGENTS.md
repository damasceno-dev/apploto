# Shared Agent Notes

## Purpose

Cross-platform runtime TypeScript code shared by `web` and `mobile`. This file is the single source of truth for the area; the milestone plan lives in `shared/docs/milestones.md`.

## Packages

Both packages use the `@loto/*` scope and live under `shared/`:

- `@loto/api` (`shared/api`)
- `@loto/core` (`shared/core`) — day-one (decided 2026-06-26); web and mobile need BRL/CPF/date formatters and pt-BR enum-label maps immediately

Only the paths in each `exports` map are public. Deep imports such as `@loto/api/src/client` are rejected by TypeScript and Node. Adding an entry is a deliberate change to this file and the milestone plan.

### `@loto/api` — two entries

- `@loto/api` (`.` → `src/index.ts`): generated contract types (`src/generated/model/`), raw functions (`src/generated/endpoints/`, one file per tag) and the client-configuration API (`src/client/`). **Server-safe:** it must never reach `./queries`, `src/generated/queries/`, React or `@tanstack/*`, directly or transitively. Next Server Components import from here.
- `@loto/api/queries` (`./queries` → `src/queries/index.ts`): the generated TanStack Query hooks, query keys and options (`src/generated/queries/`). **Client-only:** web client components and mobile screens import hooks from here.

Package layout (Shared M2):

- `openapi/server-api.openapi.json`: the generation input, the server's build-time export copied byte for byte. It is never formatted or edited (`.prettierignore`).
- `orval.config.ts`: two Orval projects over that one input and one model directory. `raw` uses the `fetch` client; `queries` uses `react-query` over `fetch`.
- `src/generated/`: generated output only. It is never hand-edited, is excluded from ESLint, and is formatted by Orval's Prettier formatter with the root `.prettierrc`.
  - `model/`, `endpoints/` and `queries/` come from Orval. The queries carry their own internal fetchers rather than importing the raw functions; both call the same mutator.
  - `operations/operations.ts` is written by `scripts/codegen.ts`: each operation's method, path, `authTier` and declared header parameters.
- `src/client/`: the hand-written client core and the Orval mutator (`fetch.ts`).
- `src/contract-checks/`: compile-time contract gates, compiled by `typecheck` and never imported by an entry.
- `scripts/`: build-time tooling, run with Node's built-in type stripping. It is the only code in the package that touches the process environment or the file system, and nothing in it ships. `header-boundary.ts` is generation tooling too (loaded by `orval.config.ts`).
- `test/`: Vitest suites, run with an injected fake `fetch` and no network.

Commands (from `shared/api`, or `pnpm --filter @loto/api <script>` from the root; every script resolves its paths from its own file, so either directory works):

| Script | What it does | Needs |
|---|---|---|
| `export:openapi` | `dotnet build server/server.API … -c Release --no-incremental -p:OpenApiGenerateDocuments=true` into a fresh temp directory, with only inert child-process config (an `.invalid` DB host and a non-secret signing key). Every variable .NET could read as connection-string, token, account-security, SMTP or environment configuration is stripped case-insensitively, including `:` spellings, `DOTNET_`/`ASPNETCORE_`-prefixed keys and the `*CONNSTR_` prefixes, then copies `server.API.json` unmodified. It fails if the build fails or writes no document, and never falls back to an older file. | .NET 10 SDK |
| `export:check` | Re-exports to a temp file and fails on any byte difference from, or absence of, the committed document. Server M9 CI can adopt it. | .NET 10 SDK |
| `generate` | Clears `src/generated/`, runs both Orval projects, then writes the operation map. | Node only |
| `generate:check` | Regenerates into an isolated temporary mirror of the package and compares every path and byte with `src/generated/`. Modified, added (including untracked) and deleted files all fail; it never uses git. | Node only |
| `test` | Vitest. | Node only |

**Regeneration procedure:**
1. `export:openapi`
2. `generate`
3. Review both diffs.
4. Commit, recording the server commit, the spec revision and the document sha256 in the Shared M2 notes.

### Client core contract (`src/client/`)

- **Configuration is explicit and injected.** Nothing runs at import time, and `src` cannot read the environment: its tsconfig has `types: []`, so `process` and `import.meta.env` do not compile.
  - `createApiClient({ baseUrl, getAccessToken, fetch? })` returns a frozen client. Pass it per call as `{ client }` in a generated function's last argument (`ApiRequestOptions`: `RequestInit` fields such as `signal`, `cache` and ordinary `headers`, without `method` or `body`); on a server, create one per request, and concurrent requests never share credentials.
  - `configureApiClient(config)` sets a process-wide default for single-credential runtimes (browser, mobile), and `clearApiClientConfiguration()` removes it.
  - Each request snapshots its client when it starts. A call with neither a per-call client nor a default rejects with `ApiConfigurationError`.
  - `baseUrl` may be an absolute origin or a same-origin prefix (for example a BFF route), and `fetch` defaults to the runtime's global, looked up per request.
- **Token provider.** `getAccessToken({ method, path, authTier })` is awaited on every request and never cached.
  - `authTier` (`anonymous` | `identity` | `branch`) is generated from each operation's published `security`: `[]`, `IdentityBearer` and `BranchBearer` respectively (server H4).
  - Generation fails on any other security shape, on a document-wide requirement, or on two templates that could match one path with equal precedence. Concrete paths resolve to the template with the most literal segments.
  - Published role names are documentation, not app-side permissions, and are not part of the tier.
  - A `null` token sends no `Authorization` header: the provider is the only source of `Authorization`, and a caller-supplied one is refused before dispatch.
  - A provider that throws or rejects ends the call with an `ApiError` of kind `token-provider` (`cause` kept), before dispatch and never uncertain.
  - Shared never stores, renews or caches tokens, never reads cookies or the secure store, and has no BFF, session or platform code.
- **Results and errors.**
  - A 2xx resolves to `{ data, status, headers }`: only the success variants are typed, and `headers` exposes `ETag`. An empty body gives `data: undefined`.
  - Everything else rejects with `ApiError`, which carries:
    - `kind`: `invalid-request` or `token-provider` (both before dispatch), `http`, `invalid-response`, `network`, `timeout` or `aborted`;
    - `status`, `method`, `path` and `headers`;
    - `errorMessages`: the `ResponseErrorJson` list verbatim and in order, duplicates kept; `null` or missing becomes `[]`;
    - `body` (`error-json`, `unrecognized` or `none`) and `rawBody`;
    - `dispatched`: true once `fetch` was invoked.
  - An abort before dispatch sends nothing. This includes one while the token provider is pending (the call settles at once) and one that lands after the provider resolved but before `fetch` (rechecked immediately before the synchronous `fetch` invocation).
  - `isOutcomeUncertain(error)` is true only for a dispatched non-GET that ended in 5xx, network loss (including a body lost after the status line), timeout or abort. Callers keep their frozen candidate and key.
  - Generated hooks' `TError` is this `ApiError`.
  - Transport messages are technical; screens render pt-BR copy.
- **Financial headers and no retry.**
  - `Idempotency-Key` and `If-Match` are required generated arguments (and required in mutation variables), sent byte for byte.
  - **Header boundary.** Callers may add ordinary headers (for example tracing headers) through `options.headers`, in any `HeadersInit` form. The generated header arguments still always win:
    - Orval 8.39.0 would merge `options.headers` after them, so `scripts/header-boundary.ts` wraps Orval's own `fetch` and `react-query` builders (the documented function-valued `client` option) and emits `headers: { …generated }, callerHeaders: options?.headers` in all 110 merge sites. Generation fails if Orval ever emits an unrecognized merge.
    - Before dispatch, the mutator refuses (`invalid-request`) a caller header that names a generated or declared header (`Idempotency-Key`, `If-Match`, `Content-Type`) with a different value in any casing, a caller `Authorization`, and a name repeated in another casing.
    - An equal value is not a conflict. Ordinary headers are sent unchanged.
  - `toIfMatch(version)` and `parseETag(header)` implement §6.11: one strong, quoted, positive decimal `xmin` up to `4294967295`.
  - Every call invokes `fetch` exactly once: no retry on any status or network error, no 401 renewal, no key minting or replay.
  - Generated hooks set no `retry` (enforced by `check-boundary.ts`), and apps keep mutation retries at 0.
- **D2/D3 outcomes (server H3/H4, landed at `763edd7`, spec `v52`).**
  - Response numbers are `number`; request numbers stay `number | string` where binding accepts numeric strings.
  - Dates stay strings (`useDates` off), including the offset-free `branchLocalDateTime`.
  - Per-operation tiers come from the document.

### `@loto/core` — one entry, pure TS only (Shared M3)

- `@loto/core` (`.` → `src/index.ts`). `src/format/` (money, spoken money, fiado presentation, the arithmetic input parser, durations, numbers), `src/dates/` (calendar, wall clock, UTC instants), `src/validate/` (identifier masks and check digits) and `src/labels/` (copy §5 maps). `test/boundary.test.ts` pins the exact export list: adding or removing one is a deliberate change here and in the milestone.
- It implements `design/specs/formatting-ptbr.md` and `copy-guidelines.md` §5; the spec is the contract and a change there is a breaking change here.
- **Purity.** No React, DOM, Node, `process`, `import.meta`, storage, clock (`Date.now`, `new Date()`), device locale/zone (`toLocale*`, `resolvedOptions`), randomness or code evaluation, and no import side effects. `@loto/api` is a dependency for `import type` only (its root entry; never `./queries` or a deep path), so nothing of the API package runs or reaches an SSR bundle through core. `test/boundary.test.ts` recompiles `src` against `lib.es2022` alone and scans the syntax tree (with seeded negatives); the package tsconfig keeps default libs only because the type-only import is checked with them.
- **Money.** Integer cents are the branded `Cents` (`asCents`, or a parser); a JSON wire number goes only to the `…Wire` functions (`centsFromWire`, `formatBrlWire`, `formatSignedBrlWire`, `presentFiadoBalanceWire`). Range is numeric(14,2): |cents| ≤ `MAX_CENTS` (99 999 999 999 999), exact in a safe integer. Wire values are read from their shortest decimal text, never by multiplying the double; a value with more than two decimals, non-finite or out of range **throws** `RangeError` (a contract violation is never rounded into a figure). `wireFromCents` returns the JSON number (≤ 2 decimals when serialized). Displays: `formatBrl` (unsigned; a negative throws rather than hiding the sign), `formatBrlAmount` (no unit), `formatSignedBrl` (`+R$`, `−R$` U+2212, unsigned zero), U+00A0 after `R$`. `presentFiadoBalance` maps one API balance to `{ kind, signedCents, amount, label, text }` (debt unsigned · **em aberto**; **quitado**; credit `+R$ …` · **crédito do cliente**) and sums nothing. `spellBrl`/`spellSignedBrl` speak pt-BR money (hand-rolled cardinals, *catorze*, **de reais** after exact millions/billions).
- **Money input.** `parseBrl(text)` → `{ ok, cents }` or `{ ok: false, error: 'empty' | 'malformed' | 'signed' | 'out-of-range' }`: optional `R$`, spaces (incl. U+00A0/U+202F) around it and at the ends, strict dot grouping in threes, 1–2 decimals after a comma; a dot is never a decimal separator (`1.5` is malformed, `1.234` is 1234). Entry digits (MoneyInput's right-to-left model): `normalizeMoneyDigits` (null over `MAX_MONEY_DIGITS` = 14 — a keystroke past it is refused), `centsFromMoneyDigits`, `moneyDigitsFromCents`, `formatMoneyDigits`.
- **Arithmetic input (server M9.6 Phase 8 / plan A9).** `evaluateMoneyExpression(text, { allowNegative? })` replaces Access's `Eval` for typed figures only; nothing is executed. Grammar: `expression := term (('+'|'−') term)*`, `term := unary (('×'|'÷') unary)*`, `unary := ('+'|'−') unary | primary`, `primary := number | '(' expression ')'`; numbers use `parseBrl`'s numeral grammar. Aliases: `-` for `−`, `*` for `×`, `/` for `÷` (keyboard keys); whitespace between tokens only; no `R$`, `%`, `x`, exponents or implicit multiplication. Arithmetic is exact (BigInt rationals); only the final value is rounded to cents, halves away from zero (`rounded: true` when that changed it). Limits: 256 UTF-16 units, 128 tokens, nesting depth 32 (parentheses and unary signs), 512-bit intermediate size, each literal ≤ 12 integer digits, result within numeric(14,2), negative results rejected unless `allowNegative`. Outcomes: `empty`, `incomplete` (text more typing can complete: trailing operator, open parenthesis, `12,`, `1.23`), `invalid` with `error` (`too-long`, `too-complex`, `unsupported-character`, `malformed-number`, `unexpected-token`, `unbalanced-parentheses`, `division-by-zero`, `out-of-range`, `negative-result`) and a position, or `ok` with `cents`, `rounded` and `expression` (an operator, sign or parenthesis was used — show the result before accepting). The app owns the pt-BR messages and sends only the resulting number; the product pattern is Design 4.9.7.
- **Dates.** `CalendarDate` is a branded `YYYY-MM-DD` string (years 0001–9999, real calendar validation via `parseCalendarDate`); parts, leap years, `weekdayOf` (0 = domingo), `addDays`, `addMonths` (clamped), `daysBetween`, `compareCalendarDates` use integer civil-day arithmetic, never `Date`. Displays: `formatDate`, `parseDateInput` (`dd/MM/yyyy`), `formatLongDate`, `formatDateWithWeekday`, `formatMonthYear`, `formatMonthField`, `monthName`, `weekdayName`, `weekdayShortName`. Branch wall clock renders as returned: `formatWallTime`, `formatWallDateTime` (an offset or `Z` throws). UTC audit instants: `formatInstant`/`formatInstantDate`/`formatInstantTime(value, timeZone)` require an offset or `Z` and the **caller's** IANA zone — the device zone (formatting §4), resolved by the app; core never reads it. Seconds are dropped like a clock; a converted local date outside 0001–9999 AD (the era is read from `Intl`) throws.
- **Durations and numbers.** `formatDuration`/`formatSignedDuration` from decimal hours via their exact decimal text: minutes = round(|h| × 60) halves away from zero, sign reapplied (`7.33` → `7h 20min`, `0.175` → `0h 11min`). `formatCount`, `formatDecimal` (exact, optional half-away rounding), `formatPercent` (percent units), `formatInstallment`, `EM_DASH`/`formatOptional` (absent ≠ zero).
- **Identifiers.** `formatCpf`/`formatCnpj`/`formatPhone`/`formatCep`/`formatIdentifier(kind)` (display) mask only an exact digit count after removing mask punctuation and spaces; anything else is returned verbatim. `maskIdentifierInput(kind, text)` is the typing mask: punctuation appears as digits arrive (`5299` → `529.9`; phone landline shape to 10 digits, mobile at 11), a complete value equals the display mask, and text with other characters or more digits than the mask holds is returned verbatim. `identifierDigits` strips only that punctuation. `isValidCpf`/`isValidCnpj` check digits and reject repeated-digit sequences; numeric CNPJ only (formatting §6 defines 14 digits).
- **Labels.** 25 maps (`roleLabels` … `operatorLinkRequestStatusLabels`, and `enumLabels` by enum name), each `Record<GeneratedEnum, string>`, so a new or removed enum value fails typecheck; `test/labels.test.ts` checks every key against the runtime enum and every label against copy §5's text. `enumLabel(map, value)` returns `{ known: false, label: value }` for an unknown value (render neutrally and report; never guess). `generatedMovementMarker` is **Automática**.
- **Strategy per formatter (native evidence state).** Everything is hand-rolled and engine-independent **except** the UTC-instant conversion, which uses `Intl.DateTimeFormat` with an explicit `timeZone` and assembles numeric parts itself (no locale strings). That one is a **web-verified candidate, provisional for native**: Node and Chrome are measured, Hermes is not (Mobile M1 4.1/4.2). BigInt (money arithmetic, durations, decimals) is ES2020 and is likewise unexecuted on Hermes; Metro/Hermes **compile** the package (Shared M3 notes), which is not execution.
- Commands (from `shared/core` or `pnpm --filter @loto/core …`): `typecheck` (src + tests), `lint`, `format`/`format:check`, `test` (Vitest, Node only). Exact devDependencies match `@loto/api`'s: `vitest` `5.0.3`, `vite` `8.3.2`, `@types/node` `26.6.4`; `typescript` for the boundary test comes from the root.

## Source-package pattern

- Packages export TypeScript source directly. There is no build step, no `main`, no `dist/`, and no emitted `.js` or `.d.ts`. Web compiles them through Next `transpilePackages`; mobile through Metro. Revisit only if a consumer outside this repo ever appears.
- Every package is ESM only (`"type": "module"`), `"private": true`, and has no `version` field. Apps depend on them with `workspace:*`.
- Shared runtime code is linked through `pnpm workspaces` (`shared/*`, `web`, `mobile`), not Turborepo.
- Each package's `tsconfig.json` extends the root `tsconfig.base.json` and adds `include: ["src"]`.
- Each package defines these scripts, which the root `pnpm typecheck|lint|format|format:check` reaches through `pnpm -r`:
  - `typecheck`: `tsc --noEmit`
  - `lint`: `eslint .`
  - `format` / `format:check`: Prettier with `--ignore-path ../../.gitignore --ignore-path ../../.prettierignore`. Prettier reads ignore files only from its working directory, so these flags are required.
- Shared tooling comes from the root `devDependencies`. `@loto/api` declares `@tanstack/react-query` as a `peerDependency` (`^5.104.1`), so each app owns the instance and its `QueryClient`; Shared never creates one. Its exact `devDependencies` serve generation, typechecking and tests only (see Toolchain pins).
- `@loto/api` adds `test` (Vitest), `export:openapi`, `export:check`, `generate` and `generate:check`. Its `typecheck` also checks `tsconfig.tooling.json` (scripts, tests and configs, with Node types), and its `lint` also runs `scripts/check-boundary.ts`.

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
- Exact `@loto/api` `devDependencies` (Shared M2). Each was published at least 14 days before it was pinned (2026-10-17), and the rationale is in the Shared M2 notes:
  - `orval` `8.39.0`
  - `vitest` `5.0.3` and its required peer `vite` `8.3.2`
  - `@tanstack/react-query` `5.104.1`
  - `react` and `@types/react` `19.3.0` (typechecking the hooks only)
  - `@types/node` `26.6.4` (tooling only)
- The root `test` script fans out with `pnpm -r test`.
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
- `pnpm -r test` runs Vitest (installed by Shared M2, decision D1) for both packages; `@loto/core`'s suites include the purity boundary and the copy §5 conformance check.
- `@loto/api` typecheck and lint carry the contract gates: enum names, required and nullable fidelity, numeric direction, financial headers and the import boundary. `generate:check` (Node only) and `export:check` (.NET 10 SDK) prove the generated tree and the committed document are current.
