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

**Status:** Implementation of Phases 1–6 is complete and implementer-verified on iOS 26.5 and Android 16 simulators/emulators. Independent review is pending; the physical-device, spoken screen-reader and Impeccable passes listed under *Open gates* did not run.

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

| Contract | Artifact (`src/ui/…`) | Decisive states/behaviors | Tests (`test/unit/ui/…`) | Gallery |
|---|---|---|---|---|
| button.md | `button.tsx` | six variants, link role, disabled with reason (guarded, hint), loading verb as spoken name, stable width | `controls`, `overlays` | Button |
| form-field.md | `form-field.tsx` | field only adds disabled/required/invalid/read-only; label focuses control; grid stacks with text size | `controls` | FormField e Input |
| input.md | `input.tsx`, `input-logic.ts` | password toggle, search clear + polite count, textarea counter, read-only text; progressive identifier masks with caret and separator deletion | `controls`, `input-logic` | FormField e Input |
| money-input.md | `money-input.tsx`, `input-logic.ts` | blank ≠ zero, right-to-left cents, 15th digit refused, replacements and pastes through `parseBrl` (malformed ones rejected, value kept), spoken value, disabled/read-only never emit, fast native typing/deletion | `controls`, `input-logic` | MoneyInput |
| select.md | `select.tsx` | groups, inactive only when flagged, no default, searchable (accent-insensitive, complete list), loading/failed/empty, out-of-options marker, disabled never opens | `controls`, `overlays` | Select |
| date-picker.md | `date-picker.tsx` | typed text kept, contract error, branch today/bounds, 0001–9999 navigation, header reflow at large text, range, day stepper, month waits without reference, `HH:mm` following external values; every member inherits FormField disabled/read-only | `controls` | DatePicker |
| choice-controls.md | `choice.tsx` | indeterminate, radio without default, switch for device preferences only, hidden-label table selection | `controls`, `feedback` | Checkbox, Radio e Switch |
| badge.md | `badge.tsx` | copy §5 labels and domain tokens by exact enum key, unknown raw/neutral/reported, Automática, feedback, mode | `feedback` | Badge |
| card.md, summary-strip.md, detail-list.md | `layout.tsx` | next-action region, checklist; unavailable figure is — never zero, signed tones with spoken sign; identity/provenance/restriction rows | `feedback` | Card, SummaryStrip e DetailList |
| table.md | `table.tsx` | shared column widths scaling with text, labelled horizontal region, sticky action column aligned per row, whole-row press vs explicit link vs nested actions/selection, loading/empty/error/updating, grouped/expandable lists | `feedback` | Table/List… |
| filter-bar.md, pagination.md | `filters.tsx` | staged apply, applied-scope summary, Filtros (n) sheet (inline inside a dialog); envelope-driven buttons, range text, out-of-bounds page corrected | `overlays`, `feedback` | Table/List… |
| modal-sheet.md, menu.md, tabs.md | `overlay.tsx`, `navigation.tsx` | one dismissal path, busy not dismissible, focus to first field/title and back to trigger, nested layers in one modal, Android back topmost-first; menu radio group, unavailable items with reason; tabs/segmented | `overlays`, `theme` | Modal/Sheet, Menu e Tabs |
| notice.md, toast.md, save-status.md, skeleton.md, empty-state.md, stepper.md, disclosure.md, page-header.md | `feedback.tsx`, `layout.tsx` | verbatim ordered messages, no-body fallback, uncertain recovery, error summary links; toast ≥5 s, pause, per-message budget, above an already-open keyboard; every SaveStatus text; static skeleton under reduced motion; four empty kinds; confirmed step statuses; disclosure state; header focus/return | `feedback`, `overlays` | Notice, Toast… |
| tokens/README.md | `src/theme/*`, `tooling/token*.mjs` | resolution/validation, parity, primitive-free output, theme lifecycle | `theme`, `tooling/tokens.test.mjs`, `tooling/colors.test.mjs` | Tema e tokens |

### Verification summary

