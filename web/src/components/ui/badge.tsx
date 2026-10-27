import type { CSSProperties, ReactNode } from 'react';
import {
  AgingBucket,
  DailyCloseReviewEventType,
  DailyCloseStatus,
  Direction,
  InstallmentPlanStatus,
  InvitationStatus,
  MailDeliveryStatus,
  MonthlyFinancialRole,
  OperatorLinkRequestStatus,
  OverviewSectionStatus,
  SettlementInstrument,
  TimeEntryStatus,
  TransactionStatus,
} from '@loto/api';
import {
  agingBucketLabels,
  dailyCloseReviewEventTypeLabels,
  dailyCloseStatusLabels,
  directionLabels,
  enumLabel,
  installmentPlanStatusLabels,
  invitationStatusLabels,
  mailDeliveryStatusLabels,
  monthlyFinancialRoleLabels,
  operatorLinkRequestStatusLabels,
  overviewSectionStatusLabels,
  settlementInstrumentLabels,
  timeEntryStatusLabels,
  transactionStatusLabels,
} from '@loto/core';
import { cn } from '@/lib/cn';
import { domainColors } from '@/theme/tokens.generated';
import { VisuallyHidden } from './visually-hidden';

// Badge (design/specs/components/badge.md): a small rectangle that names a state in text; color is
// a second signal. Domain colors come from the generated token names, looked up by the exact enum
// string the API returned. The pt-BR label is copy §5's, from `@loto/core`'s maps by default.

/** The backend enum behind each domain color group (OpenAPI enums, generated in `@loto/api`). */
export const domainEnums = {
  'transaction-status': TransactionStatus,
  'daily-close-status': DailyCloseStatus,
  direction: Direction,
  'aging-bucket': AgingBucket,
  'time-entry-status': TimeEntryStatus,
  'installment-plan-status': InstallmentPlanStatus,
  'invitation-status': InvitationStatus,
  'mail-delivery-status': MailDeliveryStatus,
  'operator-link-request-status': OperatorLinkRequestStatus,
  'review-event-type': DailyCloseReviewEventType,
  'settlement-instrument': SettlementInstrument,
  'monthly-financial-role': MonthlyFinancialRole,
  'overview-section-status': OverviewSectionStatus,
} as const;

export type EnumDomain = keyof typeof domainEnums;

/** copy §5 labels per domain (`@loto/core`); keyed by the same generated enums as the colors. */
export const domainLabels: {
  readonly [D in EnumDomain]: Readonly<Record<DomainValue<D> & string, string>>;
} = {
  'transaction-status': transactionStatusLabels,
  'daily-close-status': dailyCloseStatusLabels,
  direction: directionLabels,
  'aging-bucket': agingBucketLabels,
  'time-entry-status': timeEntryStatusLabels,
  'installment-plan-status': installmentPlanStatusLabels,
  'invitation-status': invitationStatusLabels,
  'mail-delivery-status': mailDeliveryStatusLabels,
  'operator-link-request-status': operatorLinkRequestStatusLabels,
  'review-event-type': dailyCloseReviewEventTypeLabels,
  'settlement-instrument': settlementInstrumentLabels,
  'monthly-financial-role': monthlyFinancialRoleLabels,
  'overview-section-status': overviewSectionStatusLabels,
};
export type DomainValue<D extends EnumDomain> =
  (typeof domainEnums)[D][keyof (typeof domainEnums)[D]];

// Compile-time exhaustiveness: every enum value has a token, and no token names a value the enum
// lacks. A backend enum change fails typecheck here until the design tokens are reconciled.
type TokenValues<D extends EnumDomain> = keyof (typeof domainColors)[D]['values'];
type SameKeys<A, B> = [A] extends [B] ? ([B] extends [A] ? true : false) : false;
type PerDomain = { [D in EnumDomain]: SameKeys<DomainValue<D>, TokenValues<D>> }[EnumDomain];
type Exhaustive = false extends PerDomain ? false : true;
const exhaustive: Exhaustive = true;
void exhaustive;

/** Status domains with fill and text; direction is deliberately uncolored (no fill, see DomainText). */
type FilledDomain = Exclude<EnumDomain, 'direction'>;

const badgeBase =
  'inline-flex max-w-full items-center gap-inline-xs rounded-badge px-inset-badge-x type-badge break-words';

/**
 * The per-theme domain variables (`--loto-dom-*`, emitted inside every theme block), never the
 * root-level Tailwind `--color-dom-*`: a theme variable referenced from an inline style resolves
 * where it is declared, so only the per-theme one follows a region pinned to another theme.
 */
function domainStyle(group: string, value: string, parts: readonly string[]): CSSProperties {
  const style: Record<string, string> = {};
  for (const part of parts) style[`--badge-${part}`] = `var(--loto-dom-${group}-${value}-${part})`;
  return style as CSSProperties;
}

