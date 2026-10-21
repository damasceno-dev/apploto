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

**Precondition:** **Shared M1 complete** — base tsconfig, root lint/format, and the `@loto/api` / `@loto/core` skeletons. At baseline `f424deb`, Shared M1 and Shared M2 source packages are delivered; core remains `export {}` pending Shared M3.

**Key behaviors:**

- **Versions are verified at implementation time** — Expo SDK and the libraries it pins (React Native, Expo Router, NativeWind, and their Tailwind dependency) at the current stable releases, recorded in the phase notes. Expo's version pins win over newer standalone releases.
- **Development builds, not Expo Go,** unless the phase proves Expo Go covers every native module the plan needs (secure store, crypto); record the decision.
- **Monorepo resolution stays local.** Prefer Expo's built-in monorepo support. If pnpm's symlinked layout needs a workaround, solve it inside `mobile/` first; a root `.npmrc` change (such as a hoisting rule) is acceptable only as a recorded, concrete need.
- **The subpath export must resolve.** Metro must honor the `exports` map, so `@loto/api/queries` resolves as its own entry exactly as on web.
- **Configuration is injected by the app.** `EXPO_PUBLIC_API_BASE_URL` is read in one mobile config module; `shared/` never reads `EXPO_PUBLIC_*`.

### M1 criterion coverage and verification plan (2026-10-21)

Implementation checkboxes are implementer checks, not independent acceptance. Baseline: clean `f424deb`, Node 26.6.0 / pnpm 10.34.6.

| Criteria | Intended artifacts / behavior | Meaningful verification |
|---|---|---|
| 1.1–1.3 | SDK-compatible package/config, strict multi-base tsconfig, scheme and temporary IDs | Frozen install, Expo config/install check, typecheck |
| 1.4–1.5 | Router shell + not-found, environment example, CNG ignores | Both native bundles, route/runtime inspection when native SDKs permit |
| 2.1–2.2 | Expo Metro source resolution of all three entries + app-owned React instance | Both bundles, resolver negative tests and module identity checks; native runtime remains separate |
| 2.3 | iOS simulator and Android emulator | Actual launch + logs, never inferred from a bundle |
| 3.1–3.2 | Expo/hooks lint, export exceptions, raw-color gate | Positive/negative TSX/native-style/CSS fixtures, actual lint CLI |
| 3.3–3.4 | NativeWind, cleared palette, cn + CVA, root format | Metro CSS transform, bundle/typecheck, root fan-out |
| 4.1–4.2 | Reversible Hermes-only Intl probe, code points and spec comparisons | Actual Hermes execution on each OS + measured notes; blocked platforms stay unchecked |
| 5.1–5.2 | App instructions and exact evidence/limits | Final isolated install, workspace checks, native evidence audit |

### Phase 1 — App scaffold

- [x] **1.1** Add `mobile/package.json`: name `@loto/mobile`, `"private": true`, `"type": "module"` if the verified Expo toolchain supports it (otherwise record why not), `"main": "expo-router/entry"`, scripts `start`, `ios`, `android`, `typecheck` (`tsc --noEmit`), `lint`, `format`, `format:check`; Expo, React Native, and Expo Router at the verified versions; `@loto/api` and `@loto/core` as `workspace:*` dependencies.
- [x] **1.2** Add `mobile/tsconfig.json` extending `../tsconfig.base.json` and Expo's base, with the `@/*` → `src/*` alias. Override base settings only where Expo requires it and record each override.
- [x] **1.3** Add `mobile/app.config.ts`: app name Lotero, slug, URL scheme for deep links, `userInterfaceStyle: "automatic"` (both themes), typed routes, and placeholder iOS bundle / Android package identifiers (final values decided with Mobile M12).
- [x] **1.4** Add the Expo Router skeleton under `mobile/src/app/`: root `_layout.tsx`, a placeholder `index.tsx`, and `+not-found.tsx`. Route groups are created by the milestones that fill them — no empty scaffolding.
- [x] **1.5** Extend the root `.gitignore` with the Expo block (`.expo/`, `ios/` and `android/` while the project stays on prebuild/continuous native generation, `*.jks`, `*.p8`, `*.p12`, `*.mobileprovision`, `.env*.local`) and add `mobile/.env.example`.

