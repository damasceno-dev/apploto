# MoneyInput

**Purpose.** Entry of a BRL amount where a blank and a zero mean different things: the Close day count per product, a transaction or payment value, installment rows. The parsing and formatting rules are owned by [formatting §2](../formatting-ptbr.md) and implemented in `@loto/core`; this spec owns presentation and behavior around them.

## Anatomy

[Input](input.md) container with the value right-aligned in `typography.role.money` (tabular lining figures) · the `R$` unit as a fixed leading adornment in `sem.text.muted` (always visible, never part of the editable text) · optional row action outside the field (**Repetir abertura**, **Desfazer repetição**) · optional state hint below (**não informado**). Width fits at least `R$ 999.999.999,99` at the current text size.

## Variants

- **Amount** — a positive value (`Value` of a transaction, payment or plan).
- **Count** — a Close day product row: blank is **não informado**, `R$ 0,00` is a recorded zero; the screen distinguishes them in text and in the payload.
- **Read-only amount** — the opening value and any system-calculated figure: plain right-aligned text with its provenance label (**calculado pelo sistema** / **informado pelo operador**, copy §7), never a disabled input.

There is no signed MoneyInput: signed figures (variance, deltas) are always computed by the server and displayed, never typed.

## States

Default; focus; invalid (`sem.border.control-invalid`); disabled; read-only; **blank** (placeholder **não informado** in `sem.text.placeholder`, value absent from the candidate); **dirty** (the screen's SaveStatus reports it); **pending recovery** (value restored from the device store — the screen shows its retained-input notice).

## Behavior

- Typing digits builds the value right-to-left in cents (`1234` → `R$ 12,34`); paste tolerates `R$`, dots and spaces; the parsed result is integer cents handed to `@loto/core`. No float arithmetic in the component.
- Clearing the field returns it to **blank**, not zero. Typing `0` produces a recorded zero.
- The component reports the exact typed state (blank, partial, complete) so the durable-input store and the variance preview receive the true candidate.
- **Repetir abertura** and **Desfazer repetição** are row actions owned by Close day (catalog): the component exposes a `setValue` that keeps undo semantics with the screen, and never animates the value change beyond `motion.transition.control`.

## Accessibility

The accessible value is spoken as money (**doze reais e trinta e quatro centavos**, through the platform's number formatting) with the product name in the label (**Contagem de Raspadinha**). Blank announces **não informado**; zero announces **zero reais**. The unit adornment is hidden from assistive tech because the label and value already carry it.

## Touch and scale

Numeric keypad on touch (`inputmode="numeric"` on web; number pad on native — the decimal comma is implied by the cents entry, so no decimal key is needed). Right alignment survives enlarged text; when the field cannot fit, the row stacks the label above the field instead of shrinking the digits.

## Web and native

Web keeps a real text input with `inputmode`; native uses a numeric keyboard and a custom formatter. Both keep the cursor at the end while typing.

## Copy

Blank: **não informado**. Provenance labels per copy §7. Validation reuses the backend message (**O valor da transação deve ser maior que zero**).
