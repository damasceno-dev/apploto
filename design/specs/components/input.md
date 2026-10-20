# Input

**Purpose.** Single- and multi-line text entry: names, e-mails, descriptions, Notes, rejection and cancellation reasons, passwords, client search. Numeric money uses [MoneyInput](money-input.md); dates and times use [DatePicker](date-picker.md).

## Anatomy

Field container (`sem.surface.default` fill, 1px `sem.border.control` border, `radius.control`) · value (`typography.role.control`, `sem.text.primary`) · optional placeholder (`sem.text.placeholder` — an example, never the label) · optional leading icon (search) · optional trailing action (clear, **Mostrar senha**) · optional character counter for limited fields. Height at least `spacing.size.control-height` (web) or `spacing.size.control-height-touch` (touch/native); padding `spacing.inset.control-x`.

## Variants

| Variant | Notes |
|---|---|
| Text | default; `autocomplete` and native `textContentType` set where meaningful (name, e-mail) |
| E-mail | e-mail keyboard, no auto-capitalization, trims surrounding spaces before submit only |
| Password | masked, **Mostrar senha** toggle, paste and password managers allowed; `new-password` vs `current-password` hints set correctly |
| Textarea | Notes (max 1000) and reasons; grows with content up to a cap, then scrolls; counter shows **{n}/1000** near the limit |
| Search | local filtering of a complete collection only (Clients); leading search icon, clear button, results count announced politely |
| Masked identifier | CPF, CNPJ, phone, CEP per formatting §6: punctuation is presentational; submit strips it where the contract expects digits; a non-matching length renders verbatim |

## States

Default, hover (`sem.border.control-hover`), focus (`sem.focus.ring`), invalid (`sem.border.control-invalid` plus FormField message), disabled (`sem.action.disabled.bg`, `sem.action.disabled.fg`, reason adjacent), read-only (text on `sem.surface.muted`, selectable, no border — see [FormField](form-field.md)).

## Behavior

- Values are kept exactly as typed, including leading/trailing spaces in Notes; trimming happens only where the contract trims (names, e-mail).
- Input is retained across validation errors, coordination-busy retries, session recovery and navigation, per the durable-input pattern; the component never clears itself on error.
- The clear button empties only that field and returns focus to it.

## Accessibility

Label from [FormField](form-field.md). Password toggle is a button named **Mostrar senha**/**Ocultar senha** with pressed state. Counter text is linked as description and announced only when the limit is near. Search announces **{n} clientes encontrados** after typing pauses.

## Touch and scale

Field height grows with text; the trailing action keeps a `spacing.size.touch-target-min` hit area. Textareas never trap scroll on touch: the page scrolls when the textarea is at its edge.

## Web and native

Web uses native `<input>`/`<textarea>` semantics and `inputmode`. Native uses the platform keyboard types (e-mail, default) and keeps the focused field above the keyboard (scroll into view inside forms and sheets).

## Copy

Placeholders are examples (**nome@exemplo.com.br**) or omitted. Reason fields are labelled **Motivo** with helper copy saying who reads it (copy §3).