### Phase 2 — Metro and workspace resolution

- [x] **2.1** Configure `mobile/metro.config.js` from Expo's default config and confirm it resolves workspace packages through pnpm symlinks; record any watch-folder or resolver setting that was required and why.
- [ ] **2.2** Prove the export split: a placeholder screen imports from `@loto/api`, `@loto/api/queries`, and `@loto/core` and renders through them; a deliberately wrong subpath fails to resolve.
- [ ] **2.3** Run the app on an iOS simulator and an Android emulator from the repo root workflow; record the local API address each platform will use (the iOS simulator reaches `localhost`; the Android emulator needs the host alias) in `mobile/.env.example`.

### Phase 3 — Lint, format, and styling baseline

- [x] **3.1** Add `mobile/eslint.config.mjs` layering Expo's flat config and the React Hooks rules over the root config, plus the named-exports rule with Expo Router's default-export exemptions.
- [x] **3.2** Add the **raw-color gate** with the same rule set as Web M1 2.2 (hex, `rgb()`, `hsl()`, named colors, and arbitrary color classes), with a single exemption path for the future token-adapter module. Prove it with a throwaway violation.
- [x] **3.3** Install NativeWind at the verified version with its Tailwind config, default color palette cleared, plus `tailwind-merge` and the same variant helper web chose, so class composition reads the same on both platforms. Add the `cn()` helper in `src/lib/`.
- [x] **3.4** Confirm the root Prettier config applies unchanged to `mobile/`.

### Phase 4 — Hermes `Intl` probe (input to Shared M3)

- [ ] **4.1** On both platforms under Hermes, record the output of: BRL currency formatting for positive, negative, and zero values; `pt-BR` grouping and the decimal comma; `dd/MM/yyyy` and `HH:mm` date/time formatting; and the minus sign used for negatives. Compare each against `design/specs/formatting-ptbr.md`.
- [ ] **4.2** Write the findings into this milestone's notes and point Shared M3 to them, so its `Intl`-versus-hand-rolled decision rests on measured behavior. No formatter is written here.

### Phase 5 — Docs sync + close-out

- [x] **5.1** Update `mobile/AGENTS.md` with the verified version pins, the development-build decision, the Metro/pnpm resolution notes, and any tsconfig overrides.
- [ ] **5.2** Done criteria: fresh clone → install, typecheck, lint, and format check green; the placeholder screen runs on iOS and Android and renders through all three package entries; a raw color literal fails lint; the Hermes findings are recorded; no token, API call, or auth code exists yet.


### M1 execution evidence and exact remaining prerequisites (2026-10-21)

**Mobile M1 is incomplete.** Eleven selected implementation criteria are checked; 2.2, 2.3, 4.1, 4.2 and 5.2 remain unchecked because native execution/measurement is still missing. Independent review round 3 accepted 1.1–1.5, 2.1, 3.1–3.4 and 5.1 and confirmed every finding fixed. All ready non-native criteria pass; the five native criteria remain blocked. No Node/browser output is presented as Hermes evidence.

| Criteria | Implemented and checked | Missing evidence / exact prerequisite |
|---|---|---|
| 1.1–1.5 | ESM Expo/Router scaffold, strict multi-base TS, scheme/temporary IDs, automatic appearance, typed routes, native-only platforms, ignored CNG output, environment example | Independent review; no store identifiers/signing claimed |
| 2.1 | Expo default monorepo resolution + narrowly scoped app peer resolver and export guard | Native runtime confirmation follows 2.3 |
| 2.2 | iOS/Android production and development bundles compile all public imports; Node resolves all three entries to committed TS; real Metro rejects private iOS and nonexistent Android subpaths | Placeholder rendering and disabled-query context must still execute on both native platforms |
| 2.3 | Actual native prebuild/build/launch workflows attempted locally | iOS: missing Pods plus outdated Xcode; Android: missing SDK/adb/JDK/emulator, detailed below |
| 3.1–3.4 | Expo/hooks/root lint; exact Router export exceptions; CSS/TS/native raw-color tests; NativeWind and empty resolved palette; CVA/cn; root formatting | Independent review; visual/native styling execution not inferred from bundles |
| 4.1–4.2 | Dev-only, user-triggered, Hermes-guarded Intl probe compiles for both targets; rerun procedure below | **No measured iOS or Android output yet.** Record real engine/OS/output/spec comparisons after native toolchains are available |
| 5.1 | Guide updated with exact pins, official sources, config exceptions, build mode/resolution and limitations | Independent review |
| 5.2 | Frozen clean installation and workspace checks passed; all ready artifacts delivered | Native runtime + Hermes evidence and acceptance of those native criteria remain outstanding |

