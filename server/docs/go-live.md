# Local branch go-live — M10

Use the existing local PostgreSQL/.NET/Next stack and a configured Manager/Admin branch session. This guide creates no production resources. Start the backend with the repository's local configuration and `dotnet run --project server/server.API`; start Web with its local API origin configured and `pnpm --filter @loto/web dev`. Open Gestão → Cadastros → **Implantação da filial**. Existing authentication/onboarding and Mailpit configuration are unchanged; no provider account is needed.

For a completely disposable demonstration, `pnpm --filter @loto/web e2e:stack start` starts the existing owned harness and prints its local origin/private state location. Fake credentials and captures remain outside Git. Stop only that harness with `pnpm --filter @loto/web e2e:stack stop <state.json>`. `pnpm --filter @loto/web exec playwright test go-live.spec.ts` owns and cleans an isolated stack automatically.

Activated-import openings are immutable cutover evidence: manual amend/cancel commands refuse even before repayment or cheque settlement. Normal subsequent FIFO/cheque events remain allowed; the source-to-target registry is preserved. Manual-origin openings retain their versioned amendment/cancellation rules. The opening list exposes nullable SourceImportId; Web identifies imported evidence and disables manual revision from that server-owned field. Bounded byte identity is checked before interpreting a re-upload; new sources still require full profile/schema/CSV validation. Byte digests identify exact files only; re-upload with a different profile or encoding conflicts rather than silently applying or ignoring the choice. Parsed sources and row indexes live only within the request; response redaction never mutates the cached evidence.

## Two supported paths

The manual path uses the existing OpeningCount product vector or explicitly confirmed zero, plus dedicated opening fiado/cheques. Obligations enter FIFO/receivables without cashbox rows, session, new income or operating expense. Opening and imported-debt types must use the client/CAIXA money classification without a monthly role; their later type reclassification is protected, and SQL also enforces the opening asset shape. Cheques may have an unknown client until later identification and may contain evidenced installments. Manual-origin substitution/cancellation is versioned and refused after consumption/observed fiado credit or lock. Consult the recorded obligation history before resolving an uncertain response.

The import path receives the complete CSV tables directory of either Windows (13 tables) or Mestre BE/FE 1.5.0 (19). Choose CP1252 or UTF-8 explicitly. Preserve exact filenames/header order and source values; do not submit Access binaries, archives or VBA. Limits are 16 MiB/file, 64 MiB total, 100,000 rows and 16,384 characters/field. Dates use `yyyy/MM/dd H:mm:ss` or `HH:mm:ss`, decimal point and no thousands separator. The byte digest preserves original encoded bytes and line endings; it does not hash the interpretation labels. Unsupported/truncated files refuse as a whole.

Raw source is private database evidence. API mapping details suppress source passwords/authentication flags; browser recovery retains only source filenames/digests, never base64. Reselect identical files after reload to verify a pending upload. Keep downloaded mappings, private replay and actual source outside Git/logs. Only synthetic fixtures belong in tests.

## Source → target → validation → proof

| Source / action | Target | Required review and protection | Executable evidence |
|---|---|---|---|
| Manual/opening Lancamentos | GoLiveOpening + real Transaction items | Tab/client for fiado; Safe/cheque type for cheque; exact cents, due dates, current actor, lock and active identity; no cashbox leg; append substitution | GoLiveControllerHappyPathTest, AmendHappyPathTest, ImportHappyPathTest, ChequeLifecycleGoldenTest opening variant |
| Catalogs: conta, tipo, prod, client, person, pay item | Existing authorized targets; durable mapping and primary registry destination | Current branch/active ordinary targets; explicit terminal/operator evidence; M9.5 defaults are editable suggestions; no login/membership inferred | API unhappy tests and real browser mapping |
| TblEstoque CountedSession | Ready/Finished/Migrated DailyClose + items, Draft review | Fresh box without counts/sessions, explicit operator/terminal/time window/sequence/completeness, nonoverlap/unique physical products; no invented acknowledgment/variance | HistoricalHappyPathTest, MixedHistoryTest |
| prod7/12/14 SessionField | SessionFieldEffect + revision + optional generated row | Exact session/date/custody, one slot per field; no duplicate ordinary same-role fee; zero means no generated money | MixedHistoryTest, existing field-effect regressions |
| Ordinary Lancamentos / ImportedDebt | Current classified Transaction, optional self-origin plan | Source sign/type/client/date/origin, exact money, complete plan; Terminal rows bind to exact imported session; Tab debt creates no counterpart | Import/history tests, financial golden regressions |
| Paid Lancamentos | MigratedSettlement event | Full original paid timestamp, valid chronology; no second bank receipt/payment; Tab historical paid evidence is not another FIFO credit | MixedHistoryTest plus existing settlement migration tests |
| RegistroPonto + Ponto evidence | TimeEntry/segment + evidence registry | Explicit status/operator/closed interval; current effective calendar/segment policy; preview shows source versus calculated hours and the exact policy; linked Ponto does not duplicate attendance or hours | PeopleHappyPathTest |
| BancoHoras opening/payment/adjustment | HourBankEntry | Proved anchor and separate source kind; exact target hours to four decimals when needed, signed evidence, open posting date | PeopleHappyPathTest and existing hour-bank regressions |
| RelMensal / ItemRel | EmployeePayItem | Explicit year/month/source date, current enabled category/kind; documented deduction policy, never automatic deduction/payment | PeopleHappyPathTest and validator/people regressions |
| LoteriaEspecial | SpecialDraw | Source period/contest/prize/name, exact money and current validator; no guessed fee allocation | PeopleHappyPathTest |
| Configuracao | Retained SourceLockDate / Setting.LockDate | Exact proved source date or explicitly blank, completeness; activation never relaxes an existing lock; later month lock cannot lower it | MixedHistoryTest and lock regressions |
| LogExclusao / excluded rows | Readonly source + explanation/audit | No recreated money/deletion; exact IDs/ranges, disposition and reason; unknown-history quarantine blocks | ImportUnhappyPathTest, source reader and browser tests |

