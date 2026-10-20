# PageHeader

**Purpose.** Opens every screen with where the user came from, what this is, and the context that scopes it — branch-local date, account, status, mode — plus the screen's main actions. It is also the visible half of the return contract: the breadcrumb or **Voltar para {origem}** names the semantic origin the catalog defines.

## Anatomy

Origin line (breadcrumb on wide layouts: **Fila de trabalho › Conferência**; a single **Voltar para {origem}** link on compact) · title (`typography.role.page-title`; `typography.role.page-title-compact` below `breakpoint.medium`) · context line (`typography.role.body`, `sem.text.secondary`: **Terminal 2 · quinta-feira, 24 de setembro de 2026 · Rascunho · Meu turno**) · optional status badge · actions (at most one primary; others secondary) · bottom rule (`sem.border.default`) separating it from content by `spacing.page.section-gap`.

## Variants

- **Record header** — title names the record type and the context line its identity (account, business date, status); used by close, transaction and plan screens.
- **List/report header** — title plus scope (period, filters summary lives in the FilterBar).
- **Auth header** — product mark and title only (Login, Invitation).

## Behavior

- The origin comes from the validated navigation context, never from a URL parameter claiming permission; a direct link without an origin shows the catalog fallback (**Voltar para Transações**) or none.
- Native back and browser back go to the same destination as the visible return link.
- Long titles and context wrap; actions move below the title on compact layouts instead of overlapping it — the fix for the prototype's competing header at 150%.
- Route entry moves focus to the title (programmatic focus on the heading) and announces it.

## Accessibility

The title is the page's `<h1>`; the breadcrumb is a `navigation` landmark named **Você está em** with the current page marked. The return link names its destination.

## Touch and scale

Actions keep touch sizes and wrap; the context line wraps word by word, never truncating the branch or date.

## Copy

Titles are nouns (**Fechamento do dia**, **Transações**); the context line uses formatting §4 long dates; return links **Voltar para {origem}**.
