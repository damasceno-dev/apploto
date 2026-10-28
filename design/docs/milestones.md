# Lotero — Design Milestones

> **Status:** Active
> **Draft started at** 2026-06-10
> **Planned start:** 2026-06-30

**The output of the `design/` folder is the Lotero design system** — the non-runtime source of truth that `web` and `mobile` both consume: **tokens, the component contract, screen blueprints, and the formatting/copy specs.** The milestones split into two halves:

- **Discovery & direction (M0–M1):** understand *what every screen is* (M0, semantic) and decide *how it should look* (M1, visual direction). These feed the system; they are not the system.
- **The design system (M2–M4):** **tokens** (M2) → **component contract** (M3) → **screen blueprints** (M4). This is the durable deliverable web/mobile build against.

Detailed phases with checkboxes are written just-in-time when a milestone starts, following the `server/docs/milestones.md` convention, and **executed phase by phase** so progress is followable.

### Owners

- **M0, M2, M3, M4 are AI-built, reviewed by the dev team, one phase at a time** — checkboxes track progress.
- **M1 (Visual Direction) is dev-team-owned** — it drives external generator tools (Claude Artifacts, v0, Google Stitch, Figma, Pencil) and is taste-driven, so it carries **no AI checkboxes**. Its inputs and required outputs are described, and those outputs are the precondition for M2.

### Standing rules

- **`design/` stays non-runtime.** Token JSON, markdown specs, references, and validation scripts only. No React, no Tailwind/NativeWind code, no component implementations, no build artifacts.
- **The design system is the output; M0–M1 feed it.** Tokens/components/blueprints/specs are the durable contract; the M0 catalog and the M1 direction are the inputs that shape them.
- **Semantic → visual → system.** What a screen *contains and does* (M0) is locked before *how it looks* (M1), which is locked before tokens encode it (M2). M2–M4 should lean heavily on the M0 catalog and the M1 direction rather than inventing.
- **Tokens are consumed directly, not via `shared/`.** `web` reads `design/tokens/*.json` through its Tailwind preset and `mobile` through its NativeWind theme, both **at build time**. Tokens are build-time data, not runtime TS, so they do **not** pass through `shared/` (which stays runtime API + helpers). `design/` exports nothing actively and never imports from `web`/`mobile`.
- **Three-layer token model:** *primitive* → *semantic* → *domain* (one token per colored backend enum value). App code consumes **semantic and domain tokens only** — never primitives, never raw hex. Each app ships a lint gate banning raw hex / primitive refs, with one exemption: the app's token-adapter module (Tailwind preset / NativeWind theme).
- **Light and dark from the start.** The semantic layer is keyed under both `light` and `dark`; the system ships **both themes from M2**. Structuring for both now is cheap; retrofitting dark later is not.
- **`references/` holds inputs, not implementations.** Park prompts, screenshots, exported screens, PDFs, links. **Never** vendor generated React/Expo projects or `node_modules` under `design/`. A prototype is referenced by link/snapshot.
- **Doc sync is scripted.** The domain doc-sync group now lives at repo-root `docs/`, validated by `docs/check-loto-doc-sync.sh`. The check also requires exactly one `Synced against loto-specs: vNN` marker in each of `product.md` and `screens.md`, equal to the canonical revision. A backend spec bump fails until the catalog and gap ownership are re-reviewed and both markers updated (0.4.3); matching markers do not replace semantic review. `bash docs/test-loto-doc-sync.sh` exercises current/stale/malformed/missing markers and preserves the canonical metadata gates in disposable fixtures.
- **pt-BR is the product language.** User-facing copy guidance is pt-BR; doc prose may be English.

### Milestone overview

| #      | Name                               | Owner        | Output                                                                                             |
|--------|------------------------------------|--------------|----------------------------------------------------------------------------------------------------|
| **M0** | Product & Screens                  | AI           | `product.md`, `screens.md`, `formatting-ptbr.md`, `copy-guidelines.md` (semantic + conventions)    |
| **M1** | Visual Direction                   | **dev-team** | `decisions.md` (chosen look + palette tone, light+dark) + exported screens parked in `references/` |
| **M2** | Design System — Tokens             | AI           | `design/tokens/*.json` (light+dark), contrast gate                                                 |
| **M3** | Design System — Component Contract | AI           | `design/specs/components/*`                                                                        |
| **M4** | Design System — Screen Blueprints  | AI           | `design/specs/screens/*` (visual layout per screen)                                                |

---

## M0 — Product & Screens

**Goal:** Understand the system well enough to define *what every screen contains and does* — semantically, with **no visual design** — plus the pt-BR display/copy conventions. This is the brief M1, M2–M4, and every feature build consume.

**Scope boundary:** Semantic definition + conventions only. No colors, no layout, no tokens, no generators. `product.md` **must not restate** the domain spec — it points to it.

**Precondition:** None. Reads the backend (all controllers, response DTOs, `loto-specs.md`) but writes nothing under `server/`.

### Phase 1 — Product framing

- [x] **0.1.1** `design/docs/product.md` — lean frontend framing: roles → UI gating, two-stage token model in UI terms, the **mobile-clones-web** rule, a screen *index*. Points to `loto-specs.md` §2/§7; does not restate them. No version labels. *(Reviewed baseline in 0.4.3; pending addenda remain gated.)*

### Phase 2 — Display & copy conventions *(these double as M1 tool inputs)*

- [x] **0.2.1** `design/specs/formatting-ptbr.md` — BRL (`R$ 1.234,56`, signed variance `+R$ 12,00` / `−R$ 12,00`), date `dd/MM/yyyy`, datetime, time `HH:mm`, CPF/CNPJ/phone masks, decimal-comma. The contract `shared/core` (Shared M3) implements and that M1's tool prompts cite.
- [x] **0.2.2** `design/specs/copy-guidelines.md` — tone, sentence-case, a terminology glossary (Fiado, Diferença Caixa, fechamento do dia, cheque pré-datado, bolão…) mapped from `loto-specs.md`, and the error-display rule (`ResponseErrorJson.ErrorMessages` rendered verbatim).

### Phase 3 — Semantic screen catalog

- [x] **0.3.1** `design/docs/screens.md` — the per-screen template (Purpose · Primary job · Access · Permission fallback · Default view · Data shown *with per-field source tags `[DTO]`/`[derived]`/`[composed]`/`[gap]`* · Actions · Audit/lock · States · Navigation) + the full screen list by area. Visual hierarchy and deep-link state preservation are explicitly deferred to M4.
- [x] **0.3.2** Define the spine screens in full: **Manager Work Queue** (exception-first, reframed from "Dashboard"), **Daily-close approval** (comparison framing, system-calc vs operator-entered marked), **Transaction — Create (fast entry)** + **Transaction — Edit (correction/audit)** (split into two), and **Operator Day Cockpit** (operator next-action home).
- [x] **0.3.3** Define the remaining screens, area by area, each mapped to its controller(s) + response DTO fields, every data field source-tagged.

