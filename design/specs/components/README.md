# Lotero component contract

> **Status:** Design M3, authored 2026-10-20 (D04); independent review pending. Web (Web M3) and mobile (Mobile M3) each implement these components in their own code — same anatomy, states, token mapping and accessibility, platform-appropriate interaction. Nothing here is runtime code.

A component spec answers four questions for both apps: what the parts are, which tokens paint them in each theme, how every state looks and behaves, and how keyboard, touch, screen-reader and enlarged-text users reach the same result. Semantics stay where they are authored: the [screen catalog](../../docs/screens.md) owns what each screen does and its [cross-screen patterns](../../docs/screens.md#cross-screen-patterns) own recovery, errors and concurrency; [copy](../copy-guidelines.md) owns words and [formatting](../formatting-ptbr.md) owns values. A component spec refers to those authorities instead of restating them, so there is one rule in one place.

## Index

| Component | Spec | Used by (examples) |
|---|---|---|
| Button (and text link) | [button.md](button.md) | every action; Cockpit next action (CTA) |
| FormField | [form-field.md](form-field.md) | every labelled control; error association |
| Input (text, textarea, password, search) | [input.md](input.md) | Login, Notes, reasons, Clients search |
| MoneyInput | [money-input.md](money-input.md) | Close day counts, Create value, installment rows |
| Select (and searchable select) | [select.md](select.md) | type, account, client, operator pickers |
| DatePicker (date and time) | [date-picker.md](date-picker.md) | filters, business date, TransactionTime, segments |
| Checkbox, Radio and Switch | [choice-controls.md](choice-controls.md) | Somente minhas, payment method, HandsOutCountedStock |
| Badge (status, domain, marker) | [badge.md](badge.md) | every enum status; Automática; você |
| Card | [card.md](card.md) | Cockpit next action, setup checklist, notices of state |
| SummaryStrip | [summary-strip.md](summary-strip.md) | variance preview totals, report aggregates |
| DetailList | [detail-list.md](detail-list.md) | audit identities, close header, read-only facts |
| Table / List | [table.md](table.md) | ledgers, queues, reports, admin lists |
| FilterBar | [filter-bar.md](filter-bar.md) | Transactions, reports, time entries |
| Pagination | [pagination.md](pagination.md) | every paged endpoint |
| Modal / Sheet (dialog, confirm, drawer, bottom sheet) | [modal-sheet.md](modal-sheet.md) | confirmations, client dialog, navigation drawer |
| Menu | [menu.md](menu.md) | Session menu, row overflow actions |
| Tabs (and segmented control) | [tabs.md](tabs.md) | Fiado balance debt/credit; theme choice |
| Disclosure | [disclosure.md](disclosure.md) | Ajuda sobre diferença de caixa; Ver o que mudou |
| Stepper | [stepper.md](stepper.md) | Plataforma wizard; Add/Offboard employee |
| Notice (and error summary) | [notice.md](notice.md) | errors, uncertain results, backlog banner, setup |
| Toast | [toast.md](toast.md) | confirmed success of navigational actions |
| SaveStatus | [save-status.md](save-status.md) | Close day autosave and explicit Save |
| Skeleton | [skeleton.md](skeleton.md) | every initial load |
| EmptyState | [empty-state.md](empty-state.md) | no records, no matches, all-clear, setup-needed |
| PageHeader (breadcrumb and return) | [page-header.md](page-header.md) | every screen |

The first fifteen are the planned M3 set; SummaryStrip, DetailList, FilterBar, Pagination, Menu, Tabs, Disclosure, Stepper, Notice and SaveStatus were added because a cataloged journey needs each one and none of the planned components can carry it without blurring its job.

## How a spec is written

Each file follows the same order: **Purpose** (one paragraph, with the catalog uses), **Anatomy**, **Variants**, **Tokens**, **States**, **Behavior**, **Accessibility**, **Touch and scale**, **Web and native**, and **Copy**. A section that does not apply is omitted rather than filled.

### Token notation

Specs name tokens by path, in backticks, with two shorthands for colors:

- `sem.<path>` means `color.semantic.<theme>.<path>` in **both** themes — the light and dark values come from the [token tables](../../tokens/README.md), never from the spec.
- `dom.<group>.<value>.<part>` means `color.domain.<theme>.<group>.<value>.<part>`; `dom.<group>.*` means every value of that group.
- Other categories use their full path: `typography.role.table`, `spacing.inset.panel`, `spacing.size.control-height`, `radius.control`, `shadow.floating` (the theme's recipe), `motion.transition.control`, `layer.popover`, `breakpoint.medium`.

Specs never name a primitive or a raw color. `node scripts/check-design-docs.mjs` fails on a token path that does not exist in both themes, on a primitive reference and on a hex literal in these specs and the screen blueprints; `node scripts/check-design-docs.mjs --scope components` runs the same checks on the tokens and component specs alone.

## Shared state vocabulary

Five states are easy to confuse and must look and behave differently everywhere:

| State | Meaning | Presentation | Interaction |
|---|---|---|---|
| **Disabled** | The action exists but cannot run now (clean form, missing prerequisite, permission withheld) | `sem.action.disabled.*`; label stays readable; the **reason** is adjacent visible text, not a tooltip | Not focusable as an action on web when truly inert; native announces "indisponível" with the reason as hint |
| **Read-only** | The value is real and final for this caller (locked period, cancelled row, generated movement, other operator's row) | Value as plain selectable text on `sem.surface.muted` (fields) or as a DetailList; no control chrome; a reason line names why | Text selection and copy work; focusable only as text; never looks like a disabled input |
| **Loading** | A read or write is in flight | Skeleton for first load; inline spinner + "…ndo" label for a write (**Salvando…**); region `aria-busy` | Duplicate activation blocked; previously entered input kept |
| **Error** | A confirmed failure | `sem.feedback.danger.*` with the verbatim `ErrorMessages` list | Input kept; recovery action offered; focus moves to the summary on submit |
| **Success** | The server confirmed the outcome | `sem.feedback.success.*` text or a Toast, with real confirmed values (save time from the response) | Never shown for a local draft, preview or dispatched request |

**Uncertain** is a sixth state for writes: the request was dispatched and no definitive answer arrived (timeout, lost response, any 5xx). It uses `sem.feedback.warning.*`, the title **Resultado não confirmado**, and the recovery the catalog prescribes for that operation — never success, never a fresh financial create. Components expose the states; the screen decides which applies, following the cross-screen patterns.

## Cross-cutting contracts components must honor

- **Errors.** Render every `ResponseErrorJson.ErrorMessages` string verbatim, in order, as a list — no prefix, rewording, merge or truncation. A message goes beside a field only when its association is unambiguous; otherwise it belongs in the form's error summary ([Notice](notice.md), error-summary variant), which links to the fields it can name. No field-error dictionary exists in the API.
- **Uncertain writes and retries.** Financial creates retry the same frozen payload and `Idempotency-Key`; guarded edits reload and reconcile on a stale 409; nothing financial replays automatically after session recovery. Buttons and dialogs only expose the actions the screen's catalog entry allows for that state.
- **Session recovery.** A lost session never discards input: forms keep their values in memory and the app's device store, and the catalog's re-login rules decide when they come back. Components show the app-level session notice; they never resubmit.
- **Provenance and status stay apart.** A generated movement shows its **Automática** marker beside the type and its lifecycle status in the status column; neither substitutes for the other.
- **Rows and nested actions.** A whole row may be the pointer target for its record, but it always also contains an explicit link with an accessible name, and nested controls never trigger the row ([Table](table.md)).
- **Origin and scope.** Filters, page and selection survive a drill-down and return; the return destination is the catalog's semantic origin ([PageHeader](page-header.md), [FilterBar](filter-bar.md)).
- **Focus.** Every focusable element shows `sem.focus.ring` (`spacing.size.focus-ring-width` outline at `spacing.size.focus-ring-offset`), or `sem.focus.ring-on-nav` inside navigation. Focus is never removed without a visible replacement.
- **Motion.** Use `motion.transition.*`; when the platform's reduce-motion preference is on, use the matching `motion.reduced.*` entry. Nothing essential depends on an animation.
- **Text scaling.** Components grow with text: heights are minimums, labels wrap rather than truncate (except identifiers with a full-text alternative), and touch targets never drop below `spacing.size.touch-target-min`.
- **Money and dates.** Values come pre-formatted through `@loto/core` per [formatting](../formatting-ptbr.md): money right-aligned in `typography.role.money` or `typography.role.table` with tabular figures, the `R$` kept with its digits, signs and units always visible.

## Verification boundary

These specs are written contracts. Web M3 and Mobile M3 prove them in running code — the web `/styleguide` and the mobile in-app gallery render every component state in both themes — and each feature milestone verifies keyboard-only, screen-reader, touch, 375px and enlarged-text journeys against the real API. Nothing in this folder claims that verification.
