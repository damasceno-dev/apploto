# Lotero — Mobile Milestones

> **Status:** Active
> **Draft started at** 2026-10-14
> **Approach:** Expo + Expo Router + NativeWind over the backend-owned contract — a port of the web screen set, not a redesign

This file is the general milestone list for the `mobile/` area. Detailed phases with checkboxes are written just-in-time when a milestone starts, following the `server/docs/milestones.md` convention, and executed phase by phase. Cross-area docs reference these milestones as **Mobile M1**, **Mobile M2**, …

**Mobile and web share one numbering.** `web/docs/milestones.md` uses the same milestone numbers for the same scope, so **Mobile M5 ∥ Web M5** always means the same feature area on both platforms. Web proves each foundation pattern first (M1–M3); from M4 on, each same-numbered pair runs in lockstep against one screen blueprint, and its detailed phases are written together.

### Standing rules

- **Mobile clones web.** The screen set and its semantics come from the one catalog (`design/docs/screens.md`); interaction patterns are platform-appropriate (sheets, native pickers, bottom navigation if the blueprint chooses it), and dense report tables adapt to small screens instead of being redesigned. A screen that diverges semantically from its web counterpart is a catalog change, not a mobile decision.
- **The backend owns behavior.** Same rule as web: render what the API returns, call what the catalog names, never recompute money outcomes or permissions. Money is display-only; unavoidable arithmetic uses the integer-cents helpers in `@loto/core`.
- **The gap → screen map gates mobile exactly as it gates web.** A gated screen or feature is not built until its gap is resolved and the catalog entry re-synced.
- **Design system only.** Components follow `design/specs/components/`; styling uses semantic and domain tokens through the NativeWind theme. Raw color literals and primitive-token references fail lint everywhere except the token-adapter module. Light and dark ship together from Mobile M3 on.
- **No BFF; tokens in the secure store.** Mobile calls the API directly and hands tokens to `@loto/api` through the client core's token-provider interface. Tokens never go to unencrypted storage or logs.
- **Verbatim errors** through the shared `ApiError` normalization; **branch clock** from `GET /branch/current`, never the device clock.
- **Online-only MVP unless design 0.4.2 decides otherwise.** No connection is a named state with retry, not an offline mode; unsaved input is still kept on the device and restored.
- **Screen definition of done** (every feature milestone, every screen): all catalog states, pt-BR copy and `@loto/core` formatting, screen-reader labels and focus order per the design 0.4.2 accessibility patterns, touch targets per the token guidance, both themes, verified on iOS and Android, and an end-to-end test of the screen's **primary job** against a locally running API.
- **Impeccable is a standing refinement step**, run with the token vocabulary loaded; out-of-system suggestions go back to design.
- **Standing gates:** `pnpm -r typecheck`, `pnpm -r lint`, and `pnpm format:check` stay green on every commit touching `mobile/`, and the app bundles for both platforms. Unit tests arrive with Mobile M2; the end-to-end runner is chosen there.

### Area dependency map

- **Shared M1** → Mobile M1, which may run in parallel with Web M1.
- **Shared M2** + the patterns locked in **Web M2** → Mobile M2. **Shared M3** → Mobile M4. Mobile M1 feeds Shared M3 the Hermes `Intl` findings it needs to choose between `Intl` and hand-rolled formatters.
- **Design M2 + M3** and the adapter pattern proven in **Web M3** → Mobile M3.
- **Design M4** blueprints → each feature milestone, Mobile M4–M11, shared with the same-numbered web milestone.
- **Server gates per feature** are the same as web: M7.7 Phase 5 (A, H, L, S), server M8 (C, N, U, V) for Mobile M11, server M9 alongside Mobile M12.

### Milestone overview

