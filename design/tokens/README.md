# Lotero design tokens

> **Status:** Design M2, authored 2026-10-20 (D04); the token architecture and contrast fixes passed independent repeat verification against canonical v54. The integrated v55 financial source reconciliation is recorded in design milestone 2.4.2; it changes no token or enum contract. Light and dark ship together. These files encode the [selected Claude direction](../docs/decisions.md) and its [implementation handoff](../references/claude/implementation-handoff.md), corrected where the handoff measured insufficient contrast.

Tokens are the smallest shared decisions of the Lotero design system — a color for muted text, the height of a touch target, how long a drawer takes to open. They live here as plain JSON that both apps read **at build time**, each through its own small adapter, so `web` and `mobile` draw the same product without sharing a runtime package. Nothing in this folder is imported at runtime, and nothing here knows about Tailwind, NativeWind or React.

## How the system is layered

Every color passes through three layers, and each layer exists so the one above it can change without touching the one below.

1. **Primitive** — the raw palette: tonal ramps of blue, orange, cool neutral, blue-black night, green, red, amber, sky and teal, plus a few named translucent overlays. Primitives are literal hex values and carry no meaning; `blue.600` is just a blue.
2. **Semantic** — what a color is *for*: `surface.default`, `text.muted`, `border.control`, `action.primary.bg-hover`, `selection.indicator`, `focus.ring`. The semantic layer is keyed under both `light` and `dark` with identical names, so a component asks for `text.muted` and receives the right value in either theme.
3. **Domain** — one token per colored value of a backend enum (`daily-close-status.Submitted`) plus the signed-value presentations the screens need (`cash-variance.shortage`). Domain tokens point at semantic tokens wherever a feedback role fits and at primitives only for the aging severity ramp.

**Consumption rule.** App code uses **semantic and domain tokens only** — never a primitive, never a raw hex value. Each app's lint gate rejects color literals and primitive references everywhere except its single token-adapter module (the web Tailwind preset, the mobile NativeWind theme). The non-color categories have no primitive layer: their named scales and roles are what components consume.

## Files and shape

One file per category; each holds exactly one top-level group, and `$`-prefixed keys are documentation metadata that the resolver ignores.

| File | Top-level group | Values | Unit as authored |
|---|---|---|---|
| `colors.json` | `color` | `primitive`, `semantic.{light,dark}`, `domain.{light,dark}`, plus `$contrast` (the declared contrast pairs) | lowercase `#rrggbb`, or `#rrggbbaa` for translucent overlays |
| `typography.json` | `typography` | `family`, `weight`, `size`, `role` | logical px; `letterSpacing` in em |
| `spacing.json` | `spacing` | `scale` (4px grid), `page`, `stack`, `inline`, `inset`, `form`, `size` | logical px |
| `radii.json` | `radius` | scale and named uses | logical px |
| `shadows.json` | `shadow` | `light` and `dark` elevation recipes | logical px; color by reference |
| `breakpoints.json` | `breakpoint` | `medium`, `wide`, `xwide` minimum widths | logical px at a 16px root |
| `motion.json` | `motion` | `duration`, `easing`, `transition`, `reduced` | ms; cubic-bezier `[x1, y1, x2, y2]` |
| `layers.json` | `layer` | z-index order and backdrop opacity | integers; opacity 0–1 |

### Names and references

A token's name is its dot path from the file root, for example `color.semantic.light.surface.default` or `typography.role.table`. Keys use letters, digits and hyphens only — no dots, braces or spaces — so a path can never be ambiguous. Domain values keep the exact backend enum spelling (`Submitted`, `Days31To60`), so an app looks a status up with the string the API returned.

A value is either a literal (string, number, or an array of them) or a **whole-value reference** written `{path.to.token}`. References are never embedded inside other text, always point at a single token rather than a group, and resolve recursively. Layering is enforced: a semantic token references a primitive or a same-theme semantic token; a domain token references a same-theme semantic token or a primitive; non-color files reach color only through a semantic token (the shadow colors do this). Primitives are always literals.

### Themes

`color.semantic` and `color.domain` each contain exactly `light` and `dark`, and both themes must define the same set of tokens. Shadows carry their own `light`/`dark` recipes; every other category is theme-independent. Theme choice is a device preference — **Claro / Escuro / Sistema**, defaulting to Sistema — applied before first paint where the platform allows and kept apart from account data and session storage.

## How each platform reads the files

Both apps resolve the references themselves with a small, dependency-free build step (a few dozen lines: load the eight files, walk the tree, follow `{…}` until a literal appears). Neither app imports from `design/` at runtime.

| Concern | Web (Tailwind preset, Web M3) | Mobile (NativeWind theme, Mobile M3) |
|---|---|---|
| Colors | Emit CSS custom properties per theme (`[data-theme]` / `prefers-color-scheme`) and expose only semantic/domain names to Tailwind; `#rrggbbaa` stays as is | Resolve per theme into the NativeWind theme; translucent values keep their alpha |
| Font sizes, line heights | `rem` = px ÷ 16, so browser text size and zoom scale them | dp/pt with the OS font scale applied (`allowFontScaling` stays on) |
| Spacing, sizes, radii | `rem` = px ÷ 16, so layout grows with text | dp/pt, not font-scaled; touch sizes are hit-area minimums (`hitSlop` may supply the difference) |
| `numeric: tabular-lining` | `font-variant-numeric: tabular-nums lining-nums` | `fontVariant: ['tabular-nums', 'lining-nums']` |
| Breakpoints | Media queries in `em` (px ÷ 16), so enlarged text selects the narrower layout | Compare window width in dp; phones are always the compact arrangement |
| Shadows | `box-shadow: x y blur spread color` | iOS shadow offset/radius/color; Android `elevation` |
| Motion | CSS transitions; `prefers-reduced-motion: reduce` swaps `transition.*` for `reduced.*` | Animated/Reanimated durations; the OS reduce-motion setting swaps in `reduced.*` |
| Layers | `z-index`; native `<dialog>` uses the browser top layer, and popovers opened inside it render in the same layer | Modal/portal order follows the same sequence |

