# Operator day — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M5**. Semantics: catalog entries [Operator Day Cockpit](../../docs/screens.md#operator-day-cockpit), [Open day](../../docs/screens.md#open-day), [Close day](../../docs/screens.md#close-day), [Fix & resubmit](../../docs/screens.md#fix--resubmit) and the [close-day autosave pattern](../../docs/screens.md#close-day-autosave-dirty-save-and-repeat-undo). Contracts: M7.7 Phases 2–7.6 (shipped). **Remainder:** gap AA — editing, previewing or saving a close that holds a retained retired-product value — is not blueprinted beyond its read-only row; see [AA remainder](#aa-remainder-retired-product-rows).

The operator's day is one question asked again and again: *what do I do next?* The Cockpit answers it with one recommended step, while keeping today's count one tap away even when an older correction takes priority. The close screens then put the opening, the count and the day's movement on one page, with an honest line saying whether what is on screen is what the server holds.

## Operator Day Cockpit

### Regions, in reading order

1. **PageHeader** — title **Hoje**; context line: branch-local long date · workstation terminal; **Trocar** (secondary) when the operator holds more than one Terminal.
2. **Terminal choice** *(first entry of the branch-local day, more than one Terminal)* — a [Card](../components/card.md) titled **Em qual terminal você vai operar hoje?** with one option row per Terminal (name, number); nothing below renders a next action until a choice is made.
3. **Next action** — [Card](../components/card.md) next-action variant: eyebrow **Próximo passo** (no internal P-codes), title (`typography.role.next-action`), one-sentence body, the CTA [Button](../components/button.md) and at most one secondary action. Content per P1–P7 as cataloged; P3's split (**4 suas · 1 de outro operador**) sits in the body; P1 shows the rejection reason or the recount sentence with **Ver o que mudou**.
4. **Fechamento de hoje** — one line whenever today's close exists on the workstation: **{Terminal} · [status badge] · Ver fechamento de hoje**, plus **O próximo passo acima tem prioridade, mas não bloqueia: a contagem de hoje abre por aqui.** when a different step leads.
5. **Outros terminais** *(when My open closes today holds another terminal)* — compact list: terminal, status badge, **Ir para {terminal}**.
6. **Ponto** *(Members only)* — clock state (**Em andamento desde 08:02** / **Fora do ponto**) and **Abrir ponto**.
7. **Transações de hoje no {terminal}** — the first rows of the `(workstation, today)` ledger as a grouped list (time, type with **Automática** marker, value, status), the exact Draft count with its **seus / de outros operadores** split, and **Ver todas** (Transactions list filtered to that account and today) plus **Nova transação**.
8. **Meus números** — links to **Minhas transações** and **Meu banco de horas**.

### Layout

| Width | Arrangement |
|---|---|
| ≥ `breakpoint.wide` | two columns: main (regions 3, 4, 7) at roughly two thirds; side (5, 6, 8) at one third, each a stacked section. The next-action card never shares its row with another card. |
| < `breakpoint.wide` | **one column in the order 3 → 4 → 5 → 6 → 7 → 8.** The next-action card spans the full content width; its eyebrow, title, body and buttons stack, and buttons go full width below `breakpoint.medium`. |

This replaces the prototype's 375px defect, where a fixed `84px 220px` grid squeezed the next action into a one-word-per-line tower: no Cockpit region is ever placed in a column narrower than the content width on compact layouts, and enlarged text only adds height.

### Focus and actions

Route entry focuses **Hoje**. Tab order follows the regions; the CTA is the first actionable element after **Trocar**. Only one orange CTA appears (the next action); every other action is secondary or text.

### Origin and return

Home of Meu turno. Every destination it opens (Open day, Close day, Fix & resubmit, Transactions list, Create, Clock, self reports) returns here with the Cockpit re-read; the workstation choice persists per device, branch and branch-local day.

### States

