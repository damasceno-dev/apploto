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
- **Standing gates:** `pnpm -r typecheck`, `pnpm -r lint`, and `pnpm format:check` stay green on every commit touching `mobile/`, and the app bundles for both platforms. Mobile M2 adds Jest/RNTL and native end-to-end verification.

### Area dependency map

- **Shared M1** → Mobile M1, which may run in parallel with Web M1.
- **Shared M2** + the patterns locked in **Web M2** → Mobile M2. **Shared M3** → Mobile M4. Mobile M1 feeds Shared M3 the Hermes `Intl` measurements it needs to choose between `Intl` and hand-rolled formatters.
- **Design M2 + M3** and the adapter pattern proven in **Web M3** → Mobile M3.
- **Design M4** blueprints → each feature milestone, Mobile M4–M11, shared with the same-numbered web milestone.
- **Server gates per feature** are the same as web: M7.7 Phase 5 (A, H, L, S), server M8 (C, N, U, V) for Mobile M11, server M9 alongside Mobile M12.
- **Flow-alignment plan (owner, 2026-10-26):** the same batches as web, all before release — Batch B in M4–M5 plus the Batch B increments of M6 (transaction entry) and M7 (review and approval) (server M9.6), Batch C in M6/M8 (server M9.7), Batch D in M8–M10 (server M9.8, M10). Scope is defined in the same-numbered web milestone; mobile follows the web implementation of each batch.

### Milestone overview

| #       | Name                           | Main precondition                            | Output                                                                           |
| ------- | ------------------------------ | -------------------------------------------- | -------------------------------------------------------------------------------- |
| **M1**  | Expo Scaffold                  | Shared M1                                    | `@loto/mobile` in the workspace, Metro + pnpm, lint/style baseline, Hermes probe |
| **M2**  | Session & Data Plumbing        | Shared M2, Web M2, design 0.4.2              | Secure-store session, renewal, query layer, cross-screen pattern plumbing        |
| **M3**  | Design System Implementation   | Design M2 + M3, Web M3                       | NativeWind token theme, themes, primitive components, in-app gallery             |
| **M4**  | Entry & Navigation Shell       | Mobile M2 + M3, Shared M3, Design M4 (shell) | Login, Branch picker, Gestão ↔ Meu turno shell, deep-link gate                   |
| **M5**  | Operator Day                   | Mobile M4, Design M4 (day close)             | Operator Day Cockpit, Open day, Close day, Fix & resubmit                        |
| **M6**  | Transactions Ledger            | Mobile M5, Design M4 (ledger)                | Create, Edit, list, installment plan, client statement, payment                  |
| **M7**  | Manager Work Queue & Approvals | Mobile M5 + M6, Design M4 (queue)            | Manager Work Queue, Daily-close approval                                         |
| **M8**  | Reports & Month Lock           | Mobile M7, Design M4 (reports)               | Six management reports, adapted for small screens                                |
| **M9**  | Time & Self-Service            | Mobile M4, Design M4 (time)                  | Clock in / out, Time-entry management, both self-service summaries               |
| **M10** | Admin & Configuration          | Mobile M4, Design M4 (admin)                 | The nine admin/configuration screens                                             |
| **M11** | Account & Onboarding           | Server M8, design 0.4.6                      | The auth & account screen set, guided employee flows, link requests              |
| **M12** | Store Release                  | Server M9                                    | Build profiles, store submission, update policy, crash reporting                 |

Feature scope, catalog gates, and screen behavior for M4–M11 are defined once, in the same-numbered web milestone; the entries below add only what is specific to mobile.

---

## Milestone 1 — Expo Scaffold

**Goal:** Establish the Expo/Router workspace app, native build workflow, styling/lint baseline and measured Hermes behavior needed by Shared M3.

**Precondition:** Shared M1. Auth/session plumbing belongs to Mobile M2; themes and product components belong to Mobile M3.

**Status:** Complete. Implementation and native verification on both OSs are done; the iOS crash below remains an open residual risk.

### Phase 1 — App scaffold

- [x] **1.1** Add `@loto/mobile` with pinned Expo SDK 57, React Native, Router and workspace dependencies; provide start/build/typecheck/lint/format scripts.
- [x] **1.2** Configure strict TypeScript over the Expo and workspace base configs, local aliases and generated typed routes.
- [x] **1.3** Configure the app name, scheme, automatic appearance and temporary native identifiers; final store identifiers belong to M12.
- [x] **1.4** Add the Router root, placeholder and not-found routes without empty product route groups.
- [x] **1.5** Ignore generated native/build/signing output and add the public environment example.

### Phase 2 — Metro and native workflow

- [x] **2.1** Resolve Shared source packages through pnpm, enforce their public export maps and keep React/TanStack Query app-owned.
- [x] **2.2** Render imports from all three Shared entries on both OSs; reject private subpaths and prove the disabled query makes no credential lookup or request.
- [x] **2.3** Build and launch from repository-root commands on the selected iOS simulator and Android emulator; document API addressing and dynamically discover the installed JDK/SDK for child processes.

### Phase 3 — Lint, format and styling baseline

- [x] **3.1** Layer Expo/React Hooks lint over the workspace rules with narrow Router export exceptions.
- [x] **3.2** Enforce the raw-color gate, including negative fixtures and the future token-adapter exemption.
- [x] **3.3** Configure NativeWind, clear the default palette and provide the app-owned variant/class helpers.
- [x] **3.4** Apply the root formatter unchanged.

### Phase 4 — Hermes and Shared compatibility

