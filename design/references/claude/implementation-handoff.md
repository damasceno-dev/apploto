# Claude direction — implementation handoff

**Design M1 / D03 · selected by the owner on 2026-10-20.** Read [the decision](../../docs/decisions.md), then use this handoff to build the direction without reopening visual exploration. [Screenshots and coverage](selected-2026-10-20/README.md), [browser measurements](selected-2026-10-20/browser-observations.json), [observed styles](selected-2026-10-20/style-inventory.json) and [provenance](selected-2026-10-20/manifest.json) make the inputs inspectable.

This is visual input to M2–M4. It does not approve prototype behavior or replace the catalog. All 33 screen CSS files are comment-only; the prototype's shared stylesheet and inline layout rules produce its appearance. No prototype HTML, CSS, JS, fake database or calculation code is vendored here. The captured screens use fictional prototype data, not the real API.

## 1. Character and hierarchy

The manager should immediately find exceptions, compare aligned figures and open the relevant record. The operator should see one clear next task and still find today's count. A strong blue frame identifies the workspace; neutral content surfaces leave attention on work. Orange is reserved for a scarce action. Dense tables carry detail; whitespace and thin rules separate sections without making every datum a large card.

Keep that hierarchy as content changes. More API rows require meaningful pagination or scrolling, not tiny type. Missing data requires an honest state, not invented activity. Current pt-BR labels and permissions override old seeded controls and text. The temporary orange “L” is a placeholder, not an approved final logo; no official CAIXA wordmark or clover is adopted.

## 2. Color inputs

These observed anchors come from `assets/base.css`. M2 preserves their character, derives ramps and encodes semantic references; these are not final contrast-approved runtime tokens.

| Role | Light | Dark |
|---|---|---|
| Page background | `#f1f5f7` | `#0a1520` |
| Main surface | `#ffffff` | `#0f1d2a` |
| Secondary surface / table header | `#f4f8f9` | `#132433` |
| Tertiary / read-only field | `#e9f0f2` | `#182a3a` |
| Main text | `#0f2230` | `#e4edf2` |
| Secondary text | `#33495a` | `#b6c6d0` |
| Muted text | `#5b7180` | `#8699a7` |
| Main divider | `#d3e0e4` | `#22384a` |
| Hairline | `#e4ecef` | `#1a2e3e` |
| Field border, needs stronger contrast | `#bfd0d6` | `#2c4458` |
| Sidebar | `#004b8b` | `#082440` |
| Top bar | `#ffffff` | `#0c1924` |
| Primary action, white text | `#005ca9` | `#0075bf` |
| Primary hover, dark pair needs adjustment | `#004b8b` | `#0a86d6` |
| Link / accent | `#005ca9` | `#58ade8` |
| Operator CTA | `#f39200` | `#f39200` |
| CTA ink / hover | `#1b1409` / `#ffa21f` | Same |
| Secondary accent | `#2a8576` | `#6cc9b9` |
| Success | `#16734a` | `#52c48d` |
| Warning | `#955700` | `#f2b347` |
| Danger | `#bb3528` | `#ff7d70` |
| Info | `#0067ad` | `#63b8f0` |

Blue anchors ordinary actions and structure. Orange belongs to the main operator next action and a small active-navigation marker. Turquoise is sparing secondary context, including Meu turno. Small rectangular badges use subtle semantic backgrounds, not large tinted cards. The inventory records alpha fills, navigation overlays, selection/focus and complete 74-property sets. The [Caixa reference](../caixa-palette.md) remains inspiration only.

### Contrast screening

A limited sRGB calculation covered 12 named pairs per theme, compositing alpha text over its declared solid background. These four pairs need adaptation:

| Pair | Observed ratio | Screening target | Response |
|---|---|---|---|
| Light sidebar muted labels | 3.955:1 | 4.5:1, small text | Increase label contrast |
| Dark primary hover, white text | 3.893:1 | 4.5:1, normal text | Adjust hover fill or foreground |
| Light field boundary on surface | 1.590:1 | 3:1 where boundary identifies the control | Strengthen essential control indicator |
| Dark field boundary on surface | 1.688:1 | Same | Same |