**Bundle and peer evidence.** The initial Android map reproduced two QueryClientProvider modules, from Shared's React 19.3.0 tool peer and the app's React 19.2.3 peer. The local resolver fixes origins only for React/JSX, React DOM and TanStack Query. Final maps in both targets contain **one React 19.2.3 runtime and one app-owned QueryClientProvider**; `tooling/check-bundles.mjs` checks those facts and both API source entries. No blanket hoist/override or Shared package edit was used. Core is an empty runtime namespace, not a fabricated export; optimization can omit its empty source mapping. The technical QueryProof puts the generated hook under the app-owned provider with `enabled: false` and retries disabled; no configuration/request is made. Its actual native context execution remains unverified.

**Actual native attempts.** `expo run:ios --no-install --no-bundler --device generic` generated ignored CNG sources, then xcodebuild exited **65** at `[CP] Check Pods Manifest.lock` (Pods not installed). Installed Xcode is **15.2**, with iOS/simulator SDK **17.2**; React Native 0.86.3's installed `scripts/cocoapods/helpers.rb` declares minimum Xcode **16.1**. CocoaPods is not on PATH. Available simulator devices include iPhone 15/iOS 17.2, all discovered shutdown; no simulator data was reset. `expo run:android --no-install --no-bundler` generated ignored Android sources, then failed with **`spawn adb ENOENT`** and missing default Android SDK. SDK/emulator/adb were absent from the checked locations; `java -version` reported no Java runtime. These are native build/runtime blockers, not bundle failures.

