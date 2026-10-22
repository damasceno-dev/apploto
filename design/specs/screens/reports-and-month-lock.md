# Reports and month lock — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M8**. Semantics: catalog entries [Daily ledger](../../docs/screens.md#daily-ledger), [Fiado balance](../../docs/screens.md#fiado-balance), [Fiado aging](../../docs/screens.md#fiado-aging), [Open-cheque aging](../../docs/screens.md#open-cheque-aging), [Cash-variance summary](../../docs/screens.md#cash-variance-summary), [Monthly reconciliation + lock](../../docs/screens.md#monthly-reconciliation--lock). Contracts: server M7 reports with M7.7 Phase 4 (atomic lock), Phase 5 (FIFO fiado) and M11 Phases 2–3 (whole-set totals, deactivated drill-downs) — shipped. **Not here:** Dashboard, Upcoming settlements and the Monthly financial report remain uncataloged (gap Q, server M11); nothing below borrows their figures.

Every report follows one shape so a manager learns it once: a header that names the report, a filter bar that names the scope, a summary strip of **whole-set** figures from the response, and a table whose rows open the record they summarize. Page-local sums are labelled as such, and no report adds money across accounts into a figure the server did not return.

## Shared report frame

1. **PageHeader** — report title; context line with the applied scope in words.
2. **[FilterBar](../components/filter-bar.md)** — the endpoint's own query fields only; **Ver** applies; scope summary below.
3. **[SummaryStrip](../components/summary-strip.md)** — whole-set totals from the response, each with its period; signed values use `dom.cash-variance.*`, balances `dom.fiado-balance.*`.
4. **[Table](../components/table.md)** — rows ordered by the server; record destinations as explicit links; Pagination under it.
5. **Explanation** *(where the catalog has one)* — one quiet paragraph or a collapsed Disclosure (FIFO netting, what excludes Draft rows).