This is not a complete WCAG audit. M2 validates every actual text/background and domain-badge pair, including interaction states; apps verify focus, reflow, essential control boundaries and assistive behavior. Decorative hairlines have a different purpose from a field's essential boundary.

## 3. Typography and text scaling

Use a system-first sans-serif: Apple system / Segoe UI / Roboto / Helvetica Neue / Arial / sans-serif. Reserve mono for small codes and identifiers. Money and comparable counts use tabular lining numerals, right alignment and clear signs. Do not make every entrada green as though it were business revenue, or every saída red as though it were an expense.

| Authored role | Size | Treatment |
|---|---|---|
| Body / ordinary control | 14px, line-height 1.5 | 400 body; 550–650 control |
| Table body | 13.5px | Regular, tabular figures |
| Helper / small text | 12.5px | 400–600 |
| Badge / overline | 11.5px; some nav labels 10.5px | 600–700, modest tracking |
| Table header | 11px | 650, uppercase, 0.05em tracking |
| Section heading | 15px | 650 |
| Summary value | 20px | 650 |
| Next-action heading | 22px | 650 |
| Page heading | 24px, line-height 1.2 | 650, −0.01em tracking |

**Desktop captures normally use the prototype's 150% CSS zoom.** Authored 14px body therefore occupies roughly 21px before screenshot scaling; the 252px sidebar occupies about 378px. The 100% queue comparison shows the difference. At a 375px viewport the script clamps applied zoom to 100%, although its control still reads 150%.

M2 must curate a readable role scale with the chosen appearance in view. Copying 14px at 100% does not reproduce the selected desktop's reading size. Global CSS zoom, runtime media-query rewriting and suppressing preferred enlargement on phones are not the production recipe. Use rem/platform text metrics and real reflow. Validate 100%/150%/200% web text and native font scaling in the apps. Final role-size calibration is M2 work within this direction, not another tool comparison.

## 4. Spacing, geometry and elevation

| Observed input | Authored value | Meaning |
|---|---|---|
| Spacing steps | 4, 8, 12, 16, 24, 32px | Start with this 4px rhythm |
| Page padding | 28px top, 32px sides; narrow 16px | Modest margins, clear sections |
| Content maximum | 1240px; narrow form 760px | Avoid excessively long content lines |
| Sidebar / top bar | 252px / 58px | Starting proportions, adapt to text scale |
| Section separation | 28px | Heading rule then content |
| Corners | 4px panels/controls; 3px badges | Restrained; avatars/dots may be circles |
| Table cells | 10px vertical, 12px horizontal; outer 16px | Dense scanning with visible separation |
| Form columns | 210px minimum, 16px gap | Stack when available width is insufficient |
| Button/input | 36px minimum height; small button 28px | Observation, **not** accepted mobile touch sizing |
| Dialog | Up to 540px, viewport minus 32px | Divided header/body/footer |
| Session menu | Up to 320px | Clamp and retain reachable actions |

Use elevation sparingly: light surface `0 1px 2px rgba(15,34,48,.05)`; light floating panel `0 14px 36px rgba(15,34,48,.18)`; dark floating panel `0 16px 40px rgba(0,0,0,.5)`. Prefer rules to raised card piles. The prototype's 110px bottom page padding reserves room for its statebar; remove that prototype accommodation from the app.

## 5. Shell and responsive adaptation

Desktop uses a sidebar plus `minmax(0,1fr)` workspace. The sidebar contains the mark and grouped labelled destinations; current route gets a subtle fill and orange left marker. Icons are simple 16px outlines, about 1.6px stroke. Use a consistent icon set with labels; no shared runtime UI package is implied.

The sticky top bar holds branch/date, theme choice and avatar/session menu. On narrow web it shows Menu, compact title, theme icons and avatar; branch context moves into the drawer. Gestão ↔ Meu turno belongs in the avatar menu. The catalog controls roles, modes, routes, unavailable explanations and return behavior; a screenshot is not an authorization rule.

