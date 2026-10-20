# Time and personal work — blueprint

> **Design M4 · D04 (2026-10-20), review pending.** Feeds **Web/Mobile M9**. Semantics: catalog entries [Clock in / out](../../docs/screens.md#clock-in--out), [Time-entry management](../../docs/screens.md#time-entry-management), [My transaction summary](../../docs/screens.md#my-transaction-summary), [My time-entry balance](../../docs/screens.md#my-time-entry-balance). Contracts: server M5 time entry with M7.7 Phases 6–7 (in-progress triage, effective-dated policy) and M7.7 Phase 5.8 (generated share) — shipped. No remainder.

Time screens answer one of two questions: *am I on the clock?* or *am I ahead or behind?* The first wants a single big state and one tap; the second wants period totals before the day-by-day trail. Managers never see the Member tap: they record their own hours, like anyone else's, in the management console.

## Clock in / out

- **Regions:** PageHeader **Ponto**; context: today (branch-local) · operator name · **Overnight choice** *(when yesterday has an open segment)* — a warning Card **Seu turno de ontem ainda está aberto.** with two explicit actions: **Encerrar o turno de ontem** (closes yesterday's entry) and **Começar o dia de hoje** (opens today) · **Estado de hoje** — large status line (`typography.role.next-action`): **Em andamento desde {HH:mm}** with a live running duration, or **Fora do ponto**, or **Dia encerrado** · the single tap button — **Entrada** or **Saída** (CTA, full width on compact, at least `spacing.size.touch-target-comfortable` tall) · **Registros de hoje** — segments (Entrada / Saída wall-clock times as returned), total hours and balance for the day · link **Meu banco de horas**.
- **Behavior:** repeated taps are idempotent no-ops on the server; the button shows **Registrando…** and then the new state from the response. The running duration updates once a minute, is never announced per tick, and is derived only for display.
- **States:** setup needed (no operator link: EmptyState with the verbatim 403 and guidance); loading; not clocked in; running; closed for today; overnight choice; error (verbatim); lock-blocked (verbatim, rare).
- **Layout:** one column at every width; the tap button stays in the first screen height on a 375px phone even at enlarged text (the segments list goes below it).
- **Return:** Cockpit.

## Time-entry management

- **Regions:** PageHeader **Gestão de ponto**; actions **Banco de horas da equipe** (My time-entry balance roll-up) · **[FilterBar](../components/filter-bar.md)** — Operador, De / Até (default current month), Situação, **Somente em andamento** (`IsInProgress`), **Em andamento primeiro** (on by default), **Somente minhas** (`Mine`) · **Table** — Data · Operador · Situação (`dom.time-entry-status.*`) · Horas · Saldo (`dom.hour-balance.*`) · in-progress marker · **Abrir registro** · **Entry detail** (opens in a side panel on wide layouts, a full screen on compact) — segments list with Entrada/Saída times and audit (Atualizado em / por); the day's Situação select; **Adicionar segmento**; per segment **Editar** / **Remover**; **Salvar dia** (snapshot); **Remover registro do dia** (danger outline).
- **Editing rules made visible:** existing segments show their Entrada time as read-only in the snapshot form with **Editar segmento** for a granular change (the snapshot cannot change an existing `ClockIn`); new segments are added as new rows; removal is a separate confirmed action, never omission. The day's balance shown after save is the server's, under that day's own policy.
- **Dialogs:** **Remover registro do dia** uses the catalog's confirmation text verbatim; **Remover segmento** confirms with the segment times.
- **States:** loading; empty (**Nenhum registro neste filtro.**); in-progress highlighted (rows marked and sorted first); detail loading; validation (verbatim day-bound and 24h rules); lock-blocked (`TIMEENTRY_DATE_LOCKED`, detail read-only with the lock reason); coordination busy (retry); stale/unknown outcome (re-read and reconcile; no replace-all promise); success.
- **Layout:** wide — table plus detail panel side by side (panel at about 40%); below `breakpoint.wide` the detail replaces the list with a return link; below `breakpoint.medium` the table is a grouped list.
- **Return:** Gestão navigation; the detail returns to the list with filters kept.

## My transaction summary

- **Regions:** PageHeader **Minhas transações** (own context) or **Transações de {operador}** (manager-selected, **Inativo** badge when deactivated); FilterBar De / Até (default current month) · SummaryStrip: Transações (**{n} transações, {m} delas automáticas**) · Entradas · Saídas · Líquido, each with the automatic share beside it (**R$ … automáticas**) · Table by category: Categoria · Quantidade (and automatic) · Entradas · Saídas · link **Ver transações** (Transactions list with `Mine` and the window).
- **States:** loading; empty (no transactions or unlinked Member — **Nenhuma transação no período.**); error; success.
- **Notes:** these are recorded volumes, not revenue; no profit/loss color. The automatic share is stated so a fiado repayment's drawer entrada is not read as a second receipt.
- **Return:** Cockpit (own) or Operators (manager-selected).

## My time-entry balance

- **Regions:** PageHeader **Meu banco de horas** (own) or **Banco de horas da equipe** (roll-up) / **Banco de horas de {operador}**; FilterBar De / Até (default current month) · in-progress Notice **Há registros em andamento** when any `ContainsInProgress` · SummaryStrip (single operator): Horas · Saldo do banco de horas (`dom.hour-balance.*`) · Presentes · Ausências · Devendo · Abonados · Table (single operator): Dia · Situação · Horas · Meta do dia (derived; single operator only) · Saldo · in-progress marker; window days with no entry read **Sem registro** · Roll-up table: Operador (**Inativo** badge) · Horas · Saldo · Presentes · Ausências · Devendo · Abonados · **Ver detalhes**.
- **States:** loading; empty (no entries or unlinked — **Nenhum registro no período.**); in-progress variant; error (verbatim window violations); success.
- **Notes:** the target appears only per day in a single-operator view, never on the roll-up, because a window can span a policy change.
- **Return:** Cockpit (own), or Time-entry management (roll-up and manager-selected views).

## Verification boundary

Static: regions, rules made visible, states, returns. Real-UI (Web/Mobile M9): overnight choice with real data, live duration without announcement noise, snapshot reconciliation errors, and the manager detail panel's focus handling at every width.