No adapter, preset or component is implemented in this milestone. Native font rendering, text scaling and device behavior are verified when the apps exist; this folder does not claim them.

## Color

The direction is a deep blue frame around cool-neutral work surfaces: blue for structure and ordinary actions, orange reserved for the operator's single next action and the active-navigation marker, turquoise as a quiet secondary accent for Meu turno context. Dark mode keeps the same roles on blue-black surfaces with brighter blue links and lighter feedback tones. The ramps below are derived from the observed prototype anchors (`#005ca9`, `#004b8b`, `#0075bf`, `#f39200`, `#54bbab`, `#f1f5f7`, `#0a1520` and the rest of the handoff table) and the [Caixa reference](../references/caixa-palette.md); they are Lotero's own ramps, not the Caixa games palette.

### Primitive ramps

| Ramp | Steps |
|---|---|
| `blue` | 50 `#eef5fb` · 100 `#d7e8f6` · 200 `#b0d2ee` · 300 `#5fb0ea` · 400 `#2a93d8` · 500 `#0075bf` · 550 `#0068ac` · 600 `#005ca9` · 700 `#004b8b` · 800 `#003f74` · 900 `#082440` · 950 `#061a2e` |
| `orange` | 50 `#fff4e5` · 100 `#ffe3bd` · 200 `#ffc978` · 300 `#ffb347` · 400 `#ffa21f` · 500 `#f39200` · 600 `#d97f00` · 700 `#b46800` · 800 `#7a4600` · 950 `#1b1409` |
| `neutral` | 0 `#ffffff` · 25 `#f6fafb` · 50 `#f4f8f9` · 75 `#f1f5f7` · 100 `#ebf1f3` · 125 `#e9f0f2` · 150 `#e4ecef` · 200 `#d3e0e4` · 300 `#bfd0d6` · 400 `#7a909d` · 450 `#6e8391` · 500 `#546978` · 700 `#33495a` · 800 `#1f3445` · 900 `#0f2230` |
| `night` | 100 `#e4edf2` · 200 `#b6c6d0` · 250 `#93a8b8` · 300 `#8699a7` · 400 `#587389` · 450 `#6a7f91` · 500 `#2c4458` · 600 `#22384a` · 650 `#1a2e3e` · 675 `#1a2d3d` · 700 `#182a3a` · 750 `#142637` · 800 `#132433` · 850 `#0f1d2a` · 900 `#0c1924` · 950 `#0a1520` |
| `green` | 50 `#e8f5ee` · 100 `#cdebdb` · 200 `#9fd8bb` · 300 `#52c48d` · 400 `#2fa66e` · 500 `#1f8a59` · 600 `#146b45` · 700 `#105c3b` · 800 `#0b452c` · 900 `#062c1c` |
| `red` | 50 `#fdeeec` · 100 `#fad5d0` · 200 `#ffb1a8` · 300 `#ff897c` · 400 `#e5584a` · 500 `#cf4334` · 600 `#b33225` · 700 `#992a20` · 800 `#741f17` · 900 `#4d140f` |
| `amber` | 50 `#fff7e6` · 100 `#fdebc8` · 200 `#f9d48f` · 300 `#f2b347` · 400 `#e09a20` · 500 `#c27d00` · 600 `#a86a00` · 700 `#8f5300` · 800 `#6f4100` · 900 `#4a2c00` |
| `sky` | 50 `#eaf6fd` · 100 `#cdeafa` · 200 `#9fd5f5` · 300 `#63b8f0` · 400 `#2f9fe0` · 500 `#0d86c9` · 600 `#0067ad` · 700 `#005592` · 800 `#004473` · 900 `#002f50` |
| `teal` | 50 `#e9f7f4` · 100 `#cdeee7` · 200 `#9fdcd0` · 300 `#6cc9b9` · 400 `#54bbab` · 500 `#3a9f8f` · 600 `#2a8576` · 700 `#226f63` · 800 `#154f46` · 900 `#0c342e` |
| `black` | `#000000` |
| `alpha` | blue-08 `#005ca914` · blue-30 `#0075bf4d` · blue-light-13 `#5fb0ea21` · blue-light-16 `#5fb0ea29` · blue-light-35 `#5fb0ea59` · green-10 `#146b451a` · green-light-14 `#52c48d24` · red-09 `#b3322517` · red-light-13 `#ff897c21` · orange-14 `#f3920024` · orange-16 `#f3920029` · orange-24 `#f392003d` · sky-09 `#0067ad17` · sky-light-13 `#63b8f021` · teal-16 `#54bbab29` · white-05 `#ffffff0d` · white-08 `#ffffff14` · white-12 `#ffffff1f` · white-14 `#ffffff24` · clear `#00000000` · blue-16 `#005ca929` · blue-light-18 `#5fb0ea2e` · red-16 `#b3322529` · red-light-24 `#ff897c3d` · ink-05 `#0f22300d` · ink-18 `#0f22302e` · black-30 `#0000004d` · black-50 `#00000080` |
| `navy` | backdrop `#061626` |

### Semantic tokens in both themes

Each row resolves to the hex shown; the primitive it references follows in parentheses. `surface.selected` and the feedback, overlay and nav-state values are translucent and always sit on an opaque surface.