| #       | Name                                 | Main precondition                                    | Output                                                                  |
|---------|--------------------------------------|------------------------------------------------------|-------------------------------------------------------------------------|
| **M1**  | Expo Scaffold                        | Shared M1                                            | `@loto/mobile` in the workspace, Metro + pnpm, lint/style baseline, Hermes probe |
| **M2**  | Session & Data Plumbing              | Shared M2, Web M2, design 0.4.2                      | Secure-store session, renewal, query layer, cross-screen pattern plumbing |
| **M3**  | Design System Implementation         | Design M2 + M3, Web M3                               | NativeWind token theme, themes, primitive components, in-app gallery     |
| **M4**  | Entry & Navigation Shell             | Mobile M2 + M3, Shared M3, Design M4 (shell)         | Login, Branch picker, Gestão ↔ Meu turno shell, deep-link gate           |
| **M5**  | Operator Day                         | Mobile M4, Design M4 (day close)                     | Operator Day Cockpit, Open day, Close day, Fix & resubmit                |
| **M6**  | Transactions Ledger                  | Mobile M5, Design M4 (ledger)                        | Create, Edit, list, installment plan, client statement, payment          |
| **M7**  | Manager Work Queue & Approvals       | Mobile M5 + M6, Design M4 (queue)                    | Manager Work Queue, Daily-close approval                                 |
| **M8**  | Reports & Month Lock                 | Mobile M7, Design M4 (reports)                       | Six management reports, adapted for small screens                        |
| **M9**  | Time & Self-Service                  | Mobile M4, Design M4 (time)                          | Clock in / out, Time-entry management, both self-service summaries       |
| **M10** | Admin & Configuration                | Mobile M4, Design M4 (admin)                         | The nine admin/configuration screens                                     |
| **M11** | Account & Onboarding                 | Server M8, design 0.4.6                              | The auth & account screen set, guided employee flows, link requests      |
| **M12** | Store Release                        | Server M9                                            | Build profiles, store submission, update policy, crash reporting         |

Feature scope, catalog gates, and screen behavior for M4–M11 are defined once, in the same-numbered web milestone; the entries below add only what is specific to mobile.

---

## Milestone 1 — Expo Scaffold

**Goal:** Stand up `@loto/mobile` inside the existing pnpm workspace — Expo with Expo Router, TypeScript on the shared base config, Metro resolving the source packages through pnpm's symlinked layout, the NativeWind and lint baseline including the raw-color gate — and run it on both platforms, recording the Hermes `Intl` behavior that Shared M3 depends on.

**Scope boundary:** Tooling, configuration, and an app skeleton only. No auth, no API calls, no tokens (Mobile M3), no screens. Touches nothing under `server/`, `design/`, `shared/`, or `infra/`; root files change only where a phase names them.

**Precondition:** **Shared M1 complete** — base tsconfig, root lint/format, and the `@loto/api` / `@loto/core` skeletons. As of this draft only Shared M1 Phase 1 has landed.

**Key behaviors:**

- **Versions are verified at implementation time** — Expo SDK and the libraries it pins (React Native, Expo Router, NativeWind, and their Tailwind dependency) at the current stable releases, recorded in the phase notes. Expo's version pins win over newer standalone releases.
- **Development builds, not Expo Go,** unless the phase proves Expo Go covers every native module the plan needs (secure store, crypto); record the decision.
- **Monorepo resolution stays local.** Prefer Expo's built-in monorepo support. If pnpm's symlinked layout needs a workaround, solve it inside `mobile/` first; a root `.npmrc` change (such as a hoisting rule) is acceptable only as a recorded, concrete need.
- **The subpath export must resolve.** Metro must honor the `exports` map, so `@loto/api/queries` resolves as its own entry exactly as on web.
- **Configuration is injected by the app.** `EXPO_PUBLIC_API_BASE_URL` is read in one mobile config module; `shared/` never reads `EXPO_PUBLIC_*`.

### Phase 1 — App scaffold

