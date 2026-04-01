# Shared Agent Notes

## Purpose

Cross-platform runtime TypeScript code shared by `web` and `mobile`.

## Initial structure

- `shared/api`
- `shared/core` later if needed

## `shared/api` contains

- Orval config
- generated request and response types
- raw API functions
- API client setup
- TanStack Query keys, options, and hooks

## `shared/core` contains only pure TS code

- constants
- validation schemas
- formatters
- utilities
- shared non-UI helpers

## Important decisions

- Backend owns the API contract.
- `shared/api` starts with the generated client, raw functions, and TanStack Query helpers.
- `shared/core` should be added only when pure TS helpers begin repeating.
- Shared packages should not read `NEXT_PUBLIC_*` or `EXPO_PUBLIC_*` directly.
- `server.Exceptions` stays backend-only; consumers rely on the serialized API error contract instead.
- Required C# fields must remain required in generated TS contracts.

## Rules

- Do not put React UI components in `shared`
- Do not put Tailwind or NativeWind code in `shared`
- Do not put Next-specific or Expo-specific APIs in `shared`
- Keep raw API functions available for Next Server Components
- Keep TanStack Query wrappers available for web client components and mobile screens
- Shared runtime code is linked via `pnpm workspaces`, not via Turborepo