### Phase 4 — IA + cross-screen patterns + close-out

- [x] **0.4.1** Add the **navigation map / IA** into `screens.md`: how screens link, the branch-session gate, role-based nav differences (Member vs Manager/Admin) — including the **manager-as-operator dual-mode decision** (gap R: explicit **Gestão ↔ Meu turno** switch for linked Manager/Admin; management-only setup explanation when unlinked; mode changes navigation/home context, not token role or server permissions).
- [x] **0.4.2** Add the shared **cross-screen patterns** into `screens.md` (semantically): error display, list pagination, empty/loading conventions, auth-tier gating, **session/token expiry** (silent-renew failure → re-login → return-to-context; branch-token drop on switch), **server-authoritative branch date/time**, **idempotent submits / uncertain-save-outcome handling**, unsaved-changes + concurrency conventions (decided: unsaved input is always kept on the device and restored on return; Close day additionally autosaves to the server on today's plain Draft — `screens.md` Conventions + Close day), the explicit **online-only vs offline** decision (an online-only MVP is acceptable — make it a decision, not an accident), and **screen-level accessibility patterns** (focus order, touch targets, error announcement). **Server dependency delivered in M7.7 Phase 6 (spec v39):** branch time comes from `GET /branch/current`; transaction/installment creates use persisted 24-hour `Idempotency-Key` replay; guarded mutations use strong quoted-decimal `If-Match`/ETag PostgreSQL `xmin` with verbatim stale 409 keys. Authored and source-checked in D01 (2026-10-15): the cross-screen section consumes those exact contracts, including global-token renewal → branch-session recreation, isolated durable input, uncertain outcomes, close autosave/undo, and accessible parity. Restore input after a fresh authenticated login using the current returned-email scope plus fresh branch/permission checks; gap U retains its original id-comparison features only. Rotation/single-flight recovery, 5xx uncertainty and live-role reconciliation are explicit. These seed M3/M4; runtime implementation and independent acceptance are separate.
- [x] **0.4.3** Add `Synced against loto-specs: vNN` markers to `product.md` + `screens.md`; mark both **Reviewed baseline** — a close-out with explicit *decision-blocked* entries (every screen named in the catalog's **gap → screen map** stays draft until its gap's decision lands and the entry is re-synced; the map is authoritative, inline pointers are mirrors), **not** a claim that all semantics are final. M1 may now start. Requires 0.4.5 done (gap P is Work-Queue semantics, not visuals). **D02 (2026-10-15):** markers align at v43; the expanded checker and its disposable regression suite preserve the original three-file gates. The D02 implementation audit checked 33 screen templates, IA/role/mode/entry/return, source attribution, lock/states and per-feature ownership against actual contracts. Source tags, an empty state and stale revision prose were reconciled. Newly exposed retired-product count edit/preview/save recovery stays explicitly blocked as gap AA, owned by server DailyClose/Product with scheduling pending M7.7 close-out triage. This records the implementation audit, not acceptance of the current review. The proposed baseline permits M1 visual direction after reviewer acceptance; it does not implement pending contracts or close deferred 0.4.6.
- [x] **0.4.4** Reconcile `product.md` with the 2026-07 review findings: the identity-tier "profile" promise has no cataloged Profile screen (gap N — profile / logout / password recovery are uncataloged and unserved by the contract, now scoped into server M8); clarify that **mobile clones web** means the same screen *set and semantics*, with platform-appropriate interaction patterns. **Authored and checked in D01 (2026-10-15):** framing now distinguishes role/mode, Member scopes, unlinked-manager close access, manager time-entry snapshots, app-owned transport/storage, M8 auth/Plataforma and M11 deferred reports from current promises.
- [x] **0.4.5** Resolve **gap P** in the catalog before the 0.4.3 baseline: define the Work Queue default-day rule (no silently borrowed due-date business-day convention; whether Saturdays/Sundays count as operating days is itself part of the decision — the system has no universal operating calendar), the cross-date pending-backlog presentation, and the Draft-count composition (scope to the earliest unlocked month and drop the extra call, or define its range). **Done:** today is literal and never switched automatically; a pending-backlog banner covers earlier unlocked days; the Draft count spans every unlocked day up to today (both served by the dashboard aggregate — gap W, server M7.7 Phase 7.5); the queue's group order is fixed — gap P is resolved in `screens.md`.
- [x] **0.4.6** Catalog the **auth & account screen set** against the shipped server M8 contracts (Phases 1–7) and M7.7 decision 1.8. **Authored 2026-10-20 (D04); semantics independently confirmed; corrections closed by owner instruction 2026-10-22:** nine new entries — Session menu, Profile, Change password, Forgot password, Reset password, Invitation (preview / password register / Google / existing-account accept / decline), Plataforma — Nova lotérica (wizard, lookup by request id, owner-invite reissue), Add employee and Offboard employee (guided, composed from separate commands with honest partial completion) — and reconciled Login (**Continuar com Google** with the link-confirmation step), Branch picker (empty state, Plataforma entry), Manager Work Queue (owner setup checklist composed from existing reads, no invented readiness field), Operator Day Cockpit (link-request status), Operators (request resolution), Categories & Transaction Types (`FiadoPaymentKind`/`SystemRole`) and Branch members (invitation list/reissue). A new cross-screen section fixes capability-link privacy, account-wide session ends, generic login failure, fresh platform authority and membership guards. Gaps C/N leave the blueprint map (launch-only provider gates stay with server M8 Phase 8); **AB** (no onboarding list) and **AC** (no invitation withdrawal) qualify single features. Product framing, the screen index (42 entries) and copy (§4 glossary, §5 labels for every displayed shipped enum including the previously missing `MissingExpectedClose`, §7.4 microcopy) follow. No spec bump: this reconciles design to shipped facts at v54.

> **M0 status (2026-10-27):** 0.4.1–0.4.6 are checked; whole-M0 completion is not declared. The product/catalog are the reviewed baseline (D01 0.4.2/0.4.4 and D02 0.4.3, 2026-10-15); every screen in the catalog's gap → screen map stays gated until its decision lands and the entry is re-synced — AA (retained retired-product edit/preview/save recovery, server DailyClose/Product, scheduling pending M7.7 close-out triage), AB (no onboarding list) and AC (no invitation withdrawal). The v56/v57 catalog reconciliation was independently accepted (`ea26814`). 0.4.6, M3 3.5.2 and M4 4.8.5 were closed by the owner's explicit 2026-10-22 instruction after an independent semantic review confirmed them and the verified corrections landed (admin labels Recebível / Relatório financeiro mensal and Sem classificação / Nenhuma, the current canonical reference, the 45-entry coverage inventory); no separate final reviewer report exists. Both design markers now read v58 (see the Phase 9 marker disposition).
>
> | Criterion | Behavior / artifact reconciled against source | Gate |
> |---|---|---|
> | M0 0.4.6 | Nine account/employee entries and routes/scopes; M8 capability privacy, generic failures, uncertain recovery, fresh platform authority and membership-first offboarding | Closed 2026-10-22 (owner instruction after semantic confirmation) |
> | M3 3.5.2 | Input/FormField retain non-secrets only; password clearing stays screen-owned; capability retention is stricter; no persistence of assertions/challenges | Closed 2026-10-22; running galleries are 3.5.3 (apps) |
> | M4 4.8.5 | Membership-first journey preserves ids, verifies invitations after removal, reports partial completion; no inferred product-active marker; operator drill-down carries its id | Closed 2026-10-22 |
> | M4 4.8.4 | Dashboard, Upcoming settlements and Monthly financial blueprints plus D-M2-1 IA/shell reconciliation | Implemented; independent acceptance pending (see 4.8.4) |
> | M4 4.8.2/4.8.3/4.8.6 | AA, AB, AC remainders; real-app/native verification | Owner contract/product/app prerequisites |
>
> The D04 financial reconciliation originally used canonical v55 with server `07cdb76`; its Q3/Q9/Q5 assumptions and AA/transition limitations are reconciled separately. Checker success proves references/coverage, not semantic acceptance.
---

## M1 — Visual Direction ·  *Owner: dev-team (external tools — no AI checkboxes)*

> **Owner selected and closed M1 exploration (2026-10-20): the existing Claude prototype is the locked direction.** The owner judged that GPT and Google did not improve on its appearance and asked for the implementation handoff. This supersedes the October 18/19 pending-selection record; the choice is now explicit, rather than inferred from old screenshots. [`decisions.md`](decisions.md) records the choice and scope, and the [implementation handoff](../references/claude/implementation-handoff.md) provides observed style inputs, 33-screen static coverage and required adaptations. M2 may start from this direction; its criteria remain unchecked. Dashboard will inherit the direction when its actual reporting contract/catalog blueprint is ready, rather than hold the visual choice open. No Dashboard design or financial definition was invented or accepted. Other backend, Shared, per-feature and launch gates remain in force; technical review of this new extraction remains separate from the owner's taste decision.

**Goal:** Decide *how it looks*. Explore directions in the generator tools against the M0 catalog, pick one, and lock the **palette tone** (inspired by Caixa blue/orange, with our own character — light **and** dark intent). This milestone feeds M2; it produces no design-system files itself.

**Why no checkboxes:** this is taste-driven work done in external tools, by the dev-team. The milestone's job is to make the **inputs** explicit and pin the **outputs** that M2 consumes.

### Inputs to feed the tools *(reference the live system — do not snapshot dead data)*

For each screen the dev-team prototype, attach/paste:
1. The screen's entry from **`screens.md`** (purpose, data, actions, states).
2. The relevant **`loto-specs.md`** sections (domain rules for that screen).
3. A **real example response** for the screen's endpoint — grab it live from **Swagger / the running API** (don't hand-author a snapshot file).
4. **`design/references/caixa-palette.md`** (color inspiration) + your design adjectives.
5. **`formatting-ptbr.md`** (so money/dates render correctly).
6. The three **reference dashboards** (Rasket / Aune / SciFi) in `design/references/dashboards/` — visual inspiration to borrow from / react against (these are *inputs*, not something the milestone produces).
7. **Review feedback on the M0 clickable prototype:** keep corners restrained (≤ 4px), prefer hairline dividers to boxed cards, and avoid pill-shaped or heavily tinted highlight cards — they read as improvised rather than finance-grade.

