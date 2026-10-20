# Shell, identity and account — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M4** (shell, Login, Branch picker, Session menu) and **Web/Mobile M11** (Profile, Change password, Forgot/Reset password, Invitation, Google on Login). Semantics: [catalog](../../docs/screens.md) entries and the [navigation map](../../docs/screens.md#navigation--information-architecture). Contracts: server M8 Phases 1–7 (shipped). Launch-only gates (live Google, production e-mail, login throttling) stay with server M8 Phase 8 and do not change these layouts.

The shell is the frame every later blueprint assumes: a blue navigation column, a white work surface, and a top bar that always says **which branch, which day, which mode, who**. Identity screens sit outside that frame on a calm sheet, because no branch exists yet when they run.

## Navigation shell

### Regions

1. **Skip link** — **Pular para o conteúdo**, first focusable element, visible on focus (`layer.skip-link`).
2. **Sidebar** (wide) — product mark (placeholder mark, not a final logo; no CAIXA wordmark), grouped destinations, `sem.nav.bg`, width `spacing.size.sidebar`.
3. **Top bar** — sticky (`layer.sticky`, `sem.topbar.bg`, bottom rule `sem.topbar.border`, `shadow.raised` once content scrolls under it), minimum height `spacing.size.topbar-min`:
   branch name · mode badge ([Badge](../components/badge.md) mode variant) · branch-local date and time (`GET /branch/current`, labelled **horário da filial**) · **Trocar filial** (multi-branch identities) · theme control ([Tabs](../components/tabs.md) segmented: Claro / Escuro / Sistema, icons with names) · avatar trigger with name and role → [Session menu](#session-menu).
4. **Status rail** — directly under the top bar, full width, holding app-level [Notices](../components/notice.md) in priority order: session recovery, branch date changed, no connection, retained input waiting. At most two visible; the rest collapse into **+{n} avisos**.
5. **Workspace** — `sem.background`, page gutters `spacing.page.inline-wide` (wide) / `spacing.page.inline-compact` (compact), content capped at `spacing.size.content-max`; every screen starts with a [PageHeader](../components/page-header.md).

### Navigation groups

| Mode | Groups and destinations (labels per copy) |
|---|---|
| **Gestão** (Manager/Admin) | **Gestão**: Fila de trabalho · Transações · Nova transação. **Relatórios**: Movimento da conta · Saldo fiado · Fiado por idade · Cheques em aberto · Diferenças de caixa · Fechamento mensal. **Ponto**: Gestão de ponto. **Cadastros**: Operadores · Contas dos operadores · Contas · Clientes · Categorias e tipos · Produtos · Feriados · Configurações · Membros da filial |
| **Meu turno** (Member; linked Manager/Admin) | **Meu turno**: Hoje · Nova transação · Transações · Clientes. **Meus números**: Ponto *(Members only — never shown to Manager/Admin)* · Minhas transações · Meu banco de horas |

Current destination: `sem.nav.active` fill, `sem.nav.text-strong` label and a start-edge `sem.nav.marker` bar `spacing.size.marker-width` wide; `aria-current="page"`. Group labels use `typography.role.overline` in `sem.nav.text-muted`. Focus inside the sidebar uses `sem.focus.ring-on-nav`. **Nova transação** is a shortcut (catalog IA): it has no origin, so it returns to Transactions list. Hidden destinations are absent, never disabled — except Meu turno in the Session menu, which shows its unavailable reason.

### Responsive layout

| Width | Layout |
|---|---|
| ≥ `breakpoint.wide` | persistent sidebar + workspace (`minmax(0, 1fr)`); full top bar |
| `breakpoint.medium` – `breakpoint.wide` | sidebar becomes a [drawer](../components/modal-sheet.md); top bar: **Menu** button, branch name, mode badge, date, theme icons, avatar |
| < `breakpoint.medium` | top bar: **Menu**, compact screen title, avatar only; branch name, mode, date/time and **Trocar filial** move to the drawer header; theme moves into the Session menu |

At enlarged text the em-based breakpoints switch earlier, so the top bar wraps into the drawer before it crowds — the fix for the prototype's clipped branch context at 150%. The drawer is `spacing.size.drawer-max` wide (about 86% of a phone), over the backdrop; it closes on Escape, backdrop tap, navigation and native back, contains focus while open and returns it to **Menu**.

**Native (Mobile M4):** the same drawer and groups (React Navigation drawer) with a stack per destination for drill-downs; the native header shows the compact title and avatar; Android back and iOS swipe-back follow the catalog's return rules. No bottom tab bar is introduced, so both platforms keep one navigation model.

### Presentation questions delegated by the catalog

- **Member menu:** shows no mode group at all — a Member has one mode and nothing to switch; no line about Gestão is added.
- **Trocar filial in the menu:** yes, for identities with more than one branch, in addition to its place beside the branch name (wide) or in the drawer header (compact).
- **Mode when the top bar is narrow:** the mode badge moves to the drawer header and the Session menu header, and the avatar trigger's accessible name includes it (**Conta de Ricardo Moura, Gestão**).

### Route map (web)

Paths are shareable addresses of a destination and its **filters only**. The return origin travels in navigation state (web history state, native params), never as a URL parameter, so a link cannot claim an origin or a permission; a direct link uses the catalog fallback.

| Destination | Path |
|---|---|
| Login · Forgot password | `/entrar` · `/entrar/esqueci-senha` |
| Reset password · Invitation *(fixed by the server's e-mail links)* | `/reset-password` · `/invitation` |
| Branch picker · Plataforma | `/filiais` · `/plataforma` |
| Profile · Change password | `/conta` · `/conta/senha` |
| Manager Work Queue · Daily-close approval | `/gestao/fila?data=` · `/gestao/conferencia/{dailyCloseId}` |
| Operator Day Cockpit | `/turno/hoje` |
| Open day · Close day · Fix & resubmit | `/fechamentos/abrir?conta=&data=` · `/fechamentos/{dailyCloseId}` · `/fechamentos/{dailyCloseId}/correcao` |
| Transactions list · Create · Edit | `/transacoes?de=&ate=&conta=&…` · `/transacoes/nova` · `/transacoes/{id}` |
| Installment plan (new · existing) | `/transacoes/cheque-pre-datado/novo` · `/transacoes/cheque-pre-datado/{originTransactionId}` |
| Clients · Client statement · Receber pagamento | `/clientes` · `/clientes/{id}/extrato` · `/clientes/{id}/receber` |
| Reports | `/relatorios/movimento`, `/relatorios/saldo-fiado`, `/relatorios/fiado-por-idade`, `/relatorios/cheques`, `/relatorios/diferencas`, `/relatorios/fechamento-mensal/{ano}/{mes}` |
| Self-service | `/turno/ponto`, `/turno/minhas-transacoes`, `/turno/banco-de-horas` (manager-selected: `/gestao/operadores/{id}/transacoes`, `/gestao/ponto/banco-de-horas`) |
| Time-entry management | `/gestao/ponto` |
| Administration | `/gestao/operadores`, `/gestao/contas-dos-operadores`, `/gestao/contas`, `/gestao/categorias`, `/gestao/produtos`, `/gestao/feriados`, `/gestao/configuracoes`, `/gestao/membros` |
| Guided employee journeys | `/gestao/funcionarios/novo`, `/gestao/funcionarios/{operatorId}/desligar` |

Every path passes the session gate first; a branch-scoped path opened without a branch session is kept as intent until Branch picker resolves a session for the right branch and permission.

### Shell states

| State | Presentation |
|---|---|
| Loading branch context | top bar shows the branch name from the session and a Skeleton for date/time; navigation renders immediately |
| Session recovery | warning Notice in the status rail (**Sua sessão está sendo renovada…**); writes paused; on failure → Login with the verbatim/neutral message |
| Branch date changed | info Notice **A data da filial mudou. Confira os dados antes de continuar.**; it stays until acknowledged |
| No connection | warning Notice; financial actions disabled with the reason; reads show last data marked **Desatualizado** |
| Role changed live | the shell re-resolves navigation from the live role; a screen no longer permitted falls back to the role home with an explanation |
| Operator link lost | Meu turno removed or showing setup-needed per the catalog; retained input notice. With no push contract, `GET /operator/self-context` is re-read at session creation or recreation, at the mode switch, on entry to any Meu turno route, on foreground return, and after a write answered 403/400 for a missing link — the only moments the shell learns of the change |

## Login

- **Regions:** brand strip (`sem.nav.bg`, mark, theme control) · centered [Card](../components/card.md) sheet (`spacing.size.dialog-max`, `spacing.inset.dialog`): eyebrow **Lotero**, title **Entrar**, E-mail, Senha (with **Mostrar senha**), **Entrar** (primary, full width on compact), **Esqueci minha senha** (text link), a divider **ou**, **Continuar com Google** (secondary, Google's mark per its branding rules), static hint **Primeiro acesso? Abra o link do convite recebido por e-mail.**
- **Layout:** one column at every width; the sheet becomes edge-to-edge below `breakpoint.medium` with `spacing.page.inline-compact` gutters. Nothing beside the form.
- **Focus order:** E-mail → Senha → Mostrar senha → Entrar → Esqueci minha senha → Continuar com Google. Enter in Senha submits.
- **Link confirmation step:** replaces the form inside the same sheet: title **Confirme sua senha do Lotero**, explanation that the Google account matches an existing Lotero account, Senha field, **Confirmar senha e vincular**, **Cancelar** (back to the idle form). Focus moves to the title.
- **States:** validating and invalid credentials as an error Notice above the fields (verbatim; fields kept, password cleared); Google unavailable or not confirmed as a Notice under the Google button with the password form intact; attempt expired → Notice plus a fresh **Continuar com Google**; rate limited → Notice, buttons stay enabled; loading → **Entrando…** on the pressed button, both methods disabled while one runs; provider cancelled → silent return.
- **Return:** success → Branch picker (or a validated pending intent). This is the landing for every session end.
- **Verification boundary:** layout and states are specified statically; Google's real dialog and device sign-in are Mobile/Web M11 with M8 Phase 8 credentials.

## Branch picker / session

- **Regions:** same brand strip and sheet as Login · title **Escolha a filial** · list of branches as full-width option rows (name in `typography.role.body-strong`, address/CNPJ in `typography.role.helper`, role [Badge](../components/badge.md)) · **Plataforma** link row (platform operators only) · account footer: signed-in name and e-mail, **Minha conta**, **Sair**.
- **Layout:** one column; rows at least `spacing.size.touch-target-comfortable` tall; the whole row is the action (it is a button whose name includes the role: **Lotérica Boa Sorte — Centro, Gerente**).
- **States:** loading (Skeleton rows); single branch → auto-open (a brief **Abrindo {filial}…**, no list); empty → [EmptyState](../components/empty-state.md) setup variant **Você ainda não tem acesso a uma lotérica.** with guidance and the account footer (plus **Plataforma** when flagged); branch unavailable on selection → danger Notice with verbatim `BRANCH_NOT_FOUND` and a refreshed list; error → Notice with retry; selecting → the chosen row shows **Abrindo…**, others disabled.
- **Return:** to the role home of the opened branch, or a validated intent.

## Session menu

- **Anatomy ([Menu](../components/menu.md)):** header block — name, e-mail, branch and role, mode badge · **Modo de trabalho** group (Manager/Admin only): Gestão and Meu turno with one-line descriptions, the active one checked; Meu turno unavailable shows **Indisponível: seu login não está vinculado a um operador desta filial.** with **Resolver em Operadores** · **Trocar filial** (multi-branch) · **Minha conta** · **Alterar senha** (when `HasPassword`) · **Plataforma** (flagged) · theme control (compact only) · **Sair**.
- **Layout:** popover anchored to the avatar on wide/medium (max `spacing.size.menu-max`); a [bottom sheet](../components/modal-sheet.md) on compact and native.
- **States:** self-context loading (**Verificando…** on Meu turno); signing out (**Saindo…**); confirm-before-sign-out dialog only when device-kept input or an unconfirmed operation exists (copy §7.4); sign-out unconfirmed → Login shows the info Notice.
- **Return:** Escape/close → focus to the avatar trigger; mode switch → that mode's home; account items → their screens, which return to the screen that opened the menu.

## Profile

- **Regions:** PageHeader **Minha conta** (no breadcrumb; **Voltar** to the opener) · [DetailList](../components/detail-list.md): E-mail (read-only), Forma de entrada (**Senha definida** / **Sem senha — esta conta entra com Google**), **Operador da plataforma** marker when true · form section: **Nome** ([Input](../components/input.md)) with **Salvar** · password section: **Alterar senha** link or **Receber link para criar senha** with its consequence text.
- **Layout:** single column at `spacing.size.form-max`; identical on all widths.
- **States:** loading (Skeleton values); Save disabled while unchanged with its reason; saving; saved (success Notice **Nome atualizado** in place, shell name updated); validation (inline under Nome, verbatim); coordination busy (warning Notice + retry); link requested (the Forgot password confirmation text inline).
- **Shell context:** works with or without a branch session; with none, it sits on the identity sheet layout instead of the shell.

## Change password

- **Regions:** PageHeader **Alterar senha** · consequence [Notice](../components/notice.md) (info): **Ao alterar a senha, todas as sessões desta conta são encerradas, inclusive neste dispositivo.** · Senha atual · Nova senha · Repita a nova senha · **Alterar senha** (primary) · **Cancelar** (text).
- **Layout:** single column at `spacing.size.form-max`.
- **States:** client checks inline (length, repeat match); wrong current password (after the single recovery cycle) → danger Notice verbatim, current-password field cleared and focused; submitting **Alterando…**; result unconfirmed → warning Notice with **Entrar novamente**; success → credentials cleared, Login with **Senha alterada. Entre novamente com a nova senha.**
- **Focus:** on load the first field; on error the summary.

## Forgot password

- **Regions:** identity sheet · title **Recuperar acesso** · one paragraph explaining that a link will be sent · E-mail · **Enviar link** · **Voltar para entrar**.
- **States:** sent → the sheet body is replaced by the fixed confirmation text (copy §7.4) with **Voltar para entrar** and **Enviar novamente**; invalid e-mail inline; busy/no-connection Notice. The confirmation never varies with the account.
- **From Profile:** the e-mail is pre-filled and read-only; return goes to Profile.

## Reset password

- **Regions:** identity sheet · title **Definir nova senha** · Nova senha · Repita a nova senha · **Salvar nova senha**.
- **Entry:** `/reset-password#token=…`; the app captures the fragment, replaces the URL with `/reset-password` before first render, and keeps the token in memory only.
- **States:** link missing → the sheet shows **Abra novamente o link recebido por e-mail.** and **Pedir novo link**; unusable link (409) → danger Notice verbatim + **Peça um novo link**; rate limited; result unconfirmed → **Tente entrar com a nova senha**; success → Login with **Senha definida. Entre com a nova senha.**

## Invitation

- **Regions (identity sheet, wider: `spacing.size.form-max`):**
  1. Invitation summary: **Você foi convidado para {lotérica} como {função}.**, **Válido até {data/hora}**, operator line when pre-linked, **Use o e-mail que recebeu este convite.**
  2. Signed-in banner when a session exists: **Você entrou como {e-mail}** · **Aceitar convite** (primary) · **Sair e usar outra conta**.
  3. Otherwise three paths as a [Tabs](../components/tabs.md) set or stacked sections on compact: **Criar conta com senha** (Nome, Senha, Repita a senha, primary **Criar conta e entrar**) · **Continuar com Google** (Seu nome, Google button) · **Já tenho conta** (**Entrar e aceitar** → embedded Login form).
  4. Footer: **Recusar convite** (danger text button → confirmation dialog **O convite deixará de valer.**).
- **Entry:** `/invitation#token=…`, captured and stripped like Reset password; held in tab-scoped storage only while an embedded sign-in runs.
- **States:** loading preview (Skeleton summary); unusable (the sheet shows the verbatim message and **Peça um novo convite a quem administra a lotérica.**, no forms); link missing; e-mail mismatch (danger Notice + **Sair e usar outra conta**); already a member; validation inline; rate limited; Google unavailable; declined (**Convite recusado.** with **Ir para entrar**); result unconfirmed (**Verifique se você já tem acesso** → Branch picker); success → Branch picker or the accepted branch's home.
- **Focus:** summary heading on load; the chosen path's first field when a path opens.

## Verification boundary

Static here: regions, order, tokens, states, focus and returns. Real-UI work for the app milestones: keyboard-only and screen-reader passes through every state above, 375px and 200% text, drawer focus containment, Google and e-mail links on real devices, and fragment stripping in browser history.
