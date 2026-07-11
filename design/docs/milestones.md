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
- **Doc sync is scripted.** The domain doc-sync group now lives at repo-root `docs/`, validated by `docs/check-loto-doc-sync.sh`. Extending that check to the frontend docs — `product.md` and `screens.md` carrying a `Synced against loto-specs: vNN` marker that a backend spec bump flags for re-check — is **still pending item 0.4.3**; it is not implemented yet.
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

- [x] **0.1.1** `design/docs/product.md` — lean frontend framing: roles → UI gating, two-stage token model in UI terms, the **mobile-clones-web** rule, a screen *index*. Points to `loto-specs.md` §2/§7; does not restate them. No version labels. *(Drafted — pending lock.)*

### Phase 2 — Display & copy conventions *(these double as M1 tool inputs)*

- [x] **0.2.1** `design/specs/formatting-ptbr.md` — BRL (`R$ 1.234,56`, signed variance `+R$ 12,00` / `−R$ 12,00`), date `dd/MM/yyyy`, datetime, time `HH:mm`, CPF/CNPJ/phone masks, decimal-comma. The contract `shared/core` (Shared M3) implements and that M1's tool prompts cite.
- [x] **0.2.2** `design/specs/copy-guidelines.md` — tone, sentence-case, a terminology glossary (Fiado, Diferença Caixa, fechamento do dia, cheque pré-datado, bolão…) mapped from `loto-specs.md`, and the error-display rule (`ResponseErrorJson.ErrorMessages` rendered verbatim).

### Phase 3 — Semantic screen catalog

- [x] **0.3.1** `design/docs/screens.md` — the per-screen template (Purpose · Primary job · Access · Permission fallback · Default view · Data shown *with per-field source tags `[DTO]`/`[derived]`/`[composed]`/`[gap]`* · Actions · Audit/lock · States · Navigation) + the full screen list by area. Visual hierarchy and deep-link state preservation are explicitly deferred to M4.
- [x] **0.3.2** Define the spine screens in full: **Manager Work Queue** (exception-first, reframed from "Dashboard"), **Daily-close approval** (comparison framing, system-calc vs operator-entered marked), **Transaction — Create (fast entry)** + **Transaction — Edit (correction/audit)** (split into two), and **Operator Day Cockpit** (operator next-action home).
- [x] **0.3.3** Define the remaining screens, area by area, each mapped to its controller(s) + response DTO fields, every data field source-tagged.

### Phase 4 — IA + cross-screen patterns + close-out

- [ ] **0.4.1** Add the **navigation map / IA** into `screens.md`: how screens link, the branch-session gate, role-based nav differences (Member vs Manager/Admin).
- [ ] **0.4.2** Add the shared **cross-screen patterns** into `screens.md` (semantically): error display, list pagination, empty/loading conventions, auth-tier gating. These seed M3/M4.
- [ ] **0.4.3** Add `Synced against loto-specs: vNN` markers to `product.md` + `screens.md`; mark both **Locked**. M1 may now start.

---

## M1 — Visual Direction ·  *Owner: dev-team (external tools — no AI checkboxes)*

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

### Suggested prompt template

> *"You are designing one screen of **Lotero**, an internal **pt-BR** management web app for a Brazilian lottery house (lotérica). Audience: a [manager / operator]. Build the **[SCREEN NAME]** screen.*
> *Data + behavior: [paste the `screens.md` entry]. Example API response: [paste from Swagger]. Required states to show: loading / empty / error / success.*
> *Visual tone: [your adjectives] — calm, dense, finance-grade, light theme (also show a dark variant). Money as `R$ 1.234,56`, dates `dd/MM/yyyy`, tabular numerals in tables.*
> *Color inspiration (do NOT copy the Caixa logo/clover): [paste palette]. Use our own neutral mark placeholder.*
> *Output a single responsive screen (works desktop → mobile, since mobile clones web)."*

### Process (the dev-team call)

- Use **2–3 tools, time-boxed** (not all five). Generate, compare.
- Score each direction on the scorecard: **operator task speed · manager info density · accessibility & legibility · web↔mobile adaptability · brand fit without copying Caixa · Tailwind/NativeWind feasibility.**

### Outputs (these are M2's precondition)

