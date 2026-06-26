# Mobile Agent Notes

## Stack direction

- Use Expo
- Use Expo Router
- Use NativeWind

## Integration guidance

- Reuse `shared/api` for the same API contract, raw functions, and TanStack Query helpers used by the web app
- Consume `shared/core` (day-one) for BRL/CPF/date formatters and pt-BR enum-label maps shared with web
- Keep upload, media, and device-specific adaptation at the mobile app boundary
- Consume design guidance from `design` tokens, specs, assets, and references

## UI guidance

- Keep mobile UI components inside `mobile`
- Do not create a shared runtime `ui-mobile` package yet
- Do not force web-oriented components into React Native
- Build mobile components using the same design tokens and specs, but let mobile own its implementation