| Semantic token | Light | Dark |
|---|---|---|
| `accent.secondary-bg` | `#54bbab29` (alpha.teal-16) | `#54bbab29` (alpha.teal-16) |
| `accent.secondary-fg` | `#226f63` (teal.700) | `#6cc9b9` (teal.300) |
| `action.cta.bg` | `#f39200` (orange.500) | `#f39200` (orange.500) |
| `action.cta.bg-active` | `#d97f00` (orange.600) | `#d97f00` (orange.600) |
| `action.cta.bg-hover` | `#ffa21f` (orange.400) | `#ffa21f` (orange.400) |
| `action.cta.fg` | `#1b1409` (orange.950) | `#1b1409` (orange.950) |
| `action.danger.bg` | `#ffffff` (neutral.0) | `#0f1d2a` (night.850) |
| `action.danger.bg-active` | `#b3322529` (alpha.red-16) | `#ff897c3d` (alpha.red-light-24) |
| `action.danger.bg-hover` | `#b3322517` (alpha.red-09) | `#ff897c21` (alpha.red-light-13) |
| `action.danger.border` | `#b33225` (red.600) | `#ff897c` (red.300) |
| `action.danger.fg` | `#b33225` (red.600) | `#ff897c` (red.300) |
| `action.danger-solid.bg` | `#b33225` (red.600) | `#ff897c` (red.300) |
| `action.danger-solid.bg-active` | `#741f17` (red.800) | `#fad5d0` (red.100) |
| `action.danger-solid.bg-hover` | `#992a20` (red.700) | `#ffb1a8` (red.200) |
| `action.danger-solid.fg` | `#ffffff` (neutral.0) | `#0a1520` (night.950) |
| `action.disabled.bg` | `#ebf1f3` (neutral.100) | `#1a2d3d` (night.675) |
| `action.disabled.border` | `#d3e0e4` (neutral.200) | `#22384a` (night.600) |
| `action.disabled.fg` | `#6e8391` (neutral.450) | `#6a7f91` (night.450) |
| `action.ghost.bg` | `#00000000` (alpha.clear) | `#00000000` (alpha.clear) |
| `action.ghost.bg-active` | `#005ca929` (alpha.blue-16) | `#5fb0ea2e` (alpha.blue-light-18) |
| `action.ghost.bg-hover` | `#005ca914` (alpha.blue-08) | `#5fb0ea21` (alpha.blue-light-13) |
| `action.ghost.fg` | `#005ca9` (blue.600) | `#5fb0ea` (blue.300) |
| `action.ghost.fg-hover` | `#004b8b` (blue.700) | `#b0d2ee` (blue.200) |
| `action.primary.bg` | `#005ca9` (blue.600) | `#0075bf` (blue.500) |
| `action.primary.bg-active` | `#003f74` (blue.800) | `#005ca9` (blue.600) |
| `action.primary.bg-hover` | `#004b8b` (blue.700) | `#0068ac` (blue.550) |
| `action.primary.fg` | `#ffffff` (neutral.0) | `#ffffff` (neutral.0) |
| `action.secondary.bg` | `#ffffff` (neutral.0) | `#0f1d2a` (night.850) |
| `action.secondary.bg-active` | `#ebf1f3` (neutral.100) | `#182a3a` (night.700) |
| `action.secondary.bg-hover` | `#f4f8f9` (neutral.50) | `#142637` (night.750) |
| `action.secondary.border` | `#7a909d` (neutral.400) | `#587389` (night.400) |
| `action.secondary.fg` | `#0f2230` (neutral.900) | `#e4edf2` (night.100) |
| `background` | `#f1f5f7` (neutral.75) | `#0a1520` (night.950) |
| `border.control` | `#7a909d` (neutral.400) | `#587389` (night.400) |
| `border.control-hover` | `#546978` (neutral.500) | `#8699a7` (night.300) |
| `border.control-invalid` | `#b33225` (red.600) | `#ff897c` (red.300) |
| `border.default` | `#d3e0e4` (neutral.200) | `#22384a` (night.600) |
| `border.strong` | `#bfd0d6` (neutral.300) | `#2c4458` (night.500) |
| `border.subtle` | `#e4ecef` (neutral.150) | `#1a2e3e` (night.650) |
| `feedback.danger.bg` | `#b3322517` (alpha.red-09) | `#ff897c21` (alpha.red-light-13) |
| `feedback.danger.border` | `#b33225` (red.600) | `#ff897c` (red.300) |
| `feedback.danger.fg` | `#b33225` (red.600) | `#ffb1a8` (red.200) |
| `feedback.info.bg` | `#0067ad17` (alpha.sky-09) | `#63b8f021` (alpha.sky-light-13) |
| `feedback.info.border` | `#0067ad` (sky.600) | `#63b8f0` (sky.300) |
| `feedback.info.fg` | `#005592` (sky.700) | `#9fd5f5` (sky.200) |
| `feedback.neutral.bg` | `#ebf1f3` (neutral.100) | `#1a2d3d` (night.675) |
| `feedback.neutral.border` | `#bfd0d6` (neutral.300) | `#2c4458` (night.500) |
| `feedback.neutral.fg` | `#33495a` (neutral.700) | `#b6c6d0` (night.200) |
| `feedback.success.bg` | `#146b451a` (alpha.green-10) | `#52c48d24` (alpha.green-light-14) |
| `feedback.success.border` | `#146b45` (green.600) | `#52c48d` (green.300) |
| `feedback.success.fg` | `#146b45` (green.600) | `#9fd8bb` (green.200) |
| `feedback.warning.bg` | `#f3920024` (alpha.orange-14) | `#f3920029` (alpha.orange-16) |
| `feedback.warning.border` | `#f39200` (orange.500) | `#f39200` (orange.500) |
| `feedback.warning.fg` | `#8f5300` (amber.700) | `#f2b347` (amber.300) |
| `focus.halo` | `#0075bf4d` (alpha.blue-30) | `#5fb0ea59` (alpha.blue-light-35) |
| `focus.ring` | `#005ca9` (blue.600) | `#5fb0ea` (blue.300) |
| `focus.ring-on-nav` | `#ffffff` (neutral.0) | `#ffffff` (neutral.0) |
| `nav.active` | `#ffffff24` (alpha.white-14) | `#5fb0ea29` (alpha.blue-light-16) |
| `nav.bg` | `#004b8b` (blue.700) | `#082440` (blue.900) |
| `nav.divider` | `#ffffff1f` (alpha.white-12) | `#ffffff14` (alpha.white-08) |
| `nav.hover` | `#ffffff14` (alpha.white-08) | `#ffffff0d` (alpha.white-05) |
| `nav.marker` | `#f39200` (orange.500) | `#f39200` (orange.500) |
| `nav.text` | `#eef5fb` (blue.50) | `#e4edf2` (night.100) |
| `nav.text-muted` | `#b0d2ee` (blue.200) | `#93a8b8` (night.250) |
| `nav.text-strong` | `#ffffff` (neutral.0) | `#ffffff` (neutral.0) |
| `overlay.backdrop-base` | `#061626` (navy.backdrop) | `#000000` (black) |
| `selection.indicator` | `#005ca9` (blue.600) | `#5fb0ea` (blue.300) |
| `selection.indicator-fg` | `#ffffff` (neutral.0) | `#0a1520` (night.950) |
| `shadow.floating` | `#0f22302e` (alpha.ink-18) | `#00000080` (alpha.black-50) |
| `shadow.raised` | `#0f22300d` (alpha.ink-05) | `#0000004d` (alpha.black-30) |
| `skeleton.base` | `#ebf1f3` (neutral.100) | `#1a2d3d` (night.675) |
| `skeleton.highlight` | `#f4f8f9` (neutral.50) | `#182a3a` (night.700) |
| `surface.default` | `#ffffff` (neutral.0) | `#0f1d2a` (night.850) |
| `surface.hover` | `#f6fafb` (neutral.25) | `#142637` (night.750) |
| `surface.muted` | `#e9f0f2` (neutral.125) | `#182a3a` (night.700) |
| `surface.raised` | `#ffffff` (neutral.0) | `#132433` (night.800) |
| `surface.selected` | `#005ca914` (alpha.blue-08) | `#5fb0ea21` (alpha.blue-light-13) |
| `surface.subtle` | `#f4f8f9` (neutral.50) | `#132433` (night.800) |
| `text.disabled` | `#6e8391` (neutral.450) | `#6a7f91` (night.450) |
| `text.link` | `#005ca9` (blue.600) | `#5fb0ea` (blue.300) |
| `text.link-hover` | `#004b8b` (blue.700) | `#b0d2ee` (blue.200) |
| `text.muted` | `#546978` (neutral.500) | `#93a8b8` (night.250) |
| `text.placeholder` | `#546978` (neutral.500) | `#93a8b8` (night.250) |
| `text.primary` | `#0f2230` (neutral.900) | `#e4edf2` (night.100) |
| `text.secondary` | `#33495a` (neutral.700) | `#b6c6d0` (night.200) |
| `topbar.bg` | `#ffffff` (neutral.0) | `#0c1924` (night.900) |
| `topbar.border` | `#d3e0e4` (neutral.200) | `#22384a` (night.600) |