### Suggested prompt template

> *"You are designing one screen of **Lotero**, an internal **pt-BR** management web app for a Brazilian lottery house (lotérica). Audience: a [manager / operator]. Build the **[SCREEN NAME]** screen.*
> *Data + behavior: [paste the `screens.md` entry]. Example API response: [paste from Swagger]. Required states to show: loading / empty / error / success.*
> *Visual tone: [your adjectives] — calm, dense, finance-grade, light theme (also show a dark variant). Money as `R$ 1.234,56`, dates `dd/MM/yyyy`, tabular numerals in tables.*
> *Color inspiration (do NOT copy the Caixa logo/clover): [paste palette]. Use our own neutral mark placeholder.*
> *Output a single responsive screen (works desktop → mobile, since mobile clones web)."*

### Process (the dev-team call)

- The visual-tools study used the owner's Rascunho guide and real API inputs for the available screens. On 2026-10-20 the owner ended exploration and chose the existing Claude prototype; do not require another tool comparison or whole-backend completion to begin M2. Preserve current contract/per-feature prerequisites for actual feature implementation.
- Compare **operator task speed · manager info density · accessibility & legibility · web↔mobile adaptability · brand fit without copying Caixa · Tailwind/NativeWind feasibility.** Record only observations and comparisons actually made; screenshots alone do not establish accessibility or native readiness.
- The owner's explicit 2026-10-20 choice locks the visual direction. Historical screenshots alone did not establish that choice; the current record names its authority. New extraction details remain technically reviewable, and app accessibility/functional acceptance still needs real implementation evidence.

### Outputs (these are M2's precondition)

- **Chosen screens** — [selected Claude reference set](../references/claude/selected-2026-10-20/README.md), with source/capture fingerprints, all 33 screen paths and additional theme/responsive/state examples. October 15 captures remain historical evidence; generated runtime code stays outside the checkout. These are fictional prototype layouts, not live API or native-device acceptance.
- **`design/docs/decisions.md`** — records the owner's chosen direction and the [M1→M2 handoff](../references/claude/implementation-handoff.md): light/dark palette, typography/zoom distinction, density, geometry, component/navigation patterns, measured inputs, contrast findings and adaptations required by current contracts. The owner has locked the direction; independent technical review of the new extraction is not claimed. No M2 tokens or component implementation are delivered by M1.

---

## M2 — Design System: Tokens

**Goal:** Encode the M1 direction into the complete non-runtime token source both apps consume — colors (light + dark, with domain status colors per colored backend enum), typography, spacing, radii, shadows, breakpoints/touch, motion, and z-index layers.

**Precondition:** **M1 locked** — the palette tone and visual direction are decided, so tokens encode a real choice. M0's catalog says which domain tokens are actually needed.

**Key behaviors:**

- **Start from M1's output, don't re-decide.** Seed the token JSON from the chosen direction (and any token/Tailwind export the tool produced), then **curate** it into the structured three-layer JSON. The tools accelerate the first draft; M2's job is to structure, dedupe to ramps, add domain tokens, and pass the contrast gate — not to invent fresh.
- **Format:** plain nested JSON, one file per category, shape documented in `design/tokens/README.md`. Not W3C DTCG — a ~50-line dependency-free Node adapter must resolve it.
- **Light + dark** semantic layers ship together.
- **Domain tokens are first-class** — one token per colored enum value (spec §4), named identically to the enum value.
- **References, not duplication:** semantic/domain tokens point to lower layers via `{path.to.token}`.

### Phase 1 — Architecture & conventions

