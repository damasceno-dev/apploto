# DetailList

**Purpose.** Label/value pairs for facts that are read, not edited: close identities (**Aberto por**, **Contado por**, **Enviado por**), audit stamps, a loaded transaction's fixed fields, profile facts, an invitation preview. It is the read-only counterpart of a form, and the way locked, cancelled or generated records present their data.

## Anatomy

Rows of label (`typography.role.label` weight regular or `sem.text.secondary`) and value (`typography.role.body`, `sem.text.primary`), aligned in two columns on wide layouts and stacked on compact ones; optional row actions (a link) at the end of a value. Rows separated by `spacing.stack.sm`; groups separated by a rule (`sem.border.subtle`).

## Variants

- **Identity rows** — a person plus optional operator and time (**Lucas Ferreira · operador Lucas Ferreira · 24/09/2026 08:02**); a null operator renders **Sem operador associado ao registro** (copy §7.2), a missing person **ainda não**.
- **Provenance rows** — value plus provenance label (**calculado pelo sistema** / **informado pelo operador**).
- **Restriction row** — the reason an item is read-only (**Período bloqueado até 31/08/2026**), styled `sem.text.muted` with an info icon.

## States

Loading (Skeleton per value), unavailable value (**—** with formatting §8 rules; **Data da rejeição indisponível** where the catalog defines a fallback), and success. No edit states.

## Behavior

Values are selectable and copyable. Timestamps follow formatting §4 (audit instants converted to the device zone; business dates as returned). Absent values use the em dash, never `R$ 0,00` or an empty cell.

## Accessibility

Web `<dl>` with `<dt>`/`<dd>`; native grouped label/value elements read in order. Row actions have names that include the row label.

## Touch and scale

Stacks below `breakpoint.medium` and at large text sizes; long names wrap.
