# Modal / Sheet

**Purpose.** Temporary surfaces that take focus: confirmation of consequential actions, short forms that must not leave their context (new client from a sale, reject reason, cancel reason), the navigation drawer on compact web, and bottom sheets for pickers and filters on touch screens.

## Anatomy

Backdrop (`sem.overlay.backdrop-base` at `layer.backdrop.opacity.light` / `layer.backdrop.opacity.dark`) · container (`sem.surface.raised`, `shadow.floating`, `radius.panel`; sheets square at the screen edge) · header with title (`typography.role.section-title`) and close button · body (padding `spacing.inset.dialog`, scrolls internally) · divided footer (1px `sem.border.default`) with actions, primary last on web, full-width stacked on compact.

## Variants

| Variant | Size and placement | Layer |
|---|---|---|
| Dialog | centered, up to `spacing.size.dialog-max` wide, viewport minus 32 | `layer.dialog-backdrop` / `layer.dialog` |
| Confirmation | dialog whose body states the consequence; confirm label names the object; destructive confirm uses `sem.action.danger-solid.*` | same |
| Drawer | start-edge panel up to `spacing.size.drawer-max` (≈ 86% of narrow viewports) for navigation | `layer.drawer-backdrop` / `layer.drawer` |
| Bottom sheet | full-width from the bottom edge, up to 90% of the height, drag handle plus a visible close button | `layer.dialog` |

## States

Opening/closing (`motion.transition.dialog-enter`/`-exit`, `motion.transition.drawer-enter`/`-exit`; instant under `motion.reduced.*`), busy (confirm button loading, other actions disabled, the dialog not dismissible while a write is in flight unless the screen defines cancellation), error (verbatim messages inside the body above the actions; input kept), uncertain (the dialog closes only to the screen's uncertain state, never to success).

## Behavior

- Focus moves to the first meaningful element (the title for confirmations, the first field for forms), is contained while open, and returns to the trigger on close — or to the logical heading if the trigger was removed.
- Escape, the close button, native back and a backdrop tap dismiss through the same path; a form with entered values asks before discarding only when the values are not kept on the device.
- The background is inert (web `inert`/top-layer `<dialog>`, native modal); background scroll is locked.
- On phones the keyboard never covers the focused field or the primary action: the body scrolls and the footer stays above the keyboard.
- Dialogs never stack more than one level; a picker opened inside a dialog uses `layer.popover`.

## Accessibility

Web `<dialog>` (modal) with `aria-labelledby` (title) and `aria-describedby` (consequence text); native modal with an accessible title and `accessibilityViewIsModal`. The drawer is a dialog named **Menu de navegação**. Close buttons are named **Fechar**.

## Touch and scale

Actions meet touch minimums and stack full-width below `breakpoint.medium`. At enlarged text the body scrolls; header and footer stay visible.

## Copy

Title states the decision (**Cancelar esta transação?**); body states the consequence (copy §3); actions: confirm names the action, dismiss is **Voltar** — never a bare **OK**/**Sim**.
