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
