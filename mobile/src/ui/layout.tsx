import { EM_DASH } from '@loto/core';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Animated, Easing, View, type DimensionValue } from 'react-native';
import { useTheme } from '@/theme/provider';
import { isCompact, radius, spacing } from '@/theme/style';
import type { DomainColors } from '@/theme/theme';
import { Button } from './button';
import { AppText, Glyph, moveFocusTo } from './text';

// ------------------------------------------------------------------------------------------------
// Card
// ------------------------------------------------------------------------------------------------

export type CardVariant = 'section' | 'next-action' | 'checklist' | 'sheet';

/**
 * A bounded panel used sparingly (card.md). Never a whole-card press target: actions are explicit
 * buttons. The next-action card is a region named by its heading with a primary top rule.
 */
export function Card({
  variant = 'section',
  eyebrow,
  title,
  children,
  actions,
  accent,
  testID,
}: {
  readonly variant?: CardVariant;
  readonly eyebrow?: string;
  readonly title?: string;
  readonly children?: ReactNode;
  readonly actions?: ReactNode;
  /** Status cards mark their kind with a feedback border rule. */
  readonly accent?: 'info' | 'success' | 'warning' | 'danger' | 'neutral';
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  const rule =
    variant === 'next-action'
      ? colors.sem.action.primary.bg
      : accent
        ? colors.sem.feedback[accent].border
        : undefined;
  return (
    <View
      testID={testID}
      role={variant === 'next-action' ? 'region' : undefined}
      accessibilityLabel={variant === 'next-action' ? title : undefined}
      style={{
        backgroundColor: colors.sem.surface.default,
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.default,
        borderTopWidth: rule ? spacing.size['marker-width'] : spacing.size['border-width'],
        borderTopColor: rule ?? colors.sem.border.default,
        borderRadius: radius.panel,
        padding: spacing.inset.panel,
        gap: spacing.stack.sm,
        maxWidth: variant === 'sheet' ? spacing.size['dialog-max'] : undefined,
        width: variant === 'sheet' ? '100%' : undefined,
        alignSelf: variant === 'sheet' ? 'center' : undefined,
      }}
    >
      {eyebrow ? (
        <AppText variant="overline" tone="muted">
          {eyebrow}
        </AppText>
      ) : null}
      {title ? (
        <AppText
          variant={variant === 'next-action' ? 'next-action' : 'section-title'}
          accessibilityRole="header"
        >
          {title}
        </AppText>
      ) : null}
      {children}
      {actions ? (
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            gap: spacing.size['touch-target-gap'],
            marginTop: spacing.stack.sm,
          }}
        >
          {actions}
        </View>
      ) : null}
    </View>
  );
}