- [ ] **1.1** Add `mobile/package.json`: name `@loto/mobile`, `"private": true`, `"type": "module"` if the verified Expo toolchain supports it (otherwise record why not), `"main": "expo-router/entry"`, scripts `start`, `ios`, `android`, `typecheck` (`tsc --noEmit`), `lint`, `format`, `format:check`; Expo, React Native, and Expo Router at the verified versions; `@loto/api` and `@loto/core` as `workspace:*` dependencies.
- [ ] **1.2** Add `mobile/tsconfig.json` extending `../tsconfig.base.json` and Expo's base, with the `@/*` → `src/*` alias. Override base settings only where Expo requires it and record each override.
- [ ] **1.3** Add `mobile/app.config.ts`: app name Lotero, slug, URL scheme for deep links, `userInterfaceStyle: "automatic"` (both themes), typed routes, and placeholder iOS bundle / Android package identifiers (final values decided with Mobile M12).
- [ ] **1.4** Add the Expo Router skeleton under `mobile/src/app/`: root `_layout.tsx`, a placeholder `index.tsx`, and `+not-found.tsx`. Route groups are created by the milestones that fill them — no empty scaffolding.
- [ ] **1.5** Extend the root `.gitignore` with the Expo block (`.expo/`, `ios/` and `android/` while the project stays on prebuild/continuous native generation, `*.jks`, `*.p8`, `*.p12`, `*.mobileprovision`, `.env*.local`) and add `mobile/.env.example`.

### Phase 2 — Metro and workspace resolution

- [ ] **2.1** Configure `mobile/metro.config.js` from Expo's default config and confirm it resolves workspace packages through pnpm symlinks; record any watch-folder or resolver setting that was required and why.
- [ ] **2.2** Prove the export split: a placeholder screen imports from `@loto/api`, `@loto/api/queries`, and `@loto/core` and renders through them; a deliberately wrong subpath fails to resolve.
- [ ] **2.3** Run the app on an iOS simulator and an Android emulator from the repo root workflow; record the local API address each platform will use (the iOS simulator reaches `localhost`; the Android emulator needs the host alias) in `mobile/.env.example`.

### Phase 3 — Lint, format, and styling baseline

- [ ] **3.1** Add `mobile/eslint.config.mjs` layering Expo's flat config and the React Hooks rules over the root config, plus the named-exports rule with Expo Router's default-export exemptions.
- [ ] **3.2** Add the **raw-color gate** with the same rule set as Web M1 2.2 (hex, `rgb()`, `hsl()`, named colors, and arbitrary color classes), with a single exemption path for the future token-adapter module. Prove it with a throwaway violation.
- [ ] **3.3** Install NativeWind at the verified version with its Tailwind config, default color palette cleared, plus `tailwind-merge` and the same variant helper web chose, so class composition reads the same on both platforms. Add the `cn()` helper in `src/lib/`.
- [ ] **3.4** Confirm the root Prettier config applies unchanged to `mobile/`.

### Phase 4 — Hermes `Intl` probe (input to Shared M3)

- [ ] **4.1** On both platforms under Hermes, record the output of: BRL currency formatting for positive, negative, and zero values; `pt-BR` grouping and the decimal comma; `dd/MM/yyyy` and `HH:mm` date/time formatting; and the minus sign used for negatives. Compare each against `design/specs/formatting-ptbr.md`.
- [ ] **4.2** Write the findings into this milestone's notes and point Shared M3 to them, so its `Intl`-versus-hand-rolled decision rests on measured behavior. No formatter is written here.

### Phase 5 — Docs sync + close-out

- [ ] **5.1** Update `mobile/AGENTS.md` with the verified version pins, the development-build decision, the Metro/pnpm resolution notes, and any tsconfig overrides.
- [ ] **5.2** Done criteria: fresh clone → install, typecheck, lint, and format check green; the placeholder screen runs on iOS and Android and renders through all three package entries; a raw color literal fails lint; the Hermes findings are recorded; no token, API call, or auth code exists yet.

---

## Milestone 2 — Session & Data Plumbing

