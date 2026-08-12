# Lotero — Copy Guidelines (pt-BR)

> **Status:** Draft (Design M0, Phase 2 — item 0.2.2)
> **What this is:** the copy contract for web and mobile — the rules behind every word the interface shows. It sets the tone and casing of the UI's own text, fixes a glossary so every screen calls each domain concept by the same name, and gives each backend enum value its pt-BR display label: the API sends raw values like `Submitted`, the user sees "Enviado", and the tables in §5 are the dictionary in between — later coded as TypeScript maps in the `shared/core` package (Shared M3). Finally, it pins down how API errors reach the screen: the backend already writes them in pt-BR, so the UI renders them exactly as received (§6). M1's generator-tool prompts **cite** this file so prototypes carry real product language.
> **Language:** all user-facing copy is **pt-BR**. Doc prose (this file) is English; every quoted string is the literal copy.

## 1. Voice & tone

- **Calm, direct, professional.** Operators are standing at a counter with a customer waiting; managers are scanning for exceptions. Short sentences, no exclamation marks, no filler ("Por favor," "Ops!").
- **Never blame the user.** State what happened and what to do next: "Não foi possível salvar a transação", not "Você preencheu errado".
- **Implicit você.** Address the user with imperative verbs; avoid "você" spelled out except where omission reads oddly ("Solicite ao gerente").
- **Numbers, dates, and money** always follow [`formatting-ptbr.md`](formatting-ptbr.md) — copy never inlines a differently-formatted value.

## 2. Casing

- **Sentence case everywhere** — titles, buttons, labels, tabs, empty states: "Fechamento do dia", "Enviar para aprovação". Never Title Case.
- Proper nouns and acronyms keep their casing: PIX, CEF, CPF, CNPJ, CEP, Telesena, BrasilAPI.
- **Tenant data renders verbatim** — category, transaction-type, product, account, and client names come from the branch's own rows (e.g. the seeded product "Diferença Caixa", the type "Pgto Prêmio") and are never recased, translated, or "corrected" by the UI.

## 3. Actions & buttons

- Imperative infinitive, one verb: "Salvar", "Aprovar", "Rejeitar", "Enviar", "Cancelar", "Tentar novamente".
- When the same screen has near-neighbors, name the object: "Cancelar transação" vs the dialog-dismiss "Voltar". Never a bare "OK"/"Sim" as the confirming action of a destructive dialog.
- Destructive or irreversible confirmations state the consequence in the body: "A transação será cancelada e deixará de contar nos totais. Essa ação não pode ser desfeita."
- **Reason-required flows** (reject a close — `RequestRejectDailyCloseJson`; cancel a transaction — `RequestCancelTransactionJson`): the reason field is labeled "Motivo" and required; the helper copy says who reads it — "O operador verá este motivo" / "O motivo fica no histórico da transação".
- Drafts: "Salvar como rascunho" (create), "Finalizar rascunho" (promote Draft → Active).
- Daily-close undo actions stay distinct: **"Desfazer envio"** is the operator-facing label for explicit `Recall` (`Submitted → Draft`); **"Reabrir fechamento"** is the Manager/Admin correction action for `Reopen` (`Approved → Draft`). Never translate Recall as "Recolher"/"Recolhido", and do not call either action "Cancelar".

## 4. Terminology glossary

Canonical UI terms, mapped from the domain spec and the backend's own pt-BR error strings (`ResourcesErrorMessages.resx`) — the UI must use the same words the backend errors use, since those render verbatim next to our copy. When the two diverge, the fix goes to the backend string, not to this rule: "conta Tab" (the C# entity name leaking into user copy) was corrected to "conta fiado" in the resx for exactly this reason, keeping one voice on screen.