Each mapping save replaces the complete resolution set under ExpectedRevision and invalidates preview. Reference mappings may supply target account/product/client/type/pay-category to row decisions; custody is never derived from an account name. Range exclusions expand to exact source identities. KnownIrrelevant/OutsideScope are reviewed omissions; UnresolvedHistory remains blocking. Excess precision is preserved and requires an explicit target value/reason. A missing year/custodian is an editable product state, never an assumed fact.

Preview writes staging/audit/idempotency metadata only. It identifies source quality exceptions, blockers, reviewed plans/exclusions and exact target fingerprint. A confirmation includes source digest, mapping/preview/tool versions and that fingerprint. Activation holds the exclusive month boundary, people key and sorted account coordination, rechecks current authority/rows/locks and commits supported history, targets, registry, audit and stored result atomically. Failed flushes leave no partial operational effect. Exact retries return the stored result; a changed request/key pairing conflicts. One activated import per branch is intentional one-time cutover, not recurring synchronization.

Migrated counted sessions remain Draft with ReviewHistoryComplete=false. The last supported count may propose the next opening, which a person must actually acknowledge. No historical count attempt, official variance, commission publication, payroll payment or deduction is fabricated. Unsupported or retired-count lifecycle remains gap AA, outside this acceptance.

## Private D4 replay

Run from this checkout, with a **new private output directory outside both the checkout and the export tree**:

```sh
dotnet run --project server/server.Cli -- access-replay /external/export/tables windows cp1252 /private/tmp/loto-replay-new
dotnet run --project server/server.Cli -- access-replay /external/mestre/tables mestre-1.5.0 utf-8-sig /private/tmp/loto-mestre-new /private/path/mapeamento-implantacao.json
```

The optional mapping is the saved envelope downloaded from Web: sourceDigest, mappingRevision and resolutions. Only explicit raw Tab→Terminal associations affect source comparison; no names or ownership are inferred. The tool uses the same bounded reader and unchanged application variance formula, without a database or Access execution. `box-days.json` contains private source components and per-box/day evidence; `aggregates.json` contains anonymized counts, tool/mapping version and source digest. It never overwrites output or source.

The Windows and Mestre read-only replays with `m10-replay-2` produced 48 and 404 box-days. Only 2 and 7 days respectively had both financial comparisons available; those matched. Missing first/prior-day counts, Terminal fiado origin, unknown Tab associations and unsupported fiado method remain explicit. These are arithmetic/evidence results, not approval to activate actual history. Synthetic replay proves the 2V positive-row discrepancy and the app's last-counted opening across a date gap. Raw excess precision stays exact; unavailable components are not zero.

## Verification and limits

Run Python reader tests with `python3 -m unittest discover -s server/tools/access_import -p 'test_*.py'`. The three real-browser cases additionally cover excess-precision resolution, stale-preview refusal and the manual setup/explicit-grant/zero-count/debt/first-start path. Backend suites use disposable PostgreSQL through the existing Testcontainers setup; use `dotnet test server/tests/Validators.Test`, `.../UseCases.Test` and `.../WebApi.Test`. Generate authoritative contracts with `pnpm --filter @loto/api export:openapi`, `generate`, and Web `generate:bff`; verify their drift/type/test/lint gates. Browser evidence is in `web/e2e/go-live.spec.ts`.

Independent technical/semantic review remains pending by owner choice. Canonical v68 and design candidate semantics are reconciled, while both accepted design markers remain v67 until that review; the doc-sync gate intentionally reports this pending reconciliation. Actual source resolution, screen-reader speech, Mobile/native and production/customer activation are not asserted. Local financial tests use known synthetic facts only.