- [x] **2.1.1** Create `design/tokens/` and write `design/tokens/README.md`: the three-layer model, the naming convention (dot-path, e.g. `color.semantic.light.surface-raised`), the `{path.to.token}` syntax, the build-time consumption rule (apps read JSON via their own adapters; only semantic/domain may be referenced), and the light+dark theme structure. *(Folders are created as files land — no empty scaffolding.)* *(D04: model, dot paths, whole-value references, build-time consumption and theme structure in `design/tokens/README.md`; semantic groups nest, so the example reads `color.semantic.light.surface.raised`.)*
- [x] **2.1.2** Document the JSON file shape: one file per category (`colors.json`, `typography.json`, `spacing.json`, `radii.json`, `shadows.json`, `breakpoints.json`, `motion.json`, `layers.json`). *(D04: eight files with one top-level group each; units and per-platform interpretation tabled in the README.)*
- [x] **2.1.3** Base the color work on the parked inputs — `design/references/caixa-palette.md` + the locked tone in `decisions.md` + the tool's exported styles. Do not create a new identity reference here. *(D04: ramps derived from the handoff's observed anchors and the Caixa reference; no new identity reference.)*

### Phase 2 — Color system (light + dark)

- [x] **2.2.1** Primitive layer in `colors.json`: brand-blue, brand-orange, neutral, plus green/red/amber/sky feedback ramps (~10 steps each), derived from the M1 tone. *(D04: blue, orange, neutral, night (dark surfaces), green, red, amber, sky and teal ramps plus named translucent overlays.)*
- [x] **2.2.2** Semantic layer under **both** `light` and `dark`: `background`, `surface`, `surface-raised`, `border`, `text-primary/secondary/muted`, `text-on-primary`, `primary`/`primary-hover`/`primary-active`, `accent`, `focus-ring`, and `success`/`danger`/`warning`/`info` as `-bg`+`-text` pairs. All `{...}` references into primitives. *(D04: both themes with identical names; actions carry hover/active/disabled, plus focus, nav, top bar, overlay, shadow and skeleton roles. Corrected the four handoff failures and three more found by the gate: light turquoise text, light muted text on read-only fills, and feedback text on selected rows.)*
- [x] **2.2.3** Domain layer: one token per value of every colored enum (spec §4) — `TransactionStatus`, `DailyCloseStatus`, `Direction`, `AgingBucket` (severity ramp), `TimeEntryStatus` — for both themes. *(D04: the five named enums plus InstallmentPlanStatus, InvitationStatus, MailDeliveryStatus, OperatorLinkRequestStatus and DailyCloseReviewEventType, each matched value-for-value to the committed OpenAPI enum; signed cash variance, fiado balance, hour balance and the generated-movement marker. Direction is deliberately neutral.)*
- [x] **2.2.4** `scripts/check-contrast.mjs` (repo-level): zero-dep Node script that resolves references and verifies every text/bg + badge pair meets WCAG AA in **both themes**. Doubles as the JSON/reference validator. Run green. *(D04: `node scripts/check-contrast.mjs` — 727 tokens, 912 combinations, all green (lowest text 4.63 light / 4.61 dark; lowest essential boundary 3.04); action states and domain surfaces are generated and required by the checker, so a trimmed list or missing state fails; `node scripts/test-check-contrast.mjs` — 34 disposable scenarios, source hashes unchanged. Review fixes: ghost/danger pressed fills, selection indicator for selected rows.)*
- [x] **2.2.5** Domain-token table in the README. *(D04: domain table with labels, both themes and lowest ratios.)*

### Phase 3 — Typography, spacing, geometry, motion, layers

- [x] **2.3.1** `typography.json`: font stacks (system-first unless M1 chose a face), role scale (`display`→`overline`), weights, line heights, tabular-numerals rule for money/tables. *(D04: system stacks, 18 roles, weights 400–700, tabular lining figures; the README relates the scale to the 150% captures without global zoom.)*
- [x] **2.3.2** `spacing.json` (4px base) + `radii.json` + `shadows.json`. *(D04: 4px scale with named page/stack/inline/inset/form uses and sizes; radii capped at 4 except avatars/dots; two shadow recipes per theme.)*
- [x] **2.3.3** `breakpoints.json` (web) + touch-target guidance (mobile) + `motion.json` (durations/easings) + `layers.json` (z-index scale + backdrop opacity). *(D04: em-based content breakpoints 640/1024/1440; 44/48 touch targets with 8 gaps; motion with a reduced counterpart for every transition; ordered layers with backdrop opacity per theme.)*
- [x] **2.3.4** Extend README tables; re-run the contrast script green. *(D04: every category tabled; gate re-run green after the last color change.)*

### Phase 4 — Close-out

- [x] **2.4.1** README tables complete for every category; contrast green in both themes; update `design/AGENTS.md` with the token model + consumption rules. *(D04: README complete, contrast green in both themes, `design/AGENTS.md` records the model and consumption rules. The token architecture and review fixes passed independent repeat verification against v54; app adapters and real-UI verification belong to Web/Mobile M3.)*
- [x] **2.4.2** Reconcile the actual integrated server baseline (`21dcc77`, including M9.5 `07cdb76`) against canonical v55: seed/category/type defaults, identity-map migration guards, unchanged calculator/DTO/enums, and the hidden prior-only retired-fee opening were technically reviewed against source. Product/catalog/copy and the affected Close day, approval and transaction blueprints reflect the working Q3/Q9/Q5 assumptions and the first-transition limitation. Both design markers align at v55 after this reconciliation; run both doc-sync scripts on this integrated checkout. This closes design revision alignment only: server 3.7/AA transition policy, M0 account-addendum acceptance, corrected M3/M4 acceptance, M10/M11 and real-app gates remain open.

---

## M3 — Design System: Component Contract

**Goal:** Write the component contract — the primitive set `web` and `mobile` each implement with their own code but identical anatomy, states, and token mapping: Button, Input, MoneyInput, Select, DatePicker, Checkbox/Switch, Badge (status), Card, Table/List, Modal/Sheet, Toast, Skeleton, EmptyState, FormField, PageHeader.

**Scope boundary:** Written specs in `design/specs/components/` — anatomy, variants, states (default/hover/focus/disabled/loading/error), token mapping (light + dark), accessibility, pt-BR microcopy. Includes the `ResponseErrorJson` rendering contract and skeleton conventions. No code.

**Note — the visual gallery is built in the apps, not here.** A browsable "see every color + component" view (like a Figma library or Storybook) is a **runtime** artifact, so it's built in `web` during the web milestones (Web M3) — a Storybook or a `/styleguide` route that renders the M2 tokens + these component specs. `design/` stays non-runtime; M3 is the *contract* that gallery renders. (The M1 prototype is the early visual reference until that gallery exists.)

**Precondition:** M2 (tokens to map against) + the M0 cross-screen patterns (Phase 4) that say which components the screens actually need.

