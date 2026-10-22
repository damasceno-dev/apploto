# FormField

**Purpose.** The wrapper that gives every input a persistent label, optional helper text, an inline error and a programmatic link between them. Every [Input](input.md), [MoneyInput](money-input.md), [Select](select.md), [DatePicker](date-picker.md) and choice group sits in a FormField; a form never relies on placeholder text as its label.

## Anatomy

Label (`typography.role.label`, `sem.text.primary`) · optional "opcional" marker or required cue · control · helper text (`typography.role.helper`, `sem.text.muted`) · inline error (`typography.role.helper`, `sem.feedback.danger.fg`, preceded by an error icon) · optional read-only reason. Vertical rhythm `spacing.stack.xs` between parts, `spacing.form.row-gap` between fields.

## Variants

- **Stacked** (default): label above the control. Used everywhere on compact layouts.
- **Grid column**: fields flow into columns at least `spacing.form.column-min` wide with `spacing.form.column-gap`; they stack when the container cannot hold two columns, including at enlarged text.
- **Fieldset**: a group of related choices (payment method, status filter) with a legend instead of a label.

## States

| State | Presentation |
|---|---|
| Default | label and helper as above |
| Focus | the control shows `sem.focus.ring`; the label does not change color |
| Invalid | control border `sem.border.control-invalid`; inline message below; the message, not color, carries the meaning |
| Disabled | label `sem.text.disabled` only if the whole field is unavailable; the reason replaces the helper |
| Read-only | the value renders as text on `sem.surface.muted` with no control border; the reason line (`sem.text.muted`) says why (**Período bloqueado**, **Gerada automaticamente**) |
| Unsaved | an optional marker beside the label (**Alterado**) when the screen tracks dirty fields (corrections, Settings) |

## Behavior

- Client pre-validation reuses backend wording where a backend rule exists (copy §6) and runs on blur or submit, never on every keystroke.
- A server message is placed inline only when its association to this field is unambiguous; otherwise it stays in the form's [error summary](notice.md). Non-secret fields keep their values either way. Passwords follow [Input's retention boundary](input.md#behavior) and the owning authentication screen's clearing rule; capabilities follow the catalog's capability-link rule.
- Required/optional: mark the minority. Most Lotero forms are short and mostly required, so optional fields carry **(opcional)**.
- Masks and formatting belong to the specific input and `@loto/core`, not to the field.

## Accessibility

The label is the control's accessible name (`<label for>` on web; `accessibilityLabel` on native). Helper and inline error are linked with `aria-describedby` (native: `accessibilityHint` plus an error announcement), and invalid fields set `aria-invalid`. On submit failure focus moves to the error summary or the first invalid field once, and the summary is announced assertively; later edits announce nothing until the next submit.

## Touch and scale

The label is tappable and focuses its control. At enlarged text the label wraps; helper and error text wrap fully — never truncated.

## Copy

Labels are nouns in sentence case (**Valor**, **Data da transação**, **Motivo**). Helper text explains consequences, not mechanics (**O operador verá este motivo**). Errors follow copy §6.
