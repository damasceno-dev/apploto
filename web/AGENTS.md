# Web Agent Notes

Milestones for this area live in `web/docs/milestones.md`.

## Stack direction

- Use Next.js App Router
- Prefer Server Components whenever possible
- Use Suspense and skeletons for loading states
- Use TanStack Query for client-side server state
- Package name is `@loto/web`; ESM only; the tsconfig extends the root `tsconfig.base.json`
- Consume `@loto/api` and `@loto/core` as source packages through Next `transpilePackages` — no build step in `shared/`

## Component guidance

- Use composition instead of prop-heavy components
- Prefer reusable components only
- Use Tailwind with merge and variant helpers rather than raw class interpolation
- Prefer named exports over default exports
- Build components to the design system's component contract (`design/specs/components/`) and screens to the screen catalog (`design/docs/screens.md`) plus their blueprints (`design/specs/screens/`)

## Design tokens

- Read `design/tokens/*.json` at build time through the web Tailwind preset — tokens never pass through `shared/`
- App code uses semantic and domain tokens only: no raw hex/rgb literals and no primitive-token references outside the token-adapter module (enforced by lint)
- Light and dark themes ship together
- Impeccable (design-refinement CLI) is a standing refinement tool at the build stage; feed it the token vocabulary so refinements stay inside the system

## API usage

- Server Components and route handlers call raw functions from `@loto/api`
- Client components use the hooks from `@loto/api/queries`
- Errors render `ResponseErrorJson.ErrorMessages` verbatim through the shared `ApiError` normalization

## Auth and the route-handler BFF

- The backend remains the real backend for web and mobile. The web layer never re-implements business rules, aggregations, or validation the API owns.
- A thin route-handler BFF **is expected** for browser auth: Next route handlers own login, branch session, token renewal, and logout, and keep the identity, branch, and refresh tokens in `httpOnly` cookies so no token is readable from browser JavaScript.
- Browser-side API calls reach the backend through that same cookie-to-bearer layer; it forwards request and response headers the contract depends on (`Idempotency-Key`, `If-Match`, `ETag`) unchanged and passes error bodies through untouched.
- Anything beyond auth, cookies, and header forwarding is not a BFF concern and needs a concrete web-only reason before it is added.

## Important decisions

- Mobile clones web: the screen set and its semantics are identical across platforms, so a web screen is built from the same catalog entry the mobile screen uses.
- pt-BR is the product language; follow `design/specs/copy-guidelines.md` and `design/specs/formatting-ptbr.md` (implemented by `@loto/core`).
- Business day and time come from `GET /branch/current`, never the device clock.

## M1 implementation baseline (2026-10-21; all 15 criteria independently accepted)