- [x] **4.1** Measure actual Hermes Intl outputs, options and Unicode code points on both OSs against the pt-BR formatting contract.
- [x] **4.2** Exercise all 99 public core exports through 338 native assertions and record the per-family strategy in [Shared M3](../../shared/docs/milestones.md#milestone-3--core-helpers).

### Phase 5 — Verification and close-out

- [x] **5.1** Document version pins, native development builds, Metro resolution and tool discovery in `mobile/AGENTS.md`.
- [x] **5.2** Complete frozen installation, workspace checks, both-platform exports/builds, placeholder/query proof and five cold-start/probe/scroll/background-resume cycles per OS before introducing M2 auth.

### Verification and stability

Verified on iOS 26.5 / iPhone 17 Pro simulator and Android 16 / API 36 emulator using native Hermes OSS 250829098.0.17, bytecode 98, Expo runtime SDK 57.0.0/package 57.0.31. Each final cycle passed 338/338 core assertions and zero disabled-query dispatches. Production/development source maps confirm one app-owned React/query runtime and the actual Shared sources.

Raw Intl uses ASCII minus and a comma in datetime output; Android adds a plus to signed zero, while iOS omits the requested positive plus. Both engines expose hybrid calendar behavior at ancient dates, and iOS splits historical offsets across parts. Shared M3's manual/BigInt/IANA-offset strategies preserve the contract rather than copying those raw display differences.

- **Android:** a `MountingCoordinator::pullTransaction` SIGSEGV recurred. The pinned screens 4.26.2 package carries the narrow upstream [#4413](https://github.com/software-mansion/react-native-screens/pull/4413) listener-lifetime/concurrency backport. Ownership/reentrancy and 80,000-cycle concurrency tests pass normally and under ThreadSanitizer; later native cycles had no new app crash. The historical crash's unique cause and permanent elimination are not established.
- **iOS:** the 2026-10-27 10:53 development-app SIGABRT in `objc_storeWeak` during UIKit keyboard-shortcut enumeration remains unexplained and unfixed. Later clean cycles do not remove that residual risk.

Evidence is from simulators/emulators, not physical touch, screen-reader or store-release testing. Native setup and reproduction commands belong in `mobile/AGENTS.md`; raw captures remain outside the repository.

---

## Milestone 2 — Session & Data Plumbing

**Goal:** Implement app-owned session, API and device-data plumbing over the current backend contracts, without product login/cashier screens.

**Preconditions:** Mobile M1 implementation/native proof, Shared M2, Web M2 session semantics and Design 0.4.2. Planned Server M9.6 financial APIs remain outside this milestone.

**Status:** Complete. Implementation, tests and native verification on both OSs are done.

### Phase 1 — Configuration, credentials and gates

- [x] **1.1** Inject the public API URL through one app config module; require HTTPS in release.
- [x] **1.2** Store versioned identity/refresh/branch credentials only in SecureStore, with explicit failures and no plaintext fallback.
- [x] **1.3** Implement separate identity and branch gates over current M8 login/profile/renew/logout/branch contracts; read role and business time from authoritative responses.
- [x] **1.4** Prevent inherited keychain sessions after reinstall and stale restoration after failed sign-out deletion.

### Phase 2 — Renewal, scopes and lifecycle

- [x] **2.1** Single-flight renewal with durable rotated replacement, bounded recovery and one GET retry; never automatically replay writes.
- [x] **2.2** Fence token waits, delayed bodies and late replies by immutable scope; cancel/clear old query caches on replacement.
- [x] **2.3** Revoke through the real logout command, handle in-flight renewal and distinguish local cleanup from confirmed server revocation.
- [x] **2.4** Revalidate foreground/connectivity recovery, membership, role and branch-local clock; pause writes on stale time, unknown rotation or storage/network failure.
- [x] **2.5** Handle branch/day changes and selection cleanup; register the default client after commit, with no global configuration side effect during render.

### Phase 3 — Retained input and navigation

- [x] **3.1** Partition non-secret drafts by verified user/email, branch, resource and schema; preserve original base/revision and acknowledge only the saved revision.
- [x] **3.2** Require fresh resource/grant checks before restoration; refuse corrupt, foreign, secret/capability and durable ETag/version data; retain memory on save failure.
- [x] **3.3** Validate held/return destinations and origins; keep unclocked anonymous links in memory for at most 30 minutes and discard them on restart.
- [x] **3.4** Provide explicit counts-only device cleanup across retained owners/branches, with confirmation, active-operation/stale-review safeguards and visible failure; do not silently expire unresolved financial input.

### Phase 4 — Financial operation durability

- [x] **4.1** Persist a secure UUID and exact payload before the three existing contract-idempotent create commands; preserve the same key/payload through unknown responses and restart.
- [x] **4.2** Block changed unresolved intent, enforce a conservative server-time replay window and provide explicit checked reconciliation/discard recovery; definite refusals without prior uncertainty permit a fresh key.
- [x] **4.3** Keep mutation retries at zero; preserve backend ErrorMessages and Retry-After separately from recovery copy; treat dispatched network/timeout/abort/5xx/unreadable successful writes as uncertain.
- [x] **4.4** Keep guarded ETag/If-Match preconditions in memory and require a fresh read after conflict/uncertainty; never invent deduplication or backend financial outcomes.

### Phase 5 — Tests and native proof

- [x] **5.1** Establish Jest/RNTL tests for the session machine, actual generated calls, scoped provider and storage hooks.
- [x] **5.2** Cover simultaneous renewal, persistence failure, late replies, logout, membership/role/day changes, offline recovery and malformed/foreign retention.
- [x] **5.3** Verify refusal recovery, exact replay, expired/confirmed-record reconciliation, guarded conflicts, 429 handling and safe cleanup.
- [x] **5.4** Run real login → identity → branch/clock, native persistence, kill/restart and uncertain-write replay against an owned disposable API/PostgreSQL/fake-mail stack on both OSs.
- [x] **5.5** Verify native response headers, retained note/intent, reviewed device cleanup and logout/cold-start anonymity; exclude technical UI, fixtures and fault injection from production maps/bytecode even with proof flags enabled.

### Phase 6 — Integration

- [x] **6.1** Pass workspace checks, API generation/export drift, both-platform development/production bundle identity, native execution and relevant Web regressions.
- [x] **6.2** Reconcile the Mobile/Shared milestone records and guides without changing canonical specs/design markers.

### Delivered artifacts and verification

`src/session/` owns gates, renewal and scoped providers; `src/storage/` owns native adapters, privacy and retention; `src/data/` owns navigation, guarded commands and durable financial operations. Auth policy/request shapes derive from unchanged server OpenAPI. Shared remains platform-independent, and generated clients are not hand-edited. The DEV harness is exercised through Maestro/XCUITest and the Android UIAutomator fallback.

Mobile passes **24 tooling + 87 Jest/RNTL tests**. Workspace typecheck/lint/format/tests, API drift/export, both-OS development/production bundle gates, Web build/gallery/session regressions and canonical v58/design checks pass. The production gate rejects any bundle containing probe/fault modules and passes the current bundles with proof flags enabled.

Both native flows passed response loss → cold restart → exact replay, with two matching dispatches producing **one durable transaction per OS**, matching ETags and current grants. Both platforms also passed reviewed device cleanup, logout/cold-start anonymity and five final 338/338 core/stability cycles. The M1 iOS crash risk remains open; these runs do not establish permanent crash elimination.

Product screens, physical-device/accessibility acceptance, provider sign-in and store release remain in later milestones. Resource-specific/manual reconciliation must be supplied by the consuming screen; an absent listing row alone is not proof of non-execution.

---

## Milestone 3 — Design System Implementation

**Goal:** The NativeWind token theme (light + dark), theme switching that follows the system setting with an explicit override, every primitive in `design/specs/components/` implemented natively, and a development-only in-app gallery showing every token and component state in both themes.

**Mobile-specific key behaviors:** the token adapter mirrors web's (same JSON, same reference resolution, semantic and domain tokens only) and is the single lint exemption; primitives use platform controls where the contract allows (native date picker, bottom sheet for Modal/Sheet); touch targets and dynamic type follow the token guidance; the first Impeccable pass runs over the primitives.

**Precondition:** Mobile M1, Design M2 + M3, and Web M3 (adapter pattern proven).

**Status:** Implementation of Phases 1–6 is complete and implementer-verified on iOS 26.5 and Android 16 simulators/emulators. Independent review is pending; the physical-device, spoken screen-reader and Impeccable passes listed under _Open gates_ did not run.

### Decisions (2026-10-27)

- **Token adapter.** `tooling/token-resolver.mjs` validates `design/tokens/*.json` (file set, groups, keys, whole-value references, missing/group references, cycles, layering, cross-theme, literals in semantic/domain, light/dark parity, value shapes) and `tooling/tokens.mjs` generates `src/theme/token-adapter.ts` — resolved semantic/domain colors per theme (no primitive names), native type roles (letter spacing em → px, `tabular-lining` → `fontVariant`), spacing, radii, shadows (iOS offset/radius/opacity + Android elevation), motion with reduced variants, layers and breakpoints. The generated adapter is the single raw-color exemption; `lint` fails on drift and on any `sem-`/`dom-` class name that does not exist. Tailwind 3 reads the same resolver: semantic/domain colors as CSS variables supplied per theme through NativeWind `vars()`, the 4px scale, radii and type roles. `design/` is never imported at runtime.
- **Themes.** `ThemeProvider` (root, outside the session providers) reads the stored preference (AsyncStorage key `loto.theme-preference.v1`, presentation only) before any app UI paints; Claro/Escuro also set the OS appearance so native keyboards/pickers match; Sistema follows OS changes and re-reads on resume; reduced motion swaps every transition for its reduced entry; listeners are released on unmount. A failed write keeps the choice for the session and reports it.
- **Components** live in `src/ui/` (barrel `@/ui`), styled from the theme tokens; NativeWind classes remain available for layout and token colors. Platform choices: overlays are React Native modals; pickers, menus and filters opened inside a dialog render as in-modal layers (one modal trap; Android back closes the topmost layer first); the calendar is an app-owned sheet over core's calendar arithmetic (0001–9999, no `Date`, no OS-picker clipping); time is typed `HH:mm`.
- **Gallery** `src/probes/gallery/` is reached only through the Metro-folded `__DEV__` require in the home route; production maps/Hermes bytecode are checked for its path and strings.

### Phase 1 — Coverage map and restart documents

- [x] **1.1** Progress-only cleanup of the five area milestones (criterion IDs, ticks, gates and links preserved).
- [x] **1.2** Component-to-artifact/state/test map below.

### Phase 2 — Token adapter and theme lifecycle

- [x] **2.1** Build-time resolver/validator and generated adapter with drift and invalid-consumption gates; independent malformed-reference, layering, parity and value tests.
- [x] **2.2** Primitive-reference ban (classes, token paths, variables, CSS) alongside the existing raw-color gate.
- [x] **2.3** Native unit mapping: OS text scaling, tabular figures, touch minimums with hit slop, shadows/elevation, motion and reduced motion, layers, safe areas, keyboard insets (`ScreenScroll`; Android 15+ edge-to-edge does not resize the window).
- [x] **2.4** Sistema/Claro/Escuro with persisted override, no app paint before the preference is known, live OS change and resume, credentials untouched.

### Phase 3 — Components

- [x] **3.1** Inputs and forms: Button/link, FormField/Fieldset/FormGrid, TextField (text, e-mail, password, textarea, search), IdentifierInput, MoneyInput, Select (listbox, searchable, no default), DatePicker family, Checkbox/Radio/Switch.
- [x] **3.2** Data and structure: Badge, Card, SummaryStrip, DetailList, DataTable/GroupedList, FilterBar, Pagination, PageHeader.
- [x] **3.3** Overlays, navigation and feedback: Overlay (dialog, confirmation, drawer, bottom sheet), Menu, Tabs/SegmentedControl, Disclosure, Stepper, Notice/ErrorSummary/UncertainNotice, ToastProvider, SaveStatus, Skeleton, EmptyState.

### Phase 4 — DEV gallery and visual pass

- [x] **4.1** Gallery of every token category and component family with arbitrary-value inputs, event logs and labelled technical examples; restricted arithmetic demonstrated only there.
- [x] **4.2** Production exclusion: the bundle gate rejects the gallery by module path and by its strings (UTF-8/UTF-16), with a deliberate-leak negative.
- [x] **4.3** Screenshot-driven corrections on both OSs (see verification).

### Phase 5 — Tests and native proof

- [x] **5.1** Jest/RNTL contract tests and tooling tests (counts below).
- [x] **5.2** Native gallery journeys on iOS and Android dev builds from a fresh owned Metro.

### Phase 6 — Close-out

- [x] **6.1** Guide and milestone records; full workspace and bundle gates.

### Component map

| Contract                                                                                                    | Artifact (`src/ui/…`)               | Decisive states/behaviors                                                                                                                                                                                                                                                                                  | Tests (`test/unit/ui/…`)                                      | Gallery                         |
| ----------------------------------------------------------------------------------------------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- | ------------------------------- |
| button.md                                                                                                   | `button.tsx`                        | six variants, link role, disabled with reason (guarded, hint), loading verb as spoken name, stable width                                                                                                                                                                                                   | `controls`, `overlays`                                        | Button                          |
| form-field.md                                                                                               | `form-field.tsx`                    | field only adds disabled/required/invalid/read-only; label focuses control; grid stacks with text size                                                                                                                                                                                                     | `controls`                                                    | FormField e Input               |
| input.md                                                                                                    | `input.tsx`, `input-logic.ts`       | password toggle, search clear + polite count, textarea counter, read-only text; progressive identifier masks with caret and separator deletion                                                                                                                                                             | `controls`, `input-logic`                                     | FormField e Input               |
| money-input.md                                                                                              | `money-input.tsx`, `input-logic.ts` | blank ≠ zero, right-to-left cents, 15th digit refused, replacements and pastes through `parseBrl` (malformed ones rejected, value kept), spoken value, disabled/read-only never emit, fast native typing/deletion                                                                                          | `controls`, `input-logic`                                     | MoneyInput                      |
| select.md                                                                                                   | `select.tsx`                        | groups, inactive only when flagged, no default, searchable (accent-insensitive, complete list), loading/failed/empty, out-of-options marker, disabled never opens                                                                                                                                          | `controls`, `overlays`                                        | Select                          |
| date-picker.md                                                                                              | `date-picker.tsx`                   | typed text kept, contract error, branch today/bounds, 0001–9999 navigation, header reflow at large text, range, day stepper, month waits without reference, `HH:mm` following external values; every member inherits FormField disabled/read-only                                                          | `controls`                                                    | DatePicker                      |
| choice-controls.md                                                                                          | `choice.tsx`                        | indeterminate, radio without default, switch for device preferences only, hidden-label table selection                                                                                                                                                                                                     | `controls`, `feedback`                                        | Checkbox, Radio e Switch        |
| badge.md                                                                                                    | `badge.tsx`                         | copy §5 labels and domain tokens by exact enum key, unknown raw/neutral/reported, Automática, feedback, mode                                                                                                                                                                                               | `feedback`                                                    | Badge                           |
| card.md, summary-strip.md, detail-list.md                                                                   | `layout.tsx`                        | next-action region, checklist; unavailable figure is — never zero, signed tones with spoken sign; identity/provenance/restriction rows                                                                                                                                                                     | `feedback`                                                    | Card, SummaryStrip e DetailList |
| table.md                                                                                                    | `table.tsx`                         | shared column widths scaling with text, labelled horizontal region, sticky action column aligned per row, whole-row press vs explicit link vs nested actions/selection, loading/empty/error/updating, grouped/expandable lists                                                                             | `feedback`                                                    | Table/List…                     |
| filter-bar.md, pagination.md                                                                                | `filters.tsx`                       | staged apply, applied-scope summary, Filtros (n) sheet (inline inside a dialog); envelope-driven buttons, range text, out-of-bounds page corrected                                                                                                                                                         | `overlays`, `feedback`                                        | Table/List…                     |
| modal-sheet.md, menu.md, tabs.md                                                                            | `overlay.tsx`, `navigation.tsx`     | one dismissal path, busy not dismissible, focus to first field/title and back to trigger, nested layers in one modal, Android back topmost-first; menu radio group, unavailable items with reason; tabs/segmented                                                                                          | `overlays`, `theme`                                           | Modal/Sheet, Menu e Tabs        |
| notice.md, toast.md, save-status.md, skeleton.md, empty-state.md, stepper.md, disclosure.md, page-header.md | `feedback.tsx`, `layout.tsx`        | verbatim ordered messages, no-body fallback, uncertain recovery, error summary links; toast ≥5 s, pause, per-message budget, above an already-open keyboard; every SaveStatus text; static skeleton under reduced motion; four empty kinds; confirmed step statuses; disclosure state; header focus/return | `feedback`, `overlays`                                        | Notice, Toast…                  |
| tokens/README.md                                                                                            | `src/theme/*`, `tooling/token*.mjs` | resolution/validation, parity, primitive-free output, theme lifecycle                                                                                                                                                                                                                                      | `theme`, `tooling/tokens.test.mjs`, `tooling/colors.test.mjs` | Tema e tokens                   |

### Verification summary

- **Automated:** Mobile 30 tooling tests (token resolution/rejections, parity, primitive-free output, invalid class consumption, primitive references, gallery leak by path and UTF-8/UTF-16 strings) and 156 Jest/RNTL tests (69 new for theme/components); workspace frozen install, typecheck, lint (incl. token drift/consumption), format, Shared API 103 + core 296, Web 9 + 246, `generate:check` (287 files), real v58 doc-sync + 34 regressions, design-doc full/components + 14 negatives, contrast 1,164 pairs + 40 negatives.
- **Bundles:** development and production exports for both OSs; the production gate passes with proof flags set and rejects a deliberately leaked gallery (`technical probe module in production map`). Production ships the theme and the UI modules routes use; the gallery appears only in development maps.
- **Native:** dev clients on iOS 26.5 (iPhone 17 Pro simulator) and Android 16/API 36 (emulator) loaded fresh bundles from an owned Metro on port 8083 (project root verified). Maestro gallery journeys passed end to end on both OSs: theme override and return to Sistema, button states, money entry (right-to-left, deletion to blank, recorded zero, disabled unchanged), select sheet and disabled select, dialog with an in-modal nested select, busy confirmation that Back/close cannot dismiss, toast, branch-bounded calendar, table row versus nested action; on Android additionally Back closing the nested layer, then the dialog, then section → gallery → home, under OS reduced motion. Screenshots covered light/dark and the largest iOS text size, including the calendar sheet reopened at that size (month controls, year field and two-digit days whole), and a toast shown and replaced over an already-open keyboard. No new crash report on either OS.
- **M2 regression (root providers changed):** on an owned disposable PostgreSQL/Mailpit/.NET stack with fake identities, the real native login → identity → branch/clock, retained note, held intent, lost response → cold restart → exact replay flow passed on both OSs, each verified as one durable result from two dispatches with one key, one body hash and matching ETag. Stack, proxy, Metro and emulator were stopped afterwards.
- **Native rules** that the device runs established (pressed-state fills, order-independent money entry, keyboard insets, screen-reader-only focus, in-modal layers) are kept as current guidance in `mobile/AGENTS.md` § _M3 design system_.

### Open gates and risks

- Independent review of this milestone.
- Not executed: physical-device touch, spoken VoiceOver/TalkBack journeys and the named Impeccable tool (not installed; the contract checklist and screenshot pass were used instead).
- Native residual risks from Mobile M1 remain (Android `MountingCoordinator` SIGSEGV history, iOS `objc_storeWeak` SIGABRT); none recurred during these runs, which does not establish elimination.
- Controlled formatted text fields can still drop one keystroke when deletions arrive with no inter-key delay (automation-speed `eraseText`); the editing logic itself is order-independent and human-speed input passed.
- `INVALID_TIME` (**Hora inválida. Use hh:mm.**) mirrors the cataloged date message; the copy guidelines do not yet define a time-field message.

**Next step:** Mobile M4 (Entry & Navigation Shell) on these components, after Web M4's preconditions per the shared numbering.

---

## Milestone 4 — Entry & Navigation Shell

**Goal:** Real pt-BR password entry, branch selection, native drawer/stack shell and Session menu on the existing M2 engine and M3 controls. Every exposed business destination is protected and truthfully identifies its later owning milestone. No financial feature or server contract is added.

**Status (2026-10-29):** implementation candidate in the current `paralell/app` checkout, baseline `dfd4474`. M2/M3 foundations and the registered screens patch are present. Foreground/reconnect write holds, quiet menu authority refresh and clock-refresh race corrections are implemented and verified on the final runtime source. Automated, production and uninterrupted native checks pass as recorded below. Implementation checks are not independent acceptance. Independent review is pending. The narrow M3 prerequisites are technically acceptable for M4 use; whole-M3 independent acceptance, physical-device, spoken screen-reader and Impeccable gates remain open.

**Owning sources:** [catalog IA, entry/return and recovery](../../design/docs/screens.md#navigation--information-architecture), [shell blueprint](../../design/specs/screens/shell-and-identity.md), [product](../../design/docs/product.md), [D-M2-1](../../design/docs/decisions.md), component contracts and pt-BR formatting/copy. Current Web M4 policy/components/tests at `dfd4474` supply semantic evidence only. Native keeps one active branch and native routes/parameters; it imports no Web UI and introduces no tab model or shared UI package.

### Phase 1 — Coverage and prerequisites

- [x] **1.1** Inspect current Git/source/contracts and map the full catalog below. Preserve area ownership, pins, patch and acceptance gates.
- [x] **1.2** Exercise actual M3 product composition: additive field errors, disabled input callbacks, nested session-menu confirmation, theme switching, keyboard-aware entry and native drawer. Strengthen the field inheritance regression; do not certify whole M3 from these checks.
- [x] **1.3** Narrow M3 prerequisite disposition: additive errors, disabled/read-only callbacks and per-option unavailability reasons are technically acceptable for M4 use. This does not accept Mobile M3 as a whole.
- [ ] **1.4** Obtain independent acceptance of Mobile M4 after technical recheck. Whole-M3 independent, physical/spoken and Impeccable gates remain separate.

### Phase 2 — Password Login and picker

- [x] **2.1** E-mail/Senha labels, autocomplete, visibility, next/go focus flow, duplicate suppression, Entrando…, in-memory password clearing on submit/journey end, verbatim generic failures, advisory Retry-After and explicit manual recovery after uncertainty. No provider/account-existence inference or durable credentials.
- [x] **2.2** Zero/single/multiple-membership rule; exactly-once automatic opening; full-row branch name/address/CNPJ/live-role controls, Abrindo…, competing-selection suppression, scope-surviving refusals and manual refresh/retry. Identity alone opens no branch data; account/logout and truthful M11 availability remain reachable.

### Phase 3 — Native shell and Session menu

- [x] **3.1** Safe-area header, grouped compact drawer, current destination, real caller/branch/role/mode and server-owned branch date/time. Claro/Escuro/Sistema uses M3's independent preference and first-paint gate. The Stack uses native gestures and reduced-motion animation settings.
- [x] **3.2** Real mode/branch/logout commands; account-menu opening quietly refreshes authority for newly linked operators; unavailable Meu turno is focusable/readable with its reason, not an implied grant. Confirm logout only for actual retained/unconfirmed work; keep input/theme and distinguish local cleanup from unconfirmed server revocation. Account/provider business pages stay M11.
- [x] **3.3** Prioritized recovery/day/retained-work rail (offline uses the full recovery sheet) with at most two notices plus remainder; live permission explanation; M3 announcements and keyed headings avoid clock-refresh focus theft. Technical tools/gallery remain behind folded DEV imports.

### Phase 4 — Guarded registry and live context

- [x] **4.1** Typed app-owned registry of 40 branch destinations, concrete Expo routes and compile-time Href sentinels. Distinguish not-found, forbidden, setup/read failure and intentional future availability. Member defaults Hoje and has no Gestão/on-behalf menu; elevated roles default Fila, with optional linked Meu turno. Ponto remains Member-only; nine reports remain distinct; elevated Gestão Open/Close/Fix has no blanket operator-link gate.
- [x] **4.2** Engine-owned generated self-context reads at creation/recreation, mode/shift entry, foreground and relevant 400/403 refusals. Verified identity/branch fingerprints survive failures and scoped recreation; clock-only timers/focus do not re-read self-context, and quiet background self checks hold branch writes internally until verification completes without publishing checking or a verification rail item. Foreground/reconnect holds cover profile, clock and self-context reads, with a second write guard immediately before transport dispatch. Concurrent clock-only refreshes cannot skip required authority verification or discard a successfully opened branch; branch-opening commands are never replayed. Role/link/grant changes pause writes, cancel scoped clients/results, clear selections and reconcile the mode/home while isolated input remains retained. No URL, mode or stored candidate grants authority; failed reads never become a missing-link or empty-account assertion.

### Phase 5 — Navigation and recovery

- [x] **5.1** App-owned non-secret navigation storage is separate from SecureStore and financial input, partitioned by verified UserId + canonical-email hash + branch. Allowlisted filters/origins are revalidated; secret/capability, corrupt, cross-identity and unauthorized candidates are refused. Resource restoration requires a feature-owned fresh validator; M4 intentionally refuses such durable resource candidates until that feature exists.
- [x] **5.2** Branch switch cancels old scope and clears active mode/selection/workstation/origin while preserving isolated input. Explicit choice Y cannot resume an old X intent. Logout and branch-switch transitions cannot recapture an outgoing screen as a new anonymous/unscoped intent. A genuine incoming link has its own capture boundary.
- [x] **5.3** Catalog fallback/return handling, native keyboard-first Android back, drawer dismissal, stack/account returns and protected cold/warm links. The shell blocker test retains count/Notes/business-date candidates through fresh branch/resource checks with no form implementation, write replay or durable ETag.

### Phase 6 — Verification

- [x] **6.1** Jest/RNTL exercises actual generated calls and providers, login failures/uncertainty, branch counts/requests/refusals, exhaustive role/mode policy, setup versus read errors, foreground/link/grant/role changes, old-scope responses, uncertain logout, theme retention, restored shared mode and blocker/input isolation. Tooling checks every registry template owns a guarded Expo route.
- [x] **6.2** Final-tree uninterrupted iOS/Android product journeys against the owned disposable API stack, native screenshots and relevant M3 regressions. Native build success alone is not journey acceptance; physical/spoken and independent gates remain below.
- [x] **6.3** Final workspace typecheck/lint/format/tests and fresh both-OS proof-flag production export/map/bytecode exclusions after the last correction. Preserve the existing single-runtime/query-context and auth-policy drift gates; do not export/change server contracts in this lane.

### Phase 7 — Delivery

- [x] **7.1** Record final current verification and true open gates; provide the detailed unsigned proposed commit message in chat only. No Git integration/commit/push/deployment or release/financial-readiness claim.

### Route / state / test map

Every branch destination below passes the common authority gate before its availability content. A future page does not assert that a resource exists or grant a financial action.

| Catalog family                                                                                                                           | Routes / state / return coverage                                                                                                                           | Owning artifacts and decisive tests                                                            |
| ---------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| Login, picker, Session menu                                                                                                              | Anonymous → identity → branch; zero/one/multiple, pending/error/uncertain, account/logout, intent-X versus choice-Y                                        | `src/shell/entry.tsx`, `login.ts`, `branches.ts`, `menu.tsx`; `shell-ui`, `shell-session`      |
| Profile, Change password, Forgot/Reset password, Invitation, Plataforma                                                                  | M11 availability; identity/platform/anonymous capability gates; actual stack opener or Login/picker fallback; no capability persistence                    | Concrete `src/app/` routes, `screen.tsx`, existing session gates; UI/policy/storage tests      |
| Fila, approval; Hoje, Open/Close/Fix                                                                                                     | M7/M5 availability; role homes, linked/unlinked setup, elevated Gestão exception access, validated queue/report/operation origin or mode home              | `policy.ts`, `screen.tsx`, engine self-context; exhaustive policy and blocker round-trip tests |
| Transactions list/Create/Edit, new/existing plans; Clients, statement, payment                                                           | Shared-mode M6/M10 availability; native shortcuts, list/source/client/blocker returns, independent scoped input and fresh resource restoration gate        | Concrete routes, `navigation.ts`, provider; policy, UI and retained-candidate tests            |
| Dashboard, Movimento, Saldo fiado, Fiado por idade, Cheques, A receber, Diferenças, Fechamento mensal, Financeiro mensal                 | Nine Gestão/M8 destinations, valid year/month segments, report/source fallback; Dashboard is never the default home                                        | Registry, compiled Hrefs, route tooling test and full role matrix                              |
| Ponto, own transaction/hour summaries; manager-selected summaries, Gestão de ponto                                                       | M9 availability; Member-only clock, linked own context versus manager-selected context, appropriate Hoje/Operators/time-management fallback                | Registry/policy tests and native menu mode journeys                                            |
| Operators, assignments, Accounts, Clients, categories/types, Products, Holidays, Settings, members; Add/Offboard employee (both entries) | M10/M11 availability; protected admin routes, shared Clients exception, actual admin source or catalog fallback                                            | Registry, route tooling/Hrefs, field/menu/overlay composition tests                            |
| Invalid/foreign/hidden path, stale/corrupt origin, role/link/membership change                                                           | Distinct forbidden/not-found/setup/read failure; no branch dispatch from identity; clear active scope, retain isolated input; no write/precondition replay | Existing M2 suites plus `shell-policy`, `shell-session`, `shell-ui`                            |

### Current verification and remaining gates

- **Automated:** 31 Mobile tooling tests and 213 Jest/RNTL tests across 17 suites pass. Full workspace typecheck, lint and format pass; serial workspace tests pass with unchanged timeouts (Shared API 103, core 296, Web 9 tooling + 334 unit tests). An earlier concurrent run during simulator startup timed out in two Mobile startup tests; the complete serial rerun passed. Regression coverage includes quiet foreground/reconnect write holds through profile/clock/self-context, transport-time refusal, stale-scope hold isolation, menu link discovery and concurrent clock refreshes during foreground and single-branch opening.
- **Production:** fresh iOS/Android exports pass with both native proof and fixture-prefill flags enabled. Source-map/bytecode checks exclude technical probes, gallery and fault-injection markers, retain one React 19.2.3 runtime and one app-owned query context, and confirm actual API/core sources compiled. Log: `/private/tmp/loto-m4-recheck-production.log`.
- **Final-source native:** `/private/tmp/loto-m4-recheck-native/{ios/full.log,android/full.log,android/linked-manager.log}` each completed uninterrupted with exit 0 against the owned disposable API. All three ran on runtime SHA-256 `ccca51f2598c24fdc9e27a46f8ebc3da8cd1add986882adb50819610ba927c53`; source bytes and modification times remained unchanged after the runs. `runtime-source.sha256` and `verification-summary.json` record that boundary. Earlier `/private/tmp/loto-m4-review-native` evidence, initial hashes, emulator exits and pre-fix attempts are historical, not final-tree proof.
- **Journey coverage:** iPhone 17 Pro / iOS 26.5 and Android 16 / API 36 completed real login → multiple-branch picker → native drawer/account/availability/return → Home/relaunch foreground → theme → branch switch → logout → protected cold Dashboard link → fresh login/branch → logout. Android additionally completed a linked Manager's single-branch automatic opening, Gestão/Meu turno switches and Member-only Ponto exclusion. Linked-manager native proof is Android only; screenshots remain outside Git.
- **Native environment:** two default-renderer Android emulator processes exited 139 after QEMU thread hangs. Final accepted Android runs used the same image/data with per-process headless SwiftShader and Vulkan disabled; no SDK upgrade or device-data reset. Android's final app crash buffer is empty; no new iOS app crash report appeared. Nonfatal text-field measurement warnings remain; clean journeys do not establish elimination of historical native risks.
- `tooling/native-stack.mjs` owns disposable PostgreSQL/Mailpit/.NET fixtures seeded through supported flows; it imports no Web source or runtime source-rewrite harness. `native-shell-stack.mjs` starts/stops only owned resources; `native-shell-flow.mjs` writes native-touch flows outside Git. Credentials, captures and raw logs stay outside Git. The owned stack, Metro and selected native targets were stopped after verification; no device data was reset.
- Open independently: M4 reviewer acceptance and whole-M3 independent acceptance, physical-device touch, spoken VoiceOver/TalkBack and named Impeccable. Historical Android MountingCoordinator SIGSEGV and iOS objc_storeWeak SIGABRT remain residual risks; a clean run does not prove elimination. Financial features, resource-specific restore validators, providers and store release retain their later milestones.

## Milestone 5 — Operator Day

**Scope and gates:** as Web M5 — the mobile-first flows of the catalog. This includes gap AA on Close day and Fix & resubmit (retained-retired-product edit/preview/save recovery only; server DailyClose/Product, scheduling pending M7.7 close-out triage); other screen behavior and historical reads proceed. The catalog’s gap → screen map remains authoritative. **Mobile-specific:** the "once per device and branch-local day" terminal choice is stored on the device; Close day's count entry is tuned for numeric keypads and survives the app being backgrounded mid-count. **Flow-alignment (Batch B):** the blind count, Conferir/Recontar, inline opening and _Passar o caixa_ follow Web M5; the arithmetic money input works with the numeric keypad (operators need `+`), and a backgrounded app never reveals the difference before Conferir.

**Status (2026-11-06):** implementation in the current `paralell/app` checkout (merged v63 API, OpenAPI unchanged by this lane). Phases 1–8 are implemented and locally verified, including native journeys on both OSs against the owned real stack. The first independent review did not accept M5 (1 P1, 4 P2, 2 P3); all seven are fixed with Jest regressions, without a native rerun or recheck yet. Physical-device and spoken screen-reader acceptance are pending.

**Sources:** catalog [Operator Day Cockpit, Open day, Close day, Fix & resubmit](../../design/docs/screens.md#operator-day-cockpit), close-day autosave and durable-input patterns, the [operator-day blueprint](../../design/specs/screens/operator-day.md) (its pre-v59 terminal/day/Open-day prose is superseded by the catalog v59–v61 session rows), Web M5's flow-alignment clause, and the actual `SessionController`, `SessionStarter`, `SessionOpeningResolver`, `PutDailyCloseItemsUseCase`, `SessionAllowedActionsResolver`, handover/finish/custody-exception use cases and their DTOs.

### Phase 1 — merged-contract reconciliation and state map

- [x] **1.1** Categories & Types follow v62/v63: `FinancialClass` is edited and sent; the copy says a posting freezes its classification and a save never re-labels posted or locked rows; **Reclassificar período aberto** is a separate audited command shown as later availability, never run by a save. Pre-v63 drafts restore with the fresh server class. `ChequeFiadoConversion` system source added (it broke the merged typecheck).
- [x] **1.2** Resource model: cashbox (Terminal account), physical terminal CAIXA, custody turno and authentication session are distinct; the exact session id selects a close (several per cashbox/day, separate cashboxes on one terminal). `validateDayResource` re-reads the exact session before any origin/intent restores; URL, mode or stored choice never grants authority.
- [x] **1.3** Business dates arrive as `YYYY-MM-DDT00:00:00`: only their calendar part is compared with `BranchLocalDate` (found against the real API). Failed reads are never "no session"; unlinked Manager/Admin keep Gestão Close/Fix access; Ponto stays Member-only.

### Phase 2 — Hoje and workstation context

- [x] **2.1** Branch day, cashbox choice per verified identity/branch/device/server day (cleared on another day or a lost grant), one/many/none states, Member setup with read-only link-request status and M11 availability.
- [x] **2.2** Next action P1–P7 from complete, successful reads only (every page of the session lists; a longer list than the page ceiling is a failed read) (failed higher-priority read → **Não foi possível verificar…**), today's close link independent of P1, other open cashboxes, custody held by another operator, offered handover, Member Ponto, bounded movements with **Automática**, exact Draft split, self reports and guarded M6 links.

### Phase 3 — opening and custody

- [x] **3.1** **Conferir abertura e iniciar** echoes the proposal source and sends the full acknowledged vector; **Abertura diferente** changes only observed rows; a missing or all-zero proposal needs explicit zero. In-memory idempotency intent: an unknown outcome is retried only explicitly with the same key/body; a changed source is refused verbatim, re-read and the typed values kept. The confirmed receipt (with an unattributed discrepancy) survives the follow-up reads.
- [x] **3.2** Gestão: cashbox and custodian choice with required reason for another operator, custody exception (Finish/AcceptAndStart, F1 included), past interval as declared-turno availability; active/unresolved/no-terminal states.

### Phase 4 — count form and arithmetic entry

- [x] **4.1** Review rows by exact ProductId, blank ≠ zero, signed Relatório do terminal and nonnegative fees, only changed close fields sent, variance row never sent, blind until Conferir.
- [x] **4.2** Design 4.9.7 mobile behavior ([money-input](../../design/specs/components/money-input.md#arithmetic-entry-design-497--mobile-behavior-mobile-m5)): **Calcular** buffer with operator keys above it, result before **Usar**, only the number sent, autosave, Conferir and Enviar wait while a calculation is open, raw buffer retained. Signed amount: a pasted `-`/`−` value sets **Negativo** with its magnitude.
- [x] **4.3** Repetir abertura / Desfazer repetição row-local, exact pre-copy value, survives autosave. AA: the whole count is locked for edit and save (never trimmed, cleared or resent) while it holds a stored row of a product the active product list no longer has, while that list cannot be read, or after a 404 save refusal (not lifted by later edits or rereads in that view); typed input stays shown and the rest of the app usable.

### Phase 5 — save, autosave, conflicts

- [x] **5.1** Today's plain Draft autosaves after real edits; corrections/backdated/rechecks need **Salvar**. Single flight, coalesced newer edits, strong If-Match from the verified ETag, older acknowledgements never clear newer device input. Stale → **Recarregar e conferir** with deliberate current-versus-local choice; unknown → reread that confirms a landed save without resending.
- [x] **5.2** Device drafts per identity/branch/session (no token/version), restore for review only; renewal keeps the verified view mounted with writes paused; server day change stops autosave and keeps the original session.

### Phase 6 — Conferir, Recontar, submit, Fix & resubmit

- [x] **6.1** Candidate states only from server `Count`/`AllowedActions`; Conferir/Recontar with token, revisions, If-Match and one key per intent (key, body and If-Match fixed at the first send and reused on an explicit retry); Unconfirmed needs **Confirmo que esta contagem ainda representa o caixa** (bound to the exact token/revisions shown; any new revision clears it) or Recontar; Submit sends the Current attempt; refusals verbatim with a fresh reread; omitted opening products by attempt identity.
- [x] **6.2** Fix & resubmit: dated Rejected/OpeningChanged events, **Ver o que mudou**, honest fallbacks, Draft blockers routed to guarded M6 availability.

### Phase 7 — finish and handover

- [x] **7.1** After submit the custodian (or Gestão with a reason) can **Encerrar turno** or **Passar o caixa**; pending offers show state and can be withdrawn with a reason; the incoming operator accepts the immutable vector or observed differences, or disputes. Custody actions require a current history read: a failed read shows that a pending passagem cannot be verified and offers no Encerrar/Passar/Retirar. Member outgoing offers need an operator list Members cannot read (`GET /operator` is Manager/Admin): shown as a precise limitation.

### Phase 8 — verification

- [x] **8.1** Jest/RNTL: `day-contracts` (21), `day-ui` (35), `day-router` (3) on an in-memory model of the shipped rules (`test/unit/day-server.ts`, business dates in the API's date-time shape). Mobile totals: 32 tooling + 446 Jest across 27 suites; workspace typecheck/lint/format; Shared API 103, core 319, Web 9 + 334.
- [x] **8.2** Native journeys (iPhone 17 Pro / iOS 26.5 with Maestro 2.11.0; Lotero_API36 / Android 16 through `native-day-android.mjs`) against the owned .NET/PostgreSQL stack with API-seeded card, PIX, expense, fiado sale, fiado cash and PIX repayments and Depósito Dinheiro: Hoje with the per-day cashbox choice → explicit-zero start (iOS) / previous-session proposal (Android) → blind close → arithmetic count and signed report → autosave → Conferir → another actor's ledger change → Unconfirmed + explicit confirmation → submit → Gestão **Passar o caixa** → incoming different opening (unattributed −R$ 10,50 / −R$ 12,50) → Recontar → submit → Encerrar turno → rejection → Fix & resubmit with an app kill and device-input restore → unlinked Gestão read. Android also ran F1 (a turno left active past the branch midnight finished by the manager exception). Supported API rereads confirm per OS: attempts Initial/StillObserved/Correction and Initial/Recount, accepted handover, finished/handed-over custody, the restored correction value saved, generated close-field row, single outcome per command.
- [x] **8.3** Fresh `dist/m5-final` iOS/Android exports with both proof flags pass the exclusion/single-runtime/source gate and ship all `src/day` sources.
- [ ] **8.4** Independent review, physical-device, spoken VoiceOver/TalkBack and enlarged-text/theme native passes for M5.

**Review findings fixed (2026-11-06):** AA lock covers the whole count and is derived before any save from the active product list; open calculations block Conferir/Enviar; the still-observed confirmation is bound to the presented revisions; a failed history read is unknown custody, not "no passagem"; the cockpit reads every page of its session lists; command retries keep their original If-Match; Relatório do terminal accepts a signed paste. Each has a Jest regression that fails with the defect restored. Native reruns of these paths are not done.

**Defects found natively and fixed:** session dates arrive as `YYYY-MM-DDT00:00:00` (autosave/day checks were wrong against the real API); a negative server expected closing crashed the unsigned formatter; the iOS keypad covered the calculator keys/result (now above the buffer); key-driven buffer edits desynced iOS native text (remount + refocus); the refocused buffer stayed under the Android keypad (`useRevealFocused`); sign labels collapsed in a wrapping row.

**Limitations:** the iOS **Passar o caixa** tap was completed by a corrected one-off native replay after the journey's tap landed during a keypad layout shift (the server shows the resulting offer and acceptance); several iOS assertions on transient toasts were replaced by server rereads; Maestro's Android visibility/scroll was unreliable on this emulator, so Android ran the same generated command lists through the adb/UIAutomator driver. The Android 05/11 chain crossed the branch midnight and was continued on 06/11 (F1 then a new turno). Member outgoing **Passar o caixa** needs an operator list Members cannot read (`GET /operator` is Manager/Admin). Captures, rereads and logs stay in `/private/tmp/loto-m5-native`.

### Surface / test map

| Surface               | Artifacts                                                          | Decisive tests                                                                                                                                                                                                            |
| --------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hoje                  | `src/day/cockpit.tsx`, `workstation.ts`, `contracts.ts#nextAction` | P1–P7 from successful reads, partial failure, choice per day, setup, held/receive                                                                                                                                         |
| Abrir dia             | `src/day/open.tsx`                                                 | echoed source/full vector, explicit zero, unknown retry same key, changed source, handover accept, receipts                                                                                                               |
| Fechar dia / Corrigir | `src/day/close.tsx`, `count.ts`, `arithmetic.tsx`                  | blind states, autosave/If-Match/coalescing, stale reconciliation, Conferir/Recontar/Submit, AA lock, open-calculation gate, revision-bound confirmation, retry If-Match, signed paste, unknown custody, rollover, restore |
| Routing               | `surface.tsx`, `resources.ts`, shell integration                   | renewal keeps input, branch claim, unlinked Gestão                                                                                                                                                                        |

**Boundaries:** M6 owns transaction create/edit/list since Mobile M6 (native M5 proof seeded movements through APIs); M7 queue/approval and opening-discrepancy attribution; M11 link request/guided flows; gap AA editing semantics; P6/P7 P&L and attributed shortages; declared/rearranged past turnos.

**Next action:** recheck of the seven review fixes (native rerun of AA lock, open calculation and unknown custody included); physical/spoken device checks.

## Milestone 6 — Transactions Ledger

**Scope and gates:** as Web M6. **Mobile-specific:** fast entry optimized for one-handed numeric input; the Transactions list is server-paginated (20/50/100 rows per page, rendered inside the screen scroll; no client-side virtualization or local search is needed at those bounds).

**Status (2026-11-08):** the M6 core and the reachable cheque lifecycle are implemented in the current `paralell/app` checkout against the merged v64 contract (OpenAPI and generated Shared unchanged by this lane) and verified by Jest/RNTL against an in-memory model of the shipped rules, both-OS production exports and native journeys on both OSs (all journeys on Android; a subset on iOS, see 9.3). The first independent review did not accept M6 (2 P1, 3 P2); the re-review accepted four fixes and found the cheque receipt still lost on a failed transaction reread, now also fixed, together with already-open cancel dialogs and cheque sheets that could still send after a read failure, each with a Jest regression; re-review of these fixes pending. The M5 seven fixes passed the code/test recheck; their full native acceptance and the remaining M6 acceptance stay open (criteria 9.4–9.5). Bank/provider discovery, OFX and card-batch UI are a named later banking increment (criterion 8.4), not part of this delivery.

**Sources:** catalog [Transaction — Create](../../design/docs/screens.md#transaction--create-fast-entry), [Edit](../../design/docs/screens.md#transaction--edit-correction--audit), [Transactions list](../../design/docs/screens.md#transactions-list), [Installment plan](../../design/docs/screens.md#installment-pre-dated-cheque-plan), [Client statement](../../design/docs/screens.md#client-statement-extrato-de-fiado) and [Receber pagamento](../../design/docs/screens.md#receber-pagamento); Web M6 flow-alignment (Batch B/C); the actual `TransactionController`, `ChequeController`, `ReceivableController`, `DailyCloseLedgerGuard`, `ListTransactionsUseCase`, create/preview/update/finalize/cancel, installment, fiado-payment, safe-deposit and cheque use cases and their DTOs.

### Phase 1 — inventory, routing and authority

- [x] **1.1** Destinations `transactions`, `create`, `edit`, `plan-new`, `plan`, `statement`, `payment` and the new shared `safe-deposit` (`/transacoes/deposito-cofre`) dispatch to `src/ledger/` after the shell guard; the surface re-checks role. Availability placeholders for these routes are gone; M7/M8/M11 destinations keep theirs.
- [x] **1.2** Resource origins/intents (`/transacoes/:id`, plan, client statement/payment) restore only after a fresh server read in this branch (`src/ledger/resources.ts`, chained into `validateResource`); statement/payment also re-check current statement access. Mode, `Mine`, `OperatorId` and the stored address grant nothing.
- [ ] **1.3** M5 seven-fix recheck by the independent reviewer and its native rerun (see criterion 9.4).

### Phase 2 — Transactions list and detail/audit

- [x] **2.1** Server pages, ordering and filters only (date window, status, account, type, client, operator, `Mine`), kept in the route so opening a row and returning restores filter, page and focus. Members never send `OperatorId`; an unlinked caller never sends `Mine` (a server no-op). No totals, no local search.
- [x] **2.5** Applying or clearing filters writes every filter key (a cleared one empty), so a removed filter never survives in later queries.
- [x] **2.2** Rows show type, signed value by direction, status, **Automática** (structural `GenerationKind`), frozen financial class, plan membership, cheque due date and the turno marker. Draft rows of a plan open the plan editor.
- [x] **2.3** M5 Draft-blocker context (one cashbox, one day, Draft): server own/other split via `Mine`; "nothing pending" only after a fresh successful read; **Voltar ao fechamento** returns to the retained close input.
- [x] **2.4** Detail shows the current resource, frozen classification, recorder (own/other), audit instants, cancellation reason, generated-row origin/counterpart, session (close) and plan links.

### Phase 3 — fast entry and preview

- [x] **3.1** Type-driven form: system, repayment (Tab `In` or `FiadoPaymentKind`), retired and unknown-direction types are never offered; fiado types list only Tabs, others cashboxes and banks (never a cofre). A fiado sale names its paired cashbox leg and never offers a second saída; an unpaired/ungranted pairing blocks the sale.
- [x] **3.2** Amount through the M5 arithmetic field (blank ≠ zero, open calculation blocks sending); date/time from the branch clock; client with **Cadastrar cliente** → Clientes → returns selected with values intact; Manager/Admin on-behalf operator (required when unlinked).
- [x] **3.3** Automatic, debounced, signature-fenced server preview (late responses never shown; never blocks or saves); Draft close variance stays blind; a non-cashbox account says so instead of a zero difference.
- [x] **3.4** Input retained on the device (exact schema/privacy validation, no token/version); a lost choice is cleared and named, never reinterpreted.

### Phase 4 — inline opening, durable intent, create/finalize

- [x] **4.1** No active turno today: the M5 acknowledgment (proposal source, full vector, observed differences, explicit zero, responsible operator/reason) travels on the same create request after an explicit confirmation bound to the shown proposal; never a separate Start. A competitor turno or changed proposal at dispatch refuses locally and keeps the form. Earlier day: Manager/Admin choose a Draft session of that day with a reason; Members are told it is a manager task.
- [x] **4.2** Durable intent per form (`FinancialOperation`, now also Depósito Dinheiro and the five cheque commands; targeted commands persist their path id): exact bytes/key before dispatch, unknown outcome verified only by the identical explicit retry (also after remount/restart), changed input blocked while unresolved, replay-window expiry → manual reconciliation and explicit discard.
- [x] **4.3** A confirmed result keeps its receipt through failed follow-up reads; **Salvar como rascunho** and **Finalizar** are separate commands (finalize with the returned version as If-Match).

### Phase 5 — correction/audit Edit and cancellation

- [x] **5.1** Only Description/DueDate/ClientId/TransactionTime are editable; value/account/type/date read-only; no PaidAt. Generated, cancelled and (Member) other-operator/prior-day rows are read-only with the reason; the client is locked once the receivable has command events (Manager/Admin read; Members get the server refusal verbatim).
- [x] **5.2** Manager/Admin edit-preview (fenced, non-blocking); save/finalize/cancel with the loaded strong ETag in memory only. A conflict keeps the buffer, blocks every command and requires **Reler a transação**: unchanged version → nothing applied; new version → explicit adopt-or-discard. Unknown outcomes are reconciled the same way, never replayed.
- [x] **5.3** Cancel with required reason (counterpart cancelled server-side); **Corrigir valor, conta, tipo ou data** is an explicit cancel followed by a separate new transaction prefilled from a fresh read (`copyOf`). Settlement-event refusals render verbatim; no compensating row.

### Phase 6 — cheque plans

- [x] **6.3** Saving binds to the exact accepted request: the input on screen at the tap is sent, and an edit made while the turno is re-checked stops the send until the new schedule is accepted. A plan restored on return from Clientes keeps later edits (state-driven retention, no stale overwrite).
- [x] **6.1** New plan: cheque-rule types on cashbox/bank accounts, manual rows (2–24, integer-cent sum hint) or auto-generation (first due date + count); the server preview schedule must be explicitly accepted (bound to the request signature) before a durable create/Draft create, with the same inline opening.
- [x] **6.2** Existing plan: status/counts/total and every sibling with its status; **Finalizar plano** / **Finalizar parcelas restantes** sends every Draft id and version in one request (`ConfirmMixedPlan` for Mixed); a stale set changes nothing and requires a reread. Selected-row cancellation is one guarded command per row in order, stopping at the first failure without undoing earlier ones. Row corrections open each installment's own editor and return to the plan.

### Phase 7 — statement, repayment, Depósito Dinheiro

- [x] **7.1** Statement access (Manager/Admin or explicit Tab grant) is checked before any read; balance, oldest unpaid date, window opening/closing, running balances and rows are the server's through core's fiado mapping (debt, quitado, `+… · crédito do cliente`); window/page in the route.
- [x] **7.4** The balance after a payment comes only from a read that succeeded after the confirmation; a failed reread says so and never shows the cached earlier balance.
- [x] **7.2** Receber pagamento: repayment method with no default (refused locally with **Escolha a forma de pagamento.**), receiving Tab (workstation's paired Tab default only when granted), drawer effect by `FiadoPaymentKind`; Dinheiro may need the inline opening of the paired cashbox, other methods send no session fields. New balance is re-read from the statement.
- [x] **7.3** Depósito Dinheiro: granted cashbox → granted cofre through `POST /transaction/safe-deposit` (two server legs, both or none), inline opening when needed; receipt links both legs; no bank deposit or revenue claim.

### Phase 8 — cheque lifecycle

- [x] **8.1** Manager/Admin: the receivable chain (state, outstanding, custody, attempts, events with occurred/posted dates) is read for the exact cheque; commands offered by current state: Levar ao cofre, Depositar, Registrar devolução (client fixed once identified, next deposit date, alínea), Reapresentar, Converter em fiado (Tab, no cashbox leg). Exact expected version and one durable idempotent intent per cheque; unknown outcomes are verified by the identical retry.
- [x] **8.2** Batch custody/deposit from explicitly selected list or plan rows: each selected cheque is re-read; only eligible whole cheques are sent in one request; excluded ones are named. A deposit reports one pending bank receipt, never arrival.
- [x] **8.5** A confirmed cheque command's receipt stays visible when a follow-up read fails — the receivable or the transaction itself (a failed transaction reread keeps the screen mounted with a warning); no further command is offered or sent until a successful reread, including from a cancel dialog or cheque sheet that was already open.
- [x] **8.3** Results show the server's new cheque state, pending receipt and the converted fiado debt link.
- [ ] **8.4** Later banking increment (not in this delivery): provider settlement batches, bank movements/recolhimento, OFX statement import and match confirmation, card batches — destinations for M6/M8 banking UI. No GUID entry, receipt inbox or manual credit workaround is offered.

### Phase 9 — verification

- [x] **9.1** Jest/RNTL: `ledger-contracts` (40), `ledger-operations` (3), `ledger-ui` (31, on `test/unit/ledger-server.ts`, an in-memory model of the shipped transaction/fiado/cofre/cheque rules), plus an overlay regression. Request-shape validation now understands nullable `oneOf` references (the inline opening was refused locally before).
- [x] **9.2** Workspace typecheck, lint and format pass; Mobile 32 tooling tests and 531 Jest tests in 30 suites on the final source (an earlier run showed one load-dependent timeout in the existing `admin-financial-ui` suite that passes in isolation). Fresh `dist/m6-final` iOS/Android exports with both proof flags and source maps pass the exclusion/single-runtime gate and ship all 14 `src/ledger` modules, no probe or test source.
- [x] **9.3** Native journeys (`tooling/native-ledger-fixture.mjs`, `native-ledger-flow.mjs`; Maestro 2.11.0 on iPhone 17 Pro / iOS 26.5, `native-day-android.mjs` on Lotero_API36 / Android 16) against the owned .NET/PostgreSQL stack, checked by supported API rereads. **Android, all journeys:** inline opening with explicit zero → expense (turno and row in one create); Draft → explicit finalize; Draft → Fechar dia blocked → **Finalizar movimentações** → list → finalize → list re-read clear → back to the close with the typed count kept (server: count 77,70 saved, 0 Drafts); Cadastrar cliente → back with the new client and the typed values → fiado sale with its server `FiadoSale` cashbox leg → statement → PIX (no cashbox leg) and Dinheiro (cashbox leg) repayments with re-read balances (40 → 30 → 25); Depósito Dinheiro opening the second cashbox atomically (generated cofre leg); cheque plan (server schedule accepted) saved as Draft → atomic **Finalizar plano** → cheque custody → deposit (one pending bank receipt) → return → re-present → conversion (one Tab fiado debt, no cashbox leg; chain version 0→5). **iOS:** inline opening + create, Draft → finalize, Depósito Dinheiro with inline opening, and a full cheque lifecycle on a concrete cheque (custody → deposit → return → re-present → conversion, version 0→5). Not run natively on iOS: the Draft-blocker round trip and the fiado sale/repayments (after the branch midnight the iOS cashbox held the previous day's turno, which the server refuses for today's writes until it is ended), and the plan form (its lower fields could not be driven by Maestro on iOS). Owned-stack tokens were raised to 15 minutes with the existing runtime tool for these journeys (renewal itself is covered by M2/M9).
- [ ] **9.4** Final-source native rerun of the seven M5 fixes on both OSs (not done; only the M5 close/Draft-blocker integration ran, on Android) and independent recheck.
- [ ] **9.5** Independent M6 re-review of the five corrections; physical-device, spoken VoiceOver/TalkBack and enlarged-text acceptance.

### Surface / test map

| Surface                 | Artifacts                                                    | Decisive tests                                                                                                                                   |
| ----------------------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Transactions list       | `src/ledger/list.tsx`, `contracts.ts#parseListFilter`        | server filter/paging, Member no OperatorId, unlinked no Mine, Draft blocker split and fresh clear                                                |
| Create / inline opening | `create.tsx`, `opening.tsx`, `common.tsx#useFinancialIntent` | type exclusions, fenced late preview, atomic opening, competitor/refused opening, unknown→remount→identical retry, receipt retention, fiado pair |
| Edit                    | `detail.tsx`                                                 | stale If-Match → reread → adopt, generated/other-operator read-only, Member no preview, event-locked cancel                                      |
| Plan                    | `plan.tsx`                                                   | accepted schedule, Draft create, atomic finalize with all versions, stale all-or-nothing                                                         |
| Statement / payment     | `statement.tsx`, `payment.tsx`                               | access before read, credit mapping, no default method, PIX vs Dinheiro legs                                                                      |
| Depósito Dinheiro       | `safe.tsx`                                                   | one two-leg command                                                                                                                              |
| Cheque                  | `cheque.tsx`                                                 | custody→deposit→return→re-present versions/keys, conversion debt without cashbox leg, unknown deposit single receipt                             |

**Boundaries:** M7 queue/approval, M8 reports and month lock, M11 identity/guided flows, the later banking increment (8.4) and server M9.8 people features.

**Defects found natively and fixed:** a preview sent before the inline opening was confirmed was refused and read as "unavailable" (now waits for the binding; the opening preview names the turno being opened); a cancelled or session-waiting preview showed "unavailable" (now re-asked); the opening hint changed height under the finger; **Nova transação** after a receipt silently stopped retaining input (fresh form per create); a client created from the form was cleared by the stale client list and the router's fresh form lost the typed values (fresh list read; validated device copy restored for review with the client); Receber pagamento/Extrato remounted mid-send during a self-context check, losing the receipt (last access decision kept while checking); short bottom sheets (a searched select) sat behind the Android keyboard (sheet now above it); the return sheet named R$ 0,00 after a deposit (now the cheque's face value). Each has a Jest regression.

**Limitations:** iOS Maestro on this simulator reports off-screen nodes as visible, so iOS journeys scroll with fixed swipes and rely on screenshots and API rereads for below-the-fold checks; the Android emulator needed one reboot (System UI/app not responding under load). Captures, flows and rereads were kept outside Git in a temporary folder that the system has since cleaned; the server-confirmed outcomes are recorded above, and the journeys regenerate from `tooling/native-ledger-flow.mjs`.

**Next action:** iOS native Draft-blocker and fiado journeys on a fresh branch day, the M5 seven-fix native rerun (9.4) and the independent review (9.5).

## Milestone 7 — Manager Work Queue & Approvals

**Scope and gates:** as Web M7. **Mobile-specific:** the comparison view on Daily-close approval adapts opening → closing → variance to a narrow layout per its blueprint.

## Milestone 8 — Reports & Month Lock

**Scope and gates:** as Web M8. **Mobile-specific:** report tables adapt to small screens (priority columns, row detail sheets) per the blueprints, with the same data and drill-downs.

## Milestone 9 — Time & Self-Service

**Status (2026-10-30):** implementation and local native verification complete in `paralell/app`, starting from `2301ad1`. External acceptance remains pending. M9 can precede M5–M8 under Web M9's dependency note. Ownership is mobile runtime, tests and disposable tooling only.

**Sources:** [catalog](../../design/docs/screens.md#clock-in--out), [blueprint](../../design/specs/screens/time-and-self-service.md), actual `TimeEntryController`, upsert/segment use cases, report resolvers and committed generated API. `src/time/` owns presentation/request safeguards; M4 retains session/route authority.

### Phase 1 — contracts, states and routing

- [x] **1.1** Inspect current checkout, owning contracts and M2–M4 navigation/session boundaries. Member tap uses Action; elevated writes use Segments. Lists have no segment fields; fresh details supply them. No time-write ETag/If-Match or financial idempotency header exists.
- [x] **1.2** Guard all four catalog surfaces and native detail before reads; preserve validated periods, filters and nested origins, including resource restoration.
- [x] **1.3** Verify role/context matrix, list pagination/order and the exact server span limit `DateTo − DateFrom ≤ 366` with inclusive endpoints. Member summary without link/accounts returns empty; balance without link returns no operators. Elevated summary requires mine or explicit operator; elevated balance with neither is team.

### Phase 2 — Member Ponto

- [x] **2.1** Branch-local today, linked identity, list totals/status plus detail pairs; explicit yesterday Close versus today Open; suppress pending duplicates.
- [x] **2.2** Foreground/day freshness, verbatim refusal and uncertain-outcome reread/review, including retained original date/action across restart and rollover; monotonic display-only elapsed time without live announcements.

### Phase 3 — manager attendance

- [x] **3.1** Current-month server-paged list with operator/status/mine/in-progress filters and in-progress-first; full-screen detail and return context.
- [x] **3.2** Day snapshot/status/new segments, granular edit/remove and confirmed whole-day removal. Retain active IDs; existing ClockIn is readonly in snapshot. No omission-as-delete. A confirmed create retires the outgoing form while canonical detail waits for its first fresh read.
- [x] **3.3** Locked-day read-only reason, audit identities, scoped retained input, stale/unknown outcome review. Fresh preflight is advisory, not an atomic concurrency guarantee. Acknowledgement/reread/navigation failures after success retain the confirmed receipt and require local reconciliation, never a failed-write label or replay.

### Phase 4 — transaction summary

- [x] **4.1** Own and explicit manager operator requests, server totals/category/generated subsets, inactive and empty states, neutral recorded-volume copy.
- [x] **4.2** Source-supplied window and validated return; Ver transações targets guarded M6 availability with mutually exclusive context.

### Phase 5 — period balance

- [x] **5.1** Own/team/operator wrapper, active zero-entry and inactive history as returned, server totals and in-progress notice.
- [x] **5.2** Exact day trail, Sem registro and per-returned-day target only; no historical Member tap or invented missing-day target. Nested return preserves context.

### Phase 6 — verification

- [x] **6.1** Role/generated-call/provider/UI regression tests for guards, writes, retention, stale/unknown recovery, report semantics, windows and navigation.
- [x] **6.2** Owned disposable API fixtures and actual iOS/Android journeys with distinct synthetic users; supported API rereads prove writes. Cold entry, lock/refusal/recovery, themes and enlarged text included.
- [x] **6.3** 31 tooling + 259 Jest/RNTL tests; workspace type/lint/format checks and final mobile rechecks; fresh both-OS production exports with proof flags enabled and exclusion/single-runtime gates.

### Phase 7 — completion

- [x] **7.1** Concise artifact/contract and actual verification notes; honest outstanding criteria and limitations. Proposed unsigned commit message stays in chat.
- [ ] **7.2 External acceptance:** independent read-only review, physical-device checks and spoken VoiceOver/TalkBack verification are pending; simulator interaction and accessibility-oriented unit tests do not establish them.

**Contract notes:** the catalog already flags canonical §6.7 replace-all wording as stale: shipped snapshots reconcile IDs. Granular correction is PUT despite an older use-case comment saying PATCH. No server/design/generated file is changed by this lane. Month-lock coordination and effective-dated targets remain server-owned.

**Excluded:** M9.8 lifetime carry-over, Pagamento/Ajuste de horas, payroll/employee statement/commission and financial close. Period balance is never labelled lifetime carry-over. Physical-device, spoken VoiceOver/TalkBack and independent acceptance must be reported separately.

**Artifacts:** `src/time/{clock,attempt,management,reports,contracts,resources}.tsx/ts`, guarded `/gestao/ponto/[id]`, and the existing M4 policy/provider/navigation. `test/unit/time-{contracts,ui,router}.test.*` and `test/unit/provider.test.tsx` cover generated calls, authority, branch/foreground fences, unknown/stale recovery, ID/ClockIn invariants, granular/day confirmation, lock/input retention, filters/paging, subsets, exact date boundaries, missing dates and nested origins/focus. The exhaustive native core oracle now covers all 107 public exports and the eight current custody/session label maps; historical M1 counts remain historical. Current native Hermes measurements passed **415/415 cases on each OS**, with their runtime Intl outputs captured outside Git.

**Local verification (2026-10-30):** real iPhone 17 Pro/iOS 26.5 simulator and Lotero_API36/Android 16 emulator, Maestro 2.11.0, existing development app and checkout-local .NET/PostgreSQL stack. Final same-source Manager create→canonical detail→granular correction→new-segment snapshot journeys passed continuously on both OSs with one-minute tokens and at least one successful credential renewal after canonical detail opened. UI assertions and checkpoint API reads agreed on ClockIn 05:45 and 1.25 closed hours; the final snapshot retained the original segment ID and added one new ID. The other OS app was stopped during each renewal measurement. Final native regressions also passed nested team/list/detail returns, manager own-hours return, selected-summary return, separate segment removals, status, whole-day removal and the Member overnight/tap/report return journey. Supported API reads confirmed corrected ClockIn 05:45, retained/new IDs, separate yesterday/today open state for the Android choice, both final closed days, automatic summary subset 2/1, zero-entry/inactive team members, policy-change targets, and deleted-day 404 plus absence from active list/balance. A real locked snapshot returned 409 with its verbatim message and unchanged persisted data. Unknown response/restart recovery is covered through generated transport/provider tests; no native packet-loss experiment is claimed.

Earlier owned cycles exercised both themes and enlarged text on locked screens (iOS XXXL, Android 1.5); font sizes and theme selection were restored. Keyboard-visible editing and overlay confirmations ran natively. Full nested report return passed at the original text size; the enlarged iOS report pass reached day detail and back to the report, with a later automation title/scroll assertion not completing. Captures, native measurements, API rereads and matching before/after runtime-source/server-build/OpenAPI identity are outside Git in the task-local M9 artifacts. Final `dist/m9-lifetime-final` iOS/Android exports used both proof flags; source-map and UTF-8/UTF-16 bytecode exclusion, single React 19.2.3/query context, and actual API/core inclusion gates passed. Real-Router/provider tests hold renewal responses while editing and while a granular write is in flight, preserving the buffer/confirmed receipt without replay. They also cover presentation-only address changes, branch/role rejection, and suspension/restoration of selectors and confirmations without a second native modal; later read failures retain the editor while blocking controls. Scope teardown explicitly disposes mutation-cache GC timers; the complete test command exits without forced exit or shortened cache lifetimes. A separate measured `time-ui` run passed 31 tests and exited with code 0 in 6.55 s. React act and development keyboard/measurement warnings remain observable.

**Reproduction:** start `tooling/native-shell-stack.mjs`, seed with `native-time-fixture.mjs` and `native-time-share.mjs`; the owned-only `native-time-runtime.mjs` preserves the build/database and defaults to one-minute tokens, with readiness checks and route/status-only HTTP logging. An explicit 1–15-minute override supports other experiments; longer tokens do not establish renewal-lifetime behavior. Generate native-touch flows with `native-time-flow.mjs`/`native-time-finish-flow.mjs`, use the observed-IME Android runner, and reread through `native-time-verify.mjs` (`context-matrix`, `overnight-open`, `locked-refusal`, `deleted-days` with pre-delete evidence). All state/output paths are private temporary paths, never repository fixtures. No external provider setup, OS/SDK, native patch, dependency pin or server/generated contract was changed.

**Next action:** independent read-only recheck of the current mobile diff and revised chat-only proposed commit message; retain physical/spoken verification for the device acceptance matrix.

## Milestone 10 — Admin & Configuration

**Scope and gates:** as Web M10. Products inherits gap AA only for deactivation warning/recovery (server DailyClose/Product; scheduling pending M7.7 close-out triage); ordinary Product CRUD and reads proceed.

**Status (2026-11-04): ALL NINE SURFACES IMPLEMENTED, locally verified; external acceptance pending.** Operators, Clients, Products, Holidays and Settings (2026-10-31, from `74bad83`) and Accounts, Account assignment, Categories & Transaction Types and Branch members (2026-11-04, see below). The guided M11 Add/Offboard/invitation-acceptance/link-request flows keep guarded, truthful availability.

**Sources:** catalog entries ([Operators](../../design/docs/screens.md#operators-admin) … [Settings](../../design/docs/screens.md#settings-admin)), [administration blueprint](../../design/specs/screens/administration.md), actual Operator/Client/Product/Holiday/Setting controllers and use cases, committed generated API. No server, design or generated file is changed.

### Phase 1 — integration correction, scope and routing

- [x] **1.1** Reconcile the app-owned label oracle with copy §5 (Safe, generated kinds, system roles, `SpecialDrawServiceFee`, three new event kinds and four new session maps) and cover all 111 public core exports; a discrimination test proves stale labels, missing values/maps and missing cases fail.
- [x] **1.2** Route the five surfaces through the M4 guard (Member refused before reads on management routes; Clients shared with the entering mode kept; statement guarded before reads). Verified views stay mounted through same-authority renewal. `year` joins the allowlisted presentation parameters.

### Phase 2 — Operators

- [x] **2.1** List with linked identity (active membership), **Sem login**, unresolved link and **você** from self-context; create/rename/link/unlink/deactivate with contracted fields only, verbatim conflicts, retained input and confirmed unlink/deactivation copy that never implies login, membership or employee removal.
- [x] **2.2** Self-link changes keep their receipt across the engine's scope rebuild and re-resolve self-context; row actions reach the real M9 summary with row return, Account assignment and M11 availability destinations.

### Phase 3 — Clients

- [x] **3.1** Complete list with local name search and announced count; all-role create/edit with masks, CPF digits, blank-to-null semantics and verbatim conflicts; Manager/Admin deactivation; statement gate; validated create-return to transaction-create origins.
- [x] **3.2** Confirmed receipts survive reread failure; unknown create outcomes block until an explicit reread shows possible matches; nothing is resent automatically.

### Phase 4 — Products

- [x] **4.1** Counting order, create/edit, accessible move up/down as individual updates (tie renumbering confirmed first), per-product partial/unknown outcomes with explicit continue/keep, reserved **Diferença Caixa** protection, AA-honest deactivation copy. A product set that changed since the list was shown sends nothing, reports it and blocks moves until a reread; an already-satisfied order is reported as such, never as saved.

### Phase 5 — Holidays

- [x] **5.1** Branch-year default, year selector, paged list without Source, manual batch, description edit with immutable date, deactivation with shipped-effect copy.
- [x] **5.2** Source/optional-federal selection, preview with type/source/**Já existe**, option change discards the preview and late responses, confirmation names year/source/options and states that import refetches, result counts and per-item status, verbatim provider outage, explicit retry only.

### Phase 6 — Settings

- [x] **6.1** Read-only lock date with Monthly reconciliation link; h/min inputs converting display units only (exact hundredths, imprecise minutes refused); no request for value-identical saves; target and lunch-only confirmations with branch-local effective date and old/new values.
- [x] **6.2** Branch day re-read before the dialog and before dispatch; strong in-memory If-Match; stale/unknown keep the candidate and require **Recarregar e conferir** plus explicit adoption; success adopts the returned ETag.

### Phase 7 — verification and delivery

- [x] **7.1** Jest/RNTL and tooling coverage listed below; full mobile gates.
- [x] **7.2** Native iOS/Android journeys against the owned stack with supported API rereads (details below).
- [ ] **7.3 External acceptance:** independent review, physical-device and spoken VoiceOver/TalkBack checks.

### Scope / test map

| Surface       | Artifacts                                                              | Decisive tests                                                                                                                          |
| ------------- | ---------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Guard/routing | `src/admin/surface.tsx`, `src/shell/screen.tsx`, navigation allowlists | `admin-ui` role/direct-entry/revocation, statement guard                                                                                |
| Operators     | `src/admin/operators.tsx`                                              | identities/você, verbatim link conflict, unlink confirmation + self-context recheck + receipt, summary return                           |
| Clients       | `src/admin/clients.tsx`                                                | Member create/CPF digits, search count, deactivation, receipt after reread failure, unknown outcome, retained/cancelled drafts, renewal |
| Products      | `src/admin/products.tsx`                                               | reserved protection, partial and unknown reorder, AA copy                                                                               |
| Holidays      | `src/admin/holidays.tsx`                                               | branch year/paging, changed-option preview invalidation, import params/results, 502 retry, manual batch                                 |
| Settings      | `src/admin/settings.tsx`                                               | no-op, both confirmations, If-Match, stale reconciliation, day rollover, precision                                                      |
| Contracts     | `src/admin/contracts.ts`                                               | `admin-contracts`                                                                                                                       |
| Oracle        | `src/probes/label-reference.json`                                      | `tooling/core-probe.test.mjs`                                                                                                           |

**Local verification (2026-10-31, correction round 2026-11-01):** 32 tooling + 314 Jest/RNTL tests (new `admin-contracts` 15, `admin-ui` 39, enlarged-text Button); workspace typecheck/lint/format; Shared API 103, core 308, Web 334 unit tests. Fresh `dist/m10-final` iOS/Android exports with both proof flags pass the exclusion/single-runtime/source gates and contain the admin sources. Native core oracle: **464/464 on iPhone 17 Pro/iOS 26.5 and Lotero_API36/Android 16** (Hermes).

Native journeys (Maestro 2.11.0, checkout-local .NET/PostgreSQL stack, loopback holiday provider, separate synthetic Admin/Member/spare users per OS) passed on both OSs: Member client create with masked phone/CPF and verbatim duplicate-CPF refusal without a deactivate action; Admin link of an unlinked operator, own unlink (confirmation, receipt, **você** gone), relink, M9 operator summary and return; client deactivation; product name conflict, create, edit and move-up; manual holiday, Composite preview, confirmed import with per-item results and the verbatim BrasilAPI 502; lunch-only and target confirmations; a concurrent API change producing the verbatim stale refusal, **Recarregar e conferir**, explicit adoption and saved reconciliation. Supported API rereads confirmed every persisted result, a settings version advance with the concurrent change kept, and the policy window: yesterday's attendance balance unchanged, today's recalculated under the new policy. Evidence, credentials and identity (`HEAD 74bad83`, OpenAPI sha256 `689c1608…`, no server/shared/design changes) stay outside Git.

**Correction round:** reorder against a changed product set no longer reports success; deactivation dialogs (and new Operator/Product editors) close only after their reread, so a reread failure keeps the confirmed receipt and its explicit recovery; `Button` caps itself to its container and wraps its label — at the largest iOS accessibility text size **Adicionar cliente** wraps fully on the iPhone 17 Pro (simulator text size restored).

**Defects found natively and fixed:** `IdentifierInput` dropped or duplicated digits at mask separators under fast iOS typing (controlled `selection` race; now imperative caret placement — earlier garbled API records remain as evidence); an explicitly cancelled editor left its device draft; a self-link receipt was lost when the engine rebuilt the scope; client search memory crossed engines; **Importar** sat under the dialog footer.

**Limitations:** iOS journeys ran on progressively corrected sources, Android on the final source (later edits are formatting only). With one-minute tokens a write meeting expiry is refused once and not replayed (observed natively); remaining journeys used five-minute tokens, renewal during editing is covered by Jest. Physical-device, spoken screen-reader, enlarged-text/theme native passes for M10 and independent acceptance are not done. Still open from this slice: M11 guided flows; gap AA (Products deactivation warning/recovery). The other four surfaces followed on 2026-11-04 (below).

**Next action:** see the remaining-surfaces section below.

### Remaining surfaces (2026-11-04) — Accounts, Account assignment, Categories & Types, Branch members

**Status: IMPLEMENTED and locally verified on both OSs; external acceptance pending.** Targets the committed v61 API (`abf6bf1`, OpenAPI sha256 `689c1608…`). The current backend reads report classifications at query time; the UI says so (`RECLASSIFICATION_POLICY` in `src/admin/categories.tsx`), to be reconciled after the server C1/C2 merge.

- [x] **8.1 Routing/guards.** The four availability routes dispatch native screens through the M4 guard and `AdminSurface`; Member is refused before reads; `accountId`/`operatorId` are fresh resource reads; `tab`/`section`/`status`/`page`/`focus` reuse allowlisted params. Dialogs stay mounted through a failed post-write reread so confirmed receipts survive; `useConfirmedWrite` records the server confirmation before any reread, so recovery never re-enables a confirmed command and closes the dialog once a reread succeeds. Add/Offboard employee stay M11 availability.
- [x] **8.2 Accounts** (`src/admin/accounts.tsx`). List/detail (shown first when requested)/edit with no type control; caixa create sends `createTabAccount: true` by default, `existingTabAccountId` alone, or an explicit `false` for **Sem conta fiado**, with optional terminal CAIXA; bank and cofre (`/account/safe`); create Tab, pair, unpair, terminal CAIXA reassignment (`PUT /terminal/cashbox/{id}`), deactivation stating the pairing release and custody refusal. 409/404 refusals render verbatim and block until **Reler contas e conferir**; unknown creates reread and list same-name matches without resending; a confirmed create stays unsendable through recovery.
- [x] **8.3 Account assignment** (`src/admin/assignments.tsx`). Operator picker from a fresh `GET /operator`; assign/unassign/set primary as separate commands; the paired Tab is a separate grant (**Atribuir também**); self-operator changes keep the receipt across the scope rebuild and recheck self-context; **Ver conta** opens the account detail with the assignment as origin.
- [x] **8.4 Categories & Types** (`src/admin/categories.tsx`). Category direction required (no default), rename only, deactivation states the type cascade and is disabled where system types live; types grouped by category in collapsible sections (focused type's group open); create/edit send every visible field (explicit None clears), immutable category, system categories excluded, fiado repayment kind only for In + Tab/client with no default, monthly-role direction rule explained, reclassification confirmation against the accepted reference (the server values just verified, adopted or kept over), advisory preflight reread reporting concurrent changes, system types read-only with their source.
- [x] **8.5 Branch members** (`src/admin/members.tsx`). **você**, role matrix (Manager never targets/assigns Admin), self and last-active-Admin reasons, role change after fresh authority/membership recheck on the exact membership id; **Remover acesso** opens the M11 offboard availability (no raw DELETE). Invitations: Pendente default, filter, paging, Member-only unlinked operator, issue/reissue (omitted body keeps, `null` clears, id replaces); HTTP result, SMTP acceptance and failed delivery reported separately (notice + toast); unknown issue rereads pending invitations; no cancel action (gap AC).
- [x] **8.6 Shared UI fix.** `Overlay` caps dialog and sheet height by the keyboard (`useKeyboardHeight`), so a tall dialog is no longer pushed above the screen with the keyboard open (reproduced natively on Android). Regression test in `ui/overlays`.
- [x] **8.7 Tests.** `admin-financial-contracts` (20) and `admin-financial-ui` (58): guards/revocation, pairing options, Safe, verbatim conflicts/busy/custody with reread gates, unknown outcomes and receipts after reread failure, recovery after confirmed creates/commands never resending, separate grants, self-context receipt, type constraints/omit-clear/reclassification (including after adopting or keeping over a newer server value)/concurrent change/grouping, role matrix/self/last-admin, failed/sent delivery, reissue bodies, renewal while editing, retained drafts, late response after revocation. Mobile 32 tooling + 393 Jest; workspace typecheck/lint/format and doc sync green.
- [x] **8.8 Native proof** (Maestro 2.11.0, iPhone 17 Pro/iOS 26.5, Lotero_API36/Android 16, checkout-local .NET/PostgreSQL + Mailpit stack). Tooling: `native-finance-fixture.mjs`, `native-finance-flow.mjs` (ten journeys), `native-finance-verify.mjs` and `LOTO_NATIVE_MAIL=unreachable` in `native-admin-runtime.mjs`. Both OSs passed: default-paired caixa, cofre, pair/unpair, a concurrent API pairing producing the verbatim conflict and reread, separate caixa/Tab grants with primary and self receipt, account detail and return, category with required direction, reclassification confirmation, duplicate-type refusal, explicit repayment kind, system types read-only, Member denial, Admin role change, pending invitations without cancel, issue with SMTP disclaimer, reissue clearing the operator, offboard availability, Manager role matrix, failed delivery under unreachable SMTP. Supported API rereads: **34/34** checks across both OSs. Fresh production exports with both proof flags pass the bundle gate and ship the four new admin sources.
- [ ] **8.9 External acceptance**, physical-device and spoken screen-reader checks, enlarged-text/theme native passes for these surfaces; M11 guided flows; gap AC.

**Limitations:** Android ran the full sequence after the `Overlay` fix; iOS ran accounts/race/assignment before it and re-ran types, members, denial, manager and failed delivery after it (the accounts pairing journey needs fresh fixtures to repeat). Later journey-tooling edits (iOS picker/header handling) were not re-run on Android. Journeys used 15-minute tokens after a 5-minute token expired mid-write and was correctly refused without replay; renewal while editing is covered by Jest. The iOS 26.5 XCTest driver intermittently failed hierarchy requests (HTTP 500) and needed a simulator restart; these are tooling faults, not app crashes. A searchable picker inside a dialog lost its sheet under the iOS keyboard; it is not used (no shipped screen combines them), and the `Overlay` keyboard fix above was not re-checked against that combination.

**Next action:** independent review of the remaining-surfaces change; after the user's server C1/C2 merge, regenerate and adapt the classification copy and wiring to the actual contract.

## Milestone 11 — Account & Onboarding

**Scope and gates:** as Web M11, minus the internal **Plataforma** area unless design 0.4.6 places it on mobile. **Mobile-specific:** Continuar com Google through the native sign-in flow; invitation and password-reset links open the app through the URL scheme or universal/app links when installed.

---

## Milestone 12 — Store Release

**Goal:** Ship the app to the stores: build profiles per environment aligned with server M9, final bundle identifiers, signing, store listings in pt-BR, the over-the-air update policy (what may ship as an update versus a store release), crash reporting, and a device test matrix for the spine flows.

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires — including every flow-alignment batch (B, C, D).
