# Work queue and review — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M7** (the owner setup checklist region ships with **M11**, which owns the account/onboarding set). Semantics: [Manager Work Queue](../../docs/screens.md#manager-work-queue) and [Daily-close approval](../../docs/screens.md#daily-close-approval). Contracts: `GET /report/manager-queue` (M7.5, M7.7 Phases 7.5/7.8), `GET /session/{id}/review` and `/history`, approve/reject/reopen/recall, M8 link-request count and list (all shipped). **Design 4.9.5 revision (2026-10-30, server M9.6 P5 / canonical v60; review pending):** approval by an independent manager, *Aprovar como único gestor* and *Aprovar dia do caixa*, and the Conferir attempts as review evidence. Opening-discrepancy attribution and per-session custody rows remain in the catalog for the app M7 increment.

A manager opens Lotero to act, not to browse. The queue therefore reads top to bottom in the order of urgency the catalog fixed, every group says how many items it holds even when that number is zero, and every row offers exactly one way into the screen that resolves it.

## Manager Work Queue

### Regions, in reading order

1. **PageHeader** — title **Fila de trabalho**; context line: long date of the selected day with **(hoje na filial)** when it is today · **data de bloqueio {dd/MM/yyyy}**; day stepper ([DatePicker](../components/date-picker.md) day-stepper variant: **Dia anterior** · date · **Próximo dia**) aligned with the title on wide layouts, below it on compact. Secondary **Dashboard** link → `/f/{branchId}/relatorios/dashboard` (Web; native branch-scoped destination), fresh current-month overview; retain this queue date/mode as return origin. It does not replace the queue home or inherit its selected date as Dashboard today. This 2026-10-22 report-entry integration awaits acceptance under M4 4.8.4.
2. **Owner setup checklist** *(only while a required item is pending; otherwise one collapsed line)* — [Card](../components/card.md) checklist variant titled **Configuração inicial da lotérica**: one row per item with status text (**Concluído** / **Pendente** / **Não foi possível verificar** + retry), what it unlocks, and its link; the three required items first, then **Conforme o uso** items, then **Equipe** counts. Collapsed form: **Configuração inicial concluída · Ver itens opcionais** with a device-local **Ocultar**.
3. **Earlier-days banner** *(when any backlog count is non-zero)* — warning [Notice](../components/notice.md): **Dias anteriores com fechamentos pendentes** · **{n} enviado · {n} rejeitado · {n} em rascunho — o mais antigo em {data}** · **Ver dia mais antigo**.
4. **Seu turno** *(linked Manager/Admin)* — one line: workstation terminal and today's close status, **Escolher terminal**, or **Sem terminal atribuído** with its link; the link switches to Meu turno.
5. **Groups**, fixed order, each a section with title and count badge (`sem.feedback.neutral.*`): **Pendências de aprovação** · **Rejeitados / precisam de correção** · **Não enviados** · **Exceções de diferença de caixa** (with **Total do dia** and **média** in the section header and the ordering note **Não aprovados primeiro, maior diferença absoluta primeiro**) · **Bloqueios do fechamento mensal — {mês}** (with **Pronto para bloquear** or the blocker count) · **Transações em rascunho** (the unlocked Draft count with **Ver rascunhos**) · **Solicitações de vínculo** (count and the oldest requests with **Resolver em Operadores**).

### Group rows

| Group | Row content | Row destination (whole row + explicit link) |
|---|---|---|
| Pendências de aprovação | Conta · Contado por (user + operator) · Enviado por · Enviado às · Diferença (signed, `dom.cash-variance.*`) | **Conferir** → Daily-close approval |
| Rejeitados | Conta · Contado por · rejected status badge | **Abrir correção** → Fix & resubmit |
| Não enviados | Conta · Operador · Situação (**sem fechamento aberto** or a Draft badge) | **Abrir fechamento** (Draft → Close day) or **Abrir o dia** (missing → Open day) |
| Exceções de diferença | Conta · status badge · Diferença with **Falta/Sobra de R$ …** caption | status-routed (**Conferir** / **Ver**) |
| Bloqueios | blocker sentence composed from the structured fields (copy §5 labels) | status-routed close, Draft-filtered list, or Open day |
| Solicitações de vínculo | person · **solicitado em {data/hora}** | **Resolver** → Operators with that request focused |

