import { formatCount } from '@loto/core';
import { useState, type ReactNode } from 'react';
import { Pressable, ScrollView, View, type DimensionValue } from 'react-native';
import { useTheme } from '@/theme/provider';
import { PressableBox } from './focus';
import { radius, shadowStyle, spacing } from '@/theme/style';
import { Button } from './button';
import { Checkbox } from './choice';
import { Disclosure, Notice } from './feedback';
import { EmptyState, SkeletonBlock, type EmptyKind } from './layout';
import { AppText } from './text';

export interface Column<Row> {
  readonly key: string;
  readonly header: string;
  /** Money and counts align right with tabular figures; the header follows. */
  readonly numeric?: boolean;
  /** Column width in dp, shared by header and rows so columns line up; the table scrolls rather than dropping columns. */
  readonly minWidth: number;
  readonly render: (row: Row) => ReactNode;
}

export interface RowLink<Row> {
  /** **Abrir transação**, **Conferir** … */
  readonly label: string;
  /** Names the row (date, value, account) for assistive tech. */
  readonly accessibleName: (row: Row) => string;
  readonly onOpen: (row: Row) => void;
}

export type TableState =
  | { readonly kind: 'ready' }
  | { readonly kind: 'loading'; readonly rows?: number }
  | { readonly kind: 'updating' }
  | {
      readonly kind: 'empty';
      readonly empty: EmptyKind;
      readonly statement: string;
      readonly action?: ReactNode;
    }
  | {
      readonly kind: 'error';
      readonly title: string;
      readonly messages?: readonly string[];
      readonly onRetry: () => void;
    };

/**
 * Record table (table.md) on native: same fields and order as web, inside its own labelled
 * horizontal scroll region; the action column stays sticky at the end. A row whose record can be
 * opened is a whole-row press target **and** carries an explicit link; nested controls (selection,
 * actions) are separate accessible elements whose presses never also open the row.
 */