| State | Presentation |
|---|---|
| Loading | header immediately; Skeleton next-action card (title and button shapes); other regions Skeleton independently — an unknown close status is never shown as "Dia ainda não aberto" |
| Terminal choice | region 2 only, with a short explanation; **Trocar** later reopens it |
| Setup needed — no operator link (Member) | [EmptyState](../components/empty-state.md) setup variant in place of regions 3–7: **Seu login ainda não está vinculado a um operador desta filial.**, **Solicitar vínculo** (CTA) or the request status (**Solicitação enviada em {data/hora}**) |
| Setup needed — no usable Terminal | same treatment: **Você ainda não tem um terminal atribuído.**; Member: request action/status; linked Manager/Admin: **Ir para Contas dos operadores** (Gestão) |
| P1–P7 | as cataloged; P6 shows **Enviado às {hora}** and **Desfazer envio** (same-day recorder only); P7 has no CTA and **Ver fechamento** as a text link |
| Partial failure | a region whose read failed shows its own Notice and retry; the next action is computed only from successful reads and says **Não foi possível verificar {o quê}** instead of guessing a lower-priority step |
| Branch day changed | status-rail Notice; workstation choice cleared; the Cockpit re-reads |

## Open day

- **Regions:** PageHeader **Abrir o dia** (breadcrumb from its origin) · confirmation card **Abrir dia para {terminal}** with **Trocar terminal** (Meu turno) or a Terminal [Select](../components/select.md) (Gestão) · date: branch-local today shown as text in Meu turno; in Gestão a [DatePicker](../components/date-picker.md) defaulting to today with future dates disabled and backdating as an explicit change (**Abrir em outra data**) · **Abrir dia** (CTA in Meu turno, primary in Gestão) · **Voltar**.
- **Origin context:** when opened from a blocked sale or payment, a neutral Notice explains **Abrir o dia não registra a venda; você volta ao formulário para confirmar.**
- **States:** setup needed (no Terminal: EmptyState with the setup link); validating; conflict (already open → info Notice **Este dia já está aberto** + **Ir para o fechamento**, resolved through the exact account/date read); lock-blocked (verbatim); error; success → return.
- **Return:** Meu turno → Cockpit; Gestão → originating Work Queue / Monthly reconciliation (a missing-day entry; Dashboard and Monthly financial report open only existing closes, not Open day); blocked operation → that same form with everything typed.

## Close day

### Regions, in reading order

1. **PageHeader** — breadcrumb from the origin; title **Fechamento do dia**; context: Terminal · long date · status badge · mode. Actions: **Voltar**.
2. **Cause notices** — when present, in this order: correction context summary with a link to the full cause group (rejected / opening changed — see Fix & resubmit), successor returned to Draft (**O fechamento de {data} voltou para rascunho porque o fechamento anterior foi corrigido.**), stale version, retained input restored, AA notice.
3. **Identities** — [DetailList](../components/detail-list.md): Aberto por · Contado por (immutable recorder) · Enviado por (current submitter), each user + operator or **Sem operador associado ao registro**, or **ainda não**.
4. **Prévia da diferença · ainda não enviada** — [SummaryStrip](../components/summary-strip.md): Total do fechamento · Total da abertura · Entradas · Saídas; worked equation with actual values (`typography.role.money`), explicitly separate from the server preview. Caption **Calculada com os valores que estão nos campos agora, salvos ou não. Não é a diferença oficial: essa só passa a existir no envio.** Compare the equation with the server preview for the same inputs, version and complete active ledger in exact cents. On disagreement use the catalog’s **explanation differs from server** state: **Subtotal da abertura exibida**, neutral signed **Diferença calculada pelo servidor**, and persistent warning **A conta exibida não confere com a diferença do servidor. Confira a abertura e o fechamento anterior com a gerência antes de enviar ou aprovar.** Do not use Falta/Sobra or sign-based variance colors in that state. Pending, failed or stale comparison reads show **Não foi possível conferir a diferença** and require matching refreshed data. Ordinary save/submit controls remain; management review is guidance, with no forced lock. [Disclosure](../components/disclosure.md) **Ajuda sobre diferença de caixa** uses copy §7.1 for the same state.
5. **Contagem por produto** — editable grid ([Table](../components/table.md)): Produto · Abertura (system-derived, read-only, labelled) · Fechamento (informado pelo operador, [MoneyInput](../components/money-input.md) count variant) · row action (**Repetir abertura** / **Desfazer repetição**). The variance row is last in its `DisplayOrder` position and reads **em branco até o envio** while Draft. Never an "expected closing".
6. **Observações** — [Input](../components/input.md) textarea (max 1000, counter).
7. **Save and submit bar** — [SaveStatus](../components/save-status.md) · **Salvar** (secondary; visible always, disabled when clean or saving) · **Enviar para aprovação** (primary in Gestão; CTA in Meu turno).
8. **Pendências para enviar** — Submit blockers as a list: Draft transactions on this account/date (**{n} movimentações em rascunho** · **Finalizar movimentações**, carrying this close as origin), never-recorded count, and the server's prior-day message when Submit returns it.
9. **Transações do dia** — the `(account, date)` ledger: time, type with **Automática** marker, value, direction, status; Draft rows visibly distinct; net shown; never filtered by author.