> **Status:** 3.1.1–3.5.2 are complete and the contract is accepted (3.5.2). 3.5.3 closes only when both apps prove it in running code: Web M3 and Mobile M3 are implemented and implementer-verified; both await independent acceptance, and real screen-reader/physical-device evidence is still open.

### Phase 1 — Contract frame

- [x] **3.1.1** `design/specs/components/README.md`: index of every component with its catalog uses, the fixed spec template, and the token notation (`sem.*`, `dom.*`, full paths for other categories; no primitives or raw colors).
- [x] **3.1.2** Shared state vocabulary (disabled · read-only · loading · error · success, plus uncertain) and the cross-cutting contracts components honor by reference — verbatim `ErrorMessages`, uncertain writes and retries, session recovery without financial replay, provenance versus status, whole-row destinations with nested actions, origin/scope return, focus, motion, text scaling, money/date presentation — pointing at the catalog's cross-screen patterns instead of restating them.

### Phase 2 — Inputs and forms

- [x] **3.2.1** FormField, Input (text, e-mail, password, textarea, search, masked identifiers) and MoneyInput (blank versus recorded zero, cents entry through `@loto/core`, provenance of read-only amounts).
- [x] **3.2.2** Select (native, listbox, searchable over complete collections, no-default), DatePicker (date, range, day stepper, month, time — branch clock, calendar-only values) and Checkbox/Radio/Switch (no preselection where the catalog forbids it).

### Phase 3 — Actions and feedback

- [x] **3.3.1** Button (primary, scarce CTA, secondary, text/link, outlined and solid danger; loading and uncertain behavior) and Badge (domain status, aging severity, provenance marker, feedback and mode markers; unknown values neutral).
- [x] **3.3.2** Notice (including the error summary and **Resultado não confirmado**), Toast (confirmed success echo only), SaveStatus (pending/saved/failed/unsaved-on-server/stale/device-only with the acknowledged save time), Skeleton and EmptyState (no records · no matches · all-clear · setup needed).

### Phase 4 — Structure and data

- [x] **3.4.1** PageHeader (breadcrumb/return to the semantic origin), Card (sparing; Cockpit next action, checklist, auth sheet), SummaryStrip (scoped, never fabricated totals) and DetailList (identities, provenance, restriction reasons).
- [x] **3.4.2** Table/List (record tables, grouped lists, editable grid, expandable groups, selectable rows; whole-row destination with an explicit link and independent nested actions; horizontal scroll regions), FilterBar (real query fields, scope summary, preserved context) and Pagination (endpoint envelope, page subtotal labelling).
- [x] **3.4.3** Modal/Sheet (dialog, confirmation, drawer, bottom sheet; focus containment and return, phone keyboard), Menu (Session menu, overflow), Tabs (and segmented control), Disclosure (collapsed help, Ver o que mudou) and Stepper (confirmed per-step status for composed journeys).

### Phase 5 — Close-out

- [x] **3.5.1** `scripts/check-design-docs.mjs` verifies every token named by the component and screen specs exists in both themes, that no spec names a primitive or raw color, local links and anchors under `design/`, screen-to-blueprint coverage and copy labels for every contract enum; it passes, and `node scripts/test-check-design-docs.mjs` proves each rule fails on a disposable copy (14 scenarios, `design/` hashed unchanged). `--scope components` validates M2/M3 alone — links and token references in tokens and component specs, without the catalog, blueprints or copy coverage — and is proven on a tree with only the committed M0 docs.
- [x] **3.5.2** Independent acceptance of the current component contract after the follow-up credential-retention clarification. Independent review confirmed the button-state/token-mapping fixes and the clarification’s semantics; the owner closed the verified corrections on 2026-10-22.
- [ ] **3.5.3** Web M3 and Mobile M3 prove the accepted contract in running code (`/styleguide`, in-app gallery) — real keyboard, screen-reader, touch and text-scaling evidence is theirs.

---

## M4 — Design System: Screen Blueprints

**Goal:** Per-feature **visual** blueprints written just-in-time ahead of each web/mobile feature milestone: layout, information hierarchy, and the state presentation — built **on top of** each screen's semantic definition in M0's `screens.md`, in the M1 visual direction, using M2 tokens and M3 components.

**Scope boundary:** One blueprint per feature in `design/specs/screens/`. Stays open across the frontend build.

**Precondition:** M3 + M0's baselined `screens.md` + M1's direction. **Per-screen gate:** a blueprint for any screen named in the catalog's **gap → screen map** (the authoritative table in `screens.md`'s gaps section — inline pointers are convenience mirrors) requires that gap's implementation has landed and the entry has been re-synced. A parenthetical feature qualifier on a map row limits the gate to that feature; the rest of that screen's blueprint proceeds. At D04 (2026-10-20) the map holds only Q (uncataloged M11 reports), AA (retained retired-product edit/preview/save and the Products deactivation warning), AB (Plataforma onboarding list) and AC (withdrawing a pending invitation). Every server dependency through M7.7 and M8 Phases 1–7 is shipped; M8 Phase 8 provider and launch acceptance is a release gate, not a blueprint gate.

> **Status:** Phases 1–7, 4.8.1 and 4.8.5 are complete (D04 plan, inventory and unblocked blueprints authored in `50990d8`; later corrections remove unsupported retired-product identification, allow offboarding a member without an operator, preserve operator-specific drill-down filters and qualify statement access and generated-movement copy). 4.8.4 is implemented with independent acceptance pending; 4.8.2, 4.8.3 and 4.8.6 wait on their owners' prerequisites.

### Phase 1 — Plan and coverage

- [x] **4.1.1** Blueprint inventory below, keyed to the 45 catalog entries with their owning app milestone, current contract and precise remainder; this table is the coverage authority.
- [x] **4.1.2** `design/specs/screens/README.md` screen → blueprint index; `scripts/check-design-docs.mjs` fails on any entry without a row, a missing blueprint, or a blueprint without that screen's heading.

### Phase 2 — Shell, identity and account

- [x] **4.2.1** Navigation shell: sidebar/top bar/drawer, status rail, mode groups, the three presentation questions delegated by the catalog, native navigation model and the web route map (origins in navigation state, never URL authority).
- [x] **4.2.2** Login (with the Google link-confirmation step), Branch picker / session, Session menu.
- [x] **4.2.3** Profile, Change password, Forgot password, Reset password and Invitation, including capability-fragment handling.

### Phase 3 — Operator day and review

- [x] **4.3.1** Operator Day Cockpit — the grid stacks into a single column below `breakpoint.wide`, today's close always reachable beside a different next action — and Open day.
- [x] **4.3.2** Close day and Fix & resubmit: autosave versus explicit correction save, visible disabled **Salvar**, repeat undo, collapsed variance help, candidate equation distinct from the official value, compact per-product count list. Retired-product editing stays as the AA remainder.
- [x] **4.3.3** Manager Work Queue (fixed group order, backlog banner, owner setup checklist region) and Daily-close approval.

### Phase 4 — Transactions, cheque plans and fiado