### What changed from the prototype, and why

The selected look is preserved; only pairs that failed the gate moved, by the smallest step that passes.

| Pair | Prototype | Token now | Result |
|---|---|---|---|
| Light navigation group labels on the sidebar | `rgba(214,231,243,.66)` on `#004b8b`, 3.96:1 | `nav.text-muted` = `#b0d2ee`, opaque | 5.59:1 |
| White label on dark primary hover | `#0a86d6`, 3.89:1 | `action.primary.bg-hover` = `#0068ac` (hover deepens, as in light) | 5.87:1 |
| Light field boundary | `#bfd0d6`, 1.59:1 on white | `border.control` = `#7a909d` | 3.33:1 on white, 3.04:1 on the page background |
| Dark field boundary | `#2c4458`, 1.69:1 | `border.control` = `#587389` | 3.44:1 on the surface, 3.18:1 on raised/subtle |
| Light turquoise text (Meu turno badge) | `#2a8576`, 4.45:1 on white, 3.93:1 on its tint | `accent.secondary-fg` = `#226f63` | ≥ 5.25:1 on every checked base |
| Light muted text on the read-only field fill | `#5b7180` on `#e9f0f2`, 4.42:1 | `text.muted` = `#546978` | 4.96:1 |
| Light feedback text on its tint over a selected row | success/danger ≈ 4.46–4.51:1 | success `#146b45`, danger `#b33225`, warning `#8f5300` | ≥ 4.63:1 |
| Dark danger text on its tint over a selected row | `#ff7d70`, 4.52:1 | `#ff897c` | ≥ 4.80:1 |
| Selection indicator on a selected row (dark) | checkbox fill `#0075bf` over the selected-row tint, 2.78:1 | `selection.indicator` = `#5fb0ea` with a `#0a1520` check mark (light keeps `#005ca9`/white) | ≥ 5.14:1 dark, ≥ 5.72:1 light |
| Focus ring | `0 0 0 3px rgba(0,117,191,.30)` halo, ≈ 1.5:1 | solid `focus.ring` (2px outline, 2px offset) plus the optional decorative `focus.halo`; white `focus.ring-on-nav` inside the sidebar | ≥ 5.87:1 light, ≥ 6.21:1 dark |

Decorative dividers (`border.subtle`, `border.default`, `border.strong`) keep the prototype's light hairlines: they separate content but do not identify a control, so they are not held to 3:1. Only boundaries that identify an operable control (`border.control*`, outlined button borders) are.

### Domain tokens

Status is always named in text; color is a second signal, never the only one. Badges use a subtle tint with the feedback tone as text; the 91+ day aging bucket is the one solid badge, because it is the most urgent state a report shows.