### Layout

| Width | Arrangement |
|---|---|
| ≥ `breakpoint.wide` | single reading column up to `spacing.size.content-max`; the count grid as a table with all four columns; Transactions as a table |
| < `breakpoint.wide` | same order; the summary strip wraps to two columns, then one |
| < `breakpoint.medium` | **the count grid becomes a stacked list per product**: product name (`typography.role.body-strong`), **Abertura R$ …** line, the full-width MoneyInput, and the row action below — opening and closing stay together without horizontal scrolling. Region 7 becomes a sticky bottom bar (`layer.sticky`, `shadow.raised`) that hides while the on-screen keyboard is open, so it never covers the focused field. Transactions become a grouped list. |

### Fee entry and historical opening (canonical v55)

The fresh default product catalog contains no Tarifa Bolão or Operador count slot. Under working Q3/Q9, enter each actual daily amount through [Transaction — Create](transactions.md#transaction--create-fast-entry) on this Terminal/date: default Tarifa Bolão under Receita/In and Operador under Entradas/In, both SameDay without a Tab/client requirement. Preserve this close's typed input across that round-trip. Count their physical cash in Dinheiro; never generate an automatic fee, fixed rate or required daily row. Use actual configured type options and ids: names are editable. M11 supplies `MonthlyFinancialRole` on transaction types for reporting; the fresh fee type is `BolaoServiceFee`, while Operador stays `None`. This marker neither creates a daily row nor identifies a counted product. Ordinary non-fiado cash sales remain count-only under working Q5; a stock/cash swap is not also recorded as a product Out. These assumptions remain pending the named shop answers.

For the first close after an upgrade, the server may include a prior-only retired fee in the opening while omitting it from current review rows. Follow the catalog’s [transition procedure](../../docs/screens.md#close-day) and its **explanation differs from server** state, also used on approval. A numerical disagreement is detectable before submit; it does not identify the omitted amount or its cause. Management reviews the inventory output retained with the deployment review and the identified prior close, records the explanation in ordinary Notes, and stops/escalates an unexplained difference. Preserve history and counts; do not add an offsetting transaction. This does not resolve AA or certify a zero first transition.

### Save, autosave and undo presentation

- **Today's plain Draft:** autosave runs; SaveStatus cycles **Alterações não salvas → Salvando… → Salvo às {hora}**. **Salvar** stays visible and disabled while clean (reason: **Nenhuma alteração desde o último salvamento**), enabled when dirty, and enabled for a never-recorded first count.
- **Correction, opening recheck or historical Draft:** no autosave; SaveStatus shows **Alterações não salvas no servidor** in the warning tone until **Salvar** succeeds.
- **Enviar para aprovação:** always explicit; while it awaits the pending save and blockers it reads **Enviando…**; any failure stops with input intact.
- **Repetir abertura / Desfazer repetição:** the row action toggles in place; **Desfazer repetição** remains as long as the catalog allows (not a toast), and the preview updates.

### States

Loading (header and Skeleton grid; inputs not editable until the review loads); validating (inline per row + summary); live variance shown; autosaving / saved / not saved; unsaved on server; opening changed / recount required (cause notice leading); successor returned to Draft; stale (409 → warning Notice **Recarregar e conferir**, a reconciliation view listing current versus your values per product, deliberate choice before a fresh save); coordination busy (verbatim, retry); lock-blocked (read-only grid with the lock reason); **submitted** (read-only DetailList + grid as text, **Desfazer envio** when permitted); error; success (Toast **Fechamento enviado** on the destination).

### Origin and return

From the Cockpit (including **Ver fechamento de hoje**), a status-routed Work Queue / Cash-variance summary / Monthly reconciliation / Dashboard / Monthly financial report exception, or Daily-close approval’s **Gerenciar fechamento**. Report entries use their returned DailyCloseId/status, never date alone. After submit or Voltar: Cockpit (Meu turno) or the actual originating management/report view, retaining its month/window, filters, section/row and focus; from approval → back to that approval with the complete parent origin chain. Without origin → active mode home. Unlinked Manager/Admin retains this access in Gestão.

## Fix & resubmit

- **Regions:** PageHeader **Corrigir fechamento** (context as Close day) · **Por que este fechamento precisa de revisão?** group: one block per event, oldest first, each with its own dated title — **Rejeitado na conferência** (**Rejeitado em {data/hora}**, actor, **Observação da gerência** quoted) and **Abertura alterada** (**Alterada em {data/hora}**, actor, source → target sentence, the "não é uma nova rejeição" line, and [Disclosure](../components/disclosure.md) **Ver o que mudou** with a table Produto · Antes · Depois · Alteração); event badges `dom.review-event-type.*`; honest fallbacks when history is incomplete · next-action line · then the full Close day form (regions 3–9) in its correction mode (explicit **Salvar**, **Reenviar para aprovação**).
- **Layout:** the cause group sits above the form at every width; on compact each event is its own section and the before/after table becomes label/value rows per product.
- **States:** rejected · opening changed · both · editing · resubmitted (read-only, awaiting) · error; history incomplete fallbacks (**Data da rejeição indisponível**, **Detalhes da alteração indisponíveis para este fechamento**).
- **Origin and return:** Cockpit rejected state for own work; Work Queue, Cash-variance summary, Monthly reconciliation, Dashboard or Monthly financial report for a returned Rejected close. After elevated resubmit or Voltar, restore the actual management/report origin with month/window, filters, section/row, mode and focus; retain any nested approval chain. Own resubmit returns to Cockpit; without origin → active mode home.

## AA remainder: retired-product rows

The catalog keeps historical reads available and gates editing (gap AA). Ready now: historical values remain readable by product name with their returned opening and closing values. `ResponseDailyCloseReviewItemJson` has no product-active flag, so the review response alone cannot identify a retired row or justify an **Inativo** marker. Do not infer retirement from a name, display order or saved value. Identifying unsupported candidates, assigning per-row editability and presenting a retirement marker belong to the AA contract reconciliation; a read-only view may show all returned values without classifying product activity. **Not blueprinted until the owner's contract decision:** whether and how a close that holds such a row can be edited, previewed or saved, the operator-facing explanation, and any recovery path. Apps must not ship a save or preview for such a close that drops or resends the retired row.

## Verification boundary

Statically specified: region order, the compact stacking, tokens, focus order, states and returns. For Web/Mobile M5 with a real API: autosave timing against `UpdatedAt`, keyboard-only count entry, screen-reader announcements of preview and save results (coalesced), 375px with enlarged text, the sticky bar with the on-screen keyboard, and a current-day scenario for same-day rules.
