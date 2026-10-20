# Checkbox, Radio and Switch

**Purpose.** Binary and single-choice inputs: **Somente minhas** (`Mine`), row selection for sibling cancellation, **Entregou mercadoria contada?** (`HandsOutCountedStock` — never preselected), the payment method when shown as options, invitation role, theme preference in settings-like contexts. A switch is reserved for an immediately applied device preference; anything submitted with a form is a checkbox or radio.

## Anatomy

Control box/circle/track (16 visual units inside a `spacing.size.touch-target-min` hit area) · label to its right (`typography.role.control`) · optional description below (`typography.role.helper`, `sem.text.muted`). Groups use a fieldset legend.

## Variants and tokens

| Variant | Off / unselected | On / selected |
|---|---|---|
| Checkbox | `sem.surface.default` fill, `sem.border.control` border, `radius.xs` | `sem.selection.indicator` fill, check mark `sem.selection.indicator-fg` |
| Indeterminate checkbox | — | `sem.selection.indicator` with a `sem.selection.indicator-fg` bar mark (group selection, e.g. "all remaining installments") |
| Radio | circle, `sem.border.control` | `sem.selection.indicator` ring and dot |
| Switch | track `sem.border.control`, thumb `sem.surface.default` | track `sem.selection.indicator`, thumb `sem.selection.indicator-fg`; state also written (**Ativado**) |

**Why a selection token.** Row checkboxes sit on selected rows (`sem.surface.selected` over `sem.surface.default`), where the dark primary fill would fall below 3:1. `sem.selection.indicator` passes 3:1 on every surface a choice control or selected tab can sit on, including selected and hovered rows, and the gate checks it there.

## States

Default, hover (`sem.border.control-hover`), focus (`sem.focus.ring` around the control), invalid (group-level message; `sem.border.control-invalid`), disabled (`sem.action.disabled.*`, reason adjacent), read-only (the chosen value as text).

## Behavior

- No default where the catalog says none: `HandsOutCountedStock` is a radio pair **Sim** / **Não** with neither selected until the operator answers (an unchecked checkbox would silently send `false`), and the payment method starts unselected.
- A row checkbox never triggers the row's navigation; it lives in its own cell with its own hit area.
- Switch changes apply immediately and are reversible; they are never used for financial or permission changes.

## Accessibility

Native inputs on web (`<input type="checkbox|radio">`, `role="switch"` for switches); native roles `checkbox`, `radio`, `switch` with checked state. Labels are clickable. Radio groups use arrow keys; checkboxes toggle with Space. Errors attach to the group legend.

## Touch and scale

The whole label row is the hit area; minimum height `spacing.size.touch-target-min`, separation `spacing.size.touch-target-gap`. The control scales with text size (minimum 16, growing with the label's line height).

## Copy

Labels state the condition positively (**Somente minhas**, **Entregou mercadoria contada ao cliente**). Yes/no radios use **Sim** / **Não** only when the question above them is explicit.
