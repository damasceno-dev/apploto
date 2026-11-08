# Transactions, cheque plans and fiado — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M6**. Semantics: catalog entries [Transaction — Create](../../docs/screens.md#transaction--create-fast-entry), [Transaction — Edit](../../docs/screens.md#transaction--edit-correction--audit), [Transactions list](../../docs/screens.md#transactions-list), [Installment plan](../../docs/screens.md#installment-pre-dated-cheque-plan), [Client statement](../../docs/screens.md#client-statement-extrato-de-fiado), [Receber pagamento](../../docs/screens.md#receber-pagamento). Contracts: M3–M7 ledger, M7.7 Phases 5 (fiado, pairing, generated movements), 6 (idempotency, `If-Match`), 7.6.2 (atomic plan finalize), 7.6.3 (type filter) — all shipped. No remainder; open product questions (mixed tender) stay with the server, not with this layout.

> **Design 4.9.6 revision (2026-10-31, server M9.6 P6/P9, canonical v61; source semantics reconciled).** The counted-stock question is removed (every fiado sale writes its saída do caixa), the repayment method stays mandatory with no default over the seeded Dinheiro/PIX and configured others, and **Depósito Dinheiro** is added as the two-leg cofre transfer. Opening/imported debts and cheque → fiado conversion are not entered on these screens. Arithmetic input in money fields follows Design 4.9.7 (Shared/core parser).

The ledger has three dates and the screens must never blur them: **Data da transação** (`Date`, the business day), **Vencimento** (`DueDate`, when payment is expected) and, since v62, the receivable's **settlement events** (each with *Data do evento* `OccurredOn` and *Lançado em* `PostedOn`; formerly the `PaidAt` marker: cheque deposited under product Q2, or card paid; not proof of clearing or bank reconciliation). Every form and table labels them separately, and fiado debt is never read from a row's `PaidAt` — it comes from the server's branch-wide FIFO statement.

## Transaction — Create (fast entry)

### Regions

