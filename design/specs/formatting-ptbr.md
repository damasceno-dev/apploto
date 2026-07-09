# Lotero — pt-BR Display Formatting (contract)

> **Status:** Draft (Design M0, Phase 2 — item 0.2.1)
> **What this is:** the display-format contract for every value the UI renders. `shared/core` (Shared M3) **implements** these rules as pure-TS helpers; web and mobile render through those helpers, never ad-hoc. M1's generator-tool prompts **cite** this file so prototypes show correctly formatted values.
> **Domain reference:** `docs/loto-specs.md` §3 (column types), §6.12 (variance semantics). Wire shapes come from the OpenAPI contract; this file governs **display only**.

## 1. Locale ground rules

- Locale is **pt-BR, always** — there is no locale switch. Decimal separator is the **comma**, thousands separator is the **dot**.
- Formatting is deterministic and identical on web and mobile. `shared/core` decides per formatter whether to use `Intl` or hand-rolled code (Hermes/Expo `Intl` support permitting) — the *output* defined here is the contract either way.
- Backend data (names of categories, transaction types, products, accounts, clients) renders **verbatim** — never recased, trimmed, or translated (see `copy-guidelines.md`).
- Money, dates, and times in tables use **tabular lining numerals** (M2 typography token) so columns align.

## 2. Money (BRL)

Wire format is JSON `number` mirroring `numeric(14,2)` — up to 12 integer digits, exactly 2 decimals. Client code treats money as **display-only**; where arithmetic is unavoidable it goes through `shared/core` integer-cents helpers (`parseBrl` → cents, `formatBrl` ← cents or wire number). No float arithmetic on money, ever.

**Display:** `R$ 1.234,56`

| Rule     | Detail                                                                                                                                                                                  |
|----------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Symbol   | `R$` prefix, separated from the digits by a **non-breaking space** (U+00A0) so the symbol never wraps alone                                                                             |
| Decimals | Always exactly 2, even for whole values: `R$ 5,00`, never `R$ 5`                                                                                                                        |
| Grouping | Dot every 3 integer digits: `R$ 1.234.567,89`                                                                                                                                           |
| Sign     | **Unsigned by default.** `Transaction.Value` is always positive; direction is a separate field (`Direction`), communicated by labels/columns/color — never by a minus sign on the value |
| Zero     | `R$ 0,00`                                                                                                                                                                               |

**Input (MoneyInput):** the mask accepts digits only and builds the value right-to-left in cents (typing `1234` shows `R$ 12,34`). Paste tolerates `R$`, dots, spaces; the parsed result is integer cents. Submitted wire value is the plain number (`12.34`).

## 3. Signed money — variance and impact deltas

Some values are **signed by nature** and carry their sign explicitly. These are the exceptions to the unsigned-by-default rule:

- Cash variance (Diferença de Caixa): `DailyCloseItem` variance row, dashboard `TotalVariance` / `MeanVariance` / per-close `VarianceValue`, monthly-reconciliation `NetVariance`.
- Impact-preview deltas (§6.14): `CashVarianceImpact.VarianceDelta`, `CurrentVariance` / `ProjectedVariance`, fiado balance deltas.
- Time-entry hour balances (see §5, signed durations).

**Display:** `+R$ 12,00` · `−R$ 12,00` · `R$ 0,00`

| Rule          | Detail                                                                                                          |
|---------------|-----------------------------------------------------------------------------------------------------------------|
| Sign position | Before the symbol: `+R$ 12,00`, not `R$ +12,00`                                                                 |
| Plus          | Explicit `+` on every positive value in a signed context                                                        |
| Minus         | True **minus sign U+2212** (`−`), never the hyphen-minus (`-`) — it matches the digit width and reads correctly |
| Zero          | No sign: `R$ 0,00`                                                                                              |

**Semantics (pinned by spec v32, §6.12):** negative variance = **falta** (drawer short — the worked example `−R$ 20,00` means the drawer is R$ 20 short); positive = **sobra** (surplus). Copy and color treatments build on this; the sign itself is always shown — color is never the only signal.

## 4. Dates, datetimes, times

The backend carries time in three different shapes, and each shape gets its own display rule — collapsing them into one rule is how an audit timestamp ends up three hours off on screen:

| Wire semantics                                                        | Example fields                                                                 | Display rule                                                                    |
|-----------------------------------------------------------------------|---------------------------------------------------------------------------------|----------------------------------------------------------------------------------|
| **Date-only, branch-local** (`date`)                                  | `Transaction.Date`, `DueDate`, `DailyClose.Date`, `LockDate`, report dates     | Render exactly as returned — never shift by timezone                            |
| **Wall-clock, branch-local** (`timestamp` without time zone / `time`) | `TimeEntrySegment.ClockIn` / `ClockOut`, `TransactionTime`                     | Render exactly as returned — it already reads as the clock on the branch's wall |
| **UTC instant** (`timestamptz`)                                       | `CreatedAt`, `UpdatedAt`, `SubmittedAt`, `ApprovedAt`, `CancelledAt`, `PaidAt` | **Convert to the device timezone**, then format                                 |

