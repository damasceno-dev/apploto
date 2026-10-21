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

## M1 implementation baseline (2026-10-21; non-native criteria accepted, native evidence blocked)

- Stable SDK line selected for NativeWind's explicit compatibility: Expo `57.0.31`, React / React DOM `19.2.3`, React Native `0.86.3`, Router `57.0.29`, NativeWind `4.2.7`, CSS interop `0.2.7`, Tailwind `3.4.19`, Reanimated `4.5.1`, Worklets `0.10.1`, safe-area-context `5.7.0`, screens `4.26.2`. Expo 58 was available, but was not used to infer NativeWind 4 compatibility. Full exact dependency/tooling pins are in `package.json` and the lockfile.
- Sources: [Expo SDK 57 release](https://expo.dev/changelog/sdk-57), [SDK 57 reference](https://docs.expo.dev/versions/v57.0.0/), [NativeWind 4 installation and SDK 57 support](https://www.nativewind.dev/docs/getting-started/installation), [Expo monorepos](https://docs.expo.dev/guides/monorepos/), [Metro custom resolution](https://metrobundler.dev/docs/configuration/#resolverequest), [Expo Router installation](https://docs.expo.dev/router/installation/), [development builds](https://docs.expo.dev/develop/development-builds/introduction/). The selected Expo package's `bundledNativeModules.json` and npm registry metadata supplied the exact compatibility ranges; `expo install --check` passed online.
- Use local development builds (`pnpm --filter @loto/mobile ios|android`, then `start`), with CNG-generated native directories ignored. This gives the SDK-compatible native modules and Hermes runtime without relying on Expo Go's changing availability/accounts. No cloud build or signing setup was performed. Secure store/crypto remain M2 dependencies, not M1 session implementation.
- ESM application with explicit `.cjs` exceptions for Babel, Metro and Tailwind: their documented synchronous Node configuration APIs use `require` / `module.exports`. `app.config.ts` is ESM. Hermes is the SDK default (the SDK no longer exposes a `jsEngine` app-config property); generated native properties were inspected, but this is not engine execution evidence.
- tsconfig uses supported multiple inheritance: Expo first, root second. Root strict/module/ES2022 settings win; Expo supplies JSX, native conditions and DOM type declarations needed for the shared fetch/Headers/AbortSignal signatures. `allowJs: false`, app-only types and `@/*` are local refinements. DOM type declarations are not a dependency on a browser runtime.
- Metro starts from Expo defaults, without custom watch folders/node paths/hoisting. NativeWind's Babel-inserted `react-native-css-interop/jsx-runtime` required a direct, exact app dependency under isolated pnpm installation. React DOM/types are pinned locally to the SDK family to satisfy Router's optional peers without pulling web's React family.
- A reproduced Android bundle contained two TanStack contexts (Shared tooling peer versus app peer). `tooling/workspace-resolver.cjs` routes React, its JSX entries, React DOM and TanStack Query through the app origin. It also rejects Shared subpaths absent from the actual package export maps, because Metro's normal fallback can resolve private source paths. Final source maps prove one native React runtime and one app-owned query context on each platform; a disabled query under the app provider is the pending on-device context proof.
- CVA `0.7.1` and clsx `2.1.1` match Web's choice; tailwind-merge `2.6.1` matches Tailwind 3 (Web uses the Tailwind 4-compatible major). No token adapter, theme/gallery or business component exists in M1. Root Prettier is unchanged; scripts explicitly load both root ignore files.
- Expo's lint rules are retained while reusing the root TypeScript plugin identity to avoid duplicate-plugin errors. Default exports are allowed for the current exact Router files/config conventions; other source files use named exports. The empty `@loto/core` namespace has one documented import/namespace exemption, not a fake Shared export. Raw-color enforcement has the same semantics as Web, including source CSS and app config; only `src/theme/token-adapter.ts` is the future TS exemption.
- Rerun bundle evidence: `pnpm --filter @loto/mobile exec expo export --platform all --source-maps --output-dir dist/proof`, then `pnpm --filter @loto/mobile exec node tooling/check-bundles.mjs dist/proof`. This does not replace device execution. Native launch blockers and the Hermes measurement procedure/status live in `docs/milestones.md`.

### M1 review fixes (independently accepted in review round 3)

- P3-1: `typecheck` runs `expo customize tsconfig.json && tsc --noEmit`. This is Expo's [documented CI route-generation command](https://docs.expo.dev/router/reference/typed-routes/#type-generation), not an internal generator or a Metro server. It regenerates ignored `.expo/types/router.d.ts` and `expo-env.d.ts`; the precise app-local `.gitignore` entry prevents the CLI from creating/changing tracked ignore content. The multi-base tsconfig remains intact. A compile-only Href sentinel rejects missing generation; a nonexistent Link href was rejected with TS2322 in a fresh isolated install.
- P3-2: the probe reads `expoSdk` from runtime Constants (null if missing), the installed Expo package version from bundled `expo/package.json` metadata, and default device Intl resolved options (including locale/timeZone). It has no manually maintained SDK/package fallback. Package metadata is not represented as an engine measurement. Neither platform has actual Hermes readings yet.
- P2-2/P4: Outside styles, only an entire trimmed hex/function string or template fails, including exported palettes. Style values and arbitrary classes retain substring checks; CSS at-rules including `@apply` are checked. Named colors stay context-dependent; duplicate diagnostics are suppressed. App-local rule semantics still match Web. Dependency keys are sorted; `supportsTablet` was removed so M1 does not decide tablet support ahead of M12. No dependency version/lockfile changes were needed for these fixes.

- The same reviewer independently confirmed all round-1 fixes. Round-2 P3 adds passing reference-number and aria-label copy fixtures while preserving isolated palette rejection; the same reviewer confirmed the correction in round 3. Mixed composite values outside recognized style contexts remain an accepted, non-blocking trade-off; broad style-key matching was not added.
