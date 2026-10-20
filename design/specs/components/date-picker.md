# DatePicker

**Purpose.** Entry of business dates (`Date`, `DueDate`, report windows, the Work Queue day, month selection) and wall-clock times (`TransactionTime`, time-entry `ClockIn`/`ClockOut`). The three time shapes and their display rules belong to [formatting §4](../formatting-ptbr.md); the branch clock from `GET /branch/current` supplies "today" — never the device clock.

## Anatomy

Text field accepting `dd/MM/yyyy` (or `HH:mm` for time) with a calendar/clock trigger · popover calendar (`sem.surface.raised`, `shadow.floating`, `layer.popover`, `radius.control`) with month heading, previous/next month buttons, weekday header, day grid · today marker (from the branch clock) · selected day (`sem.action.primary.bg` fill, `sem.action.primary.fg` text) · optional range highlight (`sem.surface.selected`). Time variant: field with a native time picker; no clock face is required.

## Variants

| Variant | Use |
|---|---|
| Date | business dates; ISO on the wire, `dd/MM/yyyy` on screen |
| Date range | `DateFrom`/`DateTo` pairs in filters; two fields with one shared popover on wide layouts, two fields on compact; the catalog's window limits (≤ 366 days) surface as the server's verbatim 400 |
| Day stepper | Work Queue: **Dia anterior** · date field · **Próximo dia** |
| Month | Monthly reconciliation and month filters: month/year selector, `MM/yyyy` |
| Time | `HH:mm`, 24-hour, wall-clock as returned — no timezone conversion |

## States

Default, focus, open, invalid (unparseable date — field keeps the typed text and the FormField explains **Data inválida. Use dd/mm/aaaa.**), disabled, read-only, **out of permitted range** (days after the branch's today disabled for Open day; locked-period days are not hidden — the server's lock message decides).

## Behavior

- Typing is always possible; the calendar is a convenience. Partial input is preserved, never auto-corrected into another date.
- Bounds shown in the calendar are hints from known rules (future dates disabled where every role is refused); the server remains authoritative and its message renders verbatim.
- The value object is calendar-only (`YYYY-MM-DD`); it is never turned into a JavaScript `Date` instant for business logic.
- Holidays are not invented here: a due-date preview comes from the server.

## Accessibility

Web: the field is a text input with the format in its description; the calendar is a dialog with a grid (arrow keys move days, Page Up/Down months, Home/End week edges, Enter selects, Escape closes and returns focus). Day buttons have full names (**quinta-feira, 24 de setembro de 2026**); today is announced as **hoje**. Native: the platform date/time picker in a sheet or inline, with the same labels.

## Touch and scale

Day cells meet `spacing.size.touch-target-min`; on compact layouts the calendar opens in a [bottom sheet](modal-sheet.md). Large text enlarges cells and the sheet scrolls rather than clipping weeks.

## Web and native

Web may use the native `<input type="date">` only where it supports `dd/MM/yyyy` display reliably; otherwise the custom popover above. Native uses the platform pickers set to pt-BR.

## Copy

Weekday and month names lowercase (formatting §4). Range labels **De** / **Até**; the day stepper **Dia anterior** / **Próximo dia**.