function isKnown(group: string, value: string): readonly string[] | null {
  const values = (domainColors as Record<string, { values: Record<string, readonly string[]> }>)[
    group
  ]?.values;
  return values !== undefined && Object.hasOwn(values, value) ? (values[value] ?? null) : null;
}

/**
 * An enum status or severity bucket (`dom.<group>.<value>`), labelled from copy §5 unless the caller
 * passes its own text. An unknown future value renders its raw text in the neutral variant and is
 * reported as a contract mismatch — never a guessed color or label.
 * Where no column header names it, pass `context` (**Situação**) for visually hidden context.
 */
export function StatusBadge<D extends FilledDomain>({
  domain,
  value,
  children,
  context,
  dot = false,
  onUnknown,
  className,
}: {
  readonly domain: D;
  readonly value: DomainValue<D> | (string & {});
  /** Overrides the copy §5 label (`@loto/core`) only where a screen's copy expands it. */
  readonly children?: ReactNode;
  readonly context?: string;
  /** Dense tables only: a leading dot that aids scanning. */
  readonly dot?: boolean;
  readonly onUnknown?: (domain: D, value: string) => void;
  readonly className?: string;
}) {
  const raw = String(value);
  const parts = isKnown(domain, raw);
  const label = enumLabel(domainLabels[domain], raw);
  if (parts === null || !label.known) {
    onUnknown?.(domain, raw);
    return (
      <FeedbackBadge kind="neutral" context={context} className={className}>
        {raw}
      </FeedbackBadge>
    );
  }
  return (
    <span
      data-domain={domain}
      data-value={raw}
      style={domainStyle(domain, raw, parts)}
      className={cn(badgeBase, 'bg-(--badge-bg) text-(--badge-fg)', className)}
    >
      {context ? <VisuallyHidden>{`${context}: `}</VisuallyHidden> : null}
      {dot ? (
        <span aria-hidden="true" className="size-[0.375rem] shrink-0 rounded-dot bg-(--badge-fg)" />
      ) : null}
      {children ?? label.label}
    </span>
  );
}

/** **Automática**: provenance beside the type, never in the status column (outlined, no fill). */
export function GeneratedMarker({
  children = 'Automática',
  className,
}: {
  readonly children?: ReactNode;
  readonly className?: string;
}) {
  return (
    <span
      style={domainStyle('generated-movement', 'automatic', ['border', 'fg'])}
      className={cn(badgeBase, 'border border-(--badge-border) text-(--badge-fg)', className)}
    >
      {children}
    </span>
  );
}

export type FeedbackKind = 'info' | 'success' | 'warning' | 'danger' | 'neutral';

const feedbackClasses: Record<FeedbackKind, string> = {
  info: 'bg-feedback-info-bg text-feedback-info-fg',
  success: 'bg-feedback-success-bg text-feedback-success-fg',
  warning: 'bg-feedback-warning-bg text-feedback-warning-fg',
  danger: 'bg-feedback-danger-bg text-feedback-danger-fg',
  neutral: 'bg-feedback-neutral-bg text-feedback-neutral-fg',
};

/** Markers such as **Inativa**, **Pendente**, **você**. */
export function FeedbackBadge({
  kind,
  children,
  context,
  className,
}: {
  readonly kind: FeedbackKind;
  readonly children: ReactNode;
  readonly context?: string;
  readonly className?: string;
}) {
  return (
    <span className={cn(badgeBase, feedbackClasses[kind], className)}>
      {context ? <VisuallyHidden>{`${context}: `}</VisuallyHidden> : null}
      {children}
    </span>
  );
}

/** The top-bar mode indicator: **Gestão** (info) or **Meu turno** (secondary accent), overline. */
export function ModeBadge({
  mode,
  className,
}: {
  readonly mode: 'gestao' | 'turno';
  readonly className?: string;
}) {
  return (
    <span
      className={cn(
        badgeBase,
        'type-overline',
        mode === 'gestao'
          ? feedbackClasses.info
          : 'bg-accent-secondary-bg text-accent-secondary-fg',
        className,
      )}
    >
      {mode === 'gestao' ? 'Gestão' : 'Meu turno'}
    </span>
  );
}

type SignedDomain = 'cash-variance' | 'fiado-balance' | 'hour-balance' | 'direction';

/**
 * A signed figure or direction in its domain color (`dom.cash-variance.shortage`, …). The sign and
 * the words (**falta**, **sobra**, **crédito do cliente**) always travel in the text itself.
 */
export function DomainText<D extends SignedDomain>({
  domain,
  value,
  children,
  className,
}: {
  readonly domain: D;
  readonly value: keyof (typeof domainColors)[D]['values'] & string;
  readonly children: ReactNode;
  readonly className?: string;
}) {
  return (
    <span style={domainStyle(domain, value, ['fg'])} className={cn('text-(--badge-fg)', className)}>
      {children}
    </span>
  );
}