**Aprovar dia do caixa** (v60): when one cashbox has two or more Submitted sessions on the selected day, the Pendências group shows them together under the cashbox name with the group action **Aprovar dia do caixa**, which sends each session's id and version to `POST /session/approve-day`. A refusal approves nothing; its Notice lists every returned **Turno {id}: {motivo}** line verbatim and the rows stay pending. The batch never merges sessions or sums their counts.

"Who counted" and "who sent" are separate columns at every width. An empty group shows its zero count and a one-line all-clear (**Nenhum fechamento rejeitado neste dia.**), never disappears, so the order stays learnable.

### Layout

| Width | Arrangement |
|---|---|
| ≥ `breakpoint.wide` | one column of sections; groups as [Tables](../components/table.md) |
| < `breakpoint.wide` | groups become grouped lists: line 1 account + destination link, line 2 people, line 3 time and signed difference; the day stepper wraps under the title |
| < `breakpoint.medium` | the checklist and banner span the width with actions full width; section headers keep their counts beside the title |

### States

| State | Presentation |
|---|---|
| Loading | header immediately; Skeleton for the banner and each group with its title kept |
| All-clear for the day | the groups show zero counts; a neutral [EmptyState](../components/empty-state.md) **Tudo certo — nada pendente de aprovação.** above them; the backlog banner still appears when earlier days are pending; optional recent-close line |
| Partial failure | the queue read failing shows one Notice with retry in place of the queue groups; reconciliation, lock date, link-request list and checklist items fail and retry independently, each saying which part is missing — counts never turn into zero |
| Imported current/future lock date | info Notice routing to Monthly reconciliation (catalog) |
| Updating day | previous groups dimmed with **Atualizando…**; the stepper shows the requested day |

### Focus, origin and return

Route entry focuses **Fila de trabalho**. Every drill-down records the queue with its selected day as origin; returning restores the day, scroll position and the focused row's link. The queue is the Gestão home and the fallback for any source-less management screen.

## Daily-close approval

### Regions, in reading order

1. **PageHeader** — breadcrumb from the origin (**Fila de trabalho › Conferência**); title **Conferência do fechamento**; context: Terminal · long date · status badge; actions on the right (wide) or below (compact): **Aprovar** (primary), **Rejeitar** (danger outline), **Gerenciar fechamento** (secondary, unlocked Submitted only), **Reabrir para correção** (danger outline, Approved before lock).
2. **Approval guard** *(when `AllowedActions.CanApprove` is false and `ApprovalBlockedReason` is set)* — info [Notice](../components/notice.md) with the server's reason verbatim (for example **Quem registrou a contagem deste turno não pode aprová-lo.**); **Aprovar** is not offered. When `CanApproveAsSingleManager` is true, the Notice adds the secondary action **Aprovar como único gestor**.
3. **Identities** — DetailList: Aberto por · Contado por · Enviado por · Enviado em; **Sem operador associado ao registro** where applicable; the recorder's Notes as a quoted block **Observações do operador**.
4. **Comparação** — the review table: Produto · Abertura (**calculado pelo sistema**) · Fechamento (**informado pelo operador**) with the **Diferença Caixa** row marked system-calculated and showing the official persisted value.
5. **Como se chega à diferença** — SummaryStrip (four totals) and the worked equation of the submitted attempt (`Count.SubmittedAttemptId` in `/history` `CountAttempts`), never device arithmetic; a session submitted before v60 shows the official row alone. Then check the table's completeness by identity, never by arithmetic: when an attempt item with a non-null `OpeningValue` names a `ProductId` absent from the comparison rows, the table omits part of the opening (the first-transition retired fee — the attempt and the official value both include it and agree, so their equality cannot reveal it). List each such product with the attempt's `ProductName` and `OpeningValue`. On an omission, or if the attempt's difference ever disagrees with the official value, use the catalog’s **explanation differs from server** state: **Subtotal da abertura exibida**, neutral signed **Diferença oficial** without Falta/Sobra or sign-based variance colors, and persistent warning **A conta exibida não confere com a diferença do servidor. Confira a abertura e o fechamento anterior com a gerência antes de enviar ou aprovar.** Preserve ordinary permissions and approval controls; refresh stale/failed reads with **Não foi possível conferir a diferença**. Clear the warning only when no attempt opening product is missing from the rows and the values agree. Collapsed **Ajuda sobre diferença de caixa** follows copy §7.1 for that same state.
6. **Transações do dia** — the `(account, date)` ledger with **Automática** markers.
7. **Conferências** — every count attempt, oldest first ([Table](../components/table.md); per-attempt rows on compact): Nº · tipo (copy §5 `SessionCountAttemptKind`) · conferido por · quando · fechamento · esperado · diferença, the submitted attempt marked **Enviada**. Several attempts are evidence, never an alert by themselves.
8. **History** *(when events exist)* — the dated review events (same treatment as Fix & resubmit, read-only).

