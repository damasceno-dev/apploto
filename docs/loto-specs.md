# Lotero — Backend Schema Specification

<!--
Sync group: loto-backend-docs
Canonical source: docs/loto-specs.md (this file is canonical; derived artifacts: docs/loto_presentation.html, docs/loto_entity_relationship_diagram.html)
Coverage: Full entity model, relationships, invariants, workflows, and Access-to-Lotero mapping.
Spec revision: v59
-->

> **v59 (2026-10-29), owner-accepted M9.6 foundation; independent technical review of the candidate and the runtime pending (M9.6 1.3 stays open until it accepts):** Server M9.6 Phases 2–3 ship the session/custody model (§3.30, §6.20–§6.22). `DailyClose` is the session: custodian, per-cashbox `Sequence`, half-open `[StartedAt, EndedAt)` interval and custody state separate from review status. New records: physical `Terminal`, acknowledged `SessionOpening`, `SessionHandover`, `SessionOpeningDiscrepancy` with append-only revisions and attribution events, `SessionEvent`, `CustodyMappingIssue` and the P3 `OpeningCount`. `/dailyclose` is replaced by `/session` with no compatibility routes; the first financial operation may carry its opening acknowledgment on the existing commands. Owner decisions F1 and F2 ship as the custody exception and the declare/rearrange commands. Database guards enforce non-overlapping Ready intervals, one active custody per cashbox, retained Ready history, session-bound cashbox rows and row/session business-date coherence. The upgrade quarantines existing closes and unsessioned cashbox rows as Unresolved with mapping issues; disposable data may instead be reset (`server/scripts/reset-disposable-database.sh`). Not shipped: P4 Conferir attempts and blind count (`LedgerRevision` exists, increments land with P4; variance preview stays), P5 approval separation, P6 fiado boundaries, P7 close-form fields, P9 safe, and a reviewed mapping-plan apply path.
> **Previous status (v58 candidate, 2026-10-26):** M9.5 Phase 5 facts and M9.6 Phase 1 operation/session foundation (§3.30, §6.19–§6.23). Documentation only; shipped DTOs/OpenAPI and runtime unchanged. Design 4.9.1 semantics reconciled as a candidate; both markers aligned to v58 at the owner’s explicit request on 2026-10-27. This records marker alignment, not a separately supplied final technical acceptance. M9.6 1.3 owner/reviewer gate is open. 2026-10-29 foundation-gate corrections within the same candidate: session `TotalOpening` source, acknowledged-successor correction, one coordination order including pairing/deactivation and Account-row writes, first operation only through the existing guarded financial commands, unresolved history blocking opening count, and two owner decisions (§6.20 *Open foundation decisions*; F1 and F2 decided 2026-10-29).
> **Previous status: v57; design catalog reconciliation accepted, independent M8 Phase 8 and M11 server acceptance pending.** Server M8 Phase 8 local subset: password login now spends two persistent failed-check budgets (per account and per account + client network, new §3.29 `LoginThrottle`) behind a process-local per-network request limit; a refusal is 429 `SECURITY_RATE_LIMITED` with `Retry-After`, identical for every account state and decided before the account is read (§6.15, §6.16, §6.18). Recovery-throttle retention is discharged (§3.23). Production sender/Google/launch gates stay open. Design catalog reconciliation for v56 and v57 was accepted together by independent review on 2026-10-21; both design markers are v57. The server's final M8 Phase 8 review and M11 Phases 4–7 repeat review remain separate.
> **Previous status (v56, independent M11 semantic acceptance pending):** Server M11 Phases 4–7: explicit TransactionType report classification (`SettlementInstrument`, `MonthlyFinancialRole`; §3.10), open-cheque aging widened to standalone cheques, and three new Manager/Admin reads — upcoming settlements, the monthly financial report and the Dashboard's management overview (§6.14) — on the owner-adopted working answers to §9 questions 2–6. Existing types stay unclassified until configured; no financial write, formula or Work Queue behavior changed. Design catalog reconciliation awaits independent review before the design markers move.
> **Previous status (v55, independent M9.5 semantic acceptance pending):** M9.5 Q3/Q9 opening-side correction: fresh seeds record Tarifa Bolão and Operador as ordinary SameDay In transaction types, not counted products. Existing-branch migration requires reviewed identities and refuses retirement on unlocked retained counts (gap AA). Q5 adopts count-only cash sales provisionally; formula, HTTP contracts and historical values are unchanged.
> **Accepted previous baseline:** Revised spec (v54) — Server M8 Phases 1–7 provide account security, invitations, operated onboarding and verified Google identity. The old public register/create and direct add-member routes are closed; invitation acceptance and platform onboarding are the entry paths. Independent technical and semantic acceptance of both M8 batches was recorded by the 2026-10-18 repeat review, and both design markers are aligned to v54. Production SMTP/Google setup, design 0.4.6 and launch acceptance remain pending. See §3.22–3.28 and §6.16–6.18.
> **Previous baseline (v52):** M7.6 follow-up Phase 4 (Shared M2 handoffs H3/H4) makes the generated contract describe two more runtime facts (§6.15): response numbers are published as `number`/`integer` only, while request numbers keep admitting the numeric strings binding accepts; and every operation carries its own `security` — none, the identity token, or a branch token with its admitted roles. No endpoint, DTO, status code, header, permission, serializer or persistence behavior changed. As of v51, M11 Phases 2–3 add whole-set report totals and historical drill-downs (§6.14): open-cheque aging `OutstandingTotal`/`OpenRowCount`/`ByBucket`, cash-variance status counts, monthly-reconciliation lock context, fiado balance debt/credit split, FIFO fiado aging totals and `OldestUnpaidDate`, and Manager/Admin drill-downs into deactivated accounts and operators. M7.7 Phase 8 corrects §6.7's segment snapshot wording and the presentation's create-time `PaidAt` examples. As of v50, M7.7 Phase 5 ships fiado settlement coherence (§3.10, §3.12, §6.4, §6.5, §6.12, §6.14): branch/client-wide FIFO netting shared by balance, aging and the new client fiado statement; *Receber pagamento* with a required, configured repayment method; counted-stock fiado sales and counted-cash repayments that write a linked, system-generated Terminal leg atomically; protected generation origin; the operator summary's automatic share; and no close-day fiado reference for any role. Every remaining v43 forward marker is discharged. As of v49, M7.7 Phase 7.6.3 ships the transaction-type list filter (§6.10): `GET /transaction` accepts an optional `TransactionTypeId`, combined with the other filters before paging and counts inside the unchanged branch and Member account scope. As of v48, M7.7 Phase 7.6.2 ships atomic cheque-plan finalization (§6.3): a plan read with a computed status, one command that activates every Draft installment or none, explicit confirmation for Mixed plans, and per-row finalize refused for plan installments. As of v47, M7.7 Phase 7.6.1 ships durable close review events (§3.21, §6.13): a rejection and every real count change that moves a later close's opening are recorded as immutable events, read through `GET /dailyclose/{id}/history` under the close's own read permission; closes that predate the events report incomplete history instead of inferred values. As of v46, M7.7 Phase 5.3 ships Terminal↔Tab pairing integrity (§3.7): deactivating either side clears the pairing in the same commit, pairing changes and deactivations serialize per branch, a data-only migration cleared earlier stale pairings, and Tab access stays an explicit `OperatorAccount(Tab)` grant; the rest of Phase 5 (fiado settlement, statement, linked movements) is still pending. As of v45 (documentation-only): the owner's 2026-10-16 decision for a separate read-only manager Dashboard and the reviewed-pending M11 reporting plan are recorded as ⚠ *Pending M11 change* forward markers (§6.14, §7.2); the shipped fiado balance and open-cheque aging totals are now documented as they behave (§6.14); and five reporting questions join §9. No shipped endpoint, DTO, persistence, or runtime behavior changed. As of v44 (contract rename only): the Manager Work Queue read is `GET /report/manager-queue`, and its four response DTOs are `ResponseManagerQueue*Json` (§6.14); the former `GET /report/dashboard` route is removed with no alias, and fields, permissions, validation, status codes, and semantics are unchanged. As of v43 (documentation-only): the owner decisions from the 2026-10 prototype review are recorded as forward markers for their owning M7.7 phases (§3.12, §6.3, §6.4, §6.5, §6.10, §6.12, §6.13, §6.14), and new §9 holds open product questions; no shipped endpoint, DTO, persistence, or runtime behavior changed. As of v42, M7.7 Phase 7.5 adds the Work Queue backlog aggregate: the queue read (then `GET /report/dashboard`) additively gains `Backlog` and `UnlockedDraftTransactionCount` (§6.14), with no schema change and no change to the selected-day facts. The M7.6 close-out stands: the generated OpenAPI document is a faithful codegen-grade contract (§6.15). Required/optional metadata is derived project-wide from C# nullability, and every contract enum is published by declared name as `type: string`. Apart from the two additive queue members and the v44 rename, endpoints, DTO shapes, status codes, and domain semantics are unchanged from v40; the v30 `required`-metadata caveat is discharged. M7.7 Phase 7 time-entry policy integrity, the Phase 6 contracts, the v38 month-lock findings, and the expanded Phase 3 close chain all remain as described.
> **Scope:** Entity model, relationships, business rules, domain knowledge  
> **Stack:** .NET + EF Core + PostgreSQL  
> **Revision notes:**  
> v59: M9.6 P2–P3 runtime — session/custody persistence and database guards, `/session` routes replacing `/dailyclose`, inline opening on the financial commands, handover, finish, custody exception (F1), declared/rearranged past sessions (F2), opening discrepancies and attribution, opening count (P3), physical terminals, session-keyed variance/ledger/gap/reports, legacy quarantine; OpenAPI and `shared/api` regenerated.
> v58: Documentation candidate for M9.5 P5 / M9.6 P1: product facts with provenance, O01–O22/E01–E15, proposed session/custody entities, commands, consumers and upgrade resolution. No runtime/OpenAPI changes; Design 4.9.1 reconciled with markers pending technical review.
> v1→v2: Fixed classification invariant (TransactionType is single source of truth, CategoryId/Direction denormalized), added transaction authorship (RecordedByOperatorId + CreatedByUserId), corrected Fiado product seed (calculated, not persisted), fixed lco_status mapping (unused in Access), fixed OriginTransactionId self-reference for installments.  
> v2: Added Account.TabAccountId invariants, OperatorAccount.IsPrimary uniqueness, branch consistency rules (6.9), CashVariance calculation semantics (6.10), seed data scope clarification.  
> v3: Rename 6h and 4h to 6H and 4H to attend for entities naming rules.  
> v4: Added Client.Cpf uniqueness per branch as a filtered unique constraint on active rows.  
> v5: Corrected the Account creation/pairing flow: Tab accounts are optional per terminal, pairing/unpairing is explicit for existing accounts, account creation is split into explicit Bank/Terminal/Tab operations, new Tabs are always created for an existing or newly-created Terminal, and a new paired Tab inherits the Terminal descriptive fields at creation time.  
> v6: Changed Client.Cpf storage from formatted varchar(14) to normalized digits varchar(11); application layer strips non-digit chars before persistence.
> v7: Added TransactionType settlement metadata (`SettlementRule`, `RequiresTabAccountAndClient`) and documented due-date and fiado enforcement semantics.
> v8: Added the active Operator user-link uniqueness invariant: at most one active linked Operator per `(User, Branch)`, enforced by filtered unique index.
> v9: Documented that updating an Operator with `UserId = null` clears the login link while preserving the Operator row.
> v10: Aligned §6.3 with the Phase 3.1 cheque-installment contract — manual rows by default, optional auto-generation with monthly stagger and weekend adjustment, exact-sum invariant, prefix-only description fallback, and SaveAsDraft propagation.
> v11: Added §6.10 documenting the Member transaction read scope: 403 vs 404 contract on Get, account-scope visibility on List (members see all rows on linked accounts regardless of recording operator), empty-scope short-circuit on List, and the enriched list response shape (joined names + paging metadata).
> v12: Added §6.11 documenting the transaction update contract: editable and non-editable fields, local-business-day permission matrix, Member account-scope ordering, Mine list filter behavior, update audit stamping, and lock-date behavior.
> v13–v15: (internal milestone iterations)
> v16: Milestone 4 DailyClose workflow hardened — §3.14 audit pair, §6.5 Submitted→Draft recall transition, §6.12 explicit Direction handling, §6.13 DailyClose contract including role × state × business-day matrix.
> v17: Milestone 5 Phase 1 TimeEntry & Holiday foundation — §3.16 updated with audit fields and filtered unique constraint; §3.17 updated with table and filtered unique constraint; §6.3 installment stagger now skips weekends and branch holidays; §6.7 references ITimeEntryCalculationService with midnight-crossing and exact-boundary notes; §6.8 documents holiday-aware business-day calculation via DueDateCalculator and IBranchHolidaySource.
> v18: Milestone 5 Phase 3.5 foundational live-running — §6.7 updated with partial-Present semantics (ClockIn required, ClockOut optional), live-running rule with synthetic effectiveClockOut on same branch-local day, forgotten-clock-out path returns (0, 0), ClockOut-without-ClockIn always invalid (TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN); CalculateLiveRunning method signature and contract added.
> v19: Milestone 5 Phase 3.6 multi-segment TimeEntry — §3.16 TimeEntry updated to drop ClockIn/ClockOut and add Segments navigation; §3.16a TimeEntrySegment added with full DateTime clock fields, FK, filtered unique open-segment constraint, day-bounds invariant, and audit fields; §6.7 fully rewritten for the segment-list contract using DateTime semantics, top-up gap-aware lunch rule, dual-shape PUT (Member Action vs Admin Segments), idempotent no-ops, status-transition rule, day-bounds rule, and worked examples (overnight, live-running gap, forgotten open).
> v20: Clarified §6.7 Member tap routing for overnight vs forgotten-close cases: prior-day open segments are resolved by the next submitted Action and Date, not by server reinterpretation.
> v21: Added §5.1 Brazilian Holiday Calendar appendix documenting the M6 pure-function import source: 10 mandatory national holidays, 3 curated optional federal Easter-anchored entries, Law 9.093/1995 for Sexta-feira Santa, Law 14.759/2023 for Consciência Negra, and the Anonymous Gregorian / Meeus/Jones/Butcher Easter algorithm reference.
> v22: Milestone 6 Phase 6.5 multi-source Brazilian Holiday providers — §3.17 Holiday gains the `Source` column (`HolidaySource` enum: Manual=0, Canonical=1, BrasilApi=2, Nager=3) with a default of `Manual` and a Phase 6.5 migration backfilling existing rows. §5.1 grows a "Sources" subsection covering composite ordering (Nager → BrasilAPI → Canonical), the 13-concept identity catalog with name-match + ±3-day date proximity tiebreaker, the documented provider quirks (BrasilAPI's "Confraternização mundial" / "Dia da consciência negra" / "Independência do Brasil" renames; Nager's regional `global: false` rows being dropped; both providers missing Quarta-feira de Cinzas → always canonical backfill), and the 502 `HOLIDAY_SOURCE_UNAVAILABLE` contract for explicit single-source failures (Composite never 502s because canonical always backfills).
> v23: Milestone 7 Phase 1 Reporting Surface foundation — §6.14 added covering the read-only reporting contract, three permission buckets (Manager/Admin whole-branch views, operator-self with empty-scope short-circuit, write-twin scope for preview endpoints), `AgingBucket` enum definition with exact boundary semantics (day 30 → `Days0To30`, day 31 → `Days31To60`, day 90 → `Days61To90`, day 91+ → `Days91Plus`), date-range guardrails (closed window, span ≤ 366 days, `AsOfDate` defaults to branch-local today via `IBranchClock`), `Status = Active AND Active = true` filter on financial totals, and the preview-never-commits invariant pinned by reload assertions.
> v24: Milestone 7 Phase 4 Fiado Aging Report — §6.14 extended with "Fiado aging report" subsection documenting `GET /report/fiado/aging` per-row contract (`ResponseFiadoAgingItemJson` fields: `TransactionId`, `Date`, `DueDate`, `Value`, `DaysOutstanding`, `Bucket`, `ClientId`, `ClientName`, `AccountId`, `AccountName`, `Description`), `DaysOutstanding = max(0, (asOfDate.Date − dueDate.Date).Days)` formula, bucket assignment via `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)`, future-due rows included in `Current` bucket, filter semantics (Tab-only, `PaidAt IS NULL`, optional `clientId`/`accountId`), deterministic ordering `DueDate ASC, Date ASC, Id ASC`, and `ResponseFiadoAgingJson` envelope shape.
> v25: Milestone 7 Slice 5 Open-Cheque Aging Report — §6.14 extended with "Open-cheque aging report" subsection documenting `GET /report/cheques/open-aging` per-origin-group rollup contract. Groups are built from rows with `OriginTransactionId IS NOT NULL AND Status = Active AND Active = true`; a group appears only when it has at least one row with `PaidAt IS NULL`. Group fields: `OriginTransactionId`, `OutstandingTotal` (sum of unpaid row values), `OldestOpenDueDate` (earliest unpaid DueDate), `OldestOpenBucket` (bucket for `OldestOpenDueDate`), `OpenRowCount`, `TotalRowCount`, `AccountId`, `AccountName`, `ClientId`, `ClientName`, `Description` (from origin row). Sibling rows within each group: unpaid installment rows loaded by `OriginTransactionId`; per-row fields: `TransactionId`, `DueDate`, `Value`, `DaysOutstanding`, `Bucket`. `AsOfDate` optional, defaults to `IBranchClock.LocalBusinessDate(UtcNow())`. Optional `accountId` and `clientId` filters. Ordering: `OldestOpenDueDate ASC, OriginTransactionId ASC`.
> v26: Milestone 7 Slice 7 Edit Impact Preview — §6.14 extended with "Edit impact preview" subsection documenting `POST /transaction/{id}/edit-preview` (Manager/Admin). The body reuses `RequestUpdateTransactionJson` verbatim (no parallel DTO, no field made optional); `asOfDate?` lives on the query string. The use case mirrors `PUT /transaction/{id}` step-for-step (role check → validate → no-tracking load with TransactionType + Account → cancelled/lock/fiado/client guards → relative validator) minus `IUnitOfWork` and the mutation/Commit, then delegates to the concrete `TransactionEditImpactProjector` helper (no interface — same convention as the other M7 compute helpers). Response `ResponseEditTransactionPreviewJson { TransactionId, Impact, Warnings }` always carries all three impact sections: `ReceivableImpact` (bucket-before/after on a DueDate shift, appears/disappears flags on a `PaidAt` flip), `FiadoBalanceImpact` (per-client outstanding deltas only when `AccountType = Tab` and the client changes; old client −signedValue, new client +signedValue, `signedValue = Out ? +Value : −Value`), and `CashVarianceImpact` (`AccountId`/`Date`/`DailyCloseStatus` populated whenever a close exists; `CurrentVariance`/`ProjectedVariance` live-recomputed via `ICashVarianceCalculator` for any non-Draft close (Submitted/Approved/Rejected), with the close status surfaced via `DailyCloseStatus` so the manager can tell a pending vs signed-off vs repudiated number; `VarianceDelta` is the computed net-flow delta, 0 today because no editable field changes §6.12's inputs but derived, never hardcoded; single-close boundary documented). Monthly lock-readiness is deferred to the Milestone 10 monthly-reconciliation report. Preview-never-commits is enforced by construction (no `IUnitOfWork`) and pinned by WebApi reload assertions plus a determinism test that compares every impact against the post-PUT state: receivable bucket via fiado aging, fiado deltas via before/after fiado balances, and cash variance via a real post-write calculator recompute.
> v27: Milestone 7 Slice 7.1 Create Impact Preview — §6.14 extended with "Create impact preview" subsection documenting `POST /transaction/preview`. **Permission is the same as the `POST /transaction` write twin** — `[TokenAuthenticateBranch]`, not Manager/Admin-only; the use case runs through `TransactionCreatePreamble`, so Members inherit the same linked-operator + account-scope checks as the real create flow (preview/write parity: anyone who can create the row can preview it). This contrasts with the edit-impact preview, which stays the deliberate Manager/Admin exception because it previews an edit to an existing persisted row loaded by id, not a scope-limited create. The body reuses `RequestCreateTransactionJson` verbatim; `asOfDate?` lives on the query string. The use case mirrors `CreateTransactionUseCase` step-for-step (validate → preamble resolve) minus `AddRange`/`Commit`, then delegates to `TransactionEditImpactProjector.ProjectCreate(...)` (the same single DI-registered Reports helper, now hosting both projections). Response `ResponseCreateTransactionPreviewJson { Impact, Warnings }` carries **no** `TransactionId` (nothing is created). The shared impact envelope `ResponseTransactionEditImpactJson` was renamed to the neutral `ResponseTransactionImpactJson` (free rename — v26 unreleased) and is now returned by both previews. A `Draft` would-be row short-circuits all three sections to empty/zero. For an `Active` row: `ReceivableImpact` marks a Tab row appearing (`RowAppearsInOpenReceivables = true`, `BucketAfter` from `ReportAgingBucketizer.BucketFor(DueDate, asOfDate)`, `BucketBefore` null; empty for non-Tab); `FiadoBalanceImpact` carries one `+signedValue` delta on the selected Tab client (`signedValue = Out ? +Value : −Value`, §6.4); `CashVarianceImpact.VarianceDelta = −NetFlow(Direction, Value)` (`NetFlow = In ? +Value : −Value`) is **genuinely non-zero** — the reason this slice exists, contrasting the edit preview's structural zero — with `CurrentVariance`/`ProjectedVariance` live-recomputed for any non-Draft close and `DailyCloseStatus` surfaced. For Member callers every impact query uses only the preamble-resolved `(account, client, date)` — no branch-wide balances, receivables, or all-account variance summaries. Preview-never-commits is enforced by construction (no `IUnitOfWork`/`ITransactionsRepository`) and pinned by a WebApi row-count assertion plus a determinism test that compares every impact against the post-create state: receivable bucket via fiado aging, fiado delta via fiado balance, and cash variance via a real post-create calculator recompute.
> v28: Milestone 7 Slice 7.2 Cheque Installment Impact Preview — §6.14 extended with "Installment impact preview" subsection documenting `POST /transaction/installment/preview?asOfDate?`. **Permission is the same as the `POST /transaction/installment` write twin** — `[TokenAuthenticateBranch]`, not Manager/Admin-only; the use case runs through `TransactionCreatePreamble` (Member linked-operator + account-scope checks inherited), so anyone who can create the plan can preview it. The body remains `RequestCreateTransactionInstallmentJson` verbatim; `asOfDate?` lives on the query string. The Phase 6 row preview (`TotalValue`, `InstallmentCount`, `Rows`) is **byte-for-byte unchanged**; a new additive `Impact: ResponseInstallmentPreviewImpactJson { OpenChequeAgingImpact, FiadoBalanceImpact, CashVarianceImpact }` is returned alongside it (the fiado and cash-variance sections reuse the create/edit preview DTOs verbatim; the open-cheque section is a group rollup with its own `ResponseOpenChequeAgingImpactJson` envelope, and per-row identity is positional via `Index` — no `OriginTransactionId` / `TransactionId` is exposed because nothing is created). The use case adds `IBranchClock` + the concrete `TransactionEditImpactProjector.ProjectInstallment(...)` and stays non-persisting by construction (no `ITransactionsRepository`/`IUnitOfWork`). A `Draft` plan short-circuits all three sections to empty/zero (draft rows are excluded from the open-cheque report, §6.4 fiado sums, and §6.12 cash variance) while still returning the generated rows. For an `Active` plan the projector **aggregates** the generated rows (it does not loop the single-row create projection): `OpenChequeAgingImpact` is one would-be group (`GroupAppearsInOpenCheques = true`, `OutstandingTotal = Σ row.Value`, `OldestOpenDueDate = min(row.DueDate)`, `OldestOpenBucket` from `ReportAgingBucketizer`, `OpenRowCount = TotalRowCount = rows.Count`, per-row `DaysOutstanding`/`Bucket` by the same rule as `GET /report/cheques/open-aging`); `FiadoBalanceImpact` is one aggregated client delta `Σ(Out ? +row.Value : −row.Value)` when `AccountType = Tab` and a client is set (else empty), closing the parity hole for cheque types that also require Tab account + client; `CashVarianceImpact.VarianceDelta = −Σ NetFlow(Direction, row.Value)` on the plan's `(account, Date)`, with `CurrentVariance`/`ProjectedVariance` live-recomputed for any non-`Draft` close and `DailyCloseStatus` surfaced (`ProjectedVariance` is the variance if current close counts remain unchanged after these cheque rows are recorded). Preview-never-commits is pinned by a WebApi row-count assertion plus determinism tests: open-cheque parity (`GET /report/cheques/open-aging` group matches the preview position-for-position), fiado parity (before/after `GET /report/fiado/balance` delta equals the previewed delta against a non-zero starting balance), cash-variance parity (a real post-write `ICashVarianceCalculator` recompute equals the previewed `ProjectedVariance`), and draft (a committed draft plan does not appear in the open-cheque report).
> v29: Milestone 7 Phase 10 Monthly Reconciliation Report — §6.14 extended with "Monthly reconciliation report" subsection documenting `GET /report/monthly-reconciliation/{year}/{month}` (Manager/Admin). The route accepts years 2000–2100 and months 1–12; out-of-range route values do not match and return 404. The response envelope is `ResponseMonthlyReconciliationJson { Year, Month, LockReady, Days, Blockers }`, with every calendar day in `Days`. Each day includes close rows, Active/Draft/Cancelled transaction counts, and `NetVariance`; close variance is keyed by `(Date, AccountId)` so multiple closes on the same day keep distinct values. `LockReady` is true only when every active close in the month is Approved and there are zero Draft transactions. `Blockers` is a list of structured `ResponseMonthlyReconciliationBlockerJson` items (`Type` ∈ `UnapprovedClose`/`DraftTransactions`, plus `Day`, `DailyCloseId?`, `AccountId?`, `AccountName?`, `CloseStatus?`, `DraftTransactionCount?`) so clients format/localize the text and deep-link to the offending close via the returned ids.
> v30: Milestone 7 close-out (Phase 12) — §6.14 reporting contract shape-stable for downstream code generation (project-wide OpenAPI `required`-metadata hardening tracked separately in the Backend OpenAPI Contract Hardening milestone); preview-never-commits invariant pinned at the architecture level. §6.14 gains an explicit "Time-entry balance summary" subsection documenting `GET /report/timeentry-balance` — the three single-operator resolution paths (Member own / Manager-Admin `operatorId` / `mine`), the Phase 11 addendum **branch-wide all-operator roll-up** (Manager/Admin who name neither `operatorId` nor `mine` → one element per operator with ≥ 1 entry, ordered by name), and the `ResponseTimeEntryBalanceSummaryJson { DateFrom, DateTo, Operators[] }` wrapper shape (window on the wrapper; unlinked Member → empty `Operators`; `REPORT_OPERATOR_ID_REQUIRED` is no longer a returnable cause here). The `[ProducesResponseType]` declarations on all eleven M7 actions were audited against their actual returnable status codes. One contract correction: `GET /report/monthly-reconciliation/{year}/{month}` dropped its `min/max` route constraints (now bare `:int`) so an out-of-range year/month reaches `MonthlyReconciliationFluentValidation` and returns a real 400 `ResponseErrorJson` (`REPORT_YEAR_OUT_OF_RANGE` / `REPORT_MONTH_INVALID`) instead of a body-less framework 404 — the declared `{200,400,401,403}` set is now fully reachable and WebApi-tested. The other ten actions matched their declarations unchanged.
> v31: Milestone 7.5 Frontend UX Contract Gaps close-out — §6.14 extended with "Manager dashboard aggregation" documenting `GET /report/dashboard?date=` (Manager/Admin; `ResponseDashboardJson { Date, TotalVariance, MeanVariance, PendingApprovalCount, Closes, NotSubmitted }`; per-close `VarianceValue?` joined by `(Date, AccountId)`, never by date alone; `Draft` closes surface only in `NotSubmitted` carrying their close id for deep-linking; expected-closer rule = active Terminal accounts with ≥ 1 active OperatorAccount link to an active Operator, primary link surfaced first with operator-name fallback; dates after branch-local today have no expected closers; 400 `REPORT_AS_OF_DATE_INVALID` on a default date; deliberately no 404 path) and "Daily-close review context" documenting `GET /dailyclose/{dailyCloseId}/review` (DailyClose read scope reused: 404 `DAILYCLOSE_NOT_FOUND` on missing/cross-branch before the member-scope 403s; dedicated `ResponseDailyCloseReviewJson` because `ResponseDailyCloseJson` is also a write response; per-item `{ ProductId, ProductName, OpeningValue?, ClosingValue, IsCashVarianceProduct }` ordered by product name; §6.5 opening values from the most recent prior active close for the same `(BranchId, AccountId)` ordered `Date DESC, CreatedAt DESC, Id DESC`, zero when no prior item exists; the `Diferença Caixa` row is flagged system-calculated with `OpeningValue = null`; the branch-scoped prior lookup prevents cross-branch leakage). The M7.5 Phase 3 list-variance gate resolved as a **deferral**: `ResponseListDailyCloseItemJson` intentionally unchanged. The read-surface never-commits architecture scan now also covers `UseCases.DailyCloses.Review`. No schema changes — the ERD is untouched.
> v32: CashVariance Direction-semantics clarification — §6.12 gains a "Direction semantics on terminals" paragraph: `In`/`Out` records value entering/leaving the operator's custody (counted drawer cash + product stock); observed terminal days are almost entirely `Out` rows (cash-to-bank deposits, PIX/card cash-outs, prize/surplus payouts); the exactly-once invariant is stated (each value is a ledger row or a counted asset, never both), while the representation of ordinary product sales (count-only vs product-transaction-explained) is deferred to the Milestone 9.5 semantics audit. Verified against the original Access system: `FrmCaixa.AtualizarDiferenca` computed `DifCaixa = Lançamentos + TotalFinal − TotalInicial` with effectively all-`Negativo` terminal rows (116 of 118 real rows; `lco_valor` stored positive, `lco_sinal` carrying direction — the same value+direction model as this port), so the §6.12 formula and `CashVarianceCalculator` are **confirmed correct and unchanged**. New Access-faithful regression tests pin the convention end-to-end (−R$20 on a realistic all-`Out` day). The presentation's UC1/UC2/UC6 stories were corrected to the outflow reading and the UC6 worked example restored. No contract or schema changes.
> v33: M7.7 Phase 2 (items 2.1–2.5) — §6.5/§6.12/§6.13/§6.14 now define the one-call active-product review projection (`ProductId`, `ProductName`, `DisplayOrder`, `OpeningValue?`, nullable `ClosingValue`, `IsCashVarianceProduct`), the candidate-driven non-persisting `POST /dailyclose/{id}/variance-preview`, and Draft item-save Notes (`max 1000`, empty clears, frozen at submit) guarded by the DailyClose row's PostgreSQL `xmin` `Version`. The breaking `ClosingValue: decimal → decimal?` change is explicitly pinned in emitted OpenAPI. The §6.5 pending marker drops only its now-implemented Notes clause; freeze/recall and paired-Tab gap-A clauses remain pending their M7.7 Phases 3/5. No user-defined database column was added: `xmin` is a PostgreSQL system column, checkpointed only as EF concurrency metadata.
> v34: M7.7 Phase 2 review follow-up — the active-product review projection is unioned with the current close's own active saved items, preserving retired products only when needed to reconcile that close while still omitting prior-only retired products. A Submitted-close request that attempts to change frozen Notes now rejects atomically with 409 `DAILYCLOSE_NOTES_FROZEN`; omission (or the unchanged value) may still accompany the legacy recall until Phase 3 replaces it. `DbUpdateConcurrencyException` translation is scoped to entries whose entity is `DailyClose`, and real PostgreSQL two-context regressions prove both the `xmin` conflict and that a RefreshToken delete race is not mislabeled. The OpenAPI regression now also pins the Phase 2 `Version`, nullable `Notes`, and preview request/response schemas; project-wide `required` emission remains deferred to M7.6.
> v35: Expanded M7.7 Phase 3 (items 3.1–3.13) — freeze-at-submit now covers create/installment/finalize/cancel under one account-wide transaction-scoped advisory lock shared by all eleven ledger/close mutations. DailyClose gains first-count eligibility, distinct opener/immutable-recorder/current-submitter identities, and restricted opening-recheck relations. The first successful item save (including empty) atomically claims the recorder; an unclaimed unlocked close may be claimed by any account-scoped Member, and coordination makes the first claim win. Submit stamps only the current submitter and never reassigns the recorder. Submit also requires a recorded count, no Draft rows, and no intervening active-activity day without a counted close. A real predecessor count change demotes exactly the next eligible official successor to Draft with explicit audit and response metadata. Recall and Reopen are explicit, Rejected/opening-recheck corrections retain their recorder ownership, every ordinary Draft response hides the retained CashVariance row, and official reads use only the persisted snapshot. Open is Terminal/date constrained; Terminal create/installment requires an open close while Tab/Bank remain exempt from that prerequisite. Coordination is stable-keyed, cancellable, and bounded to five seconds with retryable 409.
> v36: M7.7 Phase 4 atomic month lock — `POST /setting/lock-month { Year, Month }` is the only way to advance `Setting.LockDate`; `PUT /setting` rejects `LockDate`. Readiness uses closes in any state plus direct active Terminal ledger activity as the expected-pair set, applies no calendar or activation-window special case, ignores paired-Tab fiado for Terminal expectation, validates every unlocked month from the branch-created floor, and prohibits current/future months. The command and every Phase 3 ledger/close-history writer share a branch advisory boundary in canonical month-before-account order. Monthly reconciliation now uses the same single-month readiness evaluator and reports missing expected closes structurally.
> v37: M7.7 Phase 4 floor-integrity audit — on a fresh `DateTime.MinValue` setting, the initial lock floor is `min(branch-local Branch.CreatedAt month, earliest active DailyClose.Date month, earliest non-soft-deleted Transaction.Date month across every status)`. These discovery reads execute under the exclusive branch month boundary, so participating backdated writes either commit first and are validated or wait and fail their in-scope `LockDate` recheck. Clean pre-creation history is accepted without extra manager work; unresolved earlier history returns `SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED` with its `MM/yyyy` month. No schema change or manager-facing backdate prohibition was added.
> v38: M7.7 Phase 4 findings audit — all five TimeEntry mutation routes (`PUT /timeentry`, whole-entry `DELETE`, and granular segment `POST`/`PUT`/`DELETE`) acquire the shared branch month boundary, reload or resolve their effective date, recheck `TIMEENTRY_DATE_LOCKED`, and commit before releasing it, so payroll-hour corrections cannot bypass or race a successful lock. Month and account coordination have independently configurable five-second timeouts; the settings boundary recognizes nested PostgreSQL `55P03` and returns `SETTING_LOCK_MONTH_COORDINATION_BUSY`. Non-MinValue legacy boundaries are clamped to the data-aware operational floor when lower, while an impossible current/future value written by the retired PUT path is repaired only through a successful whole-interval command. The live report projection, both settings 409 paths, concurrent lock commands, and the Phase 4 OpenAPI surface are pinned against PostgreSQL. Dead `SETTING_LOCK_DATE_RETREAT` live-contract wording/resources were removed; no schema change or migration was required.
> v39: M7.7 Phase 6 (items 6.1–6.8) — transaction lists accept `OriginTransactionId` without widening Member account scope and return each row's `OriginTransactionId` plus `xmin` `Version` for direct guarded sibling actions; cash-variance rows carry their exact `(Date, AccountId)` close id; membership removal atomically clears every Operator login link for that user/branch; `GET /branch/current` exposes one-capture branch-local date/time with an unambiguous `date` plus offset-free branch-wall-clock wire format; transaction and installment creates require persisted, endpoint+branch+user-scoped `Idempotency-Key` handling with canonical typed-body hashing and a 24-hour replay envelope; transaction update/finalize/cancel, DailyClose items, setting update, and month lock share the exact strong quoted-decimal `xmin` `If-Match`/ETag convention; and time-entry lists filter/order in-progress rows before paging. The Phase 6 forward marker in §6.11 is discharged; later Phase 5/7 markers remain.
> v40: M7.7 Phase 7 (items 7.1–7.4, decision 1.6a) — time-entry policy integrity. New §3.20 effective-dated `TimeEntryPolicy` entity (`EffectiveFrom` date + the three time constants, active-unique `(BranchId, EffectiveFrom)`, restricted Branch FK); every §6.7 calculation resolves the row with the greatest `EffectiveFrom` on or before the entry's own date, so changing `DailyTargetHours` or either lunch tier today never rewrites historical balances. Get and List recompute every row — closed rows included — under its entry-date policy, exactly like the balance report, so no read surface can serve a stale persisted checkpoint that disagrees with payroll after a same-day change. `Setting` keeps `LockDate`, is **not** versioned, and its three constants become the current-policy mirror kept in sync by the single `PUT /setting` write path, which appends the policy row effective the branch-local day of the change (a second same-day change mutates that day's row in place) in the same commit that advances the Setting `xmin` ETag. Migration `M7_7Phase7TimeEntryPolicy` deterministically backfills one MinValue-dated initial row per existing branch from its current constants; `CreateBranchSeedFactory` seeds the same initial row for new branches. §6.14's time-entry balance branch-wide roll-up now includes **every active operator** — zero-entry rows return empty `Days` and zeroed totals, with or without a login link (`UserId = null`), and operators deactivated after working inside the window are retained; ordering is unchanged. The "Sem registro" day labelling stays a client-side window-minus-entries derivation — no server operating-day matrix. The §6.7 Phase 7 forward marker is discharged; later Phase 5 markers remain.
> v41: Milestone 7.6 close-out (Phases 1–3) — OpenAPI contract fidelity only. The generated document now derives required/optional from C# nullable metadata across every request body, response body, flattened `[FromQuery]` DTO member, and direct query parameter (read-state for responses, write-state for inputs, with accepted omission defaults preserved), and publishes every contract enum as a named `type: string` component whose values are the canonical declared names — including enums reached only through nullable properties. Responses serialize declared enum names; new §6.15 records both rules. **No endpoint, DTO, persistence, or domain-contract shape changed, and no endpoint's runtime status code changed**; the only behavioural difference is how enum values are spelled on the wire. Emitted metadata did change — that is the milestone — so downstream code generation output differs from v40 even where runtime behaviour is identical. Integer enum input is still accepted during the transition window, and the owning FluentValidation `IsInEnum` rule keeps its localized message for undefined-but-representable values. Two audit corrections landed: `POST /user/login`, `/user/register`, and `/user/renew-token` now declare their `[ProducesResponseType]` metadata, so `ResponseUserLoginJson`, `ResponseUserRegisterJson`, and `ResponseTokenJson` appear in the document at all — three components a generated client previously had no type for. `register`'s **declared** success status was corrected from `200` to the `201` the endpoint has always returned, which is a codegen-visible metadata change; its routes, payloads, and runtime responses are unchanged. A whole-document contract-surface snapshot regression now pins both guarantees, alongside a scan proving no schema anywhere falls back to bare numeric enum values.
> v42: M7.7 Phase 7.5 (items 7.5.1–7.5.3, design gap W) — Work Queue backlog aggregate. §6.14 "Manager dashboard aggregation" documents two additive members on `ResponseDashboardJson`: `Backlog` (`ResponseDashboardBacklogJson { SubmittedCount, RejectedCount, DraftCount, OldestPendingDate? }`) over active closes with `LockDate < Date <` the requested date in `Submitted`/`Rejected`/`Draft` (`Approved` never counts; `OldestPendingDate` is the earliest counted `Date`, `null` when none), and `UnlockedDraftTransactionCount` over non-soft-deleted `Draft` transactions with `LockDate < Date ≤` branch-local today. The two upper bounds differ on purpose: the backlog follows the requested date, the Draft count always runs through today. Each is one repository aggregation scoped to the caller's branch and that branch's own `LockDate` (the same §6.6 boundary the write guards use; a branch with no Setting row is never-locked). Permission (Manager/Admin), validation, the declared status set, and every pre-existing member — the selected-day `Closes`, `PendingApprovalCount`, variance aggregates, and `NotSubmitted` — are unchanged, and no operating-day calendar is introduced. The change is additive under §6.15: both new members and the three counts are `required`, `OldestPendingDate` is optional and nullable, and the contract-surface snapshot was refreshed after review. No schema change — the ERD is untouched beyond its revision metadata.
> v43: Documentation-only — owner decisions from the 2026-10 prototype review recorded as ⚠ *Pending M7.7 change* markers, each discharged by its owning phase's spec sync: fiado operations that change counted cash or stock write an automatically generated, linked Terminal movement with system-owned origin data (§3.12, §6.4, §6.12 — the formula is unchanged and the Tab balance is never added); repayments only through *Receber pagamento*, with a required method and no default; `OldestUnpaidDate` is the oldest unpaid **sale** date (§6.4); the close-day fiado reference is removed for every role, superseding the gap-A sub-decision (§6.5); the operator transaction summary includes generated rows and reports their share (§6.14); a `TransactionTypeId` list filter (§6.10); atomic whole-plan finalization for wholly Draft cheque plans (§6.3); and a durable close review event history — rejection and opening-change events with before/after values and actor display names readable under the close's own read permission (§6.13). New §9 *Questions to ask the product team*. The HTML artifacts carry only the revision bump.
> v44: M7.7 Phase 7.8 (server M11 tripwire, coordination label SV03; Shared M2 handoff H1) — Manager Work Queue rename, ahead of the first generated TypeScript client. `GET /report/dashboard?date=` → `GET /report/manager-queue?date=`; `ResponseDashboardJson` / `ResponseDashboardCloseJson` / `ResponseDashboardNotSubmittedJson` / `ResponseDashboardBacklogJson` → `ResponseManagerQueueJson` / `ResponseManagerQueueCloseJson` / `ResponseManagerQueueNotSubmittedJson` / `ResponseManagerQueueBacklogJson`, and the query DTO `RequestDashboardJson` → `RequestManagerQueueJson`. Every JSON member name, nullability, the Manager/Admin permission, the `REPORT_AS_OF_DATE_INVALID` validation, the declared `{200, 400, 401, 403}` status set, the selected-day facts, and the backlog/Draft-count bounds are unchanged. The old route is removed with no compatibility alias (it now returns 404). The OpenAPI contract-surface snapshot changes by name only. §6.14 "Manager dashboard aggregation" becomes "Manager queue aggregation"; §3.14, §6.12, and the §6.14 official-variance, permission-table, and list-enrichment references say "manager queue"; historical revision notes keep the names they shipped with. No schema, migration, or runtime behavior change. The M11 KPI page, upcoming-settlements view, and monthly report remain deferred to M11.
> v45: Documentation-only — server M11 phase planning. The owner decision of 2026-10-16 (a separate read-only Dashboard that summarizes every manager report with links to each, beside the unchanged exception-first Work Queue) and the proposed contracts are recorded as ⚠ *Pending M11 change* markers in §6.14 and §7.2: `GET /report/management-overview` → `ResponseManagementOverviewJson`, `GET /report/upcoming-settlements`, `GET /report/monthly-financial/{year}/{month}`, an explicit `SettlementInstrument` on TransactionType, additive whole-set totals on existing reports, and deactivated-entity drill-down in the daily ledger, operator summary and time-entry balance — all proposals, not shipped. §6.14 now documents two shipped facts the planning audit found: `GET /report/fiado/balance` returns `TotalOutstanding` as the signed sum of client balances (a client in credit lowers it), and open-cheque aging covers installment plans only, so a single cheque row never appears. §9 gains questions 2–6 (card settlements, single cheques, Tarifa Bolão, bolão/federal leftovers, operator attribution). No endpoint, DTO, schema, or runtime behavior changed.
> v46: M7.7 Phase 5.3 (decision 1.8, gaps L + S) — §3.7's pending marker is discharged. `DELETE /account/{id}` on a Terminal clears its `TabAccountId`, and on a Tab clears every Terminal that holds it, in the same commit as the deactivation, so an inactive account never keeps or reserves a pairing. Pair, unpair, create-with-pairing and deactivate serialize on one branch-scoped advisory lock (bounded by `AccountPairingCoordination:LockTimeoutSeconds`, default 5 s); a timeout returns 409 `ACCOUNT_PAIRING_COORDINATION_BUSY`, now declared on unpair and deactivate as well. Data-only migration `M7_7Phase5StalePairingCleanup` clears pairings where either side was already inactive. Tab (fiado) access is unchanged and confirmed: only an explicit `OperatorAccount(Tab)` grant, never implied by a Terminal assignment, with Tab grants allowed across several terminals. No schema change.
> v47: M7.7 Phase 7.6.1 (gap X) — §6.13's review-event pending marker is discharged. New §3.21 `DailyCloseReviewEvent` / `DailyCloseReviewEventItem` (migration `M7_7Phase76CloseReviewEvents`) and `DailyClose.ReviewHistoryComplete` (false for every close that existed before the migration, true for new ones). `POST /dailyclose/{id}/reject` records a `Rejected` event (time, reviewer user and linked operator, reason) in the same transaction. `PUT /dailyclose/{id}/items` records an `OpeningChanged` event on the one next eligible successor whenever its opening changes — a real count change or a first count that replaces the opening source — including when that successor is already Draft; before-values come from the opening as it stood inside that transaction, never from current mutable items. Actor and product names are snapshots at event time. The one-successor invalidation rule, the `AffectedSuccessor` response (still demotions only) and official snapshots are unchanged. New read `GET /dailyclose/{dailyCloseId}/history` → `ResponseDailyCloseHistoryJson { DailyCloseId, IsHistoryComplete, CurrentOpeningSource?, Events }` with the close's own read permission (Members included); it declares no `ETag`. New enum `DailyCloseReviewEventType`. §8's table count is corrected to 22 (it had not counted the M5–M7.7 tables).
> v48: M7.7 Phase 7.6.2 (gap Y) — §6.3's plan marker is discharged with the owner decisions of 2026-10-16 (Mixed plans finalize their remaining Draft installments only with explicit confirmation; Draft edits stay per-row saves; discard stays per-row cancel). New `GET /transaction/installment/{originTransactionId}` → `ResponseInstallmentPlanJson` with computed `InstallmentPlanStatus` (Draft, Active, Mixed, Cancelled), and `POST /transaction/installment/{originTransactionId}/finalize` with `RequestFinalizeInstallmentPlanJson { Installments: [{ TransactionId, Version }], ConfirmMixedPlan }`: under the plan account's coordination, every Draft installment becomes Active in one commit or none does. New 409 keys `TRANSACTION_PLAN_NOT_DRAFT`, `TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED`, `TRANSACTION_PLAN_STALE_WRITE`, `TRANSACTION_PLAN_FINALIZE_REQUIRED` (per-row finalize of a plan installment), 404 `TRANSACTION_PLAN_NOT_FOUND`, and three 400 validation keys. §6.6 now lists twelve account-coordination participants and §6.11 the plan's multi-row precondition. No schema change; no plan entity.
> v49: M7.7 Phase 7.6.3 (gap Z) — §6.10's type-filter marker is discharged. `RequestListTransactionsJson` gains optional `TransactionTypeId` (`GET /transaction?TransactionTypeId=`), applied in the shared list/count predicate with the other filters before paging, after the unchanged branch and Member account predicates. Retired types and types without rows are filterable; an unknown or cross-branch id returns an empty page, not an error. No new error key, no schema change, no change to list ordering or response shape.
> v50: M7.7 Phase 5 (items 5.1, 5.2, 5.4–5.8; gaps H, A, L routing) — every remaining v43 forward marker (§3.12, §6.4 ×2, §6.5, §6.12, §6.14) is discharged. §6.4 is rewritten to the shipped contract: one SQL netting projection feeds balance, aging and the new `GET /client/{clientId}/fiado-statement`; FIFO is branch/client-wide by `DueDate, Date, Id`, account filters apply after netting, `PaidAt` no longer settles fiado, aging `Value` is the unpaid remainder and aging now uses the balance's `Date ≤ AsOfDate` input. New `POST /transaction/fiado-payment` (*Receber pagamento*, `RequestReceiveFiadoPaymentJson`, idempotent) with a required configured method (`TransactionType.FiadoPaymentKind` CountedCash/NonCash); `RequestCreateTransactionJson.HandsOutCountedStock`; generic creates reject Tab `In` and system types. Migration `M7_7Phase5FiadoLinkedMovements`: `Transactions.GenerationKind` + `GeneratedFromTransactionId` (unique, restrict, paired check), `TransactionTypes.SystemRole` + `FiadoPaymentKind`, and two system types/categories per branch (no origin or method guessed). Generated legs: protected origin on list/detail/create, 409 `TRANSACTION_GENERATED_ROW_READ_ONLY`, coupled Draft/finalize/cancel, both-or-neither under a two-account coordination scope. Operator summary gains `Generated*` subset fields. `ResponseOperatorAccountJson.PairedTabAccountId` gives the workstation's routing target in the operator's own context. §6.5 drops the close-day fiado reference for every role. Review fixes in the same revision: the migration seeds collision-safely and downgrades only what it owns; the create preview validates and projects the whole counted-stock pair and reads aging membership from the FIFO projection; the statement validates its resolved window and pages without offset overflow; the update response keeps `GeneratedTransactionId`. New keys: `CLIENT_FIADO_STATEMENT_ACCESS_DENIED`, `CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID`, `TRANSACTION_TYPE_SYSTEM_ONLY`, `TRANSACTION_FIADO_PAYMENT_REQUIRES_COMMAND`, `TRANSACTION_COUNTED_STOCK_REQUIRES_FIADO_SALE`, `TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED`, `TRANSACTION_FIADO_PAYMENT_METHOD_INVALID`, `TRANSACTION_FIADO_PAYMENT_CLIENT_REQUIRED`, `TRANSACTION_GENERATED_ROW_READ_ONLY`, `TRANSACTION_TYPE_SYSTEM_READ_ONLY`, `TRANSACTION_TYPE_FIADO_PAYMENT_KIND_INVALID`, `TRANSACTION_TYPE_FIADO_PAYMENT_KIND_ENUM_INVALID`, `CATEGORY_SYSTEM_READ_ONLY`.
> v51: M11 Phases 2–3 and M7.7 Phase 8 (contract additions only; no schema change). §6.14 gains additive whole-set fields: open-cheque aging `OutstandingTotal`, `OpenRowCount`, `ByBucket` (new `ResponseAgingBucketTotalJson { Bucket, Value, RowCount }`); cash-variance summary `SubmittedCount`/`ApprovedCount`/`RejectedCount`; monthly reconciliation `LockDate?`, `IsMonthLocked`, `IsMonthElapsed`; fiado balance `DebtorCount`, `TotalDebt`, `CreditorCount`, `TotalCredit` (credit positive; signed `TotalOutstanding` unchanged); FIFO fiado aging `OutstandingTotal`, `ByBucket`, `OldestUnpaidDate?` (oldest unpaid sale date). Manager/Admin drill-downs in the daily ledger (`AccountId`), operator summary and time-entry balance (`OperatorId`) now resolve deactivated branch entities and flag them (`AccountActive`, `OperatorActive`); missing/cross-branch stay 404 and Member/`Mine` scopes are unchanged. The M11 forward marker drops the clauses those phases shipped. §6.7 now states the shipped Admin segment reconcile (id-matched, no deletion through the snapshot, `ClockIn` locked there, granular routes for removal/correction); the presentation's create examples no longer carry `PaidAt`. §5's seed tables now list the two system categories and counterpart types every branch receives since v50 (an omission in v50).
> v52: M7.6 follow-up Phase 4 (Shared M2 handoffs H3/H4; generated-document change only, no HTTP behavior change). §6.15 gains two systemic guarantees. **Response numeric fidelity:** a numeric member reachable from a response is published as `number`/`integer` (nullability and format kept, no `string`, no numeric `pattern`), because the MVC serializer writes JSON numbers; request bodies and query parameters keep `number|string`/`integer|string` because binding accepts numeric strings. The direction comes from the document's own request/response reachability; each response number is checked against the MVC serializer's effective number handling for its actual member, starting from the response type ApiExplorer declares and following nested objects, collections and dictionary values. A component that both a request and a response reach keeps its input form and gains a separate `{id}Output` response representation; responses reference it. **Per-operation token requirement:** the single document-wide `Bearer` requirement is replaced by two named HTTP bearer schemes, `IdentityBearer` and `BranchBearer`, and each operation's `security` is derived from the token filters MVC runs for it: `[]` for the three `[AllowAnonymous]` user operations, `IdentityBearer` for branch create, my-branches and session, and `BranchBearer` for every other operation, listing the admitted roles when `[TokenAuthorize]` restricts them. Token filters wrapped in `[ServiceFilter]` count like the attributes. An `[AllowAnonymous]` action that also carries a token filter, an undeclared action, a mix of identity and branch token filters (whose outcome depends on filter order), an empty-role combination, ASP.NET `[Authorize]` metadata, an opaque filter factory and any unmodeled authorization filter all stop generation. Endpoints, DTOs, statuses, headers, enums, permissions, serialization and persistence are unchanged.
> v53: M8 Phases 1–5 — required caller `UserId` on login; dedicated self-membership error and serialized branch mutations; identity profile/name, confirmed password change, stable-session logout, recovery with hashed expiring single-use capabilities, all-session refresh revocation and normalized email; new PasswordResetToken, RecoveryThrottle, OperatorLinkRequest and Invitation tables; request own/list/resolve and queue pending count; invitation lifecycle, existing-account acceptance and atomic invitation registration/membership/pre-link. SMTP is provider-neutral, disabled by default, with local capture evidence only. Canonical documentation and design semantics are reconciled in this delivery; design review markers intentionally await independent acceptance. Phase 6 legacy public-path closure, Phase 7 Google and Phase 8 operational/provider acceptance remain open.
> v54: M8 Phases 6–7 — platform flag defaults false on legacy Users and is writable only through explicit CLI/database administration. A platform identity may create a seeded branch and pending owner Admin invitation through `POST /platform/onboarding`, read/reissue its onboarding by request id, but receives no branch membership or data access. The same application use case serves the dev/staging CLI; its first-operator command bootstraps an empty migrated database through a hashed, emailed single-use password-setup capability, never a CLI password or public grant. The actual `POST /user/register`, `POST /branch/create` and direct `POST /branch/users` routes are removed. Password login gives the same invalid-credentials response for missing, inactive, wrong-password and passwordless accounts; login abuse throttling remains Phase 8. Google sign-in adds a hashed, expiring single-use state/nonce challenge and a fixed-trust Google ID-token verifier with configured audience, signature/issuer/expiry and verified-email checks. A verified linked subject returns the ordinary identity/refresh envelope; a new Google account exists only through atomic matching-invitation acceptance. Existing legacy password accounts require password proof before first link unless recorded local email proof and Google mailbox authority both exist. New tables are OperatedOnboardings, GoogleLoginChallenges and GoogleIdentities; User adds `IsPlatformOperator` and nullable `EmailVerifiedAt`, with no fabricated backfill. Independent technical and semantic acceptance of Phases 1–7 was recorded by the 2026-10-18 repeat review; production sender/Google project/consent, design 0.4.6 and launch acceptance remain open.
> v55: M9.5 Q3/Q9 opening-side inflow seeds and guarded data migration; provisional Q5 count-only baseline, unchanged formula/API, retained historical-opening limitation and pending independent design reconciliation.
> v56: Server M11 Phases 4–7. **§3.10/§4/§5:** `TransactionType.SettlementInstrument` (`None | Cheque | CreditCard | DebitCard`) and `MonthlyFinancialRole` (`None | BolaoServiceFee | CommercialLeftover`), explicit admin configuration on the create/update/read contracts (update: omitted keeps, explicit `None` clears; a role must match its category direction), read at query time so rename/retirement keep them; migrations `M11Phase4SettlementInstrument` and `M11Phase6MonthlyFinancialRole` leave every existing type `None`; system types guarded `None`; the fresh seed classifies `Depósito Cheque`, both card types, `Tarifa Bolão` and both Sobra rows from the working answers. **§6.14:** open-cheque aging also lists unpaid standalone rows of `Cheque` types as one-row groups (`OriginTransactionId` now nullable; `TotalCount` = open groups; one batched row read per page); new `GET /report/upcoming-settlements`, `GET /report/monthly-financial/{year}/{month}` and `GET /report/management-overview` (one clock read, one read-only `REPEATABLE READ` snapshot, atomic failure, `OverviewSectionStatus`); the M11 forward markers in §6.14/§7.2 are discharged; §9 Q2–Q6 name their consumers. The manager-queue, monthly-reconciliation, time-entry-balance and cash-variance computations moved into shared readers with unchanged behavior. New error keys: `TRANSACTION_TYPE_SETTLEMENT_INSTRUMENT_INVALID`, `TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_INVALID`, `TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_DIRECTION_INVALID`, `REPORT_SETTLEMENT_INSTRUMENT_INVALID`, `REPORT_MONTH_IN_FUTURE`, `REPORT_YEAR_MONTH_PAIR_REQUIRED`. Design markers stay v55 until independent semantic acceptance.
> v57: Server M8 Phase 8 local subset (login abuse protection). **§3.29** new `LoginThrottle` table (migration `M8Phase8LoginThrottles`: hash-only key, account key, positive failure count, window start, optional block end, expiry; unique key, account and expiry indexes) and a `RecoveryThrottles.NextAllowedAt` index. **§6.16** `POST /user/login` passes a process-local request limit per client network (default 30/min) and then, inside the existing email lock and before the account is read or BCrypt runs, two persistent failed-check budgets: 5 per (account, client network) and 20 per account within 15 minutes, each blocking for 15 minutes (all configurable and validated at startup). Missing, inactive, passwordless and existing accounts count and are refused identically; a refused attempt mints nothing, even with the right password. Success clears only that network's budget; password reset/change clear the account's. The Google first-link password proof spends the same budgets (§6.18). Client network = peer IPv4 address or IPv6 /64, with `X-Forwarded-For` honored only from configured trusted proxies; the recovery and capability limiters share it. **§6.15** every 429 declares a required integer `Retry-After`; login declares 429. **§3.23** expired login and elapsed recovery throttle rows are swept in bounded `SKIP LOCKED` batches. §8's table count is corrected to 30 (it had not counted the three v54 tables). No other DTO, status or permission changed.

> v13: Extended §6.11 with Draft → Active finalization rules, reusing the same member account scope, mutation permission matrix, lock-date behavior, and update audit convention.
> v14: Extended §6.11 with the cancellation contract: required cancellation reason, terminal `Cancelled` state from `Draft` or `Active`, dedicated cancellation audit fields stamped from the same clock instant as the generic update audit fields, installment-sibling isolation, and exclusion of cancelled rows from active sums.
> v15: Added DailyClose/DailyCloseItem audit and uniqueness details, the DailyClose workflow contract including `Rejected -> Draft` and same-day `Submitted -> Draft` recall, most-recent-prior-close opening values, lock-date coverage for all DailyClose transitions, explicit CashVariance direction handling, and the system-only `"Diferença Caixa"` product invariant.
> v16: Closed the DailyClose workflow hardening pass: locked the Open enforcement order, clarified guard-owned versus account-scope/unique-constraint rules, and confirmed CashVariance is updated in place across rejected resubmits and submitted recall cycles.

---

## Table of Contents

1. [Domain Overview](#1-domain-overview)
2. [Multi-Tenancy Model](#2-multi-tenancy-model)
3. [Entity Reference](#3-entity-reference)
4. [Enums](#4-enums)
5. [Seed Data](#5-seed-data)
6. [Business Rules](#6-business-rules)
7. [Key Workflows](#7-key-workflows)
8. [Access-to-Lotero Mapping](#8-access-to-lotero-mapping)
9. [Product answers, provenance and remaining questions](#9-product-answers-provenance-and-remaining-questions)

---

## 1. Domain Overview

A *casa lotérica* is a privately-owned branch that operates under contract with Caixa Econômica Federal (CEF), Brazil's federal bank. It processes lottery sales, bill payments, cash deposits, transfers, and other financial services on behalf of CEF.

### The core problem

Every day, each operator (cashier) processes dozens of financial transactions at their terminal. At the end of the day, the operator must close their register: count what is physically in the drawer, compare it to what the system says should be there, and report the difference. The owner then reconciles this against CEF's own settlement report (*borderô*).

This is currently done on paper + Microsoft Access. Lotero digitizes the entire flow.

### The two parallel systems

The system tracks the same financial reality from two angles:

**The transaction ledger** (`Transaction` table): every individual financial event — "R$2,000 cash deposit at 12:30", "R$560 credit card payment", "R$50 client tab". This is the analytical, auditable record. It answers: *what happened, when, how much, to whom, through which account.*

**The daily closing snapshot** (`DailyClose` + `DailyCloseItem` tables): the physical count of what is in the drawer at end of day — how much cash, how many Telesena tickets worth how much, Raspadinha scratch cards, etc. This is the reconciliation record. It answers: *what do we actually have right now, and does it match the ledger?*

The **CashVariance** (Diferença de Caixa) is the gap between expected (ledger) and actual (snapshot). This is the single most important number in the system — the owner reviews it daily for each operator.

### The Fiado (tab/credit) subsystem

Regular customers can buy on credit ("fiado"). In branches that use fiado, a terminal may have a paired **Tab account** that tracks credit separately from the cash drawer. Some branches may not use Tab accounts at all, and a terminal may exist without one until fiado is configured. Selling on credit records an Out transaction on the Tab account. When the customer pays, that is an In transaction on the same Tab account. The balance is the outstanding credit. This separation prevents credit from distorting the daily cash reconciliation.

### The three-date model

Every transaction has three dates that track its full lifecycle:

- **Date** (`Date`): when the financial event occurred
- **DueDate** (`DueDate`): when payment is expected (same day for cash/PIX, +1 for debit card, +2 business days for credit card, custom for checks)
- **PaidAt** (`PaidAt`): when money actually arrived/left — `null` means unpaid

This enables receivables tracking ("money expected this week"), aging reports, and reconciliation with CEF settlement reports.

> *On the name:* **Lotero** is drawn from *lotérica* — the Portuguese word for a lottery house — and mirrors *lotero*, the Spanish term for a lottery-ticket seller. It names the operator behind the counter, the person this system is built for, rather than the lottery draw itself.

---

## 2. Multi-Tenancy Model

Lotero is designed as a **SaaS platform** where different owners operate independent lottery houses.

### Isolation boundary

`Branch` is the tenant. Every business entity carries a `BranchId` foreign key. Data isolation is enforced at the query level — every repository query filters by the authenticated user's branch context.

### User model

- **User** is global (authentication only): email, password, name
- **BranchUser** is the junction: ties a User to a Branch with a specific Role
- A single User can belong to multiple Branches (e.g., owner manages 2 lottery houses)
- **Operator** is the employee concept within a Branch, linked to a User but separable (operator can exist without login, historical data persists after user deactivation)
- **Account** is purely financial — terminal drawers, bank accounts, tab accounts

```
User (global auth)
 └── BranchUser (role per branch)
      └── Branch
           ├── Operator (employee)
           │    └── OperatorAccount → Account
           ├── Account (financial)
           ├── Transaction
           ├── Client
           ├── DailyClose
           ├── TimeEntry
           ├── Category
           ├── TransactionType
           ├── Product
           ├── Holiday
           └── Setting
```

### Why separate User, Operator, and Account

| Concern | Entity | Reason |
|---|---|---|
| Login/auth | User | Admin/manager may have no terminal account |
| Employee | Operator | Historical records survive user deactivation; operator can be linked to multiple accounts |
| Financial | Account | Accounts can be reassigned between operators; reporting by person stays separate from accounting by account |

---

## 3. Entity Reference

### Conventions

- All entities inherit from `EntityBase` (Id, CreatedAt, Active)
- All monetary values: `decimal` mapped to `numeric(14,2)` in PostgreSQL
- All IDs: `Guid`
- All timestamps: `DateTime` in UTC
- Tenant-scoped entities carry `BranchId`
- Soft delete via `Active = false` on EntityBase
- Transaction cancellation has dedicated fields (not just Active flag)

### 3.1 EntityBase

```csharp
public class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;
}
```

### 3.2 Branch

The tenant — one per lottery house.

```csharp
public class Branch : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Cnpj { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }

    // Navigation
    public ICollection<BranchUser> BranchUsers { get; set; } = [];
    public ICollection<Account> Accounts { get; set; } = [];
    public ICollection<Operator> Operators { get; set; } = [];
}
```

| Column    | Type         | Null     | Notes                                               |
|-----------|--------------|----------|-----------------------------------------------------|
| Id        | uuid         | PK       |                                                     |
| Name      | varchar(255) | NOT NULL | Display name: "Lotérica Centro", "Lotérica Asa Sul" |
| Cnpj      | varchar(18)  | NULL     | Brazilian company ID                                |
| Address   | text         | NULL     |                                                     |
| Phone     | varchar(20)  | NULL     |                                                     |
| CreatedAt | timestamptz  | NOT NULL |                                                     |
| Active    | boolean      | NOT NULL | Default true                                        |

### 3.3 User

Global authentication identity. No BranchId.

```csharp
public class User : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsPlatformOperator { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    // Navigation
    public ICollection<BranchUser> BranchUsers { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
```

| Column    | Type         | Null     | Notes  |
|-----------|--------------|----------|--------|
| Id        | uuid         | PK       |        |
| Name      | varchar(255) | NOT NULL |        |
| Email     | varchar(255) | NOT NULL | UNIQUE; Unicode trim + database-consistent lowercase canonicalization |
| Password  | varchar(255) | NOT NULL | Hashed for local-password users; empty for invitation-created Google-only users |
| IsPlatformOperator | boolean | NOT NULL | Defaults false, including all pre-migration users; CLI/database only |
| EmailVerifiedAt | timestamptz | NULL | Recorded proof only, no blanket legacy backfill |
| CreatedAt | timestamptz  | NOT NULL |        |
| Active    | boolean      | NOT NULL |        |

### 3.4 RefreshToken

Server-generated refresh credentials retain stable `SessionId` across rotation. Login replaces the user's active refresh authority with a new session; renewal revokes the previous value and issues a new value in the same session. Refresh credentials expire seven days after their own CreatedAt (rotation starts a new credential lifetime). Revoked values remain inactive so logout of the contracted original credential also revokes a concurrently renewed successor. Reads for renewal require `Active`; history is never reusable for authentication. `Value` is unique varchar(128), generated from 32 random bytes; `(UserId, SessionId)` is indexed. Password change/reset revoke **every active session**, regardless of the current one-active-refresh convention. The migration backfills each legacy row's `SessionId` from its own `Id`. Retention/cleanup policy belongs to M8 Phase 8; access/identity JWTs expire naturally.

```csharp
public class RefreshToken : EntityBase
{
    public Guid SessionId { get; set; }
    public string Value { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
```

### 3.5 BranchUser

Junction: a User's role within a specific Branch.

```csharp
public class BranchUser : EntityBase
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public Role Role { get; set; } = Role.Member;
}
```

| Column    | Type        | Null     | Notes                              |
|-----------|-------------|----------|------------------------------------|
| Id        | uuid        | PK       |                                    |
| UserId    | uuid        | NOT NULL | FK → User                          |
| BranchId  | uuid        | NOT NULL | FK → Branch                        |
| Role      | smallint    | NOT NULL | Enum: Admin=0, Manager=1, Member=2 |
| CreatedAt | timestamptz | NOT NULL |                                    |
| Active    | boolean     | NOT NULL |                                    |

**Unique constraint:** `(UserId, BranchId)` — a user has one role per branch. Inactive membership can reactivate in place under authorized membership/invitation semantics. A caller cannot change their own role (even to the same role) or remove their own membership: 409 `BRANCH_USER_SELF_MEMBERSHIP_CONFLICT`, distinct from `BRANCH_USER_LAST_ADMIN_CONFLICT`. Branch mutations serialize and reread current actor membership/role under the same PostgreSQL transaction; mutual removals/demotions cannot eliminate the final active Admin. Removal atomically clears matching operator login links while preserving employee history and account assignments.

### 3.6 Operator

The employee at a branch. Linked to a User for login, but exists independently for historical integrity.

```csharp
public class Operator : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    // Navigation
    public ICollection<OperatorAccount> OperatorAccounts { get; set; } = [];
    public ICollection<TimeEntry> TimeEntries { get; set; } = [];
}
```

| Column    | Type         | Null     | Notes                                                           |
|-----------|--------------|----------|-----------------------------------------------------------------|
| Id        | uuid         | PK       |                                                                 |
| Name      | varchar(255) | NOT NULL | "Lenna", "Jennifer", "Tracy"                                    |
| BranchId  | uuid         | NOT NULL | FK → Branch                                                     |
| UserId    | uuid         | NULL     | FK → User. Null = no login (former employee, or not yet set up) |
| CreatedAt | timestamptz  | NOT NULL |                                                                 |
| Active    | boolean      | NOT NULL |                                                                 |

**Unique active user-link constraint:** `(BranchId, UserId) WHERE UserId IS NOT NULL AND Active = true` — a user can have at most one active linked Operator per branch. Multiple terminal/account access is represented through `OperatorAccount`, not through multiple active Operator rows for the same user. Operators without a login keep `UserId = null` and are not constrained by this index.

**Login-link clearing:** `PUT /operator/{id}` with `UserId = null` intentionally clears the login link while preserving the active `Operator` row for history, reporting, and account assignment continuity. This does not deactivate or delete the employee record.

Removing a branch membership applies the same rule automatically: the membership deactivation and every matching `Operator.UserId = null` update for that `(UserId, BranchId)` commit atomically. Clearing was chosen instead of exposing a stale link because Operator is the durable employee/audit identity while User membership is login authority; historical rows and account assignments remain attached to the Operator, but no employee row can continue to imply a branch login after that login is removed.

### 3.7 Account

A financial account: terminal drawer, bank account, or tab (fiado) account.

```csharp
public class Account : EntityBase
{
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? Number { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Self-reference: terminal → its tab account
    public Guid? TabAccountId { get; set; }
    public Account? TabAccount { get; set; }

    // Navigation
    public ICollection<OperatorAccount> OperatorAccounts { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<DailyClose> DailyCloses { get; set; } = [];
}
```

| Column       | Type         | Null     | Notes                                                                                   |
|--------------|--------------|----------|-----------------------------------------------------------------------------------------|
| Id           | uuid         | PK       |                                                                                         |
| Type         | smallint     | NOT NULL | Enum: Terminal=0, BankAccount=1, Tab=2                                                  |
| Name         | varchar(255) | NOT NULL | "Lenna", "CEF 1292", "Tab Lenna"                                                        |
| Institution  | varchar(255) | NULL     | "Lotérica" for terminals, "Caixa Econômica" for bank accounts                           |
| Number       | varchar(50)  | NULL     | Terminal number "1","2","3" or bank account number                                      |
| BranchId     | uuid         | NOT NULL | FK → Branch                                                                             |
| TabAccountId | uuid         | NULL     | FK → Account (self). Only for Terminal type → points to its optional paired Tab account |
| CreatedAt    | timestamptz  | NOT NULL |                                                                                         |
| Active       | boolean      | NOT NULL |                                                                                         |

**Example data (for a branch where fiado is enabled on all three terminals):**

| Name         | Type        | TabAccountId     | Institution     | Number     |
|--------------|-------------|------------------|-----------------|------------|
| Lenna        | Terminal    | → "Tab Lenna"    | Lotérica        | 1          |
| Jennifer     | Terminal    | → "Tab Jennifer" | Lotérica        | 2          |
| Tracy        | Terminal    | → "Tab Tracy"    | Lotérica        | 3          |
| Tab Lenna    | Tab         | null             | Lotérica        | 4          |
| Tab Jennifer | Tab         | null             | Lotérica        | 5          |
| Tab Tracy    | Tab         | null             | Lotérica        | 6          |
| CEF 1292     | BankAccount | null             | Caixa Econômica | 5780706014 |
| CEF 4995     | BankAccount | null             | Caixa Econômica | 5780706014 |

**TabAccountId invariants** (enforced at service layer and/or DB check constraints):
- Only `Terminal` accounts may have a non-null `TabAccountId`
- A Terminal may exist with `TabAccountId = null`
- The referenced account must be of type `Tab`
- A Tab account can belong to at most one Terminal (unique constraint on `TabAccountId` where not null)

**Account create/pair flow** (application layer):
- `CreateBankAccount` creates standalone Bank accounts
- `CreateTerminalAccount` creates standalone Terminals, Terminals linked to an existing Tab, or Terminals with a new Tab created in the same request
- When `CreateTerminalAccount` creates a new Tab in the same request, the new Tab starts with the same `Name`, `Institution`, and `Number` as the Terminal
- `CreateTabAccount` creates a new Tab for an existing Terminal that does not yet have one
- Pairing existing Terminal/Tab accounts is explicit
- Unpairing an existing Terminal/Tab association is explicit

**Pairing integrity and access** (M7.7 Phase 5.3, decision 1.8):
- Pairing is **terminal-centric**: the Tab belongs to its Terminal through `TabAccountId`; there is no reverse column.
- **Deactivation unpairs atomically.** `DELETE /account/{id}` on a Terminal clears its `TabAccountId`; on a Tab it clears every Terminal that holds it. The pairing change and the deactivation commit together, so an inactive account never keeps or reserves a pairing, and the released Tab (or the still-active Terminal) can be paired again.
- **Pairing writes serialize per branch.** Pair, unpair, create-with-pairing (`CreateTerminalAccount`, `CreateTabAccount`) and deactivate take one branch-scoped transaction advisory lock before reading the accounts, so a pair racing a deactivation can never leave an inactive account paired, whichever runs first. Waiting is bounded by `AccountPairingCoordination:LockTimeoutSeconds` (default 5 s); a timeout returns 409 `ACCOUNT_PAIRING_COORDINATION_BUSY` and changes nothing.
- **The workstation's routing target is readable in the operator's own context.** `ResponseOperatorAccountJson.PairedTabAccountId` (on `GET /operator/self-context` and the assignment reads) names a Terminal's paired Tab and is null for every other account. It is routing information only: recording fiado on that Tab still needs it among the operator's own explicit grants (M7.7 Phase 5.6).
- **Tab (fiado) access is an explicit grant.** A Member reaches a Tab only through an active `OperatorAccount(Tab)` row; a Terminal assignment grants till operation only and never implies its paired Tab. An operator may hold Tab grants across several terminals.
- Migration `M7_7Phase5StalePairingCleanup` (data-only) cleared every pairing in which either side was already inactive before this rule shipped.

### 3.8 OperatorAccount

Junction: which accounts an operator can use.

```csharp
public class OperatorAccount : EntityBase
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public bool IsPrimary { get; set; } = false;
}
```

| Column     | Type        | Null     | Notes                                                      |
|------------|-------------|----------|------------------------------------------------------------|
| Id         | uuid        | PK       |                                                            |
| OperatorId | uuid        | NOT NULL | FK → Operator                                              |
| AccountId  | uuid        | NOT NULL | FK → Account                                               |
| IsPrimary  | boolean     | NOT NULL | Default false. True = this is the operator's main terminal |
| CreatedAt  | timestamptz | NOT NULL |                                                            |
| Active     | boolean     | NOT NULL |                                                            |

**Unique constraint:** `(OperatorId, AccountId)`

**IsPrimary invariant:** At most one `IsPrimary = true` row per operator. Enforced via a unique filtered index: `UNIQUE (OperatorId) WHERE IsPrimary = true`.

### 3.9 Category

Top-level transaction classification. Determines the default direction (money in vs money out).

```csharp
public class Category : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public Direction DefaultDirection { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<TransactionType> TransactionTypes { get; set; } = [];
}
```

| Column           | Type         | Null     | Notes                                            |
|------------------|--------------|----------|--------------------------------------------------|
| Id               | uuid         | PK       |                                                  |
| Name             | varchar(255) | NOT NULL | "Receita", "Saídas", "Despesas Comerciais", etc. |
| DefaultDirection | smallint     | NOT NULL | Enum: In=0, Out=1                                |
| BranchId         | uuid         | NOT NULL | FK → Branch                                      |
| CreatedAt        | timestamptz  | NOT NULL |                                                  |
| Active           | boolean      | NOT NULL |                                                  |

**Unique constraint:** `(BranchId, Name) WHERE Active = true`

### 3.10 TransactionType

Subtype within a category. This is what the operator selects when creating a transaction.

```csharp
public class TransactionType : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public SettlementRule SettlementRule { get; set; }
    public bool RequiresTabAccountAndClient { get; set; }

    // M7.7 Phase 5: system-owned counterpart types and configured fiado repayment methods
    public TransactionTypeSystemRole? SystemRole { get; init; }
    public FiadoPaymentKind? FiadoPaymentKind { get; set; }

    // M11 Phases 4 and 6: report classification, read at query time
    public SettlementInstrument SettlementInstrument { get; set; } = SettlementInstrument.None;
    public MonthlyFinancialRole MonthlyFinancialRole { get; set; } = MonthlyFinancialRole.None;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = [];
}
```

| Column                      | Type         | Null     | Notes                                                                                             |
|-----------------------------|--------------|----------|---------------------------------------------------------------------------------------------------|
| Id                          | uuid         | PK       |                                                                                                   |
| Name                        | varchar(255) | NOT NULL | "Depósito Dinheiro", "PIX", "Cartão de Crédito", etc.                                             |
| SettlementRule              | smallint     | NOT NULL | Enum: SameDay=0, NextCalendarDay=1, NextBusinessDay=2, TwoBusinessDays=3, OperatorEnteredCheque=4 |
| RequiresTabAccountAndClient | boolean      | NOT NULL | When true, transaction creation requires `Account.Type == Tab` and `ClientId != null`             |
| SystemRole                  | smallint     | NULL     | Enum: FiadoCountedStockSaleCounterpart=1, FiadoCashRepaymentCounterpart=2. Non-null only on the two system-owned types |
| FiadoPaymentKind            | smallint     | NULL     | Enum: CountedCash=1, NonCash=2. Non-null marks a fiado repayment method for *Receber pagamento* |
| SettlementInstrument        | smallint     | NOT NULL | Enum: None=0, Cheque=1, CreditCard=2, DebitCard=3. Receivable classification (M11 Phase 4); default 0 |
| MonthlyFinancialRole        | smallint     | NOT NULL | Enum: None=0, BolaoServiceFee=1, CommercialLeftover=2. Monthly financial report marker (M11 Phase 6); default 0 |
| CategoryId                  | uuid         | NOT NULL | FK → Category                                                                                     |
| CreatedAt                   | timestamptz  | NOT NULL |                                                                                                   |
| Active                      | boolean      | NOT NULL | Soft-disable without deleting                                                                     |

**Note:** TransactionType inherits BranchId through its Category. No direct BranchId column needed.

**Unique constraint:** `(CategoryId, Name) WHERE Active = true`

**Important:** The same name can exist under different categories. "Cliente" exists under both "Entradas" (In) and "Saídas" (Out) — this is how the Fiado system distinguishes credit sales from client payments.

**System-owned counterpart types (M7.7 Phase 5.6).** Every branch has exactly two types with a non-null `SystemRole`: *Saída de estoque contado — venda fiado* (`FiadoCountedStockSaleCounterpart`, under the Out category *Movimentos automáticos de fiado (saída)*) and *Entrada de dinheiro — recebimento fiado* (`FiadoCashRepaymentCounterpart`, under the In category *Movimentos automáticos de fiado (entrada)*). `CreateBranchSeedFactory` creates them for a new branch and migration `M7_7Phase5FiadoLinkedMovements` for every existing branch, always in **new** category rows: an existing category is never adopted, a branch that already has an *active* category with the default name gets the name suffixed `— sistema {id prefix}` (so the active-name unique index cannot fail), and a branch already holding a system type of a role is skipped. The migration's downgrade removes only the system types and the categories that host them alone; every pre-existing category, active or inactive, survives. They classify generated Terminal legs only: no manual write may use them (409 `TRANSACTION_TYPE_SYSTEM_ONLY`), they cannot be updated or deactivated (409 `TRANSACTION_TYPE_SYSTEM_READ_ONLY`), and their categories cannot be deactivated or receive new types (409 `CATEGORY_SYSTEM_READ_ONLY`). They are listed by `GET /transaction-type` with their `SystemRole`, so clients can filter by them but never offer them for create.

**Fiado repayment methods (M7.7 Phase 5.1).** A repayment method is an `In`-category type with `RequiresTabAccountAndClient = true` and a configured `FiadoPaymentKind`, set through the transaction-type create/update requests (any other type → 409 `TRANSACTION_TYPE_FIADO_PAYMENT_KIND_INVALID`; check constraint: never both `SystemRole` and `FiadoPaymentKind`). `CountedCash` means the money enters the paired terminal's counted drawer; `NonCash` (PIX is one example) means it does not. The kind is configuration, never inferred from a name or category, and no kind is seeded or migrated: the legacy "Cliente" (Entradas) type is not a method until a manager configures it.

**Report classification (M11 Phases 4 and 6).** Two explicit, admin-configured markers tell the reports what a type's rows are; neither is ever inferred from the name, category, direction or `SettlementRule`, and neither is copied onto rows — every report classifies a row by its type's **current** value, active or retired.
- `SettlementInstrument` (`None | Cheque | CreditCard | DebitCard`) marks a receivable for upcoming settlements and the standalone-cheque part of open-cheque aging (§6.14). It is orthogonal to `SettlementRule` (timing): installment plans keep requiring `OperatorEnteredCheque` and are not also required to carry `Cheque`.
- `MonthlyFinancialRole` (`None | BolaoServiceFee | CommercialLeftover`) marks the monthly financial report's fee and leftover figures (§6.14). `BolaoServiceFee` requires an `In` category and `CommercialLeftover` an `Out` category (409 `TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_DIRECTION_INVALID`), so a role's rows always share one direction.
- Both travel on the create/update/read contracts. On create, omitted means `None`; on `PUT /transaction-type/{id}` an omitted value keeps the current one (so a client that only renames never clears it) and an explicit `None` clears it; undeclared values → 400 `TRANSACTION_TYPE_SETTLEMENT_INSTRUMENT_INVALID` / `TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_INVALID`. Rename and retirement keep them; retired types cannot be edited. A change re-labels every past row in those reports (locked months included) without changing any row, balance, variance, close, lock or plan. System-owned types stay `None` (check constraints `CK_TransactionTypes_SystemRole_NoSettlementInstrument` / `…_NoMonthlyFinancialRole`).
- Migrations `M11Phase4SettlementInstrument` and `M11Phase6MonthlyFinancialRole` leave every existing type `None`; only a fresh branch's seed classifies its own rows (§5). Existing branches configure their types; until then the reports state that nothing is configured instead of reporting zero. A type retired before it was classified cannot be classified (no reactivation path exists).

### 3.11 Client

A customer of the lottery house.

```csharp
public class Client : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Cep { get; set; }
    public string? Address { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? PhoneSecondary { get; set; }
    public string? Notes { get; set; }
    public string? Email { get; set; }

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<Transaction> Transactions { get; set; } = [];
}
```

| Column         | Type         | Null     | Notes                                               |
|----------------|--------------|----------|-----------------------------------------------------|
| Id             | uuid         | PK       |                                                     |
| Name           | varchar(255) | NOT NULL |                                                     |
| Cpf            | varchar(11)  | NULL     | Brazilian personal ID (normalized digits, 11 chars) |
| Cep            | varchar(9)   | NULL     | Postal code                                         |
| Address        | text         | NULL     |                                                     |
| Phone          | varchar(20)  | NOT NULL | Primary phone                                       |
| PhoneSecondary | varchar(20)  | NULL     |                                                     |
| Notes          | text         | NULL     | Freeform observations                               |
| Email          | varchar(255) | NULL     |                                                     |
| BranchId       | uuid         | NOT NULL | FK → Branch                                         |
| CreatedAt      | timestamptz  | NOT NULL |                                                     |
| Active         | boolean      | NOT NULL |                                                     |

**Unique constraint:** `(BranchId, Cpf) WHERE Cpf IS NOT NULL AND Active = true` — at most one active client per CPF per branch. Prevents duplicate customer records while allowing the CPF field to remain optional.

### 3.12 Transaction

The central ledger. One row per financial event. This is the most important table in the system.

**Generated rows (M7.7 Phase 5.6/5.7).** A fiado operation that changes counted cash or stock also writes a **system-generated Terminal transaction** linked to its Tab source (§6.4). The generated row carries structured, system-owned origin data — `GenerationKind` (`FiadoCountedStockSale` or `FiadoCashRepayment`) and `GeneratedFromTransactionId` (the Tab source) — that no request can set: the create and payment DTOs have no such fields and forged JSON members are ignored. Both columns are null on every ordinary row, a check constraint keeps them null or non-null together, and a filtered unique index allows at most one generated row per source. The generated row keeps the initiating audit identity (`CreatedByUserId`, `RecordedByOperatorId`), distinct from its generation origin. It is never updated, finalized, cancelled or edit-previewed on its own (409 `TRANSACTION_GENERATED_ROW_READ_ONLY`); its source carries it. List and detail responses expose `GenerationKind` and `GeneratedFromTransactionId`, and detail, create, finalize and cancel responses of a source expose `GeneratedTransactionId`, so clients never infer the link from the description or type name. Each id is a navigation hint only: reading either row applies that row's own branch and account scope. Installment-plan rows are not generated rows and keep their own lifecycle; no historical row was given an origin by migration.

```csharp
public class Transaction : EntityBase
{
    // Core
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string? Description { get; set; }
    public TimeOnly? TransactionTime { get; set; }

    // Classification — TransactionType is the source of truth.
    // CategoryId and Direction are denormalized: set automatically from
    // TransactionType.CategoryId and Category.DefaultDirection at creation,
    // never independently editable.
    public Guid TransactionTypeId { get; set; }
    public TransactionType TransactionType { get; set; } = null!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Direction Direction { get; set; }

    // Account
    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    // Client (optional)
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    // Payment lifecycle
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    // Installment linking — all installments in a group share the same OriginTransactionId,
    // including the first (which points to itself).
    public Guid? OriginTransactionId { get; set; }
    public Transaction? OriginTransaction { get; set; }

    // System-owned generation origin (M7.7 Phase 5.7) — never bound from a request
    public TransactionGenerationKind? GenerationKind { get; init; }
    public Guid? GeneratedFromTransactionId { get; init; }
    public Transaction? GeneratedFromTransaction { get; set; }

    // Authorship
    public Guid RecordedByOperatorId { get; set; }
    public Operator RecordedByOperator { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    // Status
    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    // Cancellation (soft delete with audit)
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? CancellationReason { get; set; }

    // Tenant
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // PostgreSQL xmin system column; not a user-defined table column.
    public uint Version { get; set; }
}
```

| Column               | Type          | Null     | Notes                                                                                                                        |
|----------------------|---------------|----------|------------------------------------------------------------------------------------------------------------------------------|
| Id                   | uuid          | PK       |                                                                                                                              |
| Date                 | date          | NOT NULL | When the financial event occurred                                                                                            |
| Value                | numeric(14,2) | NOT NULL | Always positive                                                                                                              |
| Description          | varchar(500)  | NULL     | Freeform notes. For credit card: "Parcelado 3x" or "à vista"                                                                 |
| TransactionTime      | time          | NULL     | Time of day (used by cash deposits and other time-sensitive entries)                                                         |
| TransactionTypeId    | uuid          | NOT NULL | FK → TransactionType. **Source of truth for classification**                                                                 |
| CategoryId           | uuid          | NOT NULL | FK → Category. **Denormalized** — always equals TransactionType.CategoryId, set at creation, never independently editable    |
| Direction            | smallint      | NOT NULL | Enum: In=0, Out=1. **Denormalized** — always equals Category.DefaultDirection, set at creation, never independently editable |
| AccountId            | uuid          | NOT NULL | FK → Account                                                                                                                 |
| ClientId             | uuid          | NULL     | FK → Client. Only for client-related transactions                                                                            |
| DueDate              | date          | NOT NULL | When payment is expected                                                                                                     |
| PaidAt               | timestamptz   | NULL     | When actually paid. NULL = unpaid                                                                                            |
| OriginTransactionId  | uuid          | NULL     | FK → Transaction (self). All installments in a group share the same value, including the first (self-reference)              |
| GenerationKind       | smallint      | NULL     | Enum: FiadoCountedStockSale=1, FiadoCashRepayment=2. Non-null only on a system-generated Terminal leg                         |
| GeneratedFromTransactionId | uuid    | NULL     | FK → Transaction (self, restrict). The Tab source of a generated leg; unique where not null                                  |
| RecordedByOperatorId | uuid          | NOT NULL | FK → Operator. Which operator's context (terminal) this transaction belongs to                                               |
| CreatedByUserId      | uuid          | NOT NULL | FK → User. Who actually created this record (may differ from operator for manager corrections)                               |
| Status               | smallint      | NOT NULL | Enum: Draft=0, Active=1, Cancelled=2                                                                                         |
| CancelledAt          | timestamptz   | NULL     |                                                                                                                              |
| CancelledByUserId    | uuid          | NULL     | FK → User                                                                                                                    |
| CancellationReason   | varchar(500)  | NULL     |                                                                                                                              |
| BranchId             | uuid          | NOT NULL | FK → Branch                                                                                                                  |
| Version              | xid           | NOT NULL | PostgreSQL `xmin` system column, exposed as `uint`; optimistic-concurrency token                                            |
| CreatedAt            | timestamptz   | NOT NULL |                                                                                                                              |
| Active               | boolean       | NOT NULL | EntityBase default. Redundant with Status for transactions but kept for consistency                                          |

**Nullable columns analysis** (10 nullable content columns):
- `Description` — not all transaction types use it
- `TransactionTime` — only cash deposits and similar need time tracking
- `ClientId` — only client-related transactions
- `PaidAt` — null = unpaid (meaningful null)
- `OriginTransactionId` — only installments (non-null for all members of a group, including the first)
- `GenerationKind`, `GeneratedFromTransactionId` — only system-generated Terminal legs (both or neither)
- `CancelledAt` — only cancelled transactions
- `CancelledByUserId` — only cancelled transactions
- `CancellationReason` — only cancelled transactions

**Key indexes:**
- `(BranchId, Date, AccountId)` — the primary query pattern (daily ledger per operator)
- `(BranchId, AccountId, Direction, Date)` — Fiado balance calculation
- `(BranchId, DueDate)` WHERE `PaidAt IS NULL` — receivables/aging queries
- `(OriginTransactionId)` WHERE NOT NULL — installment group lookup
- `(GeneratedFromTransactionId)` UNIQUE WHERE NOT NULL — one generated leg per fiado source
- `(BranchId, Status)` — draft/active filtering
- `(BranchId, RecordedByOperatorId, Date)` — "my transactions today" for operator view

### 3.13 Product

Categories of values tracked in the daily closing snapshot. Not physical inventory — these are the line items an operator fills in when closing their register.

```csharp
public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 0;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // Navigation
    public ICollection<DailyCloseItem> DailyCloseItems { get; set; } = [];
}
```

| Column       | Type         | Null     | Notes                                                                                                           |
|--------------|--------------|----------|-----------------------------------------------------------------------------------------------------------------|
| Id           | uuid         | PK       |                                                                                                                 |
| Name         | varchar(255) | NOT NULL | "Dinheiro", "Telesena", "Raspadinha", "Jogos", "Loteria Especial", "Federal", "Diferença Caixa" |
| DisplayOrder | int          | NOT NULL | Controls form field ordering                                                                                    |
| BranchId     | uuid         | NOT NULL | FK → Branch                                                                                                     |
| CreatedAt    | timestamptz  | NOT NULL |                                                                                                                 |
| Active       | boolean      | NOT NULL |                                                                                                                 |

**Unique constraint:** `(BranchId, Name) WHERE Active = true`

### 3.14 DailyClose

The daily register closing session. One row per account per day. Tracks the submission/approval workflow and counted-opening lineage. Every close starts ineligible as an opening source; its first successful item save, including an explicit empty save, stamps `ItemsFirstRecordedAt` once. There is no legacy initialization path.

```csharp
public class DailyClose : EntityBase
{
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid OpenedByUserId { get; init; }
    public User OpenedByUser { get; init; } = null!;

    public Guid? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public Guid? RecordedByOperatorId { get; set; }
    public Operator? RecordedByOperator { get; set; }

    public Guid? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? ItemsFirstRecordedAt { get; set; }
    public DateTime? OpeningRecheckRequiredAt { get; set; }

    public Guid? OpeningRecheckTriggeredByDailyCloseId { get; set; }
    public DailyClose? OpeningRecheckTriggeredByDailyClose { get; set; }

    public Guid? OpeningRecheckTriggeredByUserId { get; set; }
    public User? OpeningRecheckTriggeredByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public bool ReviewHistoryComplete { get; init; } = true;

    // Navigation
    public ICollection<DailyCloseItem> Items { get; init; } = [];
}
```

| Column                                    | Type          | Null     | Notes                                                       |
|-------------------------------------------|---------------|----------|-------------------------------------------------------------|
| Id                                        | uuid          | PK       |                                                             |
| Date                                      | date          | NOT NULL | The business day being closed                               |
| Status                                    | smallint      | NOT NULL | Enum: Draft=0, Submitted=1, Approved=2, Rejected=3          |
| AccountId                                 | uuid          | NOT NULL | FK → Account                                                |
| OpenedByUserId                            | uuid          | NOT NULL | Restricted FK → User who opened the close                   |
| RecordedByUserId                          | uuid          | NULL     | Restricted FK → User who made the first successful count    |
| RecordedByOperatorId                      | uuid          | NULL     | Restricted FK → that user's Operator, when one exists        |
| SubmittedByUserId                         | uuid          | NULL     | Restricted FK → User for the current submission             |
| SubmittedByOperatorId                     | uuid          | NULL     | Restricted FK → that submitter's Operator, when one exists  |
| SubmittedAt                               | timestamptz   | NULL     |                                                             |
| ApprovedAt                                | timestamptz   | NULL     |                                                             |
| ApprovedByUserId                          | uuid          | NULL     | FK → User (manager/admin who approved)                      |
| RejectionReason                           | varchar(500)  | NULL     | Retained through a Rejected correction/opening recheck      |
| Notes                                     | varchar(1000) | NULL     | Operator notes about the day                                |
| ItemsFirstRecordedAt                      | timestamptz   | NULL     | Immutable-once-set first successful item-save timestamp     |
| OpeningRecheckRequiredAt                  | timestamptz   | NULL     | When a predecessor correction invalidated this opening      |
| OpeningRecheckTriggeredByDailyCloseId     | uuid          | NULL     | Restricted self-FK → DailyClose that changed                |
| OpeningRecheckTriggeredByUserId           | uuid          | NULL     | Restricted FK → User who changed the predecessor            |
| UpdatedAt                                 | timestamptz   | NULL     | Generic workflow mutation audit timestamp                   |
| UpdatedByUserId                           | uuid          | NULL     | User who last changed the workflow/item state               |
| BranchId                                  | uuid          | NOT NULL | FK → Branch                                                 |
| ReviewHistoryComplete                     | boolean       | NOT NULL | False for closes that predate review events (§3.21)         |
| CreatedAt                                 | timestamptz   | NOT NULL |                                                             |
| Active                                    | boolean       | NOT NULL |                                                             |

**Unique constraint:** filtered unique index `(BranchId, AccountId, Date) WHERE Active = true` — one active closing per account per day.

`OpenedByUserId` is immutable creation audit. `RecordedByUserId` and optional `RecordedByOperatorId` are stamped atomically with `ItemsFirstRecordedAt` on the first successful item save and never reassigned; a Manager/Admin without an Operator therefore remains a reachable user recorder with a null operator. `SubmittedByUserId` and optional `SubmittedByOperatorId` describe only the current submission and are cleared whenever the close returns to Draft. The recording-identity check constraint requires all recorder fields to be null before first count and a non-null recorder user after first count. All identity and opening-recheck foreign keys use restricted deletion. The rich close and review responses expose opener, recorder, and submitter identities; list and manager-queue rows expose recorder and submitter separately.

### 3.15 DailyCloseItem

Individual product values within a daily closing.

```csharp
public class DailyCloseItem : EntityBase
{
    public decimal Value { get; set; }

    public Guid DailyCloseId { get; init; }
    public DailyClose DailyClose { get; init; } = null!;

    public Guid ProductId { get; init; }
    public Product Product { get; init; } = null!;
}
```

| Column       | Type          | Null     | Notes                              |
|--------------|---------------|----------|------------------------------------|
| Id           | uuid          | PK       |                                    |
| Value        | numeric(14,2) | NOT NULL | The closing value for this product |
| DailyCloseId | uuid          | NOT NULL | FK → DailyClose                    |
| ProductId    | uuid          | NOT NULL | FK → Product                       |
| CreatedAt    | timestamptz   | NOT NULL |                                    |
| Active       | boolean       | NOT NULL |                                    |

**Unique constraint:** filtered unique index `(DailyCloseId, ProductId) WHERE Active = true` — one active value per product per closing session. Soft-deleted item rows do not block a later re-insert for the same product.

### 3.16 TimeEntry

Operator attendance tracking. Standalone — not mixed with the transaction flow. Clock pairs live on child `TimeEntrySegment` rows (full `DateTime`, branch-local wall clock); this parent row stores the day, status, and computed totals.

```csharp
public class TimeEntry : EntityBase
{
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; set; }
    public decimal TotalHours { get; set; }
    public decimal BalanceHours { get; set; }

    public Guid OperatorId { get; init; }
    public Operator Operator { get; init; } = null!;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<TimeEntrySegment> Segments { get; init; } = [];
}
```

| Column          | Type         | Null     | Notes                                                                    |
|-----------------|--------------|----------|--------------------------------------------------------------------------|
| Id              | uuid         | PK       |                                                                          |
| Date            | date         | NOT NULL | Branch-local calendar day                                                |
| Status          | smallint     | NOT NULL | Enum (see section 4)                                                     |
| TotalHours      | numeric(6,2) | NOT NULL | Net hours worked after lunch deduction                                   |
| BalanceHours    | numeric(6,2) | NOT NULL | TotalHours minus daily target. Positive = overtime, negative = owes time |
| OperatorId      | uuid         | NOT NULL | FK → Operator                                                            |
| BranchId        | uuid         | NOT NULL | FK → Branch                                                              |
| UpdatedAt       | timestamptz  | NULL     | Stamped on every upsert after creation                                   |
| UpdatedByUserId | uuid         | NULL     | FK → User; who last modified this entry                                  |
| CreatedAt       | timestamptz  | NOT NULL |                                                                          |
| Active          | boolean      | NOT NULL |                                                                          |

**Filtered unique constraint:** `(BranchId, OperatorId, Date) WHERE Active = true` — one active entry per operator per day.

### 3.16a TimeEntrySegment

Each clock-in/clock-out pair for a `TimeEntry`. A single day may have multiple segments (e.g. morning + afternoon after a lunch break). Clocks are stored as `DateTime` (branch-local wall clock, `timestamp without time zone`).

```csharp
public class TimeEntrySegment : EntityBase
{
    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }

    public Guid TimeEntryId { get; init; }
    public TimeEntry TimeEntry { get; init; } = null!;

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
```

| Column          | Type              | Null     | Notes                                                                          |
|-----------------|-------------------|----------|--------------------------------------------------------------------------------|
| Id              | uuid              | PK       |                                                                                |
| ClockIn         | timestamp (no tz) | NOT NULL | Branch-local wall clock; must fall within `[parent.Date, parent.Date + 1 day)` |
| ClockOut        | timestamp (no tz) | NULL     | Null = open shift; when set must be > ClockIn and ClockOut − ClockIn ≤ 24 h    |
| TimeEntryId     | uuid              | NOT NULL | FK → TimeEntry (CASCADE on delete)                                             |
| UpdatedAt       | timestamptz       | NULL     | Stamped on admin edit                                                          |
| UpdatedByUserId | uuid              | NULL     | FK → User; who last modified this segment                                      |
| CreatedAt       | timestamptz       | NOT NULL |                                                                                |
| Active          | boolean           | NOT NULL |                                                                                |

**Day-bounds invariant:** `segment.ClockIn ∈ [parent.Date, parent.Date + 1 day)`. When `ClockOut` is set: `ClockOut > ClockIn` and `ClockOut − ClockIn ≤ 24 h`, which implicitly bounds `ClockOut < parent.Date + 2 days`.

**Filtered unique constraint:** `(TimeEntryId) WHERE "ClockOut" IS NULL AND "Active" = true` — at most one open segment per TimeEntry at any moment. Named `IX_TimeEntrySegments_TimeEntryId_OpenShift`.

**Secondary index:** `(TimeEntryId, ClockIn)` for segment ordering queries.

**FK behaviour:** `OnDelete(DeleteBehavior.Cascade)` from `TimeEntry` — deleting (or hard-deleting) a TimeEntry also removes its segments.

### 3.17 Holiday

Branch-specific holidays that affect business day calculations and time entries.

```csharp
public class Holiday : EntityBase
{
    public DateTime Date { get; init; }
    public string? Description { get; set; }
    public HolidaySource Source { get; set; } = HolidaySource.Manual;

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
```

| Column      | Type         | Null     | Notes                                                                                                    |
|-------------|--------------|----------|----------------------------------------------------------------------------------------------------------|
| Id          | uuid         | PK       |                                                                                                          |
| Date        | date         | NOT NULL |                                                                                                          |
| Description | varchar(500) | NULL     | Human-readable label (e.g. "Natal")                                                                      |
| Source      | smallint     | NOT NULL | `HolidaySource` enum (Manual=0, Canonical=1, BrasilApi=2, Nager=3); default `Manual`; informational only |
| BranchId    | uuid         | NOT NULL | FK → Branch                                                                                              |
| CreatedAt   | timestamptz  | NOT NULL |                                                                                                          |
| Active      | boolean      | NOT NULL |                                                                                                          |

**Filtered unique constraint:** `(BranchId, Date) WHERE Active = true` — one active holiday per date per branch.

**`Source` provenance.** `HolidaySource` records where each row came from. Manual entries from `POST /holiday` set `Manual`; the canonical-only import path sets `Canonical`; the multi-source composite path sets the per-concept claim (`BrasilApi`, `Nager`, or `Canonical` for backfilled concepts). The column is informational — no business rule reads it. The Phase 6.5 migration adds it with default `0` (Manual) and backfills every existing row to `Manual` (correct for any row predating Phase 6 imports and indistinguishable from a hand-entry).

### 3.18 Setting

Per-branch configuration. Single row per branch.

```csharp
public class Setting : EntityBase
{
    public DateTime LockDate { get; set; }
    public decimal DailyTargetHours { get; set; } = 7.33m;
    public decimal LunchDeductionOver6H { get; set; } = 1.0m;
    public decimal LunchDeductionOver4H { get; set; } = 0.25m;

    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    // PostgreSQL xmin system column; not a user-defined table column.
    public uint Version { get; set; }
}
```

| Column               | Type         | Null     | Notes                                                                             |
|----------------------|--------------|----------|-----------------------------------------------------------------------------------|
| Id                   | uuid         | PK       |                                                                                   |
| LockDate             | date         | NOT NULL | Transactions on or before this date cannot be edited. Access: `conf_dtfechamento` |
| DailyTargetHours     | numeric(6,2) | NOT NULL | Default 7.33 (7h20m). Current-policy mirror — see the dating-model note below     |
| LunchDeductionOver6H | numeric(4,2) | NOT NULL | Default 1.00. Current-policy mirror — hours deducted for lunch when worked >6h    |
| LunchDeductionOver4H | numeric(4,2) | NOT NULL | Default 0.25. Current-policy mirror — break deduction when worked >4h but ≤6h     |
| BranchId             | uuid         | NOT NULL | FK → Branch. UNIQUE — one setting row per branch                                  |
| Version              | xid          | NOT NULL | PostgreSQL `xmin` system column, exposed as `uint`; optimistic-concurrency token  |
| CreatedAt            | timestamptz  | NOT NULL |                                                                                   |
| Active               | boolean      | NOT NULL |                                                                                   |

**Time-constant dating model (M7.7 Phase 7, decision 1.6a).** The three time constants on this row are the branch's **current values only** — a denormalized mirror of the latest §3.20 `TimeEntryPolicy` row, kept for the Settings read surface (`GET /setting`) and as the `If-Match`/ETag concurrency root of `PUT /setting`. **No calculation reads them.** Every §6.7 balance computation resolves the effective-dated `TimeEntryPolicy` row applicable to the entry's own date. `PUT /setting` is the single write path and keeps mirror and ledger in sync in one commit: it mutates this row (advancing `xmin`) and upserts the policy row effective from the branch-local day of the change. `Setting` itself keeps `LockDate` and is **not** versioned.

---

### 3.19 IdempotencyRequest

A persisted replay record for supported financial create commands. It is infrastructure for command delivery semantics, not a financial ledger row.

```csharp
public class IdempotencyRequest : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResponseEnvelope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public Guid BranchId { get; init; }
    public Guid UserId { get; init; }
}
```

| Column           | Type         | Null     | Notes                                                        |
|------------------|--------------|----------|--------------------------------------------------------------|
| Id               | uuid         | PK       |                                                              |
| Key              | varchar(128) | NOT NULL | Client `Idempotency-Key`                                     |
| Endpoint         | varchar(100) | NOT NULL | Exact command scope                                           |
| PayloadHash      | varchar(64)  | NOT NULL | Uppercase SHA-256 of canonical typed JSON                    |
| ResourceId       | uuid         | NOT NULL | Created transaction id or first installment id               |
| ResponseEnvelope | text         | NOT NULL | Deterministic response replay                                |
| ExpiresAt        | timestamptz  | NOT NULL | Replay expiry; row becomes purge-eligible after 24 hours     |
| BranchId         | uuid         | NOT NULL | Restricted FK → Branch                                      |
| UserId           | uuid         | NOT NULL | Restricted FK → User                                        |
| CreatedAt        | timestamptz  | NOT NULL | Reset when an expired key is reused                          |
| Active           | boolean      | NOT NULL | EntityBase default                                           |

**Unique scope:** `(Endpoint, BranchId, UserId, Key)`. A transaction-scoped PostgreSQL advisory lock serializes this exact scope before its tracked row is read or created. The reservation/replay record and financial rows commit in the same database transaction, so neither can survive without the other.

---

### 3.20 TimeEntryPolicy

Effective-dated time-entry policy ledger (M7.7 Phase 7, decision 1.6a). Each row carries the branch's time constants valid from `EffectiveFrom` (inclusive, branch-local date) until a later row's `EffectiveFrom` supersedes it. The applicable row for an entry date is the one with the **greatest `EffectiveFrom` on or before that date**, so changing today's constants never rewrites balances for earlier days. There is no per-entry snapshot and `Setting` is not versioned — this table is the single dating model.

```csharp
public class TimeEntryPolicy : EntityBase
{
    public DateTime EffectiveFrom { get; init; }
    public decimal DailyTargetHours { get; set; }
    public decimal LunchDeductionOver6H { get; set; }
    public decimal LunchDeductionOver4H { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
}
```

| Column               | Type         | Null     | Notes                                                                              |
|----------------------|--------------|----------|-------------------------------------------------------------------------------------|
| Id                   | uuid         | PK       |                                                                                    |
| EffectiveFrom        | date         | NOT NULL | First branch-local date this policy applies to (inclusive)                          |
| DailyTargetHours     | numeric(6,2) | NOT NULL | Target worked hours per day while this row is effective                            |
| LunchDeductionOver6H | numeric(4,2) | NOT NULL | Lunch deduction when worked >6h while this row is effective                        |
| LunchDeductionOver4H | numeric(4,2) | NOT NULL | Break deduction when worked >4h but ≤6h while this row is effective                |
| BranchId             | uuid         | NOT NULL | Restricted FK → Branch                                                             |
| CreatedAt            | timestamptz  | NOT NULL |                                                                                    |
| Active               | boolean      | NOT NULL | EntityBase default; reads filter on it                                             |

**Unique constraint:** filtered unique index on `(BranchId, EffectiveFrom)` where `Active = true` — at most one active policy per branch and effective date, which keeps per-date resolution unambiguous.

**Lifecycle:**
- **Initial row:** every branch has exactly one row dated `0001-01-01` (`DateTime.MinValue`) so resolution is total for any entry date, including entries backdated before branch creation. `CreateBranchSeedFactory` seeds it for new branches; migration `M7_7Phase7TimeEntryPolicy` deterministically backfilled it for every pre-existing branch from that branch's own `Setting` constants (so all historical balances recompute unchanged). The `Branch → Setting` relationship is not database-enforced, so the migration runs a fail-loud precondition check that aborts the whole migration if any existing branch has no `Setting` row, rather than silently leaving that branch with zero policy rows.
- **Change:** a `PUT /setting` that actually changes a constant appends a row with `EffectiveFrom =` the branch-local day of the change; a **second change on the same day mutates that day's row in place** (no second row). Value-identical updates append nothing.
- **Resolution:** rows are never deleted or end-dated; the next row's `EffectiveFrom` closes the previous one implicitly. A change therefore applies to the whole branch-local day it was made on and to every later day until superseded. Every read surface — Get, List, and the §6.14 balance report — recomputes each row under its entry-date policy on every call, closed rows included, so the day of a change reflects it immediately everywhere and no surface can serve a stale persisted checkpoint that disagrees with payroll.

---

### 3.21 DailyCloseReviewEvent and DailyCloseReviewEventItem

Immutable review history of a close (M7.7 Phase 7.6.1, gap X). An event is written in the same transaction as the command that causes it and is never updated, recalculated or backfilled. `DailyCloseId` is the close the event explains: the rejected close, or the successor whose opening changed. Actor and product names are **snapshots** taken when the event happened, so a later rename never rewrites history.

```csharp
public class DailyCloseReviewEvent : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid DailyCloseId { get; init; }
    public DailyCloseReviewEventType Type { get; init; }
    public DateTime OccurredAt { get; init; }

    public Guid ActorUserId { get; init; }
    public string ActorUserName { get; init; } = string.Empty;
    public Guid? ActorOperatorId { get; init; }
    public string? ActorOperatorName { get; init; }

    public string? RejectionReason { get; init; }            // Rejected only
    public Guid? SourceDailyCloseId { get; init; }           // OpeningChanged only
    public uint? SourceVersion { get; init; }                // OpeningChanged only
    public Guid? PreviousSourceDailyCloseId { get; init; }   // OpeningChanged only
    public DailyCloseStatus? AffectedStatusBefore { get; init; }
    public DailyCloseStatus? AffectedStatusAfter { get; init; }

    public ICollection<DailyCloseReviewEventItem> Items { get; init; } = [];
}

public class DailyCloseReviewEventItem : EntityBase
{
    public Guid DailyCloseReviewEventId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal? BeforeValue { get; init; }
    public decimal? AfterValue { get; init; }
}
```

| Column (DailyCloseReviewEvents) | Type         | Null     | Notes                                                                                   |
|---------------------------------|--------------|----------|-----------------------------------------------------------------------------------------|
| Id                              | uuid         | PK       |                                                                                         |
| BranchId                        | uuid         | NOT NULL | Restricted FK → Branch                                                                  |
| DailyCloseId                    | uuid         | NOT NULL | Restricted FK → DailyClose — the close this event explains                              |
| Type                            | smallint     | NOT NULL | `DailyCloseReviewEventType`                                                             |
| OccurredAt                      | timestamptz  | NOT NULL | The command's single captured instant                                                   |
| ActorUserId                     | uuid         | NOT NULL | Restricted FK → User                                                                    |
| ActorUserName                   | varchar(255) | NOT NULL | Snapshot                                                                                |
| ActorOperatorId                 | uuid         | NULL     | Restricted FK → Operator — the actor's linked operator, when any                        |
| ActorOperatorName               | varchar(255) | NULL     | Snapshot                                                                                |
| RejectionReason                 | varchar(500) | NULL     | Rejected only                                                                           |
| SourceDailyCloseId              | uuid         | NULL     | Restricted FK → DailyClose — the close whose saved count changed (OpeningChanged)       |
| SourceVersion                   | bigint       | NULL     | The source's `xmin` version the saved count was applied to                              |
| PreviousSourceDailyCloseId      | uuid         | NULL     | Restricted FK → DailyClose — the opening source before the save; null when none existed |
| AffectedStatusBefore / After    | smallint     | NULL     | The affected close's status before and after the same save                              |
| CreatedAt / Active              |              | NOT NULL | EntityBase                                                                              |

| Column (DailyCloseReviewEventItems) | Type          | Null     | Notes                                                  |
|-------------------------------------|---------------|----------|--------------------------------------------------------|
| Id                                  | uuid          | PK       |                                                        |
| DailyCloseReviewEventId             | uuid          | NOT NULL | Restricted FK → DailyCloseReviewEvent                  |
| ProductId                           | uuid          | NOT NULL | Restricted FK → Product (retired products allowed)     |
| ProductName                         | varchar(255)  | NOT NULL | Snapshot                                               |
| BeforeValue / AfterValue            | numeric(14,2) | NULL     | Null = no saved item, distinct from a recorded zero    |
| CreatedAt / Active                  |               | NOT NULL | EntityBase                                             |

**Indexes:** `(BranchId, DailyCloseId, OccurredAt)` on events; unique `(DailyCloseReviewEventId, ProductId)` on items.

**Legacy rows:** migration `M7_7Phase76CloseReviewEvents` sets `DailyClose.ReviewHistoryComplete = false` for every close that existed before it and writes no events for them; new closes start `true`.

---


### 3.22 PasswordResetToken (M8 Phase 3)

Global account recovery capability: `Id`, `UserId` (restricted User FK), `TokenHash` (unique varchar(64), SHA-256 only), `ExpiresAt` (UTC), `ConsumedAt?`, base `CreatedAt`/`Active`. No plaintext token column. A 32-byte random Base64Url token is valid only while active, unconsumed and `now < ExpiresAt`. Default lifetime 30 minutes, configurable up to 60. A new eligible request invalidates prior recovery tokens; successful password change/reset invalidates all remaining recovery capabilities. Single-use consumption, password update and refresh revocation share one transaction.

### 3.23 RecoveryThrottle (M8 Phase 3)

`Id`, unique `KeyHash` (SHA-256 of normalized target email, varchar(64)), UTC `NextAllowedAt` (indexed, v57), base `CreatedAt`/`Active`. No User FK, target email or capability: missing/inactive/existing targets receive the same persisted cooldown (default 60 seconds, configurable 1–3600). A separate per-process limiter per client network (§6.16) bounds anonymous request volume; neither response reveals account existence. A request reads its row `FOR UPDATE`; since v57 a hosted sweep (every 15 minutes, batches of 500, `FOR UPDATE SKIP LOCKED`) deletes rows whose cooldown has elapsed, which mean exactly what a missing row means.

### 3.24 OperatorLinkRequest (M8 Phase 4)

`Id`, `BranchId`, `UserId` (restricted FKs), `Status` (`Pending`, `Resolved`), `ResolvedByUserId?` (restricted User FK), UTC `ResolvedAt?`, base `CreatedAt`/`Active`. Filtered unique `(BranchId, UserId)` where active and Pending. No historical requests are inferred by migration. Reads/counts show only active Member memberships in an active branch with an active user; removal or role change hides a pending request without destroying history. A repeat eligible create returns the same pending resource. Resolution requires a live linked operator; it records the actor/time and reports actual usable-account state without assigning any account.

### 3.25 Invitation (M8 Phase 5)

`Id`, normalized `Email` varchar(255), `BranchId`, `Role`, optional `OperatorId`, unique `TokenHash` varchar(64), UTC `ExpiresAt`, `Status` (`Pending`, `Accepted`, `Declined`, `Expired`), `IssuedByUserId`, optional `AcceptedByUserId`, `RespondedAt?`, `DeliveryStatus` (`Pending`, `Sent`, `Failed`), base `CreatedAt`/`Active`. Branch/operator/actor FKs restrict deletion. Filtered unique active Pending `(BranchId, Email)`; expired pending rows are retired inside issuance before a replacement occupies the slot. Effective Expired is projected at read from UTC; no scheduler or email arrival is implied. Lifetime defaults to 72 hours, configurable up to 168. Capabilities use the same 32-byte entropy/SHA-256 boundary as recovery, with no durable plaintext outbox. Reissue replaces the hash and expiry, making every old capability unusable. `Sent` means SMTP acceptance only. Pre-link is permitted only for a Member role and an existing active unlinked operator in that same branch; it preserves assignments/history.

### 3.26 OperatedOnboarding (M8 Phase 6)

`Id`, unique caller-supplied `RequestId`, unique `BranchId`, `PlatformUserId`, unique `OwnerInvitationId`, `RequestHash` varchar(64) and base `CreatedAt`/`Active`. The three FKs are restricted. Only a platform operator may create/read/reissue it. A repeat with the same id, actor and canonical request hash returns the original branch; a changed request conflicts. It persists no plaintext invitation capability. Initial branch state may have a pending owner Admin invite and zero members; the platform caller is not enrolled.

### 3.27 GoogleLoginChallenge (M8 Phase 7)

`Id`, unique SHA-256 `StateHash`/`NonceHash` varchar(64), UTC `ExpiresAt` and optional `ConsumedAt`, plus base `CreatedAt`/`Active`. The API returns 256-bit random state/nonce to the client once; only hashes persist. The challenge expires after five minutes and is consumed in the same transaction as account link, invitation acceptance and refresh-session issue. Reuse, wrong state or wrong signed-token nonce fails.

### 3.28 GoogleIdentity (M8 Phase 7)

`Id`, unique `(Issuer, Subject)`, unique `UserId`, restricted User FK and base `CreatedAt`/`Active`. The issuer is canonical `https://accounts.google.com` and subject is Google's stable `sub`; provider email is not a user key and is not persisted in this table. One Google identity per local account in this phase. A new link needs the stored local proof plus authoritative Google mailbox or the existing password; pre-migration accounts have null proof. Local account membership continues to decide branch access.

### 3.29 LoginThrottle (M8 Phase 8)

`Id`, unique `KeyHash` varchar(64), indexed `AccountKeyHash` varchar(64), `FailureCount` (check > 0), UTC `WindowStartedAt`, optional UTC `BlockedUntil`, indexed UTC `ExpiresAt`, base `CreatedAt`/`Active`. One row is one failed-password budget: the account (`KeyHash` = `AccountKeyHash` = SHA-256 of `login-account:` + canonical email) or the account seen from one client network (SHA-256 of `login-client:` + canonical email + network). No User FK, email, address, password or user id is stored, so missing and existing accounts are indistinguishable. A row exists only after a failed check; `ExpiresAt` is the block end, else the window end. Rows are read and written `FOR UPDATE` under the account's email advisory lock, and a hosted sweep deletes expired rows in bounded `SKIP LOCKED` batches. Rules in §6.16.

### 3.30 Custody model — M9.6 Phases 2–3 shipped, later phases proposed

This section and §6.19–§6.23 were the M9.6 Phase 1 foundation. Phases 2–3 ship the session, opening, handover, discrepancy, event, mapping-issue, terminal and opening-count records below (owner acceptance 2026-10-29; independent technical review of the candidate and runtime still pending, M9.6 1.3). `SessionCountAttempt` (P4), `SessionFieldEffect` (P7) and `CustodyTransfer` (P9) remain proposed. §3.7–§3.15 and §6.5/§6.13 describe the review workflow, which now runs per session.

**Shipped in v59 (M9.6 P2–P3).** Tables `Terminals`, `SessionOpenings`/`SessionOpeningItems`, `SessionHandovers`/`SessionHandoverItems`, `SessionOpeningDiscrepancies` with `…Revisions`/`…RevisionItems`, `OpeningDiscrepancyAttributionEvents`, `SessionEvents` (jsonb details), `CustodyMappingIssues` and `OpeningCounts`/`OpeningCountItems`; columns on `DailyCloses` (MappingState, Origin, CustodyState, CustodianOperatorId, CustodianUserIdAtStart, TerminalId, Sequence, StartedAt, EndedAt, LedgerRevision, CountRevision, CustodyHistoryComplete), `Accounts` (TerminalId, CustodyHistoryRevision) and `Transactions` (SessionId). Migration `M9_6Phase2SessionCustody` replaces `IX_DailyCloses_BranchId_AccountId_Date` with unique `(BranchId, AccountId, Sequence)` for Ready rows and unique `(BranchId, AccountId)` for Ready active custody, and installs in SQL: composite `(BranchId, AccountId, SessionId)` foreign key on transactions; same-branch custodian/terminal keys; the interval trigger below; Ready-history retention (no delete, deactivation or downgrade); start/date coherence (StartedAt's São Paulo date is the session Date); the declared-window check (ManagerDeclared sessions end in the past, inside one business day); cashbox-insert-requires-session, session-only-on-cashbox and immutable-session-link guards on transactions; and deferred row/session business-date coherence in both directions. `LedgerRevision` is stored but increments land with P4. `Account.TerminalId` stays nullable for legacy cashboxes; a start or declaration on a cashbox without a terminal is refused, new cashboxes and operated-branch seeds get one, and `PUT /terminal/cashbox/{accountId}` reassigns it (refused during active custody or a pending offer).

**Identity decision.** Keep `Account.Id` and `AccountType.Terminal` as the stable cashbox custody key (pt-BR **caixa**), preserving ledger, grants and paired `TabAccountId`. Add a distinct `Terminal` (physical CAIXA report reference), never rename/reuse Account IDs as hardware IDs. Keep `DailyClose.Id` as the stable **SessionId** (pt-BR **turno**), extending that aggregate rather than creating a competing close/session root. Proposed names below are domain names, not generated schemas. Cashbox → Terminal is many-to-one; a terminal may have parallel cashboxes. A cashbox has sequential sessions and at most one active custodian. A person may operate separate granted cashboxes; there is no unique operator/day restriction. A session does not imply an attendance shift.

| Proposed record / relation | Required data and invariants (all ids Guid; money numeric(14,2); audit instants UTC) | Owner |
|---|---|---|
| `Terminal` | Id, BranchId, Name, optional external report reference (unique per branch when configured), Active; branch-scoped identity; no cash, Tab or ledger balance | M9.6 P2 |
| `Account.TerminalId` and `CustodyHistoryRevision` | TerminalId is required on operational cashboxes after reviewed mapping and null for Tab/Bank/Safe, with same-branch restricted FK. CustodyHistoryRevision is a server-only non-null bigint on Account, initially 0 and incremented by the interval trigger for cashbox history writes, independent of ledger/count revisions. Terminal reassignment is manager-only, no active custody; previous session retains TerminalId snapshot | P2 |
| `DailyClose` extended as session | Existing Id/BranchId/AccountId/Date and review identities; immutable CustodianOperatorId and CustodianUserIdAtStart (nullable for an unlinked employee), TerminalId snapshot, Sequence (per cashbox), StartedAt, optional EndedAt; CustodyState; durable bigint LedgerRevision and CountRevision; opening provenance; xmin Version stays short-lived write token | P2 |
| `SessionOpening` and items | One per session, immutable acknowledged product/value vector, AcknowledgedByUserId and optional acting OperatorId, AcknowledgedAt, EvidenceKind (IncomingAcknowledgment/ManagerException), source kind/id/revision and proposed vector snapshot. Kind = ExplicitZero, OpeningCount, PreviousSession, ImportedSession; missing acknowledgment is never zero. ImportedSession references an actual mapped historical session | P2/P3 |
| `OpeningCount` and items | BranchId, AccountId, AsOfDate, revision, recorder and count vector; implementation seed/custody proposal only, no variance or close status. No predecessor is invented. Refused while the cashbox has any counted Ready session, imported session or Unresolved legacy history: an opening count never starts fresh over unmapped history | P3 |
| `SessionHandover` | OutgoingSessionId, IncomingSessionId nullable until accepted (unique when non-null), immutable offered count/attempt/revision, intended incoming operator, offer/ack actors/times, proposed and acknowledged vectors independently, state Offered/Accepted/Disputed/Withdrawn; one unresolved offer per outgoing session | P2 |
| `SessionOpeningDiscrepancy` and immutable revisions | Id, BranchId, SessionOpeningId (unique), optional HandoverId, optional PredecessorSessionId, source kind/id/revision, proposed and acknowledged vectors, signed per-product deltas/total = acknowledged − proposed, current DiscrepancyRevision and Version. Nonzero comparison starts Unattributed, whether from direct handover, Finish→Start, opening count or imported opening; no financial leg | P2/P3; personal consumption M9.7 P7/M9.8 P1 |
| `OpeningDiscrepancyAttributionEvent` | OpeningDiscrepancyId + DiscrepancyRevision, target Outgoing/Incoming/Neither, reason, actor/time, supersedes event id and expected discrepancy Version. Outgoing is available only for an evidenced PredecessorSessionId with known custodian; never infer an outgoing person from the opening-count recorder. Unattributed is initial and distinct from Neither | P2; personal consumption M9.7 P7/M9.8 P1 |
| `SessionCountAttempt` and items | SessionId, AttemptNumber, ConfirmationToken, CountRevision, LedgerRevision, opening revision, immutable count + calculation snapshot, actor/time, command id. UNIQUE (SessionId, AttemptNumber) and UNIQUE (SessionId, ConfirmationToken); each token permits at most one CountConferred event regardless of request keys. A later attempt never overwrites an older one | P4 |
| `CustodyMappingIssue` | Id, BranchId, source entity kind/id, issue code, source digest, mapping-plan id/version, proposed mapping payload, status Unresolved/Resolved, resolution reason/evidence reference, reviewer actor/time. Sensitive per-id exports remain private; aggregate counts may be reported. No financial effect | P2 migration |
| `SessionEvent` | SessionId, event kind, immutable actor identity/name snapshots, recorded UTC time, reason, before/after state, source/correction ids and revisions; preserve existing DailyCloseReviewEvents rather than replacing their history. CountConferred carries AttemptId with restricted FK and UNIQUE (AttemptId) WHERE kind = CountConferred, null on unrelated events | P2–P5 |
| `Transaction.SessionId` | Required for cashbox rows (ordinary and generated; Draft included), null on Tab/Bank/Safe. Composite same-branch/account FK to session; immutable once recorded. A legacy row's null moves to a Ready session only through the reviewed mapping (§6.22), never by a command. Source fiado row remains on Tab; generated counterpart has paired cashbox's session | P2/P6 |
| `SessionFieldEffect` | Unique SessionId + field role (TerminalReport/BolaoFee/SpecialFee), revision and linked transaction id nullable at zero; append history per change. No request can forge generation origin | P7 |
| `CustodyTransfer` and legs | Stable operation id, from/to accounts, asset kind and amount, source session where cashbox is involved, protected generated legs and source versions. Cheque custody references receivable identity; it is never another counted cash asset | P9/M9.7 |

**Database enforcement required by Phase 2.** Replace `IX_DailyCloses_BranchId_AccountId_Date`; use UNIQUE `(BranchId, AccountId, Sequence)` for resolved sessions and UNIQUE `(BranchId, AccountId) WHERE Active AND MappingState = Ready AND CustodyState = Active`. No `Status = Draft` filter: historical Draft corrections coexist with today's custody. Restricted composite FKs `(BranchId, AccountId, SessionId)` prevent attaching another box's transaction; use alternate key `(BranchId, AccountId, Id)` on sessions. Branch-consistent Terminal, Operator, opening-source and handover endpoints require composite branch keys/FKs. A session's predecessor must be the immediately preceding sequence of the same cashbox; enforce under cashbox coordination, prohibit self/cyclic links, never order same-day sessions by guessed times. CHECK state/end-time consistency, nonnegative ordinary counts, positive transaction magnitudes, generation null-pair constraints and role uniqueness. Enforce non-overlapping actual `[StartedAt, EndedAt)` intervals per cashbox (unbounded active interval) with the database trigger specified below, in addition to active uniqueness. Backfilled unknown intervals remain explicitly unknown and cannot fabricate an interval to satisfy enforcement: quarantine before activating the new operational constraint. No hard deletion or soft-deactivation may release an occupied cashbox or erase an acknowledgment/history.

**Interval enforcement without an extension prerequisite (P2).** Do not use a uuid-equality GiST exclusion constraint: it would require unconfigured `btree_gist`, and hosted availability has not been verified. Instead, the migration installs a VOLATILE row trigger on session INSERT/UPDATE covering MappingState, Active, AccountId, StartedAt and EndedAt. AccountId is immutable on existing rows. For a Ready interval the trigger first locks and increments the owning Account row (`UPDATE ... SET CustodyHistoryRevision = CustodyHistoryRevision + 1 RETURNING ...`), then in a separate query rejects another Ready interval of that same BranchId/AccountId whose `tstzrange(StartedAt, EndedAt, '[)')` overlaps (`&&`), excluding the current Id. Half-open intervals permit exact handover boundaries, not overlap; an unbounded EndedAt means current custody. The root update serializes competing intervals even when no prior session exists: at Read Committed the separate VOLATILE query observes committed history after the wait; a Repeatable Read/Serializable transaction with an older root snapshot must abort on the concurrent root update instead of checking an old interval snapshot. The revision increment rolls back with any rejected interval and has no financial or count-attempt effect. The check runs against history too: soft deactivation cannot erase a Ready custody interval to make room. Reject Ready→Unresolved downgrades and deletion of Ready session history; quarantine is for originally unresolved legacy rows, not an escape from interval enforcement. Keep a branch/account lookup index. Application batches acquire all affected Account root rows in sorted order inside the existing month→account coordination before session DML; trigger acquisition is reentrant. Because the trigger takes an Account row lock while its writer already holds that account's advisory key, **no path may update an operational cashbox Account row and then wait for that account's advisory key**: pairing, unpairing, deactivation and Terminal reassignment of a cashbox acquire the full §6.20 order before their Account-row write (a rename that waits on nothing else may stay outside it). Direct conflicting writes must either observe the committed predecessor after the lock or abort (including serialization/deadlock failure), never commit overlapping history. Phase 2 PostgreSQL tests must cover two independent connections, initially empty history, insert/update overlap, equal boundaries, active unbounded intervals, Read Committed versus Repeatable Read/Serializable races, soft-deactivation/downgrade/delete bypass and unresolved→Ready mapping; no hosted extension installation or production access is required by this design.

**Unresolved legacy rows have an explicit storage disposition.** Existing DailyClose/Transaction ids remain intact with `MappingState=Unresolved`; unknown Custodian/Terminal/Sequence/StartedAt/CustodyState fields are nullable **only** in that state, with original columns and incomplete-history flags retained. `Ready` requires all operational identity/interval fields, coherent state/end-time checks and supported opening provenance. Unresolved rows are history-only, never an active session or opening acknowledgment; new commands cannot attach to them. Each has CustodyMappingIssue rows and blocks affected cutover/lock until a reviewed map transitions it to Ready atomically, without changing financial amounts/review status. A box with unresolved history cannot silently start fresh over it; choosing an authorized disposable reset remains a separate alternative. Operational filtered indexes and overlap trigger apply to Ready intervals; this is explicit quarantine, not invented history or dropped exceptions.

**Identity and authorization.** CustodianOperatorId identifies the employee answerable for this session, even when a manager records their work. Acting User is authenticated for every command; immutable opener and first-count recorder retain their existing meaning; current submitter may change after recall. Neither manager entry nor correction reassigns custodian/recorder. A normal Member must be active, linked to that custodian, and hold the explicit cashbox grant; fiado additionally requires its explicit Tab grant. Cashbox pairing never grants Tab authority. Grant revocation blocks new member writes immediately but preserves readable historical manager evidence and unresolved custody; a manager resolves it explicitly. Manager/Admin may record for another active granted custodian through an explicit start/exception with reason; a manager without an operator cannot name themselves as a nonexistent custodian. Physical Terminal selection grants no authority. No generic manager command silently chooses today's session for a historical row.

## 4. Enums

M8 adds `OperatorLinkRequestStatus { Pending = 0, Resolved = 1 }`, `InvitationStatus { Pending = 0, Accepted = 1, Declined = 2, Expired = 3 }`, and `MailDeliveryStatus { Pending = 0, Sent = 1, Failed = 2 }`. As with existing public enums, JSON/OpenAPI use declared names; validators reject undefined request values.

M11 adds `SettlementInstrument { None = 0, Cheque = 1, CreditCard = 2, DebitCard = 3 }` and `MonthlyFinancialRole { None = 0, BolaoServiceFee = 1, CommercialLeftover = 2 }` (TransactionType report classification, §3.10), and the response-only `OverviewSectionStatus { Ready = 0, Empty = 1, NotConfigured = 2 }` (management overview, §6.14). `Empty` means the section's source ran and found nothing; `NotConfigured` means a deterministic configuration is missing and must never be shown as zero.


Only truly fixed, binary, or logic-dependent values are enums. Everything else is data.

```csharp
public enum Role
{
    Admin = 0,     // Full access, can manage branches and users
    Manager = 1,   // Can approve closings, cancel old transactions, view all operators
    Member = 2     // Operator: can only see/edit own terminal, submit closings
}

public enum Direction
{
    In = 0,        // Money enters (Positivo) — Receita, Entradas, Crédito Banco
    Out = 1        // Money leaves (Negativo) — Saídas, Despesas, Débito Banco
}

public enum AccountType
{
    Terminal = 0,     // Operator's cash drawer
    BankAccount = 1,  // CEF or other bank account
    Tab = 2           // Fiado (credit) account, optionally paired with a Terminal
}

public enum TransactionStatus
{
    Draft = 0,       // Saved but not finalized (mobile offline, or operator still editing)
    Active = 1,      // Finalized, counts in all calculations
    Cancelled = 2    // Soft-deleted with audit trail
}

public enum SettlementRule
{
    SameDay = 0,                 // DueDate = Date
    NextCalendarDay = 1,         // DueDate = Date + 1 calendar day
    NextBusinessDay = 2,         // DueDate = next Monday-Friday after Date
    TwoBusinessDays = 3,         // DueDate = second Monday-Friday after Date
    OperatorEnteredCheque = 4    // DueDate is supplied by the operator for pre-dated cheques
}

public enum DailyCloseStatus
{
    Draft = 0,       // Operator is entering values
    Submitted = 1,   // Operator finished, awaiting manager review
    Approved = 2,    // Manager confirmed
    Rejected = 3     // Manager found issues, operator needs to fix
}

public enum DailyCloseReviewEventType
{
    Rejected = 0,       // A manager rejected the close (time, actor, reason)
    OpeningChanged = 1  // A real count change on its opening source moved this close's opening
}

public enum InstallmentPlanStatus
{
    Draft = 0,      // Every installment is Draft
    Active = 1,     // No Draft installment remains; at least one is Active
    Mixed = 2,      // Draft installments alongside Active and/or Cancelled ones
    Cancelled = 3   // Every installment is Cancelled
}

public enum TimeEntryStatus
{
    Present = 1,
    DayOff = 2,                // Folga banco de horas — hours owed
    Sunday = 3,                // Abonado — no hours owed
    Holiday = 4,               // Abonado
    Vacation = 5,              // Abonado
    JustifiedAbsence = 6,      // Abonado
    UnjustifiedAbsence = 7     // Hours owed
}
```

**SettlementRule due-date semantics:**

| Value                 | DueDate behavior                                                                          |
|-----------------------|-------------------------------------------------------------------------------------------|
| SameDay               | DueDate equals `Date`                                                                     |
| NextCalendarDay       | DueDate equals `Date.AddDays(1)`                                                          |
| NextBusinessDay       | DueDate is the next business day after `Date`; weekends and branch holidays are skipped   |
| TwoBusinessDays       | DueDate is the second business day after `Date`; weekends and branch holidays are skipped |
| OperatorEnteredCheque | DueDate must be explicitly entered by the operator and must be on or after `Date`         |

### What is NOT an enum

| Concept         | Why data table                                                     |
|-----------------|--------------------------------------------------------------------|
| Category        | Owner may add new expense categories                               |
| TransactionType | New payment methods emerge (PIX was added to Access mid-lifecycle) |
| Product         | New lottery products can appear from CEF                           |

### Wire representation

The numeric values above are the **persisted database representation only**. They are not the JSON contract:
responses serialize each enum as its canonical declared name (`"Approved"`, not `2`), and the generated OpenAPI
document publishes every contract enum as a named `type: string` component listing those same names. See §6.15
for the full rule, including what request bodies and query strings accept during the integer-input transition
window.

---

## 5. Seed Data

When a new Branch is created, these rows are seeded automatically.

These are the minimum default seeds for the new operational flow, not a complete migration of every Access lookup row. The owner can create additional Categories, TransactionTypes, and Products through the admin interface as needed (e.g., the 30+ expense subtypes under Despesas Administrativas, Pessoal, and Financeiras that exist in the Access system).

### Categories

| Name                     | DefaultDirection | Access cat_id |
|--------------------------|------------------|---------------|
| Receita                  | In               | 1             |
| Crédito Banco            | In               | 2             |
| Entradas                 | In               | 3             |
| Despesas Administrativas | Out              | 4             |
| Despesas Comerciais      | Out              | 5             |
| Despesas Pessoal         | Out              | 6             |
| Despesas Financeiras     | Out              | 7             |
| Débito Banco             | Out              | 8             |
| Saídas                   | Out              | 9             |
| Movimentos automáticos de fiado (saída)   | Out | — (system, M7.7 Phase 5.6) |
| Movimentos automáticos de fiado (entrada) | In  | — (system, M7.7 Phase 5.6) |

### TransactionTypes

| Name              | Parent Category     | SettlementRule        | RequiresTabAccountAndClient | Access tipo_id |
|-------------------|---------------------|-----------------------|-----------------------------|----------------|
| Tarifa Bolão      | Receita             | SameDay               | false                       | — (Access prod 7; M10 target) |
| Operador          | Entradas            | SameDay               | false                       | — (Access prod 12; M10 target) |
| Cliente           | Saídas              | SameDay               | true                        | 2              |
| Depósito Dinheiro | Saídas              | NextCalendarDay       | false                       | 3              |
| Cartão de Crédito | Saídas              | TwoBusinessDays       | false                       | 4              |
| MarketPlace       | Saídas              | SameDay               | false                       | 5              |
| Sobra de Bolão    | Despesas Comerciais | SameDay               | false                       | 6              |
| Sobra de Federal  | Despesas Comerciais | SameDay               | false                       | 9              |
| Depósito Cheque   | Saídas              | OperatorEnteredCheque | false                       | 15             |
| PIX               | Saídas              | SameDay               | false                       | 16             |
| Cartão de Débito  | Saídas              | NextBusinessDay       | false                       | 17             |
| Telesena          | Saídas              | SameDay               | false                       | 18             |
| Troca de Telesena | Saídas              | SameDay               | false                       | 19             |
| Raspadinha        | Saídas              | SameDay               | false                       | 20             |
| Encalhe Federal   | Saídas              | SameDay               | false                       | 22             |
| Cliente           | Entradas            | SameDay               | true                        | 23             |
| Pgto Prêmio       | Saídas              | SameDay               | false                       | 24             |
| Desconto          | Despesas Comerciais | SameDay               | false                       | 25             |
| Volante rejeitado | Despesas Comerciais | SameDay               | false                       | 26             |
| Tarifa cartão     | Despesas Comerciais | SameDay               | false                       | 27             |
| Outras Despesas   | Despesas Comerciais | SameDay               | false                       | 28             |
| Saída de estoque contado — venda fiado   | Movimentos automáticos de fiado (saída)   | SameDay | false | — (`SystemRole` FiadoCountedStockSaleCounterpart) |
| Entrada de dinheiro — recebimento fiado  | Movimentos automáticos de fiado (entrada) | SameDay | false | — (`SystemRole` FiadoCashRepaymentCounterpart) |

Note: "Cliente" appears twice — under Saídas (credit sale, money leaves) and Entradas (client payment, money enters). This is the Fiado in/out mechanism. No seeded type carries a `FiadoPaymentKind`: a branch configures its repayment methods (§3.10). The last two rows are the system-owned counterpart types (§3.10); they have no Access equivalent and are never offered for manual create.

**Report classification of the shipped fresh seed (M11 Phases 4 and 6; product evidence updated in §9).** Only these seed rows are classified, by the seed factory's own row identity; every other seeded type — the system types included — is `None`, and existing branches are never classified by name (§3.10):

| Seed row          | `SettlementInstrument` | `MonthlyFinancialRole` | Product provenance / planning evidence |
|-------------------|------------------------|------------------------|----------------|
| Depósito Cheque   | Cheque                 | None                   | SQ2 / §9 product Q2: paid = deposited (2026-10-23); single cheques evidenced by export, counts remain reported |
| Cartão de Crédito | CreditCard             | None                   | SQ1 / §9 product Q1: owed by the acquirer until payout |
| Cartão de Débito  | DebitCard              | None                   | SQ1 / §9 product Q1 |
| Tarifa Bolão      | None                   | BolaoServiceFee        | SQ3 / §9 product Q3: that day's fee |
| Sobra de Bolão    | None                   | CommercialLeftover     | SQ4 / §9 product Q4: commercial loss |
| Sobra de Federal  | None                   | CommercialLeftover     | SQ4 / §9 product Q4 |

`Operador` (M9.5 question 9) is a separate inflow and is never marked as the fee.

### Products (DailyClose items)

| Name             | DisplayOrder | Access prod_id |
|------------------|--------------|----------------|
| Telesena         | 1            | 2              |
| Raspadinha       | 2            | 3              |
| Jogos            | 3            | 4              |
| Loteria Especial | 4            | 5              |
| Dinheiro         | 5            | 6              |
| Federal          | 7            | 11             |
| Diferença Caixa  | 8            | 13             |

Note: **Fiado is NOT a DailyClose product**: client balance is queried from Tab transactions (§6.4). The inspected Access export includes an early Fiado slot (`prod 1`); later form code calculates it. Under working Q9, M10 defaults skip/transform `prod 1/8/9/10` (Fiado, Total Caixa, hours, absence), while `prod 7` (Tarifa Bolão) and `prod 12` (Operador) target **Terminal In transactions**, one per non-zero Terminal/day value. This is a pending M10 mapping, not an implemented importer.

**Shipped Q3/Q9 seed use (P7 changes pending).** Enter the actual day's fee under Receita and the actual Operador inflow under Entradas through the existing transaction command. Both are ordinary `SameDay` types, require neither Tab nor client, and inherit `In`. Operador is interpreted as a CAIXA report expectation, not a confirmed cash top-up; exact definition, printed label and sign remain pending Q9. Signed generated report and Tarifa Especial are future P7 contracts (§6.19 O08). No automatic fee/rate, synthetic receipt or mandatory daily row is created. The fee and Operador are excluded from the fresh product catalog; their physical cash remains in Dinheiro. Other product-like type names stay unchanged pending shop questions 5/6.

**Existing branches.** `M95OpeningSideInflows` is data-only. Old random seed IDs and mutable labels cannot establish provenance. An operator-reviewed per-branch ID map identifies the old fee product (or explicitly attests none) and the active ordinary In categories (or authorizes new ones). Same-name custom types are never adopted; collisions require an unused reviewed label. Only the identified product is soft-deactivated. Historical items, close identities/versions, official variances, approvals and opening chains are untouched. Retired existing products remain retired. The migration refuses any active retained fee count on an unlocked close, in every status, because gap AA has no approved save/preview recovery. Locked history remains readable; its fee still contributes to a later opening under the unchanged calculator. A retired fee appearing only in the prior close is omitted from current review rows even though the calculator includes its active saved amount in TotalOpening. The amount is thus invisible in the current review; use the per-Terminal inventory and prior-close read procedure in the operational guide before submitting/approving the first transition. Therefore the first transition from old history is **not** certified zero/parity; no opening is rewritten to force it. See [database environments](../server/docs/database-environments.md#m95-opening-side-inflow-upgrade) for preflight and forward-only recovery. M10 opening count/import and M11 classification/reporting remain pending.

### 5.1 Brazilian Holiday Calendar

Milestone 6 adds a deterministic Brazilian calendar source used by `GET /holiday/import-br/{year}/preview` and `POST /holiday/import-br/{year}`. This source is branch-agnostic and has no external network dependency: fixed-date national holidays are emitted directly, and Easter-relative dates are computed with the Anonymous Gregorian algorithm, also known as the Meeus/Jones/Butcher algorithm. The supported tested range is 1900-2200.

When `includeOptionalFederal = false`, the API returns/imports only the 10 `National` entries. When `includeOptionalFederal = true`, it appends the 3 `OptionalFederal` entries below and returns all 13 rows in date order.

#### National holidays

| Date rule       | Description                | Type     | Basis                                                                                  |
|-----------------|----------------------------|----------|----------------------------------------------------------------------------------------|
| 1 Jan           | Confraternização Universal | National | Mandatory federal holiday                                                              |
| Easter - 2 days | Sexta-feira Santa          | National | Law 9.093/1995 religious-holiday basis; included in the mandatory operational baseline |
| 21 Apr          | Tiradentes                 | National | Mandatory federal holiday                                                              |
| 1 May           | Dia do Trabalho            | National | Mandatory federal holiday                                                              |
| 7 Sep           | Independência              | National | Mandatory federal holiday                                                              |
| 12 Oct          | Nossa Senhora Aparecida    | National | Mandatory federal holiday                                                              |
| 2 Nov           | Finados                    | National | Mandatory federal holiday                                                              |
| 15 Nov          | Proclamação da República   | National | Mandatory federal holiday                                                              |
| 20 Nov          | Consciência Negra          | National | Law 14.759/2023 declared the date a national holiday                                   |
| 25 Dec          | Natal                      | National | Mandatory federal holiday                                                              |

#### Optional federal operational subset

These rows are the curated M6 operational subset of MGI annual optional federal `pontos facultativos`. They are not mandatory national holidays by federal law. State or municipal law, banking practice, or branch policy may make any of them operationally closed for a specific branch.

| Date rule        | Description                      | Type            | Basis                                                                      |
|------------------|----------------------------------|-----------------|----------------------------------------------------------------------------|
| Easter - 47 days | Carnaval (terça)                 | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |
| Easter - 46 days | Quarta-feira de Cinzas (até 14h) | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |
| Easter + 60 days | Corpus Christi                   | OptionalFederal | MGI annual optional federal calendar; deterministic Easter-anchored subset |

The MGI annual optional calendar is broader than this subset and can include Carnival Monday, the Corpus Christi bridge Friday, Christmas Eve and New Year's Eve afternoons, Dia do Servidor Público, and other bridge days that vary by annual decree. Those are intentionally not bulk-imported by the M6 default because they are not fixed by federal law and are not all Easter-derived. Branch admins can add any additional local or annually declared optional day through the existing `POST /holiday` endpoint.

#### Sources

Both import endpoints accept a `source` query parameter (`Composite | Canonical | BrasilApi | Nager`) that selects which source the resolver uses. `source = Composite` is the default and is what existing M6 Phase 6 callers continue to get when they omit the parameter.

**Composite ordering.** When `source = Composite`, the resolver walks providers in priority order `Nager → BrasilAPI → Canonical`. For each of the 13 canonical concepts the highest-priority provider that returns a name-matching row claims it (with that provider's date and description); any concept still unclaimed after both providers run is backfilled from the canonical calendar. Composite **never** surfaces 502 because the canonical backfill always provides a value — failed provider results are logged as warnings and skipped.

**Concept catalog identity.** The 13 canonical concepts each have a stable `ConceptId` (`CONFRATERNIZACAO_UNIVERSAL`, `CARNAVAL_TERCA`, `QUARTA_FEIRA_CINZAS`, `SEXTA_FEIRA_SANTA`, `TIRADENTES`, `DIA_DO_TRABALHO`, `CORPUS_CHRISTI`, `INDEPENDENCIA`, `NOSSA_SENHORA_APARECIDA`, `FINADOS`, `PROCLAMACAO_REPUBLICA`, `CONSCIENCIA_NEGRA`, `NATAL`), a canonical description, a `BrazilianHolidayType`, an Easter-aware `ExpectedDateForYear` function (Easter is computed once via the Meeus/Jones/Butcher algorithm), and a list of accent-stripped lowercased name-match keywords. Provider rows are matched to concepts by **name pattern first, then ±3-day date proximity tiebreaker** among same-name candidates (Nager returns two "Carnaval" rows for Mon+Tue — the one nearer Easter−47 wins). Provider rows that match no concept are dropped silently. Provider rows that match by name but fall further than ±3 days from the expected date still match — the winning source's date overrides expected, so a future provider update or a leap-year corner case never silently drops a real holiday.

**Provider quirks.**

- **BrasilAPI** (`https://brasilapi.com.br/api/feriados/v1/{year}`) renames a few concepts versus the federal-law text: "Confraternização mundial" instead of "Confraternização Universal", "Dia da consciência negra" instead of "Consciência Negra", "Independência do Brasil" instead of "Independência". The catalog's name matchers normalize over these differences so the same concept still gets claimed. BrasilAPI also returns "Páscoa" (Easter Sunday) which matches no concept and is dropped.
- **Nager.Date** (`https://date.nager.at/api/v3/PublicHolidays/{year}/BR`) returns regional rows with `global: false` and a populated `counties` array (e.g. "Revolução Constitucionalista de 1932" for `BR-SP`). All `global: false` rows are dropped — the calendar tracks only national entries. Nager's English `name` field is concatenated with `localName` before normalization so English-keyword matchers (e.g. "good friday", "ash wednesday", "labour day") can also hit.
- **Quarta-feira de Cinzas** is missing from both providers in practice. With `source = Composite | BrasilApi | Nager`, the catalog's canonical Easter-46 date always backfills this concept, and the persisted `Holiday.Source` for that row is `Canonical` even when the rest of the import came from a provider.

**Per-row provenance.** Both `ResponseBrazilianHolidayPreviewItemJson` and `ResponseBrazilianHolidayImportItemJson` carry a `Source` field tagging the actual provenance per row (`Canonical | BrasilApi | Nager`). The top-level response envelopes (`ResponseBrazilianHolidayPreviewJson`, `ResponseBrazilianHolidayImportJson`) echo the requested `Source` so a UI can label the result. On `POST /holiday/import-br/{year}`, each persisted `Holiday.Source` column receives the per-concept tag from the resolver — so a row that BrasilAPI claimed during a composite run is persisted with `Holiday.Source = BrasilApi`, while a row that fell through to canonical backfill is persisted with `Holiday.Source = Canonical`.

**Failure contract.** Explicit single-source calls (`source = BrasilApi` or `source = Nager`) return **502 Bad Gateway** with the `HOLIDAY_SOURCE_UNAVAILABLE` error key when the provider HTTP call times out, returns a non-2xx status, or returns malformed JSON. The Infrastructure provider clients themselves never throw `server.Exceptions` types — they return a domain `BrazilianHolidayProviderResult<T>` with `Success = false`, and the Application-layer resolver is the single place that translates a failed result into `ExternalProviderUnavailableException`. `source = Composite` is unreachable for this error path because Canonical never fails.

**Operational pattern.** The route range stays `min(1900):max(2200)` — admins can preview/import any year inside that range. The recommended cron pattern (external to this repo, not shipped by Phase 6.5) is a single annual run in late December that imports `currentYear + 1`, so day one of the next year already has its calendar populated. Re-running the same year is all-Skipped because the `(BranchId, Date) WHERE Active = true` filtered unique index makes the import idempotent.

---

## 6. Business Rules

### 6.1 Transaction creation

- `Value` must be > 0 (always positive, `Direction` determines sign semantics)
- **Classification invariant:** `TransactionTypeId` is the only input from the operator. On creation, the system automatically sets:
  - `CategoryId` = `TransactionType.CategoryId`
  - `Direction` = `Category.DefaultDirection`
  - These denormalized fields are NEVER independently editable after creation
- The Fiado in/out distinction is handled by two separate TransactionTypes ("Cliente" under "Entradas" = In, "Cliente" under "Saídas" = Out), not by overriding Direction
- `RequiresTabAccountAndClient = true` on a TransactionType is enforced by the branch-consistency layer: the transaction must use an `Account` with `Type == Tab` and must include `ClientId`. Both seeded "Cliente" rows set this flag; all other seeded TransactionTypes set it to false.
- **Generic creates never bypass fiado rules (M7.7 Phase 5.1/5.6).** `POST /transaction`, `POST /transaction/installment` and their previews reject a system-owned type (409 `TRANSACTION_TYPE_SYSTEM_ONLY`) and any `In` row on a Tab account (409 `TRANSACTION_FIADO_PAYMENT_REQUIRES_COMMAND`): a Tab `In` is a repayment by effect (§6.4 netting) and is recorded only through *Receber pagamento*. `RequestCreateTransactionJson.HandsOutCountedStock` (default false) declares that a fiado sale — a Tab `Out` with a client — hands out counted stock and therefore also writes the generated Terminal `Out` (§6.4); on any other row it is 409 `TRANSACTION_COUNTED_STOCK_REQUIRES_FIADO_SALE`. It is never assumed.
- `DueDate` defaults are driven by `TransactionType.SettlementRule`:
  - `SameDay`: same as `Date`
  - `NextCalendarDay`: `Date + 1 day`
  - `NextBusinessDay`: next business day after `Date` (skips weekends and branch holidays)
  - `TwoBusinessDays`: second business day after `Date` (skips weekends and branch holidays)
  - `OperatorEnteredCheque`: operator enters custom date; must be ≥ `Date`
- `RecordedByOperatorId` is set from the authenticated operator's context
- `CreatedByUserId` is set from the authenticated user's session
- `Draft` status transactions are excluded from all financial calculations (sums, balances, reports)
- Only `Active` transactions count in ledger totals

**Financial-create idempotency.** `POST /transaction` and `POST /transaction/installment` require a client `Idempotency-Key` header containing 1–128 visible ASCII characters. Its persisted scope is the exact endpoint plus authenticated `BranchId`, `UserId`, and key, so the same text may be used independently by another command, branch, or user. The typed request is serialized with the web JSON contract and canonicalized by ordinal object-property ordering, stable invariant decimal/double formatting, and preserved array order before uppercase SHA-256 hashing. A matching unexpired key and hash replays the persisted response envelope, including the same resource id and `Version`; it does not execute the business command again. The same scoped key with a different hash returns 409 `IDEMPOTENCY_KEY_PAYLOAD_CONFLICT`. Missing/invalid keys return 400 `IDEMPOTENCY_KEY_REQUIRED` / `IDEMPOTENCY_KEY_INVALID`.

Reservations expire after **24 hours**. The exact idempotency scope is serialized with a transaction-scoped PostgreSQL advisory lock, and the replay row plus financial write commit in the same database transaction. Concurrent equal requests therefore produce exactly one write and deterministic replay; a crash can roll back both, never strand a reservation or an unpaired business write. The reusable infrastructure is also the required delivery contract for the Phase 5 *Receber pagamento* command when that command ships.

A committed replay lookup runs first in a short transaction that acquires only the idempotency advisory key. A matching retry therefore returns its durable envelope without waiting for DailyClose account/month coordination and cannot become `DAILYCLOSE_LEDGER_COORDINATION_BUSY` merely because Submit/Approve is holding the account boundary. A miss or expired key releases that short scope, acquires the normal account coordination, and rechecks/reserves the idempotency scope inside the same transaction as the business write; the atomicity guarantee above remains unchanged.

The replay-first coordinator sets a transaction-local PostgreSQL `lock_timeout` before taking the idempotency advisory key: five seconds by default, independently configurable through `IdempotencyRequestCoordination:LockTimeoutSeconds`. If that wait expires, the request returns retryable 409 `IDEMPOTENCY_COORDINATION_BUSY`; it never falls through to the longer provider command timeout or reuses the unrelated DailyClose coordination message. The account-coordinated reservation path inherits its own transaction-local timeout and uses the same conflict key.

Expiry also drives physical retention. A hosted cleanup runs once every 24 hours by default and selects `ExpiresAt <= utcNow` candidates ordered by `ExpiresAt, Id`, using `IX_IdempotencyRequests_ExpiresAt`. Each deletion transaction is capped at 500 candidates, while the daily run drains successive bounded batches until none remain; therefore an uninterrupted default deployment normally retains an envelope for 24 to 48 hours. Every candidate is serialized on its normal idempotency advisory key and its expiry is rechecked in the deletion transaction, so a concurrent expired-key refresh wins safely and multiple API instances may sweep without deleting a live replay. Both interval and batch size are positive, deploy-time settings documented in `server/docs/database-environments.md`.

### 6.2 Transaction cancellation

- Transactions on the current day: operator can cancel their own (checked via `RecordedByOperatorId == currentOperator.Id`)
- Transactions older than today: requires Role.Manager or Role.Admin
- Transactions on or before `Setting.LockDate`: cannot be cancelled by anyone
- Cancellation sets `Status = Cancelled`, `CancelledAt = now`, `CancelledByUserId`, and requires `CancellationReason`
- Cancelled transactions remain in the database, visible in audit views, excluded from financial calculations
- `POST /transaction/{id}/cancel` requires the current Transaction ETag in `If-Match` and returns the new ETag; see the exact §6.11 concurrency wire contract.

### 6.3 Installments (cheque pre-dated)

When a pre-dated cheque is recorded with installments, the system creates N separate Transaction rows, one per installment. The endpoint requires a TransactionType with `SettlementRule = OperatorEnteredCheque`; otherwise the request is rejected.

**Manual rows are the default.** Each installment item carries its own `Value` and `DueDate`. The request-level `Value` must equal the exact sum of all row values. Row count is bounded `[2..24]`. Row due dates must all be on or after the transaction `Date`, the first must be in the future, and they must be strictly increasing.

**Auto-generation is optional** via `AutoGenerateInstallments = true` together with `InstallmentCount` and a base `DueDate`. When enabled:
- All generated rows have equal `Value`, rounded to 2 decimal places (`MidpointRounding.AwayFromZero`); any residual goes on the last row so the row sum matches the request total exactly.
- Generated `DueDate`s are staggered monthly from the base date (`baseDueDate.AddMonths(i-1)`); any row falling on a weekend or a branch holiday is moved to the next business day.
- If the auto-split would produce any non-positive row (e.g. `0.10` over 6 installments), the request is rejected.
- The base `DueDate` must be in the future; manual `Installments` must be empty when the flag is on.

**Row description format:** `CH PRE ({i}/{N}) - {Description}` when the operator supplies a non-empty description; otherwise the row description is exactly `CH PRE ({i}/{N})` with no trailing separator. The operator-supplied description is capped to leave room for the longest possible prefix (`CH PRE (24/24) - `, 17 chars) so the persisted row description fits the `varchar(500)` column.

**All installments share the same `OriginTransactionId`, including the first** (which points to itself). This simplifies group lookups: `WHERE OriginTransactionId = @groupId` returns all members. This matches the Access behavior where `lco_origem` on the first installment is set to its own `lco_id`.

`SaveAsDraft = true` persists every generated/manual row with `Status = Draft`; the default is `Active`.

The installment create uses the same persisted idempotency contract as the single-row create, with its own endpoint scope. Its replay envelope contains the full ordered plan response; `ResourceId` records the first installment id. The non-empty plan is inserted by one PostgreSQL transaction, so every returned row has the same create-time `xmin`; `ResponseCreateTransactionInstallmentJson.Version` exposes that plan token and the 201 `ETag` matches it. Response mapping rejects an empty plan or mixed row versions as an internal invariant violation instead of indexing an unchecked first row.

**Atomic plan finalization (M7.7 Phase 7.6.2, gap Y; owner decisions 2026-10-16 for mixed plans, Draft save and discard).** A plan is identified by its `OriginTransactionId` (the first installment's own id); there is no plan entity.
- **Plan read.** `GET /transaction/installment/{originTransactionId}` (any branch role; missing, cross-branch or not a plan → 404 `TRANSACTION_PLAN_NOT_FOUND`; a Member needs an operator link and the plan's account in scope → 403) returns `ResponseInstallmentPlanJson { OriginTransactionId, Status, AccountId, Date, DraftCount, ActiveCount, CancelledCount, TotalValue, Installments }`. `Status` is `InstallmentPlanStatus`: **Draft** when every installment is Draft, **Cancelled** when every one is Cancelled, **Active** when no Draft remains, and **Mixed** when Draft installments sit alongside Active and/or Cancelled ones. `TotalValue` excludes Cancelled installments; `Installments` (every status, `DueDate, Id` order) carry their versions. Clients use `Status` to open a plan in finalize mode or in per-row correction mode.
- **Finalize.** `POST /transaction/installment/{originTransactionId}/finalize` with `RequestFinalizeInstallmentPlanJson { Installments: [{ TransactionId, Version }], ConfirmMixedPlan }`. Under the plan account's §6.6 coordination (every installment shares the plan's account and business date), the server resolves the authoritative installments by `OriginTransactionId` inside the branch and requires: at least one Draft installment (else 409 `TRANSACTION_PLAN_NOT_DRAFT`); for a Mixed plan, `ConfirmMixedPlan = true` (else 409 `TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED` — only the remaining Draft installments are ever activated, never silently); the request set equal to the current Draft installments with every version matching (else 409 `TRANSACTION_PLAN_STALE_WRITE`; a per-row edit that lands after this check and before the save returns the same key, with nothing persisted); then the per-row rules for every Draft installment — Member account scope, the §6.11 mutation permission (recording operator, same local day), the lock date, and the DailyClose ledger freeze. All Draft installments become Active with one `UpdatedAt`/`UpdatedByUserId` stamp in one commit, or none do. It returns the plan read shape. Versions travel in the body because one `If-Match` names only one row; retry follows the guarded-write rule (a replay after an uncertain result returns 409 and the client re-reads), with no `Idempotency-Key`. Validation: the list is required and non-empty, ids non-empty and distinct, versions positive.
- **No bypass.** `POST /transaction/{id}/finalize` refuses any Draft installment of a plan with 409 `TRANSACTION_PLAN_FINALIZE_REQUIRED`; a standalone Draft keeps its individual finalize. Example: three R$ 100 Draft installments contribute nothing until the plan command makes all three Active and contributes R$ 300 at once; one stale or blocked installment prevents all three.
- **Unchanged per-row rules.** Draft edits are per-row saves (`PUT /transaction/{id}` with that row's `If-Match`, the same editable fields and guards); there is no atomic plan save. Discarding is per-row cancel; there is no plan discard command, so cancelling some Draft installments makes the plan Mixed. After activation, corrections stay per row (§6.11) — cheques clear on different days. Nothing here changes amounts, the plan total, the installment count, account, business date, type or direction.

Credit card transactions do NOT create multiple rows — the total is stored as one transaction with description "Parcelado 3x" or "à vista". The installment split is informational only for credit cards in the current system.

### 6.4 Fiado (tab) balance

Fiado is a client's debt with the branch, recorded on Tab accounts. M7.7 Phase 5 implements the signed decision 1.3 (client-level balance + explicit *Receber pagamento*, FIFO query-time netting) and its 2026-10 amendment (linked Terminal movements). Per-row `PaidAt` plays **no** part in fiado: it neither settles a sale nor removes it from aging; a legacy stamp is ignored.

**Authoritative projection.** One SQL projection (`FiadoNettingSql` in the infrastructure layer) feeds every fiado read — balance, aging and the client statement — so they cannot drift apart. Its input is the branch's active rows (`Active = true AND Status = Active`; Draft and Cancelled excluded) on **Tab** accounts with a client, dated on or before the as-of date. It never persists allocations or balances.
- **Balance** per client = `Σ Out − Σ In` across **every** Tab of the branch. A negative balance is client **credit**; storage and formula keep the sign, and clients display it as a positive credit (design formatting §3). No credit-spending or refund command exists.
- **FIFO netting** is branch/client-wide: the client's cumulative `In` (on any Tab) is applied to the client's `Out` rows ordered `DueDate, Date, Id`; each sale keeps `Remaining = min(Value, max(0, CumulativeOut − TotalIn))`. Only sales with a positive remainder are open. Repayment `In` rows and credit never age as debt; a debtor's open remainders always add up to its balance, and a client in credit has none.
- **Account filtering happens after netting.** A Tab filter narrows the netted open sales; it never re-nets per Tab. A repayment received on Tab B therefore settles the oldest-due debt on Tab A first.
- **`OldestUnpaidDate`** (owner decision 2026-10-05) is the earliest sale `Date` — not `DueDate` — among **all** sales still holding a positive remainder, computed over all of them rather than taken from the first row in due order; null when none remains. Aging keeps using due dates.

**Receber pagamento (M7.7 Phase 5.1).** `POST /transaction/fiado-payment` with `RequestReceiveFiadoPaymentJson { Date, Value, TransactionTime?, Description?, TransactionTypeId, AccountId, ClientId, RecordedByOperatorId?, SaveAsDraft }` → 201 `ResponseCreateTransactionJson` with a strong `ETag`. The method is `TransactionTypeId`: required with no default (empty → 400), and it must be a configured repayment method (§3.10) — any other type → 409 `TRANSACTION_FIADO_PAYMENT_METHOD_INVALID`; `ClientId` is required (400 `TRANSACTION_FIADO_PAYMENT_CLIENT_REQUIRED`). `AccountId` is the explicit receiving Tab: a Member needs an active `OperatorAccount` grant for it (decision 1.8), Manager/Admin may use any branch Tab, and recording identity follows the §6.1 rules. Netting stays branch-wide, so the receiving Tab only records where the money came in. The command honors the §6.11 persisted `Idempotency-Key` contract under its own endpoint scope: a committed replay returns the original complete operation (payment id and `GeneratedTransactionId`) even after the day freezes, the same key with a different payload conflicts, concurrent same-key requests write one operation, and a failure after reservation leaves no row and no reservation.

**Linked Terminal movements (M7.7 Phase 5.6, M9.5 Phase 2.2 option (a)).**
- A fiado sale created with `HandsOutCountedStock = true` writes the Tab `Out` and, in the same commit, a generated Terminal `Out` of the same value — a departure of counted stock, not a cash withdrawal. A `CountedCash` repayment writes the Tab `In` and a generated Terminal `In`. A `NonCash` repayment writes the Tab `In` only: no drawer movement, no change to counted Dinheiro, and no close requirement. The same Tab can receive both kinds; the distinction belongs to the method, not the Tab.
- The generated leg goes to the Tab's paired terminal (§3.7; none → 409 `TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED`) and uses the system counterpart type (§3.10). It copies the source's date, value, time, status, `RecordedByOperatorId` and `CreatedByUserId`; its `ClientId` is null and its `DueDate` is its date.
- **Exactly once.** The Tab remains the source of client balance, FIFO and aging; the Terminal leg explains the assets in the operator's custody. The pair is not two sales or two receipts, and the Tab balance is never added to the cash-variance formula (§6.12).
- **Atomic pair.** Both rows or neither. The command locks the Tab and the terminal in one §6.6 coordination scope (ascending key order) and re-reads the pairing inside it. The terminal leg needs that terminal's open Draft close for the date exactly like any Terminal write (409 `TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE`; a Submitted/Approved/Rejected close → 409 `TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN`), and a Member needs both the explicit Tab grant and the terminal in scope (403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`). Draft legs stay Draft together; finalizing or cancelling the source finalizes or cancels its counterpart in the same commit, under the terminal's own ledger and scope checks; cancellation corrects the records and is not a refund. Physical counts stay human-entered: no command rewrites DailyClose items and no stock-quantity ledger exists.
- **Still open (not implemented):** receiving physical cash anywhere other than the paired terminal, mixed cash/non-cash tender, bank-side PIX reconciliation, reversals/refunds, inconsistent legacy links, legacy backfill, and which fiado sales (bolão, items outside the counted-products list) move counted assets — the sale declares it per operation until that is decided (§9).

**Client fiado statement (M7.7 Phase 5.5).** `GET /client/{clientId}/fiado-statement?DateFrom&DateTo&Page&PageSize` → `ResponseClientFiadoStatementJson { ClientId, ClientName, AsOfDate, OutstandingTotal, OldestUnpaidDate?, DateFrom?, DateTo, OpeningBalance, ClosingBalance, Items, Page, PageSize, TotalCount, TotalPages, HasNext, HasPrevious }`, items `ResponseClientFiadoStatementItemJson { TransactionId, Date, Direction, Value, AccountId, AccountName, TransactionTypeName, Description?, RunningBalance }`.
- **Window.** Optional on both sides: without `DateFrom` (response `DateFrom = null`) the statement starts at the first movement; without `DateTo` it ends at branch-local today. The **resolved** window is validated: `DateFrom` after `DateTo` — including a `DateFrom` after today with `DateTo` omitted — → 400 `REPORT_DATE_RANGE_INVERTED`; there is no span limit because the default is the whole history. `PageSize` defaults to 50 (1–100, else 400 `CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID`); `Page ≥ 1` (400 `REPORT_PAGE_INVALID`); any valid page, up to `int.MaxValue`, is answered (the SQL offset is computed in 64 bits), beyond the last page with an empty `Items`.
- **Rows and order.** The client's active fiado rows on every Tab in the window, ordered `Date DESC, CreatedAt DESC, Id DESC`. `RunningBalance` is computed over the client's full chronological history (`Date, CreatedAt, Id`): each row's running balance is the chronologically previous row's plus its own `Out − In`, so consecutive rows reconcile on any page, ties and page boundaries included.
- **Window balances.** `OpeningBalance` is the signed balance of every movement dated before `DateFrom` (0 without it) and `ClosingBalance` adds every movement in the window across **all** pages, so `OpeningBalance + Σ(Out − In over every window row) = ClosingBalance`. No page-level boundary balance is returned.
- **Position.** `AsOfDate` is branch-local today; `OutstandingTotal` and `OldestUnpaidDate` are the shared projection's position as of that date, so a window ending today closes at `OutstandingTotal` and the statement agrees with `GET /report/fiado/balance`.
- **Access.** Manager/Admin read any branch client — a deactivated client keeps its statement, like the balance report. A Member must hold at least one active `OperatorAccount` grant on an active Tab and then reads the complete branch-wide statement, including repayments on Tabs outside their grants (hiding them would break the running balances); without such a grant → 403 `CLIENT_FIADO_STATEMENT_ACCESS_DENIED`, which is checked before validation. A Terminal grant never implies fiado authority. Unknown or cross-branch client → 404 `CLIENT_NOT_FOUND`. The use case only reads (architecture never-commits scan).

### 6.5 Daily closing

**Shipped contract (v59):** each close below is now a session of one cashbox (§3.30, §6.20); its review workflow is unchanged, its routes are `/session/{sessionId}/…`, and its rows, opening and successor follow the session rather than the account/day. A cashbox may hold several sessions per day; only one is in custody at a time. OpeningCount plus inline opening ship with P3.

**No close-day fiado reference (gap A, owner decision 2026-10-05 — supersedes the signed 2026-07-27 sub-decision; M7.7 Phase 5.2).** The close-day form, correction form included, shows **no** fiado balance for any role, and no member-scoped paired-Tab balance read exists: a raw Tab balance records where movements were registered, not what clients owe (a sale on one Tab paid on another reads +R$ 100 / −R$ 100 while the client owes zero). A client's debt is read from the client fiado statement (§6.4) under its own access rules.

1. Custody starts with Conferir abertura e iniciar (`POST /session/start`, or the first financial operation carrying the same acknowledgment): a Draft session on branch-local today, stamping immutable `OpenedByUserId`, the custodian and the acknowledged opening. A past period is recorded only by a Manager/Admin `DeclarePastSession` (owner decision F2); nobody starts a future session and there is no account/day open.
2. A permitted user saves the complete count. The first successful `PUT /items`, including an explicit empty list, atomically stamps immutable-once-set `ItemsFirstRecordedAt`, `RecordedByUserId`, and nullable `RecordedByOperatorId`. Before that save, any Member scoped to the account may claim an unlocked close, including one backdated and opened by an elevated user; account coordination makes the first claim win. Later/retry/notes-only/correction saves never reassign the recorder.
3. A permitted user submits only after the count exists, with no Draft row of the session, no uncounted earlier session of the same cashbox carrying activity and no unresolved legacy cashbox activity after the lock date. Submit stamps the current `SubmittedByUserId` and nullable `SubmittedByOperatorId` without changing the recorder, computes and persists CashVariance, then freezes the same account/day ledger. A Member who claims a fresh backdated close still cannot submit it because the ordinary same-day Submit gate is unchanged; Manager/Admin submits it instead.
4. Manager reviews:
   - Approve (Status=Approved, ApprovedAt=now)
   - Reject with reason (Status=Rejected, RejectionReason filled)
5. If rejected, direct resubmit is allowed; the first permitted correction save transitions `Rejected -> Draft`, retains `RejectionReason`, and remains editable by the recording operator until lock or by Manager/Admin.
6. If submitted by mistake, explicit `POST /session/{id}/recall` (**Desfazer envio**) returns it to Draft. The recording Member may Recall only on the same branch-local day; Manager/Admin may Recall pre-lock. Ordinary item-save against Submitted returns `DAILYCLOSE_NOT_EDITABLE`.
7. Manager/Admin may `POST /session/{id}/reopen` an Approved close. Reopen returns it to Draft, clears submission/approval fields, retains counted eligibility and the physical CashVariance row, unfreezes the ledger, and requires submit + approval again.

**Opening values** for the current day = closing values from the most recent prior active **counted** close for the same account: `ItemsFirstRecordedAt != null`, regardless of Draft/Submitted/Approved/Rejected state. The backward query orders `Date DESC, CreatedAt DESC, Id DESC`; the mirrored forward successor query orders `Date ASC, CreatedAt ASC, Id ASC`. Both use the identical `Active && ItemsFirstRecordedAt != null` eligibility predicate, handling weekends, holidays, never-counted closes, and missing days without an operating calendar.

**Submit prerequisites and interval.** Submit resolves `LockDate` and the opening source once while holding account coordination. It rejects the current close without `ItemsFirstRecordedAt` using `DAILYCLOSE_ITEMS_NOT_RECORDED`, and rejects same-day Draft transactions using `DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS`. It then finds the earliest date in `(max(opening-source date or DateTime.MinValue, LockDate), close date)` that has an active, non-soft-deleted `Transaction` with `Status = Active` but no active counted close and rejects with `DAILYCLOSE_PRIOR_DAY_NOT_COUNTED`. Both endpoints are exclusive; Draft/Cancelled/soft-deleted transactions do not create a gap, and counting the blocking date provides the escape path.

**Close-day review context.** `GET /session/{sessionId}/review` enumerates **every active branch product**, even when the close has no saved items, plus any retired product that still has an active saved item on the current close; rows are ordered by `DisplayOrder ASC, ProductId ASC`. Each `ResponseDailyCloseReviewItemJson` is `{ ProductId, ProductName, DisplayOrder, OpeningValue?, ClosingValue?, IsCashVarianceProduct }`. A missing prior product maps to `OpeningValue = 0`; a not-yet-entered current product maps to `ClosingValue = null`. Products that exist only on the prior close are not resurrected, while a retired product already counted on the current close remains visible so its persisted total and variance can be reconciled; newly-active products appear with opening zero and a null closing. The `"Diferença Caixa"` row is always present and flagged by server-resolved product id, with `OpeningValue = null`; its `ClosingValue` is null before calculation and is the persisted submitted value when such a row exists.

**CashVariance** (Diferença Caixa) is system-calculated and persisted as a DailyCloseItem with the "Diferença Caixa" product. See rule 6.12 for the calculation formula. The operator does not enter this value directly. Recall, Reopen, Rejected correction, and opening recheck retain that physical row and `ItemsFirstRecordedAt`, but every ordinary Draft `ResponseDailyCloseJson.Items` and official Draft projection hides it until a successful resubmit updates the same row in place.

**Pre-submit variance.** `POST /session/{sessionId}/variance-preview` accepts the complete candidate item list currently in the form and computes §6.12 through the same `ICashVarianceCalculator` used by Submit. Candidate values are authoritative for the preview — saved Draft items are never substituted. It reuses Submit's DailyClose read/edit scope, status and lock checks plus the item validation rules (active in-branch products only; no duplicate ids; non-negative `numeric(14,2)` values; the reserved variance product forbidden). It returns `ResponseDailyCloseVariancePreviewJson { CashVariance }` and never persists.

**Item-save response, first claim, change detection, and Notes.** `PUT /session/{sessionId}/items` accepts the normalized complete non-variance item set and optional `Notes`; the close `Version` precondition is carried only by the required `If-Match` header, never duplicated in the request body. It discovers the key without tracking, acquires account coordination, reloads, and only then authorizes the tracked state. An unclaimed close (`RecordedByUserId == null`) is the sole item-edit exception that lets any account-scoped Member make the first count; it is deliberately absent from Submit and Recall. Omitted Notes preserve the current value; an empty string clears it; non-empty text is stored verbatim with a 1000-character maximum. Notes are editable on a permitted Draft/Rejected-correction path and frozen once submitted. The response is `ResponsePutDailyCloseItemsJson { DailyClose, AffectedSuccessor? }`; the nested DailyClose carries the new `Version`, and the HTTP response carries its matching `ETag`. An affected successor carries `{ DailyCloseId, Date, PreviousStatus, NewStatus, OpeningRecheckRequiredAt }`. Notes-only, identical, reordered, and retried saves do not invalidate a successor or change recorder identity. Add/remove/value changes and first-count eligibility do. Exactly the earliest later eligible Submitted/Approved/Rejected close returns to Draft with opening-recheck audit; Draft/none is a no-op and no official snapshot is recomputed in place.

**Fiado balance** is NOT stored as a DailyCloseItem and is not part of the close — it is always calculated at query time from Tab account transactions (see rule 6.4) and is not displayed on the close-day form.

### 6.6 Date locking

`Setting.LockDate` defines the immutable cutoff. Transactions on or before this date cannot be created, edited, finalized, or cancelled. Every DailyClose workflow transition on or before this date is blocked: Open, item-save/first count, Submit, Approve, Reject, Rejected correction, Recall, and Reopen. Every TimeEntry mutation is likewise blocked on or before it: dual-shape upsert, whole-entry deactivation, and granular segment add/update/deactivate. `PUT /setting` treats `LockDate` as read-only: any non-null value returns 400 `SETTING_LOCK_DATE_READ_ONLY`; that endpoint may update only the other settings. The sole write path for `LockDate` is the explicit Manager/Admin command `POST /setting/lock-month`.

**Lock-month command.** The body is `RequestLockSettingMonthJson { Year, Month }`; year must be 2000–2100 (`SETTING_LOCK_MONTH_YEAR_OUT_OF_RANGE`) and month 1–12 (`SETTING_LOCK_MONTH_INVALID`), both 400. The command requires the current Setting root ETag in `If-Match`. A successful 200 returns `ResponseSettingJson` with the new `Version` and matching `ETag`, sets `LockDate` to the requested month's final calendar day, and commits the readiness check and update atomically. A stale root returns 409 `SETTING_STALE_WRITE`. The branch-local current month and every future **request** month are unfinished and return 409 `SETTING_LOCK_MONTH_CURRENT_OR_FUTURE`. Replaying a month already at or before a valid historical `LockDate` is a conflict/no-op with 409 `SETTING_LOCK_MONTH_ALREADY_LOCKED`; requesting before the effective operational floor returns 409 `SETTING_LOCK_MONTH_BEFORE_BRANCH_FLOOR`.

`PUT /setting` uses the same Setting-root `If-Match`/ETag contract. Aggregate coordination and client concurrency have separate jobs: the branch boundary makes readiness and writes atomic, while `xmin` rejects a client decision based on a stale Setting representation.

**Interval, initial floor, and legacy recovery.** The command validates every month from the first unlocked month through the requested month, inclusive. `Branch.CreatedAt` is only the **no-data default**: the effective floor is the earliest month among branch-local `Branch.CreatedAt`, every active DailyClose's business `Date` (any status), and every non-soft-deleted Transaction's business `Date` (Active/Draft/Cancelled). Thus a legitimate backfill/import before branch creation remains allowed and is reconciled instead of silently sealed. Clean earlier history passes without manager intervention. A valid existing boundary begins in its next month (or the same month when that old imported value is not month-end), clamped upward to the effective operational floor; a non-MinValue year-1 value therefore cannot trigger thousands of empty monthly queries. A current/future `LockDate` could only have come from the retired PUT path and is invalid under the final contract: the command ignores that untrusted boundary, validates from the operational floor through the requested elapsed month, and replaces it with the requested month-end on success. This narrow self-repair keeps `PUT /setting` closed and never permits a current/future request month. A non-final month that is not ready stops the request with 409 `SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED`, formatted with the first blocking month (`O mês MM/yyyy precisa ser resolvido antes de avançar o bloqueio`), so the manager knows exactly which reconciliation view to open. Therefore a request for June cannot skip unresolved May—or unresolved operational data before the CreatedAt month.

**Activity-derived readiness.** An expected `(Terminal, Date)` pair exists iff there is an active DailyClose in any state for that account/date or a direct, active, non-soft-deleted `Transaction` on the Terminal account with `Status = Active` that date. A close in any non-Approved state returns 409 `SETTING_LOCK_MONTH_UNAPPROVED_CLOSE`; any branch transaction left in Draft returns 409 `SETTING_LOCK_MONTH_DRAFT_TRANSACTIONS`; direct Terminal activity without its corresponding close returns 409 `SETTING_LOCK_MONTH_MISSING_EXPECTED_CLOSE`. When a requested month has expected direct Terminal activity but no close at all, the more actionable blocker is 409 `SETTING_LOCK_MONTH_NO_CLOSE_WITH_EXPECTED_ACTIVITY`. A month with neither closes, direct Terminal activity, nor Draft transactions is empty and lockable.

There is no operating-calendar or activation-window expectation: weekdays, weekends, holidays, and dates before/after account activation follow exactly the same close-or-direct-activity rule. Activity on a paired Tab, including fiado, does not by itself make the Terminal expected. The direct Terminal activity branch deliberately remains even though new API writes require an open close; it detects historical, imported, or persistence-bypassed rows. CEF borderô attestation is outside MVP.

**Canonical concurrency order.** Every participant first takes the branch month boundary and only then, when needed, the Phase 3 `(BranchId, AccountId)` history key. The lock-month command takes the branch boundary exclusively and never takes an account key. Open, item-save, Submit, Approve, Reject, Recall, Reopen, transaction create/installment/finalize/plan finalize/cancel, transaction update, and every TimeEntry mutation take it shared; the thirteen close/ledger participants (the eleven of Phase 3, the Phase 7.6.2 plan finalize and the Phase 5 *Receber pagamento*) then acquire the account key. A physical fiado pair — a counted-stock sale, a counted-cash repayment, or the finalize/cancel of such a source — holds the Tab and its paired terminal in one scope and acquires their account keys in ascending key order, the single global order shared with single-account scopes, so no pair can deadlock another participant. This fixed **month → account** order prevents a deadlock cycle. All locks are PostgreSQL transaction-scoped advisory locks derived in infrastructure from stable SHA-256 namespaced keys with pinned big-endian conversion. The earliest-operational-date reads happen after exclusive acquisition and before readiness evaluation. A participating backdated write therefore either commits first and lowers the observed floor, or waits until the command commits and is rejected by its coordinated `LockDate` recheck. The command checks all months and saves `LockDate` while its exclusive scope remains held; participating writers reload and repeat `LockDateGuard` inside their shared scope before committing. Thus a close-history correction, first count, Recall/Reopen, cascade demotion, ledger write, time-entry snapshot, whole-entry deactivation, or granular segment correction cannot land inside a month whose lock command succeeds.

Acquisition propagates request cancellation and uses transaction-local PostgreSQL `lock_timeout = 5s`. Month-boundary and account-coordination timeouts are configured independently so a test or operational override cannot silently alter both. A busy lock-month boundary, including a nested PostgreSQL `55P03`, returns retryable 409 `SETTING_LOCK_MONTH_COORDINATION_BUSY`; account-key timeout remains retryable 409 `DAILYCLOSE_LEDGER_COORDINATION_BUSY`. No partial state is committed on timeout or cancellation.

### 6.7 Time entry calculation

This logic is implemented in `ITimeEntryCalculationService` / `TimeEntryCalculationService` under `server.Application/Services/TimeEntries/`. The service consumes a list of segment inputs (full `DateTime` pairs) rather than a single clock pair, which makes overnight segments unambiguous without any wrap-around arithmetic.

**Effective-dated policy resolution (M7.7 Phase 7).** The three constants the calculation consumes — `DailyTargetHours`, `LunchDeductionOver6H`, `LunchDeductionOver4H` — come from the §3.20 `TimeEntryPolicy` row applicable to the **entry's own date**: the active row with the greatest `EffectiveFrom` on or before that date, resolved by `TimeEntryPolicyResolver` over the branch's policy rows (single-entry writes and reads resolve one date; list and report paths resolve per row, since one page or window can span an effective boundary). `Setting`'s constants are only the current-policy mirror and are never a calculation input. Consequences: changing the constants today leaves every earlier day's balance untouched; the change applies from the branch-local day it was made (inclusive) until a later policy supersedes it. Get and List recompute **every** row under its entry-date policy — closed rows as well as live-running ones — exactly like the balance report, so all three surfaces answer with one number even for a closed current-day entry read right after a policy change; persisted `TotalHours`/`BalanceHours` stay a write-time checkpoint that reads never rewrite. Every branch has a MinValue-dated initial row (seeded on branch create, backfilled by migration for pre-existing branches from their then-current constants), so resolution is total and a miss is a system invariant breach, not a user error.

All TimeEntry mutations enforce 409 `TIMEENTRY_DATE_LOCKED` when the effective entry date is on or before `Setting.LockDate`: dual-shape `PUT /timeentry` checks the requested date, whole-entry `DELETE /timeentry/{timeEntryId}` checks the loaded row's date, and granular segment `POST`/`PUT`/`DELETE` checks the parent date. Each path acquires the shared branch month boundary before loading mutable state and holds it through lock-date recheck, total recalculation where applicable, and commit. A write therefore either lands before month readiness is evaluated or waits and is rejected after the lock succeeds. Member idempotent tap retries still run inside the boundary; they remain no-persist no-ops while the date is unlocked.

**Method signature (Phase 3.6 target):**

```
ITimeEntryCalculationService.Calculate(
    status,
    segments: IReadOnlyList<TimeEntrySegmentInput>,  // (DateTime ClockIn, DateTime? ClockOut)
    entryDate: DateTime,
    branchLocalNow: DateTime,
    dailyTargetHours,        // caller-resolved from the entry-date TimeEntryPolicy (§3.20)
    lunchDeductionOver6H,    // caller-resolved from the entry-date TimeEntryPolicy (§3.20)
    lunchDeductionOver4H)    // caller-resolved from the entry-date TimeEntryPolicy (§3.20)
  → (TotalHours, BalanceHours)
```

**Dual-shape PUT contract:**

The PUT /timeentry endpoint accepts two mutually exclusive shapes:

- **Member shape:** supplies `Action: Open | Close` (no `Segments`). The server stamps the current branch-local `DateTime` as the segment clock. `Open` appends a new open segment (ClockOut = null). `Close` closes the single open segment for that TimeEntry. Sending both `Action` and `Segments` is rejected with `TIMEENTRY_MEMBER_SHOULD_NOT_SEND_SEGMENTS`. Sending neither when the caller is a Member is rejected with `TIMEENTRY_MEMBER_TAP_ACTION_REQUIRED`.
- **Admin shape (Manager or Admin):** supplies `Segments` (no `Action`) as an explicit list of `RequestTimeEntrySegmentJson { Id?, ClockIn: DateTime, ClockOut: DateTime? }`. The server **reconciles** that snapshot against the entry's active segments by id, in one save — it never replaces the set wholesale: every active persisted segment must be listed by its `Id` (an omitted one, or an unknown `Id`, → 400 `TIMEENTRY_SEGMENT_NOT_FOUND`), so the snapshot cannot delete; an item without `Id` is inserted; a listed segment keeps its `ClockIn` (a changed value → 400 `TIMEENTRY_SEGMENT_CLOCK_IN_LOCKED`) and may change only its `ClockOut`. The resulting active set is then validated as a whole (day bounds, one open segment, no overlap). Removing a segment or correcting its `ClockIn` uses the granular routes: `DELETE /timeentry/segment/{segmentId}` deactivates one segment, `PUT /timeentry/segment/{segmentId}` corrects one segment's `ClockIn`/`ClockOut`, and `POST /timeentry/{timeEntryId}/segment` adds one. Sending both `Action` and `Segments` is rejected with `TIMEENTRY_ADMIN_SHOULD_NOT_SEND_TAP_ACTION`. Sending neither `Action` nor `Segments` as an Admin is rejected with `TIMEENTRY_ADMIN_REQUIRES_SEGMENTS`.

**Idempotent Member no-ops:**

If a Member retries `Action: Open` when an open segment already exists, the use case returns the current entry unchanged (no-op, not a conflict). If a Member retries `Action: Close` when no open segment exists, the use case also returns the current entry unchanged. These no-ops exist so that mobile clients can safely retry on network errors.

**Status-transition rule:**

The PUT payload may change `Status`. If the current TimeEntry already has active segments and the incoming `Status` ≠ `Present`, the use case rejects with `TIMEENTRY_STATUS_CHANGE_REQUIRES_SEGMENT_CLEANUP`. Admins must deactivate all segments before switching a TimeEntry to a non-Present status.

**Day-bounds rule:**

For the Member (Action) shape, an `Open` tap requires the server-stamped `branchLocalNow ∈ [entryDate, entryDate + 1 day)` so the new segment's `ClockIn` satisfies §3.16a. A `Close` tap requires `branchLocalNow > openSegment.ClockIn` and `branchLocalNow - openSegment.ClockIn <= 24h`; this allows a valid overnight close on the prior-day row while still enforcing the segment span cap. For the Admin (Segments) shape, each supplied segment must satisfy the day-bounds invariant documented in §3.16a.

**Calculate rules (segment list):**

```
Input: segments, Status, entryDate, branchLocalNow
Constants: DailyTarget + LunchDeduction rules from the TimeEntryPolicy row applicable
           to entryDate (§3.20; greatest EffectiveFrom <= entryDate)

If Status ≠ Present:
    Segments must be empty (enforced by TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS).
    TotalHours and BalanceHours use the non-Present rules below.

If Status = Present:
    grossHours = sum of closed segment durations
                 + live contribution of any open segment (if date == branchLocalToday)
    # Closed segment duration: (segment.ClockOut − segment.ClockIn).TotalHours
    # Live contribution: (branchLocalNow − openSegment.ClockIn).TotalHours
    # DateTime subtraction is exact; no midnight wrap-around needed.

    total_gap = sum of gaps between consecutive segment pairs where the preceding segment
                is closed, including the gap from the last closed segment to an open trailing
                segment if present.
    # gap between seg[i] and seg[i+1] = seg[i+1].ClockIn − seg[i].ClockOut (when seg[i].ClockOut != null)
    # A trailing open segment (the last one, ClockOut = null) counts as
    # "following a closed segment" only if there is a closed segment before it.

    effective_lunch = max(0, lunch_tier(grossHours) − total_gap)
    # lunch_tier: grossHours > 6 → LunchDeductionOver6H (strictly > 6)
    #             grossHours > 4 → LunchDeductionOver4H (strictly > 4, ≤ 6)
    #             otherwise → 0
    # Boundary: exactly 4 h gross → 0; exactly 6 h gross → LunchDeductionOver4H.

    TotalHours  = grossHours − effective_lunch
    BalanceHours = TotalHours − DailyTarget

If Status ∈ {Sunday, Holiday, Vacation, JustifiedAbsence}:  (abonado)
    TotalHours = DailyTarget
    BalanceHours = 0

If Status ∈ {DayOff, UnjustifiedAbsence}:  (hours owed)
    TotalHours = 0
    BalanceHours = -DailyTarget
```

**Live-running (open segment on current day):**

When any active segment has `ClockOut = null` and `entryDate.Date == branchLocalNow.Date`, the entry is "in progress". The contribution of that open segment is `(branchLocalNow − segment.ClockIn).TotalHours`. Persisted `TotalHours`/`BalanceHours` are a last-write checkpoint; read endpoints recompute on every call so the API is the live source of truth.

When a segment has `ClockOut = null` and `entryDate.Date < branchLocalNow.Date` (forgotten clock-out), the open segment contributes 0 h (needs manual review). The entry is still marked `IsInProgress = true` in the response.

**Overnight vs forgotten-close disambiguation:**

A prior-day open segment is ambiguous in isolation: it can mean the worker is still on shift (overnight) or that they forgot to clock out. The Member tap protocol resolves this by the next submitted `Action` and `Date`; the server never reinterprets the submitted date or scans other dates to infer intent.

- **Overnight recorded correctly:** the worker taps `Action: Close` with `Date = <prior day>`. The server closes the open segment on that TimeEntry and stamps `ClockOut = branchLocalNow`. The segment span rule (`ClockOut - ClockIn <= 24h`) enforces the cap. See Example 4 and Example 8.
- **Forgotten close:** the worker taps `Action: Open` with `Date = <today>`. The server creates or updates today's TimeEntry with a fresh open segment. The prior-day open is untouched, contributes 0 h while it remains open, and stays visible as `IsInProgress = true` for manual review. See Example 6.

The mobile client routes `Close` to the prior-day row when an active prior-day open exists and the user is ending that overnight shift. Admin recovery for misclassified cases uses the Admin `Segments` snapshot in `PUT /timeentry` or the granular segment endpoints.

**Worked examples:**

```
Example 1 — 10-min gap (no lunch top-up needed):
  seg[0]: 08:00 → 12:00  (4 h gross)
  seg[1]: 12:10 → 17:00  (4h50 gross)
  grossHours = 8h50 ≈ 8.833 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 10 min = 0.167 h
  effective_lunch = max(0, 1.00 − 0.167) = 0.833 h
  TotalHours = 8.833 − 0.833 = 8.00 h

Example 2 — 30-min gap:
  seg[0]: 08:00 → 12:00  (4 h)
  seg[1]: 12:30 → 17:00  (4h30)
  grossHours = 8.50 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 0.50 h
  effective_lunch = max(0, 1.00 − 0.50) = 0.50 h
  TotalHours = 8.50 − 0.50 = 8.00 h

Example 3 — 1-h gap exactly meeting tier (no deduction):
  seg[0]: 08:00 → 12:00  (4 h)
  seg[1]: 13:00 → 17:00  (4 h)
  grossHours = 8.00 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 1.00 h
  effective_lunch = max(0, 1.00 − 1.00) = 0
  TotalHours = 8.00 h

Example 4 — overnight single segment:
  seg[0]: 2026-05-12T22:00 → 2026-05-13T06:00  (8 h)
  grossHours = 8 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 0
  effective_lunch = max(0, 1.00 − 0) = 1.00 h
  TotalHours = 7.00 h
  # DateTime subtraction requires no wrap-around; overnight is unambiguous.

Example 5 — multi-segment overnight:
  seg[0]: 2026-05-12T22:00 → 2026-05-13T02:00  (4 h)
  seg[1]: 2026-05-13T02:30 → 2026-05-13T06:00  (3h30)
  grossHours = 7.50 h  →  tier = LunchDeductionOver6H (1 h)
  total_gap = 30 min = 0.50 h
  effective_lunch = max(0, 1.00 − 0.50) = 0.50 h
  TotalHours = 7.50 − 0.50 = 7.00 h

Example 6 — forgotten open segment with closed sibling:
  seg[0]: 08:00 → 12:00  (closed, 4 h)
  seg[1]: 13:00 → null   (open, forgotten on a prior day)
  Live contribution of open segment on a prior day = 0
  total_gap = gap between seg[0] and seg[1] = 1 h (seg[1] has a preceding closed segment)
  grossHours = 4 h  →  tier = 0 (≤ 4 h)
  effective_lunch = 0
  TotalHours = 4 h  (needs manual review; entry marked IsInProgress = true)

Example 7 — live-running with gap before open segment:
  Current branchLocalNow = 14:00
  seg[0]: 08:00 → 12:00  (closed, 4 h)
  seg[1]: 13:00 → null   (open, same day)
  live contribution = 14:00 − 13:00 = 1 h
  grossHours = 4 + 1 = 5 h  →  tier = LunchDeductionOver4H (0.25 h)
  total_gap = 1 h (gap before the open segment)
  effective_lunch = max(0, 0.25 − 1.00) = 0
  TotalHours = 5 h

Example 8 — overnight via Member tap routing:
  Mon 22:00: Member sends Action = Open, Date = Monday
    → Monday TimeEntry gets seg[0].ClockIn = Mon 22:00, ClockOut = null

  Tue 08:00: Member sends Action = Close, Date = Monday
    → Server closes Monday seg[0].ClockOut = Tue 08:00
    → Duration = 10 h, allowed because span ≤ 24 h

  Tue 08:01: Member sends Action = Open, Date = Tuesday
    → Server creates Tuesday TimeEntry with a fresh open segment
    → Monday remains a completed overnight entry; Tuesday is a separate current-day entry
```

**List triage contract.** `GET /timeentry` accepts nullable `IsInProgress` and `InProgressFirst` filters in `RequestListTimeEntriesJson`. `IsInProgress = true` means the active entry has an active segment whose `ClockOut` is null; `false` means it does not. The predicate is translated into the repository query before count or paging. When `InProgressFirst = true`, that same database predicate orders open entries first before the existing deterministic date/id order and before `Skip`/`Take`, so forgotten punches are prioritized across the whole result set rather than only inside the current page.

### 6.8 Credit card due date calculation

Business day aware: skips weekends and branch holidays.

```
baseDueDate = TransactionDate + 2 business days
// Business day = not Saturday, not Sunday, not in branch holiday set
// DueDateCalculator.Compute(TwoBusinessDays, date, null, branchHolidayDates)
```

Debit cards use the same logic with +1 business day instead of +2.

The branch holiday set is supplied at call time via `IBranchHolidaySource.GetHolidayDatesAsync`. Existing M3 transaction callers pass an empty set (no holiday skipping) until Phase 5 wires the real source.

### 6.9 Branch consistency

All entities referenced by a Transaction must belong to the same Branch. This is a service-layer validation that runs on every Transaction create/update:

- `Transaction.BranchId` must equal `Account.BranchId`
- `Transaction.BranchId` must equal `RecordedByOperator.BranchId`
- `Transaction.BranchId` must equal `Client.BranchId` (when ClientId is present)
- `Transaction.BranchId` must equal `TransactionType.Category.BranchId` (follow the chain: TransactionType → Category → BranchId)

The same principle applies to DailyClose: `DailyClose.BranchId` must equal `Account.BranchId`, and any non-null `RecordedByOperator` / `SubmittedByOperator` must belong to that branch.

This prevents cross-branch data leakage in a multi-tenant system. In EF Core, implement as a shared validation method called by all relevant use cases, not as a DB trigger (keeps the logic testable and explicit).

**Server-authoritative branch clock.** `GET /branch/current` returns `ResponseGetCurrentBranchSummaryJson` with `BranchLocalDate: DateOnly` and `BranchLocalDateTime: DateTime`. The use case captures `IBranchClock.UtcNow()` once and derives both values from that same instant in the selected branch time zone; the date is exactly the date component of the local time. The JSON wire contract is exact: `BranchLocalDate` is `YYYY-MM-DD` (OpenAPI `string/date`), while `BranchLocalDateTime` is an offset-free branch-wall-clock `YYYY-MM-DDTHH:mm:ss[.fffffff]` value (OpenAPI `string/date-time`) with neither `Z` nor a numeric offset. Clients treat the former as a calendar date and the latter as branch-local display context—not as a UTC instant—and never derive the branch business date from a device clock. The account/operator self-context remains focused on assignments and does not duplicate the clock fields.

### 6.10 Member transaction read scope

Members read transactions through the same scope used by `MemberAccountScopeGuard`: a Member sees a transaction if and only if the transaction's `AccountId` is in the Member's set of active linked operator accounts. Three response shapes follow from this contract:

- **GET `/transaction/{id}`:**
  - Member without a linked operator → 403 with `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`.
  - Member linked to an operator but the transaction's account is not in their linked-account set → 403 with `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`.
  - Missing or cross-branch transaction id → 404 with `TRANSACTION_NOT_FOUND` regardless of role. 404 is reserved for "not in your branch" so attackers cannot probe transaction existence outside their branch via the 403/404 split.

- **GET `/transaction` (list):**
  - A Member sees **every** row on a linked account, including rows recorded by another operator on the same account. List visibility is account-scoped, not recording-operator-scoped, so a shared terminal account exposes both operators' rows to both members linked to it.
  - A Member-supplied `OperatorId` filter is ignored server-side because Member list scope is linked-account based. To filter to the authenticated operator's own rows, clients use `Mine = true`.
  - `Mine = true` is a convenience filter: the server resolves the caller's linked operator and sets the repository `OperatorId` filter to that operator. It never expands or changes `AllowedAccountIds`, and combining `Mine = true` with an explicit `OperatorId` returns 400.
  - When a Member has no linked operator OR has a linked operator with zero active `OperatorAccount` rows AND does not supply an explicit `AccountId`, the use case short-circuits to `Items = []`, `TotalCount = 0` without calling the repository's list/count methods.
  - When a Member supplies an explicit `AccountId` outside their linked set, the same empty short-circuit applies.
  - `OriginTransactionId` is an optional server-side filter. It enumerates every sibling plan row sharing that origin only after the branch and Member account predicates have been applied; it never widens `AllowedAccountIds`. The no-operator, zero-account, and explicit out-of-scope empty short-circuits remain unchanged.
  - `TransactionTypeId` is an optional server-side filter (owner decision 2026-10-02, shipped in M7.7 Phase 7.6.3). It is combined with the other filters in the same repository predicate that feeds both the page and `TotalCount`, so paging metadata always describes the type-filtered set, and it applies only after the branch and Member account predicates; it never widens `AllowedAccountIds`. Any type id is accepted without an existence check: a retired type, or a type a future phase generates automatically and that cannot be created manually, still filters its historical rows, while an unknown or another branch's type id returns an empty page. The no-operator, zero-account, and explicit out-of-scope empty short-circuits remain unchanged. Filtering grants no mutation access.

The list response carries paging metadata (`TotalPages`, `HasNext`, `HasPrevious`), joined names (`AccountName`, `ClientName`, `TransactionTypeName`), the nullable plan-group `OriginTransactionId`, and the row's positive `xmin` `Version`. A client can identify plan membership and enumerate `GET /transaction?OriginTransactionId={groupId}`, then send one guarded cancel per selected sibling using each returned `Version` as `If-Match`; no per-row detail GET is required.

### 6.11 Transaction mutation contract

**Optimistic-concurrency wire contract.** `DailyClose`, `Transaction`, and `Setting` responses expose their PostgreSQL `xmin` as a positive `uint Version` and successful versioned reads/writes emit the same value as a strong HTTP `ETag`. The only accepted precondition representation is one quoted decimal token, for example `If-Match: "123"`; weak tags (`W/`), unquoted values, zero, wildcard, or comma-separated tags are invalid. A missing precondition returns 400 `CONCURRENCY_IF_MATCH_REQUIRED`; malformed input returns 400 `CONCURRENCY_IF_MATCH_INVALID`. Stale aggregate roots return 409 using the exact family key: `TRANSACTION_STALE_WRITE`, `DAILYCLOSE_STALE_WRITE`, or `SETTING_STALE_WRITE`. EF's `DbUpdateConcurrencyException` translation uses the same keys as the explicit pre-write comparison.

An `xmin` ETag is a short-lived UI concurrency token, not a durable business revision id. PostgreSQL transaction-id wraparound maintenance and tuple freezing can make a long-held token stale; clients must reload after the resulting 409 and must not persist ETags for later sessions.

The guarded commands are transaction update/finalize/cancel, the installment plan finalize, DailyClose item-save, setting update, and month lock. The plan finalize is the one multi-row guarded command: it carries every Draft installment's version in its body (§6.3), because one `If-Match` names only one row. Aggregate commands compare the root row: Transaction for finalize, DailyClose for `/items`, Setting for `lock-month`. A success returns the new `Version` plus matching `ETag`; clients must reload and reconcile after a stale conflict rather than resubmit blindly. Financial create replay is separately safe for blind retry through §6.1 idempotency.

Transaction update is intentionally narrow. The client sends the editable subset only, and the server preserves the financial identity of the row. Draft finalization is a pure state transition: the client sends no request body, and the server promotes a `Draft` transaction to `Active`. Cancellation is a terminal state transition: the client sends a required `CancellationReason`, and the server moves a `Draft` or `Active` row to `Cancelled` with an explicit cancellation audit trail. All three operations share the same member account scope, mutation permission matrix, lock-date behavior, and generic update audit convention.

**Fiado pairs (M7.7 Phase 5.6/5.7).** A generated Terminal leg refuses update, finalize, cancel and edit preview on its own (409 `TRANSACTION_GENERATED_ROW_READ_ONLY`). Finalizing or cancelling its Tab source applies the same transition to the leg in one commit, after the source's own checks plus the leg's terminal ledger guard (`EnsureLedgerIsMutable`) and, for a Member, the terminal scope; a frozen terminal day blocks both. Updating a source never touches its leg (no editable field changes §6.12 inputs); its response, like `GET`, still names the leg in `GeneratedTransactionId`.

`PUT /transaction/{transactionId}`, `POST /transaction/{transactionId}/finalize`, and `POST /transaction/{transactionId}/cancel` require the current Transaction `If-Match`; all three return `ResponseTransactionJson.Version` and the matching new `ETag`. A stale finalization returns 409 `TRANSACTION_STALE_WRITE` and leaves the newer Draft values untouched. A Draft installment of a cheque plan is never finalized here: 409 `TRANSACTION_PLAN_FINALIZE_REQUIRED` points to the atomic plan finalize (§6.3).

**DailyClose ledger binding.** A Terminal `POST /transaction` or `/transaction/installment` requires an active same-day DailyClose. Missing close returns 409 `TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE`; Draft is writable; Submitted/Approved/Rejected returns 409 `TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN`. Tab and Bank are exempt only from the missing-close prerequisite: if an active non-Draft close does exist for their exact account/day, the same freeze applies. Finalize, the plan finalize and cancel remain null-tolerant for legacy/existing rows with no close, but reject any active non-Draft close on the account/day. These mutation families share §6.6 account coordination with the close workflow.

**Editable fields:**

- `Description`
- `DueDate`
- `PaidAt`
- `ClientId`
- `TransactionTime`

**Non-editable fields after creation:**

- `Date`
- `Value`
- `AccountId`
- `TransactionTypeId`
- `CategoryId`
- `Direction`
- `RecordedByOperatorId`
- `CreatedByUserId`
- `BranchId`
- `OriginTransactionId`
- `CreatedAt`
- `Status` through the update endpoint
- cancellation fields (`CancelledAt`, `CancelledByUserId`, `CancellationReason`)

`UpdatedAt` and `UpdatedByUserId` are server-stamped on every successful update. `UpdatedAt` uses the current UTC instant; `UpdatedByUserId` is the authenticated branch user's `UserId`.

The same audit convention applies to successful finalization: `POST /transaction/{transactionId}/finalize` sets `Status = Active`, stamps `UpdatedAt` with the current UTC instant, stamps `UpdatedByUserId` with the authenticated branch user's `UserId`, and returns `ResponseTransactionJson`.

The same audit convention also applies to successful cancellation: `POST /transaction/{transactionId}/cancel` accepts a required `CancellationReason` (max length 500), sets `Status = Cancelled`, and stamps the cancellation audit fields (`CancelledAt`, `CancelledByUserId`, `CancellationReason`) plus the generic update audit fields (`UpdatedAt`, `UpdatedByUserId`) from the same clock instant. The branch clock is captured once per request so the same-day permission check, the cancellation audit timestamp, and the generic update timestamp cannot drift apart under concurrent clock ticks. The endpoint returns `ResponseTransactionJson`.

**Entity-relative validation:**

- `DueDate` must be on or after the transaction `Date`.
- `PaidAt`, when present, must be on or after the transaction `Date`.
- A transaction type that requires fiado context must keep a valid `ClientId`.
- A replacement `ClientId`, when present, must resolve to an active client in the authenticated branch.

**Permission matrix for update, finalize, and cancel:**

- `Admin` and `Manager` can update branch-visible `Draft` or `Active` transactions, finalize branch-visible `Draft` transactions, and cancel branch-visible `Draft` or `Active` transactions, subject to validation where applicable and lock date.
- `Member` must have a linked active operator.
- `Member` must have an active `OperatorAccount` link to the transaction's account. A linked Member with zero active account links is denied by account scope before mutation-specific rules.
- `Member` must be the transaction's `RecordedByOperator`.
- `Member` may update, finalize, or cancel only on the same local business day as the transaction `Date`. MVP local business day uses `America/Sao_Paulo`; branch-level time zones can replace this later.

**Failure contract:**

- Missing or cross-branch transaction id → 404 `TRANSACTION_NOT_FOUND`.
- Update called for an already-cancelled transaction → 409 `TRANSACTION_CANNOT_UPDATE_CANCELLED`.
- Finalize called for any non-`Draft` transaction, including already `Active` or `Cancelled` → 409 `TRANSACTION_CANNOT_FINALIZE_NON_DRAFT`.
- Cancel called for an already-cancelled transaction → 409 `TRANSACTION_ALREADY_CANCELLED`.
- Cancel called with a missing `CancellationReason` → 400 `TRANSACTION_CANCELLATION_REASON_EMPTY`.
- Cancel called with a `CancellationReason` longer than 500 characters → 400 `TRANSACTION_CANCELLATION_REASON_MAX_LENGTH`.
- Transaction date at or before branch `LockDate` → 409 `TRANSACTION_DATE_LOCKED`.
- Member with no linked operator → 403 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`.
- Linked Member without account scope → 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`.
- Linked Member who is not the recording operator → 403 `TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR`.
- Linked recording Member outside the local business day → 403 `TRANSACTION_UPDATE_REQUIRES_SAME_DAY`.

**Installment sibling isolation:**

Cancelling an installment row never touches its siblings. Each installment is loaded by its own id and only that single row is mutated; rows that share an `OriginTransactionId` retain their `Status`, their generic update audit fields (`UpdatedAt`, `UpdatedByUserId`), and their cancellation audit fields. Reactivating the cancelled installment is intentionally not part of this contract — cancellation is terminal. Cancelled installment rows are also excluded from the active-sum query (`SumActiveValueByAccountAndDate`) used by Milestone 4 cash-variance calculations because the query filters on `Status == Active`.

**Why per-row.** A cheque-installment plan is N independent post-dated cheques (§6.3), not one atomic obligation. Cheques bounce or get renegotiated individually, and siblings may already have settled (`PaidAt` set) and been counted in a closed `DailyClose` snapshot. When the manager wants to cancel the whole plan (for example, the client returns the next day and pays the outstanding balance in cash), the frontend is responsible for offering a "cancel siblings too?" confirmation when the targeted row has active siblings and, if confirmed, issuing one `POST /transaction/{transactionId}/cancel` per sibling. The backend never cascades automatically. Each sibling cancel runs the full permission/lock/scope chain on its own and carries its own `CancellationReason`, so a sibling that legitimately cannot be cancelled (for example, already past a `LockDate` or in a different branch-local business day for a Member) is rejected without affecting the others.

The read/list rules in §6.10 and the mutation rules here intentionally share the same linked-account scope but expose different response shapes: `GET /transaction/{id}` uses 403/404, list uses empty result sets for empty or out-of-scope account filters, and update/finalize/cancel use 403 because they are attempted mutations.

### 6.12 CashVariance calculation

CashVariance (Diferença de Caixa) is **system-calculated, not operator-entered**. The operator enters closing product values (Dinheiro, Telesena, etc.); the system computes the variance and persists it as a DailyCloseItem.

```
CashVariance = TotalClosing - TotalOpening - TotalTransactions

Where:
  TotalClosing  = sum of today's DailyCloseItems (excluding CashVariance itself)
  TotalOpening  = sum of the most recent prior active counted close's items
                  for the same account where Date < today and ItemsFirstRecordedAt != null,
                  excluding CashVariance
  TotalTransactions = SumActive(Direction.In) - SumActive(Direction.Out)
                      for today's transactions for the same account where
                      Transaction.Active = true and Status = Active
```

The calculator reads `Direction.In` and `Direction.Out` with two explicit calls to `SumActiveValueByAccountAndDateAsNoTracking(branchId, accountId, date, direction)`, then subtracts `In - Out`. It never relies on an unfiltered transaction sum for CashVariance. Both calls require `Transaction.Active && Status == Active`.

**Direction semantics and exactly once.** On a Terminal, `In`/`Out` records value entering/leaving counted custody (cash plus stock). Counted closing assets and ledger-explained external movement serve different sides of the conservation equation. An ordinary cash sale is **count-only, confirmed by product Q5 on 2026-10-23 at equal purchase/sale value**: stock −V and cash +V, no ledger row, variance zero. The internally consistent alternative remains characterized: a product `Out` V with stock −V and proceeds excluded from the count also yields zero, but is not the adopted workflow. Counting the swap and recording an `Out` V double-explains the sale and gives +V. Product-like names are not renamed until questions 5/6 confirm the events.

**Opening-side inflows — shipped representation, updated evidence.** Tarifa Bolão is confirmed as today's service fee (Q3); Operador is interpreted as the CAIXA report expectation, with exact content, printed label and sign still pending Q9. The following positive example is characterization, not proof of report composition; proposed generated signed fields and the second fee are in §6.19 O08. Record each actual amount through the existing transaction command as an Active `In` on that Terminal/date, never as a product count; the real drawer cash is still counted. No rate or daily amount is calculated by the server. With physical opening 2,000, today's fee 120, yesterday's fee 100 already included in opening cash, and ordinary outflows 40, an exact physical closing is 2,080. The old representation additionally counted today's fee and carried yesterday's fee: `2,200 − 2,100 − (0 − 40) = +140`. The explicitly modeled Access opening-side equation is `2,080 + 40 − (2,000 + 120) = 0`; the corrected inputs give `2,080 − 2,000 − (120 − 40) = 0`. Operador 350 independently gives old +350; together old +490; representing both as In gives zero. This characterizes correctly represented consecutive days, not automatic repair of old close history.

**Evidence limit.** The inspected Access `DSum` filtered account/date but ignored sign. Its export contains positive, misclassified and test-like rows. Algebraic equivalence above assumes ordinary legacy rows are outflows and excludes the known fiado-in-totals difference; it does not establish shop practice or global historical parity. The M10 dry run must show mismatches. Example with no opening-side inflows: opening 5,800, Out 3,000, counted 2,780 → `2,780 − 5,800 − (0 − 3,000) = −20`.

**Product evidence supersedes working answers.** See §9 for Q1–Q10 and exact consumers. Paid cheque means deposited; Depósito Dinheiro moves drawer→Safe; PIX is customer tender; current bank recording is one-sided. Bill/game fiado and per-person shared/separate custody require proposed M9.6 changes (§6.19–§6.23); they are not implemented by these notes.

**Deferred model extension (M9.5 3.4).** `VarianceEffect`, optional ProductId attribution and variance-ledger filtering are DEFERRED. The unchanged formula needs none for canonical count-only sales and linked fiado legs. Revisit only if Q5 contradicts count-only; formal decision-gate acceptance remains pending. No new formula is adopted.

**Fiado and counted assets (M7.7 Phase 5.6).** The fiado-in-totals divergence described above is resolved by **linked Terminal movements** (§6.4), not by adding the fiado delta to the variance. A generated Terminal row is an ordinary `Status = Active` transaction of its terminal: it always enters `TotalTransactions` and is never excluded from the close, its transaction list, or the Daily ledger. With opening assets 1,000 and a R$ 5 counted-stock fiado sale, the correct count of 995 gives `995 − 1,000 − (0 − 5) = 0`; with a R$ 5 counted-cash repayment, `1,005 − 1,000 − 5 = 0`; a non-cash repayment writes no Terminal row and changes no count. Without the generated leg the same counts leave −5 / +5 — the characterized M9.5 1.4 divergence. Only a further mismatch in the actual count remains as variance. The Tab balance is never also added — that would explain the same value twice. Ordinary non-fiado cash sales use equal-value count-only confirmed by Q5; the alternative remains characterized, not adopted.

The CashVariance DailyCloseItem is written by the system when the operator submits the daily close, and updated in place if the close is rejected and resubmitted or recalled/reopened and submitted again. The operator cannot directly type a variance value. Review, ordinary non-Draft close responses, manager queue, cash-variance summary, and monthly reconciliation use this persisted official value. Live recomputation is divergence/hypothetical data only.

If a real counted-input or first-eligibility change affects a later opening, account coordination selects only the earliest later active counted close (`Date ASC, CreatedAt ASC, Id ASC`). A Submitted, Approved, or Rejected successor moves to Draft with `OpeningRecheckRequiredAt`, triggering-close, and triggering-user audit; a Rejected reason is retained. Draft/none is a no-op. The successor's persisted variance is not changed in place and is suppressed while Draft; re-submit recalculates it and clears the recheck lineage. No successor-wide cascade occurs unless that demoted node's own counts are later changed.

The pre-submit preview uses the same calculator with an alternate current-closing source: the request's candidate `{ ProductId, Value }` rows replace the saved Draft-item read for `TotalClosing`; prior-close and active-transaction reads are identical to Submit. Thus saving those exact candidates and submitting produces the same variance, while unsaved changes immediately change the preview.

### 6.13 DailyClose contract

**M9.5 shipped operational boundary (updated provenance §9).** Count physical assets, with no fresh fee/Operador count slot; enter actual fee/Operador as Terminal transactions before submission. Their ordinary Active/Draft, scope, idempotency, version and lock rules are unchanged. Retained historical fee values remain readable and still affect the opening chain; save/preview reject retired IDs, and omitting one from save would deactivate its item. This is gap AA, not a supported recovery path. Submit uses saved items including retired values; a period lock still blocks it. Q8 adds no mandatory fiado product selector. Q10 requires future session identity (P2); until then the current role/recorder/submitter/navigation contract is unchanged.


**Workflow states.** The state machine is `Draft -> Submitted -> Approved | Rejected`. `Rejected` may be directly resubmitted; its first permitted correction save applies `Rejected -> Draft` while retaining the reason. Explicit Recall applies `Submitted -> Draft`; Manager/Admin Reopen applies `Approved -> Draft`. A predecessor correction may apply a distinct opening-recheck demotion from Submitted/Approved/Rejected to Draft. None of these transitions silently recomputes the official snapshot.

**Role x state x local-day matrix.**

| Operation  | Draft                                                                                                                                                | Submitted                                                                                                  | Approved                    | Rejected                                                                 |
|------------|------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------|-----------------------------|--------------------------------------------------------------------------|
| Open       | Terminal only: scoped Member for branch-local today; Manager/Admin today or pre-lock past; every role rejects future                                 | n/a                                                                                                        | n/a                         | n/a                                                                      |
| Edit items | Unclaimed: any account-scoped Member pre-lock may first-count; claimed: recording Member same local day for a plain Draft or until lock for opening recheck; Manager/Admin pre-lock | Never; returns `DAILYCLOSE_NOT_EDITABLE`                                                                   | Never                       | Recording operator until lock or Manager/Admin; first correction → Draft |
| Submit     | Same ownership as the applicable Draft correction lineage; requires count, no same-day Draft rows, and no intervening uncounted active-activity date | Not submittable                                                                                            | Not submittable             | Direct resubmit permitted under Rejected correction ownership            |
| Recall     | n/a                                                                                                                                                  | Recording Member same local day; Manager/Admin pre-lock. Member-window denial is 403; state/lock is 409     | n/a                         | n/a                                                                      |
| Reopen     | n/a                                                                                                                                                  | n/a                                                                                                        | Manager/Admin pre-lock only | n/a                                                                      |
| Approve    | n/a                                                                                                                                                  | Manager/Admin only (controller filter plus workflow defence)                                               | Not approvable              | Not approvable                                                           |
| Reject     | n/a                                                                                                                                                  | Manager/Admin only (controller filter plus workflow defence)                                               | Not rejectable              | Not rejectable                                                           |

All local-day decisions use `IBranchClock.IsSameLocalDay` / `LocalBusinessDate`, never `DateTime.UtcNow.Date`.

**Account scope.** Get uses `404` for missing/cross-branch ids, `403 TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` for Members without a linked operator, and `403 TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE` when a linked Member targets an account outside `AllowedAccountIds`. List uses the same server-resolved `AllowedAccountIds`; empty scope or explicit out-of-scope account filters return an empty page without leaking row existence. Writes use `MemberAccountScopeGuard` and surface the same two `403` keys.

**Start ordering (v59).** `POST /session/start` replays an exact Idempotency-Key first, then takes the cashbox coordination, resolves the current proposal under it and starts custody, opening, optional discrepancy and events in one transaction (§6.20). A second active custody is refused by the server and by the filtered unique index `IX_DailyCloses_BranchId_AccountId_ActiveCustody` (`23505` → 409 `SESSION_ACTIVE_CUSTODY_CONFLICT`); overlapping intervals by the interval trigger.

**Identity, audit stamping, and correction ownership.** Open stamps only immutable `OpenedByUserId`. The first successful item save stamps `ItemsFirstRecordedAt` plus immutable `RecordedByUserId` / nullable `RecordedByOperatorId`; this recorder identity owns Member correction windows. Submit stamps the current `SubmittedByUserId` / nullable `SubmittedByOperatorId` and never changes the recorder. Every workflow mutation stamps `UpdatedAt` and `UpdatedByUserId` from one captured branch-clock instant. Recall/Reopen and opening-recheck demotion clear current submission/approval identity and timestamps while retaining first-count eligibility and recorder identity; opening recheck stamps its three dedicated fields. A direct prior-day Manager/Admin Recall/Reopen creates a Manager-owned plain Draft by permission lineage, not by overwriting the recorder or synthesizing Member correction access.

**Close review events (M7.7 Phase 7.6.1, gap X).** Close review history is durable and immutable (§3.21) instead of latest-only fields.
- **Rejection.** `POST /session/{id}/reject` records a `Rejected` event in the same transaction: the rejection's own time, the reviewer and their linked operator (if any), and the reason. It survives later demotions, recalls and correction saves, which overwrite the generic `UpdatedAt`/`UpdatedByUserId`; those fields never stand in for a rejection time.
- **Opening change.** When a `PUT /items` changes counted input (a real value change, or a first count), the one next eligible successor — the same close the invalidation rule selects — receives an `OpeningChanged` event, **including when it is already Draft and therefore not demoted again**. The event names the corrected source close and the version the count was applied to, the previous opening source (the same close for a correction; the earlier counted close, or none, for a first count that becomes the new source), the successor's status before and after, and one item per product whose opening value changed, with before and after values. Before-values are the opening as it stood inside that same transaction — the source's items before the update, or the previous source's items — never derived later from current mutable items; a product with no saved item is null, distinct from a recorded zero. Repeated corrections are separate events. A save that changes no counted input records nothing.
- **Unchanged rules.** The one-successor invalidation rule, the `AffectedSuccessor` response (still reported only for a real demotion), recorder/submitter ownership, locks, account coordination and official variance snapshots are unchanged; events add no demotion and recompute nothing. Event and command commit or roll back together.
- **Read.** `GET /session/{sessionId}/history` (any branch role, the same permission as `GET /session/{id}`: cross-branch or missing → 404; Member without operator link or with the account out of scope → 403) returns `ResponseDailyCloseHistoryJson { DailyCloseId, IsHistoryComplete, CurrentOpeningSource? { DailyCloseId, Date }, Events }`. Events are ordered `OccurredAt, Id`; each carries `Type`, `OccurredAt`, the actor's user id and display name and optional operator id and name (snapshots, readable by every caller who may read the close, Members included), the rejection reason or the source/previous-source ids and dates, the affected status before/after, and `Items { ProductId, ProductName, BeforeValue?, AfterValue?, Change }` where `Change = (After ?? 0) − (Before ?? 0)` is the signed effect on the opening total. `CurrentOpeningSource` is the close the opening comes from now. The read declares no `ETag` (it returns no versioned close) and never commits.
- **Legacy data.** A close that existed before events shipped has `IsHistoryComplete = false`; its earlier rejections and opening changes were never captured, so clients show them as unavailable rather than inferring times, actors or values. Full audit history is out of scope.

**Lock-date behavior.** `LockDateGuard` applies to every transition listed in §6.6. DailyClose callers pass `DAILYCLOSE_LOCK_DATE_VIOLATION`.

**Sibling-account isolation.** Submit computes and persists CashVariance for exactly one `(BranchId, AccountId, Date)` close. It reads transactions and prior close rows for that account only; sibling accounts never contribute to the variance.

**System-only product.** The `"Diferença Caixa"` product is resolved by display name and is owned by Submit. It is never accepted in client `PUT /items` payloads (`DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN`), never deleted on correction/Recall/Reopen/opening recheck, and is updated in place on resubmission. Every ordinary Draft `ResponseDailyCloseJson.Items` omits the retained row; review flags it but returns a null Draft closing value.

**Draft item-save concurrency and Notes.** `ResponseDailyCloseJson` and `ResponseDailyCloseReviewJson` carry `Version`, mapped to PostgreSQL's `xmin` system column. `PUT /session/{id}/items` performs no-track key discovery, account coordination, tracked reload/revalidation, then mutation/commit. This ordering also serializes first claim: after one scoped Member commits the recorder identity, a competing scoped Member reloads a claimed close and receives `DAILYCLOSE_NOT_EDITABLE`. It requires the strong `If-Match` precondition defined in §6.11; a stale value returns 409 `DAILYCLOSE_STALE_WRITE`, and EF's concurrency check guards the post-comparison race. The request body contains only items and Notes. The request's optional `Notes` is max 1000, verbatim, empty-clearing, and frozen at submit.

---

### 6.14 Reporting Surface

**Read-only contract.** All Milestone 7 and Milestone 7.5 reporting endpoints are read-only. None call `Add`, none mutate persisted state, none open a unit of work. The four preview endpoints (`POST /transaction/installment/preview`, `POST /transaction/{id}/edit-preview`, `POST /transaction/preview`, `POST /session/{id}/variance-preview`) are compute-only and never persist; the "preview cannot commit" invariant is enforced by construction (no `IUnitOfWork` dependency) and pinned by WebApi.Test reload / row-count assertions that verify persisted state is unchanged after a 200 response. The architecture-level never-commits scan covers the report namespaces — for report use cases transitively through every Application reader they construct (M11) — the daily-close review context (`UseCases.DailyCloses.Review`), and the candidate variance preview (`UseCases.DailyCloses.VariancePreview`).

**Official variance authority.** Review, manager queue, cash-variance summary, monthly reconciliation, and ordinary non-Draft close responses expose the persisted CashVariance snapshot. A predecessor correction first demotes the one affected successor to Draft under account coordination; every official/Draft projection then suppresses its retained physical variance row. Live recalculation is permitted only as separately labelled divergence/hypothetical data.

**Permission buckets.**

| Bucket                                   | Applies to                                                                                                                                                     | Behavior                                                                                                                                                                                                                                                                                                                                                                          |
|------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Manager/Admin only                       | Daily ledger, fiado balance, fiado aging, open-cheque aging, upcoming settlements, cash-variance summary, monthly reconciliation, monthly financial, manager queue, management overview, transaction edit-impact preview | `[TokenAuthorize(Role.Manager, Role.Admin)]` per-action; Members receive 401/403                                                                                                                                                                                                                                                                                                  |
| Any branch role with operator-self scope | Operator transaction summary, time-entry balance summary                                                                                                       | `[TokenAuthenticateBranch]` per-action; Member with no linked operator → empty summary short-circuit; Member targeting another operator → 403 `REPORT_MEMBER_NOT_OWN_OPERATOR`. Time-entry balance additionally has a branch-wide roll-up: a Manager/Admin who names neither `operatorId` nor `mine` gets one summary per operator (see "Time-entry balance summary" subsection). |
| Same as write twin                       | Installment plan preview, create-impact preview, DailyClose variance preview                                                                                    | Member with linked operator + account scope, Manager/Admin elevated; mirrors the matching write/submit scope and status checks                                                                                                                                                                                                                                                      |

**Operator summary automatic share (owner decision 2026-10-07, M7.7 Phase 5.8).** `GET /report/operator-summary` keeps counting generated Terminal legs: they carry the initiating operator, so no server-side exclusion exists and the established totals are unchanged. `ResponseOperatorTransactionSummaryJson` reports how much of them is automatic — `GeneratedTransactionCount`, `GeneratedInValue`, `GeneratedOutValue` overall and `GeneratedCount`, `GeneratedIn`, `GeneratedOut` per `ResponseOperatorCategoryTotalJson` — as a **subset** of `TotalTransactionCount`/`TotalInValue`/`TotalOutValue` (and of each category's `Count`/`TotalIn`/`TotalOut`), never an addition, so a generated cash entrada is not read as a second receipt. Recorded volume is not revenue. Scope (operator, `Mine`, Member accounts) and the Draft/Cancelled exclusion are unchanged.

**Existing-report completeness (M11 Phases 2–3).** Additive whole-set fields, never computed from one page:
- **Cash-variance summary** (`GET /report/cash-variance`): `ResponseCashVarianceSummaryJson` gains `SubmittedCount`, `ApprovedCount` and `RejectedCount` over exactly the official non-Draft rows behind `TotalCount` and the variance totals (they add up to `TotalCount`; a Draft close never contributes a row).
- **Historical drill-down entities (M11 Phase 2.4 — a deliberate widening of three shipped reports).** For Manager/Admin, `GET /report/daily-ledger` resolves `AccountId`, and `GET /report/operator-summary` and `GET /report/timeentry-balance` resolve an explicit `OperatorId`, among **any** account/operator the branch owns — active or deactivated — so a row the overview lists for a deactivated entity can always open its full report. Only a missing or cross-branch id is 404. The responses flag the entity: `ResponseDailyLedgerJson.AccountActive`, `ResponseOperatorTransactionSummaryJson.OperatorActive`, and `ResponseTimeEntryBalanceOperatorJson.OperatorActive` (the branch-wide time-entry roll-up already listed deactivated operators with activity; they are now flagged too). Member scopes, `Mine` (always the caller's own active linked operator) and the Manager/Admin-only daily ledger are unchanged.
- **Open-cheque aging, monthly reconciliation, fiado balance and fiado aging:** see their subsections below.

**Aging buckets.** Aging is computed server-side by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` (static pure function, no DI registration):

| `daysOutstanding = (asOfDate.Date − dueDate.Date).Days` | Bucket       |
|---------------------------------------------------------|--------------|
| `dueDate > asOfDate` (future-due)                       | `Current`    |
| 0 – 30 (inclusive)                                      | `Days0To30`  |
| 31 – 60 (inclusive)                                     | `Days31To60` |
| 61 – 90 (inclusive)                                     | `Days61To90` |
| > 90                                                    | `Days91Plus` |

Boundary examples: day 30 → `Days0To30`; day 31 → `Days31To60`; day 90 → `Days61To90`; day 91 → `Days91Plus`.

**Date-range guardrails.** Every paginated report that accepts a `[DateFrom, DateTo]` window enforces:
- `DateFrom` and `DateTo` are valid (non-default) dates.
- `DateFrom <= DateTo` (inverted range → 400 `REPORT_DATE_RANGE_INVERTED`).
- `(DateTo − DateFrom).TotalDays <= 366` (too-wide span → 400 `REPORT_DATE_RANGE_TOO_WIDE`).

`AsOfDate` parameters are optional; when omitted the use case resolves branch-local today via `IBranchClock.LocalBusinessDate(IBranchClock.UtcNow())`.

**Financial total filters.** All financial aggregates filter on `Status = Active AND Active = true` (entity-base soft-delete). `Draft` and `Cancelled` rows are excluded from sums, balances, and totals. This applies to daily ledger balances, fiado balances, aging rows, cash-variance inputs, and monthly reconciliation totals.

**Aggregation.** Aggregate queries (`SumActiveByX`, `ListOpenReceivables`, `ListVarianceTimeSeries`) live in repositories. Use cases never materialize whole tables into memory and aggregate in C#.

**Cash-variance summary deep-link.** Each `ResponseCashVarianceSummaryItemJson` carries `{ DailyCloseId, Date, AccountId, AccountName, DailyCloseStatus, VarianceValue }`. `DailyCloseId` identifies the close for the row's exact `(Date, AccountId)` within the authenticated branch; it is never resolved by date alone. Sibling accounts closed on the same date therefore keep independent variance values and deep-links.

**Fiado balance report.** `GET /report/fiado/balance?clientId?&asOfDate?` — Manager/Admin. Reads the §6.4 projection. One unpaged row per client whose `Out − In` over active Tab rows dated on or before `AsOfDate` (default branch-local today) is non-zero: `ResponseFiadoBalanceJson { AsOfDate, Items, TotalOutstanding, DebtorCount, TotalDebt, CreditorCount, TotalCredit }`, items `{ ClientId, ClientName, OutstandingTotal }` ordered by client name then id. The balance is branch-wide per client, across every Tab. `TotalOutstanding` is the **signed** sum of the items, so a client in credit (a negative balance) lowers it; it is a net figure, not total debt. **Debt/credit split (M11 Phase 3.1):** over the same unpaged client set, `DebtorCount`/`TotalDebt` count and sum the positive balances and `CreditorCount`/`TotalCredit` the negative ones, with `TotalCredit` a **positive** magnitude; `TotalDebt − TotalCredit = TotalOutstanding`. Client credit never offsets another client's debt in `TotalDebt`.

**Fiado aging report.** `GET /report/fiado/aging` — Manager/Admin. Returns a paginated list of the fiado sales that still hold a positive unpaid remainder after the §6.4 branch/client-wide FIFO netting, with per-row aging metadata.

*Endpoint:* `GET /report/fiado/aging?clientId?&accountId?&asOfDate?&page&pageSize`

*Filter semantics:*
- `clientId` (optional) — narrows to one client's open sales.
- `accountId` (optional) — narrows the **netted** open sales to one Tab; it never re-nets per Tab.
- `asOfDate` (optional) — defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())` when omitted. Netting uses the same input as the balance report: active Tab rows with a client dated on or before `asOfDate`.
- Only `Out` rows with a positive remainder are returned; repayment `In` rows and clients in credit never appear, and `PaidAt` is ignored. `TotalCount` counts the open sales of the whole filtered set; across all pages a debtor's remainders add up to its balance.
- `DueDate > asOfDate` rows are included (they belong to the `Current` bucket).

*Per-row contract (`ResponseFiadoAgingItemJson`):*

| Field             | Description                                                                    |
|-------------------|--------------------------------------------------------------------------------|
| `TransactionId`   | Transaction `Id`                                                               |
| `Date`            | Transaction event date                                                         |
| `DueDate`         | Payment due date                                                               |
| `Value`           | The sale's unpaid remainder after FIFO netting (the original value when untouched) |
| `DaysOutstanding` | `max(0, (asOfDate.Date − dueDate.Date).Days)` — zero for future-due rows       |
| `Bucket`          | `AgingBucket` assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` |
| `ClientId`        | Optional client id                                                             |
| `ClientName`      | Optional client name (joined)                                                  |
| `AccountId`       | Tab account id                                                                 |
| `AccountName`     | Tab account name (joined)                                                      |
| `Description`     | Optional transaction description                                               |

*Envelope (`ResponseFiadoAgingJson`):* `Items`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`, `AsOfDate`, and the **whole-set totals (M11 Phase 3.2)** — computed by one grouped query over the same §6.4 netting projection and filters (client, then account after netting), independent of the page: `OutstandingTotal` (sum of every open sale remainder; for a debtor it equals the balance), `ByBucket` (`ResponseAgingBucketTotalJson { Bucket, Value, RowCount }` for every `AgingBucket` in declared order, zeros included, each remainder bucketed by its due date at `AsOfDate`), and nullable `OldestUnpaidDate` — the earliest sale **`Date`** (not due date) among the open remainders, null when none. Repayments and client credit never appear in any of them.

*Bucket assignment:* `DaysOutstanding` is computed as `max(0, (asOfDate.Date − dueDate.Date).Days)`. The bucket is then assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` per the table in §6.14: future-due → `Current`; 0–30 → `Days0To30`; 31–60 → `Days31To60`; 61–90 → `Days61To90`; > 90 → `Days91Plus`. Boundary: day 30 → `Days0To30`, day 31 → `Days31To60`, day 90 → `Days61To90`, day 91 → `Days91Plus`.

*Ordering:* `DueDate ASC, Date ASC, Id ASC` (deterministic) — the FIFO order itself.

**Open-cheque aging report.** `GET /report/cheques/open-aging` — Manager/Admin. Returns a paginated list of open cheque groups — installment plans grouped by origin transaction and, since M11 Phase 4.2, standalone cheque rows — showing only groups with at least one unpaid row.

*Endpoint:* `GET /report/cheques/open-aging?accountId?&clientId?&asOfDate?&page&pageSize`

*Precondition:* Rows with `Status = Active AND Active = true` participate when they are **installment plan rows** (`OriginTransactionId IS NOT NULL`, any type — the shipped plan definition, unchanged) or **standalone cheques** (`OriginTransactionId IS NULL` and the row's type, active or retired, carries `SettlementInstrument = Cheque`; SQ2 updated by product Q2 / §9: single-cheque support retained; deposited semantics). A group is included only when it has at least one row with `PaidAt IS NULL` ("paid" means deposited, product Q2; no clearing guarantee). A standalone row of a type not classified `Cheque` never appears, whatever its `SettlementRule`. Paging applies to groups.

*Filter semantics:*
- `accountId` (optional) — narrows to groups whose installment rows belong to a specific account.
- `clientId` (optional) — narrows to groups whose installment rows belong to a specific client.
- `asOfDate` (optional) — defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())` when omitted. Used for `DaysOutstanding` and bucket computation only; it does not filter which rows are "open" (that is determined by `PaidAt IS NULL`).

*Per-group contract (`ResponseOpenChequeAgingGroupJson`):*

| Field               | Description                                                                    |
|---------------------|--------------------------------------------------------------------------------|
| `OriginTransactionId` | Shared `OriginTransactionId` for a plan's rows; `null` for a standalone cheque (its single row carries the id — no plan identity is invented) |
| `OutstandingTotal`  | Sum of `Value` for unpaid (`PaidAt IS NULL`) sibling rows                      |
| `OldestOpenDueDate` | Earliest `DueDate` among unpaid sibling rows                                   |
| `OldestOpenBucket`  | `AgingBucket` for `OldestOpenDueDate` computed via `ReportAgingBucketizer`     |
| `OpenRowCount`      | Count of unpaid sibling rows                                                   |
| `TotalRowCount`     | Count of all active sibling rows (paid + unpaid)                               |
| `AccountId`         | Account id (from origin row)                                                   |
| `AccountName`       | Account name (joined from origin row)                                          |
| `ClientId`          | Optional client id (from origin row)                                           |
| `ClientName`        | Optional client name (joined from origin row)                                  |
| `Description`       | Optional description (from origin row)                                         |
| `Rows`              | Unpaid sibling installment rows (see per-row contract below)                   |

*Per-row contract (`ResponseOpenChequeAgingRowJson`):*

| Field             | Description                                                                    |
|-------------------|--------------------------------------------------------------------------------|
| `TransactionId`   | Installment row `Id`                                                           |
| `DueDate`         | Installment payment due date                                                   |
| `Value`           | Installment value                                                              |
| `DaysOutstanding` | `max(0, (asOfDate.Date − dueDate.Date).Days)` — zero for future-due rows       |
| `Bucket`          | `AgingBucket` assigned by `ReportAgingBucketizer.BucketFor(dueDate, asOfDate)` |

*Sibling inclusion:* Within each group the `Rows` list contains only its unpaid rows (`PaidAt IS NULL`), ordered `DueDate, Id`; the page's rows are read in one batched query, never one read per group. `TotalRowCount` covers all active rows of the group regardless of payment status (a standalone cheque: 1). Account, client and description come from the plan's origin row, or from the standalone row itself.

*Envelope (`ResponseOpenChequeAgingJson`):* `Items`, `TotalCount`, `TotalPages`, `HasNext`, `HasPrevious`, `AsOfDate`, and the **whole-filter totals (M11 Phase 2.1)** — one query over every unpaid active installment row of every open plan matching the filters, grouped by due date and independent of paging: `OutstandingTotal`, `OpenRowCount`, and `ByBucket` (`ResponseAgingBucketTotalJson { Bucket, Value, RowCount }`, every bucket in declared order, zeros included, each open row bucketed at `AsOfDate`). `TotalCount` is the open-**group** count — open plans plus standalone cheques (M11 Phase 4.2; for plan-only data it equals the former plan count). Paid, Cancelled and Draft rows never count.

*Ordering:* `OldestOpenDueDate ASC`, then the group key — the plan's `OriginTransactionId`, or the standalone row's own id (both are transaction ids and never collide), so plan-only results keep the shipped order.

**Upcoming settlements report (M11 Phase 5).** `GET /report/upcoming-settlements?DueDateFrom&DueDateTo&AccountId?&Instrument?&Page&PageSize` — Manager/Admin; 200/400/401/403/404. Money due to arrive: **Active** (`Status = Active AND Active = true`), **unpaid** (`PaidAt IS NULL`) rows whose type — active or retired — carries a `SettlementInstrument` other than `None` (§3.10), with `DueDate` in the inclusive window. The window is required, not inverted and at most 366 days (the report guardrails); `Instrument = None` or an undeclared value → 400 `REPORT_SETTLEMENT_INSTRUMENT_INVALID`; an account the branch does not own → 404 `ACCOUNT_NOT_FOUND` (a deactivated branch account is accepted). Draft, cancelled, soft-deleted and paid rows never count. Each installment row is counted once and keeps its `OriginTransactionId`; standalone rows have none. Items are ordered `DueDate, Date, Id`; filters apply before paging. A page beyond the result set — however large, up to `int.MaxValue` — returns an empty page with the whole-filter figures, never an error (open-cheque aging behaves the same).
- *Response* `ResponseUpcomingSettlementsJson { AsOfDate, IsConfigured, DueDateFrom, DueDateTo, TotalCount, TotalValue, ByInstrument, OverdueCount, OverdueValue, Items, TotalPages, HasNext, HasPrevious }`; items `ResponseUpcomingSettlementItemJson { TransactionId, OriginTransactionId?, Date, DueDate, Value, Instrument, TransactionTypeId, TransactionTypeName, AccountId, AccountName, ClientId?, ClientName? }`; `ByInstrument` lists `Cheque`, `CreditCard` and `DebitCard` in declared order with zeros (`ResponseSettlementInstrumentTotalJson { Instrument, Count, Value }`).
- *Totals.* `TotalCount`/`TotalValue`/`ByInstrument` cover every matching row of the window whatever the page. `OverdueCount`/`OverdueValue` are an **independent** scope: rows due before `AsOfDate` (branch-local today) under the same account/instrument filters, whatever the window. A window that starts before today also lists overdue rows, so the two figures are never added. `Value` is the row's value; `Date`, `DueDate`, `PaidAt` and direction keep their meaning — the report never marks a row paid, infers bank reconciliation or moves money.
- *Configuration.* `IsConfigured = false` only when no branch type, active or retired, carries an instrument; the empty result then means "not configured", never "nothing due". Retiring or renaming a classified type keeps its rows reported. Seed identities and updated product provenance: §5, §9 Q1 (cards) / Q2 (cheques).

**Monthly financial report (M11 Phase 6, §7.2 step 4).** `GET /report/monthly-financial/{year}/{month}` — Manager/Admin; 200/400/401/403. Year 2000–2100 (`REPORT_YEAR_OUT_OF_RANGE`), month 1–12 (`REPORT_MONTH_INVALID`), and a month that has not started at the branch → 400 `REPORT_MONTH_IN_FUTURE`. One clock read gives `AsOfDate` and `BranchLocalNow`; a past month covers its calendar month, the current month `[1st, AsOfDate]` with `IsPartialMonth = true`. Figures are shown side by side and **never added together** (no grand total across variance, fee, leftovers and hours). Distinct from monthly reconciliation (lock readiness) and from the CEF borderô comparison, which stays manual. Source definitions SQ3–SQ5 are updated by §9 facts; the following describes the shipped account/day attribution until P2:
- *Official variance:* the persisted `Diferença Caixa` of every Active non-Draft close in the window (Submitted, Approved and Rejected — the cash-variance summary's rows), each close once; `OfficialVarianceTotal`, `OfficialCloseCount`. A Draft (recalled or recheck-demoted) close shows `OfficialVariance = null` and its retained value is never read. The §6.12 formula is unchanged; a first-transition official value that carries an old counted-fee opening (v55) is reported as the retained history it is.
- *Tarifa Bolão (SQ3):* per Terminal/day, `Σ In − Σ Out` of Active rows of `BolaoServiceFee` types on **Terminal** accounts; the month figure is the sum of the daily figures. Product counts are never read; bank-account rows are out of scope. `Operador` is never the fee.
- *Sobras (SQ4):* commercial losses, `Σ Out − Σ In` of Active rows of `CommercialLeftover` types on Terminal accounts, per `(Date, Terminal, recording operator, type)` (`Leftovers`), per type (`LeftoversByType`, so Bolão and Federal stay distinct) and for the month.
- *Attribution (SQ5):* `TerminalDays` lists every Terminal/day with a close or fee rows, ordered by date, account name, account id: close id/status, official variance and the day's fee beside the close's recorder and submitter (user and operator) — never split between the operators who shared the Terminal and never duplicated per operator; a fee day with no close appears without one. `Hours` lists operator/day rows from the shared time-entry calculation (each day's effective policy, one branch-local now; today's open segment is live and provisional, an earlier day's open segment 0 h and still in progress), with `HoursTotal` and `ContainsInProgress`.
- *Configuration:* `IsBolaoServiceFeeConfigured` / `IsCommercialLeftoverConfigured` are true when any branch type, active or retired, carries the role; otherwise the figure (`BolaoServiceFeeTotal`, `CommercialLeftoverTotal`, each day's `BolaoServiceFee`) is `null`, never zero. Markers are read at query time: rename and retirement keep them; reclassification re-labels past and locked months without changing any row, variance or lock (§3.10).

**Management overview (M11 Phase 7).** `GET /report/management-overview?Year&Month` — Manager/Admin (no operator link needed); 200/400/401/403. The data of the read-only **Dashboard**: a concise summary of the latest relevant fact from every manager report, each section carrying the ids and filter values of its full report. The API is never named `dashboard`; the Manager Work Queue (`GET /report/manager-queue`) stays the exception-first home and is unchanged.
- *Time.* One clock read: `GeneratedAtUtc`, `AsOfDate` (branch-local today, never a parameter) and `BranchLocalNow`. `Year`/`Month` are both or neither (`REPORT_YEAR_MONTH_PAIR_REQUIRED`), default to the month of `AsOfDate`, floor 2000-01, and a later month → 400 `REPORT_MONTH_IN_FUTURE`; the envelope returns `Year`, `Month`, `MonthDateFrom`, `MonthDateTo` and `IsPartialMonth` (current month ends at today).
- *Sections* (`ResponseManagementOverview{Section}Json`, each with `Status`, its own window, whole-set totals, `PreviewLimit`, `TotalCount` where it lists, and drill-down ids): `Queue` (Work Queue counts for `AsOfDate`, its rules unchanged), `RecentCloses` (`[AsOfDate − 6, AsOfDate]`, every status, per-status counts, preview 10 in `GET /session` order, official variance or null), `CashVariance` (selected month, the cash-variance summary's figures and the 5 largest absolute variances), `Reconciliation` (selected calendar month: lock context, advisory `LockReady`, blocker counts by type), `Fiado` (as of today: debt and credit split, never offset, FIFO aging totals, oldest unpaid date, 5 largest debtors), `OpenCheques` (open groups incl. standalone cheques, totals, 5 oldest), `UpcomingSettlements` (`[AsOfDate, AsOfDate + 13]`, overdue apart, preview 10), `AccountActivity` (today: every active Terminal plus any account, active or deactivated, with Active or Draft rows today, cap 50; per account `TotalIn`/`TotalOut`/`Net`/`ActiveCount`, Draft rows only in `DraftCount`, `LastActivityDate`, `IsActive`; no cross-account total), `OperatorActivity` (today, operator-summary predicates: active operators plus any operator with rows, cap 50, count DESC; generated share as a subset; not close ownership), `Hours` (selected month: live-now and unresolved-open counts, positive and negative balances summed apart, 5 most behind) and `MonthlyFinancial` (the monthly financial totals).
- *Statuses.* `OverviewSectionStatus`: `Empty` means the source ran and found nothing; `NotConfigured` only for deterministic missing configuration — no branch type carries an instrument (upcoming settlements) or neither monthly marker is set (monthly financial) — and is never shown as zero. A cheque row may appear in both open cheques and upcoming settlements; the sections are never added together.
- *Consistency and failure.* Validation, authentication and role checks fail before any section reads. Every section then runs, one after another on the request's database context, inside **one read-only PostgreSQL transaction at `REPEATABLE READ`** (`IReportReadSnapshot`): a write committed by another connection between two sections is invisible to the later ones; the transaction is rolled back on exit and refuses writes; the use case has no `IUnitOfWork`. Any runtime failure or cancellation fails the whole request — there is no partial 200. Each section uses the same predicates as its full report (the manager queue, monthly reconciliation, upcoming settlements, monthly financial and time-entry computations were extracted into shared readers and repository base queries), with no HTTP self-calls, page-by-page totals or per-group loops. Totals are folded and previews limited **in the database**: official variance per status plus the five largest, the fiado debt/credit split plus the five largest debtors over the shared netting projection, monthly fee/leftover totals and close counts, the reconciliation's lock evaluation without its per-day detail, and the account and operator lists counted and capped at 50 with sums read only for the listed page. The one exception is hours: the time-entry calculation runs in the application under each day's policy and one branch-local now, so the selected month's roll-up is computed whole once and shared with the monthly financial section. Every query inside the snapshot receives the request's cancellation token. Parity with each full report is pinned on quiescent fixtures.

**Edit impact preview.** `POST /transaction/{id}/edit-preview` — Manager/Admin. The safety net for sensitive edits: it answers "if I save this change, what moves?" by computing receivable / fiado-balance / cash-variance deltas without persisting anything. Monthly lock-readiness is intentionally out of scope here — it belongs to the Milestone 10 monthly-reconciliation report.

*Endpoint:* `POST /transaction/{transactionId:guid}/edit-preview?asOfDate?`

*Payload:* reuses `RequestUpdateTransactionJson` from §6.11 **verbatim** — same editable subset (`Description`, `DueDate`, `PaidAt`, `ClientId`, `TransactionTime`), no parallel preview DTO, no field made optional, no "at least one change" rule. `asOfDate?` is supplied on the query string (`?asOfDate=YYYY-MM-DD`), not in the body; when omitted it defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())`.

*Validation parity:* the use case mirrors `PUT /transaction/{id}` step-for-step — Manager/Admin role check → `UpdateTransactionFluentValidation` → no-tracking snapshot load (including `TransactionType` and `Account`) → 404 `TRANSACTION_NOT_FOUND` on miss/cross-branch → 409 `TRANSACTION_CANNOT_UPDATE_CANCELLED` → shared mutation permission guard (caller operator is always null; preview is Manager/Admin-only) → `LockDateGuard` (409 `TRANSACTION_DATE_LOCKED`) → 409 `TRANSACTION_FIADO_REQUIRES_CLIENT` when the type requires a Tab client and none is supplied → 404 `CLIENT_NOT_FOUND` for a cross-branch new client → the relative validator (400 `TRANSACTION_DUE_DATE_BEFORE_DATE` / `TRANSACTION_PAID_AT_BEFORE_DATE`) — but **minus** `IUnitOfWork` and the mutation/Commit step. The loaded snapshot is never mutated.

*Response (`ResponseEditTransactionPreviewJson`):* `{ TransactionId, Impact, Warnings }`. `Warnings` is currently always empty. `Impact` (`ResponseTransactionImpactJson` — the neutral envelope shared with the create-impact preview) always carries all three sections; each is "empty" when not relevant:

| Section              | DTO                                                                                                                                                                                   | Contents                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
|----------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ReceivableImpact`   | `ResponseReceivableImpactJson { AgingBucket? BucketBefore, AgingBucket? BucketAfter, bool RowAppearsInOpenReceivables, bool RowDisappearsFromOpenReceivables }`                       | **Tab rows (M7.7 Phase 5.1):** `PaidAt` never settles fiado, so a Tab `Out` reports only its `DueDate` bucket move (`BucketBefore`/`BucketAfter`, flags false) and a Tab `In` reports nothing; whether a sale still holds a remainder is the aging report's FIFO answer. **Other rows:** both `PaidAt` null → `BucketBefore`/`BucketAfter` from the current/hypothetical `DueDate` via `ReportAgingBucketizer`, flags false. Unpaid → paid → `BucketBefore` set, `BucketAfter` null, `RowDisappearsFromOpenReceivables = true`. Paid → unpaid → `BucketBefore` null, `BucketAfter` set, `RowAppearsInOpenReceivables = true`. Both paid → all null/false.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `FiadoBalanceImpact` | `ResponseFiadoBalanceImpactJson { Deltas: ResponseClientBalanceDeltaJson { ClientId, ClientName, OutstandingDelta }[] }`                                                              | Deltas only when `AccountType = Tab` **and** the client changes. `signedValue = Direction.Out ? +Value : −Value` (§6.4); the old client gets `−signedValue`, the new client `+signedValue`. A null side contributes no delta. Otherwise `Deltas = []`.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| `CashVarianceImpact` | `ResponseCashVarianceImpactJson { Guid? AccountId, DateTime? Date, DailyCloseStatus? DailyCloseStatus, decimal? CurrentVariance, decimal? ProjectedVariance, decimal VarianceDelta }` | `AccountId`/`Date`/`DailyCloseStatus` are populated whenever a daily close exists for `(branch, account, date)` (all null when no close exists). `CurrentVariance` is the day's real §6.12 variance, **live-recomputed via `ICashVarianceCalculator`** whenever the operator has committed a complete count set — populated for **any non-`Draft` close** (`Submitted`/`Approved`/`Rejected` all retain their last-submitted counts), and null for a `Draft` close (counts still being entered) or an absent close. The `DailyCloseStatus` field tells the manager which kind of number it is — pending (`Submitted`), signed-off (`Approved`), or repudiated (`Rejected`) — so they can judge the consequence of editing under it (the edit itself is gated by the lock date, not the close status). `VarianceDelta` is always the computed net-flow delta (`beforeNetFlow − afterNetFlow`, `NetFlow = In ? +Value : −Value`); it is 0 under today's edit contract — §6.12 is blind to every editable field and `Value`/`Direction`/`Date`/`AccountId` are immutable — but it is computed, never hardcoded. `ProjectedVariance = CurrentVariance + VarianceDelta`, set only when `CurrentVariance` is non-null. **Single-close boundary:** exactly one close is modeled because `AccountId`/`Date` are immutable; if they ever become editable the section must become a list of cash-variance impacts (old + new close). |

*Preview-never-commits invariant:* the use case takes no `IUnitOfWork` dependency, so it cannot persist by construction. This is pinned two ways — the Phase 12 architecture test scans the constructor for any `IUnitOfWork` parameter, and the WebApi tests reload the row after a 200 response and assert it is byte-for-byte unchanged. A determinism test additionally commits the same payload through `PUT /transaction/{id}` and compares every impact against the resulting state: `ReceivableImpact.BucketAfter` must match the row's actual `Bucket` in `GET /report/fiado/aging`; `FiadoBalanceImpact.Deltas` must match before/after client totals in `GET /report/fiado/balance`; `CashVarianceImpact.ProjectedVariance` must match a real post-write `ICashVarianceCalculator` recompute for the touched close.

**Create impact preview.** `POST /transaction/preview` — branch-authenticated, **same scope as the `POST /transaction` write twin** (not Manager/Admin-only). It answers "if I record this new transaction, what moves?" by computing the receivable / fiado-balance / cash-variance impact of a would-be row without persisting anything. This is the genuinely non-zero counterpart to the edit-impact preview: an *update* cannot change `Value`/`Direction`/`Date`/`Account`, so its cash-variance delta is structurally zero, whereas a *create* sets all of those fresh and therefore produces a real forecast — the reason `VarianceDelta`/`ProjectedVariance` live on the shared `ResponseCashVarianceImpactJson`.

*Endpoint:* `POST /transaction/preview?asOfDate?`

*Permission:* `[TokenAuthenticateBranch]`, **not** Manager/Admin-only. The use case runs through `TransactionCreatePreamble`, so Members inherit the same linked-operator + account-scope enforcement as the real create flow — preview/write parity: anyone who can create the row can preview that same create. A Member targeting an out-of-scope account gets 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`; a Member with no linked operator gets the create flow's 400 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK` (the `RecordedByOperator` resolver runs before the account-scope guard, so this is an `OnValidationException`, not a 403). This is the §6.14 "same scope as the write twin" rule that installment preview already follows; the edit-impact preview remains the deliberate Manager/Admin exception because it previews an edit to an existing persisted row loaded by id, not a scope-limited create. For Member callers every impact query uses only the preamble-resolved `(account, client, date)` — never a branch-wide balance, branch-wide receivable, or all-account variance summary.

*Payload:* reuses `RequestCreateTransactionJson` from §6.1 **verbatim** — same fields, no parallel preview DTO. `asOfDate?` is supplied on the query string (`?asOfDate=YYYY-MM-DD`), not in the body; when omitted it defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())`.

*Validation parity:* the use case mirrors `POST /transaction` step-for-step — `CreateTransactionFluentValidation` → `TransactionCreatePreamble.Resolve` (type/category/account/client resolution, Member linked-operator + account-scope checks, cross-branch 404s for type/account/client, `TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT` 409, `TRANSACTION_DATE_LOCKED` 409, due-date computation) — but **minus** `IUnitOfWork`/`ITransactionsRepository` and the `Add`/`Commit` step. Nothing is created.

*Response (`ResponseCreateTransactionPreviewJson`):* `{ Impact, Warnings }` — **no `TransactionId`** (nothing is created). `Warnings` is currently always empty. `Impact` is the shared `ResponseTransactionImpactJson` (same envelope as the edit-impact preview). The would-be row is always unpaid (`RequestCreateTransactionJson` has no `PaidAt`) and its `Status` is `Draft` when `SaveAsDraft`, else `Active`. A **`Draft`** would-be row is invisible to every Active-filtered surface (§6.4 fiado sums, open receivables, §6.12 cash variance), so all three sections short-circuit to empty/zero. For an **`Active`** row:

| Section              | Create rule                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
|----------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `ReceivableImpact`   | A Tab-account **sale** (`Out`) with a client **appears** in fiado aging only when the shared §6.4 netting projection (read through the read-only `IFiadoProjectionRepository`) says it would keep a positive FIFO remainder as of `asOfDate` — existing client credit can absorb it, a sale dated after `asOfDate` is outside that aging, and an existing sale with the same due date and date is treated as older because the new id is not known yet (M7.7 Phase 5.1; a Tab `In` is rejected like its write twin). When it appears: `RowAppearsInOpenReceivables = true`, `BucketBefore = null`, `BucketAfter = ReportAgingBucketizer.BucketFor(DueDate, asOfDate)` (`DueDate` is the preamble's computed due date — the exact value the write twin persists). Empty for a non-Tab (e.g. terminal) row.                                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| `FiadoBalanceImpact` | A Tab row with a client carries a single delta `+signedValue` onto that client, where `signedValue = Direction.Out ? +Value : −Value` (§6.4). Empty for a non-Tab row or a Tab row with no client.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| `CashVarianceImpact` | `VarianceDelta = −NetFlow(Direction, Value)` where `NetFlow = In ? +Value : −Value` — the new row adds to its `(account, Date)` `In − Out`, so the variance moves by the negative of its net flow. **Genuinely non-zero** (contrast the edit preview's structural zero). `AccountId`/`Date`/`DailyCloseStatus` are populated whenever a daily close exists for the create's `(branch, account, Date)`. `CurrentVariance` is live-recomputed via `ICashVarianceCalculator` for any non-`Draft` close (`Submitted`/`Approved`/`Rejected`), null for a `Draft` or absent close; `ProjectedVariance = CurrentVariance + VarianceDelta`, set only when `CurrentVariance` is non-null. Single-close boundary: exactly one close, since the create's `(account, Date)` is fixed. **Counted-stock fiado sale (`HandsOutCountedStock`, M7.7 Phase 5.6):** the preview validates the whole pair exactly as the write does — active pairing, the Member's terminal scope (403), the terminal's open Draft close (409) — and the section describes the **paired terminal's** close, moved by the generated Terminal `Out` (`VarianceDelta = +Value`); the Tab itself has no close. |

*Preview-never-commits invariant:* the use case injects neither `IUnitOfWork` nor `ITransactionsRepository` (its projector reads fiado through the query-only `IFiadoProjectionRepository`), so it cannot persist by construction (pinned at the architecture level by the Phase 12.3 scan, which covers `UseCases/Transactions/CreatePreview/`). The WebApi tests assert the branch transaction count is unchanged across the call, and a determinism test commits the same payload through `POST /transaction` and compares every impact against the resulting state: `ReceivableImpact.BucketAfter` must match the created row's actual `Bucket` in `GET /report/fiado/aging`; `FiadoBalanceImpact.Deltas` must match the selected client's total in `GET /report/fiado/balance`; `CashVarianceImpact.ProjectedVariance` must match a real post-create `ICashVarianceCalculator` recompute for the touched close.

**Installment impact preview.** `POST /transaction/installment/preview` — branch-authenticated, **same scope as the `POST /transaction/installment` write twin** (not Manager/Admin-only). It keeps its Phase 6 row-plan preview and adds downstream impact forecasting for the would-be cheque plan: the open-cheque aging group it would create, the optional aggregated fiado delta, and the cash-variance shift on its `(account, date)`. This is the N-row analogue of the create-impact preview — instead of one hypothetical row, it forecasts the aggregated effect of the whole generated installment plan.

*Endpoint:* `POST /transaction/installment/preview?asOfDate?`

*Permission:* `[TokenAuthenticateBranch]`, **not** Manager/Admin-only. The use case runs through `TransactionCreatePreamble`, so Members inherit the same linked-operator + account-scope enforcement as the real installment create flow — preview/write parity: anyone who can create the plan can preview it. This is the §6.14 "same scope as the write twin" rule. For Member callers every impact query uses only the preamble-resolved `(account, client, date)` — never a branch-wide balance, branch-wide receivable, or all-account variance summary.

*Payload:* reuses `RequestCreateTransactionInstallmentJson` from §6.3 **verbatim** — same fields (manual rows or auto-generation, `SaveAsDraft`, etc.), no parallel preview DTO. `asOfDate?` is supplied on the query string (`?asOfDate=YYYY-MM-DD`), not in the body; when omitted it defaults to branch-local today via `IBranchClock.LocalBusinessDate(UtcNow())`.

*Validation parity:* the use case mirrors `POST /transaction/installment` step-for-step — `CreateTransactionInstallmentFluentValidation` → `TransactionCreatePreamble.Resolve` (Member linked-operator + account-scope checks, cross-branch 404s, lock-date 409, holiday loading) → the `SettlementRule = OperatorEnteredCheque` conflict guard (409 `TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE`) → `InstallmentPlanBuilder` row generation — but **minus** `IUnitOfWork`/`ITransactionsRepository` and the `AddRange`/`Commit` step. Nothing is created.

*Response (`ResponseInstallmentPreviewJson`):* additive and backward-compatible. The Phase 6 fields — `TotalValue`, `InstallmentCount`, and `Rows` (`ResponseInstallmentPreviewRowJson { Index, DueDate, Value, Description }`) — are **byte-for-byte unchanged**. A new `Impact` (`ResponseInstallmentPreviewImpactJson { OpenChequeAgingImpact, FiadoBalanceImpact, CashVarianceImpact }`) always carries all three sections; each is "empty" when not relevant. The `FiadoBalanceImpact` and `CashVarianceImpact` sections reuse the create/edit preview DTOs verbatim; the open-cheque section is a group rollup, so it has its own `ResponseOpenChequeAgingImpactJson` envelope. The plan's `Status` is `Draft` when `SaveAsDraft`, else `Active`. A **`Draft`** plan is invisible to every Active-filtered surface (the open-cheque report, §6.4 fiado sums, §6.12 cash variance), so all three sections short-circuit to empty/zero while the generated `Rows` are still returned. For an **`Active`** plan the projector **aggregates** the generated rows (it does not loop the single-row create projection):

| Section                 | DTO                                                                                                                                                                                                                                                                                                                                            | Installment rule                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
|-------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `OpenChequeAgingImpact` | `ResponseOpenChequeAgingImpactJson { GroupAppearsInOpenCheques, OutstandingTotal, OldestOpenDueDate?, OldestOpenBucket?, OpenRowCount, TotalRowCount, ClientId?, ClientName?, AccountId?, AccountName?, Description?, Rows }`; per-row `ResponseOpenChequeAgingPreviewRowJson { Index, DueDate, Value, DaysOutstanding, Bucket, Description }` | One would-be origin group mirroring `GET /report/cheques/open-aging`: `GroupAppearsInOpenCheques = true`, `OutstandingTotal = Σ row.Value`, `OldestOpenDueDate = min(row.DueDate)`, `OldestOpenBucket = ReportAgingBucketizer.BucketFor(oldest, asOfDate)`, `OpenRowCount = TotalRowCount = rows.Count` (a fresh plan has no paid rows). Per-row `DaysOutstanding = max(0, (asOfDate.Date − DueDate.Date).Days)` and `Bucket` follow the same rule as the report. **No `OriginTransactionId` / row `TransactionId` is exposed** (nothing is created); row identity is positional via `Index`, and the row `Description` is surfaced.                                                                                                                                                                                             |
| `FiadoBalanceImpact`    | `ResponseFiadoBalanceImpactJson { Deltas: ResponseClientBalanceDeltaJson[] }`                                                                                                                                                                                                                                                                  | When `AccountType = Tab` **and** a client is set, exactly one aggregated delta on that client: `OutstandingDelta = Σ(Direction.Out ? +row.Value : −row.Value)` (§6.4). Empty for a non-Tab account or a plan with no client. This closes the parity hole for cheque transaction types that also require a Tab account + client.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| `CashVarianceImpact`    | `ResponseCashVarianceImpactJson { AccountId?, Date?, DailyCloseStatus?, CurrentVariance?, ProjectedVariance?, VarianceDelta }`                                                                                                                                                                                                                 | `VarianceDelta = −Σ NetFlow(Direction, row.Value)` (`NetFlow = In ? +Value : −Value`) on the plan's `(account, Date)`. `AccountId`/`Date`/`DailyCloseStatus` are populated whenever a daily close exists for `(branch, account, Date)`. `CurrentVariance` is live-recomputed via `ICashVarianceCalculator` for any non-`Draft` close (`Submitted`/`Approved`/`Rejected`), null for a `Draft` or absent close; `ProjectedVariance = CurrentVariance + VarianceDelta`, set only when `CurrentVariance` is non-null. `ProjectedVariance` is "the variance if current close counts remain unchanged after these cheque rows are recorded" — later count adjustments can offset it; the preview reports the immediate ledger-side consequence. Single-close boundary: exactly one close, since the plan's `(account, Date)` is fixed. |

*Preview-never-commits invariant:* the use case injects neither `IUnitOfWork` nor `ITransactionsRepository`, so it cannot persist by construction (pinned by the Phase 12.3 scan, which covers `UseCases/Transactions/InstallmentPreview/`). The WebApi tests assert the branch transaction count is unchanged across the call, and determinism tests commit the same payload through `POST /transaction/installment` and compare each impact against the resulting state: the open-cheque group matches the `GET /report/cheques/open-aging` group (`OutstandingTotal`/`OldestOpenDueDate`/`OldestOpenBucket`/`OpenRowCount`/`TotalRowCount` and rows position-for-position); the fiado delta equals the before/after change in the client's `GET /report/fiado/balance` total (against a non-zero starting balance); `CashVarianceImpact.ProjectedVariance` matches a real post-write `ICashVarianceCalculator` recompute; and a committed `Draft` plan does not appear in the open-cheque report.

**Monthly reconciliation report.** `GET /report/monthly-reconciliation/{year}/{month}` — Manager/Admin. This is the single-month diagnostic managers consult before invoking the authoritative whole-interval `POST /setting/lock-month` command after month end. The report does not mutate `Setting.LockDate` and cannot replace the command's transactional recheck.

*Endpoint:* `GET /report/monthly-reconciliation/{year:int}/{month:int}`

*Permission:* `[TokenAuthorize(Role.Manager, Role.Admin)]`. Members receive 401/403 through the standard auth filters and role check.

*Validation:* the request shape is `RequestMonthlyReconciliationJson { Year, Month }`. Year must be in 2000–2100 (`REPORT_YEAR_OUT_OF_RANGE`); Month must be in 1–12 (`REPORT_MONTH_INVALID`). The route carries bare `:int` constraints (no min/max), so range checking is owned by the validator: an out-of-range `year`/`month` reaches the action and returns **400** with the standard `ResponseErrorJson` body — the same shape every other report's 400 uses — rather than a body-less framework 404. A non-integer path segment still fails the `:int` route match (404), which is the universal typed-route behavior, not a documented contract response. (Prior to the v30 close-out the route carried `min/max` constraints and rejected out-of-range values with that framework 404; the audit replaced it so the declared `{200,400,401,403}` contract is fully reachable and testable.)

*Response (`ResponseMonthlyReconciliationJson`):* `{ Year, Month, LockReady, LockDate?, IsMonthLocked, IsMonthElapsed, Days, Blockers }`. **Lock context (M11 Phase 2.3):** `LockDate` is the branch's current boundary, null when no month was ever locked (the `DateTime.MinValue` seed); `IsMonthLocked` is true when the month's last day is on or before `LockDate`; `IsMonthElapsed` is true when the month's last day is before the branch-local business date. `LockReady` and the blockers are unchanged by them. `Days` contains one row for every calendar day in the requested month, including days with no closes and no transactions.

*Per-day contract (`ResponseMonthlyReconciliationDayJson`):*

| Field                       | Description                                                                                                      |
|-----------------------------|------------------------------------------------------------------------------------------------------------------|
| `Date`                      | Calendar date for the row                                                                                        |
| `Closes`                    | Account close rows for that day, ordered by account name                                                         |
| `ActiveTransactionCount`    | Count of active, non-soft-deleted transactions on the day                                                        |
| `DraftTransactionCount`     | Count of draft, non-soft-deleted transactions on the day                                                         |
| `CancelledTransactionCount` | Count of cancelled, non-soft-deleted transactions on the day                                                     |
| `NetVariance`               | Sum of the day's persisted `"Diferença Caixa"` values from non-Draft closes; zero when none is official          |

*Per-close contract (`ResponseMonthlyReconciliationDayCloseJson`):* `{ DailyCloseId, AccountId, AccountName, Status, VarianceValue }`. `VarianceValue` is the persisted official value looked up by `(Date, AccountId)`, not by Date alone, so multiple account closes on the same day keep distinct values. It is `0` while the close is Draft even if a retained physical row exists.

*Transaction counts:* the repository groups non-soft-deleted transactions by `(Date, Status)` for the requested branch/month. Counts intentionally include all three statuses — Active, Draft, and Cancelled — because Draft rows block lock readiness and Canceled-only days still need to be visible in the calendar.

*Lock-ready rule:* the report uses the same `MonthLockReadinessEvaluator` as the command for this one requested month. `LockReady` is true iff every active close is Approved, no grouped transaction-count row is Draft with `Count > 0`, and every direct active Terminal transaction's `(AccountId, Date)` has a corresponding active close. A close in any state already establishes its pair; a paired-Tab fiado row does not establish a Terminal pair. There is no weekday/weekend/holiday/activation-window special case. Cancelled transactions do not block. A completely empty month is ready; direct Terminal activity with no close is not.

*Blockers contract:* `Blockers` is a list of **structured** `ResponseMonthlyReconciliationBlockerJson` items for UI triage — not pre-formatted sentences. The backend returns fields and the client renders (and localizes) the message. Each item is `{ Type, Day, DailyCloseId?, AccountId?, AccountName?, CloseStatus?, DraftTransactionCount? }` where `Type` is the `MonthlyReconciliationBlockerType` enum (`UnapprovedClose` | `DraftTransactions` | `MissingExpectedClose`). For each non-Approved close the report appends an `UnapprovedClose` item carrying `Day`, `DailyCloseId`, `AccountId`, `AccountName`, and `CloseStatus`; for each day with outstanding Draft transactions it appends a `DraftTransactions` item carrying `Day` and `DraftTransactionCount`; and for each direct Terminal activity pair without a close it appends `MissingExpectedClose` carrying `Day`, `AccountId`, and `AccountName`. An empty list means this month has no known readiness blockers. The lock command still rechecks this month plus every preceding unlocked month while holding its exclusive boundary. On a fresh branch that includes active operational history before `Branch.CreatedAt`; a 409 `SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED` names the first blocking `MM/yyyy`, which the manager opens through this same report route.

**Time-entry balance summary.** `GET /report/timeentry-balance?operatorId?&dateFrom&dateTo&mine` — any branch role (`[TokenAuthenticateBranch]`). Operator self-check ("how many hours have I worked? am I behind target?") and Manager/Admin payroll prep. Re-runs `ITimeEntryCalculationService.Calculate` per row (never the persisted checkpoint) against one branch-local "now" captured once for the whole report, so in-progress current-day rows surface live-recomputed hours (§6.7). Constants are resolved per entry date from the §3.20 `TimeEntryPolicy` ledger, so a window spanning a policy change reports each day under the policy that governed it.

*Endpoint:* `GET /report/timeentry-balance?operatorId?&dateFrom&dateTo&mine`

*Resolution after auth* (one endpoint, one always-wrapped response — no polymorphic payload, so OpenAPI/TS codegen stays a single shape):
- **Member** → own linked operator (one element). A Member with no linked operator gets an **empty `Operators` list** (empty-scope short-circuit, not 403/404), regardless of `mine` or `operatorId`. An explicit `operatorId` other than their own → 403 `REPORT_MEMBER_NOT_OWN_OPERATOR`.
- **Manager/Admin + `operatorId`** → that operator (one element), **active or deactivated** (M11 Phase 2.4; `OperatorActive = false` flags a deactivated one); 404 `OPERATOR_NOT_FOUND` only on a missing or cross-branch id.
- **Manager/Admin + `mine`** → own operator (one element); 404 `OPERATOR_NOT_FOUND` when not linked.
- **Manager/Admin + neither `operatorId` nor `mine`** → **branch-wide roll-up**: one element per **active operator** — zero-entry operators included with empty `Days` and zeroed totals/counters, whether or not they carry a login link (`UserId = null` employees are payroll rows too) — plus any operator that has active entries inside the window even after being deactivated (historical payroll rows never vanish). Ordered `OperatorName ASC, OperatorId` (unchanged). *(M7.7 Phase 7.2; before it, zero-entry operators were omitted.)* Labelling an operator's missing days *Sem registro* remains a client-side derivation (requested window minus returned `Days`) — the server publishes no operating-day matrix and fabricates no absence rows. There is no `REPORT_OPERATOR_ID_REQUIRED` 400 cause on this endpoint — the original single-operator slice rejected this case, the Phase 11 addendum turned it into the branch-wide view (the resource string survives only for Phase 9 `operator-summary`).

`mine` and `operatorId` are mutually exclusive → 400 from `TimeEntryBalanceSummaryFluentValidation`; the same validator enforces the standard `[DateFrom, DateTo]` window guardrails.

*Response (`ResponseTimeEntryBalanceSummaryJson`):* `{ DateFrom, DateTo, Operators }` — the window lives on the **wrapper** (one range for all operators). Each element of `Operators` is `ResponseTimeEntryBalanceOperatorJson { OperatorId, OperatorName, OperatorActive, TotalHours, TotalBalanceHours, PresentDays, AbsentDays, OwingDays, AbonadoDays, ContainsInProgress, Days }`; per-day `ResponseTimeEntryBalanceSummaryDayJson { Date, Status, TotalHours, BalanceHours, IsInProgress }`. Single-operator results (Member own / `mine` / targeted `operatorId`) return exactly **one** element; an unlinked Member returns an **empty** list. Clients derive "anything open in the branch" via `Operators.Any(o => o.ContainsInProgress)` — there is no branch-level `ContainsInProgress` field. Day-category counts follow §6.7: `PresentDays` (`Present`), `AbsentDays` (`JustifiedAbsence`/`UnjustifiedAbsence`), `OwingDays` (`DayOff`/`UnjustifiedAbsence`), `AbonadoDays` (`Sunday`/`Holiday`/`Vacation`/`JustifiedAbsence`); `UnjustifiedAbsence` legitimately counts in both Absent and Owing.

**Manager queue aggregation.** `GET /report/manager-queue?date=YYYY-MM-DD` — Manager/Admin (M7.5 Phase 1; renamed from `GET /report/dashboard` in M7.7 Phase 7.8, v44, with no alias). One call backs the Manager Work Queue for a single branch-local date: the backend owns the derived queue facts — pending approvals, per-close variance, not-submitted accounts — instead of clients stitching the daily-close list, cash-variance summary, and account-assignment calls.

*Endpoint:* `GET /report/manager-queue?date=YYYY-MM-DD`

*Permission:* `[TokenAuthorize(Role.Manager, Role.Admin)]`; the use case re-checks the role. Members receive 401/403. Declared status set: `{200, 400, 401, 403}` — there is deliberately no 404: the `"Diferença Caixa"` product lookup treats a missing seeded product as a bootstrap defect (unhandled 500), not a client-visible not-found.

*Validation:* `RequestManagerQueueJson { Date }` — `Date` must be a non-default date → 400 `REPORT_AS_OF_DATE_INVALID`. Any time component is truncated server-side.

*Response (`ResponseManagerQueueJson`):* `{ Date, TotalVariance, MeanVariance, PendingApprovalCount, Closes, NotSubmitted, Backlog, UnlockedDraftTransactionCount }`.

| Field                            | Description                                                                                                                                                               |
|----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Closes`                         | Submitted-or-later close rows for the day. `Draft` closes never appear here — they surface in `NotSubmitted` carrying their close id. Ordered `AccountName ASC, Id ASC`.  |
| `PendingApprovalCount`           | Count of `Closes` rows with `Status = Submitted`. `Approved` and `Rejected` closes are not pending.                                                                       |
| `TotalVariance` / `MeanVariance` | Sum / mean of the day's persisted non-Draft `"Diferença Caixa"` rows. Retained Draft rows do not participate; both values are `0` when none is official.            |
| `NotSubmitted`                   | Expected closers with no submitted-or-later close for the date (see the expected-closer rule below). Ordered `AccountName ASC, AccountId ASC`.                            |
| `Backlog`                        | Pending closes on unlocked days **before** the requested date (see the backlog rule below). Always present; never `null`.                                                 |
| `UnlockedDraftTransactionCount`  | `Draft` transactions on unlocked days **through branch-local today**, whatever date was requested (see the backlog rule below).                                           |

*Per-close contract (`ResponseManagerQueueCloseJson`):* `{ DailyCloseId, AccountId, AccountName, RecordedByUserId?, RecordedByUserName?, RecordedByOperatorId?, RecordedByOperatorName?, SubmittedByUserId?, SubmittedByUserName?, SubmittedByOperatorId?, SubmittedByOperatorName?, Status, SubmittedAt?, ApprovedAt?, VarianceValue? }`. Recorder fields identify who first counted; submitter fields identify only the current submission. `VarianceValue` is the close-day `"Diferença Caixa"` DailyCloseItem value joined by `(Date, AccountId)` — never by date alone, so sibling accounts on the same day keep distinct variances (the same rule as the monthly-reconciliation per-close variance). `null` when no variance row exists for the close.

*Expected-closer rule (M7.5 item 1.2):* active `AccountType.Terminal` accounts with at least one active `OperatorAccount` link to an active `Operator` are expected to close. Bank and Tab accounts are never expected — §6.5 daily closing covers terminal cash counts and §6.4 keeps fiado balances query-time. Each `ResponseManagerQueueNotSubmittedJson` row is `{ AccountId, AccountName, OperatorId, OperatorName, DailyCloseId?, Status? }`; the surfaced operator is the account's primary active link, falling back to the first active link by operator name (`IsPrimary DESC, Operator.Name ASC, OperatorId ASC`). When a `Draft` close exists for the account the row carries its `DailyCloseId` + `Status` so the UI can deep-link into the open draft. A `date` after branch-local today (`IBranchClock.LocalBusinessDate(UtcNow())`) has no expected closers — `NotSubmitted` is empty for future dates.

*Backlog rule (M7.7 Phase 7.5, design gap W):* `Backlog` is `ResponseManagerQueueBacklogJson { SubmittedCount, RejectedCount, DraftCount, OldestPendingDate? }`. It counts the branch's active (`Active = true`) closes with `LockDate < Date <` the requested `date` whose status is `Submitted`, `Rejected`, or `Draft`, one count per status; `Approved` closes never count. `OldestPendingDate` is the earliest `Date` among the counted closes and is `null` when all three counts are zero. The requested day's own closes are not backlog — they are the `Closes` / `NotSubmitted` rows above — and neither is any later day. `UnlockedDraftTransactionCount` counts the branch's non-soft-deleted transactions with `Status = Draft` and `LockDate < Date ≤` branch-local today (`IBranchClock.LocalBusinessDate(UtcNow())`); `Active` and `Cancelled` rows and future-dated Drafts are excluded. **The two upper bounds differ on purpose:** the backlog moves with the requested date, while the Draft count always runs through today, so browsing to an earlier day shrinks the backlog but leaves the Draft count unchanged. Both share the lower bound: `LockDate` is the caller's branch `Setting.LockDate`, read through the same contract as the §6.6 write guards (a day is locked when `Date ≤ LockDate`); a branch with no Setting row is treated as never locked. Each fact is a single repository aggregation (`COUNT … GROUP BY Status` with `MIN(Date)`, and one `COUNT`), never composed from the paginated `GET /session` / `GET /transaction` lists, whose page size and ordering cannot give exact per-status counts or a contractual oldest day. No operating-day calendar is involved: a day appears in the backlog only because a pending close row exists on it, and the selected-day facts keep their meaning.

**Daily-close review context.** `GET /session/{sessionId}/review` — any branch role within the DailyClose read scope (M7.5 Phase 2, expanded by M7.7 Phase 2). Returns the close header plus per-product opening → closing context for the close-day form and the manager approval review, with opening values derived server-side per §6.5 instead of making clients rediscover the prior close and recompute the mapping. A fresh close receives the complete form in this one call.

*Permission:* reuses the existing DailyClose read-scope semantics — missing or cross-branch id → 404 `DAILYCLOSE_NOT_FOUND` (checked before member-scope resolution); Member without a linked operator → 403 `TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK`; Member with the close's account out of scope → 403 `TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE`; Manager/Admin elevated. Declared status set: `{200, 401, 403, 404}` (no request body or query → no 400 path). The prior-close lookup is branch-scoped, so opening values can never leak cross-branch prior-close existence.

*Response (`ResponseDailyCloseReviewJson`):* a dedicated review DTO — the header fields mirror `ResponseDailyCloseJson` (id, PostgreSQL `xmin`-backed `Version`, date, status, account, opener user, immutable recorder user/operator, current submitter user/operator, approval audit, rejection reason, notes, `ItemsFirstRecordedAt`, opening-recheck timestamp/source-close/user ids, and update audit) while `Items` is review-specific. It is intentionally a separate shape: `ResponseDailyCloseJson` is also returned by write operations, where computing review-only opening values would make the shared contract context-dependent.

*Per-item contract (`ResponseDailyCloseReviewItemJson`):* `{ ProductId, ProductName, DisplayOrder, OpeningValue?, ClosingValue?, IsCashVarianceProduct }`. This is a deliberate breaking change from v31's non-nullable `ClosingValue`: rows come from every active product plus retired products with an active saved item on the current close, ordered `DisplayOrder ASC, ProductId ASC`, so active products with no saved item carry a null closing while historical counted values remain reconcilable. The emitted OpenAPI schema pins `closingValue` as nullable.

*Opening-value projection (§6.5):* the opening source is the most recent prior active **counted** close (`ItemsFirstRecordedAt != null`) for the same `(BranchId, AccountId)`, ordered `Date DESC, CreatedAt DESC, Id DESC` — weekends, holidays, missing dates, and never-counted closes are skipped naturally. Prior active items are mapped by `ProductId`; a displayed product with no prior item (or no prior counted close) gets `OpeningValue = 0`. Products that existed only on the prior close are not resurrected; newly-active products appear; a retired product is retained only when the current close has its own active saved value. The `"Diferença Caixa"` product is flagged by the server-resolved reserved product id, its `OpeningValue` is always `null`, and its `ClosingValue` is null whenever the current close is Draft even if the physical row is retained.

**Daily-close variance preview.** `POST /session/{sessionId}/variance-preview` — same branch/account/recording-operator, status, local-day, and lock rules as Submit. Body: `RequestDailyCloseVariancePreviewJson { Items }`; response: `ResponseDailyCloseVariancePreviewJson { CashVariance }`. Candidate items are validated with the item-save rules and drive `TotalClosing` directly; existing Draft items are irrelevant. The use case loads the close and products no-tracking, injects no `IUnitOfWork`, and is included in the architecture never-commits invariant. A WebApi reload assertion pins the close header, `xmin` version, and saved items unchanged after a 200 response; parity tests save and submit the same candidates and compare the persisted variance.

**DailyClose list enrichment (M7.5 Phase 3 gate) — variance deferred.** The paginated `ResponseListDailyCloseItemJson` exposes recorder user/operator and current submitter user/operator separately; its `operatorId` filter selects the immutable recording operator. It intentionally does **not** carry `VarianceValue`. The manager work queue reads inline variance from the manager queue's `Closes` rows and the approval screen reads the flagged cash-variance item from the review context, so no cataloged screen needs variance on the multi-date paginated list. The gate reopens only if a future list/history screen needs inline variance per row.

**M11 surfaces (v56).** The owner decision of 2026-10-16 — a separate read-only manager Dashboard summarizing every manager report with links to each, beside the unchanged exception-first Work Queue — ships as `GET /report/management-overview` (above), with the two missing reads `GET /report/upcoming-settlements` and `GET /report/monthly-financial/{year}/{month}` and the explicit TransactionType classifications of §3.10. Their original working definitions are superseded by the product fact register in §9; only named changed-answer consumers reopen ([M11](../server/docs/milestones.md) Phases 4.2 and 6.1 record which). The obsolete `/report/dashboard` surface never returns, and the generated document carries no `dashboard` path or schema.

### 6.15 Generated API contract

The emitted OpenAPI document is the contract web and mobile generate their clients from. Four guarantees are
systemic — they hold for every endpoint and DTO, are never patched per-DTO, and are pinned by a whole-document
regression snapshot plus assertions that do not depend on the snapshot text.

**Requiredness follows C# nullability.** A `server.Communication` member is emitted in the schema `required` set
when it is a non-nullable value type or a non-nullable reference; `Nullable<T>` and nullable references stay
optional. A non-nullable collection stays required even when it is initialized to `[]`. Nullability is read in
the serialization direction — write-state for request bodies and query inputs, read-state for responses — and
`required` always carries the serialized JSON property name, never the CLR member name. The same rule governs
flattened `[FromQuery]` DTO members and direct query parameters, except where omission has a declared runtime
meaning: a direct parameter with an explicit C# default, an initialized flattened member whose default-constructed
value is not `default(T)`, a non-nullable Boolean (omission binds `false`), and a non-nullable enum whose zero
value is a defined member all stay optional. Required sentinels — dates, ids, invalid-zero enums, and paging
integers with no initializer — stay required, which is why `Page`/`PageSize` are required on the report endpoints
whose DTOs declare no default and optional on the list endpoints whose DTOs do.

**Enums are published and serialized by name.** Every contract enum — including one reached only through a
nullable property — is a reusable `type: string` component whose values are the canonical declared names, and
responses serialize those names. During the transition window request bodies additionally accept declared names
case-insensitively and any integer representable by the enum's backing type; the integer reaches the DTO so the
owning FluentValidation `IsInEnum` rule can reject an undefined member with its own localized message, while an
unknown or ambiguous name fails at the binding boundary. Every model-state failure still returns the standard
`ResponseErrorJson` 400 envelope. An undefined value that reaches a **response** from legacy or out-of-band data
serializes as its numeric backing value rather than throwing after the response has started.

Cross-cutting financial headers stay explicit in the document: `Idempotency-Key` required on supported financial
creates, `If-Match` required on guarded mutations, and `ETag` declared on successful versioned responses (§6.11,
§6.13). Every declared 429 also declares a required integer `Retry-After` (whole seconds, at least 1), the advisory wait
the security limiters send (v57, §6.16).

**Numbers keep their direction (v52).** Request bodies and query parameters accept a JSON number or a numeric
string, so every number reachable from a request is published as `number|string` (or `integer|string`) with a
numeric `pattern`. Responses are written as JSON numbers, so every number reachable from a response is published as
`number` or `integer` only, keeping its nullability and `format` (for example `version` stays `integer`/`uint32`). The
direction is taken from what each schema is actually reachable from in the document. Each response number is then
checked against the MVC serializer itself: starting from the response type ApiExplorer declares for that status, the
check follows nested objects (inline or referenced), collection elements and dictionary values, and keeps the string
alternative wherever the serializer writes a string. That handling follows the serializer's own precedence: a
property attribute, then the declaring type's attribute, then a collection or dictionary type's own attribute, then
the options; elements take their collection's handling, and a nested collection starts again from its own type. A component that both a request and a
response reach is never narrowed in place: it keeps its input form, and responses reference a separate
`{id}Output` representation with the same requiredness, formats, nullability and named enums. A number written by a
custom converter, a response without a declared type, a schema the serializer metadata does not explain, or one
component serialized as two CLR types stops generation instead of being guessed. Path integers with a route `int`
constraint stay plain `integer`, as before.

**Every operation states its own token (v52).** There is no document-wide requirement. The document defines two HTTP
bearer schemes over the same `Authorization: Bearer` header: `IdentityBearer`, the global-scope identity token
issued by register, login and renew-token, and `BranchBearer`, the branch-scope token issued by
`POST /branch/session` for one membership. Each operation's `security` is derived from the token filters MVC actually
runs for it (controller and action scope together), never from a route table:

| Filters on the operation | Published `security` | Runtime |
|---|---|---|
| `[AllowAnonymous]`, no token filter (`/user/login`, `/user/renew-token`, recovery request/reset, capability invitation preview/decline/register, Google challenge/sign-in; §6.16–6.18) | `[]` | no token needed; Google uses its separately verified assertion and state/nonce |
| `[TokenAuthenticate]` (`/branch/my-branches`, `/branch/session`, account profile/name/password/logout and invitation accept; §6.16) | `IdentityBearer` | identity token only; a branch token is 403 |
| `[TokenPlatformOperator]` (`/platform/onboarding` and its read/reissue; §6.17) | `IdentityBearer` | identity token plus fresh stored platform flag; no branch membership or data authority |
| `[TokenAuthenticateBranch]` | `BranchBearer` with no roles | any branch role; an identity token is 403 |
| `[TokenAuthorize(roles…)]` (with or without `[TokenAuthenticateBranch]`) | `BranchBearer` listing the admitted roles | other roles are 403 |

Without a token every protected operation answers 401. Several `[TokenAuthorize]` filters on one operation must all
pass, so their roles intersect. A token filter wrapped in `[ServiceFilter]` counts like its attribute. The token
filters do not consult `[AllowAnonymous]`: an action marked `[AllowAnonymous]` that also carries a token filter still
requires the token. Mixing identity and branch token filters has no single answer — with the branch filter first a
branch token passes, with the identity filter first both tokens are refused. Those declarations, an operation with no
declared auth intent, an empty-role combination, ASP.NET `[Authorize]` metadata, a filter factory whose filters are
only known at runtime and any other authorization filter stop generation instead of being published. The published role list
describes the filter gate only; use-case scope rules — such as a Member's linked-operator and account scope — still
apply after it (§6.14).

---


### 6.16 Account security, operator-link requests and invitations (M8 Phases 1–5)

**Boundary:** Phase 6 subsequently removed legacy public register, identity-only branch create and direct add-existing-member routes; §6.17–6.18 define their replacements. The contracts below remain in force. New auth/account screens remain uncataloged design 0.4.6 work; no UI or real sender acceptance is claimed.

**Canonical email:** runtime trims Unicode whitespace then uses a parameterized lowercase query in the owning PostgreSQL database; migration applies the matching Unicode trim set and database lowercase, preflighting collisions before updates. Exact canonical lookup, lock keys, throttle hashes, invitations and authenticated acceptance share that value, avoiding .NET/database Unicode casing disagreement. The database locale/collation must remain stable.

**Identity/account contract:** login returns `{ UserId, Name, Email, ResponseToken }`, where required `UserId` is the authenticated database identity, never client token decoding. Missing, inactive, wrong-password and passwordless accounts all return the same 401 `PASSWORD_WRONG` resource; login abuse protection follows below (v57). `GET /user/profile` → `ResponseUserProfileJson { UserId, Name, Email, IsPlatformOperator, HasPassword }`; `PUT /user/profile { Name }` → same DTO (trimmed nonempty name, maximum 255). The two capability booleans are read-only; over-posted fields cannot grant authority. Both require identity tokens; email is not editable. `POST /user/change-password { CurrentPassword, NewPassword }` confirms the existing BCrypt password, changes it, invalidates recovery capabilities and revokes **all** active refresh sessions atomically (204, 401 wrong password); a Google-only account without a local password cannot use this current-password flow. `POST /user/logout { RefreshToken }` revokes the matching user's contracted stable refresh session, including a concurrently rotated successor, and is idempotent (204) for unknown/foreign/already-revoked credentials; it does not revoke another independent session. Identity/branch JWTs expire naturally. All account writes including login/invitation-register/renew serialize by normalized email and reread mutable authority after acquisition; failed commits roll back revocation.

**Login abuse protection (v57, M8 Phase 8):** `POST /user/login` declares 400, 401, 409 and 429. A request first passes a **process-local** fixed-window limit per client network (`AccountSecurity:LoginRequestsPerMinute`, default 30, 1–1000, no queue), checked before binding, validation, the database and BCrypt; each API instance counts on its own and a restart starts over. Inside the existing email advisory lock, before the account is read, two **persistent** budgets (§3.29) are checked: per (canonical account, client network) `LoginClientFailureLimit` (default 5, 1–100) and per canonical account `LoginAccountFailureLimit` (default 20, not below the network limit, at most 1000; left unset while the network limit exceeds 20, the default would undercut it, so startup stops), both counting failed checks within `LoginFailureWindowMinutes` (default 15, 1–1440) and blocking for `LoginCooldownMinutes` (default 15, 1–1440) once reached. Invalid values stop startup. Only a completed failed check counts — a wrong password on an active account and the dummy-hash check of a missing, inactive or passwordless account alike, plus a wrong Google first-link proof (409, §6.18); validation 400s, coordination 409s and refusals neither count nor extend a block. The request that reaches a limit still gets the ordinary 401; the next window starts with the first failure after the window or block ends. While either budget is blocked, every attempt — the right password included — is 429 `SECURITY_RATE_LIMITED` with `Retry-After` the longer remaining wait, without reading the user, verifying BCrypt or issuing a token or refresh session. A successful login deletes only that network's budget; the account budget runs out on its own. Successful password reset and change-password delete every persistent budget of the account in their transaction; the process-local request limit is separate and is not reset, so a sign-in right after a reset may still receive 429 for the rest of that network's minute. Renewal, logout, invitation registration and Google sign-in without a password proof never touch the budgets. The **client network** is the connection's peer address — IPv4 (or IPv4-mapped) address, or the IPv6 /64 — after the framework forwarded-headers middleware, which honors `X-Forwarded-For` only when `ForwardedHeaders:KnownProxies`/`KnownNetworks` are configured and then unwraps trusted hops only; with no address one shared partition is used. The forgot-password and capability limiters below use the same partition and limiter. A sustained attack from more networks than the account/network ratio can re-impose the account cooldown, never permanently; reset clears it. Real-sender, cross-instance request limiting and launch acceptance remain Phase 8 gates.

**Recovery:** anonymous `POST /user/forgot-password { Email }` returns empty **202** for every valid existing, missing, inactive, cooldown-throttled, IP-throttled or delivery-failed target. Invalid email shape/length is 400; technical coordination conflict is 409. Normal requests have a 200 ms minimum response duration; SMTP work uses a bounded volatile queue off the response path. Default per-target persisted cooldown is 60 seconds, plus a process-local 60 requests/minute per client network with no queue. The latter's suppressed response is also 202, with no retry header. The reset token is sent using a trusted configured application base URL; request Host, branch, device clock and redirect inputs never choose it. `POST /user/reset-password { Token, NewPassword }` is anonymous capability consumption: 204 on success; 409 `SECURITY_TOKEN_INVALID` for missing/expired/used/inactive-account capabilities, including simultaneous losers; malformed capability/password is 400. Successful reset changes password and revokes **all** refresh sessions/invalidate recovery in one transaction; it does not log in the user. BCrypt passwords retain the existing minimum 8 characters and now reject more than 72 UTF-8 bytes to avoid truncation.

**Operator requests:** Member-only `POST /operator-link-request` creates or returns the existing eligible Pending request (200 `ResponseOperatorLinkRequestJson`). Eligible means no active linked operator **or** no active assignment to an active same-branch Terminal account; an already usable context returns 409 `OPERATOR_REQUEST_NOT_NEEDED`. Member-only `GET /operator-link-request/self` returns `ResponseOwnOperatorLinkRequestJson { Request? }`, with the latest own request or null. Manager/Admin `GET /operator-link-request?Status=&Page=1&PageSize=50` returns `{ Items, TotalCount, Page, PageSize }`; status is optional, page 1–1000000 and size 1–100, filtered before paging, CreatedAt/Id ascending. Manager/Admin `POST /operator-link-request/{id}/resolve` returns 200 with actual `OperatorId`, `HasUsableAccount`, status and resolution UTC time. Missing/foreign/removed/inactive targets are 404 `OPERATOR_REQUEST_NOT_FOUND`; no linked operator is 409 `OPERATOR_REQUEST_SETUP_REQUIRED`. Repeated resolution with a surviving link is idempotent. Use the existing operator create/update then account-assignment contracts; resolution grants **no** account. A subsequent eligible missing-assignment case may create a new request after a previous resolution. `ResponseManagerQueueJson.PendingOperatorLinkRequestCount` counts active eligible pending requests across the branch, independent of the requested date; existing close totals, date bounds and row ordering do not change. The bounded list is the Queue/Operators detail read; Members cannot read managerial data.

**Invitation issuance/list:** Manager/Admin `POST /invitation { Email, Role, OperatorId? }` returns 201 `ResponseInvitationJson { Id, Email, Role, OperatorId?, Status, ExpiresAt, DeliveryStatus }`. Existing role matrix applies: Manager cannot grant Admin; Member cannot invite; only Member invitation can pre-select an operator. Existing active membership/inactive account is 409, duplicate Pending target 409 `INVITATION_PENDING_CONFLICT`, unavailable/foreign/retired/linked pre-link operator 409 `INVITATION_OPERATOR_UNAVAILABLE`. `GET /invitation?Status=&Page=1&PageSize=50` is bounded 1–100 per page (page cap 1000000), filtered before count/paging, CreatedAt descending then Id descending; Managers never see Admin invitations. `POST /invitation/{id}/reissue` accepts optional `RequestReissueInvitationJson { OperatorId? }`: omitted/null body preserves selection; a DTO object replaces selection (null field or `{}` clears it), and a non-null replacement remains Member-only and must be an active unlinked same-branch operator. Email/branch/role cannot change. This recovers stale/retired preselection without waiting for expiry. Shape/role violations are 400. The 200 command rechecks current role, target membership and operator eligibility; foreign/missing or Manager-accessed Admin invitation is 404; accepted invite 409. Declined/expired invites may reissue, unless another pending target owns the slot. Every reissue replaces the old capability.

**Safe capability and acceptance:** anonymous `POST /invitation/preview { Token }` returns only `{ BranchName, Role, ExpiresAt, OperatorName? }`; no target email, user details, token or hash. Anonymous `POST /invitation/decline { Token }` is 204, including repeats of the same declined capability while it remains unexpired; accepted/expired/replaced capability is 409. Identity-only `POST /invitation/accept { Token }` requires the active authenticated account's normalized email to match (403 `INVITATION_EMAIL_MISMATCH` otherwise), then returns `{ BranchId, BranchUserId, Role, OperatorId? }` (200). Anonymous `POST /invitation/register { Token, Name, Password }` derives email/branch/role only from the invitation, refuses **every** existing account including inactive accounts (409), and returns the normal login envelope with required caller UserId (201). It cannot take over an existing password account or accept caller-chosen identity/role/branch fields. Acceptance creates a membership or reactivates its existing unique inactive row under the invited role; an active membership conflicts rather than changing its role. Optional eligible Member pre-link, user creation if needed, membership, refresh authority and invitation consumption occur **all or none** in one PostgreSQL transaction. Another invite or manager acquiring the operator first makes acceptance fail without partial registration/membership. Employee history and account assignments remain intact. Replays/expired/declined tokens fail 409 `SECURITY_TOKEN_INVALID`; malformed input 400. Anonymous reset/preview/decline/register additionally have a process-local 120/minute capability limit per client network (429 standard error with `Retry-After`). Request failures are resource errors in pt-BR; no capability appears in an error/log/profile DTO.

**Technical operation:** PostgreSQL transaction-scoped advisory locks use namespaced hashed keys acquired in ascending key order. Branch coordination covers membership/operator mutations, request creation/resolution and invitation writes, including fresh actor-role checks after waiting. Email coordination covers credentials and invitation account uniqueness. Five-second acquisition timeout is 409 `SECURITY_COORDINATION_BUSY`. The migration preflights normalized-email collisions (named failure, no account chosen), normalizes Users and backfills session IDs, and adds four empty tables/indexes with restricted FKs. No public seed/reset/admin-grant endpoint is added.

**Delivery/trust:** `ISecurityMailSender` uses pinned MailKit SMTP; disabled by default. Plain SMTP capture is allowed only for a literal loopback host, otherwise STARTTLS with certificate validation is required. Configured application URLs require absolute HTTPS (HTTP only loopback), trailing slash, no userinfo/query/fragment; capability is placed in the fragment for future app handoff and consumed in an API body. No arbitrary redirect/Host injection. Send follows successful commit: invitations synchronously record SMTP `Sent`/`Failed`; recovery uses a bounded 100-item memory queue, logs only bounded failure outcomes and still returns 202. No automatic retry, durable plaintext outbox or delivery claim on enqueue/failure. A crash or failed attempt needs a new request after cooldown/reissue; production sender, domain/DNS, real inbox acceptance, retention, operational tuning and full launch readiness remain Phase 8.

### 6.17 Operated onboarding and public-entry cutover (M8 Phase 6)

`User.IsPlatformOperator` defaults false for every migrated account. Only explicit CLI/database administration changes it; profile, registration, invitation and membership request DTOs cannot. An active platform operator presents the ordinary **identity** bearer token to `POST /platform/onboarding` (`RequestOperateBranchJson { RequestId, Name, Cnpj?, Address?, Phone?, OwnerEmail, Terminals[], AdditionalProducts[], Operators[] }`). The backend checks the fresh stored flag; a branch Admin without it receives 403 `PLATFORM_ONLY`, and a branch-scoped token is 403. The response (`ResponseOperatedOnboardingJson`) exposes request/branch ids, branch name, owner invitation id/email/status, SMTP delivery status and expiry, never capability/hash. New creation returns 201; an identical request id, actor and canonical payload returns the same branch with 200; changed payload/actor returns 409 `ONBOARDING_REQUEST_CONFLICT`. Validation limits optional lists to 30 terminals, 30 additional products and 50 operators, checks duplicate names and assignment indexes before writes. `GET /platform/onboarding/{requestId}` returns current owner-invite status, and `POST /platform/onboarding/{requestId}/reissue-owner-invitation` replaces an expired/declined/failed owner capability without cloning the branch (200; accepted invitation is 409). Both require the same platform flag; missing id is 404. SMTP acceptance occurs **after** the branch/invitation transaction; `DeliveryStatus=Failed` means reissue is needed, never a silent delivery success.

One transaction creates the Branch, the exact `CreateBranchSeedFactory` categories/types/products/Setting/TimeEntryPolicy, optional explicit Terminal with paired Tab, additional product and Operator/assignment rows, and a Pending owner **Admin** invitation with a hashed capability. The platform caller receives **no BranchUser row** and therefore cannot list/open that tenant absent a separate membership. The branch may have zero members until its owner accepts. Reissue only replaces the owner invitation capability; no Admin membership is manufactured. CLI `onboard` invokes this same application use case with an explicit platform-user id, environment, database target, JSON request and `--execute` write gate; `inspect` is read-only, `reissue` reuses the platform reissue use case, and `set-platform` is a separate explicit administrative command. CLI-only `--demo-data` requires an `@example.invalid` owner and otherwise empty optional setup, then supplies explicitly fake Terminal/paired Tab/product/operator structure without financial transactions or payment configuration. The CLI refuses Production and database names containing `loto_prod` or `production` for ordinary commands. `bootstrap-platform --name --email` is the sole first-operator exception: it checks that the migrated database has no users (or exactly its own active, still-passwordless platform user on a delivery retry), coordinates that check under one database lock, creates the account and a hashed 256-bit reset capability in one transaction, then sends the trusted configured setup link through SMTP. A failed send is reported, and only that still-passwordless single-account case can reissue a replacement capability; activation via the normal reset endpoint establishes the password and mailbox proof. Dry-run validates input and database eligibility without writing. Development/Staging require `--execute`; Production additionally requires `--allow-production-bootstrap`, configured HTTPS application URL and enabled TLS SMTP, under the later Phase 8 operational runbook. No password/capability is accepted in CLI arguments or printed. Other CLI dry-runs validate payload and fresh platform authority, and all CLI security lifetimes use the same bounded configuration as the API. The CLI's ordinary output contains no raw invitation token. Local/demo fixtures must be explicit and non-production, never hidden seeds. Product facts, account relationships and grants are not inferred from legacy names.

The actual server routes `POST /user/register`, `POST /branch/create` and `POST /branch/users` are **removed** (`404` for the first two; `405` for `POST /branch/users` because its GET sibling remains; no alias or development bypass). Account creation is now by invitation registration or verified Google with a matching invitation (§6.18); all further branch memberships are invitations under the existing Manager/Admin role matrix. `GET /branch/users`, role changes and removal, branch selection and other tenant endpoints remain. Test fixtures may call internal seed writers directly; there is no public seed/reset/admin-grant route.

### 6.18 Verified Google identity and linking (M8 Phase 7)

`POST /user/google/challenge` is anonymous and rate limited; expired persisted challenges are purged on subsequent challenge creation. It returns `{ ChallengeId, State, Nonce, ExpiresAt }` with 256-bit random state/nonce and five-minute server-UTC expiry. Their SHA-256 hashes alone persist (§3.27). The client supplies the returned nonce in its Google ID-token request and returns `ChallengeId`, `State` and the signed ID token in `POST /user/google/sign-in`; optional `InvitationToken`, `Name` (new account) and `CurrentPassword` (first link to a password account) are accepted. The trusted Google library validates signature against Google's rotating certificates, configured client audience, Google issuer and token lifetime; the application additionally requires `email_verified`, nonempty stable `sub` and the signed nonce matching the still-pending challenge. No decoded/unsigned token, caller email, caller-supplied verification flag, alternate issuer or arbitrary certificate URL is accepted. Missing client IDs gives 503 `GOOGLE_NOT_CONFIGURED`; unavailable provider gives 503 `GOOGLE_PROVIDER_UNAVAILABLE`; invalid assertion/challenge gives 401; the per-network capability limit gives 429 with `Retry-After`.

Already-linked issuer/subject signs in the **active local User**, even if Google's current email changed; the stable subject is the identity key. For a first link by matching normalized email, legacy accounts start with `EmailVerifiedAt = null`: they require the **current local password** unless recorded local email proof exists **and** Google is authoritative for that Gmail/Workspace mailbox. That proof is a password check: it spends and obeys the §6.16 login budgets (v57) — a wrong `CurrentPassword` counts, a blocked budget answers 429 before verification, and a refused proof consumes no challenge; asking without a password spends nothing. Invitation acceptance/registration and successful emailed reset record local mailbox proof when it was actually shown, never by blanket migration. A different stored subject for the same local account conflicts, and inactive accounts do not sign in. An unknown Google identity without an invitation cannot create an account or branch. With a valid invitation, the verified Google email must equal its normalized target; a new user has no local password and is created only while the invitation and membership/pre-link are consumed atomically. Existing linked or password users may join a matching invitation under the same membership rules. Challenge consumption, provider link, user/refresh session and optional invitation join commit together under account/branch/subject coordination; replay and failed commits grant nothing. The response is the ordinary `{ UserId, Name, Email, ResponseToken }` identity session. Google never issues a branch token directly; branch membership/removal remains authoritative. Production Google project/client/consent and real provider acceptance remain Phase 8 gates.

### 6.19 Operation matrix — proposed M9.6 1.1 acceptance source

**Status and reading rule.** This is a model candidate, not shipped endpoint acceptance. Owners are server M9.6 P2–P9, M9.7 (C), M9.8 (D), M10 A/B; design 4.9.1 reconciles the catalog now, blueprints follow shipped contracts. §6.12 arithmetic stays unchanged. `C` = total counted physical cash + purchase-cost stock; purchase and sale value are **equal**. `R` = signed CAIXA report expectation, not revenue. Inclusion in R is a premise to verify, not a balancing plug. Rows marked **R?** have unconfirmed report inclusion; examples are conditional and no production command may invent an offset to force parity. “No” below means excluded by this proposed representation, not evidence about every legacy report.

**Rules shared by every row.** A financial value is explained once: R explains the bill/game expectation, a non-cash/fiado Out offsets the unreceived cash; do not also record a cash sale In. Count-only exchanges generate no ledger rows. Explicit = human command; generated = protected legs of its source command, never separately editable/finalizable/cancellable. Current normal Member work needs cashbox grant + active owned custody + Draft review; Tab commands also need explicit Tab grant. Manager historical work selects the exact session, reason and current version; Draft correction never reacquires custody. Noncash Tab-only repayment requires Tab grant and no cashbox/session. Manager/Admin own opening obligations, custody exceptions, attribution and bank/settlement commands; destination Safe grant/scope is explicitly checked (manager-only until P9 defines a narrower grant). Every mutation rechecks branch membership, account activity, lock and all affected sessions under coordination. Missing/foreign resource → 404; denied own-branch scope → 403; stale/state/lock → 409; input → 400. These are proposed categories, not new resource keys.

Draft financial rows affect neither debt nor variance until finalized; finalize/cancel changes every generated leg atomically and checks every affected period/session. Submitted/Approved/Rejected ledgers are frozen until an authorized correction to Draft; a finished/handed-over Draft is editable historically only by authorized correction, never as live custody. Cancellation requires reason and audit, cannot reverse a physical event or erase custody; an actual return/payment is a new event. Locked originals are immutable; future settlement/reversal uses open PostedOn (§6.23). Applies even where a row below says “same protection”. Amounts in the matrix are V > 0; `ΔD` is branch/client debt Out − In; zero debt impact is explicit.

| ID / operation | Physical custody and R inclusion premise | Explicit/generated account and session legs; ΔD | Variance and financial class; evidence / future owner |
|---|---|---|---|
| O01 Stock sold for cash | stock −V, cash +V; exclude stock face value from R in this count-only scenario; **R?** per product | No row; same session count; ΔD=0 | ΔC=0, net=0, variance unchanged. Custody exchange/pass-through; separate service fee only once. Product Q5 confirms equal value; M9.6 P2; R scope pending Q9 |
| O02 Stock sold for card/PIX | stock −V, cash 0; exclude stock principal from R; **R?** | Explicit tender command → cashbox Out V on session + receivable reference (not a second cashbox leg); ΔD=0 | ΔC=−V, net=−V → 0. Pass-through/receivable, not fee income. P2/P6 source rules, C1–C3/C5 settlement |
| O03 Bill/game paid cash | cash +V; R includes V as scenario premise, scope **R?** | Report field's generated In V (O08), no additional principal In; ΔD=0 | ΔC=+V, net=+V → 0; CAIXA/client pass-through. Q6/Q8 and report inference; P7 |
| O04 Bill/game by card/PIX/MarketPlace | no counted change; R includes V, **R?** | Explicit tender Out V on session; report In V via O08; receivable tracked once; ΔD=0 | net=0, C unchanged → 0. Pass-through, not expense. Q6 customer PIX/MarketPlace confirmed; C settlement later; P7 expectation |
| O05 New COUNTER fiado sale | bill/game: no cash, R includes V **R?**; counted stock: stock −V, R excludes V **R?** | Explicit Tab Out V + generated paired cashbox Out V on active session; R generated only for report-covered bill/game; ΔD=+V | Bill/game net=0 / stock net=−V → 0 under respective premise. Client receivable/pass-through. Fix extends shipped counted-stock flag to every new counter sale, P6; no compulsory product selector |
| O06 Cash fiado repayment | cash +V; excluded from R in example (**R?** legacy coverage) | Explicit Tab In V, mandatory CountedCash method + generated cashbox In V on paired active session; ΔD=−V | C +V/net +V → 0; receivable collection, not new revenue. Shipped physical pair; P6 method seeds |
| O07 Noncash fiado repayment | cash/stock unchanged; excluded R (**R?**) | Explicit Tab In V with selected NonCash method; no cashbox leg/session. Bank receivable/matching later, once; ΔD=−V | C/net=0 → 0; receivable collection. Shipped method configuration; P6 seeds Dinheiro/PIX, no default |
| O08 Report and two fees | Report is expectation; physical result supplied by constituent operations. Fees received in cash increase cash, noncash fees need tender offset; **R?** whether principal/report already includes either fee | Three explicit close fields maintain generated session effects: signed R positive In/negative Out; BolaoFee and SpecialFee In nonnegative. ΔD=0 | R=pass-through, fees=own income, not stock. Never add fee already included in R twice: inclusion pending Q9. Signed support is design decision, not proven negative legacy report. P7; SpecialFee inference, not product-team answer |
| O09 Cashbox → Safe cash | cashbox −V, safe +V; exclude R (**R?**) | Explicit transfer → protected session cashbox Out + Safe In, one operation; ΔD=0 | C −V/net −V → 0; internal transfer. Q6 confirms cofre, app improves one-sided recording; P9 |
| O10 Cheque → Safe custody | paper moves, cash/stock unchanged; no R | Explicit custody event referencing receivable, from/to custodians, no extra cashbox Out or debt; ΔD=0 | C/net=0 → 0, receivable remains open. Deliberate C2 improvement; paper not counted a second time |
| O11 Safe → Bank cash/cheque | safe custody −V, pending bank receipt +V; no cashbox/R | Explicit collection + protected Safe Out and pending Bank In/custody event once; cheque links same receivable; ΔD=0 | No session effect; transfer. Matching confirms receipt, never inserts payment; C2/C4/C5 |
| O12 Drawer expense / prize | cash −V; exclude R in example (**R?** prize inclusion) | Explicit cashbox Out V on session; ΔD=0 | C/net −V → 0. Expense = operating expense; prize = CAIXA pass-through reimbursed at bank, not business expense. Q6/Q7; P9 seeds/C4 reimbursement |
| O13 Sobras / discount / rejected slip | expected cash not obtained; R includes V in scenario (**R?**) | Explicit session commercial-loss Out V; report In V once; ΔD=0 | net/C=0 → 0. Sobras confirmed loss, discount/rejected meaning Q6 only; inclusion pending. Current monthly report includes both Sobra roles; Access monthly query only Bolão. P7/C6; no forced equivalence |
| O14 Stock return / prize in product | stock −V, cash 0; excluded R (**R?**) | Explicit cashbox product-event Out V on session; ΔD=0 | C/net −V → 0. Return=stock custody transfer/recovery; prize=pass-through, loss only if documented loss. Q5 deliveries/returns unrecorded in shop; event-specific type meanings incomplete, M9.5 3.2 deferred; P2/C6 |
| O15 Cheque received in place of cash | no cash for bill/game (R includes V **R?**), or cash handed out −V (R excludes V **R?**); pin operation intent | Explicit cheque Out V on session and same receivable identity; R separately via O08 only when covered; ΔD=0 | net=0 or −V follows C → 0. Receivable/pass-through. Single/no-client and plans exist; exact count inference remains bounded; C2 |
| O16 Initial company stock entrusted | stock +V before first session; no R | Dedicated OpeningCount/acknowledgment custody proposal, no sale/purchase row; ΔD=0 | Opening includes V, no initial variance or income. Q5; P3. If allocated into active custody, use O18, never silently modify opening |
| O17 Restock funded by drawer | cash −V, stock +V at equal cost; exclude R (**R?**) | Count-only, no Out purchase row; ΔD=0 | C/net=0 → 0, internal asset exchange, not operating expense. Product Q5; P2 |
| O18 Delivered restock / externally funded stock | stock +V during session, cash 0; exclude R (**R?**) | Explicit stock-received In V on session, source delivery identity; supplier payment/obligation separate, no second session receipt; ΔD=0 | C/net +V → 0; asset/custody transfer, not income. Shop does not record delivery; deliberate control improvement P2, supplier accounting C6 boundary |
| O19 Opening/imported debt or credit | no physical movement, no R | Dedicated opening/import Tab Out debt or In credit (never normal counter command), source provenance, no session; ΔD=±V | No cash variance; opening receivable/liability, not new income. M10 A/B |
| O20 Opening/imported receivable | no physical movement, no R | Dedicated cheque/card opening obligation with due date, client if known; no cashbox Out/session; ΔD=0 | No cash variance; opening asset. M10 A/B; C events later |
| O21 Cheque → fiado | no physical movement/R | Explicit conversion event closes remaining cheque and creates same client's Tab debt atomically; no cashbox leg; ΔD=remaining V | No variance, receivable reclassification, never double outstanding. C2; identified client mandatory |
| O22 Shared cashbox handover | one box, outgoing count C; independently acknowledged incoming opening A; no R | Explicit submit/offer/ack; no financial legs; ΔD=0 | Handover delta A−C kept separately, initially unattributed, not session expense/compensating row. P2; manager attribution reason/audit |

#### Independent worked examples (amounts BRL; future acceptance unless stated)

Each row pins `opening O; closing count C; In I; Out U; result C−O−(I−U); closing client debt D`. These are independently constructed inputs, not private-data replay. R premises from the matrix are explicit; if actual report composition differs, resolve the premise before applying the example. An implementation test must assert account/asset composition, not merely aggregate zero.

| Example / scope | O → C and physical composition | I / U / signed result / D |
|---|---|---|
| E01 O01, current count-only | O=1,500 (cash 1,000 + stock 500); sell stock 80 for cash → C=1,500 (1,080 + 420); R excludes 80 | I=0; U=0; result 0; D=0. Adding Out 80 incorrectly gives +80 |
| E02 O02, proposed journey | Same O, stock card/PIX sale 80, no cash → C=1,420 (1,000+420); R excludes stock principal | I=0; U=80; result 0; D=0; receivable 80 once |
| E03 O03/O04, report premise | O=1,000; bill/game 240 paid cash → C=1,240; R=240. Noncash alternative C=1,000; same R | Cash I=240 U=0 result 0 D=0. Noncash I=240 U=240 result 0 D=0. Does not prove every CAIXA report includes it |
| E04 O05, planned fiado correction | O=1,000; bill/game 240 on fiado, R=240; C=1,000. Alternate stock sale 80, R=0, C=920 | Bill I=240 U=240 result 0 D=240; stock I=0 U=80 result 0 D=80. Tab debt is never additionally added to formula |
| E05 O06/O07, shipped arithmetic/pair | O=1,000; opening debt 240; repay 90 cash → C=1,090; R excludes repayment. PIX alternate C=1,000 | Cash I=90 U=0 result 0 D=150; NonCash I=U=0 result 0 D=150. A further payment 160 gives credit 10 with no oldest unpaid sale (FIFO branch-wide) |
| E06 O16 initial issue | No opening proposal ≠ acknowledged zero. Company entrusts stock 500 plus cash 300 before first start; acknowledged O=800; untouched C=800 | I=U=0 result 0 D=0. OpeningCount itself is no variance-bearing session |
| E07 O17 versus O18 | O=800 (cash 300+stock 500); cash-funded restock 100 → C=800 (200+600). Delivered 100 alternate → C=900 (300+600) | Funded I=U=0 result 0; delivered I=100 U=0 result 0; D=0 in both. No “purchase Out” for initial allocation |
| E08 O09 safe | O=1,000; transfer cash 250 → C=750, Safe +250; R excludes transfer | Session I=0 U=250 result 0 D=0; Safe In 250, transfer total zero; fail second leg → neither |
| E09 O08 signed R + both fees | O=1,000; R=+300; Bolão fee=35; Especial=15, each excluded from R; cash all received C=1,350 | I=350 U=0 result 0 D=0. Hypothetical R=−60 with same fees → C=990; I=50 U=60 result 0. Negative R is design acceptance only, not a claim about printed CAIXA sign |
| E10 O19/O20/O21 | Dedicated opening debt 400 at go-live; O=C=1,000. Independently register an outstanding opening cheque of 120 (its full still-open face value); later convert that whole 120 to fiado for the identified client | Cashbox I=U=0 result 0 throughout; debt D=400, then 520 after conversion; cheque remaining 120→0 in the same commit. No unmentioned partial payment or fresh counter Out |
| E11a O22 matching/mismatch | Outgoing O=1,000 C=1,100 I=100 U=0 result 0 D=0. Incoming acknowledges A=1,100 or A=1,080 | New session O=C=A I=U=0 result 0 D=0; handover delta 0 or −20. Outgoing count remains 1,100. −20 is unattributed, never personal until reviewed |
| E11b standalone opening difference | Finished session retains closing 1,100; next Start/inline acknowledgment 1,080, with no direct handover. Independent first-session alternative: OpeningCount proposal 1,100 and actual acknowledgment 1,080 | New O=C=1,080 I=U=0 result 0 D=0; opening discrepancy −20 Unattributed in both cases. First case has evidenced predecessor; opening-count case has none, so no Outgoing attribution target. Failed first operation commits neither opening nor discrepancy; queue retains successful discrepancy separately, not as a new lock blocker |
| E12 ledger after Conferir | O=1,000; attempt 1 C=1,000 I=U=0 gives 0 at revision 7. New receipt +20 → revision 8 | Old count recomputed against I=20 U=0 gives −20 **unconfirmed**; submit refused. Recount C=1,020 gives 0 in attempt 2. Event 1 remains 0/rev7; D depends on receipt source, use non-debt receipt here D=0 |
| E13a cheque deposit across lock (C2) | September bill/game 200 paid by single cheque under explicit R=200 premise: original O=C=1,000, I=200 U=200, result 0, D=0; original session/sale is now locked. Full cheque deposited OccurredOn=30/09, posted 02/10 in open October | Original count/legs/result unchanged. Deposit settles full 200 under Q2, cheque remainder 0, Bank pending receipt 200 exactly once; matching confirms the same receipt, never creates another payment. No cashbox leg/session effect on deposit; no unsupported partial single-cheque deposit |
| E13b partial card settlement across lock (C3) | Separate September card receivable 200 from bill/game: original O=C=1,000, I=200 U=200, result 0, D=0 under same explicit R premise; sale month locked. Provider-confirmed gross partial payout 120, fees 0, OccurredOn=30/09, PostedOn=02/10 | Cashbox count/legs/result unchanged; Bank pending 120, card receivable remainder 80. Posted cutoff 30/09 still shows 200; operational occurrence cutoff 30/09 shows remaining 80, labelled with its cutoff basis. Bank matching confirms 120 once; it never consumes the remaining 80 |
| E14 O10/O11/O12/O14 | O=1,000; paper to Safe C=1,000 I=U=0 result 0; Safe→bank no session. Separate drawer expense/prize or stock return 40: C=960 | I=0 U=40 result 0 D=0. Expense, prize and return have distinct classes despite same arithmetic |
| E15 O13/O15 | O=1,000; Sobra/rejected/discount 40 included R=40, no cash → C=1,000. Bill paid cheque 40 same premise, same count | I=40 U=40 result 0 D=0; commercial loss vs receivable. Cash-advance cheque alternate R=0 C=960 I=0 U=40 result 0 |

Current arithmetic is evidenced by `CashVarianceCalculator` and `CashVarianceRepresentationCharacterizationTest` (count-only, duplicate +V, missing fiado legs, opening fee 120/100 → old +140, report-only old +350, combined old +490, corrected zero and retained-opening −100). These tests do not exercise proposed sessions, Tarifa Especial, signed generated fields or handover. Each E/O id becomes golden/API acceptance in its owning future phase; hand calculations here are no claim of runtime implementation.

### 6.20 Session lifecycle and command contract (M9.6 P2–P3 shipped; P4–P9 proposed)

#### Independent custody and review states

| Action | Custody before → after | Review before → after and ledger consequence |
|---|---|---|
| Start/ack opening | none → Active | new Draft; explicit acknowledgment required before financial effect; any nonzero proposed-versus-acknowledged difference is recorded against SessionOpening |
| Save/autosave | unchanged | Draft stays Draft; allowed Rejected correction → Draft; invalidates candidate; no count attempt |
| Conferir | unchanged | Draft; immutable attempt created, expected/difference revealed only then |
| Submit | unchanged Active/Finished/HandedOver | Draft/Rejected → Submitted; current attempt, no Draft rows/gaps; freeze exact session |
| Offer handover | Active stays Active, frozen | requires Submitted and current verified closing; incoming is not custodian yet |
| Accept matching/different opening | outgoing Active → HandedOver; incoming none → Active in one commit | outgoing Submitted retained; incoming Draft; difference belongs to SessionOpeningDiscrepancy linked to handover; work continues |
| Finish (no incoming) | Active → Finished | requires Submitted/current count; freeze preserved. Later start acknowledges latest proposal explicitly |
| Reject / recall / reopen | **unchanged**, never reactivates old custody | Submitted → Rejected; permitted Submitted/Approved → Draft; only exact historical session unfreezes for correction |
| Dispute / withdraw offer | unchanged | dispute event does not force acceptance; withdraw before acceptance allows authorized recall/correction; no financial reversal |
| Manager exception | explicit finish/start or accept, never inferred | reason, incoming custodian and observed vector mandatory; no acknowledgment attributed to absent person; mark ManagerException source evidence |

A pending offer keeps custody with the outgoing operator, with no additional financial writes while Submitted. Incoming can accept a different vector and proceed with discrepancy, or dispute and leave custody unchanged. An incoming operator may explicitly accept the current version of a Disputed offer with the independently observed vector; withdrawal permits a new offer. A manager can resolve a stalled/absent operator through the explicit exception; no automatic timeout transfer. An accepted handover cannot be withdrawn: a later correction is a new audited event, not history deletion. Failed start, acknowledgment, count save, handover or first operation rolls back all state, legs, audit and idempotency result. Concurrent starts: one succeeds, loser sees active-session conflict. Concurrent accept/withdraw/finish: serialize on the box and check offer/session versions, one outcome only. A changed outgoing count/revision invalidates any unaccepted offer, requires replacement, never silently refreshes what the incoming person accepted.

**Opening acknowledgment discrepancies, with or without a direct handover.** Every acknowledged opening compares the immutable acknowledged vector with the proposed source snapshot in the same start/accept transaction. A nonzero delta creates one SessionOpeningDiscrepancy, its first immutable revision and OpeningDiscrepancyOpened event, initially Unattributed. SessionHandover is optional evidence, never the required discrepancy key. Finish 1,100 → later Start/inline opening 1,080 therefore creates −20 against the incoming SessionOpening, with the finished session as evidenced predecessor and no fabricated direct handover or outgoing acknowledgment. An opening-count 1,100 → first acknowledgment 1,080 likewise creates −20, but has no outgoing-session person; only Incoming/Neither can be explicitly chosen with reason, or the amount remains Unattributed. Missing source still blocks normal start until explicit source/zero acknowledgment is resolved; absence is not a zero comparison. Matching openings create no nonzero discrepancy. The new session opens with the real 1,080, variance zero before work; no compensation or personal blame is generated.

Queue and review reads include OpeningDiscrepancyId, SessionOpeningId, incoming SessionId, optional HandoverId/predecessor, source/revision, signed comparison, attribution state and allowed targets. The queue's opening-discrepancy group covers handover and standalone starts with the same count and deep-link rules; derive it from persisted records, not local arithmetic. Manager attribution names the discrepancy and exact comparison revision; current personal totals/thresholds include only an explicit current Outgoing/Incoming attribution, never Unattributed or Neither. Standalone openings receive the same correction/superseding-evidence protection as direct handovers. An unattributed discrepancy remains separately visible after session approval and is **not an additional month-lock blocker**; the lock contract still requires the approved session/draft/activity prerequisites. Attribution mutations use the incoming session's business date and, if targeting an outgoing person, also the predecessor's date, each subject to LockDate. Locking preserves unresolved evidence without manufacturing an attribution; after-lock attribution is owned explicitly by server M9.7 P7 items 7.3–7.4 (pending contract/review), covering handover-backed and standalone opening discrepancies through append-only open-PostedOn adjustments. Until those items ship, the current attribution command refuses locked source dates and the discrepancy stays visible and unattributed. The future adjustment affects the posted month's informational personal totals once, preserves locked original totals/cutoffs and introduces no financial ledger leg or payroll deduction; it never rewrites locked history.

**Correction lineage.** Sequence and opening provenance, not date alone, select exactly the next dependent session on the same cashbox. For inherited/unacknowledged proposals, update proposal and flag opening recheck/demote that successor as existing rules require; preserve before/after snapshots, suppress official Draft variance. Once a real incoming acknowledgment exists, its opening vector is immutable: a predecessor correction appends a SessionOpeningDiscrepancy comparison revision (handover-backed or standalone) and recheck evidence, never substitutes a new physical opening. If the original comparison matched and has no discrepancy record, the first later nonzero comparison creates it; if a later revision returns to zero, retain the prior revisions/attributions but project the current comparison as Matched with no personal amount or attribution target. A corrected outgoing 1,100→1,090 with incoming acknowledgment 1,080 changes comparison delta −20→−10 with superseding event; incoming opening stays 1,080. If an earlier discrepancy was attributed, preserve that decision as history but invalidate its current personal projection until the manager explicitly accepts/re-attributes the revised discrepancy version; never silently change the amount blamed on a person. If the dependent successor is locked, reject the originating correction; do not modify locked history. No cascade to later cashboxes or unrelated sessions; a further cascade requires a real change to that successor's own closing input. Incomplete imported chains require resolution, not synthetic acknowledgment.

**Session opening in the §6.12 formula.** The arithmetic is unchanged; its inputs move to the session. `TotalOpening` of a Ready session is the sum of its immutable acknowledged `SessionOpening` vector (A), never the predecessor's closing; `TotalTransactions` sums the Active rows whose `SessionId` is that session; `TotalClosing` is its own count. A migrated session with no acknowledgment keeps the shipped rule — the immediately preceding counted session of the same cashbox, by Sequence — labelled as inherited. Consequently a predecessor correction leaves an acknowledged successor's variance and review status unchanged: it appends only the discrepancy comparison revision above, with no opening-recheck demotion. Demotion, `OpeningChanged` and the one-successor rule of §6.12/§6.13 apply only to an inherited (unacknowledged) successor. Either way a locked dependent successor rejects the originating correction.

#### Open foundation decisions (owner, before P2 runtime)

These are product premises the shipped contract and the 2026-10-23 answers do not settle; the gate records the owner's answer or lists the question instead of choosing:

- **F1 — Custody left Active past branch midnight — decided by the owner 2026-10-29.** `FinishSession` by the custodian still requires Submitted. `ResolveCustodyException` action Finish may also end an unsubmitted (Draft) session: Manager/Admin only, reason mandatory, CustodyExceptionResolved event, `EndedAt` = the command instant. Custody ends; review stays Draft as historical correction work under the existing recorder/manager windows, and the session remains a submit-gap and month-lock blocker until submitted and approved. No count, acknowledgment or variance is fabricated, and the next start acknowledges its own opening from the latest counted source. A Member is never blocked by an unfinished previous day beyond needing that manager action.
- **F2 — Manager-declared past session and past-session rearrangement — decided by the owner 2026-10-29 (declared interval, plus rearrangement).** The retired `POST /dailyclose` let Manager/Admin open a pre-lock past date; a session created *now* for that date would overlap current custody. The owner chose a declared interval and added a rearrangement path:
  - *Declare (`DeclarePastSession`).* Manager/Admin names cashbox, custodian (an operator of the branch, active or not; no Member authority derives from it), a declared `[StartedAt, EndedAt)` wholly in the past and inside one branch-local business day, and a reason. The session is created **Finished** (never active custody), review Draft, with opening evidence `ManagerDeclared`: no acknowledgment is attributed to the custodian, so its opening is inherited (predecessor closing, §6.20 *Session opening*). The recording manager is the recorder.
  - *Rearrange (`RearrangePastSession`).* When a past session already occupies the time or date (including one finished through F1), the manager sees it and may correct its count items (the existing correction path) and move its declared interval/business date, with reason. Only a Finished/HandedOver session in Draft (after recall/reopen as today) can move; active custody never moves, and an operator-acknowledged start time is never rewritten. The session's own cashbox rows move to the new business date in the same transaction, together with any generated pair's source row; DueDate is kept.
  - *Chain.* Sequence follows interval order on the cashbox and is renumbered atomically by these two commands under the cashbox coordination key. Every session whose predecessor changes — at the old and the new position — follows the §6.20 correction rules: an acknowledged successor gets a discrepancy comparison revision, an inherited one the opening-recheck demotion.
  - *Refusals (nothing written).* Overlap with any Ready interval of the cashbox, or a window not in the past or spanning two business days, is rejected by the database (interval trigger and CHECK/trigger on the declared fields); a cashbox row whose date differs from its session's business date is rejected by the database (date-coherence trigger on Transaction/Session). Lock dates at the old date, the new date and every affected successor, branch/role/operator validity and versions are checked by the server under the §6.20 coordination order, as every shipped lock rule is. A locked affected session, a reconciled settlement on a moved row, or an unresolved mapping on the cashbox refuses the whole command.
  - *Audit.* `SessionDeclared` and `SessionRearranged` events keep before/after interval, date, Sequence and predecessor, actor and reason; no history is deleted.

Both F1 and F2 are settled; neither changes the §6.12 arithmetic.

#### Conferir identity, first token and re-confirmation (P4)

Session owns `CurrentConfirmationToken` (server-generated opaque Guid) and a monotonic next AttemptNumber. P4 StartSession initializes the first token; the P4 migration initializes existing editable sessions once before P4 form reads are enabled, without a count event. GetSession/Review only reads that persisted token and never commits. The token is returned by GetSession/Review and successful saves, never chosen by a client. A changed count/opening/ledger revision rotates it atomically with that mutation; an identical save or Notes-only change preserves it. BeginRecount deliberately rotates it even when values are identical. Token allocation is input/session metadata only: autosave creates neither a SessionCountAttempt nor a CountConferred event and reveals no calculation.

ConferSessionCount serializes on the session and enforces UNIQUE (SessionId, ConfirmationToken) and UNIQUE (SessionId, AttemptNumber), plus one CountConferred event per AttemptId. After current authority checks it first resolves any already-conferred requested token: identical count/ledger/opening revision fingerprint returns the **same immutable AttemptId/event** even if the two clicks used different Idempotency-Keys or an old root Version. Changed fingerprint on a used token is 409, never a second calculation. The response includes current CandidateState (Current/Outdated/Unconfirmed) separately from the original immutable result, so replay cannot make an old attempt current. A replay of an existing attempt checks current authority to read that session, without requiring it still be Draft; it never mutates count or financial state. If the token is unused, Draft/edit authorization, current token and current versions are required to create an attempt; allocate AttemptNumber and persist attempt/calculation/event/replay result in one transaction. Unique-conflict races resolve by loading that same attempt and checking fingerprint, not by creating a new token or event. Failed confirmation consumes neither token nor number.

An unchanged count after a ledger change may be explicitly confirmed again **without Recontar**: the ledger mutation has already issued a fresh token, the form shows the unconfirmed result and the extra acknowledgment **Confirmo que esta contagem ainda representa o caixa**, and ConferSessionCount requires `ConfirmCountStillObserved=true` for that unchanged-count/ledger-changed path. That action creates a new immutable attempt against the current revisions; the earlier event survives. Recontar is the separate path when the operator recounts/edits, including choosing to recount to the same values; it issues a new token, hides the current calculation, then requires Conferir. Editing invalidates the candidate and rotates token; merely retrying or clicking twice does not. E12's recount is one allowed path, not a requirement to press Recontar solely because the ledger changed.

#### Common proposed command envelope

No route/DTO exists for this envelope yet. Names use verbs + Session to avoid confusing hardware Terminal with an Account or authentication refresh session. P2 implements .NET DTOs and OpenAPI; Shared regenerates afterwards. Every read/response includes BranchId, CashboxAccountId, TerminalId, SessionId (when one exists), business Date, Sequence, CustodyState, ReviewStatus, custodian, immutable recorder and current submitter separately, permitted actions, and current Version/ETag. Return context is client-held, branch-validated source route + mode + selected session + filters/window + focus; commands never trust it for authorization or choose a redirect from an arbitrary URL. Web keeps `/f/{branchId}` independently per tab; Mobile keeps equivalent scoped navigation. Every command returns affected exact session ids/versions and invalidated candidates/successor when relevant, so it can return to cockpit, fast-entry, review or queue without losing origin.

Financial creates, start, handover, finish, Conferir, recount and approval use required Idempotency-Key scoped by branch + actor + operation, request fingerprint, durable response/result identity, with unique insertion inside the mutation transaction. Exact replay returns original outcome; different payload on key → 409; concurrent key waits bounded or retryable 409. Authorization is freshly checked before replay; an inaccessible old result never leaks. Saves use strong If-Match and EF xmin; identical retry with an old token is stale, never a duplicate financial effect. Domain LedgerRevision is a durable monotonic number independent of xmin: every affected cashbox ledger mutation/finalize/cancel and generated-field change increments it in the same transaction; count/opening revisions invalidate the attempt too. A request precondition is checked after locks/reload, and the final commit checks again through tracked concurrency. P7 field-save semantics: omitted fields preserve, explicit zero cancels, null is invalid; fees must be nonnegative and report may be signed, each within numeric(14,2), at most two decimal places, never silently rounded by financial commands. Never expose a Draft expected balance through permissions, queue, autosave response or unrestricted preview before Conferir.

All writes take shared branch month boundary → branch authority/pairing coordination when needed → affected account keys sorted ascending → tracked session/offer rows, revalidate membership/grants/pairing/LockDate, then write aggregate + legs + events + idempotency outcome in **one transaction**. Month lock takes exclusive boundary. This is the single global order — month boundary (shared) → branch pairing key → cashbox/Tab account keys ascending → Account root rows ascending → session/offer rows — and every P2 path that needs two of these takes them in it. Shipped pairing, unpairing, Terminal/Tab creation and account deactivation take the pairing key alone, without the month boundary or account keys (`AccountPairingCoordination`). Once deactivation or Terminal reassignment must refuse active custody, P2 moves them into this order: a read of "no active session" under the pairing key alone can race a start that already holds the account key, and a path taking pairing → month shared can deadlock behind a queued exclusive month lock held up by a writer waiting for pairing. No network/provider step inside this transaction. Cancellation/timeout commits nothing. 400 malformed/negative count/invalid method; 401 absent authority; 403 insufficient role/grant/custodian; 404 missing/foreign; 409 stale version, lock, frozen ledger, changed opening/ledger, unresolved chain, competing active custody, reused key with different payload. P2 defines resource constants in the existing ErrorMessages list shape; clients render returned messages verbatim, no invented existing error key.

| Proposed command (owner) | Purpose and request fields beyond common key/precondition | Response / source-return context | Authorization, guards, atomic effects and events |
|---|---|---|---|
| `RecordOpeningCount` (P3) | AccountId, AsOfDate, complete ProductId/Value vector; If-Match on existing proposal or explicit create-only precondition | OpeningCountId, revision/vector/source kind; owner checklist/go-live origin | Manager/Admin, no prior counted/imported session or active custody; grant/branch validation; set proposal + OpeningCountRecorded; no variance, debt or financial row |
| `StartSession` (P2/P3) | AccountId, CustodianOperatorId (self normally), business Date, source id/kind/revision, full acknowledged vector, explicit-zero acknowledgment if zero, differing items/reason when applicable, expected cashbox version. No operation payload: the atomic first operation is the financial command carrying this same acknowledgment (`RecordSessionOperation`) | New SessionId/SessionOpeningId/versions, optional OpeningDiscrepancyId/revision/Unattributed delta; cockpit/fast entry resumes same branch | Member own link + box grant; manager explicit custodian/reason. Current-day normal start, no future custody; reject missing source/unacknowledged zero/active custody. Resolve current source under lock; start+opening+optional discrepancy+SessionStarted/OpeningAcknowledged/OpeningDiscrepancyOpened all or none; no direct handover is invented for Finish→Start |
| `SaveSessionCount` (P2/P4/P7) | exact SessionId, full items, Notes semantics as current save, three close fields once P7 ships; If-Match | saved count revision/version, candidate invalidation, affected successor; preserves local entry on failure | Owned current Draft or existing authorized recorder correction; manager historical reason. Save values + field effects + audit; first recorder immutable; CountSaved/OpeningChanged as applicable; no Conferir event/calculation disclosure |
| `ConferSessionCount` (P4) | SessionId, CountRevision, LedgerRevision, OpeningRevision and server ConfirmationToken; ConfirmCountStillObserved when unchanged count follows a ledger change | AttemptId, immutable count/calculation/revisions, CandidateState and confirmed candidate only when still current; close form origin | Same save scope, Draft, current versions; count exists, no gaps/Drafts for submit candidate. Under locks calculate on server and append CountConferred once per domain token/AttemptId regardless of request keys; failure preserves typed/saved count |
| `BeginRecount` (P4) | SessionId, previous AttemptId, expected versions | New CurrentConfirmationToken (number assigned only on successful Conferir), input retained, expected/difference hidden; close form | Same scope; RecountStarted, invalidates candidate, no calculation until a new Conferir; same values may still form distinct attempt |
| `SubmitSession` (P2/P4) | SessionId, AttemptId and all confirmed revisions; If-Match | Submitted session, official variance/revision and submitter; queue/cockpit origin | Existing recorder correction windows or manager, current candidate required; no Draft financial rows or unresolved dependent gaps; atomically calculate/check revisions, persist official snapshot + SessionSubmitted and freeze. Early P2 retains current submit calculation until P4 adds attempt requirement |
| `OfferSessionHandover` (P2) | outgoing SessionId, intended incoming operator, verified closing revision/attempt, If-Match | HandoverId/version + immutable offered vector, outgoing/incoming context | Outgoing custodian or explicit manager exception; Submitted, active custody, no unresolved prior offer. HandoverOffered, no financial legs; P2 uses saved submitted closing until P4 attempts ship |
| `AcceptSessionHandover` (P2/P3) | HandoverId, outgoing/offer versions, source revision, incoming acknowledged vector (differences explicit), acknowledgment. No operation payload; an atomic first operation carries the same HandoverId acknowledgment through `RecordSessionOperation` | outgoing HandedOver + incoming SessionId/SessionOpeningId, optional OpeningDiscrepancyId/revision/vector/total/version; incoming cockpit/entry | Intended incoming, own box grant and link; manager exception is separate command. Recheck offer and outgoing version + lock; end/start same boundary, preserve both vectors, HandoverAccepted/OpeningAcknowledged/OpeningDiscrepancyOpened. One transaction; stale grant/first-operation failure leaves outgoing untouched |
| `DisputeSessionHandover` / `WithdrawSessionHandover` (P2) | HandoverId/version, reason; observations for dispute | Offer state/event; same handover/queue | Intended incoming disputes; outgoing or manager withdraws only before acceptance. No start, custody/count rewrite or legs; HandoverDisputed/Withdrawn; new offer requires explicit resolution |
| `FinishSession` (P2) | SessionId, verified submitted closing version, reason if exceptional; If-Match | Finished session/end time/proposal; cockpit/queue | Custodian or explicit manager exception, Submitted, no unresolved offer; SessionFinished, no synthetic incoming user. New start must acknowledge its own opening |
| `ResolveCustodyException` (P2) | exact session/offer, action Finish/AcceptAndStart, incoming operator when starting, observed vector, reason and versions | explicit outgoing and new incoming states, SessionOpeningId/OpeningDiscrepancyId when starting, manager-acknowledged evidence; queue | Manager/Admin only, full grants/branch checks for named operator; reason mandatory; Finish accepts a Submitted or still-Draft session (F1), the Draft remaining a submit/lock blocker; start also creates any opening discrepancy atomically under the same rules, CustodyExceptionResolved; never impersonates an incoming acknowledgment |
| `DeclarePastSession` (P2, F2) | AccountId, CustodianOperatorId, declared StartedAt/EndedAt (past, one business day), reason; create-only precondition | New Finished/Draft SessionId, Sequence, affected successors and their versions; review/queue origin | Manager/Admin only. Database rejects overlap, invalid window and date incoherence; server checks lock dates, operator/branch and versions under the §6.20 order. Inherited opening, `ManagerDeclared` evidence, no acknowledgment; renumber Sequence, apply successor correction rules, SessionDeclared — all or none |
| `RearrangePastSession` (P2, F2) | SessionId, new declared StartedAt/EndedAt (and so business date), reason; If-Match | Session with new interval/date/Sequence, moved row ids, successors affected at old and new positions; correction origin | Manager/Admin only; Finished/HandedOver and Draft only, never active custody or an acknowledged start time. Items via the existing correction save. Move the session's cashbox rows and generated-pair sources to the new date (DueDate kept); refuse on any locked date/session, reconciled settlement or unresolved mapping. SessionRearranged with before/after — all or none |
| `AttributeOpeningDiscrepancy` (P2) | OpeningDiscrepancyId, DiscrepancyRevision, If-Match Version, Outgoing/Incoming/Neither, reason, prior attribution id if superseding | attribution event/current target, signed amount and incoming SessionOpeningId/SessionId; optional HandoverId/predecessor; queue/review | Manager/Admin, exact incoming/predecessor dates lock-aware, identities derived from evidenced sessions. No Outgoing target without known predecessor custodian. Append AttributionRecorded atomically; stale comparison/version→409, unavailable target→400. Unattributed never personal/threshold; Neither resolved but not personal; revised comparison invalidates personal projection until explicitly re-attributed |
| `RecallSession` / `ReopenSession` / `RejectSession` (P2) | exact id, If-Match, reject reason, manager historical reason as applicable | current Draft/Rejected + lineage/official suppression; correction/queue origin | Retain existing role/day/recorder windows; Approved reopen manager-only; reject Submitted manager-only. Review event, clear latest submit/approve as applicable, custody unchanged; invalidate any pending offer affected by corrected closing |
| `ApproveSession` / `ApproveCashboxDay` (P5) | exact session or explicit list of SessionId+Version (not dynamic all-on-date), normal/SingleActiveManager mode, exception reason | versions and immutable approval events; review/queue origin | Manager/Admin excludes recorder, submitter and custodian via operator identity and the custodian user captured at start as well as the current link; changing a login link cannot bypass the exclusion. Count every active branch Manager/Admin membership backed by an active user when deciding the single-manager exception, not only logged-in/linked managers. Single-active-manager exception rechecks current branch memberships and explicit reason. Batch **all-or-nothing**: validate each and rollback all on any failure, return offending session(s) and reason; never skip or silently exempt |
| `RecordSessionOperation` (P2/P6/P9) | The **existing** guarded financial commands (generic create, *Receber pagamento*, installment create; later P6/P9 operations), never a second create route. Cashbox AccountId resolved server-side to the active session, or an optional `OpeningAcknowledgment` block (the StartSession fields, or HandoverId + offer version for an acceptance) when no session is active; explicit operation intent/type/value/date/client/method as needed; historical exact SessionId + reason for manager | source/leg ids, session/ledger versions and candidate invalidation; fast-entry/payment origin | Member active custodian/grants, fiado both grants, NonCash Tab-only exempt cashbox. Guarded Active/Draft creation, pair+audit atomically; GeneratedField/Transfer cannot be manually forged. Finalize/Cancel use source If-Match, preserve exact session and operate all legs or none. With an `OpeningAcknowledgment`, the command's own Idempotency-Key covers start/accept + opening + discrepancy + operation + legs + events in one transaction; exact replay returns the original session and operation. A pending handover keeps the outgoing session in custody, so a block naming that session's unresolved offer (`HandoverId` + offer version) accepts it atomically with the operation; any other block sent while a session is already active (a lost response retried with a new key, or a competing start) is 409 competing custody and creates nothing; a request without the block when no session is active is 409 opening acknowledgment required, never an implicit start |

**Historical writes are not new custody.** Manager names exact SessionId and original business date within that session's declared date, after authorized recall/reopen when required, with reason; no active-session fallback, no changing old session's custodian or moving a transaction across sessions. Rejected/unresolved or incomplete migrated identity is not writeable until resolution. A current operation cannot be backdated into another person's old session merely by changing Date. Cross-day custody requires explicit finish and next branch-local-day start in P2; this avoids silently allocating flows across midnight. A forgotten past day is recorded only through `DeclarePastSession`/`RearrangePastSession` (F2), never by backdating a current operation. Exception resolution may finish an old active session; it cannot create overlapping historical custody. Dates beyond lock remain mandatory even for exceptions.

#### Shipped routes (v59)

| Command | Route and headers | Shipped notes |
|---|---|---|
| `GetOpeningProposal` | `GET /session/opening-proposal?accountId=` | Source kind/id/revision/date, `IsMissing`, active session, pending offer, unresolved history and `CanStart`; read-only |
| `StartSession` | `POST /session/start`, Idempotency-Key | Body `AccountId` + `Opening` acknowledgment (echoed source, items, `ExplicitZero`, reason, optional custodian). Response `ResponseStartSessionJson` (session, `SessionOpeningId`, optional discrepancy) |
| `RecordSessionOperation` | `POST /transaction`, `/transaction/installment`, `/transaction/fiado-payment`, Idempotency-Key | Optional `SessionId` + `HistoricalReason` (Manager/Admin, exact Draft session of the row's date) or `OpeningAcknowledgment`: a start when no session is in custody, or `HandoverId` + `HandoverVersion` to accept the pending offer of the session in custody (the only acknowledgment allowed over active custody; any other is 409 competing custody). Responses add `SessionId`, `StartedSessionId`, `OpeningDiscrepancyId`; transaction reads add `SessionId` |
| `SaveSessionCount` | `PUT /session/{sessionId}/items`, If-Match | A changed count increments `CountRevision`; an acknowledged successor gets a discrepancy revision, an inherited one the opening recheck |
| Submit / approve / reject / recall / reopen | `POST /session/{sessionId}/{action}` | Shipped review semantics per session; reject, recall and reopen withdraw a pending offer |
| `OfferSessionHandover` | `POST /session/{sessionId}/handover`, Idempotency-Key, If-Match (session) | `IncomingOperatorId`, `CountRevision`, optional reason (required for a manager exception) |
| `AcceptSessionHandover` | `POST /session/handover/{handoverId}/accept`, Idempotency-Key, If-Match (handover) | The incoming session starts at the outgoing session's end instant |
| `DisputeSessionHandover` / `WithdrawSessionHandover` | `POST /session/handover/{handoverId}/dispute` or `/withdraw`, If-Match | Reason required |
| `FinishSession` | `POST /session/{sessionId}/finish`, Idempotency-Key, If-Match | Submitted session, no unresolved offer |
| `ResolveCustodyException` | `POST /session/{sessionId}/custody-exception`, Idempotency-Key, If-Match | Manager/Admin; `Action` `Finish` (Submitted or Draft, F1) or `AcceptAndStart` (pending offer, or finish + start of the named operator) |
| `DeclarePastSession` | `POST /session/declare`, Idempotency-Key | Manager/Admin (F2) |
| `RearrangePastSession` | `POST /session/{sessionId}/rearrange`, If-Match | Manager/Admin (F2); rows with `PaidAt` refuse a date move |
| `AttributeOpeningDiscrepancy` | `POST /session/opening-discrepancy/{discrepancyId}/attribution`, If-Match (discrepancy) | Manager/Admin |
| `RecordOpeningCount` (P3) | `POST /session/opening-count` (body `ExpectedRevision`); `GET /session/opening-count?accountId=` | Manager/Admin |
| Reads | `GET /session` (filters add `CustodianOperatorId`, `CustodyState`, `TerminalId`), `GET /session/{id}`, `/review` (adds `OpeningDiscrepancy`), `/history` (adds `CustodyEvents`, `Handovers`, `IsCustodyHistoryComplete`) | Every session response carries `Custody` (`ResponseSessionCustodyJson`) |
| Terminals | `GET /terminal`, `POST /terminal`, `PUT /terminal/cashbox/{accountId}` | `POST /account/terminal` accepts an optional `TerminalId` and otherwise creates one |
| Work Queue | `GET /report/manager-queue` | Adds `Custody`: open sessions (with `StartedBeforeToday`), pending handovers, `OpeningDiscrepanciesAwaitingAttribution` (nonzero, current revision without attribution: never attributed or superseded by a correction), unresolved mapping-issue count |

**Shipped deviations from the envelope above.** Custody commands check the caller's current cashbox authority before returning a stored replay; the shipped financial creates still replay before their own scope checks. Approval keeps its shipped contract until P5 (no Idempotency-Key). `LedgerRevision` is not incremented before P4, so P2 candidates rely on the shipped submit calculation. A manager starting a session for themselves needs a linked operator; for another custodian it is a reasoned `ManagerException` and requires that operator's cashbox grant. Pairing, unpairing, deactivation and terminal reassignment now wait in the account coordination, so a held pairing key surfaces as `DAILYCLOSE_LEDGER_COORDINATION_BUSY`. Tab/Bank rows carry no session and are never frozen by one.

#### Read surfaces (P2 shipped above, extended P4/P7)

`GetSession`/`GetSessionReview` take exact SessionId; `ListSessions` takes optional cashbox/Terminal/custodian/date/status/custody filters, Page≥1 and PageSize 1–100 (default 50), stable Date/Sequence/Id ordering and whole-filter TotalCount. Return envelope: Items, Page, PageSize, TotalCount, plus full common identity/state/version context; review includes immutable acknowledged opening, proposed comparison source, counts, source completeness, ledger/count/opening revisions, official snapshot with status, handover state, opening discrepancy/attribution context (including standalone openings) and permitted actions. Missing/foreign→404, Member link/grant denial→403; lists intersect grants before counts/paging. Manager may read inactive historical entities; a deactivated cashbox cannot start.

`GetOpeningProposal` takes AccountId + intended custodian and returns source kind/id/revision/vector, missing-vs-explicit-zero state and cashbox version without starting custody. It checks start authority/grants, reads a consistent snapshot and never creates acknowledgment. `GetSessionHistory` returns event/attempt identities, times, immutable actors, revisions, source/correction lineage, IsReviewHistoryComplete/IsCustodyHistoryComplete, and unavailable evidence explicitly null. Own-session Member sees own past attempts after Conferir; manager sees full authorized history. Before first Conferir (and during Recontar), ordinary form reads/autosave/transaction previews must not leak expected closing or a fresh difference; historical attempts remain clearly past evidence, not a current candidate. An expected/variance read after a ledger change is labelled Unconfirmed and never creates an attempt. These reads never commit, require no idempotency key and return a point-in-time revision, not a promise that a later write will succeed. P4 retires/restricts current variance-preview and related transaction preview disclosure for blind sessions, updating all consumers together.

### 6.21 Consumer migration and opening-chain acceptance

| Consumer | Shipped baseline → required pending change / owner |
|---|---|
| Close open/save/submit/review/history, recall/reopen/reject/approve | Account/day lookup → exact SessionId/sequence; carry custodian separately from recorder/submitter; return custody and review independently; P2; P4 count attempts and P5 approval restrictions. Existing events/history-complete flag retained |
| Calculator + ledger guard/freeze; create/update/finalize/cancel and plan finalize | Current account/date sums → session-linked sums and the acknowledged opening as `TotalOpening` (inherited predecessor closing only for unacknowledged migrated sessions, §6.20); no sibling/sequential session leakage; generated source/counterpart each guarded by actual account/session. Draft historical correction does not let current counter work use old session; P2 |
| Opening/submit-gap checks | Previous counted date → same-box predecessor/provenance/acknowledgment. No uncounted financially active predecessor may be skipped by today's submit; unknown legacy activity remains blocker. Empty inactive dates create no invented session; P2/P3 |
| Work Queue / backlog | Enumerate sessions, open custody, unresolved offers, unattributed opening discrepancies (handover-backed or standalone) and recheck blockers independently of selected day; retain selected-day/backlog window distinction; open historical Draft is a review blocker, not current custody. P2 |
| Cash-variance summary / monthly reconciliation | Official submitted/non-Draft snapshots by SessionId, review status shown; lock needs every expected session approved, no Draft rows, orphan activity or required opening recheck; unattributed opening discrepancies remain separately visible and are not an extra blocker; P2. Never add sequential stock closings |
| Monthly financial / management overview / daily ledger | Replace TerminalDays grouping with session identity + custodian, preserve actor identities, fee-only/orphan cases remain visible blockers. Aggregate flows/official variance; inventory at a cutoff is the latest acknowledged/current count **per cashbox**, not sum of every session closing or a grand total mixing banks/Tabs/cashboxes. Keep read-only snapshot/failure atomicity; P2/P7 |
| Operator summary / statement / hours | Authorship summary remains recorder-based, generated portion included once; custodian liability is explicitly a separate session metric. Fiado FIFO stays branch/client-wide. Hours remain attendance/operator/day, never duplicated per session; P2/C7/D1 |
| Month lock | Exclusive month boundary retained; readiness covers all sessions and direct orphan cashbox activity, not physical Terminal as a single expected closer. Attribution is not a new readiness condition; locking an approved session may retain an unattributed opening discrepancy as separate history. Future C events post to open PostedOn; original locked sale untouched; P2/C1 |
| Account grants / pairing / deactivation | Same account ids and explicit Tab grants; refuse deactivation/reassignment while active custody/unresolved handover (resolve explicitly), taking the §6.20 global coordination order instead of the shipped pairing key alone; do not erase existing paired history. Default paired Tab creation P6 does not imply Tab permission; P2/P6 |
| Workstation selection / shell | Choose physical Terminal then granted cashbox and current session (or acknowledgment); parallel boxes allowed; old account/day cache key insufficient. Never identify custody from recorder or hardware choice; P2; Web/Mobile M4/M5 plus M6 entry and M7 review increments |
| Contracts / clients / design | P2 DTO/OpenAPI is authoritative; Shared regenerates, apps own UI/transport/storage; no manual generated edits. Lane 1 reconciles each phase; Lane 2 waits for shipped contracts. M9.6 P8 arithmetic lives solely in Shared/core; only validated resulting decimal reaches server, never expression/eval or client variance. Design 4.9.7 owns input interface |

Official variance is a submission snapshot, not the latest hypothetical result. Current confirmed candidate is one immutable attempt matching count, ledger and opening revisions; editing invalidates it. A ledger change may yield a separately labelled **unconfirmed** recalculation after Conferir; it never overwrites the attempt or becomes official. A recalled/reopened/invalidated Draft hides retained official value but keeps history. Rejection retains the last submitted snapshot with rejection state until correction demotes it. Correction lineage names source and immediate dependent session with before/after opening evidence; same day or multiple cashboxes cannot collapse identity.

### 6.22 Upgrade and preflight contract — M9.6 Phase 2

**Shipped in v59.** `M9_6Phase2SessionCustody` sets every existing close to `MappingState=Unresolved`, `Origin=Migrated`, `CustodyHistoryComplete=false`, keeping ids, dates, statuses, items and actors, and records one `CustodyMappingIssue` per legacy close (`LegacySessionUnmapped`) and per cashbox row without a session (`OrphanCashboxActivity`) with a SHA-256 source digest; the work queue reports the unresolved count. Unresolved rows stay readable and refuse every command; a cashbox with unresolved history cannot start, declare or record an opening count, and month lock treats Unresolved closes and unsessioned cashbox rows as blockers. The quarantine update changes `xmin`. No reviewed mapping-plan apply path ships yet; the owner chose to reset disposable development and staging data with `server/scripts/reset-disposable-database.sh` (refuses other database names, asks for confirmation).

The general preflight contract follows. Development/staging upgrade must preflight the prior schema before dropping account/day uniqueness; preserve old ids, dates, statuses, recorder/submitter and recorded before/after version provenance (mapping writes may change xmin; never promise the old ETag survives) and all locked amounts. Provide aggregate/anonymized issue counts publicly; private per-id mapping stays outside Git. Mapping plan itself has id/version, reviewed actor/date, source digest and per-row dispositions, rechecked before execute. Unknown actor/time never acquires a fabricated value from migration time or account name.

| Prior case | Required resolution / treatment |
|---|---|
| Unique active account/day close | Preserve DailyClose.Id as SessionId, AccountId as cashbox. Explicit physical Terminal and custody mapping; account name/User.con_id is evidence, not proof of actual custodian. Reviewed sequence/time boundaries must not overlap |
| Approved/Submitted, no later activity | Eligible for Finished **only** with explicit supported custodian/boundary mapping; preserve review status. “No later activity” alone is not proof of end time or acknowledgment. Missing evidence → quarantined history/read-only unresolved record, no active custody and no fake zero acknowledgment |
| Draft/Rejected, later activity or several candidate custodians | Resolution list: decide historical correction vs actual open custody, named owner and evidence; cannot blanket-convert to Finished. Approved history remains approved until existing rules explicitly demote it; no automatic approval |
| Transactions without a close | Orphan-activity issue; map to supported real session only with evidence, or retain unresolved read-only provenance and block go-live/lock for affected history. Do not generate a count, custodian, closing or date to make it pass |
| Existing opening-side fee/report rows | Reviewed branch/type identities; map each ordinary row to exact session/field or keep legacy explicit source with audit. Multiple rows/ambiguous totals require resolution; no generated duplicate. P7 activates field ownership only after mapping; distinguish manual legacy origin from generated replacement |
| Retired product fee / AA / first transition | Preserve locked counts and known hidden prior-only fee −100 case. AA recovery and reviewed identity/transition gate stay open (M9.5 3.7); no compensation, delete, opening rewrite or mass marker acceptance |
| Inactive account/operator, old grants | Keep historical FKs/visibility, no new active custody or grant. Explicit resolve/reactivate through approved policy if needed; do not silently unretire or infer user/custodian from recorder |
| Incomplete review/custody history | Keep ReviewHistoryComplete=false and separate CustodyHistoryComplete=false. Read-only exception evidence, no synthetic events/acknowledgments or inferred actor/date. Required unknown fields quarantined before operational constraints activate; unresolved affected scope blocks cutover, never silently dropped |

Preflight also rejects foreign branch/session references, overlapping intervals, duplicate active custody, cycles, ambiguous paid/opening sources and unreconciled old generated pairs. A corrected plan can retry idempotently against unchanged source digest; stale source or partial plan aborts transaction. Schema+mapping activation must be atomic or a controlled read-only maintenance cutover; no mixed old/new writers. Already accepted historical mappings remain traceable to their real source, with unavailable times explicitly unavailable. An **authorized development reset/reseed** is an alternative for disposable fixtures only, documented by the runtime owner; this task authorizes no destructive command. M10 import is later and uses the same explicit mapping/resolution, never “every Access day is Finished” by default.

### 6.23 Approved boundaries reserved to later phases

- **P3 opening is delivered with inline opening**, independent of M10 import. Distinguish missing proposal, explicitly acknowledged zero, previous session and imported session; failed first operation leaves neither opening session nor financial effect. Seeded openings support Batch B journeys; guided go-live UI remains M10/Design 4.9.15.
- **P4 blind count:** opening visible, expected closing/difference hidden until Conferir. Autosave stores input only. Idempotent double click returns one event; Recontar begins another attempt; no event overwritten. Submit atomically checks current versions; ledger-changed calculation labelled unconfirmed, no client recomputation.
- **P5 duties:** recorder, current submitter and custodian excluded from approval. Single-active-manager explicit action with reason/audit only after membership recheck; batch validates every included version/session and fails all on one failure.
- **P6 fiado:** only NEW COUNTER sale creates paired cashbox Out; opening/import/conversion commands never do. Repayment method required with NO DEFAULT; Dinheiro/PIX initial seeds, configured alternatives, every NonCash method changes no counted cash. Preserve branch-wide FIFO, debt/credit and explicit grants. Existing HandsOutCountedStock remains shipped until removal in .NET DTO/OpenAPI and regenerated clients.
- **P7 three fields:** generated unique effect per session/field; positive report In, negative Out, fees In; zero cancels active effect with audit. Repeated/concurrent saves do not duplicate; unique role slot points to versioned effect history, no reuse of source-only fiado uniqueness. Replacing sign cancels old effect/creates new magnitude atomically, never a negative Transaction.Value. Preview never saves. Unconfirmed R/fee overlap is unresolved product evidence, not automatic subtraction.
- **P9 safe:** two protected atomic legs for cash transfer; no cashbox variance at destination Safe. Initial expense seeds improve fresh setup; no settlement/provider feature smuggled into P9.
- **C settlements:** new events with OccurredOn and open PostedOn, immutable originals, partial remainder/as-of basis explicit. Pending bank receipt confirmed once by matching; unexplained differences stay unmatched, fees only provider-supported/manager-confirmed. Posting-time classification freezes locked history, replacing shipped query-time reclassification. PaidAt migration reconstructs provenance without a second payment; unknown posting dates flagged, not guessed.
- **D people/go-live:** effective-dated configurable commission (legacy 18.5% is one rule); no automatic shortage payroll deduction. Opening obligations use dedicated commands. Replay/import D4 and full acceptance D5 remain later. Neither 220/232 nor 227/232 is a test run by this documentation batch.

## 7. Key Workflows

### 7.1 Operator daily flow (mobile app)

The following is the shipped account/day workflow. Pending session flow: acknowledge opening (or inline opening+first operation atomically) → operate exact session → blind count/Conferir → submit → finish or offer/accept handover. See §6.20 for states and versions; opening remains visible.

```
1. Login → system resolves Operator → finds primary Account (terminal)
2. "Open Day" → creates DailyClose with Status=Draft for today + account
3. Throughout the day: add transactions
   - Tap transaction type button
   - Fill required fields (value, time, client, due date — varies by type)
   - Save → Transaction with Status=Active
   - Or save as Draft → Transaction with Status=Draft (finalize later)
4. "Close Day" → enter closing snapshot values (DailyCloseItems)
   - System shows opening values (yesterday's closing for each product)
   - Operator enters today's closing values
   - System calculates/displays CashVariance
5. "Submit" → DailyClose Status=Submitted
6. Wait for manager approval
   - If Rejected: operator sees reason, edits, resubmits
   - If Approved: day is finalized
```

### 7.2 Manager daily flow (web dashboard)

> **Served by (v56):** step 1 is the Manager Work Queue (`GET /report/manager-queue`), the exception-first home; the read-only Dashboard (`GET /report/management-overview`) summarizes every manager report with links to each and performs no action. Step 3's upcoming due dates are `GET /report/upcoming-settlements` (with open-cheque and fiado aging); step 4 is `GET /report/monthly-financial/{year}/{month}` — close figures per Terminal/day with recorder and submitter, hours per operator/day, on the source definitions reconciled with §9; proposed per-session attribution remains pending M9.6 P2. The CEF borderô comparison stays manual. The flow below is the original workflow description.

```
1. Login → see yesterday's summary across all operators
   - CashVariance per operator (the key number)
   - Pending DailyClose submissions needing approval
   - Operators who haven't submitted yet (red alert)
2. Review each operator's submission
   - See DailyCloseItems (closing snapshot)
   - See transaction list for that day
   - Compare against expected values
   - Approve or Reject with reason
3. Receivables view
   - Outstanding Fiado by client (aging report)
   - Upcoming due dates (card settlements, pre-dated cheques)
4. Monthly report
   - CashVariance + Tarifa Bolão + Sobras Bolão + Hours by operator by day
   - Comparison with CEF borderô (manual for MVP)
5. Corrections
   - Cancel erroneous transactions (with reason)
   - Advance LockDate after month-end reconciliation
```

### 7.3 Fiado lifecycle

Shipped flag-based workflow below; P6 changes only new counter sales to unconditional pairing, and seeds repayment methods without a default (§6.23). Opening/import/conversion remain dedicated later commands.

```
1. Client buys on credit:
   → POST /transaction: Type="Cliente" under "Saídas", Direction=Out,
     Account=Tab account, ClientId=client
   → HandsOutCountedStock=true only when counted stock leaves the drawer:
     also writes the generated Terminal Out on the paired terminal (§6.4)
   → Client balance increases (customer owes more)

2. Client pays back (Receber pagamento, POST /transaction/fiado-payment):
   → Method = a configured repayment type (FiadoPaymentKind), no default;
     Tab In on the receiving Tab, ClientId=client
   → CountedCash also writes the generated Terminal In; NonCash writes none
   → Client balance decreases; FIFO settles the oldest-due sales first

3. Manager checks outstanding tabs:
   → GET /report/fiado/balance (branch-wide per client, signed)
   → GET /report/fiado/aging (FIFO unpaid remainders by due-date bucket)
   → GET /client/{id}/fiado-statement (running balance per movement)
```

---

## 8. Access-to-Lotero Mapping

### Tables

| Access Table     | Lotero Entity                | Notes                                                                                                  |
|------------------|------------------------------|--------------------------------------------------------------------------------------------------------|
| TblUsuario       | User + BranchUser + Operator | Split into 3 concerns                                                                                  |
| TblContas        | Account                      | Added Tab type, self-referencing FK                                                                    |
| TblCategoria     | Category                     | Data table, not enum                                                                                   |
| TblTipoCategoria | TransactionType              | Child of Category                                                                                      |
| TblLancamentos   | Transaction                  | Added Status, CancelledAt, TransactionTime, OriginTransactionId, RecordedByOperatorId, CreatedByUserId |
| TblClientes      | Client                       | Absorbed TblTelefones                                                                                  |
| TblProduto       | Product                      | Only daily-close products, not workarounds                                                             |
| TblEstoque | DailyClose + DailyCloseItem | Current account/day close + items; proposed session mapping requires explicit custody evidence |
| TblRegistroPonto | TimeEntry                    | Linked to Operator, not Account                                                                        |
| TblFeriados      | Holiday                      | Per-branch                                                                                             |
| TblConfiguracao  | Setting                      | Expanded with time-tracking config                                                                     |
| TblLogExclusao   | *(absorbed)*                 | Replaced by soft delete fields on Transaction                                                          |
| TblTelefones     | *(absorbed)*                 | Merged into Client                                                                                     |
| —                | Branch                       | NEW: multi-tenant                                                                                      |
| —                | BranchUser                   | NEW: role per branch                                                                                   |
| —                | Operator                     | NEW: employee concept                                                                                  |
| —                | OperatorAccount              | NEW: account assignment                                                                                |
| —                | RefreshToken                 | Existing: auth                                                                                         |

**Additional exportMestre tables (future targets, not shipped entities):**

| Access source | Target / owner and ambiguity |
|---|---|
| TblItemRel / TblRelMensal | Configurable pay items / employee monthly statement, M9.8 P1 + M10 B; month without year requires explicit resolution |
| TblLoteriaEspecial | Special-draws catalog + Tarifa Especial allocation, M9.8 P1/M10 B; prod14 → M9.6 P7 field, not ordinary stock |
| TblMovimentacaoBancoHoras | Attendance-linked source vs Pagamento/Ajuste entries, M9.8 P2/M10 B; preserve source identity, no duplicate attendance minutes |
| TblVersaoBE / TblVersaoFE | Import provenance/version, M10 B; not financial or runtime lookup entities |

Legacy table count 13 below describes the earlier subset; exportMestre has 19 exported tables, including these six. Shipped app entity count remains 30; proposed model records are not included in it. Access prod7/12/14 map to fee/report/fee effects only after reviewed identity/session mapping, never duplicate ordinary and generated entries.

### Access columns → Transaction columns

| Access (TblLancamentos) | Lotero (Transaction)          | Notes                                                                                                                          |
|-------------------------|-------------------------------|--------------------------------------------------------------------------------------------------------------------------------|
| lco_id                  | Id                            | Auto-increment → Guid                                                                                                          |
| lco_data                | Date                          |                                                                                                                                |
| lco_valor               | Value                         | Access decimal → Postgres numeric(14,2)                                                                                        |
| id_categoria            | CategoryId                    | Integer FK → Guid FK                                                                                                           |
| id_tipo                 | TransactionTypeId             | Integer FK → Guid FK                                                                                                           |
| lco_descricao           | Description + TransactionTime | Time values split out to dedicated field                                                                                       |
| id_cliente              | ClientId                      |                                                                                                                                |
| lco_vencimento          | DueDate                       |                                                                                                                                |
| lco_data_pagamento      | PaidAt                        |                                                                                                                                |
| id_conta                | AccountId                     |                                                                                                                                |
| lco_sinal               | Direction                     | "Positivo"/"Negativo" string → In/Out enum                                                                                     |
| lco_condicao            | *(removed)*                   | Overloaded in Access (payment condition + authorization flag). Not needed with proper TransactionType and Description          |
| lco_forma_pagamento     | *(removed)*                   | Redundant with TransactionType                                                                                                 |
| lco_origem              | OriginTransactionId           | Integer ID → Guid self-referencing FK. First installment references itself.                                                    |
| lco_status              | *(unused in Access)*          | Always empty in production data. Lotero `Status` (Draft/Active/Cancelled) is new functionality, not a migration of this column |
| lco_dataRegistro        | CreatedAt                     | From EntityBase                                                                                                                |
| *(none)*                | BranchId                      | NEW: multi-tenant                                                                                                              |
| *(none)*                | RecordedByOperatorId          | NEW: which operator's context                                                                                                  |
| *(none)*                | CreatedByUserId               | NEW: who actually created the record                                                                                           |
| *(none)*                | CancelledAt                   | NEW: audit trail                                                                                                               |
| *(none)*                | CancelledByUserId             | NEW: audit trail                                                                                                               |
| *(none)*                | CancellationReason            | NEW: audit trail                                                                                                               |
| *(none)*                | TransactionTime               | NEW: extracted from Description                                                                                                |

### Entity count

|              | Access | Lotero                                                              |
|--------------|--------|---------------------------------------------------------------------|
| Tables       | 13     | 30                                                                  |
| New entities | —      | Branch, BranchUser, Operator, OperatorAccount, DailyClose (session) |
| Absorbed     | —      | TblLogExclusao (→ Transaction soft delete), TblTelefones (→ Client) |

---

## 9. Product answers, provenance and remaining questions

**Current fact register (2026-10-26, M9.5 Phase 5 candidate).** Product answers were received 2026-10-23; the approved owner plan records them in server M9.5 P5. The earlier 2026-10-20 working answers remain historical evidence in that milestone, superseded only for the consumers named here. Facts about shop practice do not certify runtime or propose unapproved meanings for incomplete events.

| Product answer / evidence | Disposition and exact consumers |
|---|---|
| Q1 cards: credit up to two days, debit one, grouped net of fees; reconciliation not yet done | Confirms receivable intent; no verified provider calendar/fee schedule. M11 P4/P5 instruments/upcoming stay shipped; C1/C3/C5 adds settlement batches and actual statement matching. Seed business-day timing is a configurable app choice, not proof of the provider's actual timing |
| Q2 paid cheque = **deposited**, not cleared | Supersedes working Q2. Shipped PaidAt remains current read/edit mechanism; seed Cheque identity and M11 P4/P5/P6 report consumers unchanged structurally. C2 separates Safe custody from bank deposit and models return/re-presentation; matching never pays twice |
| Q3 fee: quota 100 + service fee 35 = customer price 135 | Confirms daily Tarifa Bolão service income. M9.5 3.6 ordinary In seed and M11 P6 fee role retained. P7 generated close field is future; no automatic 35% universal fee rule follows |
| Q4 unsold quotas/Federal charged fully by CAIXA | Confirmed shop loss; M11 P6 CommercialLeftover on both Sobra types remains shipped. Access monthly query counts only Bolão, so comparison must explain scope, never pretend identical reports |
| Q5 stock valued at purchase cost and sold at same value; operators also buy with drawer cash; shop does not record delivery/returns | Confirms count-only cash exchange (M9.5 3.1, §6.12). Initial entrusted stock ≠ drawer purchase. O16/P3 opening and O17 funded restock differ from O18 delivered restock In (deliberate app control). Specific product-type events remain incomplete; 3.2 naming deferred, formal 3.4 extension decision still deferred/pending |
| Q6 deposit = drawer → **cofre inteligente** | Supersedes bank interpretation. Current ordinary cashbox Out remains shipped (seed NextCalendarDay does not prove arrival at bank); P9 adds protected Safe counterpart; C4 later Safe→Bank |
| Q6 PIX = customer pays bill/game into shop account; MarketPlace = CAIXA online games, credited end of day | Supersedes withdrawal interpretation. O04/P7 report expectation offset, C5 instruments/matching; not proof of current bank-side recording |
| Q6 prize = drawer payout; discount = bolão discount; rejected slip = misprint that cannot be reversed; Outras Despesas = outside listed types | Physical/event meanings only. Report inclusion and event subtypes not fully answered; O12/O13 conditional premises, C4 prize reimbursement, C6 classification. No invented universal sign/type rename |
| Q7 bank movements are **not recorded on both sides today**; CAIXA reimburses prizes to bank | Supersedes working Q7. Bank remains outside cashbox variance; P9/C4 paired transfer is deliberate improvement. New export contains no bank lançamentos: evidence of this file, not proof bank events never occurred |
| Q8 clients buy bills and games on fiado | Shipped HandsOutCountedStock covers only subset. M9.6 P6 removes that choice and pairs **new counter sales**, not opening/import/conversion; M7.7 FIFO/grants/payment semantics retained |
| Q10 every person pulls report for their period, responsible for it; shared and separate cashboxes both occur | Supersedes working one-close/day attribution. Shipped M11 TerminalDays still account/day with recorder/submitter; P2 session model/consumer migration changes it, C7/D1 later personal totals. No retrospective custody guessed |
| Q9 was not understood | CAIXA-report interpretation of Operador is owner-adopted inference, not product confirmation. Exact printed label, content/inclusion of stock, fees, prizes and tender offsets, and sign remain **pending** with product owner + M9.5. Design label Relatório do terminal is app vocabulary. Signed support is an app design choice; never assert legacy report goes negative |

**New export evidence (read-only; private source outside Git).** `../exportMestre` is the exported LoteriaMestre BE/FE 1.5.0 file, not a complete audit of shop activity. `row_counts.txt` records 4,642 ledger / 2,355 stock rows; this batch checked those aggregate file counts. `forms/all_form_code.txt:375` sums terminal values without sign; `:408`, `:1066–1092` include Tarifa Especial and the two fiado balances; `:4010–4013` gives Troca/Compra labels; `:7311` shows the 18.5% commission expression. Queries/schema identify the extra tables (§8). Source anchors identify exported mechanics, not the certainty of every inferred product meaning.

The Access equation is `(today physical counts + unsigned Terminal lançamentos + today's paired Tab balance) − (literal previous-day counts + previous-day Tab balance + Operador + Tarifa Bolão + Tarifa Especial)`. The app keeps §6.12 with last-counted opening across gaps and linked fiado legs, never adds Tab balance as well. **Reported, not reproduced here:** legacy replay 220/232; proposed representation 227/232; 160/175 disagreements attributed to fiado; 69 single cheques / 18 plan rows. These figures are bounded prior analyses, not current test results or universal equivalence. Cheque counts depend on classification/query assumptions. Tarifa Especial is inferred from prod14, the form and draw table, accepted for planning by owner; its exact product-event allocation still needs C/D evidence. No method field in legacy data cannot establish that noncash repayments never occurred. Noncash, shared/separate custody and new controls require new acceptance journeys; numerical parity must not manufacture missing report premises.

**Remaining acceptance:** Operador definition/printed label/sign and R inclusion above; product-like event distinctions; provider/calendar/fee facts; historical custody/AA/transition mapping. M9.5 5.1–5.4 documents the answers but does not itself close 4.2/whole milestone, 3.2, formal 3.4, 3.7 or external acceptance. M9.6 1.3 needs owner + independent reviewer acceptance of §3.30 and §6.19–§6.23 before runtime P2. Design markers advance only after independent technical semantic review, followed by the same implementer's justified marker patch and the same reviewer's final check.