1. **PageHeader** — **Nova transação**; context: mode, branch-local date; **Voltar** to origin.
2. **Form** (single column on compact; two columns of [FormFields](../components/form-field.md) at `spacing.form.column-min` on wider layouts), in operator order: **Tipo** ([Select](../components/select.md) searchable; repayment and system types never listed) · **Valor** ([MoneyInput](../components/money-input.md)) · **Conta** (own accounts in Meu turno; any account in Gestão; Tab for fiado types) · **Cliente** (required on a fiado sale and whenever the type requires it; searchable, with **Novo cliente** opening the Clients dialog inline) · **Vencimento** (card/cheque types) · **Data da transação** (defaults to branch today) · **Hora** (prefilled from the branch clock) · **Descrição** (optional) · **Registrado por** (Gestão on-behalf only; required when the manager has no linked operator).
3. **Impact preview** — a quiet panel below the form, updating after input pauses: only the sections that apply (Recebíveis, Fiado for the selected client, Diferença de caixa of the terminal's close), each as before → after values; a failed preview shows **Prévia indisponível** and never blocks saving. For every fiado sale it names the paired caixa and states **Saída automática do caixa — o valor não entrou no caixa (venda fiado)**; the caixa's difference stays hidden while its turno is Draft (blind count).
4. **Actions** — **Salvar** (primary) · **Salvar como rascunho** (secondary) · **Cancelar** (text). A cheque-rule type swaps the form for **Continuar como cheque pré-datado** → Installment plan — never on a conta fiado (a fiado sale is one entry; the server refuses a fiado plan verbatim).
5. **Depósito Dinheiro** is not a type in this list: its system type is refused here and it has its own step below.

### Opening-side inflows (canonical v55; superseded in v61)

**v61:** Tarifa Bolão, Tarifa Especial and Relatório do terminal are now close-form fields on [Close day](operator-day.md) and their system types never appear in **Tipo**; the paragraph below is the v55–v60 history. Until v61 the configured Tarifa Bolão and Operador types remained shipped ordinary Terminal In entries. Product Q3 confirms the daily service fee; only Q9 report definition, printed label, sign and inclusion remain pending here. Enter the actual amount and use the existing scoped create, preview, idempotent retry and lock rules; no automatic fee or special command is introduced. Their default names are not stable identities. When entered from Close day, preserve its input and return context as in the [fee-entry guidance](operator-day.md#fee-entry-and-historical-opening-canonical-v55-fee-entry-superseded-in-v61). Product Q5 confirms equal purchase/sale value and count-only ordinary cash sales: do not also record a product Out for the same stock/cash swap. Q3/Q5 are confirmed product facts (2026-10-23); this v58 candidate factual correction adds no future M9.6 P7 generated field or command. M10 import and M11 financial classification are not implemented by this form.

### States

Loading lookups (fields Skeleton; type list required before anything else); validating (inline + summary, input kept); fiado sale refused (verbatim: no client, unpaired conta fiado — the Manager/Admin Notice links to Accounts —, not the caixa's custodian, fiado plan); **terminal day not open** (warning Notice **O dia do {terminal} ainda não foi aberto. Abrir o dia para registrar esta venda?** with **Abrir o dia** when permitted; no offer when the close is frozen); lock-blocked (verbatim); permission fallback (setup Notice integrating the verbatim 400/403); saving (**Salvando…**, form locked); **result unconfirmed** (warning Notice, the frozen candidate shown read-only, **Verificar e tentar novamente** resends the same key and body; editing the form starts a separate candidate and the screen keeps both visible until the first is settled); idempotency conflict (verbatim, original preserved); success → Toast **Transação salva** at the origin.

### Layout and focus

Compact: one column, actions at the end and a sticky action bar that hides while the keyboard is open. Focus starts on **Tipo**; changing the type keeps valid values and moves focus to the first newly required field. Enter in a field never saves unless focus is on **Salvar**.

### Return

Cockpit when entered there; Transactions list otherwise (including the **Nova transação** shortcut); after **Abrir o dia**, back here with everything typed.

## Transaction — Edit (correction / audit)

### Regions

1. **PageHeader** — **Transação**; context: type, value (money), **Data da transação**, status badge, **Automática** marker when generated; breadcrumb from the origin.
2. **Fatos fixos** — [DetailList](../components/detail-list.md): Valor, Conta, Tipo, Direção, Data da transação, Registrado por (operator), Criada em / por, Atualizada em / por, Cancelada em / por / motivo. These never render as inputs.
3. **Origem** *(generated movement)* — **Gerada automaticamente pela operação de fiado de {nome}.** (the source may be a counted-stock sale or a cash repayment; use the returned generation kind and source link) with **Abrir origem fiado**; *(fiado source)* **Movimento gerado: {tipo} no {terminal}** with **Abrir movimento**; *(installment)* **Parcela {i}/{N} de um cheque pré-datado** with **Abrir plano completo** (or **Abrir plano completo para edição**).
4. **Correção** *(editable rows only)* — Descrição · Vencimento · Cliente · Hora. Since v62 there is no payment field: a cheque's deposit, return or conversion is recorded by its own command (screens pending, Design 4.9.9), and fiado is paid through **Receber pagamento**. The frozen **Classificação** (instrument, monthly role, financial class and how it was obtained) shows read-only.
5. **Prévia do impacto** *(Manager/Admin)* — **Ver impacto** runs the edit preview; sections only when they apply; **Sem fechamento — esta conta não tem contagem de caixa** for accounts without a close; a stale preview is hidden after any change.
6. **Ações** — **Salvar correção** (primary) · **Finalizar rascunho** (standalone Draft only) · **Cancelar transação** (danger outline → reason dialog) · for an installment: **Cancelar só esta parcela** / **Cancelar parcelas restantes** (confirmation listing each affected cheque with due date and value).

### States

Loading; read-only (cancelled, locked, generated, another operator's row, prior-day Member row — fields as text, the restriction reason under the header, navigation links still available); validating; preview shown; saving; **stale** (409 → **Recarregar e conferir** with current versus proposed values side by side); frozen ledger (verbatim for finalize/cancel; the narrow field edit stays available); not found; success (Toast at origin). A Draft installment of a Draft/Mixed plan never offers **Finalizar rascunho**; opening it lands in the plan editor (catalog) and this screen remains reachable read-only from there.

### Return

The originating list, report or blocker, explicitly including Upcoming settlements and Dashboard cheque/receivable items; preserve their branch-bound month/window, account/instrument filters, page/size, section and clicked TransactionId for focus. A plan route also retains OriginTransactionId and the complete report parent chain. Without origin, Transactions list in the active mode.

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

Layout: below `breakpoint.medium` each installment is a stacked block with its fields and per-row action. Return: to the installment, list or report that opened it, preserving the branch-bound origin chain, source month/window and all account/instrument/page filters. Upcoming settlements / Dashboard plan entries use the returned OriginTransactionId and retain the clicked TransactionId for focus; a changed Draft/Mixed state uses this same editor, never a per-row finalize. Without origin, return to Transactions list. This 2026-10-22 report integration awaits acceptance under M4 4.8.4.

## Client statement (Extrato de fiado)

### Regions

1. **PageHeader** — client name; context: phone and CPF masks; **Receber pagamento** (primary).
2. **Saldo** — [SummaryStrip](../components/summary-strip.md): **Saldo atual** with the balance mapping (`dom.fiado-balance.*`: **R$ … · em aberto** / **quitado** / **+R$ … · crédito do cliente**) and **Data da venda mais antiga em aberto**; a credit explanation in words when negative internally.
3. **Período** — De / Até ([DatePicker](../components/date-picker.md) range; empty De = whole history) with **Saldo inicial** and **Saldo final** of the window.
4. **Movimentos** — Table: Data da transação · Conta fiado · Tipo (the tenant's type name, which tells a sale from a repayment) · Descrição · Direção (**Entrada** / **Saída**, copy §5) · Valor · **Saldo após** (mapped display) — newest first, paged; the whole-window reconciliation is the server's.
5. **Imprimir** — the device's print/PDF.

States: Member without a Tab grant (setup Notice, no data); loading; empty (**Sem histórico de fiado.** all-clear); error; success. No Tab balance and no drawer figure appear here. Return: to its source (Clients, Create, Fiado balance).

## Depósito Dinheiro (cofre)

Two-leg cash transfer from the operator's caixa to a cofre (`POST /transaction/safe-deposit`, server M9.6 P9). Entered from the Cockpit's actions or the Transactions list in the same mode as Create; one operation, never two forms.

### Regions

1. **PageHeader** — **Depósito no cofre**; caixa and turno; **Voltar**.
2. **Form** — **Caixa de origem** (the workstation caixa in Meu turno; any caixa in Gestão) · **Cofre** ([Select](../components/select.md) of active `Safe` accounts the caller may use — a Member's granted cofres; preselected when exactly one) · **Valor** ([MoneyInput](../components/money-input.md)) · **Data** (branch today) · **Hora** · **Descrição** · **Registrado por** (Gestão on-behalf only).
3. **Efeito** — a static explanation, never a calculation: **Sai do caixa e entra no cofre na mesma operação. Não é despesa nem receita.**
4. **Actions** — **Confirmar depósito** (primary) · **Salvar como rascunho** · **Cancelar**.

States: no cofre (setup Notice; Manager/Admin link to Accounts); refused (verbatim: cofre not found or inactive, same account, not the custodian, turno frozen, locked date); saving; **result unconfirmed** (same-key verification as Create: a replay returns the same transfer); success → Toast **Depósito registrado** with both rows reachable. In lists and the turno's movements the cofre row carries the **Automática** origin linked to its caixa saída; cancelling or finalizing is offered only on the caixa row and moves both. Return: to its source.

## Receber pagamento

### Regions

1. **PageHeader** — **Receber pagamento**; client name; **Voltar**.
2. **Saldo atual** — the statement's balance with the oldest unpaid sale date.
3. **Form** — **Conta fiado que recebe** (default: the workstation terminal's paired Tab when it is among the operator's grants; otherwise a choice) · **Valor** · **Forma de recebimento** (no default — nothing preselected, nothing remembered; options from types with `FiadoPaymentKind`, normally the seeded **Dinheiro** and **PIX** plus any configured others, each labelled with its effect — **Dinheiro (entra no caixa)** / **Outro meio (não entra no caixa)**) · **Data** (branch today) · **Descrição**.
4. **Efeito no caixa** — appears once a method is chosen: cash → **Entrada automática de R$ … no {terminal}**; non-cash → **Não altera o dinheiro contado**.
5. **Ações** — **Confirmar pagamento** (primary) · **Cancelar**.

States: no method chosen (**Escolha a forma de pagamento.** on submit); no repayment method configured for the branch (setup Notice; Manager/Admin link to Categories & Transaction Types); terminal day not open (cash only — the same offer as Create, naming the paired terminal); lock-blocked; result unconfirmed (same-key verification); success → the confirmation shows the **re-read** statement balance with its mapping (an overpayment reads as client credit). Return: Client statement when opened there, otherwise its source.

## Verification boundary

Static: field order, labels, the three-date distinction, generated-movement links, plan finalize rules and states. Real-UI (Web/Mobile M6): idempotent retry with a real lost response (sale pair, repayment and deposit), `If-Match` stale paths, keyboard entry speed for Create, 375px forms with the keyboard open, and fiado scenarios against a branch with configured repayment methods.

## M9.7 P3–P5 increment — provider batches and bank movements (Design 4.9.10)

Authored against canonical v63 and the .NET CardProvider/CardSettlement/BankMovement DTOs; v63 semantics independently accepted 2026-11-05; real-app execution pending. Existing transactions still show their frozen classification and event history; none of these controls edits a locked original.

- **Manager workspace.** Show provider identity, brand, receiving account and instrument together, with separate **Previsão de chegada**, **Data do evento** and **Lançado em**. Provider configuration is explicit and immutable; new terms use a new identity. No guessed bank data, timing or percentage. `[DTO] ResponseCardProviderJson`, `ResponseBankReceiptJson`.
- **Batch selection.** A selectable row shows original date, due date, remaining gross amount and current source/chain versions from server reads. The review panel repeats each selected identity and exact allocated amount. **Liquidar lote** posts `RequestCardSettlementJson`; no app-side gross/net formula. One stale/ineligible item leaves the whole selection unsaved. The server result shows **Valor bruto**, **Taxa confirmada**, **Valor líquido esperado**, **Valor observado** and **Diferença sem conciliação** as distinct figures. `[DTO] ResponseCardSettlementJson`.
- **Fee evidence.** Offer **Confirmar valor da taxa** with required basis/reason and evidence, including an explicit zero. Otherwise show **Taxa não informada**, never R$ 0,00. Unknown fee or an unexplained observed difference prevents statement confirmation. A short receipt shows **Diferença sem conciliação — revise a origem** and never preselects Tarifa cartão. Preserve input after an error; render backend ErrorMessages verbatim.
- **Pending receipt.** After success, display the batch id, provider, all event allocations and receipt id. **Pendente de confirmação** is persistent status, distinct from the gross claim being settled. Open **Conciliar extrato** with that receipt as context; no **Marcar como pago** action. After a match, show **Confirmado no extrato**. Matching changes no original claim or bank-credit count. `[DTO] BankReceiptStatus`, `GET /bank-receipt/{id}`.
- **Cash recolhimento.** The review shows **Cofre de origem**, **Conta bancária**, positive value, configured outgoing/incoming classifications, occurrence/posting dates, recording operator and evidence. Explain: **O recolhimento registra os dois lados da transferência. A chegada ainda precisa ser conferida no extrato.** Submit once; both protected rows and one pending receipt appear together. No cashbox/session count or variance, overdraft policy, generic cancel or finalize shortcut. `[DTO] RequestBankMovementJson` SafeCollection.
- **Whole cheques.** Retain **Levar ao cofre** → **Depositar**, showing custody and the existing deposit receipt. A deposited cheque has no second recolhimento action. A return after a confirmed deposit shows its linked pending debit receipt; a return before confirmation reduces only the original pending receipt. No mixed cash/cheque amount field and no fractional cheque deposit. `[DTO] existing cheque command/read DTOs`.
- **Observed reimbursement or fee.** Separate choices **Ressarcimento de prêmios CAIXA** / **Tarifa bancária** require an explicitly observed fact, value, dates and evidence. The manager selects a configured type with the required direction/class; an unknown reimbursement amount is not saved as a credit. Show the protected bank row and pending external verification. Never copy a drawer prize value into this form. `[DTO] BankMovementKind`.

Use the existing form, selection and review component contracts; monetary fields accept the public BRL input and formatter. All coloured status treatments use semantic feedback pairs; electronic instruments use `color.domain.{theme}.settlement-instrument.Pix` / `.MarketPlace`. On small screens, keep the selected-item review and primary action after the final evidence field in reading order, with the original transaction reachable without discarding the selection. Focus the error summary on failure, preserve every entered value, and announce saved receipt status through a polite live region.


### Later fee evidence for a pending provider batch (v63)

A pending receipt with an unknown fee or unexplained difference offers **Conferir tarifa do lote**. Read the exact batch id, receipt Version and FeeEvidenceVersion from the server; show original gross/receipt amount separately from current ExpectedAmount and the immutable FeeEvidenceHistory. The manager explicitly enters the fee (including zero), basis, evidence, reason, actual evidence date and open posting date; optionally records a newly verified net observation. No amount is precomputed from a short payout. Omitted observation keeps the prior observation. Submit `POST /card-settlement/fee-evidence` with these ids/versions and Idempotency-Key. `[DTO] RequestCardFeeEvidenceJson`, `ResponseBankReceiptJson`, `ResponseCardFeeEvidenceJson`.

Success retains the same receipt and source allocations, appends history, and displays the backend expectation/difference. If the difference is resolved, request fresh matching proposals; no existing proposal is reused after a revision. Conflict preserves input and refreshes authority/versions; confirmed receipts have no fee-edit action. This command never creates another claim payment or expense. Use existing form, timeline, info/error and confirmation patterns; amount/date labels come from shared/core and backend errors remain verbatim. V63 semantics independently accepted 2026-11-05; real app UI acceptance remains pending.

Cash recolhimento uses dedicated **Recolhimento — saída do cofre** and **Recolhimento — entrada no banco** types; drawer→cofre system roles are refused. It accepts an amount, not cheque identities, so the UI must not claim that it verifies exclusion of deposited cheques. Card/cheque confirmation updates the receipt subledger without a bank Transaction; the bank ledger alone is not a complete bank position. Do not offer an unlinked compensating bank entry as reconciliation.

## Frozen result drill-down — v64 candidate

Design 4.9.12, semantics independently accepted 2026-11-08. A financial-result source opens its exact transaction/session/receipt/batch evidence with the originating month/cutoff/page retained. Display frozen FinancialClass and classification provenance from the posting, not the current type editor. A later card fee is its own signed recognition delta with source date and PostedOn; principal/receipt confirmation is never new revenue. A personal responsibility adjustment is informational history with its own id and person effects, not a transaction edit or payroll entry. Retain existing v63 unknown-fee, unmatched, discovery and bank-position limitations; no compensating manual row is suggested. Return rereads the original report scope.
