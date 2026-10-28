import { enumLabel, enumLabels, generatedMovementMarker } from '@loto/core';
import { useEffect } from 'react';
import { View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import type { DomainColors, SemanticColors } from '@/theme/theme';
import { AppText } from './text';

/** Domain groups that are backend enums: token group → generated enum name (copy §5 map). */
export const statusDomains = {
  'transaction-status': 'TransactionStatus',
  'daily-close-status': 'DailyCloseStatus',
  'aging-bucket': 'AgingBucket',
  'time-entry-status': 'TimeEntryStatus',
  'installment-plan-status': 'InstallmentPlanStatus',
  'invitation-status': 'InvitationStatus',
  'mail-delivery-status': 'MailDeliveryStatus',
  'operator-link-request-status': 'OperatorLinkRequestStatus',
  'review-event-type': 'DailyCloseReviewEventType',
  'settlement-instrument': 'SettlementInstrument',
  'monthly-financial-role': 'MonthlyFinancialRole',
  'overview-section-status': 'OverviewSectionStatus',
} as const satisfies Record<string, keyof typeof enumLabels>;

export type StatusDomain = keyof typeof statusDomains;

function statusLabel(domain: StatusDomain, value: string) {
  return enumLabel(enumLabels[statusDomains[domain]] as Readonly<Record<string, string>>, value);
}
type FeedbackKind = keyof SemanticColors['feedback'];

export type BadgeProps =
  | {
      readonly kind: 'status';
      readonly domain: StatusDomain;
      /** The exact enum value the API returned; an unknown future value renders neutrally. */
      readonly value: string;
      /** Called once with an unknown value so the screen can report the contract mismatch. */
      readonly onUnknownValue?: (domain: StatusDomain, value: string) => void;
      /** Visually hidden context when no column header names the badge (**Situação:**). */
      readonly context?: string;
    }
  | { readonly kind: 'automatic' }
  | {
      readonly kind: 'feedback';
      readonly tone: FeedbackKind;
      readonly label: string;
      readonly context?: string;
    }
  | { readonly kind: 'mode'; readonly mode: 'gestao' | 'meu-turno' };

/**
 * Badge (badge.md): a rectangular text label; color is a second signal. Labels come from core's
 * copy §5 maps and the domain token from the same exact enum key. Never truncated, never a target.
 */
export function Badge(props: BadgeProps) {
  const { colors } = useTheme();
  const unknownValue =
    props.kind === 'status' ? !statusLabel(props.domain, props.value).known : false;
  const report = props.kind === 'status' ? props.onUnknownValue : undefined;
  const domain = props.kind === 'status' ? props.domain : null;
  const value = props.kind === 'status' ? props.value : null;
  useEffect(() => {
    if (unknownValue && report && domain && value !== null) report(domain, value);
  }, [unknownValue, report, domain, value]);

  let label: string;
  let fg: string;
  let bg: string | undefined;
  let border: string | undefined;
  let overline = false;
  let context: string | undefined;
  if (props.kind === 'status') {
    const known = statusLabel(props.domain, props.value);
    label = known.label;
    context = props.context;
    const tokens = (
      colors.dom[props.domain] as Record<string, { fg: string; bg: string }> | undefined
    )?.[props.value];
    if (known.known && tokens) {
      fg = tokens.fg;
      bg = tokens.bg;
    } else {
      fg = colors.sem.feedback.neutral.fg;
      bg = colors.sem.feedback.neutral.bg;
    }
  } else if (props.kind === 'automatic') {
    label = generatedMovementMarker;
    const marker: DomainColors['generated-movement']['automatic'] =
      colors.dom['generated-movement'].automatic;
    fg = marker.fg;
    border = marker.border;
  } else if (props.kind === 'feedback') {
    label = props.label;
    context = props.context;
    fg = colors.sem.feedback[props.tone].fg;
    bg = colors.sem.feedback[props.tone].bg;
  } else {
    overline = true;
    if (props.mode === 'gestao') {
      label = 'Gestão';
      fg = colors.sem.feedback.info.fg;
      bg = colors.sem.feedback.info.bg;
    } else {
      label = 'Meu turno';
      fg = colors.sem.accent['secondary-fg'];
      bg = colors.sem.accent['secondary-bg'];
    }
  }
  return (
    <View
      accessible
      accessibilityLabel={context ? `${context}: ${label}` : label}
      style={{
        alignSelf: 'flex-start',
        maxWidth: '100%',
        borderRadius: radius.badge,
        paddingHorizontal: spacing.inset['badge-x'],
        paddingVertical: 1,
        backgroundColor: bg,
        borderWidth: border ? spacing.size['border-width'] : 0,
        borderColor: border,
      }}
    >
      <AppText variant={overline ? 'overline' : 'badge'} color={fg}>
        {label}
      </AppText>
    </View>
  );
}