**Goal:** Wire the app to the API end to end with the same session semantics web locked in Web M2 — two-stage tokens, single-flight renewal, the session gate, the branch clock, and the plumbing behind the design 0.4.2 cross-screen patterns — with no product screens yet.

**Mobile-specific key behaviors:**

- Identity, branch, and refresh tokens in the platform secure store; the token provider reads them for `@loto/api`. Logout clears them locally until server M8 adds revocation.
- Returning to the foreground re-validates the session and re-reads `GET /branch/current`, so a day that rolled over while the app slept does not keep yesterday's defaults.
- `Idempotency-Key` minted with a cryptographically secure UUID source that works under Hermes, persisted per user intent so a retry after an app kill replays instead of duplicating.
- Unsaved form input kept in device storage per branch, user, and route, cleared on save and on branch switch.
- Deep links enter through the same session gate as web: held as intent until the branch session and permission checks pass.
- Connectivity: a network-loss state with retry on every query and mutation, per the design 0.4.2 online-only decision.
- Test baseline: Jest with React Native Testing Library (Expo's supported runner) for the plumbing; choose and record the end-to-end runner (for example Maestro or Detox) with one login → branch session → gate flow against the local stack.

**Precondition:** Mobile M1, Shared M2, Web M2 (the locked session semantics), and design 0.4.2 authored.

---

## Milestone 3 — Design System Implementation

**Goal:** The NativeWind token theme (light + dark), theme switching that follows the system setting with an explicit override, every primitive in `design/specs/components/` implemented natively, and a development-only in-app gallery showing every token and component state in both themes.

**Mobile-specific key behaviors:** the token adapter mirrors web's (same JSON, same reference resolution, semantic and domain tokens only) and is the single lint exemption; primitives use platform controls where the contract allows (native date picker, bottom sheet for Modal/Sheet); touch targets and dynamic type follow the token guidance; the first Impeccable pass runs over the primitives.

**Precondition:** Mobile M1, Design M2 + M3, and Web M3 (adapter pattern proven).

---

## Milestone 4 — Entry & Navigation Shell

**Scope:** as Web M4. **Mobile-specific:** the navigation container and mode switch follow the shell blueprint's small-screen layout; the Android back button and swipe-back respect the catalog's return targets; deep links from the URL scheme pass the same gate.

## Milestone 5 — Operator Day

**Scope and gates:** as Web M5 — the mobile-first flows of the catalog. **Mobile-specific:** the "once per device and branch-local day" terminal choice is stored on the device; Close day's count entry is tuned for numeric keypads and survives the app being backgrounded mid-count.

## Milestone 6 — Transactions Ledger

**Scope and gates:** as Web M6. **Mobile-specific:** fast entry optimized for one-handed numeric input; the Transactions list uses virtualized, paginated scrolling.

## Milestone 7 — Manager Work Queue & Approvals

**Scope and gates:** as Web M7. **Mobile-specific:** the comparison view on Daily-close approval adapts opening → closing → variance to a narrow layout per its blueprint.

## Milestone 8 — Reports & Month Lock

**Scope and gates:** as Web M8. **Mobile-specific:** report tables adapt to small screens (priority columns, row detail sheets) per the blueprints, with the same data and drill-downs.

## Milestone 9 — Time & Self-Service

**Scope and gates:** as Web M9.

## Milestone 10 — Admin & Configuration

**Scope and gates:** as Web M10.

## Milestone 11 — Account & Onboarding

**Scope and gates:** as Web M11, minus the internal **Plataforma** area unless design 0.4.6 places it on mobile. **Mobile-specific:** Continuar com Google through the native sign-in flow; invitation and password-reset links open the app through the URL scheme or universal/app links when installed.

---

## Milestone 12 — Store Release

**Goal:** Ship the app to the stores: build profiles per environment aligned with server M9, final bundle identifiers, signing, store listings in pt-BR, the over-the-air update policy (what may ship as an update versus a store release), crash reporting, and a device test matrix for the spine flows.

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires.