- Exact runtime pins: Next / eslint-config-next `16.4.2`, React / React DOM `19.3.1`, TanStack Query `5.104.1`, Tailwind / its PostCSS plugin `4.3.3`, tailwind-merge `3.7.0`, CVA `0.7.1`, clsx `2.1.1`, server-only `0.0.1`, @next/env `16.4.2`. Exhaustive tooling pins are in `package.json` and the root lockfile.
- Official compatibility/release sources: [Next installation](https://nextjs.org/docs/app/getting-started/installation), [Next TypeScript](https://nextjs.org/docs/app/api-reference/config/typescript), [typed routes](https://nextjs.org/docs/app/api-reference/config/next-config-js/typedRoutes), [React versions](https://react.dev/versions), [Tailwind theme namespaces](https://tailwindcss.com/docs/theme), [CVA](https://cva.style/docs), [tailwind-merge](https://github.com/dcastil/tailwind-merge). Exact versions and peer/engine ranges were also checked in the [npm registry](https://registry.npmjs.org/next/16.4.2).
- CVA is the variant helper for both apps: a small platform-independent API, without a new shared UI package. M1 installs it; component variants arrive in M3. `cn()` combines clsx and tailwind-merge. No class-sorting plugin or root Prettier change.
- tsconfig extends the root without changing module resolution, target, strictness or `noUncheckedIndexedAccess`. JSX is preserved for Next's compiler; DOM libs, interop/JSON support, incremental checking, the Next plugin and the app alias are app-local. `allowImportingTsExtensions` lets the Node launcher load the same TS validator without a second environment reader. `agentRules: false` preserves this repository-owned guide during development. `typecheck` runs `next typegen` before `tsc --noEmit` so typed routes work on a clean installation.
- `LOTO_API_BASE_URL` is mandatory, server-only and validated by `src/config/server-env.ts`; The app-local launcher loads Next environment files, then this same module under Node's `react-server` condition before starting dev/build/start; invalid configuration fails before binding a port. The example URL never triggers a request. Use explicit inert configuration for checks: `LOTO_API_BASE_URL=http://127.0.0.1:59999 pnpm --filter @loto/web build`.
- The raw-color gate covers application TS/TSX, app configuration and source CSS, including arbitrary color classes and CSS variable fallbacks. Only `src/theme/token-adapter.ts` is exempt from the TS rule. Fixtures live as test strings; CLI negative checks use disposable copies. The two apps own their small build-time lint implementations; Shared stays runtime-only.
- The placeholder imports real API runtime values/types, an empty core runtime namespace (not a fictional formatter), and a client-only hook/key builder under a small app-owned provider, with the generated query permanently disabled. This technical context proof makes no request; M2 owns production providers/session plumbing; M3 owns tokens/components.
- Implementation evidence, reproducible checks and inherited integration limitations are recorded in `docs/milestones.md`. Do not mark independent acceptance from compiler/browser checks alone.

### M1 review fixes (independently accepted in review round 3)

- P2-1: `turbopack.resolveAlias` maps TanStack Query to `./node_modules/@tanstack/react-query`, relative to this app. The relative app alias was verified in fresh production builds; it selects the Web React 19.3.1 peer rather than Shared's tooling React 19.3.0 peer. Next retains its own React aliases. A disabled generated hook under Web's provider passes SSR and browser hydration without requests.
- Every `build` now runs `tooling/check-bundles.mjs`: production SSR maps must contain exactly Web's installed query copy, and maps/page traces must not contain another executable React. The trace can include another workspace's `react/package.json` metadata; that is distinguished from executable React files, not treated as a second runtime. See [Turbopack resolution aliases](https://nextjs.org/docs/app/api-reference/config/next-config-js/turbopack#resolving-aliases).
- P2-2/P4: Outside styling contexts, a string/template fails only when its entire trimmed value is an explicit hex color or a single color function, including exported palette literals. Style values, arbitrary classes and CSS retain substring detection; only the single adapter path is exempt. Named colors remain style-context dependent; actual URLs/link fragments and ordinary copy remain valid. CSS at-rule params are checked as well as declarations. Diagnostic ranges suppress duplicate reports of the same violation. Cross-file palettes fail at their forbidden literal definitions, without evaluating imports.
- Runtime/tooling pins and lockfile are unchanged in this correction round; dependency keys are sorted. The same reviewer independently confirmed all round-1 fixes. The round-2 P3 copy false positive is corrected with passing reference-number/aria-label fixtures; the same reviewer confirmed the correction in round 3. Mixed composite values outside recognized style contexts remain an accepted, non-blocking trade-off; broad style-key matching was not added.

## M2 session and data plumbing (2026-10-21; implemented, awaiting independent review)

Evidence and the criterion map are in `docs/milestones.md` (Web M2). Durable decisions:

- **Layout.** BFF routes: `src/app/bff/session` (GET state), `…/login`, `…/branch` (POST select/switch, DELETE leave), `…/logout`, and the forwarder `src/app/bff/api/[...path]`. Their logic lives in `src/server/session` (cookies, `exp` reading, single-flight renewal, generations, service) and `src/server/bff` (allowlist, policy, forwarder) so it is tested without Next. `src/lib/bff-protocol.ts` and `src/lib/route-policy.ts` are isomorphic (no secrets). Browser code is under `src/client`.
- **Cookies.** Identity, refresh and branch tokens plus session metadata and the navigation intent live in `__Host-loto-*` cookies: `HttpOnly; Secure; SameSite=Lax; Path=/`, no `Domain`, 7-day max-age (intent 30 min). A deletion must repeat `Secure; Path=/` with `Max-Age=0` (`applyCookieWrites`), or the browser keeps the cookie. No token appears in JSON, logs, storage, the query cache or a URL.
- **Secure local origin.** Use `http://localhost:<port>`: browsers treat localhost as a secure context, so `Secure` and `__Host-` cookies work without TLS. Any other host needs HTTPS. Never drop `Secure` to make a check pass.
- **Forwarder.** The allowlist is generated from Shared's committed operation map (`pnpm --filter @loto/web generate:bff`; `lint` fails on drift). Credential-bearing or session-ending operations are BFF-owned and never forwarded (`tooling/bff-operations.mjs`). Every BFF call needs `x-loto-bff: 1`; state changes also need a same-origin `Origin` (`LOTO_WEB_ORIGIN` behind a proxy). `x-loto-bff-error` marks a response produced **without** forwarding (nothing executed); `x-loto-session` reports a forwarded 401's recovery.
- **Renewal.** One path: proactive 30 s before `exp` (the API has zero clock skew), reactive on a forwarded 401 (one cycle). Only GET is re-sent; a write is never replayed. While a renewal outcome is unknown, writes are refused before dispatch even on a still-valid bearer; reads continue. Renewal and branch-session recreation are single flights keyed by token hashes in a process-wide registry on `globalThis` (shared by `proxy.ts` and route handlers); a rotation is handed to late holders of the old token for 15 s. **Boundary:** in-process only — more than one Next instance needs sticky sessions or a shared store (M12 hosting decision).
- **Generations.** Login, logout and branch selection/leave start or end identity (`sid`) and scope generations; a late result for a generation another request ended writes no cookie, and the forwarder refuses a request whose `x-loto-scope` is not the current scope. Cookies of an ended generation or a superseded rotation never authorize (signed out, no branch, or renewal unconfirmed).
- **Session commands.** Login, branch select/leave and logout are serialized per browser (non-secret `__Host-loto-device` id) and compare-and-swap on the scope the browser declares in `x-loto-scope`: a command issued against an older state is superseded (409 `session-scope-changed`) and changes nothing. `GET /bff/session` and the proxy are serialized behind them. The `SessionProvider` ignores any read in flight when a command starts or settles, and only the newest command applies its result.
- **Open decision D-M2-1.** The catalog's per-tab branch context is not implemented: one branch session per browser profile, with the scope fence as the safety. See Web M2 review round 1 for the options.
- **Gate.** `src/proxy.ts` renews and resolves the gate for gated page requests before render (Server Components cannot set cookies, so they never renew); layouts repeat it with `requireBranchSession`/`requireIdentity`. Destinations come from the shell blueprint route map; `gateRedirect` is the single typed-route assertion until M4 builds them. A gated branch deep link is held in the intent cookie, bound to the branch/e-mail known at capture.
- **Data.** Server Components use `requestApiClient()` (one `createApiClient` per request). The browser uses one scoped client (`/bff/api`, `null` token provider) and one `QueryClient` per session scope; the default client is installed in the browser only. Mutation retry is 0; reads retry once only for transient failures.
- **Writes and device input.** Every write is sent with the client of the scope it was prepared in (`useScopedApiClient()`, or the controller's own client), never the process default. `OperationController`/`useIdempotentOperation` freeze one `Idempotency-Key` per intent in device storage before dispatch, are disposed with their scope, recheck disposal/pause after every await, and quarantine unreadable or incomplete stored records (every recovery field is validated) until an explicit recovery; resends need the replay window proved from server `Date` headers (the forwarder relays the API's `Date`). Navigation intents with credential-like query parameters are dropped. `guardedWrite` round-trips `ETag`/`If-Match`; a guarded 409 is the generic reload path. Device drafts are partitioned by SHA-256 of the returned login e-mail + branch, refuse credential-like values and are cleared only per context/partition.
- **Tests.** `pnpm --filter @loto/web test` runs the M1 tooling tests and Vitest (`vitest.config.ts` mirrors the app's TanStack alias). `pnpm --filter @loto/web test:e2e` starts its own disposable stack (`e2e/stack.ts`) and drives the installed Google Chrome; fixtures are `page.e2e.tsx`/`layout.e2e.tsx` files compiled only with `LOTO_WEB_E2E_FIXTURES=1` into `.next-e2e`. Test pins: Vitest `5.0.3` (Vite `8.3.2`), jsdom `30.1.1`, Testing Library React `16.3.3` / DOM `10.4.2`, Playwright Test `1.64.0`.
