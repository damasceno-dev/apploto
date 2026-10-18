# Lotero — selected visual direction

> **Status:** M1 direction selected by the owner; D03 handoff recorded on 2026-10-15. References and token inputs are ready for review. Owner review/lock remains the M2 precondition; this record does not declare M1 accepted, M0 fully complete or any app implemented.
> **Selection:** Claude prototype, displayed build `2026-10-08.c9-seed-v3`. [Captured evidence and provenance](../references/claude/README.md) · [source/capture fingerprints](../references/claude/manifest.json).

## Decision and authority

Use the selected Claude direction: blue structural navigation, cool pale neutrals in light mode, deep blue-black surfaces in dark mode, system typography, compact financial tables, sparse orange emphasis and restrained geometry. Keep the calm, practical character visible in the Work Queue and Cockpit; carry their shared language into the remaining catalog rather than copying demo screens wholesale.

The owner supplied the choice. D03 records it without another comparison exercise, invented rankings or a required replay of all prototype states. No alternative was newly assessed or rejected here. The example warmer palette and mobile bottom tabs in `milestones.md` are illustrative, not the selected direction. The actual reference uses the institutional blue/orange anchors and a narrow-screen drawer. Do not claim a custom blue, a licensed typeface, a finished logo or native interaction evidence that the reference does not supply. No CAIXA wordmark, clover or game-specific palette is adopted; the small “L” mark remains a placeholder.

Behavior and field ownership come first: [`screens.md`](screens.md), including its authoritative gap → screen map and shared patterns; [`product.md`](product.md); [`copy-guidelines.md`](../specs/copy-guidelines.md); [`formatting-ptbr.md`](../specs/formatting-ptbr.md); and the actual server contracts behind them. The catalog baseline is aligned to spec v43. If a visual conflicts with these sources, adapt the visual. Do not revise the semantics to fit the reference.

## Reusable principles

**Structure carries the brand.** Blue anchors the navigation and ordinary primary actions; orange marks the active navigation edge and a carefully chosen next action. Turquoise is secondary. Large content areas stay neutral, with thin dividers and small status treatments. Avoid gradients, oversized KPI cards, heavily tinted panels and decorative shadows. The exact source anchors are recorded below; M2 owns accessible ramps and pairs rather than a new identity exercise.

**Tables explain the work.** Give each section a clear title, count and contextual date; keep numeric columns right-aligned with tabular lining numerals. Separate primary identity, secondary attribution and totals through weight, spacing and hairlines. Opening/system values and operator-entered values need distinct labels and treatment. A calculated variance is a read-only result, not a money input. Status, direction and signed difference retain text/signs as well as color.

**Density serves the task.** Prefer one prominent next action for the operator and ordered exception groups for the manager. Preserve secondary routes to today's close and blockers. Compact rows are a visual starting point, not a fixed height that clips long names, messages or scaled text. Use whitespace between groups instead of boxing every label. A loading or error state must retain enough structure and source context to explain what is happening.

**Geometry stays restrained.** Ordinary surfaces, fields, buttons and menus use radii at or below 4px, with 1px borders and minimal elevation. Small rectangular badges are appropriate; the source's pill-shaped filter chips are an exception to adapt into restrained metadata, not a general pill system. Circular avatars and status dots may retain their functional shapes. Shadows distinguish overlays from content, not every section from its background.

**Navigation adapts to space.** The reference has a persistent desktop sidebar, a compact top bar and a narrow-screen drawer with a backdrop. It does not supply mobile bottom tabs. Preserve branch/date/workstation context when the top bar compresses; the drawer can carry the longer context but cannot be the sole clue to which financial record is being edited. The session/avatar menu remains the home of Gestão ↔ Meu turno, as decided in the IA. Role is permission; mode changes navigation/home. Do not infer permission or new destinations from the prototype's visible links.

## Binding inputs for M2

These are extracted seeds from the original `assets/base.css`, not token JSON or a passed contrast audit. M2 curates them into primitive → semantic → domain layers, with both themes together. Apps consume semantic/domain tokens through their own adapters; `design/` stays non-runtime. Missing active/disabled/composite pairs, ramps and domain enum assignments are M2 work, not permission to invent backend states.

