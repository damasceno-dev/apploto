# Badge

**Purpose.** A small rectangular label that names a state in text: an enum status (Rascunho, Enviado, Aprovado), a severity bucket, a provenance marker (**Automática**), an identity marker (**você**, **Inativa**), or a mode marker (**Gestão**, **Meu turno**). Color is a second signal; the text always carries the meaning.

## Anatomy

Text (`typography.role.badge`) inside a rectangle with `radius.badge`, padding `spacing.inset.badge-x` horizontally and minimal vertical padding; optional leading dot (`radius.dot`) only when the badge sits in dense tables where the dot aids scanning. No icons replace the text.

## Variants and tokens

| Variant | Tokens | Examples |
|---|---|---|
| Status (domain) | `dom.<group>.<value>.fg` on `dom.<group>.<value>.bg` | `dom.transaction-status.*`, `dom.daily-close-status.*`, `dom.installment-plan-status.*`, `dom.time-entry-status.*`, `dom.invitation-status.*`, `dom.mail-delivery-status.*`, `dom.operator-link-request-status.*`, `dom.review-event-type.*`, `dom.settlement-instrument.*`, `dom.monthly-financial-role.*`, `dom.overview-section-status.*` |
| Severity (aging) | `dom.aging-bucket.*` — the most severe bucket is solid | **Em dia**, **0–30 dias** … **Mais de 90 dias** |
| Provenance marker | `dom.generated-movement.automatic.fg` text, 1px `dom.generated-movement.automatic.border` outline, no fill | **Automática** beside a transaction type |
| Feedback | `sem.feedback.<kind>.fg` on `sem.feedback.<kind>.bg` | **Inativa**, **Pendente**, **você** (`sem.feedback.info.*`), self-recorded marker |
| Mode | Gestão: `sem.feedback.info.*`; Meu turno: `sem.accent.secondary-fg` on `sem.accent.secondary-bg` | top bar mode indicator |

Labels come from copy §5 maps keyed by the exact enum value; the domain token is looked up by the same key. An unknown future value renders its raw text in the neutral variant (`sem.feedback.neutral.*`) and is reported as a contract mismatch — it is never mapped to a guessed color.

## States

Badges are not interactive. When a badge sits inside a link or row, the link owns focus and hover; the badge keeps its colors.

## Behavior

- Status and provenance are separate: a generated movement shows **Automática** beside its type *and* its own status badge.
- A cancelled row's badge stays legible (`dom.transaction-status.Cancelled`); strike-through, if used on the row's value, is supplementary.
- The badge never truncates; long labels wrap inside table cells.

## Accessibility

Rendered as text, so screen readers read the label in place. Where a column header already says **Situação**, nothing extra is needed; where a badge appears without context (a card), it is preceded by visually hidden context (**Situação: Enviado**). Color is never the only cue.

## Touch and scale

Grows with text; minimum height follows `typography.role.badge` line height. Not a touch target.

## Copy

Exactly the copy §5 labels and §7 markers; never uppercase badges except the mode marker, which uses `typography.role.overline`.