- [x] **4.4.1** Transaction — Create, Transaction — Edit and Transactions list (three-date labelling, generated-movement links, no date-only link, uncertain-create recovery).
- [x] **4.4.2** Installment plan in creation and existing-plan modes (atomic **Finalizar plano**, Mixed confirmation, per-row saves, no single-row finalize).
- [x] **4.4.3** Client statement and Receber pagamento (branch-wide FIFO balance from the server, positive credit display, method without default, drawer effect by `FiadoPaymentKind`, no Tab balance).

### Phase 5 — Reports and month lock

- [x] **4.5.1** Shared report frame and the six reports, including the lock confirmation and earlier-month refusal path.

### Phase 6 — Time and personal work

- [x] **4.6.1** Clock in / out (overnight choice), Time-entry management (snapshot versus granular edits made visible), My transaction summary (automatic share) and My time-entry balance (per-day target only for one operator).

### Phase 7 — Administration, onboarding and employee setup

- [x] **4.7.1** The nine administration screens with their guards; AA (Products) and AC (Branch members) recorded as remainders.
- [x] **4.7.2** Plataforma — Nova lotérica (wizard, idempotent request id, device-local list per AB), Add employee and Offboard employee (committed steps, confirmed-versus-pending summary, AC).

### Phase 8 — Coverage and close-out

- [x] **4.8.1** Cross-document audit and checks: catalog/index/IA, token and component references, local links, enum labels, contrast gate and doc-sync, all green.
- [ ] **4.8.2** AA remainder blueprints after the server DailyClose/Product contract decision and catalog re-sync.
- [ ] **4.8.3** AB and AC remainders after their owner decisions/contracts.
- [ ] **4.8.4** Dashboard, Upcoming settlements and Monthly financial report blueprints, including affected IA/shell integration. **Implemented and statically checked (2026-10-22, corrected 2026-10-23); independent acceptance pending.** Source contracts were accepted at `84f75b9`; the three enums have domain tokens in both themes (`settlement-instrument`, `monthly-financial-role`, `overview-section-status`). This criterion stays unticked until real independent design acceptance.

  - [ ] **4.8.4a · Coverage and authority:** **Implemented; independent acceptance pending.** Every envelope/section/nested report field maps to UI behavior with per-field provenance against DTOs/OpenAPI; source acceptance, design review and runtime evidence are distinguished; 45 catalog entries and nine distinct manager reports retained.
  - [ ] **4.8.4b · Dashboard:** **Implemented; independent acceptance pending.** Eleven ordered regions cover twelve source-matrix questions, all clocks/windows, whole-set totals/bounded previews, debt/credit, official/candidate distinction, queue links, inactive context, hours, last activity, Ready/Empty/NotConfigured and atomic error/loading/updating. Every destination carries source dates/ids/status and return. Verified against ManagementOverviewBuilder/DTOs, controller/use case and A1–A21 fixtures; no speculative charts or financial recomputation.
  - [ ] **4.8.4c · Upcoming settlements:** **Implemented; independent acceptance pending.** Required request dates, exact cap, enum/optional account filters, Page/PageSize, totals/overdue/instrument breakdown/page envelope and every item field; unpaid/Active/plan inclusion, retired classification, missing activity markers, no-matches/setup distinctions, 400/404/auth handling, plan-vs-transaction links and full filter/page return.
  - [ ] **4.8.4d · Monthly financial:** **Implemented; independent acceptance pending.** Every month/clock/total/config flag, TerminalDays, LeftoversByType, Leftovers and Hours field; official variance/own fee/loss/movement/settlement/hour distinctions, reclassification, working Q3/Q9/SQ assumptions, null/zero/negative/credit semantics and supported drill-downs. No new financial formula, server route or migration.
  - [ ] **4.8.4e · Shared frame / M0 IA / M4 shell:** **Implemented; independent acceptance pending.** Nine navigation entries; dated owner decision D-M2-1, `/f/{branchId}` on branch Web paths only, identity/global exceptions; a switch releases only the initiating tab's branch-session binding while other tabs keep theirs; same-tab restored mode/origin/focus is distinguished from URL-only entry, copied duplicate-tab state is untrusted and revalidated, and no state grants permission or replays a write; a bare branch URL resolves the gated role home. Report origins and month/window, ids, page/filter, mode, nested origins and row/focus are retained by every destination blueprint (including `operator-day.md` and `time-and-self-service.md`).
  - [ ] **4.8.4f · Cross-screen / responsive / accessibility audit:** **Implemented; independent acceptance pending.** Every new report defines desktop/tablet/375px order, table-local scroll/grouped rows, column priority, wrapping/full signs, source/clock/freshness, focus/row names/nested controls, announcements, keyboard return, touch targets and both themes through existing components/tokens. Negative fee/leftover aggregates have neutral signed labels; fresh defaults use `GET /branch/current` and response `AsOfDate` governs midnight presentation; fee-only rows distinguish no close from not submitted; null/zero fees and counts are distinct; debtor destinations are client-filtered balance, then aging, then statement. No real-API capture or native/screen-reader verification claimed.
  - [ ] **4.8.4g · Independent acceptance and correction verification:** the same implementer handles findings and the same assigned independent reviewer verifies corrections and the whole scope. Record the real reviewer result before ticking 4.8.4; static checks and inherited 4.8.5 acceptance are not acceptance of this extension.

  **Latest checks (2026-10-23):** full design-doc check (352 links, 457 token references, 45/45 screens, 25 enums), components scope (74 links, 349 references), 14 design-doc, 40 contrast and 34 doc-sync disposable scenarios, 1,164 light/dark contrast pairs, manual source/destination route comparison against controllers/readers/DTOs (all 285 property occurrences across 28 reachable response schemas referenced) and `git diff --check`. These prove structural references, not semantic acceptance or app behavior.
- [x] **4.8.5** Independent acceptance of the corrected D04 blueprints, including membership-first offboarding, non-inferred product activity, operator-specific drill-downs and the final integrated canonical references. Independent review confirmed the semantics; owner instruction closed the verified label/canonical-reference corrections on 2026-10-22.
- [ ] **4.8.6** Blueprint updates from real Web/Mobile feature milestones (keyboard, screen reader, touch, 375px, enlarged text and native verification against the API).

### Phase 9 — Flow-alignment plan (owner, 2026-10-26; server M9.6–M9.8, M10)

The server's flow-alignment plan (`server/docs/milestones.md`, *Flow-alignment plan* and M9.5 Phase 5) changes the close-day model and adds receivables, reconciliation and people screens. **Ease-of-use principle for every blueprint below:** the screen supplies known information, links related entries automatically and asks only for physical observations or decisions the system cannot infer; ordinary operations use the shortest safe path, and extra explanation or controls appear only on a discrepancy or exception. The work runs in **two lanes**:

- **Lane 1 — per-phase catalog reconciliation (alongside server work).** Every server phase of M9.5 Phase 5, M9.6, M9.7, M9.8 and M10 that changes a contract brings its own spec bump; the assigned design-doc owner reconciles `product.md`, `screens.md` (entries, source tags, gap ownership) and `copy-guidelines.md` against that phase's spec and actual code **in the same delivery**, obtains technical review and only then updates both `Synced against loto-specs` markers, so `docs/check-loto-doc-sync.sh` stays green phase by phase. Catalog entries describe the shipped contract of that phase; nothing waits for a whole batch.
- **Lane 2 — screen blueprints (after the contracts they draw on).** The blueprints below follow the standing per-screen gate: the catalog entry is reconciled (lane 1) against the **shipped** contract, then the blueprint is written, then independently accepted.

**Lane 1**
- [ ] **4.9.1** Per-phase catalog reconciliation for every contract-changing server phase of the plan (continuous; one sub-entry per phase in its delivery note). The first one, with M9.5 Phase 5 and M9.6 Phase 1, introduces the terminal/cashbox/session vocabulary across `product.md`, `screens.md` and `copy-guidelines.md` (proposed pt-BR terms, settled there: cashbox = *caixa*, session = *turno*), both custody arrangements, and the M9.6 operation matrix as the source for operator help text.

> **4.9.1 status (2026-10-27):** the first increment (M9.5 Phase 5 + M9.6 Phase 1) is reconciled in canonical v58, product, catalog and copy. The catalog's first-phase table preserves shipped DTO/source tags and assigns proposed session, blind count, fiado, Safe, report and migration gaps to their future phases. Caixa/turno/terminal CAIXA are proposed app terms; report printed label/sign/content remain pending. The opening discrepancy is keyed to SessionOpeningId with optional handover and no guessed outgoing person (standalone Finish→Start and inline opening included); Conferir dedup/re-confirmation is domain-level; discrepancy is not an extra lock blocker; full cheque deposit is separate from partial card payout; after-lock attribution adjustment is owned by server M9.7 P7 7.3–7.4. Report, operator-day and transactions blueprints carry the confirmed Q1–Q5 factual corrections while retaining Q9 uncertainty and shipped flows. Claude direction and Lane 2 authoring are unchanged. This is one increment of the continuous criterion.
>
> **Marker disposition:** both design markers are v58 by the owner's explicit 2026-10-27 instruction for commit preparation. This is not independent technical acceptance of the increment (still pending) and does not close server M9.6 1.3 (owner + reviewer gate). Latest checks: real v58 doc-sync and 34 regressions, design full/components and 14 negatives, contrast 1,164 pairs and 40 negatives, `git diff --check`. No runtime, native or provider evidence.

**Lane 2 — Batch B, real cashier day** *(blueprint gate: the M9.6 phase each screen draws on is shipped and reconciled; precedes Web/Mobile M4–M5 and the Batch B increments of M6–M7)*
- [ ] **4.9.2** `operator-day.md` revision — Cockpit and Open day: **inline opening** (*Conferir abertura e iniciar*, one prefilled confirmation with cashbox, proposed opening and the caller; *Abertura diferente* entering only differing items); explicit zero opening; separate cashboxes on one terminal.
- [ ] **4.9.3** `operator-day.md` revision — Close day and Fix & resubmit: **blind count** (expected closing and difference hidden until **Conferir**; opening values visible), **Recontar**, outdated and unconfirmed calculation states, the extra confirmation only when the ledger changed between Conferir and submit, autosave that reveals nothing; the three close-form fields (*Relatório do terminal*, signed; *Tarifa Bolão*; *Tarifa Especial*). Replaces the live-preview requirement ("Live variance explanation").
- [ ] **4.9.4** New — **Passar o caixa** (handover): the leaving operator's one screen and the incoming operator's acknowledgment, opening discrepancies both with and without direct handover (including Finish→Start/inline and opening-count acknowledgment), the audited manager exception.
- [ ] **4.9.5** `queue-and-review.md` revision — Work Queue and approval per session: **Aprovar dia do caixa** with per-session checks, **Aprovar como único gestor** (reason, audited), opening discrepancies, both with and without handover (unattributed until explicit manager attribution with a reason; outgoing only when an evidenced predecessor custodian exists, otherwise incoming or neither) and Conferir history visible to the manager; sessions side by side, never summed counts.
- [ ] **4.9.6** `transactions.md` revision — fiado sale with its automatic cashbox movement (no counted-stock choice), repayment method without default (Dinheiro, PIX, configured others), **Depósito Dinheiro** as the two-leg cofre transfer.
- [ ] **4.9.7** Cross-screen pattern — **arithmetic money input**: restricted grammar, visible evaluated result before acceptance, BRL rounding, error presentation; referenced by every money field.
- [ ] **4.9.8** `administration.md` revision — Accounts (cashbox with paired fiado account by default, Terminal reference, cofre account type), Categories & Transaction Types (seeded expense types, repayment methods, Tarifa Especial role, system-owned close-form types), owner setup checklist (opening count, cashbox without fiado pairing).