| Domain group (`$enum`) | Value | Label (copy §5) | Light fg / bg | Dark fg / bg | Lowest ratio L / D |
|---|---|---|---|---|---|
| transaction-status (TransactionStatus) | `Draft` | Rascunho | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| transaction-status (TransactionStatus) | `Active` | Ativa | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| transaction-status (TransactionStatus) | `Cancelled` | Cancelada | `#546978` / `#e9f0f2` | `#93a8b8` / `#182a3a` | 4.96 / 5.96 |
| daily-close-status (DailyCloseStatus) | `Draft` | Rascunho | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| daily-close-status (DailyCloseStatus) | `Submitted` | Enviado | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| daily-close-status (DailyCloseStatus) | `Approved` | Aprovado | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| daily-close-status (DailyCloseStatus) | `Rejected` | Rejeitado | `#b33225` / `#b3322517` | `#ffb1a8` / `#ff897c21` | 4.57 / 5.76 |
| direction (Direction) | `In` | Entrada | `#0f2230` / `surface` | `#e4edf2` / `surface` | 13.74 / 10.24 |
| direction (Direction) | `Out` | Saída | `#0f2230` / `surface` | `#e4edf2` / `surface` | 13.74 / 10.24 |
| aging-bucket (AgingBucket) | `Current` | Em dia | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| aging-bucket (AgingBucket) | `Days0To30` | 0–30 dias | `#8f5300` / `#fff7e6` | `#f2b347` / `#f3920029` | 5.78 / 5.19 |
| aging-bucket (AgingBucket) | `Days31To60` | 31–60 dias | `#6f4100` / `#fdebc8` | `#f9d48f` / `#f392003d` | 7.36 / 5.97 |
| aging-bucket (AgingBucket) | `Days61To90` | 61–90 dias | `#992a20` / `#fdeeec` | `#ffb1a8` / `#ff897c21` | 6.88 / 5.76 |
| aging-bucket (AgingBucket) | `Days91Plus` | Mais de 90 dias | `#ffffff` / `#b33225` | `#0a1520` / `#ff897c` | 6.18 / 7.99 |
| time-entry-status (TimeEntryStatus) | `Present` | Presente | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| time-entry-status (TimeEntryStatus) | `DayOff` | Folga | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| time-entry-status (TimeEntryStatus) | `Sunday` | Domingo | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| time-entry-status (TimeEntryStatus) | `Holiday` | Feriado | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| time-entry-status (TimeEntryStatus) | `Vacation` | Férias | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| time-entry-status (TimeEntryStatus) | `JustifiedAbsence` | Falta justificada | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| time-entry-status (TimeEntryStatus) | `UnjustifiedAbsence` | Falta injustificada | `#b33225` / `#b3322517` | `#ffb1a8` / `#ff897c21` | 4.57 / 5.76 |
| installment-plan-status (InstallmentPlanStatus) | `Draft` | Rascunho | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| installment-plan-status (InstallmentPlanStatus) | `Active` | Ativo | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| installment-plan-status (InstallmentPlanStatus) | `Mixed` | Misto | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| installment-plan-status (InstallmentPlanStatus) | `Cancelled` | Cancelado | `#546978` / `#e9f0f2` | `#93a8b8` / `#182a3a` | 4.96 / 5.96 |
| invitation-status (InvitationStatus) | `Pending` | Pendente | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| invitation-status (InvitationStatus) | `Accepted` | Aceito | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| invitation-status (InvitationStatus) | `Declined` | Recusado | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| invitation-status (InvitationStatus) | `Expired` | Expirado | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| mail-delivery-status (MailDeliveryStatus) | `Pending` | Envio pendente | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| mail-delivery-status (MailDeliveryStatus) | `Sent` | Enviado ao servidor de e-mail | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| mail-delivery-status (MailDeliveryStatus) | `Failed` | Falha no envio | `#b33225` / `#b3322517` | `#ffb1a8` / `#ff897c21` | 4.57 / 5.76 |
| operator-link-request-status (OperatorLinkRequestStatus) | `Pending` | Pendente | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| operator-link-request-status (OperatorLinkRequestStatus) | `Resolved` | Resolvida | `#146b45` / `#146b451a` | `#9fd8bb` / `#52c48d24` | 4.79 / 5.81 |
| review-event-type (DailyCloseReviewEventType) | `Rejected` | Rejeitado na conferência | `#b33225` / `#b3322517` | `#ffb1a8` / `#ff897c21` | 4.57 / 5.76 |
| review-event-type (DailyCloseReviewEventType) | `OpeningChanged` | Abertura alterada | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| settlement-instrument (SettlementInstrument) | `None` | Sem classificação | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| settlement-instrument (SettlementInstrument) | `Cheque` | Cheque | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| settlement-instrument (SettlementInstrument) | `CreditCard` | Cartão de crédito | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| settlement-instrument (SettlementInstrument) | `DebitCard` | Cartão de débito | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| monthly-financial-role (MonthlyFinancialRole) | `None` | Nenhuma | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| monthly-financial-role (MonthlyFinancialRole) | `BolaoServiceFee` | Tarifa de bolão | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| monthly-financial-role (MonthlyFinancialRole) | `CommercialLeftover` | Sobra (perda comercial) | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| overview-section-status (OverviewSectionStatus) | `Ready` | Atualizado | `#005592` / `#0067ad17` | `#9fd5f5` / `#63b8f021` | 5.78 / 6.04 |
| overview-section-status (OverviewSectionStatus) | `Empty` | Nada no período | `#33495a` / `#ebf1f3` | `#b6c6d0` / `#1a2d3d` | 8.21 / 8.06 |
| overview-section-status (OverviewSectionStatus) | `NotConfigured` | Não configurado | `#8f5300` / `#f3920024` | `#f2b347` / `#f3920029` | 4.70 / 5.19 |
| cash-variance (—) | `shortage` | −R$ … · falta | `#b33225` / `surface` | `#ffb1a8` / `surface` | 5.22 / 7.01 |
| cash-variance (—) | `surplus` | +R$ … · sobra | `#146b45` / `surface` | `#9fd8bb` / `surface` | 5.52 / 7.53 |
| cash-variance (—) | `zero` | R$ 0,00 | `#0f2230` / `surface` | `#e4edf2` / `surface` | 13.74 / 10.24 |
| fiado-balance (—) | `debt` | R$ … · em aberto | `#0f2230` / `surface` | `#e4edf2` / `surface` | 13.74 / 10.24 |
| fiado-balance (—) | `settled` | quitado | `#33495a` / `surface` | `#b6c6d0` / `surface` | 7.92 / 6.93 |
| fiado-balance (—) | `credit` | +R$ … · crédito do cliente | `#146b45` / `surface` | `#9fd8bb` / `surface` | 5.52 / 7.53 |
| hour-balance (—) | `positive` | +2h 15min | `#146b45` / `surface` | `#9fd8bb` / `surface` | 5.52 / 7.53 |
| hour-balance (—) | `negative` | −2h 15min | `#b33225` / `surface` | `#ffb1a8` / `surface` | 5.22 / 7.01 |
| hour-balance (—) | `zero` | 0h 00min | `#0f2230` / `surface` | `#e4edf2` / `surface` | 13.74 / 10.24 |
| generated-movement (—) | `automatic` | Automática | `#33495a` / `border #bfd0d6` | `#b6c6d0` / `border #2c4458` | 7.92 / 6.93 |