| UI term (pt-BR)                                            | Concept                                                                                                                    | Source                                    |
|------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------|-------------------------------------------|
| transação                                                  | `Transaction` — a ledger row. **Not** "lançamento" (Access-era term, retired)                                              | §3.12; backend errors say "transação"     |
| fiado                                                      | the credit/tab subsystem; "conta fiado" for a Tab account; "saldo fiado" for the outstanding balance                       | §1, §6.4                                  |
| diferença de caixa                                         | the CashVariance concept in prose; the product row named "Diferença Caixa" renders verbatim as data                        | §6.12                                     |
| falta / sobra                                              | negative / positive cash variance (drawer short / over)                                                                    | §6.12 v32                                 |
| fechamento do dia                                          | `DailyClose` — the daily closing flow; "abrir o dia" / "abertura do dia" for opening                                       | §6.5                                      |
| conferência                                                | the manager's review of a submitted close                                                                                  | §6.5, Access `FrmCaixa`                   |
| cheque pré-datado                                          | pre-dated cheque; its rows are "parcelas" (installments)                                                                   | §6.3                                      |
| parcela                                                    | one installment row; positions render `3/12`                                                                               | §6.3                                      |
| bolão                                                      | group lottery bet; "sobra de bolão", "tarifa bolão" are tenant data, verbatim                                              | §5 seeds                                  |
| borderô                                                    | CEF's settlement report (monthly comparison is manual for MVP)                                                             | §7.2                                      |
| filial                                                     | `Branch` — the tenant                                                                                                      | §3.2; backend errors say "filial"         |
| operador                                                   | `Operator` — the employee entity                                                                                           | §3.6                                      |
| caixa                                                      | the drawer/register context ("fechar o caixa" acceptable in informal helper text; the flow name stays "fechamento do dia") | §1                                        |
| terminal                                                   | a `Terminal` account (operator's drawer)                                                                                   | §3.7                                      |
| cliente                                                    | `Client`                                                                                                                   | §3.11                                     |
| data da transação / data de vencimento / data de pagamento | the three-date model: `Date` / `DueDate` / `PaidAt`                                                                        | §1; backend errors use these exact labels |
| em aberto                                                  | unpaid (`PaidAt = null`) — "parcelas em aberto", "fiado em aberto"                                                         | §6.14                                     |
| rascunho                                                   | `Draft` status (transaction or close) — excluded from totals                                                               | §6.1                                      |
| desfazer envio                                             | explicit DailyClose `Recall`: return an Enviado close to Rascunho; success copy "Envio desfeito"                           | §6.13                                     |
| data de bloqueio / período bloqueado                       | `LockDate` and its effect — "A data da transação está bloqueada pelo fechamento" is the backend's own wording              | §6.6                                      |
| banco de horas                                             | hour-balance system; "folga" (DayOff, hours owed), "abonado" (excused — no hours owed)                                     | §6.7, `TimeEntryStatus`                   |
| ponto                                                      | clock in/out context — "bater o ponto", "registro de ponto"                                                                | §6.7                                      |

## 5. Enum label maps (pt-BR)

The contract `shared/core` (Shared M3) implements — one map per backend enum, keys identical to the enum values. Labels are sentence-case; they appear in badges, filters, and detail rows.

**Role** *(§4)*: `Admin` → "Administrador" · `Manager` → "Gerente" · `Member` → "Membro". *(A Member is the login role; the linked employee is the "operador" — don't conflate them in copy.)*

**Direction** *(§4)*: `In` → "Entrada" · `Out` → "Saída".

**AccountType** *(§4)*: `Terminal` → "Terminal" · `BankAccount` → "Conta bancária" · `Tab` → "Conta fiado".

**TransactionStatus** *(§4)*: `Draft` → "Rascunho" · `Active` → "Ativa" · `Cancelled` → "Cancelada". *(Feminine — agrees with "transação".)*

**DailyCloseStatus** *(§4)*: `Draft` → "Rascunho" · `Submitted` → "Enviado" · `Approved` → "Aprovado" · `Rejected` → "Rejeitado". *(Masculine — agrees with "fechamento". Status hint text may expand: Submitted → "aguardando aprovação".)*

**AgingBucket** *(§6.14)*: `Current` → "Em dia" *(includes future-due rows)* · `Days0To30` → "0–30 dias" · `Days31To60` → "31–60 dias" · `Days61To90` → "61–90 dias" · `Days91Plus` → "Mais de 90 dias". *(En dash in ranges, no spaces.)*

**TimeEntryStatus** *(§4)*: `Present` → "Presente" · `DayOff` → "Folga" · `Sunday` → "Domingo" · `Holiday` → "Feriado" · `Vacation` → "Férias" · `JustifiedAbsence` → "Falta justificada" · `UnjustifiedAbsence` → "Falta injustificada".

**SettlementRule** *(§4, admin screens)*: `SameDay` → "Mesmo dia" · `NextCalendarDay` → "Dia seguinte" · `NextBusinessDay` → "Próximo dia útil" · `TwoBusinessDays` → "2 dias úteis" · `OperatorEnteredCheque` → "Cheque pré-datado (data informada)".

**HolidaySource** *(§3.17, admin screens)*: `Manual` → "Manual" · `Canonical` → "Calendário nacional" · `BrasilApi` → "BrasilAPI" · `Nager` → "Nager.Date".

**BrazilianHolidayCalendarSource** *(§5.1, the import-source picker — distinct from `HolidaySource`: it adds the composite option)*: `Composite` → "Todas as fontes" · `Canonical` → "Calendário nacional" · `BrasilApi` → "BrasilAPI" · `Nager` → "Nager.Date".

**BrazilianHolidayType** *(§5.1, holiday import preview)*: `National` → "Nacional" · `OptionalFederal` → "Ponto facultativo".

**BrazilianHolidayImportStatus** *(§5.1, holiday import result)*: `Imported` → "Importado" · `Skipped` → "Ignorado" *(the holiday already existed for that date)*.

**TimeEntryTapAction** *(§6.7 — request-only: the tap the client sends, never shown in a response; the labels double as the clock-screen buttons)*: `Open` → "Entrada" · `Close` → "Saída". *(The standard ponto terms — same words as `Direction`, but the contexts never mix.)*

**MonthlyReconciliationBlockerType** *(§6.14)*: `UnapprovedClose` → "Fechamento não aprovado" · `DraftTransactions` → "Transações em rascunho". *(Blockers arrive structured; the client formats the sentence around these labels.)*

## 6. Error display

The backend is the single source of error-message truth. Every API error is `ResponseErrorJson { ErrorMessages: string[] }`, already written in pt-BR (198 messages in `ResourcesErrorMessages.resx`).

- **Render `ErrorMessages` verbatim.** Never rewrite, translate, truncate, prefix ("Erro:"), merge, or dedupe them. Multiple messages render as a list, in the order received.
- **Client-side pre-validation** (before submit) reuses the backend's exact wording wherever a backend message exists for the rule — "O valor da transação deve ser maior que zero" — so the user never sees two voices for the same rule. Only rules with no backend counterpart get new copy, written in the same style.
- **No `ResponseErrorJson` body** (network failure, timeout, unexpected 5xx): standard fallback — "Não foi possível concluir a ação. Verifique sua conexão e tente novamente."
- **Session expiry** (401 after refresh fails): "Sua sessão expirou. Entre novamente."
- **Permission walls** (403 with a body) render the backend message verbatim like any other; screens with a known fallback state (e.g. Member without linked operator) show their designed guidance instead of a raw banner — per `screens.md`.

## 7. States & feedback microcopy

- **Empty states:** one statement + the next action. "Nenhuma transação hoje." + button "Nova transação". "Tudo certo — nada pendente de aprovação." for the work queue's all-clear.
- **Loading:** prefer skeletons (M3) over text; where text is unavoidable, "Carregando…" (with ellipsis character).
- **Success confirmations:** short, past participle, name the object: "Transação salva", "Fechamento enviado", "Envio desfeito", "Fechamento reaberto", "Fechamento aprovado". No exclamation marks.
- **Pending/waiting:** "Aguardando aprovação do gerente" (submitted close), "Rascunho — não conta nos totais" (draft rows).
- **System-calculated values** are labeled as such where the distinction matters (§6.12 / review screen): "calculado pelo sistema" vs "informado pelo operador".

## 8. Do / don't

| Do                                                                                     | Don't                                               |
|----------------------------------------------------------------------------------------|-----------------------------------------------------|
| "Enviar para aprovação"                                                                | "ENVIAR PARA APROVAÇÃO" / "Enviar Para Aprovação"   |
| "Não foi possível salvar a transação"                                                  | "Ops! Algo deu errado :("                           |
| "Rejeitar fechamento" + motivo obrigatório                                             | "Rejeitar" with no reason field                     |
| "Desfazer envio" (`Submitted → Draft`)                                              | "Recolher" / "Cancelar fechamento"                 |
| "Falta de R$ 20,00 no caixa" *(−R$ 20,00)*                                             | "Diferença: -20"                                    |
| Render "Este tipo de transação exige uma conta fiado e um cliente informado" as received | Rewriting backend errors "to sound friendlier"    |
| "Fechamento enviado"                                                                   | "Sucesso!!! Seu fechamento foi enviado com sucesso" |