### Palette and semantic mapping

| Purpose | Light source | Dark source |
|---|---|---|
| Page background | `#f1f5f7` | `#0a1520` |
| Main surface | `#ffffff` | `#0f1d2a` |
| Subtle surface / table header | `#f4f8f9` | `#132433` |
| Stronger surface | `#e9f0f2` | `#182a3a` |
| Row hover | `#f6fafb` | `#142637` |
| Primary text | `#0f2230` | `#e4edf2` |
| Secondary text | `#33495a` | `#b6c6d0` |
| Muted text | `#5b7180` | `#8699a7` |
| Border / hairline / field border | `#d3e0e4` / `#e4ecef` / `#bfd0d6` | `#22384a` / `#1a2e3e` / `#2c4458` |
| Primary action / hover / ink | `#005ca9` / `#004b8b` / `#ffffff` | `#0075bf` / `#0a86d6` / `#ffffff` |
| Link / focus hue | `#005ca9` | `#58ade8` |
| Orange emphasis / hover / ink | `#f39200` / `#ffa21f` / `#1b1409` | Same |
| Secondary turquoise | `#2a8576` | `#6cc9b9` |
| Success / warning / danger / info text | `#16734a` / `#955700` / `#bb3528` / `#0067ad` | `#52c48d` / `#f2b347` / `#ff7d70` / `#63b8f0` |
| Neutral badge background | `#ebf1f3` | `#1a2d3d` |
| Navigation / top bar | `#004b8b` / `#ffffff` | `#082440` / `#0c1924` |

**Naming translation matters:** the prototype calls blue links/focus `--accent`; M2's semantic **accent** is the sparse orange emphasis. Map blue to primary/link/focus roles and orange to accent roles rather than copying the source variable names blindly. Do not use orange behind white text merely because it is a CTA; the reference supplies dark orange ink.

The source's success/warning/danger/info soft backgrounds are respectively `rgba(22,115,74,.10)`, `rgba(243,146,0,.14)`, `rgba(187,53,40,.09)`, `rgba(0,103,173,.09)` in light; `rgba(82,196,141,.14)`, `rgba(243,146,0,.16)`, `rgba(255,125,112,.13)`, `rgba(99,184,240,.13)` in dark. Evaluate the actual composite against its intended surface. Navigation text uses translucent white in light and translucent pale text in dark; its normal, muted, hover and active combinations also need explicit validation. A CSS comment saying “AA” is not evidence that every pair passes.

### Type, spacing and geometry

| Input | Extracted seed and adoption rule |
|---|---|
| Font | System-first `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif`; platform equivalents on native. No font download required. Monospace only for appropriate technical/detail content, not body money columns. |
| Authored type scale | 11.5 / 12.5 / 14 / 15 / 20 / 24px; 14px body with 1.5 line height, 24px page heading with 1.2 line height. Tables use 13.5px body / 11px headers; next-action heading 22px. M2 maps roles and scaling explicitly; these are source sizes, not required tiny native text. |
| Weight | Body 400; supporting action 550; labels 600; heading/table emphasis 650; strongest marks 700. Platform font availability may require nearest supported weights while preserving hierarchy. Headings use slight `-.01em` tracking; avoid tracked uppercase prose. |
| Numerals | Tabular lining numerals, right-aligned amounts, labels/signs preserved. No whole-page monospace. |
| Spacing | 4px base, source steps 4 / 8 / 12 / 16 / 24 / 32px. Typical table cell padding 10 × 12px, outer horizontal edges 16px. Natural row height follows content. |
| Radius and stroke | Main radius 4px; small badges 3px; hairline/border 1px; selected/error emphasis edge 3px. |
| Source desktop frame | Sidebar 252px, top bar 58px, page max-width 1240px with 28 × 32px padding. These are authored, pre-zoom measurements, not a fixed product viewport contract. |
| Responsive seeds | Authored width breakpoints 860px (drawer) and 560px (narrow adjustments); drawer max 290px and 86vw before zoom compensation. M4 tests content/scaling and chooses platform layout; do not port breakpoint rewriting. |
| Targets | Source buttons/fields are 36px minimum and small buttons 28px. Production must meet the catalog's **44 × 44 logical-unit minimum** for touch targets, including icon/repeat/menu actions, with spacing to prevent accidental financial actions. Compact visuals may sit inside larger targets. |

