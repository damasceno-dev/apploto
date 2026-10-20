# Select

**Purpose.** Choose one value from a known list: transaction type, account, client, operator, status filter, role, settlement rule, page size. A searchable variant handles long lists (clients, types) without inventing a server search.

## Anatomy

Trigger (Input container with the chosen label and a chevron) · listbox popover (`sem.surface.raised`, `shadow.floating`, `radius.control`, `layer.popover`) · options (`typography.role.control`; selected option marked by check icon and `sem.surface.selected`) · optional group headings (`typography.role.overline`, `sem.text.muted`) · optional empty/loading row.

## Variants

| Variant | Use |
|---|---|
| Native select | short static lists (status filter, role, page size); web `<select>`, native picker |
| Listbox | lists needing rich options (account with type and number, type with category) |
| Searchable | long lists loaded in full (clients, types); filters locally over the complete collection only — never over one page of a paged endpoint |
| No default | a choice the user must make deliberately (Receber pagamento's method): the trigger reads **Escolha…** and the form refuses submission with the copy-guidelines message |

## States

Default, hover, focus, open, invalid, disabled (with reason), read-only (plain text), **loading options** (trigger shows **Carregando…** and is not actionable), **options failed** (trigger shows **Não foi possível carregar as opções** with **Tentar novamente**; the form does not treat the field as empty), **empty options** (the list says why — e.g. **Nenhuma conta disponível para você** — and links to setup when the catalog names one).

## Behavior

- Options come from the lookup the catalog names, already scoped by role (Member accounts from self-context; Manager accounts from `GET /account`). The component never widens scope.
- Excluded values are absent, not disabled, when the catalog says they are never offered (fiado repayment types on Create; system types). Retired values present on a historical record still display as the record's text.
- A carried-over filter value missing from the current options stays selected and visible with **(fora das opções atuais)**. Use **(inativo)** only when an explicit server activity field confirms inactivity; absence alone proves no activity state.
- Changing a value that drives other fields (type → required fields) keeps the other entered values where they remain valid and announces newly required fields.

## Accessibility

Web listbox follows the combobox/listbox pattern (arrow keys, Home/End, type-ahead, Escape closes and returns focus to the trigger); native uses the platform picker or an accessible sheet list. The selected value is part of the trigger's accessible name. Searchable variant announces result counts politely.

## Touch and scale

Options have at least `spacing.size.touch-target-min` height. On compact layouts and native, long lists open in a [bottom sheet](modal-sheet.md) with the search field pinned at the top and the keyboard not covering results.

## Web and native

Web listbox renders in the top layer when opened inside a dialog. Native prefers the platform picker for short lists and a sheet for searchable lists.

## Copy

Placeholder **Escolha…** only for no-default selects; filters show **Todos/Todas** as a real option (copy agreement with the noun: **Todas as contas**, **Todos os tipos**).