export function DataTable<Row>({
  caption,
  columns,
  rows,
  rowKey,
  link,
  actions,
  selection,
  state = { kind: 'ready' },
  footer,
  testID,
}: {
  readonly caption: string;
  readonly columns: readonly Column<Row>[];
  readonly rows: readonly Row[];
  readonly rowKey: (row: Row) => string;
  readonly link?: RowLink<Row>;
  /** Nested actions per row (**Ver parcelas**); they never open the row. */
  readonly actions?: (row: Row) => ReactNode;
  readonly selection?: {
    readonly selected: ReadonlySet<string>;
    readonly onToggle: (key: string, selected: boolean) => void;
    readonly label: (row: Row) => string;
  };
  readonly state?: TableState;
  /** Page subtotal or whole-set total, labelled with its scope. */
  readonly footer?: ReactNode;
  readonly testID?: string;
}) {
  const { colors, scheme, fontScale } = useTheme();
  // Widths grow with the OS text size so enlarged digits widen the column instead of breaking.
  const columnWidth = (column: Column<Row>) => Math.ceil(column.minWidth * Math.max(1, fontScale));
  const [pressedRow, setPressedRow] = useState<string | null>(null);
  // The sticky action column mirrors each data row's measured height so rows stay aligned.
  const [heights, setHeights] = useState<Readonly<Record<string, number>>>({});
  // Each row pair (data row, action cell) takes the taller natural height of the two.
  const rowHeight = (key: string) => {
    const natural = Math.max(heights[`d:${key}`] ?? 0, heights[`a:${key}`] ?? 0);
    // Both outer views carry the same bottom hairline on top of the natural content height.
    return natural === 0 ? undefined : natural + spacing.size['border-width'];
  };
  const measure = (key: string, height: number) =>
    setHeights((current) => (current[key] === height ? current : { ...current, [key]: height }));
  const cellPadding = {
    paddingVertical: spacing.inset['table-cell-y'],
    paddingHorizontal: spacing.inset['table-cell-x'],
  };
  const selectWidth = selection
    ? spacing.size['touch-target-comfortable'] + spacing.inset['table-cell-x']
    : 0;
  const header = (
    <View
      onLayout={(event) => measure('__header', event.nativeEvent.layout.height)}
      style={{
        flexDirection: 'row',
        backgroundColor: colors.sem.surface.subtle,
        borderBottomWidth: spacing.size['border-width'],
        borderBottomColor: colors.sem.border.subtle,
      }}
    >
      {selection ? <View style={{ width: selectWidth }} /> : null}
      {columns.map((column) => (
        <View key={column.key} style={[cellPadding, { width: columnWidth(column) }]}>
          <AppText variant="table-header" tone="muted" align={column.numeric ? 'right' : 'left'}>
            {column.header}
          </AppText>
        </View>
      ))}
    </View>
  );
  let body: ReactNode;
  if (state.kind === 'loading')
    body = Array.from({ length: state.rows ?? 3 }, (_, index) => (
      <View
        key={index}
        style={{
          flexDirection: 'row',
          borderBottomWidth: spacing.size['border-width'],
          borderBottomColor: colors.sem.border.subtle,
        }}
      >
        {columns.map((column) => (
          <View key={column.key} style={[cellPadding, { width: columnWidth(column) }]}>
            <SkeletonBlock height={18} width={column.numeric ? '60%' : '80%'} />
          </View>
        ))}
      </View>
    ));
  else if (state.kind === 'empty' || state.kind === 'error') body = null;
  else
    body = rows.map((row) => {
      const key = rowKey(row);
      const selected = selection?.selected.has(key) ?? false;
      const background = selected
        ? colors.sem.surface.selected
        : pressedRow === key
          ? colors.sem.surface.hover
          : colors.sem.surface.default;
      return (
        <View
          key={key}
          testID={testID ? `${testID}-row-${key}` : undefined}
          style={{
            flexDirection: 'row',
            minHeight: rowHeight(key),
            backgroundColor: background,
            borderBottomWidth: spacing.size['border-width'],
            borderBottomColor: colors.sem.border.subtle,
            opacity: state.kind === 'updating' ? 0.6 : 1,
          }}
        >
          {selection ? (
            <View
              style={{
                width: selectWidth,
                paddingLeft: spacing.inset['table-cell-x'],
                justifyContent: 'center',
              }}
            >
              <Checkbox
                labelHidden
                label={selection.label(row)}
                checked={selected}
                onCheckedChange={(next) => selection.onToggle(key, next)}
                testID={testID ? `${testID}-select-${key}` : undefined}
              />
            </View>
          ) : null}
          <Pressable
            // The whole row opens the record for touch; its link below is the accessible element.
            accessible={false}
            importantForAccessibility="no"
            testID={testID ? `${testID}-rowpress-${key}` : undefined}
            onPress={link ? () => link.onOpen(row) : undefined}
            onPressIn={link ? () => setPressedRow(key) : undefined}
            onPressOut={() => setPressedRow(null)}
            style={{
              flexDirection: 'row',
              flex: 1,
              alignItems: 'center',
              minHeight: spacing.size['touch-target-min'],
            }}
          >
            {/* Natural height of the cells, paired with the action cell's. */}
            <View
              onLayout={(event) => measure(`d:${key}`, event.nativeEvent.layout.height)}
              style={{ flexDirection: 'row', flex: 1 }}
            >
              {columns.map((column) => (
                <View
                  key={column.key}
                  style={[cellPadding, { width: columnWidth(column), justifyContent: 'center' }]}
                >
                  {renderCell(column, row)}
                </View>
              ))}
            </View>
          </Pressable>
        </View>
      );
    });
  return (
    <View
      testID={testID}
      style={{ gap: spacing.stack.sm }}
      aria-busy={state.kind === 'loading' || state.kind === 'updating'}
    >
      <AppText variant="section-title" accessibilityRole="header">
        {caption}
      </AppText>
      {state.kind === 'updating' ? (
        <AppText variant="helper" tone="muted" accessibilityLiveRegion="polite">
          Atualizando…
        </AppText>
      ) : null}
      <View
        style={{
          flexDirection: 'row',
          borderWidth: spacing.size['border-width'],
          borderColor: colors.sem.border.default,
          borderRadius: radius.panel,
          overflow: 'hidden',
          backgroundColor: colors.sem.surface.default,
        }}
      >
        <ScrollView
          horizontal
          accessibilityLabel={`Tabela ${caption}, role horizontalmente`}
          showsHorizontalScrollIndicator
          contentContainerStyle={{ flexGrow: 1 }}
          style={{ flex: 1 }}
          testID={testID ? `${testID}-scroll` : undefined}
        >
          <View style={{ flexGrow: 1 }}>
            {header}
            {body}
          </View>
        </ScrollView>
        {link || actions ? (
          // Sticky action column: never scrolled away with the data.
          <View
            style={{
              borderLeftWidth: spacing.size['border-width'],
              borderLeftColor: colors.sem.border.subtle,
              ...shadowStyle(scheme, 'raised'),
              backgroundColor: colors.sem.surface.default,
            }}
          >
            <View
              style={[
                cellPadding,
                {
                  height: heights.__header,
                  backgroundColor: colors.sem.surface.subtle,
                  borderBottomWidth: spacing.size['border-width'],
                  borderBottomColor: colors.sem.border.subtle,
                },
              ]}
            >
              <AppText variant="table-header" tone="muted">
                Ações
              </AppText>
            </View>
            {state.kind === 'ready' || state.kind === 'updating'
              ? rows.map((row) => (
                  <View
                    key={rowKey(row)}
                    style={{
                      minHeight: rowHeight(rowKey(row)) ?? spacing.size['touch-target-min'],
                      paddingHorizontal: spacing.inline.xs,
                      justifyContent: 'center',
                      borderBottomWidth: spacing.size['border-width'],
                      borderBottomColor: colors.sem.border.subtle,
                    }}
                  >
                    <View
                      onLayout={(event) =>
                        measure(`a:${rowKey(row)}`, event.nativeEvent.layout.height)
                      }
                      style={{ gap: spacing.stack.xs }}
                    >
                      {link ? (
                        <Button
                          label={link.label}
                          variant="link"
                          role="link"
                          accessibilityLabel={`${link.label} ${link.accessibleName(row)}`}
                          onPress={() => link.onOpen(row)}
                          testID={testID ? `${testID}-link-${rowKey(row)}` : undefined}
                        />
                      ) : null}
                      {actions?.(row)}
                    </View>
                  </View>
                ))
              : null}
          </View>
        ) : null}
      </View>
      {state.kind === 'empty' ? (
        <EmptyState kind={state.empty} statement={state.statement} primary={state.action} />
      ) : null}
      {state.kind === 'error' ? (
        <Notice
          kind="danger"
          title={state.title}
          messages={state.messages}
          actions={<Button label="Tentar novamente" variant="secondary" onPress={state.onRetry} />}
        />
      ) : null}
      {footer}
    </View>
  );
}