- **Chosen screens** — exported from the winning tool (links or images), parked under `design/references/`, plus a short note of *what* you picked and *why* (rough notes are fine). This is the dev-team's deliverable.
- **`design/docs/decisions.md`** — **the AI writes this for you** at the M1→M2 handoff: it reads your chosen screens + notes and formalizes the direction (palette tone light+dark, type feel, density, component-style, rejected alternatives + scorecard). You review and lock it. *(You pick; the AI writes it up — not a separate milestone, it's the first step of M2.)*

> **Example `decisions.md` entry (what the AI produces):**
 > *"**Primary:** a deeper, warmer blue (`#1B4F8A`) over the flat institutional Caixa `#005CA9` — reads more modern, hits AA on white at body size. **Accent:** muted orange, reserved for primary CTAs + active nav only. **Surface:** warm-neutral `#F7F8FA`, not the cool Caixa gelo. **Density:** compact — 36px table rows, 8px base. **Cards:** 1px border, no heavy shadow. **Nav:** collapsible left sidebar (desktop) → bottom tab (mobile). **Dark:** near-black `#0E1116` surfaces, same accent. **Rejected:** the SciFi neon direction (fails finance-legibility + brand fit)."*

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

- [ ] **2.1.1** Create `design/tokens/` and write `design/tokens/README.md`: the three-layer model, the naming convention (dot-path, e.g. `color.semantic.light.surface-raised`), the `{path.to.token}` syntax, the build-time consumption rule (apps read JSON via their own adapters; only semantic/domain may be referenced), and the light+dark theme structure. *(Folders are created as files land — no empty scaffolding.)*
- [ ] **2.1.2** Document the JSON file shape: one file per category (`colors.json`, `typography.json`, `spacing.json`, `radii.json`, `shadows.json`, `breakpoints.json`, `motion.json`, `layers.json`).
- [ ] **2.1.3** Base the color work on the parked inputs — `design/references/caixa-palette.md` + the locked tone in `decisions.md` + the tool's exported styles. Do not create a new identity reference here.

### Phase 2 — Color system (light + dark)

- [ ] **2.2.1** Primitive layer in `colors.json`: brand-blue, brand-orange, neutral, plus green/red/amber/sky feedback ramps (~10 steps each), derived from the M1 tone.
- [ ] **2.2.2** Semantic layer under **both** `light` and `dark`: `background`, `surface`, `surface-raised`, `border`, `text-primary/secondary/muted`, `text-on-primary`, `primary`/`primary-hover`/`primary-active`, `accent`, `focus-ring`, and `success`/`danger`/`warning`/`info` as `-bg`+`-text` pairs. All `{...}` references into primitives.
- [ ] **2.2.3** Domain layer: one token per value of every colored enum (spec §4) — `TransactionStatus`, `DailyCloseStatus`, `Direction`, `AgingBucket` (severity ramp), `TimeEntryStatus` — for both themes.
- [ ] **2.2.4** `scripts/check-contrast.mjs` (repo-level): zero-dep Node script that resolves references and verifies every text/bg + badge pair meets WCAG AA in **both themes**. Doubles as the JSON/reference validator. Run green.
- [ ] **2.2.5** Domain-token table in the README.

### Phase 3 — Typography, spacing, geometry, motion, layers

- [ ] **2.3.1** `typography.json`: font stacks (system-first unless M1 chose a face), role scale (`display`→`overline`), weights, line heights, tabular-numerals rule for money/tables.
- [ ] **2.3.2** `spacing.json` (4px base) + `radii.json` + `shadows.json`.
- [ ] **2.3.3** `breakpoints.json` (web) + touch-target guidance (mobile) + `motion.json` (durations/easings) + `layers.json` (z-index scale + backdrop opacity).
- [ ] **2.3.4** Extend README tables; re-run the contrast script green.

### Phase 4 — Close-out

- [ ] **2.4.1** README tables complete for every category; contrast green in both themes; update `design/AGENTS.md` with the token model + consumption rules.

---

## M3 — Design System: Component Contract

**Goal:** Write the component contract — the primitive set `web` and `mobile` each implement with their own code but identical anatomy, states, and token mapping: Button, Input, MoneyInput, Select, DatePicker, Checkbox/Switch, Badge (status), Card, Table/List, Modal/Sheet, Toast, Skeleton, EmptyState, FormField, PageHeader.

**Scope boundary:** Written specs in `design/specs/components/` — anatomy, variants, states (default/hover/focus/disabled/loading/error), token mapping (light + dark), accessibility, pt-BR microcopy. Includes the `ResponseErrorJson` rendering contract and skeleton conventions. No code.

**Note — the visual gallery is built in the apps, not here.** A browsable "see every color + component" view (like a Figma library or Storybook) is a **runtime** artifact, so it's built in `web` during the web milestones — a Storybook or a `/styleguide` route that renders the M2 tokens + these component specs. `design/` stays non-runtime; M3 is the *contract* that gallery renders. (The M1 prototype is the early visual reference until that gallery exists.)

**Precondition:** M2 (tokens to map against) + the M0 cross-screen patterns (Phase 4) that say which components the screens actually need.

---

## M4 — Design System: Screen Blueprints

**Goal:** Per-feature **visual** blueprints written just-in-time ahead of each web/mobile feature milestone: layout, information hierarchy, and the state presentation — built **on top of** each screen's semantic definition in M0's `screens.md`, in the M1 visual direction, using M2 tokens and M3 components.

**Scope boundary:** One blueprint per feature in `design/specs/screens/`. Stays open across the frontend build.

**Precondition:** M3 + M0's locked `screens.md` + M1's direction.