The captured desktop default is **150% CSS zoom**; at the requested 390px width the same preference is clamped to 100%. This explains the apparent size difference. Retain the readable hierarchy and ability to enlarge text; do not encode a screenshot's apparent pixels as the authored type scale or reproduce a clamp that prevents a user's requested scaling. M2 must document effective type roles; M3/M4 and each app validate scaled text without lost context/actions. The A−/A+ demo control does not itself decide a new production settings feature.

### Elevation, focus and motion

Use near-flat content: source light surface shadow `0 1px 2px rgba(15,34,48,.05)`, dark `0 1px 2px rgba(0,0,0,.3)`. Overlay shadow seeds are `0 14px 36px rgba(15,34,48,.18)` and `0 16px 40px rgba(0,0,0,.5)`; backdrop seeds are `rgba(6,22,38,.46)` and `rgba(0,0,0,.6)`. M2 normalizes layer roles from content → sticky header → backdrop/drawer → menu/dialog/popover; source z-index numbers and the prototype state-bar layer are not shared app contracts.

The keyboard capture shows a repeat action with a 3px translucent focus ring. Source colors are `rgba(0,117,191,.30)` / `rgba(88,173,232,.35)`; the general outline is 2px solid link color with 2px offset. Keep a persistent, high-visibility focus indicator and validate it against every adjacent surface; adjust the seed if necessary. Source row `focus-within` adds a left edge. Never rely on hover or row tint alone to expose an action or selection.

Source transitions are short: 120ms for simple feedback and 180ms ease for drawer movement. Reduced-motion styles remove animation/transitions; theme changes suppress transitional flashes. M2 records duration/easing and reduced-motion roles. M3 defines focus entry/return, Escape/back dismissal and background isolation for overlays; source CSS or one focused screenshot does not verify that lifecycle.

## Components and adaptation to the real product

| Pattern to carry forward | Required production adaptation / owner |
|---|---|
| Blue primary, orange next-action, neutral secondary/ghost and red destructive actions | M3 specifies hierarchy, loading/disabled/focus and touch variants. M4 chooses one principal action per task; color never grants authority. |
| Persistent labels, bordered fields, short helper text and nearby errors | M3 associates labels/descriptions/errors and announces validation without losing input. Render every `ResponseErrorJson.ErrorMessages` entry verbatim, alongside the designed guidance; do not replace backend messages with prototype copy. |
| Tables with section titles/counts, subtotals and source distinctions | M4 preserves every required field/action on small screens via grouped rows or scoped table scrolling with discoverable controls. Do not compress the page into a desktop grid or hide necessary columns permanently. |
| Small text-bearing status badges and read-only calculated rows | M2 uses actual enum values and copy maps; M3 documents text/icon alternatives. Differentiate a preview from the official persisted result and keep stale/loading/error result states visible. |
| Neutral notice with a narrow signal edge; inline success/status text | M3 defines loading/skeleton, empty, recoverable error and success anatomy. A viewport capture of seeded success is not evidence that these states work. Never announce success before authoritative confirmation. |
| Avatar menu, drawer, dialogs and filter summaries | M3 specifies keyboard/touch/reader behavior and focus; M4 keeps active branch, date, filters, origin and return context reachable. Auth/Profile/logout additions remain 0.4.6-owned. |

The actual narrow Cockpit capture exposes an inline two-column grid that squeezes “Próximo passo” into a thin vertical strip. **M4 must stack its main and supporting blocks at narrow widths**, allow the principal action to wrap/use available width, and keep today's close reachable. Preserve the selected palette and hierarchy while correcting this defect. The narrow count-table capture is scrolled to the middle and does not prove that all actions remain discoverable; test the count/repeat/save flow in the blueprint and each app. The mobile drawer is useful evidence of visual structure, not proof of native gesture, safe-area, software-keyboard or reader behavior.