function renderCell<Row>(column: Column<Row>, row: Row) {
  const content = column.render(row);
  if (typeof content === 'string')
    return (
      <AppText variant="table" align={column.numeric ? 'right' : 'left'}>
        {content}
      </AppText>
    );
  return content;
}

export interface ListItem {
  readonly key: string;
  /** Name or account. */
  readonly primary: string;
  /** Status and value, pre-formatted. */
  readonly secondary?: ReactNode;
  readonly trailing?: string;
  readonly onOpen?: () => void;
  readonly openLabel?: string;
  readonly actions?: ReactNode;
  readonly children?: ReactNode;
}

/**
 * Grouped list for compact queue/admin rows (table.md): each item's link comes first; status is
 * read in text; nested actions are separate elements. Optional groups expand with a Disclosure.
 */
export function GroupedList({
  caption,
  groups,
  emptyStatement,
  testID,
}: {
  readonly caption: string;
  readonly groups: readonly {
    readonly heading?: string;
    readonly count?: number;
    readonly expandable?: boolean;
    readonly items: readonly ListItem[];
  }[];
  readonly emptyStatement?: string;
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  const total = groups.reduce((sum, group) => sum + group.items.length, 0);
  const renderItems = (items: readonly ListItem[]) => (
    <View accessibilityRole="list">
      {items.map((item) => (
        <View
          key={item.key}
          style={{
            paddingVertical: spacing.stack.sm,
            borderBottomWidth: spacing.size['border-width'],
            borderBottomColor: colors.sem.border.subtle,
            gap: spacing.stack.xs,
          }}
        >
          <PressableBox
            accessibilityRole={item.onOpen ? 'link' : undefined}
            accessibilityLabel={`${item.openLabel ? `${item.openLabel}: ` : ''}${item.primary}${item.trailing ? `, ${item.trailing}` : ''}`}
            onPress={item.onOpen}
            style={(pressed) => ({
              minHeight: spacing.size['touch-target-min'],
              flexDirection: 'row',
              gap: spacing.inline.md,
              alignItems: 'center',
              backgroundColor: pressed && item.onOpen ? colors.sem.surface.hover : undefined,
            })}
          >
            <View style={{ flex: 1, gap: spacing.stack.xs }}>
              <AppText variant="body-strong" color={item.onOpen ? colors.sem.text.link : undefined}>
                {item.primary}
              </AppText>
              {typeof item.secondary === 'string' ? (
                <AppText variant="helper" tone="secondary">
                  {item.secondary}
                </AppText>
              ) : (
                item.secondary
              )}
            </View>
            {item.trailing ? (
              <AppText variant="table" align="right">
                {item.trailing}
              </AppText>
            ) : null}
          </PressableBox>
          {item.actions ? (
            <View
              style={{
                flexDirection: 'row',
                flexWrap: 'wrap',
                gap: spacing.size['touch-target-gap'],
              }}
            >
              {item.actions}
            </View>
          ) : null}
          {item.children}
        </View>
      ))}
    </View>
  );
  return (
    <View testID={testID} style={{ gap: spacing.stack.sm }}>
      <AppText variant="section-title" accessibilityRole="header">
        {caption}
      </AppText>
      {total === 0 && emptyStatement ? (
        <EmptyState kind="no-records" statement={emptyStatement} />
      ) : null}
      {groups.map((group, index) => {
        const heading = group.heading
          ? `${group.heading}${group.count !== undefined ? ` (${formatCount(group.count)})` : ''}`
          : undefined;
        if (group.expandable && heading)
          return (
            <Disclosure key={heading} label={heading}>
              {renderItems(group.items)}
            </Disclosure>
          );
        return (
          <View key={heading ?? index}>
            {heading ? (
              <AppText
                variant="overline"
                tone="muted"
                accessibilityRole="header"
                style={{ marginTop: spacing.stack.sm }}
              >
                {heading}
              </AppText>
            ) : null}
            {renderItems(group.items)}
          </View>
        );
      })}
    </View>
  );
}

/** Fixed-width helper for numeric cells that must keep their digits together. */
export const numericCellWidth = (characters: number): DimensionValue =>
  characters * 9 + spacing.inset['table-cell-x'] * 2;
