# FilterBar

**Purpose.** The controls above a list or report that set its server query — date window, account, operator, client, type, status, `Mine`, as-of date — together with a plain-language summary of the scope currently shown. It makes the scope of every figure visible and survives drill-down and return.

## Anatomy

Panel ([Card](card.md) section variant on wide layouts; a collapsible header on compact) · fields in a grid (`spacing.form.column-min` columns, `spacing.form.column-gap`) · apply action (**Filtrar** / **Ver**) where the screen applies on submit · **Limpar filtros** text button when any value differs from the default · **active scope summary** below (`typography.role.helper`): chips or a sentence naming each active value (**24/09/2026 · Terminal 2 · Rascunho · Somente minhas**) with **padrão** when the default applies.

## Variants

- **Apply on submit** (default): changes are staged and applied together, so a window and an account change produce one request and one result announcement.
- **Immediate**: a single control whose change is the whole query (the Work Queue day stepper, Fiado balance tabs).
- **Compact collapsed**: below `breakpoint.medium` the panel collapses to the scope summary plus **Filtros ({n})**, opening the fields in a [sheet](modal-sheet.md).

## States

Default, dirty (staged changes not yet applied — the apply button is primary and the summary still shows the applied scope), applying (results region updating), invalid (a FormField message; the server's verbatim 400 for out-of-range windows), options loading/failed per [Select](select.md).

## Behavior

- Uses only the endpoint's real query fields (catalog entry); no invented search or sort.
- A changed filter or page size resets to page 1. Applied values, page and page size survive opening a row and returning, refresh, and navigation back; a fresh entry uses the catalog default; a validated origin restores its own.
- Role-shaped: fields a role cannot use are absent (a Member never sees the operator filter), not disabled.
- A carried-over value missing from the current options stays visible with **(fora das opções atuais)**; use **(inativo)** only when explicit server activity data confirms it. Absence alone proves no activity state; an unknown id yields the endpoint's empty page.

## Accessibility

A labelled form region (**Filtros de transações**). Applying announces the result count politely (**37 transações**) without moving focus. The scope summary is real text, read before the results.

## Touch and scale

Fields wrap and stack; the apply and clear actions keep touch sizes and stay together at the end of the panel or the sheet.

## Copy

**Filtrar**, **Limpar filtros**, **Filtros ({n})**, **padrão — hoje, todos os tipos**. Option labels per copy §5.
