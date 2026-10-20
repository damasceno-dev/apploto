# Administration and configuration — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M10** (Branch members' invitation list and the link-request resolution ship with **M11**). Semantics: catalog entries [Operators](../../docs/screens.md#operators-admin), [Account assignment](../../docs/screens.md#account-assignment-admin), [Accounts](../../docs/screens.md#accounts-admin), [Clients](../../docs/screens.md#clients-admin--counter), [Categories & Transaction Types](../../docs/screens.md#categories--transaction-types-admin), [Products](../../docs/screens.md#products-admin), [Holidays](../../docs/screens.md#holidays-admin), [Settings](../../docs/screens.md#settings-admin), [Branch members](../../docs/screens.md#branch-members-admin). Contracts: server M2/M6 CRUD, M7.7 Phases 4, 5.3, 6, 7 and M8 Phases 2, 4, 5 — shipped. **Remainders:** Products' deactivation warning/recovery for retained close values (gap AA) and withdrawing a pending invitation (gap AC).

Configuration screens share one quiet pattern: a header with the main create action, a labelled list, and a restrained editor — a [dialog](../components/modal-sheet.md) for short records, an inline section for longer ones. Server guards are the authority; the UI disables what it knows will be refused and still renders the server's message when a race gets there first.

## Shared admin frame

PageHeader (title + primary **Adicionar …** action) · optional search over a complete collection (Clients only) · [Table](../components/table.md) (wide) / grouped list (compact) with the record's explicit action · editor dialog with FormFields and **Salvar** / **Cancelar** · destructive actions (**Desativar …**) as danger outline with a confirmation stating the consequence. States everywhere: loading, empty (with the permitted create action), error (verbatim, input kept), saving, success (the list re-reads; a Toast only when the editor closed). Returns: Gestão navigation; paired journeys return to their source admin screen.

## Operators (admin)

