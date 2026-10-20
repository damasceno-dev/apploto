# Transactions, cheque plans and fiado — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M6**. Semantics: catalog entries [Transaction — Create](../../docs/screens.md#transaction--create-fast-entry), [Transaction — Edit](../../docs/screens.md#transaction--edit-correction--audit), [Transactions list](../../docs/screens.md#transactions-list), [Installment plan](../../docs/screens.md#installment-pre-dated-cheque-plan), [Client statement](../../docs/screens.md#client-statement-extrato-de-fiado), [Receber pagamento](../../docs/screens.md#receber-pagamento). Contracts: M3–M7 ledger, M7.7 Phases 5 (fiado, pairing, generated movements), 6 (idempotency, `If-Match`), 7.6.2 (atomic plan finalize), 7.6.3 (type filter) — all shipped. No remainder; open product questions (which fiado sales move counted stock, mixed tender) stay with the operator's explicit choice and the server, not with this layout.

The ledger has three dates and the screens must never blur them: **Data da transação** (`Date`, the business day), **Vencimento** (`DueDate`, when it settles) and **Pagamento** (`PaidAt`, when a cheque or card actually cleared). Every form and table labels them separately, and fiado debt is never read from a row's `PaidAt` — it comes from the server's branch-wide FIFO statement.

## Transaction — Create (fast entry)

### Regions

