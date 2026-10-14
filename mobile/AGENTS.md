# Mobile Agent Notes

Milestones for this area live in `mobile/docs/milestones.md`.

## Stack direction

- Use Expo
- Use Expo Router
- Use NativeWind
- Package name is `@loto/mobile`; ESM only; the tsconfig extends the root `tsconfig.base.json`
- Consume `@loto/api` and `@loto/core` as source packages compiled by Metro — no build step in `shared/`

## Integration guidance

- Reuse `shared/api` for the same API contract, raw functions, and TanStack Query helpers used by the web app; screens use the hooks from `@loto/api/queries`
- Consume `shared/core` (day-one) for BRL/CPF/date formatters and pt-BR enum-label maps shared with web
- Mobile calls the backend directly — there is no BFF. Tokens live in the platform secure store and reach `@loto/api` through the client's token-provider interface
- Keep upload, media, and device-specific adaptation at the mobile app boundary
- Consume design guidance from `design` tokens, specs, assets, and references
- Business day and time come from `GET /branch/current`, never the device clock
- Errors render `ResponseErrorJson.ErrorMessages` verbatim through the shared `ApiError` normalization

## Design tokens

- Read `design/tokens/*.json` at build time through the NativeWind theme — tokens never pass through `shared/`
- App code uses semantic and domain tokens only: no raw hex/rgb literals and no primitive-token references outside the token-adapter module (enforced by lint)
- Light and dark themes ship together

## UI guidance

- Keep mobile UI components inside `mobile`
- Do not create a shared runtime `ui-mobile` package yet
- Do not force web-oriented components into React Native
- Build mobile components using the same design tokens and component contract (`design/specs/components/`), but let mobile own its implementation
- Mobile clones web: the screen set and its semantics are identical (`design/docs/screens.md`); interaction patterns are platform-appropriate, and dense screens adapt to small screens instead of being redesigned