- **List:** Nome (+ **você** badge on the caller's own operator) · Login (member name and e-mail, or **Sem login**) · actions **Editar**, **Contas** (Account assignment for this operator), **Transações** (manager-selected summary), **Desligar** (Offboard employee). Primary action **Adicionar funcionário** (Add employee); a secondary **Novo operador sem login** opens the plain create dialog.
- **Solicitações de vínculo** section (above the list when any are pending): each request with person, **solicitado em {data/hora}** and **Resolver**. Resolving opens a dialog: link to an existing operator without login (Select) or **Criar operador para {pessoa}**; on success the dialog reports **Vínculo criado** and, when `HasUsableAccount` is false, continues with **Atribuir terminal** (Account assignment). Each step's result shows separately; a link without resolution is reported as such.
- **Editor:** Nome · Login (Select of active members not linked elsewhere; **Remover login** clears the link and keeps the operator). Link conflicts render verbatim.

## Account assignment (admin)

- **Header:** operator picker (Select) — the screen is always about one operator.
- **List:** Conta · Tipo (copy §5 label) · Instituição / Número · **Principal** marker · actions **Definir como principal**, **Remover**. **Adicionar conta** opens a Select of unassigned accounts with the Terminal/Tab distinction explained: **A conta fiado é uma autorização separada; ela não vem junto com o terminal.**
- **Empty:** **Este operador ainda não tem contas. Sem um terminal atribuído, ele não consegue registrar transações.** with **Adicionar conta**.

## Accounts (admin)

- **List:** Nome · Tipo · Instituição / Número · Pareamento (**Pareado com {conta}** / **Sem conta fiado**) · actions **Editar**, **Criar conta fiado** (Terminal without a Tab), **Parear** / **Desfazer pareamento**, **Desativar**.
- **Create:** a type choice first (**Terminal** · **Conta bancária**); a Terminal create offers **Sem conta fiado** · **Criar conta fiado nova** · **Parear com conta fiado existente** (one of the three). Type is never editable afterwards (no control exists).
- **Deactivate confirmation:** states that the pairing is released in the same action. Coordination busy is retryable.

## Clients (admin + counter)

- **List:** Nome · Telefone · CPF (masked) · actions **Ver extrato** (Client statement), **Editar**, **Desativar** (Manager/Admin only). Local search over the complete list (**Buscar por nome**), with the result count announced.
- **Editor dialog:** Nome, Telefone, CPF (normalized to digits on submit), CEP, Endereço, Telefone 2, E-mail, Observações. CPF conflicts verbatim. Opened inline from Transaction — Create, it returns the created client to that form.
- **Access:** any branch role in either mode, except deactivation.

## Categories & Transaction Types (admin)

- **Two sections** (Tabs on compact): **Categorias** — Nome · Direção padrão (label) · **Editar nome** · **Desativar**; **Tipos de transação** — Nome · Categoria · Regra de vencimento · Exige conta fiado e cliente · **Forma de recebimento de fiado** (label or —) · system marker (**Automática** badge for the two system types) · actions.
- **Type editor:** Categoria (create only; immutable after), Nome, Regra de vencimento, Exige conta fiado e cliente, Forma de recebimento de fiado (offered only for an entrada type that requires a conta fiado and a client — the control explains the condition when unavailable). System types open read-only with the explanation of what generates them.
- **Empty:** permitted create action.

## Products (admin)

- **List** in counting order: Ordem · Nome · actions **Editar**, **Desativar**; reorder by editing `DisplayOrder` (move up/down buttons, not drag-only). The **Diferença Caixa** row shows a lock marker and **Produto do sistema** — rename/deactivate disabled with the reason.
- **Remainder (gap AA):** the deactivation confirmation states only the shipped consequence (the product leaves new counts). A warning about closes holding saved values of this product, and any recovery or reactivation path, are **not blueprinted** until the owner's contract decision; the confirmation must not promise that historical counts stay editable.

## Holidays (admin)

- **Header:** year selector; actions **Importar feriados nacionais** and **Adicionar feriado**.
- **List:** Data · Descrição · **Editar descrição** · **Desativar** (the date is immutable — the editor says **Para mudar a data, desative e crie de novo.**).
- **Import flow:** a dialog: source (copy §5 labels), include optional federal holidays → **Ver prévia** (table: Data · Descrição · Tipo · **Já existe** marker · Fonte) → **Importar** → result counts (**{n} importados · {m} ignorados**) and per-item status. A single-source outage renders verbatim.
- **Empty year:** **Nenhum feriado em {ano}.** with **Importar feriados nacionais**.

## Settings (admin)

- **Regions:** **Data de bloqueio** (read-only DetailList row with **Ir para fechamento mensal**) · **Jornada** form: Meta diária, Desconto de almoço acima de 6h, acima de 4h — duration inputs showing `7h 20min` style values, with the inline helper about effect dates (catalog copy) · **Salvar** (disabled while unchanged).
- **Save:** only a real change opens the catalog's confirmation (target changed vs only lunch tiers); value-identical saves send nothing. Stale `ETag` → **Recarregar e conferir**.

## Branch members (admin)

- **Two sections:** **Membros** — Nome (+ **você**) · E-mail · Função (badge label) · Ativo · actions **Alterar função** (role matrix; disabled on the own row with **Você não pode alterar o próprio cargo**) and **Remover acesso** (→ Offboard employee); last active Admin's actions disabled with the reason. **Convites** — filter by Situação (default **Pendente**); rows: E-mail · Função · Operador · Situação badge (`dom.invitation-status.*`) · Válido até · Envio (`dom.mail-delivery-status.*`) · **Reenviar** (with an optional operator change for Member invitations).
- **Primary action:** **Convidar** (dialog: E-mail, Função limited by the role matrix, Operador for Member invitations) and **Adicionar funcionário** (guided).
- **Remainder (gap AC):** no **Cancelar convite** action exists; a pending row shows **Válido até {data/hora}** and nothing implies it can be withdrawn.

## Verification boundary

Static: lists, editors, guards and states. Real-UI (Web/Mobile M10/M11): role-matrix variants as Manager and Admin, own-row and last-Admin disabling with readable reasons, pairing races, import preview at 375px, and dialog focus return on every editor.
