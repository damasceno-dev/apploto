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