### Historical opening on the first upgraded close (canonical v55)

The comparison's opening rows may omit a retired fee saved only on the prior counted close, while the unchanged calculator includes that saved amount. Apply the catalog's [transition procedure](../../docs/screens.md#daily-close-approval): label an incomplete visible sum **Subtotal da abertura exibida**, preserve the server's official figure and any mismatch, and compare it with the inventory output retained with the deployment review, prior close and actual ledger. A negative official number alone does not prove a physical shortage on this transition. The review DTO supplies no completeness flag; since v60 the submitted attempt's items name every opening product with its server value, so an attempt opening product absent from the rows identifies the omission (item 5) without client arithmetic. A session submitted before v60 has no attempt and no on-screen detector, and the historical cause is never inferred. No inferred zero or fabricated explanation is specified. The recorder's Notes must hold the reviewed explanation before submission; do not add an edit action to an already Submitted close. If the evidence is missing or the difference unexplained, stop approval and use the existing management/rejection path as permitted. AA/transition recovery remains with the server owner.

### Actions and dialogs

- **Rejeitar** opens a [dialog](../components/modal-sheet.md): **Motivo** textarea (required; helper **O operador verá este motivo**), **Rejeitar fechamento** (solid danger) and **Voltar**; an empty reason surfaces the verbatim 400 inline.
- **Aprovar** runs directly (no confirmation; it is the expected action) with **Aprovando…**, sending the review's version as `If-Match` and one Idempotency-Key per decision, reused on retry; success returns to the origin with the pending count decremented and a Toast **Fechamento aprovado**.
- **Aprovar como único gestor** opens a dialog: **Motivo** (required; helper **Fica registrado no histórico do turno**), **Aprovar como único gestor** (primary) and **Voltar**; an empty reason or a branch that now has another active manager renders the verbatim server message inline.
- **Reabrir para correção** asks for confirmation stating that the close returns to Rascunho and needs a new submission and approval.

### Layout

| Width | Arrangement |
|---|---|
| ≥ `breakpoint.wide` | header with actions in one row; the comparison table full width; transactions table below |
| < `breakpoint.wide` | actions wrap below the context line, primary first |
| < `breakpoint.medium` | the comparison becomes per-product rows: product name, then **Abertura / Fechamento** as two labelled values, the variance row last and visually distinct; actions in a sticky bottom bar that never covers a focused field |

### States

Loading (Skeleton table with header kept); not found/cross-branch (EmptyState **Fechamento não encontrado** + **Voltar para a fila**); already finalized (read-only with the outcome badge, only **Voltar** and, when permitted, **Reabrir para correção**); approval blocked by duties (guard Notice; Rejeitar unchanged; single-manager action only when offered); lock-blocked (actions absent; reason **Período bloqueado** in the header context); error (verbatim Notice); action in flight (every mutation button disabled against duplicate activation; the running one keeps its progress label); uncertain (warning Notice with **Recarregar e conferir** — approval state is re-read, never assumed).

### Origin and return

From Work Queue, a Cash-variance summary row, a Monthly reconciliation blocker, Dashboard recent/variance previews or a Monthly financial report Terminal/day close link. Return after review/approve/reject to that actual validated origin, with its month/window, filters, section/row and scroll/focus retained (Work Queue when none). **Gerenciar fechamento** → Close day in Gestão and back here with the complete parent chain kept, including a Dashboard or Monthly financial report origin. Only Submitted is approvable; Approved remains review-only.

## Verification boundary

Static: order, row content, states and returns. Real-UI (Web/Mobile M7): queue against a local API with each group populated and empty, the backlog jump, row link names in a screen reader, approval keyboard flow including the reject dialog's focus return, and 375px with enlarged text.