Remaining setup: install/select SDK-compatible Xcode (at least the package's minimum) and CocoaPods, install Pods and run the iOS simulator build; install a compatible JDK plus Android SDK/platform/build tools and emulator system image, configure `ANDROID_HOME`, then launch Android. No licenses, SDK installation, cloud account/build, signing identity or device reset was performed in this assignment. Once setup is available, run `pnpm --filter @loto/mobile ios` and `pnpm --filter @loto/mobile android`, inspect placeholder/idle query proof/Metro logs, then measure below.

**Hermes Intl input for Shared M3 — pending measurements, not findings.** In each local development build, press **Medir Intl no Hermes**. `src/probes/intl-probe.tsx` refuses web/non-Hermes execution; capture its selectable JSON outside the repository. Record platform/OS, Hermes runtime properties, runtime Expo SDK (null if unavailable), bundled Expo dependency-version metadata, default device Intl locale/timeZone, and explicitly configured test Intl options, then put concise actual readings in this section. The fixed instant is `2026-07-08T17:30:00.000Z`, locale `pt-BR`, zone `America/Sao_Paulo`. The probe checks positive/negative/zero BRL, grouping/decimal comma, explicit signed values, date/time/datetime, and both actual/expected Unicode code points. It compares without normalization to formatting-ptbr's NBSP U+00A0, minus U+2212 and exact date/time strings. **Neither platform has been measured**; Shared M3 must not infer a formatter strategy from these expected values or from a compiled Hermes bytecode file. Probe/technical UI are app-local and removable; no formatter/business logic was added.

**Commands/results.** `pnpm install` and isolated-copy `pnpm install --frozen-lockfile` passed without peer/engine relaxation. `pnpm --filter @loto/mobile exec expo install --check` passed online. Root `pnpm -r typecheck`, `pnpm -r lint`, `pnpm format:check` and `pnpm -r test` passed with both apps included (103 Shared + 6 Web + 7 Mobile tests). `expo config --type public` validated SDK/config. `expo export --platform all --source-maps --output-dir dist/proof` passed for iOS/Android here and in the clean copy; `node tooling/check-bundles.mjs dist/proof` passed there. `expo export --platform all --dev --output-dir dist/dev-proof` also passed, compiling the development-only Intl probe. These are **bundle-only** results, not native builds/runtime/engine evidence.

Actual lint CLI fixtures rejected native StyleSheet colors, Text arbitrary color classes, JS config palettes, CSS variable fallback colors and default re-exports; ordinary copy/URLs/fragments passed. Node's exports map and real Metro negatives rejected private/nonexistent Shared imports. Disposable fixtures were removed. NativeWind's resolved `theme.colors` is `{}`. Root format config is unchanged; explicit root ignore paths skip generated native/build output. Install retains root-pinned ESLint deprecation and pnpm's ignored esbuild/unrs-resolver scripts; checks passed without enabling them. Metro inherited NO_COLOR/FORCE_COLOR warnings; the dev export exited 0 after Expo stopped its own NativeWind watcher children. These warnings were not suppressed with root policy changes.

`generate:check` passed for 287 committed generated files; Shared stayed unchanged. `git diff --check` passed. Doc-sync still fails at the inherited canonical **v56** / design **v55** semantic-review mismatch; this task did not edit markers or the canonical group. Combined integration cannot be accepted while that gate is red. App-owned files/guides/milestones plus root ignore additions and lockfile are the only changed areas; no server/security checkout, Shared implementation, design token or future M2/M3 scope was changed. No staging/commit/push was performed. Lockfile impact: new Web/Mobile importers and framework graphs; the existing Shared API Vite/Vitest resolution also gains the optional Terser peer (`5.51.2`) now present through app tooling. Shared direct dependency specs/pins and source are unchanged; its 103 tests and generation drift check passed with this graph.

### Independent review round 1 — corrections independently confirmed

| Finding | Implementer correction and meaningful verification | Acceptance |
|---|---|---|
| P2-2 | Same explicit-color and CSS at-rule gate fix as Web. Exported palette literals, imported style/class consumers and `@apply` now fail actual CLI checks, including TS/CSS outside src; only the exact token-adapter file is exempt, not its folder; ordinary labels/URLs/fragments and dimensions pass after the round-2 refinement below. | Independently confirmed fixed in re-review |
| P3-1 | Official `expo customize tsconfig.json` runs before `tsc` without starting Metro. A fresh copy had no generated route file: direct tsc failed the compile-time Href sentinel; normal typecheck generated routes and passed; a real nonexistent Link href then failed TS2322. Generated declarations stay ignored and no application route was invented for the test. | Independently confirmed fixed in re-review |
| P3-2 | Removed hard-coded SDK/package fallback values. Runtime Constants supplies SDK or null; `expo/package.json` supplies labelled dependency metadata; `Intl.DateTimeFormat().resolvedOptions()` records the device's own locale/timeZone separately from test configuration. Both target bundles compile; no native measurement is claimed. | Independently confirmed fixed in re-review |
| P4 | Duplicate diagnostics suppressed, dependency keys sorted, tablet-support flag deferred to M12. | Independently confirmed fixed in re-review |

These are code/tooling corrections, not replacement native evidence. Criteria 2.2 render/context, 2.3, 4.1, 4.2 and 5.2 remain unchecked until both native toolchains are available and the actual launches/Intl readings are independently verified. SDK/JDK/emulator/Pods/Xcode prerequisites and inherited doc-sync v56/v55 remain exactly as recorded above. The round uses the same assignment and owning files, without a new delivery report, staging or commits.

### Independent re-review — round-2 P3 accepted in round 3

The reviewer confirmed all round-1 findings fixed, then found hex-like reference numbers rejected inside ordinary copy. Outside styles/arbitrary classes, the gate now requires the entire trimmed string to be one explicit hex color or one color function. Style contexts and CSS retain substring detection. Both app suites include passing `Bolão #123 confirmado` and `aria-label="Excluir lançamento #abc"` fixtures, plus template/function-copy cases; standalone/trimmed palettes and composite style colors still fail. Named colors outside styles and imported values retain the documented trade-off: forbidden explicit palette literals fail at their definitions without import evaluation.

Round-2 validation: root `pnpm typecheck`, `pnpm lint`, `pnpm format:check` and `pnpm test` passed (103 Shared + 6 Web + 7 Mobile tests). Actual ESLint CLI checks using app configuration accepted both copy examples and rejected standalone/trimmed hex, color functions, composite style values and arbitrary color classes in both apps. `git diff --check` passed. Runtime/build evidence above was independently rechecked before this tooling-only correction; unchanged browser/native bundles were not rerun. The same reviewer independently confirmed this correction in round 3. No native evidence, dependency pins, lockfile or inherited doc-sync markers changed in this round.

### Round-3 acceptance and commit readiness

The same reviewer verified the correction in a fresh isolated copy: frozen install without unmet peers; root typecheck/lint/format and all 116 tests; Web production build with app-owned bundle identity and missing-URL failure; iOS/Android exports with bundle assertions; Shared generation matching all 287 files; clean diff checks and no personal paths. No native execution or Hermes readings were substituted by these checks.

All findings are fixed. The suggested broader detection of mixed values under custom style-key names is optional and non-blocking; whole-value detection outside recognized styles is the accepted trade-off. This app foundation is ready to commit with native criteria explicitly open. Combined integration remains blocked by inherited spec v56/design v55 doc-sync reconciliation; it is not implied by app acceptance. No staging or commit was performed.

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

> **Shared M2 handoff (2026-10-17, S06; Shared M2 awaits independent review).** Mobile configures `@loto/api` once with `configureApiClient({ baseUrl, getAccessToken })`; `fetch` defaults to the runtime's global and may be injected.
> - **Token provider:** it is called on every request with `{ method, path, authTier }` (`anonymous`, `identity` or `branch`, generated from the contract) and reads the matching secure-store token. Returning `null` sends no `Authorization` header.
> - **Per-call headers:** ordinary headers may be passed in options. A caller value that conflicts with a generated financial header, and a caller `Authorization`, is refused before dispatch.
> - **Failures** reject with `ApiError`; a failing token provider becomes kind `token-provider`. `isOutcomeUncertain(error)` identifies writes whose outcome is unknown.
> - **Shared performs no retry, renewal or key minting.** The connectivity "retry" above is an app action, and a retried write must reuse its frozen `Idempotency-Key` and payload; generated hooks set no `retry`, so keep mutation retries at 0.
> - **Not tested yet:** Shared has not run this code under Hermes/Metro. Mobile M1 2.2 still proves that the `@loto/api` / `@loto/api/queries` subpaths resolve.

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

**Scope and gates:** as Web M5 — the mobile-first flows of the catalog. This includes gap AA on Close day and Fix & resubmit (retained-retired-product edit/preview/save recovery only; server DailyClose/Product, scheduling pending M7.7 close-out triage); other screen behavior and historical reads proceed. The catalog’s gap → screen map remains authoritative. **Mobile-specific:** the "once per device and branch-local day" terminal choice is stored on the device; Close day's count entry is tuned for numeric keypads and survives the app being backgrounded mid-count.

## Milestone 6 — Transactions Ledger

**Scope and gates:** as Web M6. **Mobile-specific:** fast entry optimized for one-handed numeric input; the Transactions list uses virtualized, paginated scrolling.

## Milestone 7 — Manager Work Queue & Approvals

**Scope and gates:** as Web M7. **Mobile-specific:** the comparison view on Daily-close approval adapts opening → closing → variance to a narrow layout per its blueprint.

## Milestone 8 — Reports & Month Lock

**Scope and gates:** as Web M8. **Mobile-specific:** report tables adapt to small screens (priority columns, row detail sheets) per the blueprints, with the same data and drill-downs.

## Milestone 9 — Time & Self-Service

**Scope and gates:** as Web M9.

## Milestone 10 — Admin & Configuration

**Scope and gates:** as Web M10. Products inherits gap AA only for deactivation warning/recovery (server DailyClose/Product; scheduling pending M7.7 close-out triage); ordinary Product CRUD and reads proceed.

## Milestone 11 — Account & Onboarding

**Scope and gates:** as Web M11, minus the internal **Plataforma** area unless design 0.4.6 places it on mobile. **Mobile-specific:** Continuar com Google through the native sign-in flow; invitation and password-reset links open the app through the URL scheme or universal/app links when installed.

---

## Milestone 12 — Store Release

**Goal:** Ship the app to the stores: build profiles per environment aligned with server M9, final bundle identifiers, signing, store listings in pt-BR, the over-the-air update policy (what may ship as an update versus a store release), crash reporting, and a device test matrix for the spine flows.

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires.
