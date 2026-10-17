# M9.5 and M11 SQ1–SQ5 — source-evidence map (SV13 preparation)

Prepared 2026-10-17 for the shop-verification session (coordination label SV13). It organizes what the repository can
prove today; it decides nothing. No M9.5 Phase 2–4 item and no later M11 phase is complete because a row below exists.
The questions themselves live in one register — `docs/loto-specs.md` §9 (questions 1–6) — and the M11 plan's SQ1–SQ5
are §9 questions 2–6; this map reuses them and adds no new question.

**Evidence available.** The checkout holds no Access database, export or production data. Everything marked *verified*
below comes from the shipped code, seeds, spec mappings or tests. The 2026-07 Access audit is known only through the
spec text it produced (§5 `tipo_id`/`prod_id` columns, §6.12 *Direction semantics*, §8 mapping) and the milestone
hypotheses; those are cited as recorded audit findings, not re-verified here.

**Signed and not to be reopened:** the counted-stock/counted-cash fiado rule (M7.7 decision 1.3 amendment, shipped in
Phase 5; M9.5 2.2). M9.5 Phase 1 (1.1–1.4) is accepted.

| Item | Verified code/data fact | Owner decision | Working hypothesis (unverified) | Smallest missing shop answer |
|---|---|---|---|---|
| **M9.5 2.1** — `Raspadinha`, `Telesena`, `Troca de Telesena`, `Encalhe Federal` | Seeded as `Saídas` (Out) types, `SameDay`, Access `tipo_id` 20/18/19/22 (§5). The shop creates no lançamento for raspadinha sales (recorded owner fact, M9.5 preconditions). Characterization pins both consistent representations and the double count (`CashVarianceRepresentationCharacterizationTest`). | None | A: non-sale exits (returns/encalhe, prizes, voids); B: legacy row-explained sales. The R$5,686.00 tipo-20 row is cited only by the audit. | For each of the four types: which real event a row records, and what the R$5,686.00 tipo-20 row was. |
| **M9.5 2.3 / SQ3** (§9 Q4) — opening-side `Operador` and `Tarifa Bolão` | `Tarifa Bolão` is a counted product (`prod_id` 7, display 6); the port's opening is the prior counted close only (§6.12); `prod 12 Operador` is not migrated (§5 note). | None | Access added `prod 12` and the current day's `prod 7` to the initial total (`AtualizarDiferenca`, per the audit). | What the counted Tarifa Bolão value holds each day (that day's fee or a carried balance), and what `Operador` represented. |
| **M9.5 2.4** — legacy product slots | `prod_id` 1 (Fiado), 8 (Total Caixa), 9 (HorasTrabalhadas), 10 (Ausente), 12 (Operador) are not migrated (§5 note); fiado lives on Tab accounts (§6.4). | None | All derived or legacy-only. | Confirmation that none held a value the import must keep. |
| **M9.5 2.5 / SQ1** (§9 Q2) — card types | `Cartão de Crédito` (`TwoBusinessDays`) and `Cartão de Débito` (`NextBusinessDay`) are seeded `Saídas` types (§5); `DueDate` follows `SettlementRule` (§6.1). Nothing marks them receivables. | None | Cash-advance flow with acquirer settlement. | Whether the acquirer owes the amount until it lands, on which account and date, and whether the shop marks it paid. |
| **M9.5 2.5 / SQ2** (§9 Q3) — cheques | `Depósito Cheque` (`OperatorEnteredCheque`, `tipo_id` 15); open-cheque aging covers installment plans only (§6.14); plan finalize/statuses shipped (M7.7 7.6.2). | None | — | Whether a pre-dated cheque is ever a single row, and whether "paid" means deposited or cleared. |
| **M9.5 2.5 / SQ4** (§9 Q5) — bolão/federal leftovers | `Sobra de Bolão`, `Sobra de Federal` seeded under `Despesas Comerciais` (`tipo_id` 6/9). | None | — | What the rows record, on which accounts, and whether the month's figure is the sum of active rows. |
| **M9.5 2.5** — other types (`Depósito Dinheiro`, `PIX`, `MarketPlace`, `Desconto`, `Volante rejeitado`, `Pgto Prêmio`) | Seeded `Saídas`/`Despesas Comerciais` types with the §5 settlement rules; Terminal rows are mostly `Out` (Access-verified, §6.12). | None | Listed in M9.5 2.5. | Per type: who records it and whether a matching bank-side row exists. |
| **M9.5 2.6** — bank accounts | Bank accounts are never expected closers (M7.5 rule, §6.14) and have no close. | None | — | What the shop records on `contas 9/10` and the `Crédito/Débito Banco` categories. |
| **§9 Q1** — fiado sales beyond counted stock | Sales declare counted stock per operation (`HandsOutCountedStock`, §6.4); no product selector exists. | Counted-stock and counted-cash cases signed (M7.7 1.3). | — | How bolão and other non-counted fiado sales are recorded and controlled. |
| **SQ5** (§9 Q6) — operator attribution | Closes carry recorder and submitter identities (§3.14); hours are per operator/day (§6.7). | Recommendation recorded for the owner; not signed. | — | The owner's decision on accountability for shared-Terminal and manager-recorded closes. |
| **M9.5 3.1–3.4, 4.1–4.2** | — | — | — | Depend on the answers above; 3.4 (`VarianceEffect`) is an explicit adopt/defer/reject decision, not assumed. |

**Not done here, by design:** canonizing an ordinary-sale representation, renaming seed types, adding
`VarianceEffect`, inferring card or bolão meaning from names, or starting M11 Phases 4–8.
