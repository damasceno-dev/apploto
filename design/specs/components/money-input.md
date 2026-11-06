# MoneyInput

**Purpose.** Entry of a BRL amount where a blank and a zero mean different things: the Close day count per product, a transaction or payment value, installment rows, the close-form fields. The parsing and formatting rules are owned by [formatting §2](../formatting-ptbr.md) and implemented in `@loto/core`; this spec owns presentation and behavior around them.

## Anatomy

[Input](input.md) container with the value right-aligned in `typography.role.money` (tabular lining figures) · the `R$` unit as a fixed leading adornment in `sem.text.muted` (always visible, never part of the editable text) · optional row action outside the field (**Repetir abertura**, **Desfazer repetição**) · optional state hint below (**não informado**). Width fits at least `R$ 999.999.999,99` at the current text size.

## Variants

- **Amount** — a positive value (`Value` of a transaction, payment or plan).
- **Count** — a Close day product row: blank is **não informado**, `R$ 0,00` is a recorded zero; the screen distinguishes them in text and in the payload.
- **Read-only amount** — the opening value and any system-calculated figure: plain right-aligned text with its provenance label (**calculado pelo sistema** / **informado pelo operador**, copy §7), never a disabled input.

- **Signed amount** *(M9.6 P7, v61)* — the one operator-entered signed figure: **Relatório do terminal** on Close day / Fix & resubmit. The field holds the **magnitude** exactly like Amount, and a separate sign control beside it (a two-option [Radio](choice-controls.md) group **Positivo** / **Negativo**, default **Positivo**, inline at every width) sets the sign; the committed value is `±magnitude` in integer cents. A zero magnitude is always zero (the control shows **Positivo** and is ignored). Display of the committed value follows [formatting §3](../formatting-ptbr.md) (`+R$ …` / `−R$ …`, U+2212, zero unsigned). An arithmetic expression (Design 4.9.7) is evaluated with `allowNegative: true` only in this variant; a negative result sets the control to **Negativo** and the field to its magnitude, so the field never contains a minus sign.

Every other money field stays unsigned: computed signed figures (variance, deltas) are displayed from the server, never typed.

## States