Authored breakpoints: **860px** for drawer/compact shell and **560px** for stacked details/fields. The prototype multiplies them by zoom, making the first threshold effectively 1290px at 150%. M2/M4 must choose content-aware breakpoints with text scaling, not copy that rewriting algorithm. The narrow drawer is about 86vw, capped at 290 authored px, over a dim backdrop. Implement reliable close/Escape, focus containment/return and inert background. Responsive web is not native navigation acceptance; M4 adapts platform interaction with the same semantics.

**Cockpit defect:** at 375px, a 328px-wide grid keeps `84px 220px` columns, squeezing the main task into a text tower. Stack next action, other terminals and supporting work vertically before columns become cramped. Preserve order and a usable main action. Some desktop headers compete for space at 150%; wrap or compact context rather than conceal branch/session identity behind adjacent controls.

Dense tables scroll within their own labelled region. The narrow close table measured 326px available against 648px of content. Keep opening values and meaningful headers; do not drop columns or shrink everything to fit. Surrounding actions/explanations must reflow within the page. Test root overflow, long pt-BR names/errors and keyboard access to scrolling.

## 6. Component anatomy for M3

| Component | Retain | Implement and verify |
|---|---|---|
| PageHeader / breadcrumb | Origin above strong heading; supporting context; nearby actions | Restore actual source filters and allow wrapping |
| Button | Blue primary, neutral outline, text action, outlined danger; sparse orange CTA | Loading/disabled/focus, real label, enlarged touch area |
| FormField / Input / Select | Visible label, restrained border, helper and relevant error | Input modes, real validation, error association, passing boundary contrast |
| MoneyInput | Right-aligned tabular value in normal field geometry | Shared pt-BR formatting, clear units, correct untouched/zero behavior |
| Table / List | Small muted headers, thin rows, aligned figures, row hover | Whole-row destination with keyboard link; nested actions stay independent; no date-only edit affordance |
| FilterBar / pagination | Grouped controls before results, active-scope summary | Values match request/plan/type scope and rows; return restores context |
| Badge | Small 3px rectangle, text, subtle fill | Domain tokens for actual enums; color is supplementary |
| Generated marker | Separate outlined “Automática” | Origin separate from status; actual DTO source links |
| Summary strip | Shared surface, thin separators, label then figure | Each figure keeps its period/scope; unavailable is not zero |
| Notice / error / empty | Neutral surface, colored left rule, clear action | Real error arrays, retry and input recovery; no gap jargon |
| Skeleton | Content-shaped bars, restrained shimmer | Reduced motion, meaningful loading announcement, stable layout |
| Modal / Sheet | Header, labelled body, divided action footer, backdrop | Dialog name, focus/Escape/return, phone keyboard and internal scrolling |
| Toast / saved feedback | Small status line or in-flow success notice | Real save time, pending/error, no success before server response |
| Date / time picker | Compact popover, simple arrows, active date | Locale/keyboard/holiday rules and native-platform picker adaptation |
| Read-only details | Label/value pairs and visible restriction reason | Selectable text; actual role/lock/status/recorder rules decide actions |

Observed status tones: Draft neutral; Submitted amber; Approved/Active green; Rejected red; Cancelled muted; aging increasingly urgent. M2 reconciles **every current enum value** with canonical domain rules instead of assuming the old map is complete. Header and body numeric alignment must agree; verify computed styles rather than assume a utility class wins specificity.

## 7. Screen families for M4

| Family | Screens | Layout |
|---|---|---|
| Identity / branch gate | Login, Branch picker | Blue brand strip and centered sheet; clearly actionable whole branch option |
| Queue / review | Work Queue, Close approval | Exception groups, counts, audit identities, movements, approve/reject actions |
| Operator day | Cockpit, Open day, Close day, Fix & resubmit | One next action, current close discoverable, count table, events, honest save feedback |
| Entry / correction | Transaction Create/Edit, installment plan | Context, fixed facts versus editable fields, scoped preview/confirmation |
| Financial history | Transactions, statement, ledger | Filters, dated totals, scoped table, whole-record destination and return |
| Reports / month end | Fiado balance/aging, cheques, variance, reconciliation | Summary strips and drill-down tables; actionable blockers, separate lock confirmation |
| Time / personal work | Clock, time management, both self summaries | Current state first, segments/statuses, period totals; proper management edit form |
| Administration | The nine configuration screens | Main action, labelled search/filter, dense list, restrained dialog/section editor |

