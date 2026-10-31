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
- Navigation actions name their object: "Abrir transação" (a transaction row), "Abrir plano completo" / "Abrir plano completo para edição" (an installment's plan), "Abrir origem fiado" (a generated movement's source), "Ver fechamento de hoje", "Gerenciar fechamento" (approval → Close day), "Ver o que mudou" (opening-change detail), "Abrir o dia" (open a terminal's day from a blocked operation).
- Single-step undo of a copied value: **"Desfazer repetição"** (after "Repetir abertura") — a row action, never a confirmation dialog.
- A whole Draft cheque plan activates with **"Finalizar plano"**; "Finalizar rascunho" stays the single-transaction action.
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
| em aberto                                                  | unpaid (`PaidAt = null`) — "parcelas em aberto", "fiado em aberto"; for a client's fiado balance, a positive balance        | §6.14                                     |
| quitado                                                    | a client's fiado balance at exactly zero                                                                                   | `screens.md` Client statement             |
| crédito do cliente                                         | a negative internal fiado balance (`Out − In < 0`), always shown as a positive amount — "+R$ 10,00 · crédito do cliente"; the credit tab of Fiado balance is "Saldo crédito", its total "Crédito dos clientes" | `screens.md` Client statement, Fiado balance |
| automática                                                 | a Terminal movement the system generated from a fiado operation; the label "Automática" sits beside the type and is never a status | `screens.md` Transactions list            |
| observação da gerência                                     | the reviewer's rejection reason as shown back to the operator (the reject form still labels the field "Motivo")            | `screens.md` Fix & resubmit               |
| rascunho                                                   | `Draft` status (transaction or close) — excluded from totals                                                               | §6.1                                      |
| desfazer envio                                             | explicit DailyClose `Recall`: return an Enviado close to Rascunho; success copy "Envio desfeito"                           | §6.13                                     |
| data de bloqueio / período bloqueado                       | `LockDate` and its effect — "A data da transação está bloqueada pelo fechamento" is the backend's own wording              | §6.6                                      |
| banco de horas                                             | hour-balance system; "folga" (DayOff, hours owed), "abonado" (excused — no hours owed)                                     | §6.7, `TimeEntryStatus`                   |
| ponto                                                      | clock in/out context — "bater o ponto", "registro de ponto"                                                                | §6.7                                      |
| Gestão                                                     | management navigation context — Manager/Admin home (Manager Work Queue); a mode, not a role. **Not** "modo gerente"        | `screens.md` IA map (design 0.4.1)        |
| Meu turno                                                  | shift navigation context — Operator Day Cockpit; the Member home and a linked Manager/Admin's switchable mode; not a role  | `screens.md` IA map (design 0.4.1)        |
| convite                                                    | `Invitation` — how every person joins a lotérica; "aceitar", "recusar", "reenviar"; never "cadastro"                       | §6.16                                     |
| vínculo / solicitação de vínculo                           | the login ↔ operator link, and the Member's `OperatorLinkRequest`                                                        | §6.16                                     |
| Plataforma / operador da plataforma                        | the Lotero team's onboarding area and its read-only account flag; never a branch role                                      | §6.17                                     |
| Minha conta / Sair                                          | Profile and logout in the Session menu; "Sair" ends this device's session only                                            | `screens.md` Session menu                 |

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

**MonthlyReconciliationBlockerType** *(§6.14)*: `UnapprovedClose` → "Fechamento não aprovado" · `DraftTransactions` → "Transações em rascunho" · `MissingExpectedClose` → "Fechamento ausente" *(direct Terminal activity on a day with no close)*. *(Blockers arrive structured; the client formats the sentence around these labels.)*

**InstallmentPlanStatus** *(§4, Installment plan existing-plan mode)*: `Draft` → "Rascunho" · `Active` → "Ativo" · `Mixed` → "Misto" *(hint: "parcelas em rascunho e em outras situações")* · `Cancelled` → "Cancelado". *(Masculine — agrees with "plano".)*

**DailyCloseReviewEventType** *(§3.21, Fix & resubmit)*: `Rejected` → "Rejeitado na conferência" · `OpeningChanged` → "Abertura alterada".

**TransactionGenerationKind** *(§6.12, generated movements)*: the marker is always "Automática"; the source phrase is `FiadoCountedStockSale` → "venda fiado com entrega de mercadoria" · `FiadoCashRepayment` → "recebimento de fiado em dinheiro".

**FiadoPaymentKind** *(§6.4, admin types and Receber pagamento)*: `CountedCash` → "Dinheiro (entra no caixa)" · `NonCash` → "Outro meio (não entra no caixa)".

**TransactionTypeSystemRole** *(admin types, read-only)*: `FiadoCountedStockSaleCounterpart` → "Saída automática de mercadoria (venda fiado)" · `FiadoCashRepaymentCounterpart` → "Entrada automática de dinheiro (recebimento de fiado)".

**InvitationStatus** *(§6.16)*: `Pending` → "Pendente" · `Accepted` → "Aceito" · `Declined` → "Recusado" · `Expired` → "Expirado". *(Masculine — agrees with "convite".)*

**MailDeliveryStatus** *(§6.16 — SMTP acceptance, never inbox arrival)*: `Pending` → "Envio pendente" · `Sent` → "Enviado ao servidor de e-mail" · `Failed` → "Falha no envio".

**SettlementInstrument** *(§3.10, admin types, Upcoming settlements and the Dashboard)*: `None` → "Sem classificação" · `Cheque` → "Cheque" · `CreditCard` → "Cartão de crédito" · `DebitCard` → "Cartão de débito". *(A receivable class set by the manager on the type; never inferred from the type's name.)*

**MonthlyFinancialRole** *(§3.10, admin types and the Monthly financial report)*: `None` → "Nenhuma" · `BolaoServiceFee` → "Tarifa de bolão" · `CommercialLeftover` → "Sobra (perda comercial)". *(Feminine — agrees with "classificação".)*

**OverviewSectionStatus** *(§6.14, Dashboard sections)*: `Ready` → "Atualizado" · `Empty` → "Nada no período" · `NotConfigured` → "Não configurado". *(A section's state, never a figure: `NotConfigured` is never rendered as zero — see §7.5.)*

**OperatorLinkRequestStatus** *(§6.16)*: `Pending` → "Pendente" · `Resolved` → "Resolvida". *(Feminine — agrees with "solicitação".)*

**SessionCustodyState** *(§3.30/§6.20, custody of a turno — never its review status)*: `Active` → "Em custódia" · `HandedOver` → "Passado adiante" · `Finished` → "Encerrado". *(Masculine — agrees with "turno". Shown beside, never instead of, `DailyCloseStatus`.)*

**SessionMappingState** *(§3.30/§6.22, legacy history)*: `Ready` → "Resolvido" · `Unresolved` → "Histórico não resolvido". *(An Unresolved turno is read-only history: never offer an action on it.)*

**SessionOrigin** *(§6.20, how the turno began)*: `Started` → "Iniciado no caixa" · `HandoverAccepted` → "Recebido em passagem" · `ManagerException` → "Iniciado pelo gerente (exceção)" · `ManagerDeclared` → "Declarado pelo gerente" · `Migrated` → "Migrado do sistema anterior".

**SessionOpeningSourceKind** *(§6.20, where the proposed opening came from)*: `ExplicitZero` → "Abertura zerada confirmada" · `OpeningCount` → "Saldo de implantação" · `PreviousSession` → "Turno anterior" · `ImportedSession` → "Turno importado". *(A missing source is shown as missing, never as a zero.)*

**SessionHandoverState** *(§6.20, passagem de caixa)*: `Offered` → "Oferecida" · `Accepted` → "Aceita" · `Disputed` → "Contestada" · `Withdrawn` → "Retirada". *(Feminine — agrees with "passagem".)*

**OpeningDiscrepancyAttribution** *(§6.20, diferença de abertura)*: `Unattributed` → "Não atribuída" · `Outgoing` → "Operador que entregou" · `Incoming` → "Operador que recebeu" · `Neither` → "Nenhum dos dois". *(Only an explicit Outgoing/Incoming attribution of the current revision counts toward a person; "Não atribuída" and "Nenhum dos dois" never do.)*

**CustodyExceptionAction** *(§6.20, manager custody exception)*: `Finish` → "Encerrar o turno" · `AcceptAndStart` → "Passar o caixa ao operador".

**SessionEventKind** *(§6.20, custody history)*: `SessionStarted` → "Turno iniciado" · `OpeningAcknowledged` → "Abertura conferida" · `OpeningDiscrepancyOpened` → "Diferença de abertura registrada" · `OpeningDiscrepancyRevised` → "Diferença de abertura revisada" · `HandoverOffered` → "Passagem oferecida" · `HandoverAccepted` → "Passagem aceita" · `HandoverDisputed` → "Passagem contestada" · `HandoverWithdrawn` → "Passagem retirada" · `SessionFinished` → "Turno encerrado" · `CustodyExceptionResolved` → "Exceção de custódia resolvida" · `SessionDeclared` → "Turno declarado" · `SessionRearranged` → "Turno reposicionado" · `AttributionRecorded` → "Diferença atribuída" · `CountConferred` → "Contagem conferida" · `RecountStarted` → "Recontagem iniciada" · `SessionApproved` → "Turno aprovado".

**SessionCandidateState** *(§6.20, blind count; server-owned)*: `NotConferred` → "Não conferida" · `Current` → "Conferida" · `Outdated` → "Desatualizada" · `Unconfirmed` → "Não confirmada" · `Recounting` → "Em recontagem". *(Feminine — agrees with "contagem". `Unconfirmed` labels the recalculation shown after a ledger change; it is never a confirmed difference.)*

**SessionCountAttemptKind** *(§6.20, count attempts in history)*: `Initial` → "Primeira conferência" · `Correction` → "Após correção" · `Recount` → "Recontagem" · `StillObserved` → "Contagem mantida após mudança no caixa".

**SessionApprovalMode** *(§6.20, approval)*: `Independent` → "Aprovação independente" · `SingleActiveManager` → "Aprovar como único gestor".

## 6. Error display

The backend is the single source of error-message truth. Every API error is `ResponseErrorJson { ErrorMessages: string[] }`, already written in pt-BR (`ResourcesErrorMessages.resx`); the unexpected-error handler also returns this shape for HTTP 500.

- **Render `ErrorMessages` verbatim.** Never rewrite, translate, truncate, prefix ("Erro:"), merge, or dedupe them. Multiple messages render as a list, in the order received.
- **Client-side pre-validation** (before submit) reuses the backend's exact wording wherever a backend message exists for the rule — "O valor da transação deve ser maior que zero" — so the user never sees two voices for the same rule. Only rules with no backend counterpart get new copy, written in the same style.
- **No usable `ResponseErrorJson` body** (e.g. network failure, timeout or a gateway response without that shape): standard fallback — "Não foi possível concluir a ação. Verifique sua conexão e tente novamente."
- **Unknown write outcome:** every HTTP 5xx after a write was sent (including gateway 502/503/504), timeout or lost response reads "Resultado não confirmado" with "Não foi possível confirmar se a ação foi concluída." A 500 can carry `ErrorMessages` ("Erro desconhecido" outside Development); render those strings verbatim **as well as** the confirmation warning. An error body does not prove rollback. Keep the same financial operation/key/payload for safe verification; never turn the fallback’s "tente novamente" into a new create.
- **Renewal failure** (definitive 401 from renew-token): "Não foi possível renovar sua sessão. Entre novamente." One active refresh token per user means another login or a lost successful rotation can cause this; do not assert expiry or a particular device as the cause. Other returned messages stay verbatim. Trigger/single-flight/bounded-retry rules live in `screens.md`.
- **Permission walls** (403 with a body) render the backend message verbatim like any other; screens with a known fallback state (e.g. Member without linked operator) show their designed guidance **alongside the verbatim backend message**, integrated into that state rather than a duplicate generic banner — per `screens.md`. No 403 starts a renewal loop.

## 7. States & feedback microcopy

- **Empty states:** one statement + the next action. "Nenhuma transação hoje." + button "Nova transação". "Tudo certo — nada pendente de aprovação." for the work queue's all-clear.
- **Loading:** prefer skeletons (M3) over text; where text is unavoidable, "Carregando…" (with ellipsis character).
- **Success confirmations:** short, past participle, name the object: "Transação salva", "Fechamento enviado", "Envio desfeito", "Fechamento reaberto", "Fechamento aprovado". No exclamation marks.
- **Pending/waiting:** "Aguardando aprovação do gerente" (submitted close), "Rascunho — não conta nos totais" (draft rows).
- **System-calculated values** are labeled as such where the distinction matters (§6.12 / review screen): "calculado pelo sistema" vs "informado pelo operador".
- **Counts agree in number:** "1 movimentação em rascunho", "2 movimentações em rascunho" — never "1 movimentações" or "movimentação(ões)".
- **Save button:** "Salvar" (faded and disabled when nothing changed since the last server save), "Salvando…" during the request.
- **Own row:** a small "você" badge beside the name on the caller's own row (Operators, Branch members) — a marker, not an action.

### 7.1 Cash-variance explanation

- **Blind count (v60):** before **Conferir** the close form shows no expected closing, difference or total that would compose them; the opening stays visible. The former live label "Prévia da diferença · ainda não enviada" is retired with the preview.
- Count actions and states: button **Conferir**; **Recontar** ("Recontar" starts a new count; identical values still form a new conference); the confirmed result is titled **Resultado da conferência**; after a count edit: **A contagem mudou depois da conferência. Confira novamente.**; after a ledger or opening change: the original result as past evidence plus **Recalculado — não confirmado** (never presented as a confirmed difference) and the checkbox **Confirmo que esta contagem ainda representa o caixa**; while recounting: **Recontagem em andamento — o resultado aparece depois de conferir.** Candidate-state labels follow §5 `SessionCandidateState`.
- Approval: **Aprovar**, **Aprovar como único gestor** (reason required: "Informe o motivo"), **Aprovar dia do caixa**; a refused approval renders the server reason verbatim; a refused cashbox day renders each "Turno {id}: {motivo}" line verbatim and states that nothing was approved.
- The main area shows the worked equation of the server attempt (never device arithmetic) with real values, e.g. "R$ 2.437,00 − R$ 2.199,00 − (R$ 400,00 − R$ 150,00) = −R$ 12,00". When the displayed opening omits a product named with a non-null opening in the server attempt's items, or the attempt equation differs from the official value for matching inputs and complete ledger reads (the catalog’s **explanation differs from server** state), label the opening sum "Subtotal da abertura exibida" and keep the authoritative signed figure separate under "Diferença calculada pelo servidor" / "Diferença oficial", in neutral text without Falta/Sobra wording or sign-based shortage/surplus colors; never present the equation as proof of the server result. The catalog's M9.5 transition procedure owns this limitation, with no invented completeness flag; the omitted product and its value come only from the attempt's items.
- Collapsed help, titled "Ajuda sobre diferença de caixa", shown only when expanded:
  > **Como a diferença é calculada**
  >
  > Diferença Caixa = total do fechamento − total da abertura − (entradas − saídas)
  >
  > Valor negativo indica falta; positivo indica sobra.
  >
  > A abertura e o fechamento somam todos os produtos, sem a própria Diferença Caixa. Entram apenas as transações ativas deste terminal nesta data; rascunhos e canceladas ficam fora.

- In the **explanation differs from server** state, replace the simple sign sentence above and show this same warning persistently outside collapsed help: "A conta exibida não confere com a diferença do servidor. Confira a abertura e o fechamento anterior com a gerência antes de enviar ou aprovar." The product-identity check or the numerical comparison triggers the state; neither explains the historical cause. Preserve input and ordinary controls, refresh stale data, and clear the warning only after matching values agree. No new DTO flag, acknowledgement or forced lock is implied.
- First post-upgrade close: a prior-only retired fee may affect the opening without appearing in the current rows. Its negative result alone does not establish a physical shortage; use the catalog's prior-close/inventory review procedure. Do not promise migration has reconciled that transition. Actual fee/Operador amounts are ordinary configured Terminal In transactions; no calculated fee, mandatory daily amount or hard-coded type identity is implied.

### 7.2 Close review events and recorder identity

- Group heading: "Por que este fechamento precisa de revisão?"
- Events: "Rejeitado na conferência" with "Rejeitado em {data/hora}" and the reason under "Observação da gerência"; "Abertura alterada" with "Alterada em {data/hora}" and "Esta alteração não é uma nova rejeição. O fechamento anterior mudou a abertura deste dia."
- Source/target phrasing: "Fechamento de 19/09 corrigido → abertura de 20/09 alterada". Business dates and action times carry distinct labels: "fechamento de 20/09" vs "corrigido em 22/09 às 08:00".
- Before/after columns: "Antes", "Depois", "Alteração".
- Next action: "Revise a observação da gerência, confira a abertura alterada e reenvie a contagem."
- Honest fallbacks: "Data da rejeição indisponível", "Detalhes da alteração indisponíveis para este fechamento" — never an invented date or value.
- Management recording without an operator: "Sem operador associado ao registro".

### 7.3 Terminal blockers, fiado, and generated movements

- Another operator's drafts block a Member's close: title "Pendência do terminal", body "O fechamento está bloqueado por 1 rascunho de outro operador" (plural agreement as above), action "Ver pendência" / "Ver pendências"; mixed case: "Resolver meus rascunhos" and "Ver pendências de outros operadores".
- Terminal day not open (fiado sale or cash repayment): "O dia do {terminal} ainda não foi aberto. Abrir o dia para registrar esta venda?" — "…para registrar este pagamento?" on Receber pagamento — with the action "Abrir o dia". Always name the terminal.
- Receber pagamento without a method: "Escolha a forma de pagamento."
- Generated movement detail: "Gerada automaticamente ao registrar um recebimento de fiado. Operação registrada por {nome}." (a counted-stock sale: "…ao registrar uma venda fiado…").
- The linked stock saída is described as stock leaving custody, explicitly not a cash withdrawal.
- My transaction summary's automatic share: "7 transações, 2 delas automáticas", with the automatic entradas and saídas beside the totals.
- Client balance: "Data da venda mais antiga em aberto" for the oldest unpaid sale; credit explained in words ("o cliente pagou R$ 10,00 a mais"), never as an equation.

### 7.4 Account, invitation and access

- Login: "Entrar", "Continuar com Google", "Esqueci minha senha", and the static hint "Primeiro acesso? Abra o link do convite recebido por e-mail." Never "Criar conta" or "Cadastre-se" on Login — there is no public sign-up.
- Google link step: heading "Confirme sua senha do Lotero", action "Confirmar senha e vincular".
- Rate limited (429 `SECURITY_RATE_LIMITED` on Login, its Google link step and capability screens): the returned message verbatim and nothing else — no countdown from `Retry-After`, no "conta bloqueada", no hint that the password was wrong or the account exists.
- Forgot password confirmation (identical for every outcome): "Se houver uma conta ativa com este e-mail, enviaremos um link para definir uma nova senha. O link vale por tempo limitado."
- Reset password: title "Definir nova senha", success "Senha definida. Entre com a nova senha.", missing token "Abra novamente o link recebido por e-mail.", unusable link action "Peça um novo link".
- Change password: consequence "Ao alterar a senha, todas as sessões desta conta são encerradas, inclusive neste dispositivo."; success on Login "Senha alterada. Entre novamente com a nova senha."; repeat mismatch "As senhas não conferem."
- Profile: "Minha conta", "Nome atualizado", "Senha definida", "Sem senha — esta conta entra com Google", "Receber link para criar senha", marker "Operador da plataforma".
- Invitation: "Você foi convidado para {lotérica} como {função}.", "Válido até {data/hora}", "Você será vinculado ao operador {nome}.", "Use o e-mail que recebeu este convite.", actions "Criar conta com senha", "Continuar com Google", "Já tenho conta — entrar e aceitar", "Aceitar convite", "Recusar convite" (confirmation body "O convite deixará de valer."), unusable "Peça um novo convite a quem administra a lotérica."
- Session menu: "Sair"; with device-kept input: "Há alterações guardadas neste dispositivo. Elas continuam guardadas e voltam quando você entrar de novo com este e-mail."; unconfirmed: "Você saiu deste dispositivo. Não foi possível confirmar o encerramento da sessão no servidor."
- Branch picker empty: "Você ainda não tem acesso a uma lotérica."
- Delivery status is never "entregue" or "recebido": "Enviado ao servidor de e-mail" / "Falha no envio"; a failed delivery offers "Reenviar convite".
- Guided employee journeys: "Adicionar funcionário", "Desligar", "Concluir depois", remaining items "Sem terminal atribuído", "Convite pendente até {data/hora}", "Vincular login depois", "Envio do convite falhou"; removal confirmation "A pessoa deixará de acessar esta lotérica. O histórico registrado continua disponível."
- Owner setup checklist: item states "Concluído", "Pendente", "Não foi possível verificar"; collapsed line "Configuração inicial concluída".

### 7.5 Dashboard and M11 reports

- Section states (`OverviewSectionStatus`): *Nada no período* reads as a sentence with the window — "Nenhum fechamento de 14/10 a 20/10." — while *Não configurado* names what is missing and where it is fixed, never a zero: "Nenhum tipo de transação está classificado como cartão ou cheque. Classifique os tipos em Categorias e tipos." and, for the monthly figures, "Tarifa de bolão não configurada" / "Sobras não configuradas" in place of the amount.
- Every section names its window in words: "Hoje, 20/10", "Últimos 7 dias", "Outubro até hoje (parcial)", "Vencimentos de 20/10 a 02/11". A provisional figure says so: "em andamento — provisório" beside live hours.
- Overdue stays its own line, never added to the period: "Vencidos antes de hoje: 2 · R$ 200,00".
- Figures are never summed across sections or accounts: there is no "Total geral". The account list's note: "Valores por conta; não somam entre contas."
- The Dashboard's footer is factual about freshness: "Atualizado às {hora} (horário da lotérica). Atualize para ver novos lançamentos." — never "ao vivo".
- Confirmed product meanings use factual copy: **Cartões a receber até o repasse. Cheques considerados pagos no depósito; isso não comprova compensação ou conciliação bancária.** Fee/loss copy: **Tarifa do dia somada no mês; sobras são perdas comerciais.** Do not label these Q1–Q4 facts as awaiting an answer. Keep **Definição provisória — aguarda confirmação da lotérica** only beside an actually pending definition, naming it: **Conteúdo e sinal do relatório do terminal ainda não confirmados** (Q9). Provider-specific calendar/fee data and future settlement matching remain separate limitations.

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

## 9. Flow-alignment vocabulary and pending copy (Design 4.9.1, v58 candidate)

These terms document the proposed model; they do not rename shipped wire enums or claim a command exists. **Caixa** means cash/stock custody (AccountType.Terminal); **terminal CAIXA** is the physical report reference; **turno** is one accountable custody period. Use **Responsável pelo turno**, **Registrado por** and **Enviado por** separately. Custody *Em uso / Repassado / Encerrado* is distinct from current Draft/Submitted/Approved/Rejected labels. An old correction is not “caixa em uso”.

- Opening: **Conferir abertura e iniciar**; exception **Abertura diferente**. Missing opening: **A abertura ainda não foi informada**. Explicit zero: **Confirmo a abertura de R$ 0,00**. Never assume missing means zero. No-success start message after a failed first operation; preserve entry.
- Count: **Conferir**, **Recontar**. After a ledger-only change, re-confirming the same observed count uses **Confirmo que esta contagem ainda representa o caixa** with a fresh server token; Recontar is required only for the explicit recount path. Different request keys never turn a double click into two count events. Opening stays visible. Autosave says **Valores guardados** and reveals no expected/difference. Changed input: **Contagem alterada. Confira novamente.** Ledger changed: **Cálculo não confirmado — houve movimentação após a contagem.** Historical attempt keeps its own values/time; only a current confirmation can submit.
- Opening discrepancy: **Diferença na abertura do caixa — sem atribuição** covers Finish→Start, inline opening and first OpeningCount acknowledgments as well as handovers; keep both proposed and acknowledged vectors. No preceding-session custodian means **Responsável anterior** is unavailable, not guessed from who recorded the opening count. The discrepancy stays visible separately after session approval, without an added month-lock blocker or automatic personal amount.
- Handover: **Passar o caixa**, **Confirmar recebimento**, **Contestar abertura**. Show **Contagem de quem passou** and **Abertura confirmada por quem recebeu** separately. **Diferença na passagem do caixa — sem atribuição** is neutral; do not call it the outgoing operator’s shortage. Manager choices **Responsável anterior / Responsável atual / Nenhum dos dois**, with **Motivo da atribuição**; no automatic deduction.
- Fields: **Relatório do terminal**, **Tarifa Bolão**, **Tarifa Especial**. Report is signed app input; do not claim this is the exact printed CAIXA label or that the legacy report can be negative. Fee/report overlap remains pending; do not invent help that counts it twice. Before P7, actual ordinary configured type labels render verbatim.
- Approval: **Aprovar dia do caixa**, **Aprovar como único gestor**, **Motivo da aprovação excepcional**. Failed batch: **Nenhum turno foi aprovado. Resolva os impedimentos indicados e tente novamente.** Each failure names its own turno, never silently skips.
- Payment: keep **Escolha a forma de pagamento.**, no preselected Dinheiro/PIX. Generated fiado sale copy becomes **Gerada automaticamente ao registrar uma venda fiado** only when P6 ships; current counted-stock copy remains accurate meanwhile. **Saldo inicial de fiado** and **Conversão de cheque em fiado** never describe a new counter sale.
- Corrected operational help: **Depósito no cofre**, customer **Pagamento por PIX**, cheque **Depositado** (not a clearing guarantee). The current PaidAt control is a recording mechanism, not “conciliado com o banco”. Matching, returns and bank settlement copy await C contracts. R$ amounts remain the pt-BR formatting contract; API ErrorMessages remain verbatim.

Canonical operation help uses §6.19 O01–O22/E01–E15 with unconfirmed report premises visible. Future report notes name the remaining Q9 uncertainty rather than saying confirmed Q1–Q4 answers are still unanswered. Independent review of this candidate precedes both marker advancement and future blueprint acceptance.