/** A checklist row with its status text and its own link (owner setup checklist). */
export function ChecklistItem({
  title,
  status,
  done,
  action,
}: {
  readonly title: string;
  readonly status: string;
  readonly done: boolean;
  readonly action?: ReactNode;
}) {
  const { colors } = useTheme();
  return (
    <View
      style={{
        flexDirection: 'row',
        gap: spacing.inline.md,
        alignItems: 'flex-start',
        paddingVertical: spacing.stack.sm,
        borderTopWidth: spacing.size['border-width'],
        borderTopColor: colors.sem.border.subtle,
      }}
    >
      <Glyph
        name={done ? 'check' : 'dash'}
        color={done ? colors.sem.feedback.success.fg : colors.sem.text.muted}
      />
      <View style={{ flex: 1, gap: spacing.stack.xs }}>
        <AppText variant="body-strong">{title}</AppText>
        <AppText variant="helper" tone="secondary">
          {status}
        </AppText>
        {action}
      </View>
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// SummaryStrip
// ------------------------------------------------------------------------------------------------

type SignedGroup = 'cash-variance' | 'fiado-balance' | 'hour-balance';
export type SummaryTone = {
  [G in SignedGroup]: { readonly group: G; readonly value: keyof DomainColors[G] };
}[SignedGroup];

export interface SummaryItem {
  readonly label: string;
  /** Pre-formatted by core; null renders **—** with the item's unavailable caption, never zero. */
  readonly value: string | null;
  readonly caption?: string;
  /** Signed figures colored by their meaning (sign and words stay visible in `value`). */
  readonly tone?: SummaryTone;
  /** Spoken form when it differs from the text (**menos doze reais, falta**). */
  readonly spoken?: string;
  readonly onRetry?: () => void;
  readonly loading?: boolean;
}

/**
 * Labelled figures of one scope (summary-strip.md). Items wrap to two columns, and to one on
 * compact layouts or enlarged text; figures never shrink to fit and never animate.
 */
export function SummaryStrip({
  items,
  variant = 'totals',
  testID,
}: {
  readonly items: readonly SummaryItem[];
  readonly variant?: 'totals' | 'explanatory' | 'signed';
  readonly testID?: string;
}) {
  const { colors, width, fontScale } = useTheme();
  const columns = isCompact(width, fontScale) ? 1 : 2;
  const itemWidth: DimensionValue = columns === 1 ? '100%' : '50%';
  return (
    <View
      testID={testID}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        backgroundColor: colors.sem.surface.default,
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.default,
        borderRadius: radius.panel,
      }}
    >
      {items.map((item, index) => {
        const color = item.tone
          ? (colors.dom[item.tone.group] as Record<string, { fg: string }>)[
              item.tone.value as string
            ]?.fg
          : variant === 'explanatory'
            ? colors.sem.text.secondary
            : colors.sem.text.primary;
        return (
          <View
            key={item.label}
            accessible
            accessibilityLabel={`${item.label}: ${item.loading ? 'Carregando…' : (item.spoken ?? item.value ?? EM_DASH)}${item.caption || item.value === null ? `, ${item.caption ?? 'Não foi possível carregar'}` : ''}`}
            style={{
              width: itemWidth,
              padding: spacing.inset.panel,
              gap: spacing.stack.xs,
              borderTopWidth: index >= columns ? spacing.size['border-width'] : 0,
              borderLeftWidth:
                columns > 1 && index % columns !== 0 ? spacing.size['border-width'] : 0,
              borderColor: colors.sem.border.subtle,
            }}
          >
            <AppText variant="table-header" tone="muted">
              {item.label}
            </AppText>
            {item.loading ? (
              <SkeletonBlock height={30} width="70%" />
            ) : (
              <AppText
                variant="summary-value"
                color={item.value === null ? colors.sem.text.muted : color}
              >
                {item.value ?? EM_DASH}
              </AppText>
            )}
            {item.caption || item.value === null ? (
              <AppText variant="helper" tone="muted">
                {item.caption ?? 'Não foi possível carregar'}
              </AppText>
            ) : null}
            {item.value === null && item.onRetry ? (
              <Button label="Tentar novamente" variant="link" onPress={item.onRetry} />
            ) : null}
          </View>
        );
      })}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// DetailList
// ------------------------------------------------------------------------------------------------

export interface DetailRow {
  readonly label: string;
  /** Pre-formatted; null renders the em dash (formatting §8). */
  readonly value: string | null;
  readonly provenance?: 'calculated' | 'informed';
  readonly restriction?: boolean;
  readonly action?: ReactNode;
  readonly loading?: boolean;
}

/** Read-only label/value facts (detail-list.md); two columns when wide, stacked when compact. */
export function DetailList({
  rows,
  testID,
}: {
  readonly rows: readonly DetailRow[];
  readonly testID?: string;
}) {
  const { width, fontScale, colors } = useTheme();
  const stacked = isCompact(width, fontScale);
  return (
    <View testID={testID} style={{ gap: spacing.stack.sm }}>
      {rows.map((row) => (
        <View
          key={row.label}
          style={{
            flexDirection: stacked ? 'column' : 'row',
            gap: stacked ? spacing.stack.xs : spacing.inline.lg,
          }}
        >
          <AppText
            variant="label"
            tone="secondary"
            style={stacked ? undefined : { width: '35%' }}
            importantForAccessibility="no"
            accessibilityElementsHidden
          >
            {row.label}
          </AppText>
          <View
            style={{ flex: 1, gap: spacing.stack.xs }}
            accessible
            accessibilityLabel={`${row.label}: ${row.loading ? 'Carregando…' : (row.value ?? EM_DASH)}`}
          >
            {row.loading ? (
              <SkeletonBlock height={20} width="60%" />
            ) : (
              <View
                style={{ flexDirection: 'row', gap: spacing.inline.xs, alignItems: 'flex-start' }}
              >
                {row.restriction ? <Glyph name="info" color={colors.sem.text.muted} /> : null}
                <AppText
                  variant="body"
                  tone={row.restriction ? 'muted' : 'primary'}
                  selectable
                  style={{ flex: 1 }}
                >
                  {row.value ?? EM_DASH}
                </AppText>
              </View>
            )}
            {row.provenance ? (
              <AppText variant="helper" tone="muted">
                {row.provenance === 'calculated'
                  ? 'calculado pelo sistema'
                  : 'informado pelo operador'}
              </AppText>
            ) : null}
          </View>
          {row.action}
        </View>
      ))}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Skeleton
// ------------------------------------------------------------------------------------------------

/**
 * A content-shaped placeholder; the highlight pulses only without reduce-motion. The pulse is an
 * opacity change of a highlight layer — no clipping or translation — because a clipped, moving
 * highlight inside a long scroll view kept Android's main thread busy enough to stall scrolling.
 */
export function SkeletonBlock({
  width = '100%',
  height = 16,
}: {
  readonly width?: DimensionValue;
  readonly height?: number;
}) {
  const { colors, reducedMotion, motion } = useTheme();
  const pulse = useState(() => new Animated.Value(0))[0];
  const duration = motion('skeleton').duration;
  useEffect(() => {
    if (reducedMotion || duration === 0) {
      pulse.setValue(0);
      return;
    }
    const loop = Animated.loop(
      Animated.timing(pulse, {
        toValue: 1,
        duration,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    );
    loop.start();
    return () => loop.stop();
  }, [reducedMotion, duration, pulse]);
  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={{ width, height, borderRadius: radius.xs, backgroundColor: colors.sem.skeleton.base }}
    >
      {reducedMotion ? null : (
        <Animated.View
          testID="skeleton-shimmer"
          style={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            left: 0,
            right: 0,
            borderRadius: radius.xs,
            backgroundColor: colors.sem.skeleton.highlight,
            opacity: pulse.interpolate({ inputRange: [0, 0.5, 1], outputRange: [0, 1, 0] }),
          }}
        />
      )}
    </View>
  );
}

/** A busy region announced once as **Carregando…**; shapes match the content they stand for. */
export function Skeleton({
  lines = 3,
  label = 'Carregando…',
  testID,
}: {
  readonly lines?: number;
  readonly label?: string;
  readonly testID?: string;
}) {
  return (
    <View
      testID={testID}
      accessible
      accessibilityLabel={label}
      accessibilityState={{ busy: true }}
      aria-busy
      style={{ gap: spacing.stack.sm }}
    >
      {Array.from({ length: lines }, (_, index) => (
        <SkeletonBlock key={index} width={index === lines - 1 ? '60%' : '100%'} />
      ))}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// EmptyState
// ------------------------------------------------------------------------------------------------

export type EmptyKind = 'no-records' | 'no-matches' | 'all-clear' | 'setup-needed';

/**
 * A successful, scoped response with nothing to show (empty-state.md). All-clear stays neutral —
 * "nothing pending" never reads as a financial result.
 */
export function EmptyState({
  kind,
  statement,
  explanation,
  primary,
  secondary,
  testID,
}: {
  readonly kind: EmptyKind;
  readonly statement: string;
  readonly explanation?: string;
  readonly primary?: ReactNode;
  readonly secondary?: ReactNode;
  readonly testID?: string;
}) {
  return (
    <View
      testID={testID}
      accessibilityLabel={kind}
      style={{ gap: spacing.stack.sm, paddingVertical: spacing.stack.md }}
    >
      <AppText variant="body-strong">{statement}</AppText>
      {explanation ? (
        <AppText variant="body" tone="secondary">
          {explanation}
        </AppText>
      ) : null}
      {primary || secondary ? (
        <View
          style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}
        >
          {primary}
          {secondary}
        </View>
      ) : null}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// PageHeader
// ------------------------------------------------------------------------------------------------

/**
 * Opens a screen with its semantic origin, title, context and actions (page-header.md). The title
 * receives screen-reader focus on entry; the return link names its destination and the same
 * destination is what native back goes to (the screen wires both to `origin.onPress`).
 */
export function PageHeader({
  title,
  context,
  origin,
  badge,
  actions,
  variant = 'record',
  focusOnMount = true,
  testID,
}: {
  readonly title: string;
  readonly context?: string;
  readonly origin?: {
    readonly label: string;
    readonly onPress: () => void;
    readonly trail?: readonly string[];
  };
  readonly badge?: ReactNode;
  readonly actions?: ReactNode;
  readonly variant?: 'record' | 'list' | 'auth';
  readonly focusOnMount?: boolean;
  readonly testID?: string;
}) {
  const { colors, width, fontScale } = useTheme();
  const compact = isCompact(width, fontScale);
  const heading = useRef(null);
  const [focused, setFocused] = useState(false);
  useEffect(() => {
    if (!focusOnMount || focused) return;
    const timer = setTimeout(() => {
      moveFocusTo(heading.current);
      setFocused(true);
    }, 100);
    return () => clearTimeout(timer);
  }, [focusOnMount, focused]);
  return (
    <View
      testID={testID}
      style={{
        gap: spacing.stack.sm,
        paddingBottom: spacing.stack.md,
        borderBottomWidth: spacing.size['border-width'],
        borderBottomColor: colors.sem.border.default,
        marginBottom: spacing.page['section-gap'],
      }}
    >
      {origin && variant !== 'auth' ? (
        compact || !origin.trail ? (
          <Button
            label={`Voltar para ${origin.label}`}
            variant="link"
            role="link"
            onPress={origin.onPress}
          />
        ) : (
          <View
            accessibilityRole="none"
            accessibilityLabel="Você está em"
            style={{
              flexDirection: 'row',
              flexWrap: 'wrap',
              alignItems: 'center',
              gap: spacing.inline.xs,
            }}
          >
            <Button label={origin.label} variant="link" role="link" onPress={origin.onPress} />
            {origin.trail.map((part) => (
              <AppText key={part} variant="body" tone="secondary">
                {`› ${part}`}
              </AppText>
            ))}
          </View>
        )
      ) : null}
      <AppText
        ref={heading}
        variant={compact ? 'page-title-compact' : 'page-title'}
        accessibilityRole="header"
      >
        {title}
      </AppText>
      {context || badge ? (
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            gap: spacing.inline.sm,
            alignItems: 'center',
          }}
        >
          {context ? (
            <AppText variant="body" tone="secondary">
              {context}
            </AppText>
          ) : null}
          {badge}
        </View>
      ) : null}
      {actions ? (
        <View
          style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}
        >
          {actions}
        </View>
      ) : null}
    </View>
  );
}