Use the catalog's permission and session patterns rather than the fake demo state. In particular, preserve the two-stage identity/branch gate, live branch role checks, safe renewal/re-login and context recovery, branch-switch invalidation, and scoped durable input. An unlinked Manager/Admin retains Gestão close access; changing mode never changes the Manager/Admin time-entry snapshot contract. Error announcements, first-invalid focus, loading announcements and focus restoration must follow the catalog on both platforms.

Financial writes stay online-only. Loading, dirty, saved, stale and uncertain states need distinct visual treatment. Follow the actual Idempotency-Key retry and If-Match/ETag rules; sent writes with uncertain outcomes, including 5xx responses with bodies, cannot display success or invite a new duplicate operation. Preserve recoverable input through blockers and return journeys. Today's plain-Draft autosave, dirty Save, repeat/undo and authoritative saved timestamps follow the catalog, not a localStorage simulation. Server business date/time governs today's work and historical permissions; the frozen demo clock is never a product input.

Concrete contract checks inform these adaptations:

- `ResponseDashboardJson.Backlog` and `UnlockedDraftTransactionCount`, with `ResponseDashboardBacklogJson` counts and `OldestPendingDate`, already ship. The captured “gap W” pending label is stale and must disappear from product UI. Use the catalog's distinct date ranges and fixed queue order; do not copy or recompute the fake aggregate.
- `ResponseDailyCloseReviewItemJson` supplies `ProductId`, `ProductName`, `DisplayOrder`, `OpeningValue`, `ClosingValue` and `IsCashVarianceProduct`. Use the flag to present the calculated row, never a hard-coded product name/id. `ResponseDailyCloseVariancePreviewJson.CashVariance` comes from the server preview; the prototype's arithmetic is not reusable runtime logic.
- The preview currently resolves active products and can reject retired rows. Gap **AA** remains scoped to retained-retired-product edit/preview/save recovery on Close day and Fix & resubmit, and deactivation warning/recovery on Products. A screenshot does not remove that gate or block the whole Close day screen.
- The live gap map remains the authority for all pending features, including H, L/S, U/V and X/Y/Z, auth/account/Plataforma under **0.4.6 / M8**, and deferred **M11** reports. Visible prototype fields/routes are not an implementation grant. Server milestone ordering and pending contract ownership are unchanged.

Do not copy the fake database, calculations, authentication, state switchers, source/debug badges, gap annotations, “Protótipo” tag, build label or reserved state-bar padding into runtime/spec implementations. Retain real pt-BR product language: sentence-case labels, tenant names verbatim, signed cash variance, BRL/date formatting and API errors from their authoritative sources. Web and mobile implement their own UI/transport/storage; only generated API types and pure/injected helpers are shared.

## Assessment and remaining review

The selected source demonstrates operator action emphasis, manager exception hierarchy, a coherent light/dark palette, shared table/form styling and a plausible sidebar-to-drawer structure. These observations support the direction. They are not measured task-speed scores, an accessibility pass, native validation or evidence that a Tailwind/NativeWind adapter exists. The narrow Cockpit defect is explicit contrary evidence for direct layout reuse.

There is **no unresolved taste choice blocking token authoring** after the owner locks this handoff. M2 should keep the selected color families and geometry while adjusting functional pairs to pass the both-theme contrast gate, documenting any seed adjustment. M3/M4 own the bounded visual refinements: wrapping long pt-BR table labels and names, readable grouping at large text sizes, target spacing and full-width narrow Cockpit actions. IA's still-open avatar-menu details remain with that catalog entry; this record does not silently decide them. Final logo artwork is separate, non-blocking work.

Review this record and the preserved images before the M1 lock. Once locked, M2 starts with token architecture and its light/dark validation; M3/M4 and app implementation remain separate deliveries. Deferred catalog features continue to wait for their contracts even after visual direction is locked.
