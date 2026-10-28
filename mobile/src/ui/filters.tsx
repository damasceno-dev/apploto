import { formatCount } from '@loto/core';
import { useEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import { View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { isCompact, radius, spacing } from '@/theme/style';
import { Button } from './button';
import { Overlay, useInsideOverlay } from './overlay';
import { Select } from './select';
import { announce, AppText, moveFocusTo } from './text';

// ------------------------------------------------------------------------------------------------
// FilterBar
// ------------------------------------------------------------------------------------------------

export interface FilterBarProps {
  /** Labelled region (**Filtros de transações**). */
  readonly title: string;
  /** The staged fields; the screen keeps staged versus applied values. */
  readonly children: ReactNode;
  /** The applied scope as words (**24/09/2026**, **Terminal 2**), or the default (**padrão — hoje, todos os tipos**). */
  readonly appliedSummary: readonly string[];
  /** Number of active non-default filters (**Filtros (2)**). */
  readonly activeCount: number;
  readonly mode?: 'submit' | 'immediate';
  readonly dirty?: boolean;
  readonly applying?: boolean;
  readonly onApply?: () => void;
  /** Shown only when some value differs from the default. */
  readonly onClear?: () => void;
  /** Spoken politely after a result (**37 transações**), without moving focus. */
  readonly resultAnnouncement?: string;
  readonly testID?: string;
}

/**
 * FilterBar (filter-bar.md): the endpoint's real query fields plus a plain-language summary of the
 * applied scope. On compact layouts it collapses to the summary and **Filtros (n)**, opening the
 * fields in a sheet — inline instead when already inside a dialog (one modal level).
 */
export function FilterBar({
  title,
  children,
  appliedSummary,
  activeCount,
  mode = 'submit',
  dirty = false,
  applying = false,
  onApply,
  onClear,
  resultAnnouncement,
  testID,
}: FilterBarProps) {
  const { colors, width, fontScale } = useTheme();
  const nested = useInsideOverlay();
  const compact = isCompact(width, fontScale) && !nested;
  const [open, setOpen] = useState(false);
  const trigger = useRef<View>(null);
  useEffect(() => {
    if (resultAnnouncement) announce(resultAnnouncement);
  }, [resultAnnouncement]);
  const actions = (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.size['touch-target-gap'] }}>
      {mode === 'submit' ? (
        <Button
          label="Filtrar"
          variant={dirty ? 'primary' : 'secondary'}
          loading={applying}
          loadingLabel="Filtrando…"
          onPress={() => {
            setOpen(false);
            onApply?.();
          }}
        />
      ) : null}
      {onClear && activeCount > 0 ? (
        <Button
          label="Limpar filtros"
          variant="link"
          onPress={() => {
            setOpen(false);
            onClear();
          }}
        />
      ) : null}
    </View>
  );
  const summary = (
    <AppText
      variant="helper"
      tone="secondary"
      accessibilityLabel={`Mostrando: ${appliedSummary.join(', ')}`}
    >
      {appliedSummary.join(' · ')}
    </AppText>
  );
  if (compact)
    return (
      <View testID={testID} accessibilityLabel={title} style={{ gap: spacing.stack.sm }}>
        {summary}
        <Button
          ref={trigger}
          label={`Filtros (${formatCount(activeCount)})`}
          variant="secondary"
          onPress={() => setOpen(true)}
          testID={testID ? `${testID}-open` : undefined}
        />
        <Overlay
          visible={open}
          onDismiss={() => setOpen(false)}
          title={title}
          variant="bottom-sheet"
          returnFocus={trigger}
          footer={actions}
        >
          {children}
        </Overlay>
      </View>
    );
  return (
    <View
      testID={testID}
      accessibilityLabel={title}
      style={{
        gap: spacing.stack.md,
        padding: spacing.inset.panel,
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.default,
        borderRadius: radius.panel,
        backgroundColor: colors.sem.surface.default,
      }}
    >
      {children}
      {actions}
      {summary}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Pagination
// ------------------------------------------------------------------------------------------------

/** The endpoint's own page envelope. */
export interface PageEnvelope {
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
  readonly hasNext: boolean;
  readonly hasPrevious: boolean;
}

/** `21–40 de 137` from the envelope (counts dot-grouped by core). */
export function pageRangeText({ page, pageSize, totalCount }: PageEnvelope): string {
  if (totalCount === 0) return `0 de ${formatCount(0)}`;
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);
  return `${formatCount(start)}–${formatCount(end)} de ${formatCount(totalCount)}`;
}

/** After a write: the last valid page when the current one passed `TotalPages`, page 1 when empty. */
export function boundedPage(page: number, totalPages: number): number {
  if (totalPages <= 0) return 1;
  return Math.min(Math.max(1, page), totalPages);
}

/**
 * Pagination (pagination.md) driven only by the envelope: `HasNext`/`HasPrevious` decide button
 * availability; a page beyond the bounds is corrected to the last valid one; changing page speaks
 * the new range and moves focus to the list heading.
 */
export function Pagination({
  envelope,
  onPageChange,
  listName,
  loading = false,
  pageSizes,
  onPageSizeChange,
  focusTarget,
  testID,
}: {
  readonly envelope: PageEnvelope;
  readonly onPageChange: (page: number) => void;
  readonly listName: string;
  readonly loading?: boolean;
  readonly pageSizes?: readonly number[];
  readonly onPageSizeChange?: (size: number) => void;
  readonly focusTarget?: RefObject<{ focus?: () => void } | null>;
  readonly testID?: string;
}) {
  const range = pageRangeText(envelope);
  const bounded = boundedPage(envelope.page, envelope.totalPages);
  useEffect(() => {
    if (!loading && bounded !== envelope.page) onPageChange(bounded);
  }, [bounded, envelope.page, loading, onPageChange]);
  const previousRange = useRef(range);
  useEffect(() => {
    if (previousRange.current === range) return;
    previousRange.current = range;
    announce(range);
    moveFocusTo(focusTarget?.current);
  }, [range, focusTarget]);
  if (envelope.totalPages <= 1)
    return (
      <AppText testID={testID} variant="helper" tone="secondary">
        {range}
      </AppText>
    );
  return (
    <View
      testID={testID}
      role="navigation"
      accessibilityLabel={`Paginação de ${listName}`}
      style={{ gap: spacing.stack.sm }}
    >
      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          alignItems: 'center',
          gap: spacing.size['touch-target-gap'],
        }}
      >
        <Button
          label="Anterior"
          accessibilityLabel="Página anterior"
          variant="secondary"
          disabled={loading || !envelope.hasPrevious}
          onPress={() => onPageChange(envelope.page - 1)}
        />
        <View style={{ flexShrink: 1 }}>
          <AppText
            variant="body"
            aria-current="page"
          >{`Página ${formatCount(envelope.page)} de ${formatCount(envelope.totalPages)}`}</AppText>
          <AppText variant="helper" tone="secondary">
            {loading ? `${range} · Atualizando…` : range}
          </AppText>
        </View>
        <Button
          label="Próxima"
          accessibilityLabel="Próxima página"
          variant="secondary"
          disabled={loading || !envelope.hasNext}
          onPress={() => onPageChange(envelope.page + 1)}
        />
      </View>
      {pageSizes && onPageSizeChange ? (
        <View style={{ maxWidth: 220 }}>
          <Select
            accessibilityLabel="Itens por página"
            options={pageSizes.map((size) => ({ value: String(size), label: formatCount(size) }))}
            value={String(envelope.pageSize)}
            onValueChange={(size) => onPageSizeChange(Number(size))}
            disabled={loading}
          />
        </View>
      ) : null}
    </View>
  );
}