- **Automated:** Mobile 30 tooling tests (token resolution/rejections, parity, primitive-free output, invalid class consumption, primitive references, gallery leak by path and UTF-8/UTF-16 strings) and 156 Jest/RNTL tests (69 new for theme/components); workspace frozen install, typecheck, lint (incl. token drift/consumption), format, Shared API 103 + core 296, Web 9 + 246, `generate:check` (287 files), real v58 doc-sync + 34 regressions, design-doc full/components + 14 negatives, contrast 1,164 pairs + 40 negatives.
- **Bundles:** development and production exports for both OSs; the production gate passes with proof flags set and rejects a deliberately leaked gallery (`technical probe module in production map`). Production ships the theme and the UI modules routes use; the gallery appears only in development maps.
- **Native:** dev clients on iOS 26.5 (iPhone 17 Pro simulator) and Android 16/API 36 (emulator) loaded fresh bundles from an owned Metro on port 8083 (project root verified). Maestro gallery journeys passed end to end on both OSs: theme override and return to Sistema, button states, money entry (right-to-left, deletion to blank, recorded zero, disabled unchanged), select sheet and disabled select, dialog with an in-modal nested select, busy confirmation that Back/close cannot dismiss, toast, branch-bounded calendar, table row versus nested action; on Android additionally Back closing the nested layer, then the dialog, then section → gallery → home, under OS reduced motion. Screenshots covered light/dark and the largest iOS text size, including the calendar sheet reopened at that size (month controls, year field and two-digit days whole), and a toast shown and replaced over an already-open keyboard. No new crash report on either OS.
- **M2 regression (root providers changed):** on an owned disposable PostgreSQL/Mailpit/.NET stack with fake identities, the real native login → identity → branch/clock, retained note, held intent, lost response → cold restart → exact replay flow passed on both OSs, each verified as one durable result from two dispatches with one key, one body hash and matching ETag. Stack, proxy, Metro and emulator were stopped afterwards.
- **Native rules** that the device runs established (pressed-state fills, order-independent money entry, keyboard insets, screen-reader-only focus, in-modal layers) are kept as current guidance in `mobile/AGENTS.md` § *M3 design system*.

### Open gates and risks

- Independent review of this milestone.
- Not executed: physical-device touch, spoken VoiceOver/TalkBack journeys and the named Impeccable tool (not installed; the contract checklist and screenshot pass were used instead).
- Native residual risks from Mobile M1 remain (Android `MountingCoordinator` SIGSEGV history, iOS `objc_storeWeak` SIGABRT); none recurred during these runs, which does not establish elimination.
- Controlled formatted text fields can still drop one keystroke when deletions arrive with no inter-key delay (automation-speed `eraseText`); the editing logic itself is order-independent and human-speed input passed.
- `INVALID_TIME` (**Hora inválida. Use hh:mm.**) mirrors the cataloged date message; the copy guidelines do not yet define a time-field message.

**Next step:** Mobile M4 (Entry & Navigation Shell) on these components, after Web M4's preconditions per the shared numbering.

---

## Milestone 4 — Entry & Navigation Shell

**Scope:** as Web M4. **Mobile-specific:** the navigation container and mode switch follow the shell blueprint's small-screen layout; the Android back button and swipe-back respect the catalog's return targets; deep links from the URL scheme pass the same gate.

## Milestone 5 — Operator Day

**Scope and gates:** as Web M5 — the mobile-first flows of the catalog. This includes gap AA on Close day and Fix & resubmit (retained-retired-product edit/preview/save recovery only; server DailyClose/Product, scheduling pending M7.7 close-out triage); other screen behavior and historical reads proceed. The catalog’s gap → screen map remains authoritative. **Mobile-specific:** the "once per device and branch-local day" terminal choice is stored on the device; Close day's count entry is tuned for numeric keypads and survives the app being backgrounded mid-count. **Flow-alignment (Batch B):** the blind count, Conferir/Recontar, inline opening and *Passar o caixa* follow Web M5; the arithmetic money input works with the numeric keypad (operators need `+`), and a backgrounded app never reveals the difference before Conferir.

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

**Precondition:** Server M9 (staging/production environments) and the MVP feature milestones that release scope requires — including every flow-alignment batch (B, C, D).