A few choices deserve their reasons:

- **Direction is deliberately uncolored.** `In` and `Out` resolve to the same neutral text color. An entrada is not revenue and a saída is not an expense — a cash withdrawal for a prize, a fiado sale and a deposit are all saídas — so the label, the column and the sign rules of [formatting §2–3](../specs/formatting-ptbr.md) carry direction, not a profit/loss hue.
- **Signed values follow their own meaning.** Cash variance colors a shortage (falta) with the danger tone and a surplus (sobra) with the success tone, as the selected prototype does, while the sign and the words **falta**/**sobra** always accompany the figure. Hour balances follow the same rule. A client's fiado credit uses the success tone with the mandatory **crédito do cliente** label; debt stays in the primary text color because it is the ordinary state of a fiado ledger, not an alarm.
- **The generated-movement marker is not a status.** `generated-movement.automatic` is an outlined neutral **Automática** marker that sits beside the type, never in the status column.
- **M11 classifications are categories, not alarms.** A settlement instrument is a neutral fact about a receivable, so every classified instrument uses the info tone and *Sem classificação* the neutral one; the label always names the instrument. The monthly financial role marks the fee with info and the leftover — a commercial loss — with warning. A Dashboard section that is *Não configurado* uses warning because it asks the manager to act, *Nada no período* is neutral, and *Atualizado* info: a section's status is never a money signal, and a section that is not configured is never rendered as zero.
- **Enum coverage is exact.** Each `$enum` group must list precisely the values of the same-named enum in the committed OpenAPI document (`shared/api/openapi/server-api.openapi.json`). A backend enum change fails the gate until the domain tokens and the [copy labels](../specs/copy-guidelines.md) are reconciled; nothing here invents an enum value. Enums that render only as text labels (role, account type, settlement rule, holiday source, blocker type, payment kind) have no domain color; the three M11 enums above (`SettlementInstrument`, `MonthlyFinancialRole`, `OverviewSectionStatus`) do, and the checker requires them in both themes.

## The contrast gate

```bash
node scripts/check-contrast.mjs
```

The checker is a zero-dependency Node script. It loads the eight files, refuses anything malformed (invalid JSON, unexpected or missing files, bad keys, malformed or broken references, references to groups, cycles, invalid values, literals in the semantic/domain layers, cross-theme references, missing themes or theme asymmetry, missing or invented domain values), and then checks contrast in **both themes**:

- **Every action state**, generated structurally: each `action.<variant>` must define `bg`, `bg-hover`, `bg-active` and `fg` (disabled: `bg`, `fg`, `border`), and the checker tests the label on each fill — translucent fills composited over the variant's own opaque default or, for the clear text/link fill, over the default, subtle and page surfaces — plus `fg-hover` on the hover fill. A missing state or a failing fill fails the gate.
- **Declared pairs** in `colors.json` → `$contrast.pairs`: each pair is a list of foregrounds crossed with a list of backgrounds. A background is either one opaque token or a bottom-to-top stack, so translucent tokens are always composited onto the surface they actually sit on; a translucent layer without an opaque base is an error.
- **Every domain value** on every surface in `$contrast.domainSurfaces`, generated structurally — badge text on its tint over the page background, surfaces, hover and selected rows. The list must contain the seven required surfaces, including selected rows with hover/focus, and every value must have a passing result on each, so emptying or trimming the list fails instead of silently skipping statuses.
- **Coverage rules**: every semantic text token, every `fg`/`fg-hover`, the navigation text tokens and the secondary accent must appear in at least one text check, and every control boundary, focus ring and selection token in at least one non-text check. A pair list that leaves a token out fails, which is what keeps the gate from sampling only convenient colors.
- **Thresholds**: 4.5:1 for text; 3:1 for essential non-text indicators (control boundaries, focus rings, the selection indicator and its check mark). A `large-text` pair (3:1) is accepted only with a written justification naming the actual size and weight — none is used today. Disabled labels are exempt under WCAG; the project still holds them to a 3:1 floor so a faded **Salvar** stays legible. Thresholds below WCAG AA are rejected.

`node scripts/check-contrast.mjs --report` prints every checked combination. `node scripts/test-check-contrast.mjs` runs the disposable regression suite: the true source passes, and broken, cyclic and malformed references, missing themes, missing or invented domain values, a renamed contract enum, failing text, boundary and badge pairs, emptied or trimmed domain surfaces, missing or failing action states, a selection indicator failing on a selected row, an unjustified large-text exemption, relaxed thresholds, coverage holes, missing files and invalid JSON or easings all fail — including a missing M11 domain in one theme, an invented instrument and a missing role value (40 scenarios) — on temporary copies, with the source files hashed before and after.

### Results (2026-10-20)

The selected hover/focus surface is now checked for body text, feedback text and every domain badge. Dark muted/placeholder text uses night.250; feedback and the 61–90 day badge foregrounds were retuned to preserve the 4.5:1 floor on that stack. Thresholds and existing pairs remain in force.

All 1164 combinations pass — 582 per theme. The lowest text ratios are 4.57:1 (light) and 4.76:1 (dark); the lowest essential boundary is 3.04:1 (light control border on the page background).

| Check | Kind | Combinations per theme | Lowest light | Lowest dark |
|---|---|---|---|---|
| body text on every surface | text | 48 | 4.84 | 4.94 |
| disabled label (WCAG-exempt; project floor) | disabled | 6 | 3.42 | 3.40 |
| secondary accent (Meu turno) text | text | 3 | 5.25 | 6.48 |
| navigation text | text | 6 | 5.63 | 9.82 |
| navigation group labels | text | 1 | 5.59 | 6.38 |
| top bar text | text | 4 | 5.72 | 7.23 |
| essential control boundary | non-text | 20 | 3.04 | 3.18 |
| focus indicator | non-text | 6 | 5.87 | 6.21 |
| focus indicator in navigation | non-text | 2 | 6.20 | 11.65 |
| selection indicator (checkbox, radio, switch, selected tab) | non-text | 7 | 5.72 | 5.14 |
| check mark on the selection indicator | non-text | 1 | 6.77 | 7.79 |
| success feedback text on its own tint | text | 6 | 5.01 | 6.43 |
| danger feedback text on its own tint | text | 6 | 4.78 | 6.37 |
| warning feedback text on its own tint | text | 6 | 4.90 | 5.71 |
| info feedback text on its own tint | text | 6 | 6.05 | 6.70 |
| neutral feedback text on its own tint | text | 6 | 8.21 | 8.06 |
| feedback text directly on surfaces | text | 28 | 5.21 | 6.55 |
| action cta bg (structural) | text | 1 | 7.75 | 7.75 |
| action cta bg-active (structural) | text | 1 | 6.07 | 6.07 |
| action cta bg-hover (structural) | text | 1 | 9.07 | 9.07 |
| action danger bg (structural) | text | 1 | 6.18 | 7.42 |
| action danger bg-active (structural) | text | 1 | 4.79 | 4.82 |
| action danger bg-hover (structural) | text | 1 | 5.37 | 6.03 |
| action danger-solid bg (structural) | text | 1 | 6.18 | 7.99 |
| action danger-solid bg-active (structural) | text | 1 | 10.76 | 13.58 |
| action danger-solid bg-hover (structural) | text | 1 | 7.76 | 10.61 |
| action disabled bg (structural) | disabled | 1 | 3.46 | 3.40 |
| action ghost bg (structural) | text | 3 | 6.17 | 6.69 |
| action ghost bg-active (structural) | text | 3 | 4.83 | 4.76 |
| action ghost bg-hover (structural) | text | 6 | 5.49 | 5.28 |
| action primary bg (structural) | text | 1 | 6.77 | 4.89 |
| action primary bg-active (structural) | text | 1 | 10.70 | 6.77 |
| action primary bg-hover (structural) | text | 1 | 8.83 | 5.87 |
| action secondary bg (structural) | text | 1 | 16.26 | 14.39 |
| action secondary bg-active (structural) | text | 1 | 14.25 | 12.36 |
| action secondary bg-hover (structural) | text | 1 | 15.20 | 12.99 |
| every domain value on every domain surface (structural) | text | 346 | 4.63 | 4.76 |

**What the gate proves, and what it does not.** It proves that the declared color pairs meet their thresholds when composited as declared. It does not prove that an app uses the right token on the right surface, that focus is visible in every component state, that text reflows at 200%, or that a screen reader announces anything. Those belong to the component contract and to each app's real-UI verification.

## Typography

System fonts carry the product: the Apple system face, Segoe UI and Roboto, falling back to Helvetica Neue and Arial — no downloaded display face and no CAIXA wordmark. Mono is only for short identifiers. Money, counts, dates and times in tables and summaries use tabular lining figures and right alignment so columns line up.

| Role | Size / line height | Weight | Notes |
|---|---|---|---|
| `page-title` | 28 / 34 | 600, −0.01em | `page-title-compact` 22 / 28 below `breakpoint.medium` |
| `next-action` | 26 / 32 | 600, −0.01em | the Cockpit's one next step |
| `summary-value` | 24 / 30 | 600, tabular | summary-strip figures |
| `section-title` | 18 / 26 | 600 | section heading above a rule |
| `body`, `control`, `money` | 16 / 24 | 400 (`money` tabular) | `body-strong` 600 |
| `button` | 16 / 24 | 600 | `button-small` 14 / 20 |
| `table` | 15 / 22 | 400, tabular | table cells |
| `label`, `helper`, `code` | 14 / 20 | 600 / 400 / 400 mono | field labels, helper and error text |
| `table-header` | 13 / 18 | 600, uppercase, 0.05em | column headers |
| `badge` | 13 / 18 | 600, 0.02em | status badges |
| `overline` | 13 / 18 | 600, uppercase, 0.06em | navigation group labels |

Weights resolve per platform: Segoe UI and the Apple system face render 600 directly; Roboto on Android rounds 600 to its nearest available weight. The 400/600 contrast between body and headings is the one that must survive.

### Relation to the selected screenshots

The prototype authored a 14px body, but the owner judged it at the prototype's **150% CSS zoom**, which enlarged text *and* layout together (the 252px sidebar became 378px). Copying 14px at 100% would look much smaller than the selected captures; copying the 21px zoomed size would turn every dense table into a scroll. The scale above sits at roughly 1.15× the authored values:

| Role | Authored | As captured at 150% | Token |
|---|---|---|---|
| Body | 14 | 21 | 16 |
| Table body | 13.5 | 20.25 | 15 |
| Helper | 12.5 | 18.75 | 14 |
| Badge / overline | 11.5 | 17.25 | 13 |
| Table header | 11 | 16.5 | 13 |
| Section heading | 15 | 22.5 | 18 |
| Summary value | 20 | 30 | 24 |
| Next action | 22 | 33 | 26 |
| Page heading | 24 | 36 | 28 |

At the browser's default size the app is therefore denser than the captures and noticeably larger than the authored values; at about 130% browser zoom it reproduces the selected desktop reading size, and because sizes are `rem` and breakpoints `em`, that zoom produces real reflow rather than a scaled bitmap of the desktop layout. Phones, where the prototype clamped to 100% (a 14px body), get the same 16px body before the OS font scale. If the owner prefers the captured size as the desktop default, scaling `typography.size` and `spacing.size` together is a one-file change that the gate re-validates; it is recorded here as a review question rather than decided silently.

## Spacing, sizes and touch

Spacing sits on a 4px grid: `scale` 0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64. Named uses reference it — `page.inline-wide` 32 and `page.inline-compact` 16 for page gutters, `page.section-gap` 32 between ruled sections, `inset.table-cell-y` 12 with `inset.table-cell-x` 12 and `inset.table-edge-x` 16 for dense but tappable rows, `inset.panel` 16 and `inset.dialog` 24, and `form.column-min` 224 with 16 gaps so form columns stack when they no longer fit.

| Size token | Value | Use |
|---|---|---|
| `size.control-height` | 40 | fields and buttons with a fine pointer (web desktop) |
| `size.control-height-touch` | 48 | fields and buttons on touch screens and native |
| `size.control-height-small` | 32 | compact web buttons; the hit area still meets `touch-target-min` on touch |
| `size.touch-target-min` / `-comfortable` | 44 / 48 | minimum hit area for any standalone action, including row undo and icon buttons; 48 on Android |
| `size.touch-target-gap` | 8 | minimum separation between adjacent targets |
| `size.icon` / `icon-large` | 16 / 20 | outline icons, about 1.6px stroke |
| `size.focus-ring-width` / `-offset` | 2 / 2 | focus outline drawn outside the control |
| `size.border-width` / `marker-width` | 1 / 3 | hairlines; the active-navigation and notice marker |
| `size.avatar` | 32 | avatar in the top bar |
| `size.sidebar` | 256 | persistent navigation width (grows with text on web) |
| `size.topbar-min` | 56 | top bar minimum height; it grows when text wraps |
| `size.drawer-max` / `menu-max` / `dialog-max` | 304 / 320 / 560 | caps; each is also limited to the viewport minus 32 |
| `size.form-max` / `content-max` | 768 / 1280 | readable form and content widths |

The prototype's 36px controls and 28px small buttons are observations, not touch sizing: on touch devices every control meets the 44/48 minimums, and small visual controls extend their hit area instead of shrinking it.

## Radii

`radius.md` (4) is the ceiling for controls, panels, dialogs and focus outlines; `radius.sm` (3) is for badges and `radius.xs` (2) for tiny inline marks. `radius.full` is reserved for avatars and indicator dots. Pill-shaped buttons and rounded cards are outside the direction.

## Elevation

Elevation is scarce: prefer rules and surface changes to stacked cards.

| Token | Light | Dark | Use |
|---|---|---|---|
| `shadow.<theme>.raised` | 0 1 2 0, `#0f22300d` | 0 1 2 0, `#0000004d` | sticky top bar and sticky table headers; Android elevation 1 |
| `shadow.<theme>.floating` | 0 14 36 0, `#0f22302e` | 0 16 40 0, `#00000080` | menus, popovers, drawers, dialogs, toasts; Android elevation 8 |

## Breakpoints

Layouts change where content stops fitting, measured in `em` on the web so that enlarged text moves a screen to the narrower arrangement instead of overflowing.

| Width | Arrangement |
|---|---|
| below `medium` (640 / 40em) | **compact**: one column; navigation in an off-canvas drawer; fields, summary strips and the Cockpit's blocks stack; dense tables scroll inside their own labelled region |
| `medium` to `wide` (640–1024 / 40–64em) | **medium**: drawer navigation; two-column forms where `form.column-min` allows; summary strips in two columns |
| from `wide` (1024 / 64em) | **wide**: persistent sidebar beside the workspace; full table layouts |
| from `xwide` (1440 / 90em) | content stops at `size.content-max` and centers |

The prototype's authored thresholds (860px and 560px) were multiplied by its zoom at runtime; these replace them with content-aware values. Native phones always use the compact arrangement; tablets compare their window width in dp against the same numbers.

## Motion

| Transition | Duration | Easing | Reduced motion |
|---|---|---|---|
| `control` (hover, press, focus) | 120 ms | standard | instant |
| `disclosure` (collapsible help, expandable rows) | 120 ms | standard | instant |
| `drawer-enter` / `drawer-exit` | 180 / 120 ms | enter / exit | instant |
| `dialog-enter` / `dialog-exit` | 180 / 120 ms | enter / exit | instant |
| `toast` | 180 ms | standard | instant |
| `skeleton` shimmer | 1200 ms loop | linear | none — a static placeholder |

Easings are `standard` [0.2, 0, 0, 1], `enter` [0, 0, 0.2, 1], `exit` [0.4, 0, 1, 1] and `linear`. Theme changes swap without a cross-fade. With reduced motion on, nothing slides, scales or shimmers; state still changes immediately and visibly.

## Layers

From the bottom: `base` 0, `raised` 1, `sticky` 100 (top bar, sticky headers), `drawer-backdrop` 200, `drawer` 210, `dialog-backdrop` 400, `dialog` 410, `popover` 500 (menus, selects, date pickers — above dialogs because they open inside them), `toast` 600, `skip-link` 700. Backdrops use `color.semantic.<theme>.overlay.backdrop-base` at `layer.backdrop.opacity` (0.46 light, 0.6 dark). The prototype's colliding values (its statebar shared 60 with the session menu) are not carried over.

## Changing tokens

Edit the JSON, then run `node scripts/check-contrast.mjs` and `node scripts/test-check-contrast.mjs`. When a backend enum gains a value, add its domain token in both themes and its pt-BR label in [copy §5](../specs/copy-guidelines.md) in the same change; the gate fails until both exist. Keep the tables in this file in step with the values — they were generated from the files and the checker report on 2026-10-20.
