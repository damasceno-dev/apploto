# EmptyState

**Purpose.** Explains a successful, scoped response with nothing to show — and only that. Lotero distinguishes four kinds, because each asks for a different next step, and never turns a failure, a loading count or a forbidden read into an empty state.

## Anatomy

Short statement (`typography.role.body-strong`) · optional one-line explanation (`typography.role.body`, `sem.text.secondary`) · at most one primary next action and one secondary. Placed inside the region that is empty (table body, card, panel), not as a full-page illustration. No decorative artwork.

## Variants

| Kind | Example copy | Next action |
|---|---|---|
| No records | **Nenhuma transação hoje.** | **Nova transação** when the caller may create |
| No matches | **Nenhuma transação neste filtro.** with the active scope summary | **Limpar filtros** |
| All-clear | **Tudo certo — nada pendente de aprovação.** | none, or a link to the next useful view (**Ver dia mais antigo** when the backlog banner applies) |
| Setup needed / scope empty | **Você ainda não tem um terminal atribuído.** with the catalog's explanation | the catalog's action (**Solicitar vínculo**, **Ir para Operadores**) |

## Behavior

The screen chooses the kind from the response and its own scope rules (catalog: Transactions list empty-short-circuits for out-of-scope Members). An all-clear uses neutral text, not success color, so "nothing pending" never reads as a financial result.

## Accessibility

Plain text in the region, announced with the region's completed load (**Nenhuma transação neste filtro**). Actions are real buttons/links.

## Copy

One statement plus the next action (copy §7). Sentence case, no exclamation marks.