The device-timezone choice for UTC instants is deliberate: the API contract exposes no branch timezone, and in practice operators and managers are physically at the branch, so the device zone and the branch zone agree. A manager reviewing remotely from another timezone sees audit times in their own zone — acceptable for now. If that ever becomes a problem, the fix is exposing the branch timezone on the contract; raise it as a backend gap rather than guessing client-side.

Once the value is resolved, these are the display formats:

| Kind                  | Display                 | Example                      | Notes                                                   |
|-----------------------|-------------------------|------------------------------|---------------------------------------------------------|
| Date                  | `dd/MM/yyyy`            | `08/07/2026`                 | Zero-padded, always 4-digit year                        |
| Datetime              | `dd/MM/yyyy HH:mm`      | `08/07/2026 14:30`           | 24-hour clock, no AM/PM; seconds are not shown          |
| Time                  | `HH:mm`                 | `09:05`                      | `TransactionTime`, clock in/out                         |
| Long date *(headers)* | `d 'de' MMMM 'de' yyyy` | `8 de julho de 2026`         | Month name lowercase                                    |
| Weekday               | lowercase, hyphenated   | `terça-feira` · abbr. `ter.` | pt-BR convention: weekday and month names are lowercase |

Wire formats stay ISO (`yyyy-MM-dd`, ISO 8601 datetimes) — display formatting is applied at render time only. Date inputs display and accept the `dd/MM/yyyy` mask; the submitted value is ISO.

## 5. Durations (time entry)

Time-entry endpoints return **decimal hours** on the wire — `TotalHours` / `BalanceHours` on the balance summary, and `Setting.DailyTargetHours = 7.33` means 7h20m, *not* 7h33m. Worked totals and banco-de-horas balances can also exceed 24h, so these are durations, never clock times. The formatter therefore converts first, then renders:

**Conversion:** `minutes = round(|decimalHours| × 60)` — to the nearest whole minute, halves away from zero, sign reapplied afterwards.

- **Unsigned duration:** `8h 30min` · `173h 05min` — minutes always 2 digits, `0h 45min` for sub-hour values.
- **Signed balance (banco de horas):** `+2h 15min` / `−2h 15min`, same sign rules as §3 (explicit `+`, minus U+2212, zero unsigned: `0h 00min`).
- **Pinned examples:** `7.33` → `7h 20min` (439.8 → 440) · `−1.5` → `−1h 30min` · `0.25` → `0h 15min` · `0` → `0h 00min`.

The conversion lives in a single `shared/core` helper, so web and mobile can never disagree by a minute.

## 6. Identity & contact masks

Backends store CPF as **normalized digits** (spec v6); other fields are stored as entered. The UI formats for display and strips punctuation before submit where the contract expects digits.

| Field | Display mask                         | Example                              | Stored as           | Rule                                                                          |
|-------|--------------------------------------|--------------------------------------|---------------------|-------------------------------------------------------------------------------|
| CPF   | `000.000.000-00`                     | `123.456.789-09`                     | 11 digits           | Format when exactly 11 digits; input mask auto-punctuates, submit digits-only |
| CNPJ  | `00.000.000/0000-00`                 | `12.345.678/0001-95`                 | ≤ 18 chars          | Format when 14 digits are parseable; otherwise render verbatim                |
| Phone | `(00) 00000-0000` / `(00) 0000-0000` | `(61) 91234-5678` · `(61) 3123-4567` | ≤ 20 chars freeform | 11 parseable digits → mobile mask, 10 → landline mask, anything else verbatim |
| CEP   | `00000-000`                          | `70040-010`                          | ≤ 9 chars           | Format when 8 digits are parseable; otherwise verbatim                        |

**Defensive rule:** masks are presentational. A value that doesn't match the expected digit count renders verbatim — never truncated or padded to force a mask.

## 7. Plain numbers, counts, percentages

- **Integer counts** (pending approvals, draft counts, row counts): dot-grouped — `3`, `42`, `1.234`.
- **Decimals:** comma separator — `1,5`.
- **Percentages:** comma decimal, no space before the symbol — `12,5%`.
- **Installment positions:** `3/12` (position/total, no spaces) — matches the persisted `CH PRE (3/12)` row-description convention (§6.3).

## 8. Empty and absent values

- Absent/null value in a table or detail view → **em dash `—`**, never an empty cell, `null`, `0`, or `R$ 0,00` (zero is a meaningful value, distinct from missing).
- `PaidAt = null` means **unpaid** — render the domain state (see `copy-guidelines.md`), not a dash, where the screen calls for it.
- Empty *collections* are an empty-state concern (`copy-guidelines.md` / M3 EmptyState), not a formatting one.

## 9. Implementation notes (for Shared M3)

- One helper per row of this contract; helpers cite this file. The spec is the contract, the helpers are the implementation — a change here is a breaking change there.
- Every formatter and parser gets unit tests against the examples in this file, including the edge cases: sign of zero, 12-digit money, non-parseable phone/CNPJ passthrough, sub-hour durations, and the decimal-hour rounding (`7.33` → `7h 20min`).
- The U+00A0 (money symbol space) and U+2212 (display minus) code points are part of the contract — tests must assert them explicitly, not via visually identical ASCII.