**Lane 2 — Batch C, receivables and reconciliation** *(blueprint gate: the M9.7 phase each screen draws on is shipped and reconciled; precedes the Web/Mobile M6/M8 increments)*
- [ ] **4.9.9** Cheques: custody into the cofre, batch **Depositar**, **Cheque devolvido** (client, next deposit date, optional alínea) → **Reapresentar** / **Converter em fiado**; occurrence versus posting dates explained in plain language.
- [ ] **4.9.10** Card settlement batches (provider, receiving account, fees confirmed or left unmatched) and cofre → bank **Recolhimento** with pending/confirmed receipts.
- [ ] **4.9.11** **Statement reconciliation**: OFX import, proposed matches, confirmation of the displayed matches only, unmatched items kept, duplicate-import notice.
- [ ] **4.9.12** **P&L** by financial class and posting date, frozen locked months; **shortage tracking** per person (informational, threshold alert; unattributed opening discrepancies with or without handover listed separately, with the manager's attribution action and the pending after-lock adjustment owned by server M9.7 P7 7.3–7.4); revisions to Upcoming settlements, Open-cheque aging and Monthly financial for settlement events, per-session attribution and frozen classification.

**Lane 2 — Batch D, people and go-live** *(blueprint gate: the M9.6 Phase 3, M9.8 and M10 phase each screen draws on is shipped and reconciled)*
- [ ] **4.9.13** **Employee statement** (sessions, differences, hours, pay items, effective-dated commission, no automatic deduction, printable) and the **special-draws catalog**.
- [ ] **4.9.14** Hour bank: carry-over balance and manager *Pagamento/Ajuste de horas* entries, as a revision of Time-entry management and My time-entry balance.
- [ ] **4.9.15** Go-live: **Opening count** (server M9.6 Phase 3), **opening fiado balances and opening receivables** (M10 Part A) and **Access import** (M10 Part B: export guide, mapping step, dry run with explained exceptions, confirmation).
- [ ] **4.9.16** Inventory, index and checks: every new or revised screen has its row below and in `design/specs/screens/README.md`; full design-doc, contrast and doc-sync gates green; independent acceptance per batch.

### Blueprint inventory

| Screen (catalog entry) | Blueprint | App milestone | Current contract (server) | Remainder |
|---|---|---|---|---|
| Login | `shell-and-identity.md` | Web/Mobile M4 (Google: M11) | M8 Ph 2, 7 (password + verified Google) | — |
| Branch picker / session | `shell-and-identity.md` | Web/Mobile M4 | M1 session; M7.7 Ph 6 branch clock; M8 profile flag | — |
| Session menu | `shell-and-identity.md` | Web/Mobile M4 (account items: M11) | M8 Ph 2 (profile, logout); self-context | — |
| Profile | `shell-and-identity.md` | Web/Mobile M11 | M8 Ph 2 | — |
| Change password | `shell-and-identity.md` | Web/Mobile M11 | M8 Ph 2 | — |
| Forgot password | `shell-and-identity.md` | Web/Mobile M11 | M8 Ph 3 | — |
| Reset password | `shell-and-identity.md` | Web/Mobile M11 | M8 Ph 3, 6 (first-operator activation) | — |
| Invitation | `shell-and-identity.md` | Web/Mobile M11 | M8 Ph 5, 7 | — |
| Plataforma — Nova lotérica | `onboarding-and-employees.md` | Web/Mobile M11 | M8 Ph 6 | onboarding list (gap AB) |
| Manager Work Queue | `queue-and-review.md` | Web/Mobile M7 (setup checklist: M11) | M7.5; M7.7 Ph 4, 7.5, 7.8; M8 Ph 4 | — |
| Daily-close approval | `queue-and-review.md` | Web/Mobile M7 | M7.5; M7.7 Ph 2, 3, 7.6.1 | — |
| Transaction — Create (fast entry) | `transactions.md` | Web/Mobile M6 | M3; M7.7 Ph 5, 6 | — |
| Transaction — Edit (correction / audit) | `transactions.md` | Web/Mobile M6 | M3; M7.7 Ph 5, 6, 7.6.2 | — |
| Transactions list | `transactions.md` | Web/Mobile M6 | M3; M7.7 Ph 6, 7.6.3 | — |
| Installment (pre-dated cheque) plan | `transactions.md` | Web/Mobile M6 | M3; M7 Ph 6/7.2; M7.7 Ph 6, 7.6.2 | — |
| Client statement (Extrato de fiado) | `transactions.md` | Web/Mobile M6 | M7.7 Ph 5.5 | — |
| Receber pagamento | `transactions.md` | Web/Mobile M6 | M7.7 Ph 5.1, 5.6 | — |
| Daily ledger | `reports-and-month-lock.md` | Web/Mobile M8 | M7 Ph 2; M11 Ph 2.4 | — |
| Fiado balance | `reports-and-month-lock.md` | Web/Mobile M8 | M7.7 Ph 5; M11 Ph 3.1 | — |
| Fiado aging | `reports-and-month-lock.md` | Web/Mobile M8 | M7.7 Ph 5.1; M11 Ph 3.2 | — |
| Open-cheque aging | `reports-and-month-lock.md` | Web/Mobile M8 | M7 Ph 5; M11 Ph 2.1, 4.2 | standalone-cheque groups (*Cheque avulso*, catalog v56, accepted at v57) |
| Cash-variance summary | `reports-and-month-lock.md` | Web/Mobile M8 | M7 Ph 8; M7.7 Ph 6; M11 Ph 2.2 | — |
| Monthly reconciliation + lock | `reports-and-month-lock.md` | Web/Mobile M8 | M7 Ph 10; M7.7 Ph 4; M11 Ph 2.3 | — |
| Dashboard | `reports-and-month-lock.md` | Web/Mobile M8 | M11 Ph 7 (source accepted `84f75b9`) | authored; independent design acceptance (4.8.4) |
| Upcoming settlements | `reports-and-month-lock.md` | Web/Mobile M8 | M11 Ph 4–5 (source accepted `84f75b9`) | authored; independent design acceptance (4.8.4) |
| Monthly financial report | `reports-and-month-lock.md` | Web/Mobile M8 | M11 Ph 6 (source accepted `84f75b9`) | authored; independent design acceptance (4.8.4) |
| My transaction summary | `time-and-self-service.md` | Web/Mobile M9 | M7 Ph 9; M7.7 Ph 5.8 | — |
| My time-entry balance | `time-and-self-service.md` | Web/Mobile M9 | M7 Ph 11; M7.7 Ph 7 | — |
| Operator Day Cockpit | `operator-day.md` | Web/Mobile M5 | M4; M7.7 Ph 3, 5.6, 7.6.1; M8 Ph 4 | — |
| Open day | `operator-day.md` | Web/Mobile M5 | M4; M7.7 Ph 3 | — |
| Close day | `operator-day.md` | Web/Mobile M5 | M4; M7.7 Ph 2, 3, 6, 7.6.1 | retained retired-product edit/preview/save (gap AA) |
| Fix & resubmit | `operator-day.md` | Web/Mobile M5 | M4; M7.7 Ph 3, 7.6.1 | retained retired-product edit/preview/save (gap AA) |
| Clock in / out | `time-and-self-service.md` | Web/Mobile M9 | M5 | — |
| Time-entry management | `time-and-self-service.md` | Web/Mobile M9 | M5; M7.7 Ph 6, 7 | — |
| Operators (admin) | `administration.md` | Web/Mobile M10 (link requests: M11) | M2; M7.7 Ph 6; M8 Ph 4 | — |
| Account assignment (admin) | `administration.md` | Web/Mobile M10 | M2; M7.7 Ph 5.3, 5.6 | — |
| Accounts (admin) | `administration.md` | Web/Mobile M10 | M2; M7.7 Ph 5.3 | — |
| Clients (admin + counter) | `administration.md` | Web/Mobile M10 | M2 | — |
| Categories & Transaction Types (admin) | `administration.md` | Web/Mobile M10 | M6; M7.7 Ph 5; M11 Ph 4, 6 | report classifications (catalog v56, accepted at v57) |
| Products (admin) | `administration.md` | Web/Mobile M10 | M6 | deactivation warning/recovery (gap AA) |
| Holidays (admin) | `administration.md` | Web/Mobile M10 | M5, M6 | — |
| Settings (admin) | `administration.md` | Web/Mobile M10 | M6; M7.7 Ph 4, 6, 7 | — |
| Branch members (admin) | `administration.md` | Web/Mobile M10 (invitations: M11) | M1; M8 Ph 2, 5 | withdrawing an invitation (gap AC) |
| Add employee (guided) | `onboarding-and-employees.md` | Web/Mobile M11 | M2 operator/assignment; M8 Ph 5 invitation | — |
| Offboard employee (guided) | `onboarding-and-employees.md` | Web/Mobile M11 | M1 membership; M2; M8 Ph 2, 5 | withdrawing an invitation (gap AC) |
