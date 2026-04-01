# Web Agent Notes

## Stack direction

- Use Next.js App Router
- Prefer Server Components whenever possible
- Use Suspense and skeletons for loading states
- Use TanStack Query for client-side server state

## Component guidance

- Use composition instead of prop-heavy components
- Prefer reusable components only
- Use Tailwind with merge and variant helpers rather than raw class interpolation
- Prefer named exports over default exports

## API usage

- Server Components call raw functions from `shared/api`
- Client components use the shared query layer from `shared/api`
- Do not add a separate web-only BFF unless there is a concrete auth or cookie need

## Important decisions

- The backend remains the real backend for web and mobile.
- A Next.js-only backend layer is not needed by default.
- A web-only BFF is only justified for browser-specific auth or cookie handling or similar web-only constraints.