Default; focus; invalid (`sem.border.control-invalid`; in Signed amount the invalid state covers the field and its sign control together); disabled; read-only; **blank** (placeholder **não informado** in `sem.text.placeholder`, value absent from the candidate); **dirty** (the screen's SaveStatus reports it); **pending recovery** (value restored from the device store — the screen shows its retained-input notice).

## Behavior

- Typing digits builds the value right-to-left in cents (`1234` → `R$ 12,34`); paste tolerates `R$`, dots and spaces; the parsed result is integer cents handed to `@loto/core`. No float arithmetic in the component.
- Clearing the field returns it to **blank**, not zero. Typing `0` produces a recorded zero. (Close-form fields have no blank state: the screen commits a cleared field as zero, see Close day.)
- **Signed amount:** the sign is never typed into the field (numeric keypads have no reliable minus key, and a stray hyphen must not flip a report); it is chosen in the sign control, which is keyboard- and switch-operable, and changing it marks the field dirty exactly like a value change. Paste of a value with a leading `-` or `−` sets **Negativo** and keeps the magnitude; in every other variant a signed paste is refused as today.
- The component reports the exact typed state (blank, partial, complete) so the durable-input store and the variance preview receive the true candidate.
- **Repetir abertura** and **Desfazer repetição** are row actions owned by Close day (catalog): the component exposes a `setValue` that keeps undo semantics with the screen, and never animates the value change beyond `motion.transition.control`.

## Arithmetic entry (Design 4.9.7 — mobile behavior, Mobile M5)

Operators used to type `100+50+20` into a value field, and the count still benefits from it. The grammar, exact evaluation and BRL rounding belong to `@loto/core`'s public `evaluateMoneyExpression`; this section fixes how the native app offers it, so that a calculation can never slip into a field, a draft or a request unnoticed. Web money fields adopt the same semantics when they implement it; this amendment does not claim they already do.

**Entry.** A numeric keypad has no operators, so arithmetic opens on demand: the text action **Calcular** below the field (spoken **Calcular {campo}**) opens an inline buffer — never a second native modal. The buffer is a plain text field on the decimal keypad (digits and the decimal comma), right-aligned in `typography.role.money`, with an operator row of standalone keys placed above it that meet the touch-target guidance: **+**, **−**, **×**, **÷**, **(**, **)**, **,** and **⌫**, spoken **mais**, **menos**, **vezes**, **dividido por**, **abre parênteses**, **fecha parênteses**, **vírgula** and **apagar**. A key appends to the end of the buffer; the caret stays at the end, matching the field. Physical-keyboard aliases (`-`, `*`, `/`) are the parser's.

**Grammar.** Numbers are pt-BR numerals as typed by people (`1.234,56`, `12,5`); precedence is signs, then **×**/**÷**, then **+**/**−**; parentheses nest. The buffer is not cents-typing: `100+50` means one hundred and fifty reais. Nothing is executed — the core tokenizer and recursive-descent evaluator are the only evaluator, with no `eval`/`Function` and no other parser in the app.

**Result before acceptance.** Above the buffer — the result line, the operator row and the actions all sit above it, so the on-screen keypad never covers them — a polite live line always says where the calculation stands: empty (**Digite a conta, por exemplo 100 + 50 + 20.**), incomplete (**Continue digitando a conta.**), invalid (the error in plain words — dividing by zero, a misplaced operator, unbalanced parentheses, an unsupported character, a malformed number, too long or too complex, beyond the maximum), or **Resultado: R$ …**, with **(arredondado para centavos)** when the exact value had more than two decimals and was rounded half away from zero. The primary **Usar R$ …** names the amount it will place and is enabled only for a complete, valid result; **Descartar conta** closes the buffer and leaves the field untouched. The preview is the person's own typed arithmetic — never an expected closing, a variance or any figure derived from server data.

**What reaches the field and the API.** Accepting replaces the field with the resulting amount in integer cents and closes the buffer; only that number travels in the request, never the expression text. Autosave and **Salvar** wait while any calculation is open, so an unaccepted buffer is never applied silently. A blank field stays **não informado** and is distinct from an accepted zero; for the close-form fields (no blank state) an empty field still commits zero, as before.

**Signed amount.** Only the Signed amount variant evaluates with `allowNegative`. A negative result sets the sign control to **Negativo** and the field to the magnitude, a positive one sets **Positivo**, and the preview shows the signed figure (**Resultado: −R$ 15,00**). Every other variant refuses a negative result with **O resultado não pode ser negativo neste campo.** and keeps **Usar** disabled.

**Retention and focus.** The raw buffer is retained with the screen's device-kept input (scoped like every draft, never a credential or version) and restored for review after a restart; restoring never accepts it. Opening the buffer moves focus into it; accepting or discarding leaves focus on the field's row. Read-only and disabled fields offer no **Calcular**.

## Accessibility

The accessible value is spoken as money (**doze reais e trinta e quatro centavos**, through the platform's number formatting) with the product name in the label (**Contagem de Raspadinha**). Blank announces **não informado**; zero announces **zero reais**. The unit adornment is hidden from assistive tech because the label and value already carry it. Signed amount: the sign control is labelled with the field (**Sinal do Relatório do terminal**) and the spoken value includes the sign (**menos sessenta reais**).

## Touch and scale

Numeric keypad on touch (`inputmode="numeric"` on web; number pad on native — the decimal comma is implied by the cents entry, so no decimal key is needed). Right alignment survives enlarged text; when the field cannot fit, the row stacks the label above the field instead of shrinking the digits.

## Web and native

Web keeps a real text input with `inputmode`; native uses a numeric keyboard and a custom formatter. Both keep the cursor at the end while typing.

## Copy

Blank: **não informado**. Provenance labels per copy §7. Validation reuses the backend message (**O valor da transação deve ser maior que zero**).
