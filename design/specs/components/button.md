# Button

**Purpose.** Starts an action or a navigation the user chose. Blue carries ordinary actions; orange is the scarce operator call to action — the Cockpit's single next step — and appears at most once per screen. Destructive actions are outlined, so a danger fill never competes with the primary action. Text links that move between records use the link variant.

## Anatomy

Container (fill and/or border) · optional leading icon (`spacing.size.icon`) · label · optional trailing spinner while loading. The label is always present; an icon-only button exists only where a visible label cannot fit (the top-bar theme and menu triggers) and then carries an accessible name.

## Variants

| Variant | Use |
|---|---|
| Primary | the main ordinary action of a region (**Salvar**, **Aprovar**, **Filtrar**) |
| CTA | the operator's next action on the Cockpit (**Abrir dia**, **Fechar caixa**, **Corrigir fechamento**) — once per screen |
| Secondary (outline) | alternative actions (**Voltar**, **Dia anterior**, **Ver pendências de outros operadores**) |
| Text / link | low-emphasis actions and record navigation (**Abrir transação**, **Desfazer repetição**, **Ver fechamento de hoje**) |
| Danger (outline) | destructive or irreversible actions (**Cancelar transação**, **Remover acesso**, **Rejeitar**) |
| Danger (solid) | the confirming button inside a destructive confirmation dialog only |

**Token mapping.** Every variant defines all four states; a translucent fill is drawn over the surface the button sits on (text/link) or over the variant's own default fill (outlined danger). `scripts/check-contrast.mjs` checks each label on each fill in both themes and fails when a state token is missing.

| Variant | Default fill | Hover fill | Pressed fill | Label (hover label) | Boundary |
|---|---|---|---|---|---|
| Primary | `sem.action.primary.bg` | `sem.action.primary.bg-hover` | `sem.action.primary.bg-active` | `sem.action.primary.fg` | — |
| CTA | `sem.action.cta.bg` | `sem.action.cta.bg-hover` | `sem.action.cta.bg-active` | `sem.action.cta.fg` | — |
| Secondary | `sem.action.secondary.bg` | `sem.action.secondary.bg-hover` | `sem.action.secondary.bg-active` | `sem.action.secondary.fg` | `sem.action.secondary.border` |
| Text / link | `sem.action.ghost.bg` (clear) | `sem.action.ghost.bg-hover` | `sem.action.ghost.bg-active` | `sem.action.ghost.fg` (`sem.action.ghost.fg-hover`) | — |
| Danger (outline) | `sem.action.danger.bg` | `sem.action.danger.bg-hover` | `sem.action.danger.bg-active` | `sem.action.danger.fg` | `sem.action.danger.border` |
| Danger (solid) | `sem.action.danger-solid.bg` | `sem.action.danger-solid.bg-hover` | `sem.action.danger-solid.bg-active` | `sem.action.danger-solid.fg` | — |
| Disabled (any variant) | `sem.action.disabled.bg` | — | — | `sem.action.disabled.fg` | `sem.action.disabled.border` |

On native, "pressed" is the touch-down state and there is no hover; the pressed fill is used for the duration of the press.

Sizes: default height `spacing.size.control-height` (web, fine pointer) or `spacing.size.control-height-touch` (touch screens and native); small `spacing.size.control-height-small` only on web with a fine pointer, where its hit area still reaches `spacing.size.touch-target-min` on touch. Horizontal padding `spacing.inset.button-x`; radius `radius.control`; label `typography.role.button` (`typography.role.button-small` for small).

## States

| State | Presentation | Behavior |
|---|---|---|
| Default / hover / pressed | the variant's default, hover and pressed fills from the mapping above | hover is never the only cue; `motion.transition.control` |
| Focus | `sem.focus.ring` outline outside the container (`sem.focus.ring-on-nav` in navigation) | keyboard and switch focus always visible |
| Disabled | `sem.action.disabled.bg`, `sem.action.disabled.fg`, `sem.action.disabled.border` | reason shown as adjacent text (Close day's clean **Salvar**: "Nenhuma alteração desde o último salvamento"); not activatable |
| Loading | label changes to the in-progress verb (**Salvando…**, **Enviando…**) with a spinner; width does not jump | activation blocked until the request settles; other controls stay usable unless the screen says otherwise |
| Uncertain | the button itself returns to its idle label; the screen shows the uncertain Notice and offers the catalog's verification action (**Verificar e tentar novamente**) | never re-enables a fresh financial create in place of verification |

A button never shows success itself; confirmation appears in the Notice, SaveStatus or Toast the screen names.

## Behavior

- One activation per intent: the handler disables the button for the request's lifetime so a double tap cannot submit twice. Financial creates freeze their payload and key before dispatch (screen catalog, cross-screen patterns).
- Enter submits a form only through its declared submit button; Enter in a field never triggers a destructive or financial action that the user did not focus.
- Destructive actions open a [confirmation dialog](modal-sheet.md) that states the consequence; the dialog's confirm label names the object (**Cancelar transação**), never "OK"/"Sim".
- Link-variant buttons that navigate are real links (web `<a href>`, native link role) so open-in-new-tab and native back behave.

## Accessibility

Native `<button>`/`<a>` on web; `accessibilityRole="button"`/`"link"` on native. The accessible name is the visible label, plus row context when several identical labels repeat (**Abrir transação 24/09/2026 R$ 943,10 Terminal 2**). Disabled state exposes `aria-disabled` with the reason linked through `aria-describedby`; loading exposes `aria-busy`. Icon-only buttons have an explicit name (**Tema escuro**, **Abrir menu da conta**).

## Touch and scale

Hit area at least `spacing.size.touch-target-min` (44) and comfortably `spacing.size.touch-target-comfortable` (48) on Android, separated by `spacing.size.touch-target-gap`. Labels wrap onto two lines at large text sizes instead of truncating; the container grows vertically.

## Web and native

Web: hover and pressed styles, `:focus-visible` ring. Native: pressed state replaces hover; platform ripple/highlight may add feedback but the token colors stay; the focus ring appears for hardware-keyboard and switch-access focus.

## Copy

Imperative infinitive, sentence case, one verb plus object where neighbors could be confused (copy §3): **Salvar**, **Enviar para aprovação**, **Finalizar plano**, **Desfazer envio**, **Reenviar convite**. No exclamation marks.