1. **PageHeader** — **Nova transação**; context: mode, branch-local date; **Voltar** to origin.
2. **Form** (single column on compact; two columns of [FormFields](../components/form-field.md) at `spacing.form.column-min` on wider layouts), in operator order: **Tipo** ([Select](../components/select.md) searchable; repayment and system types never listed) · **Valor** ([MoneyInput](../components/money-input.md)) · **Conta** (own accounts in Meu turno; any account in Gestão; Tab for fiado types) · **Cliente** (when the type requires it; searchable, with **Novo cliente** opening the Clients dialog inline) · **Entregou mercadoria contada ao cliente?** (fiado sale only — radio **Sim** / **Não**, none selected) · **Vencimento** (card/cheque types) · **Data da transação** (defaults to branch today) · **Hora** (prefilled from the branch clock) · **Descrição** (optional) · **Registrado por** (Gestão on-behalf only; required when the manager has no linked operator).
3. **Impact preview** — a quiet panel below the form, updating after input pauses: only the sections that apply (Recebíveis, Fiado for the selected client, Diferença de caixa of the terminal's close), each as before → after values; a failed preview shows **Prévia indisponível** and never blocks saving. For a counted-stock fiado sale it names the paired terminal and states **Saída de mercadoria — não é retirada de dinheiro**.
4. **Actions** — **Salvar** (primary) · **Salvar como rascunho** (secondary) · **Cancelar** (text). A cheque-rule type swaps the form for **Continuar como cheque pré-datado** → Installment plan.

### Opening-side inflows (canonical v55)

The configured Tarifa Bolão and Operador types are ordinary Terminal In entries under working Q3/Q9. Enter the actual amount and use the existing scoped create, preview, idempotent retry and lock rules; no automatic fee or special command is introduced. Their default names are not stable identities. When entered from Close day, preserve its input and return context as in the [fee-entry guidance](operator-day.md#fee-entry-and-historical-opening-canonical-v55). For ordinary cash sales, working Q5 is count-only: do not also record a product Out for the same stock/cash swap. M10 import and M11 financial classification are not implemented by this form.

### States

Loading lookups (fields Skeleton; type list required before anything else); validating (inline + summary, input kept); **terminal day not open** (warning Notice **O dia do {terminal} ainda não foi aberto. Abrir o dia para registrar esta venda?** with **Abrir o dia** when permitted; no offer when the close is frozen); lock-blocked (verbatim); permission fallback (setup Notice integrating the verbatim 400/403); saving (**Salvando…**, form locked); **result unconfirmed** (warning Notice, the frozen candidate shown read-only, **Verificar e tentar novamente** resends the same key and body; editing the form starts a separate candidate and the screen keeps both visible until the first is settled); idempotency conflict (verbatim, original preserved); success → Toast **Transação salva** at the origin.

### Layout and focus

Compact: one column, actions at the end and a sticky action bar that hides while the keyboard is open. Focus starts on **Tipo**; changing the type keeps valid values and moves focus to the first newly required field. Enter in a field never saves unless focus is on **Salvar**.

### Return

Cockpit when entered there; Transactions list otherwise (including the **Nova transação** shortcut); after **Abrir o dia**, back here with everything typed.

## Transaction — Edit (correction / audit)

### Regions

1. **PageHeader** — **Transação**; context: type, value (money), **Data da transação**, status badge, **Automática** marker when generated; breadcrumb from the origin.
2. **Fatos fixos** — [DetailList](../components/detail-list.md): Valor, Conta, Tipo, Direção, Data da transação, Registrado por (operator), Criada em / por, Atualizada em / por, Cancelada em / por / motivo. These never render as inputs.
3. **Origem** *(generated movement)* — **Gerada automaticamente pela operação de fiado de {nome}.** (the source may be a counted-stock sale or a cash repayment; use the returned generation kind and source link) with **Abrir origem fiado**; *(fiado source)* **Movimento gerado: {tipo} no {terminal}** with **Abrir movimento**; *(installment)* **Parcela {i}/{N} de um cheque pré-datado** with **Abrir plano completo** (or **Abrir plano completo para edição**).
4. **Correção** *(editable rows only)* — Descrição · Vencimento · Pagamento (`PaidAt`, cheques and card settlements only — disabled on fiado rows with the reason **O pagamento de fiado é registrado em Receber pagamento**) · Cliente · Hora.
5. **Prévia do impacto** *(Manager/Admin)* — **Ver impacto** runs the edit preview; sections only when they apply; **Sem fechamento — esta conta não tem contagem de caixa** for accounts without a close; a stale preview is hidden after any change.
6. **Ações** — **Salvar correção** (primary) · **Finalizar rascunho** (standalone Draft only) · **Cancelar transação** (danger outline → reason dialog) · for an installment: **Cancelar só esta parcela** / **Cancelar parcelas restantes** (confirmation listing each affected cheque with due date and value).

### States

Loading; read-only (cancelled, locked, generated, another operator's row, prior-day Member row — fields as text, the restriction reason under the header, navigation links still available); validating; preview shown; saving; **stale** (409 → **Recarregar e conferir** with current versus proposed values side by side); frozen ledger (verbatim for finalize/cancel; the narrow field edit stays available); not found; success (Toast at origin). A Draft installment of a Draft/Mixed plan never offers **Finalizar rascunho**; opening it lands in the plan editor (catalog) and this screen remains reachable read-only from there.

### Return

The originating list, report or blocker; without one, Transactions list.

## Transactions list

### Regions

1. **PageHeader** — **Transações**; context: **Toda a filial · Gestão** or **Suas contas · Meu turno**; actions **Nova transação** (primary) and **Novo cheque pré-datado** (secondary).
2. **[FilterBar](../components/filter-bar.md)** — De / Até, Situação, Conta (role-shaped), Tipo de transação (**Todos**), Operador (Gestão only), Cliente, **Somente minhas**; scope summary (**Filtro ativo: 24/09/2026 · padrão — hoje, todos os tipos**); deep-link-only plan filter shown as a removable scope chip **Parcelas do plano {descrição}**.
3. **[Table](../components/table.md)** — columns: Data da transação · Tipo (+ **Automática**) · Conta · Cliente · Direção (text, neutral) · Valor (right) · Vencimento · Pagamento · Situação (badge) · action **Abrir transação**. Draft and Cancelled rows visibly distinct and excluded from any total. The date cell is plain data — no date-only link.
4. **[Pagination](../components/pagination.md)** — with `TotalCount` in the region heading.

### Layout

≥ `breakpoint.wide`: full table. Below: the table scrolls in its labelled region with the date column sticky and the action column sticky at the end, so nothing actionable is hidden; below `breakpoint.medium` it becomes a grouped list: line 1 type + value, line 2 date · account · client, line 3 status badge + **Automática** + **Abrir transação**.

### States

Loading; empty (**Nenhuma transação neste filtro.** + **Limpar filtros**; Member out-of-scope empties look the same — no leak); error; updating; success. Whole-row destination as specified by the Table contract; selection checkboxes appear only in the sibling-cancel flow.

### Return

Itself after create/edit when it was the origin; filters, page and the focused row restored.

## Installment (pre-dated cheque) plan

### Creation mode — regions

1. **PageHeader** — **Cheque pré-datado**; context: account and branch date.
2. **Plano** — Tipo (cheque-rule types only), Valor total, Conta, Cliente, Data da transação, Hora, Descrição, Registrado por (Gestão).
3. **Parcelas** — a [Tabs](../components/tabs.md)-like choice **Informar parcelas** (default) / **Gerar automaticamente** (Vencimento da primeira + Quantidade). Manual mode: an editable table Nº · Vencimento · Valor with add/remove rows (2–24) and a running **Soma das parcelas − valor total** check (`dom.cash-variance.zero` text when zero, warning text otherwise — explanatory; the server enforces).
4. **Prévia** — **Ver prévia** → row preview (adjusted due dates, rounding on the last row, `CH PRE (i/N)` descriptions) and impact (open-cheque group with bucket, fiado delta, variance shift).
5. **Ações** — **Salvar plano** · **Salvar como rascunho** · **Cancelar**.

States: as Create, with one `Idempotency-Key` for the whole plan and the same uncertain-result recovery.

### Existing-plan mode — regions

1. **PageHeader** — **Plano de cheque pré-datado**; status badge `dom.installment-plan-status.*`; per-status counts; total of non-cancelled rows.
2. **Plan action** — Draft: **Finalizar plano** (primary; activates every installment together); Mixed: **Finalizar parcelas restantes** with a confirmation that names exactly the remaining Draft installments and their total. No per-row finalize exists anywhere in this mode.
3. **Parcelas** — one row per installment: Nº · Vencimento · Valor (read-only) · Pagamento · status badge · **Salvar esta parcela** (disabled until that row changes) · **Abrir parcela** (Transaction — Edit). Pending unsaved rows are flagged; **Finalizar plano** is disabled with the reason **Salve ou descarte as alterações das parcelas antes de finalizar** while any row is dirty.
4. **Cancelamento** — per-row cancel via Transaction — Edit; there is no whole-plan discard.

States: loading; stale set (`TRANSACTION_PLAN_STALE_WRITE` → **Recarregar e conferir**); mixed confirmation required (verbatim, then the confirmation); per-row saves report individually (never an atomic save claim); read-only rows (locked, cancelled, another operator's, prior day for a Member).

Layout: below `breakpoint.medium` each installment is a stacked block with its fields and per-row action. Return: to the installment or list that opened it.

## Client statement (Extrato de fiado)

### Regions

1. **PageHeader** — client name; context: phone and CPF masks; **Receber pagamento** (primary).
2. **Saldo** — [SummaryStrip](../components/summary-strip.md): **Saldo atual** with the balance mapping (`dom.fiado-balance.*`: **R$ … · em aberto** / **quitado** / **+R$ … · crédito do cliente**) and **Data da venda mais antiga em aberto**; a credit explanation in words when negative internally.
3. **Período** — De / Até ([DatePicker](../components/date-picker.md) range; empty De = whole history) with **Saldo inicial** and **Saldo final** of the window.
4. **Movimentos** — Table: Data da transação · Conta fiado · Tipo (the tenant's type name, which tells a sale from a repayment) · Descrição · Direção (**Entrada** / **Saída**, copy §5) · Valor · **Saldo após** (mapped display) — newest first, paged; the whole-window reconciliation is the server's.
5. **Imprimir** — the device's print/PDF.

States: Member without a Tab grant (setup Notice, no data); loading; empty (**Sem histórico de fiado.** all-clear); error; success. No Tab balance and no drawer figure appear here. Return: to its source (Clients, Create, Fiado balance).

## Receber pagamento

### Regions

1. **PageHeader** — **Receber pagamento**; client name; **Voltar**.
2. **Saldo atual** — the statement's balance with the oldest unpaid sale date.
3. **Form** — **Conta fiado que recebe** (default: the workstation terminal's paired Tab when it is among the operator's grants; otherwise a choice) · **Valor** · **Forma de recebimento** (no default; options from types with `FiadoPaymentKind`, each labelled with its effect — **Dinheiro (entra no caixa)** / **Outro meio (não entra no caixa)**) · **Data** (branch today) · **Descrição**.
4. **Efeito no caixa** — appears once a method is chosen: cash → **Entrada automática de R$ … no {terminal}**; non-cash → **Não altera o dinheiro contado**.
5. **Ações** — **Confirmar pagamento** (primary) · **Cancelar**.

States: no method chosen (**Escolha a forma de pagamento.** on submit); no repayment method configured for the branch (setup Notice; Manager/Admin link to Categories & Transaction Types); terminal day not open (cash only — the same offer as Create, naming the paired terminal); lock-blocked; result unconfirmed (same-key verification); success → the confirmation shows the **re-read** statement balance with its mapping (an overpayment reads as client credit). Return: Client statement when opened there, otherwise its source.

## Verification boundary

Static: field order, labels, the three-date distinction, generated-movement links, plan finalize rules and states. Real-UI (Web/Mobile M6): idempotent retry with a real lost response, `If-Match` stale paths, keyboard entry speed for Create, 375px forms with the keyboard open, and fiado scenarios against a branch with configured repayment methods.
