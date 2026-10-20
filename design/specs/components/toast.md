# Toast

**Purpose.** A brief confirmation of an endpoint-confirmed outcome that moved the user elsewhere: **Transação salva** after returning to the list, **Fechamento enviado** on arrival back at the Cockpit, **Convite enviado**. A toast is an echo, never the only record of a result and never the home of a recovery action.

## Anatomy

Small surface (`sem.surface.raised`, `shadow.floating`, `radius.panel`, start rule in `sem.feedback.success.border`) with an icon and one line (`typography.role.body`), optional **Fechar** button. Placed bottom-center on compact layouts and bottom-end on wide layouts, above content at `layer.toast`, clear of the on-screen keyboard and the navigation.

## Variants

Success only. Failures, uncertain results, undo and anything requiring a decision use a [Notice](notice.md) or the owning row action (**Desfazer repetição** stays a persistent row action).

## Behavior

- Shown only after the server's confirmation; the destination screen also reflects the result (re-read data).
- Visible at least 5 seconds, longer for longer text, paused while focused or hovered; dismissible; at most one at a time (a new one replaces the old).
- Enter/exit with `motion.transition.toast`; appears/disappears instantly under `motion.reduced.toast`.

## Accessibility

Announced through a polite status region; does not take focus. Because it disappears, it must not be the only place the information exists.

## Touch and scale

Width up to the content width minus gutters; text wraps; the close button meets touch minimums.