The index links all 33 screens. These are visual examples, not accepted feature implementations. Auth/account/Plataforma additions still need catalog work. Dashboard inherits the direction but still needs its real reporting contract and blueprint; do not invent charts, profit or financial totals to fill space.

## 8. Behavior the screenshots must not redefine

The current [catalog](../../docs/screens.md), [copy](../../specs/copy-guidelines.md), [formatting](../../specs/formatting-ptbr.md) and actual API govern the app:

- Variance/FIFO/authorization/atomicity stay server-owned. Call the candidate-preview endpoint; the prototype's local equation is not production logic. Show the actual-value equation distinctly from official variance; put the general formula/explanation in collapsed help. Never show an expected physical count to copy.
- Retain dirty/saving/saved/error feedback, disabled saved Save, real save date/time, repeat undo and input recovery. No save merely because a screen rendered. Autosave is limited to today's plain Draft. This inspection changed no counts and does not certify save timing.
- Distinguish rejection and opening-correction events, with actual dates/authors/before-after data and honest missing-history fallbacks. Never invent audit identities/timestamps.
- Fiado uses real branch-wide debt/credit and agreed positive credit presentation. Do not restore a raw paired-Tab reference in the close. Cash repayment and known counted-stock delivery have server-generated physical movements; nonphysical repayment has no drawer movement. Never add Tab balance to variance or infer unanswered bolão rules from fixture descriptions.
- Generated provenance is separate from status. The prototype's field names differ from current DTO fields: map actual `generationKind` and source/linked identifiers. Edit through the actual source under its restrictions.
- A cheque-plan editor does not authorize changes to immutable amounts/accounts/direction or atomic operations the API does not expose. Follow signed per-row edits and atomic finalization contracts.
- Branch sessions, Member scopes, role versus mode, locks and concurrency remain real API concerns. Remove demo login, statebar, source/gap badges, fake clock/database and prototype tools. Old “pending contract” text is not current evidence of a server gap.

The October 19 real capture bundles remain external study evidence, not app fixtures. Their recording-Member preview was verified in the preceding study; their dates remain historical. Prepare a current-day scenario when testing same-day editing in a real app. This visual inspection did not recapture or mutate the backend.

## 9. Motion, layers and preferences

Observed transitions: **120ms** controls, **180ms ease** drawer, **1.2s linear** skeleton shimmer. Theme changes swap without a long cross-fade. Reduced motion suppresses animations/transitions. M2 encodes intent; each platform uses its own animation facilities.

Observed z-indexes: sticky top bar **30**, drawer backdrop **40**, drawer **50**, session menu **60**, date/time popover **90**. Statebar also uses 60 but is absent from the app. Native modal dialogs use the browser top layer. M2/M3 define an intentional layer model, not copied collisions.

Theme is a device preference: light-first, explicit light/dark/system, applied before first paint where possible and synchronized across web tabs. Sign-out/data reset should not silently change appearance. A theme preference is not a precedent for storing session credentials in localStorage. Text enlargement must reflect what is actually applied.

## 10. Implementation sequence and evidence limits

**Design M2 Phases 1–4** now have an owner-selected input: create the planned token files, resolver/contrast gate and complete category/domain tables, correcting the findings above. No M2 criteria are ticked by this extraction. **M3** specifies components; **M4** supplies shell and per-feature layout/state blueprints just before matching Web/Mobile milestones. Scaffolds and plumbing retain their own Shared/cross-app prerequisites.

For the first real app slice, compare Work Queue, recording-Member count and Transactions with these references using a local API and fictional data. Check both themes, enlarged text, 375px reflow, table scrolling, touch targets, keyboard focus/return, real states, fields/filters and origin return. Functional acceptance requires actual API calls and permissions, not screenshots.

Evidence: unchanged local source; all 33 seeded screen paths; named theme/responsive/focus/dialog captures; loading/empty/error simulations; limited contrast screening; no error-level entries in the inspected browser console. The new handoff has not undergone independent review. No native-device test, full assistive audit, financial-flow acceptance, current-day API recapture or production operation was performed. All interactions used an isolated browser origin; source and financial data remained unchanged.
