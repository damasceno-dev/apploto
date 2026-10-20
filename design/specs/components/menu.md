# Menu

**Purpose.** A popover list of actions or destinations opened from a trigger: the Session menu (identity, mode switch, account actions, Sair) and, where a row has more secondary actions than fit, a row overflow menu. A menu never hides the only way to perform a primary action.

## Anatomy

Trigger (button with a visible label or an avatar plus name; chevron) · popover (`sem.surface.raised`, `shadow.floating`, `radius.panel`, max width `spacing.size.menu-max`, `layer.popover`) · optional header block (identity: name, e-mail, branch and role) · groups separated by `sem.border.subtle` with optional group heading (`typography.role.overline`) · items (`typography.role.control`, optional description in `typography.role.helper`) · selected/active item marked with a check and `sem.surface.selected` · unavailable item with its reason and a link.

## Variants

- **Action menu** — items run commands (**Sair**).
- **Navigation menu** — items are links (**Minha conta**, **Plataforma**).
- **Mode group** — a radio-like group (Gestão / Meu turno) where choosing an item changes mode and closes the menu.

## States

Closed, open, item hover/focus (`sem.surface.hover`), item disabled/unavailable (`sem.text.disabled` label, reason in `sem.text.muted` beneath, a link to fix it when one exists), item busy (**Verificando…**, **Saindo…**).

## Behavior

Opens on click/tap or Enter/Space/Arrow Down; closes on Escape, outside click, selection or native back; focus returns to the trigger. On compact layouts and native it may open as a [bottom sheet](modal-sheet.md) with the same items and order.

## Accessibility

Web menu button pattern (`aria-haspopup`, `aria-expanded`, roving focus with arrow keys, type-ahead). Navigation items remain links; the mode group uses `menuitemradio` with `aria-checked`. Unavailable items stay focusable so their reason can be read. Native: an accessible sheet list.

## Touch and scale

Items at least `spacing.size.touch-target-min` tall; descriptions wrap; the popover scrolls within the viewport and never clips actions off-screen.
