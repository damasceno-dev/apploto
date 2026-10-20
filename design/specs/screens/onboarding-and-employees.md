# Plataforma and employee setup — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M11**. Semantics: catalog entries [Plataforma — Nova lotérica](../../docs/screens.md#plataforma--nova-lotérica), [Add employee](../../docs/screens.md#add-employee-guided), [Offboard employee](../../docs/screens.md#offboard-employee-guided); the owner setup checklist lives in the [Work Queue blueprint](queue-and-review.md#manager-work-queue). Contracts: server M8 Phases 4–6 — shipped. **Remainders:** listing earlier onboardings (gap AB) and withdrawing a pending invitation (gap AC). Live e-mail delivery is a server M8 Phase 8 launch gate.

These journeys stitch several committed steps together. The layouts therefore always show what is already **confirmed by the server**, what is still missing, and how to continue later — a guided flow that pretended to be one atomic action would lie the first time a step failed.

## Plataforma — Nova lotérica

### Area home

- **Regions:** brand strip and identity layout (no branch shell) · PageHeader **Plataforma** with **Nova lotérica** (primary) · **Consultar por código** (Input + **Consultar**) · list **Cadastros feitos neste dispositivo** — Lotérica · Responsável (e-mail) · Convite (badge `dom.invitation-status.*`) · Envio (badge `dom.mail-delivery-status.*`) · Válido até · **Ver**; each row re-read on open.
- **Remainder (gap AB):** the list is explicitly device-local; its caption says **Esta lista mostra apenas os cadastros feitos ou consultados neste dispositivo.** No layout implies a complete history of lotéricas.

### Wizard

[Stepper](../components/stepper.md) in wizard mode (steps collect input; one final command commits): **1 Lotérica** (Nome, CNPJ, Endereço, Telefone) · **2 Terminais** (repeatable rows: Nome do terminal, optional **Conta fiado pareada** name; add/remove; up to 30) · **3 Produtos adicionais** (repeatable names, up to 30; a note that the default products are created automatically) · **4 Operadores** (repeatable: Nome + terminals chosen from step 2 as checkboxes; up to 50) · **5 Responsável e revisão** (E-mail do responsável; a read-only summary of every step with **Editar** links) → **Criar lotérica**.

- **Layout:** wide — steps in a start column and the step form beside it; compact — **Passo {n} de 5** header with a disclosure listing steps; repeatable rows become stacked blocks with their remove action.
- **Input retention:** every step's input is kept on the device in the platform operator's identity scope; the frozen `RequestId` and payload are stored at first submission.
- **Submitting and outcomes:** **Criando…**; result unconfirmed → warning Notice with **Verificar e tentar novamente** (same id and payload); request conflict → danger Notice verbatim, the original record kept and **Começar outro cadastro** offered; validation → the review step lists the verbatim messages (and focuses the step only when unambiguous).
- **Result screen:** success Card **Lotérica criada** with Lotérica, Responsável, Convite status, Envio status (with **Enviado ao servidor de e-mail** wording, never "entregue"), Válido até, and the **código do cadastro** (`RequestId`) with a copy button; **Reenviar convite do responsável** when delivery failed, the invitation expired or was declined; accepted → **O responsável já aceitou o convite.** The platform operator gets no link into the tenant.
- **Errors:** `PLATFORM_ONLY` anywhere hides the area and returns to Branch picker with the verbatim message.

## Add employee (guided)

- **Frame:** PageHeader **Adicionar funcionário** (breadcrumb from origin) · [Stepper](../components/stepper.md) in committed mode — each step writes when the manager presses **Continuar**: **1 Funcionário** · **2 Terminais e contas** · **3 Acesso ao sistema** (optional) · **4 Resumo**.
- **Step 1:** choice **Novo operador** (Nome) or **Operador existente sem login** (Select); **Continuar** creates or selects, then the step shows **Operador criado: {nome}** or **Operador selecionado: {nome}**, according to the confirmed outcome.
- **Step 2:** checklist of accounts (Terminals first, then contas fiado with the separate-grant explanation, then bank accounts); each toggle performs its assignment and reports per account (**Atribuída** / the verbatim error); **Principal** radio among the assigned accounts.
- **Step 3:** E-mail, Função (role matrix); for **Membro**, a line **O convite já vincula o login ao operador {nome}.**; for Gerente/Administrador, **Depois que a pessoa aceitar, vincule o login em Operadores.** → **Enviar convite**; membership-exists → **Vincular a {membro}** instead.
- **Step 4 — Resumo:** confirmed facts and remaining items with links (**Sem terminal atribuído** → step 2; **Convite pendente até {data/hora}**; **Vincular login depois**; **Envio do convite falhou** → **Reenviar convite**).
- **Leaving:** **Concluir depois** at any step returns to the origin; completed steps stay; re-entering for the same operator (from Operators) re-derives every status from reads.
- **Layout:** wide — step list beside the step form; compact — single column with the compact stepper header.

## Offboard employee (guided)

- **Frame:** PageHeader **Desligar funcionário — {nome}** · entry from an operator or a membership row, including a member without an operator. Resolve the membership and linked operator ids before removing access, then retain those ids within this journey while every subsequent step re-reads its own resource. If no operator exists, skip accounts/deactivation with **Sem operador associado**; with no membership/e-mail, skip invitation lookup with **Sem e-mail de acesso para consultar convites**. Never infer a link from a name. Committed-mode Stepper: **1 Acesso** · **2 Convites pendentes** · **3 Contas atribuídas** · **4 Operador** · **5 Resumo**.
- **Step 1:** DetailList of the membership and **Remover acesso** (danger outline → confirmation **A pessoa deixará de acessar esta lotérica. O histórico registrado continua disponível.**); unavailable with the reason for the caller's own membership, the last active Admin, or a Manager facing an Admin; skipped with **Sem login nesta lotérica** for an operator without a membership.
- **Step 2:** pending invitations to the same e-mail with **Válido até**; no action (gap AC) and the line **Convites pendentes não podem ser cancelados; eles expiram na data indicada.**
- **Step 3:** assigned accounts with **Remover** each (optional; **Manter contas** skips).
- **Step 4:** **Desativar operador** (optional; danger outline with confirmation that history keeps the name) or **Manter operador**.
- **Step 5:** what changed and what remains, each from a fresh read.
- **Layout and leaving:** as Add employee.

## Verification boundary

Static: step order, confirmed-versus-pending presentation, remainders and returns. Real-UI (Web/Mobile M11): partial failures at each step against a local API, the wizard's idempotent retry, local SMTP capture for invitations, role-matrix variants, and focus management between committed steps.
