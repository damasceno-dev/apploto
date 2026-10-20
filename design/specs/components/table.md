# Table / List

**Purpose.** Dense, scannable records: the ledger, Work Queue groups, reports, close counts, admin lists. On wide layouts a real table; on compact layouts either the same table scrolling inside its own region (when column comparison matters — close counts, ledgers) or a grouped row list (when each row is read alone — queues, admin lists). The screen blueprint chooses per table; both keep the same fields, order and actions.

## Anatomy

Region heading or caption (what, which scope, which period) · optional toolbar ([FilterBar](filter-bar.md), totals) · header row (`typography.role.table-header`, `sem.text.muted` on `sem.surface.subtle`) · body rows (`typography.role.table`, `sem.text.primary` on `sem.surface.default`, separated by 1px `sem.border.subtle`) · optional footer (page subtotal or whole-set total) · [Pagination](pagination.md). Cells pad `spacing.inset.table-cell-y` × `spacing.inset.table-cell-x`, with `spacing.inset.table-edge-x` on the outer columns. Money and counts right-aligned with tabular figures — header alignment matches body alignment.

## Variants

| Variant | Use |
|---|---|
| Record table | each row opens one record (transactions, closes, clients) |
| Grouped list | compact layouts for queue/admin rows: primary line (name/account), secondary line (status, value), trailing action |
| Editable grid | Close day product rows: label, opening (read-only), MoneyInput, row actions |
| Expandable groups | Open-cheque aging plans with their installments; a [Disclosure](disclosure.md) per group |
| Selectable rows | sibling installment cancellation: checkbox column, selection summary above |

## Row interaction (the record destination)

- A row whose record can be opened is a whole-row pointer target with `sem.surface.hover` on hover **and** contains an explicit link (**Abrir transação**, **Conferir**, **Abrir fechamento**) whose accessible name identifies the row (date, value, account). Keyboard users tab to that link; the row itself is not a tab stop.
- Nested controls (checkbox, **Ver parcelas**, a secondary link, a button) stop propagation: they never also open the row.
- Text selection inside a row does not trigger navigation; a drag selects text, a click without selection opens the record.
- The date cell is data, never a separate date-only link to an edit screen.
- Opening grants no permission: read-only rows open in their read-only state.

## States

Loading (header kept, Skeleton rows matching expected density), empty (an [EmptyState](empty-state.md) inside the table region — scoped: **Nenhuma transação neste filtro**), error (a [Notice](notice.md) in place of the body with retry; the filters stay), updating (previous rows dimmed with a polite **Atualizando…**, never relabeled with the new filter), row-level status (Draft rows `dom.transaction-status.Draft` badge and secondary text; Cancelled rows muted with their badge), selected (`sem.surface.selected`), focus within a row (the row shows `sem.surface.hover` while its link has focus).

## Behavior

- Server order is kept; client sorting is not offered where the endpoint has no sort.
- Columns are never dropped to fit: the table scrolls horizontally inside a labelled scroll region with sticky header row and, where it helps, a sticky first column. Surrounding text and actions reflow at page width.
- After a write the table re-reads; if the current page passed `TotalPages`, it moves to the last valid page.
- Removing the focused row moves focus to the next row's link or the table heading.

## Accessibility

Web: semantic `<table>` with `<caption>` and `<th scope>`; the scroll region is focusable with a name (**Tabela de transações, role horizontalmente**) so keyboard users can scroll. Grouped lists use list semantics with each item's link first. Native: list items expose one accessible element for the record (link role) and separate elements for nested actions; status is read in text.

## Touch and scale

Rows are at least `spacing.size.touch-target-min` tall. At enlarged text, cells wrap; numeric columns keep their width minimum so digits never break. Horizontal scroll has visible affordance (shadow edge with `shadow.raised`) and never hides the only action column — the action column is sticky at the end when the table scrolls.

## Copy

Headers in sentence case rendered uppercase by the role (**Conta**, **Contado por**, **Diferença**). Empty and subtotal copy per copy §7 and the catalog.
