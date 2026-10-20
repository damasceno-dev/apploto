# SummaryStrip

**Purpose.** A row of labelled figures that belong to the same scope: the Close day preview totals (Total do fechamento, Total da abertura, Entradas, Saídas), report aggregates (Total, Média, Maior, Menor), balance summaries (Total em aberto, Crédito dos clientes), hour totals. It summarizes; it never computes money the server did not return, except the explanatory totals the catalog explicitly allows on Close day and approval.

## Anatomy

Shared container (`sem.surface.default`, 1px `sem.border.default`, `radius.panel`) divided by thin vertical rules (`sem.border.subtle`) into items. Each item: label (`typography.role.table-header`, `sem.text.muted`) above the figure (`typography.role.summary-value`, tabular, right- or start-aligned consistently within the strip) and an optional caption (`typography.role.helper`) for scope or provenance (**ainda não enviada**, **subtotal desta página**).

## Variants

- **Totals** — whole-set aggregates returned by the endpoint.
- **Explanatory** — figures the screen derives for explanation only (Close day/approval totals), labelled as a preview or explanation and visually secondary to the official value.
- **Signed** — variance and balances use domain colors (`dom.cash-variance.*`, `dom.hour-balance.*`, `dom.fiado-balance.*`) with the sign and words visible.

## States

Loading: each figure is a Skeleton bar of figure height. Unavailable: the figure reads **—** with a caption (**Não foi possível carregar**) and its own retry; never `R$ 0,00`. Partial: when one source failed, only its figure is unavailable and the caption names it.

## Behavior

- Every figure carries its scope in the label or caption (period, account, page vs whole set). A page-local figure is labelled **Subtotal desta página**.
- Figures never animate (no counting-up) and update only when the authoritative read returns.

## Accessibility

Each item is a label/value pair (web `<dl>` or labelled group; native grouped element) read as **Total do fechamento: R$ 0,00**. Signed values speak the sign and the word (**menos doze reais, falta**).

## Touch and scale

Items wrap to two columns below `breakpoint.wide` and to one column below `breakpoint.medium`, with rules switching from vertical to horizontal. Figures never shrink to fit; they wrap the strip instead.