Responsive behavior for all six: summary strip 4 → 2 → 1 columns; tables scroll inside their region with the first column sticky between `breakpoint.medium` and `breakpoint.wide`, and become grouped lists below `breakpoint.medium` (primary line: the row's identity; second line: its figures; trailing link). States for all six: loading (Skeleton strip and rows), empty (scoped EmptyState), error (Notice with retry; filters kept), updating, success. Drill-downs record the report with its filters as origin; closing a report returns to Manager Work Queue when nothing narrower opened it.

## Daily ledger

- **Filters:** Conta (required; preselects the last account viewed on this device), De / Até (≤ 366 days; the server's 400 renders verbatim).
- **Summary:** Saldo inicial · Entradas · Saídas · Líquido · Saldo final — for the one account and window; an **Inativa** badge beside a deactivated account's name.
- **Table:** Data da transação · Tipo · Categoria · Cliente · Registrado por · Descrição · Direção · Valor · Vencimento · Pagamento · **Abrir transação**. Direction is text, never profit/loss color.
- **Notes:** the running balance is a ledger balance, not cash on hand; the header says **Movimento da conta**, never "caixa".

## Fiado balance

- **Filters:** Posição em (as-of date, default branch today), Cliente.
- **Tabs ([Tabs](../components/tabs.md)):** **Dívidas em aberto ({DebtorCount})** with **Total em aberto** = `TotalDebt` · **Saldo crédito ({CreditorCount})** with **Crédito dos clientes** = `TotalCredit` as a positive amount. The net `TotalOutstanding` is not shown as an amount to collect.
- **Table per tab:** Cliente · Saldo (mapped display) · actions **Ver extrato** (Client statement, tab and filters kept for return), **Ver por idade** (Fiado aging with the client carried), **Receber pagamento**.
- **Empty:** **Nenhum cliente com dívida em aberto.** / **Nenhum cliente com crédito.**

## Fiado aging

- **Filters:** Posição em, Cliente, Conta fiado.
- **Summary:** **Total em aberto** and one figure per bucket (`ByBucket`, zeros included) with the bucket badge (`dom.aging-bucket.*`) as the label, plus **Venda mais antiga em aberto**.
- **Explanation:** **Os recebimentos de cada cliente quitam primeiro as vendas mais antigas, em todas as contas fiado. Só o saldo restante de cada venda aparece aqui.**
- **Table:** Data da transação · Vencimento · Dias em aberto · Faixa (badge) · Cliente · Conta · Descrição · Valor restante · **Abrir transação** (read context; `PaidAt` disabled on fiado). Order: oldest exposure first (server).

## Open-cheque aging

- **Filters:** Posição em, Conta, Cliente.
- **Summary:** **Total em aberto**, **Lançamentos em aberto**, per-bucket figures; `TotalCount` is the number of groups (plans plus standalone cheques), labelled **Grupos em aberto**.
- **Table:** expandable plan groups ([Disclosure](../components/disclosure.md) per row): Cliente · Conta · Descrição · **{abertas}/{total} parcelas em aberto** · Vencimento mais antigo · Faixa · Total em aberto; expanded rows: Vencimento · Valor · Dias · Faixa · **Abrir parcela** (where `PaidAt` is set as cheques clear). A null `OriginTransactionId` identifies a **Cheque avulso** one-row group: show its transaction directly with **Abrir transação**, without inventing a plan or a plan-finalization action. Plans remain included independently of their type's instrument; standalone rows require `Cheque`, including retired classified types. Totals cover both, with paid, Draft and cancelled rows excluded.
- **Compact:** each plan is a block with its summary line and a **Ver parcelas** disclosure; each standalone cheque is a direct transaction block. Keep the report's account/client/as-of filters for return.

## Cash-variance summary

- **Filters:** De / Até (default: month to date), Conta.
- **Summary:** Total · Média · Maior · Menor (signed, colored by `dom.cash-variance.*` with **falta/sobra** captions) and **Enviados / Aprovados / Rejeitados** counts; caption **Rascunhos (inclusive desfeitos, reabertos e em correção) ficam fora.**
- **Table:** Data · Conta · Situação (badge) · Diferença · status-routed link (**Conferir** for Submitted; **Ver** otherwise). Each row opens by its own `DailyCloseId`.
- **Empty:** **Nenhuma diferença oficial neste período.** with the caption that a Draft close has no variance row yet.

## Monthly reconciliation + lock

### Regions

1. **PageHeader** — **Fechamento mensal**; month navigation (**Mês anterior** · `MM/yyyy` · **Próximo mês**); context: **Data de bloqueio {data}** or **Nunca bloqueado**; month state badges **Bloqueado** / **Em andamento** / **Encerrado, não bloqueado**.
2. **Earlier month to resolve** *(after a refused lock)* — warning Notice with the server's verbatim message and **Ir para {MM/yyyy}**, placed above everything so the month never has to be hunted.
3. **Prontidão** — a [Card](../components/card.md): **Pronto para bloquear** (success text) or **{n} bloqueios** (warning text), with the advisory note **A verificação final é feita no bloqueio.** and **Bloquear mês** (primary; absent for the current or a locked month, with the reason).
4. **Bloqueios** — list of structured blockers as sentences (copy §5 labels + day + account), each with its destination: status-routed close, **Ver rascunhos** (Transactions list, Draft filter, that day) or **Abrir o dia**.
5. **Dias do mês** — table: Dia · Fechamentos (account + status badge + official variance) · Ativas · Rascunhos · Canceladas · Diferença líquida; compact: one block per day with only non-empty facts.

### Lock flow

**Bloquear mês** opens a confirmation: **Bloquear {mês}?** — **Depois do bloqueio, transações, fechamentos e registros de ponto até {último dia} não podem mais ser alterados.** — **Bloquear mês** (primary) / **Voltar**. It sends the Setting `ETag`; a stale setting returns **Recarregar e conferir**; an unresolved earlier month shows region 2; coordination busy is retryable; success shows **Mês bloqueado** with the new lock date and the month in its locked state.

### States

Loading; clean month; blockers present; locked (read-only, no lock action); current month (lock action absent: **O mês atual não pode ser bloqueado.**); out-of-range month (verbatim 400); error; uncertain lock (warning Notice; re-read the report and settings before anything else).

## Not yet blueprinted (gap Q)

These headings only track the three server M11 catalog entries. Each is **not blueprinted**: the [catalog](../../docs/screens.md) defines what the screen shows and does against shipped contracts, but the M4 per-screen gate (gap Q) holds until server M11 Phases 4–7 are independently accepted. Nothing below defines layout, regions, components or states; design M4 4.8.4 writes them, inheriting the shared report frame above.

### Dashboard

Not blueprinted — gap Q. Catalog: [Dashboard](../../docs/screens.md#dashboard).

### Upcoming settlements

Not blueprinted — gap Q. Catalog: [Upcoming settlements](../../docs/screens.md#upcoming-settlements).

### Monthly financial report

Not blueprinted — gap Q. Catalog: [Monthly financial report](../../docs/screens.md#monthly-financial-report).

## Verification boundary

Static: frame, columns, summaries, labels and returns. Real-UI (Web/Mobile M8): whole-set totals against a populated local API, FIFO explanations with partial payments, the lock confirmation and its refusals, table scrolling and grouped lists at 375px with enlarged text.
