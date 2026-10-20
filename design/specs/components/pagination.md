# Pagination

**Purpose.** Moves through a paged endpoint using its own envelope (`Page`, `PageSize`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`) and tells the user exactly which slice of the whole they are seeing.

## Anatomy

Range text (**21–40 de 137**) · **Anterior** / **Próxima** buttons · current page indicator (**Página 2 de 7**) · optional page-size select where the endpoint allows (bounded by its maximum) · optional jump for long sets on wide layouts (page number buttons with ellipsis).

## States

First page (Anterior disabled with no reason needed — the range text says it), last page, single page (component hidden but the total count still shown in the region heading), loading (buttons disabled, range text kept from the previous page with **Atualizando…**), error (the table's Notice; pagination keeps its last valid state).

## Behavior

- Driven only by the returned envelope; `HasNext`/`HasPrevious` decide button availability.
- Page and size are part of the preserved filter context (see [FilterBar](filter-bar.md)).
- After a write, if the page exceeds the new `TotalPages`, move to the last valid page (or page 1 when empty) and re-fetch.
- Whole-set totals come from the response aggregate; anything computed from visible rows is labelled **Subtotal desta página**.

## Accessibility

A `navigation` landmark named **Paginação de {lista}**; buttons named **Página anterior**/**Próxima página**; the current page has `aria-current="page"`. Changing page announces the new range politely and moves focus to the table heading, not to the top of the page.

## Touch and scale

Buttons meet touch minimums; on compact layouts only the range text and Anterior/Próxima remain.
