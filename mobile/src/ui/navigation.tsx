import { useRef, useState, type ReactNode } from 'react';
import { ActivityIndicator, Pressable, ScrollView, View } from 'react-native';
import { formatCount } from '@loto/core';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import { FocusRing, useFocusState, PressableBox } from './focus';
import { Overlay } from './overlay';
import { AppText, Glyph } from './text';

// ------------------------------------------------------------------------------------------------
// Tabs and segmented control
// ------------------------------------------------------------------------------------------------

export interface TabItem<K extends string> {
  readonly key: K;
  readonly label: string;
  readonly count?: number;
  readonly disabled?: boolean;
  readonly disabledReason?: string;
}

/** Sibling views of one scope (tabs.md); labels never truncate — the list scrolls instead. */
export function Tabs<K extends string>({
  tabs,
  value,
  onValueChange,
  accessibilityLabel,
  testID,
}: {
  readonly tabs: readonly TabItem<K>[];
  readonly value: K;
  readonly onValueChange: (key: K) => void;
  readonly accessibilityLabel?: string;
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      testID={testID}
      style={{
        borderBottomWidth: spacing.size['border-width'],
        borderBottomColor: colors.sem.border.default,
      }}
    >
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator
        accessibilityRole="tablist"
        accessibilityLabel={accessibilityLabel}
      >
        {tabs.map((tab) => (
          <Tab
            key={tab.key}
            tab={tab}
            selected={tab.key === value}
            onPress={() => onValueChange(tab.key)}
          />
        ))}
      </ScrollView>
    </View>
  );
}

function Tab<K extends string>({
  tab,
  selected,
  onPress,
}: {
  readonly tab: TabItem<K>;
  readonly selected: boolean;
  readonly onPress: () => void;
}) {
  const { colors } = useTheme();
  const [focused, handlers] = useFocusState();
  const label = tab.count === undefined ? tab.label : `${tab.label} (${formatCount(tab.count)})`;
  return (
    <PressableBox
      accessibilityRole="tab"
      accessibilityLabel={label}
      accessibilityHint={tab.disabled ? tab.disabledReason : undefined}
      accessibilityState={{ selected, disabled: Boolean(tab.disabled) }}
      onPress={() => {
        if (!tab.disabled) onPress();
      }}
      {...handlers}
      style={(pressed) => ({
        minHeight: spacing.size['touch-target-comfortable'],
        paddingHorizontal: spacing.inline.lg,
        justifyContent: 'center',
        borderBottomWidth: spacing.size['marker-width'],
        borderBottomColor: selected ? colors.sem.selection.indicator : colors.sem.action.ghost.bg,
        backgroundColor: pressed ? colors.sem.surface.hover : undefined,
      })}
    >
      <AppText
        variant="label"
        tone={tab.disabled ? 'disabled' : selected ? 'primary' : 'secondary'}
      >
        {label}
      </AppText>
      <FocusRing visible={focused} />
    </PressableBox>
  );
}

/** Small immediate preference (theme Claro / Escuro / Sistema): a radio group of equal buttons. */
export function SegmentedControl<V extends string>({
  options,
  value,
  onValueChange,
  accessibilityLabel,
  disabled = false,
  testID,
}: {
  readonly options: readonly {
    readonly value: V;
    readonly label: string;
    readonly disabled?: boolean;
    readonly disabledReason?: string;
  }[];
  readonly value: V;
  readonly onValueChange: (value: V) => void;
  readonly accessibilityLabel: string;
  readonly disabled?: boolean;
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  return (
    <View
      testID={testID}
      accessibilityRole="radiogroup"
      accessibilityLabel={accessibilityLabel}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.control,
        borderRadius: radius.control,
        overflow: 'hidden',
        alignSelf: 'flex-start',
      }}
    >
      {options.map((option, index) => {
        const selected = option.value === value;
        const blocked = disabled || Boolean(option.disabled);
        return (
          <Pressable
            key={option.value}
            accessibilityRole="radio"
            accessibilityLabel={option.label}
            accessibilityState={{ checked: selected, disabled: blocked }}
            accessibilityHint={blocked ? option.disabledReason : undefined}
            onPress={() => {
              if (!blocked) onValueChange(option.value);
            }}
            style={{
              minHeight: spacing.size['touch-target-comfortable'],
              paddingHorizontal: spacing.inline.lg,
              flexDirection: 'row',
              alignItems: 'center',
              gap: spacing.inline.xs,
              backgroundColor: selected ? colors.sem.surface.selected : colors.sem.surface.default,
              borderLeftWidth: index === 0 ? 0 : spacing.size['border-width'],
              borderLeftColor: colors.sem.border.control,
            }}
          >
            {selected ? <Glyph name="check" color={colors.sem.selection.indicator} /> : null}
            <AppText
              variant={selected ? 'body-strong' : 'control'}
              tone={blocked ? 'disabled' : 'primary'}
            >
              {option.label}
            </AppText>
          </Pressable>
        );
      })}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Menu
// ------------------------------------------------------------------------------------------------

export interface MenuItem {
  readonly key: string;
  readonly label: string;
  readonly description?: string;
  /** `link` items navigate; `radio` items form the mode group (Gestão / Meu turno). */
  readonly kind?: 'action' | 'link' | 'radio';
  readonly checked?: boolean;
  readonly onSelect?: () => void;
  /** Unavailable items stay focusable so their reason can be read, with a link to fix it. */
  readonly unavailableReason?: string;
  readonly fix?: { readonly label: string; readonly onPress: () => void };
  /** In progress (**Saindo…**): not activatable. */
  readonly busyLabel?: string;
}

export interface MenuGroup {
  readonly heading?: string;
  readonly items: readonly MenuItem[];
}

/**
 * Menu (menu.md) on native: an accessible bottom-sheet list opened from its trigger. Selection,
 * the close button, the backdrop and Android back close it; focus returns to the trigger.
 */
export function Menu({
  triggerLabel,
  triggerAccessibilityLabel,
  title,
  header,
  groups,
  testID,
}: {
  readonly triggerLabel: string;
  readonly triggerAccessibilityLabel?: string;
  readonly title: string;
  readonly header?: ReactNode;
  readonly groups: readonly MenuGroup[];
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  const [open, setOpen] = useState(false);
  const trigger = useRef<View>(null);
  const [focused, handlers] = useFocusState();
  return (
    <>
      <PressableBox
        ref={trigger}
        testID={testID}
        accessibilityRole="button"
        accessibilityLabel={triggerAccessibilityLabel ?? triggerLabel}
        accessibilityState={{ expanded: open }}
        aria-haspopup="menu"
        onPress={() => setOpen(true)}
        {...handlers}
        style={(pressed) => ({
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.inline.sm,
          minHeight: spacing.size['touch-target-comfortable'],
          paddingHorizontal: spacing.inline.md,
          borderRadius: radius.control,
          backgroundColor: pressed ? colors.sem.action.ghost['bg-active'] : undefined,
          alignSelf: 'flex-start',
        })}
      >
        <AppText variant="button" color={colors.sem.action.ghost.fg}>
          {triggerLabel}
        </AppText>
        <Glyph name="chevronDown" color={colors.sem.action.ghost.fg} />
        <FocusRing visible={focused} />
      </PressableBox>
      <Overlay
        visible={open}
        onDismiss={() => setOpen(false)}
        title={title}
        variant="bottom-sheet"
        returnFocus={trigger}
        testID={testID ? `${testID}-sheet` : undefined}
      >
        {header}
        {groups.map((group, groupIndex) => (
          <View
            key={group.heading ?? groupIndex}
            accessibilityRole={
              group.items.some((item) => item.kind === 'radio') ? 'radiogroup' : 'menu'
            }
            accessibilityLabel={group.heading}
            style={{
              borderTopWidth: groupIndex === 0 ? 0 : spacing.size['border-width'],
              borderTopColor: colors.sem.border.subtle,
              paddingTop: groupIndex === 0 ? 0 : spacing.stack.sm,
            }}
          >
            {group.heading ? (
              <AppText variant="overline" tone="muted" accessibilityRole="header">
                {group.heading}
              </AppText>
            ) : null}
            {group.items.map((item) => (
              <MenuRow
                key={item.key}
                item={item}
                onSelect={() => {
                  setOpen(false);
                  item.onSelect?.();
                }}
              />
            ))}
          </View>
        ))}
      </Overlay>
    </>
  );
}

function MenuRow({ item, onSelect }: { readonly item: MenuItem; readonly onSelect: () => void }) {
  const { colors } = useTheme();
  const unavailable = item.unavailableReason !== undefined;
  const busy = item.busyLabel !== undefined;
  const role = item.kind === 'link' ? 'link' : item.kind === 'radio' ? 'radio' : 'menuitem';
  return (
    <View>
      <PressableBox
        accessibilityRole={role}
        accessibilityLabel={busy ? item.busyLabel : item.label}
        accessibilityHint={item.unavailableReason ?? item.description}
        accessibilityState={{
          disabled: unavailable || busy,
          checked: item.kind === 'radio' ? Boolean(item.checked) : undefined,
          busy,
        }}
        onPress={() => {
          if (!unavailable && !busy) onSelect();
        }}
        style={(pressed) => ({
          minHeight: spacing.size['touch-target-comfortable'],
          paddingVertical: spacing.stack.sm,
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.inline.md,
          backgroundColor: item.checked
            ? colors.sem.surface.selected
            : pressed
              ? colors.sem.surface.hover
              : undefined,
          borderRadius: radius.control,
          paddingHorizontal: spacing.inline.sm,
        })}
      >
        {busy ? <ActivityIndicator size="small" color={colors.sem.text.secondary} /> : null}
        <View style={{ flex: 1, gap: spacing.stack.xs }}>
          <AppText variant="control" tone={unavailable ? 'disabled' : 'primary'}>
            {busy ? item.busyLabel : item.label}
          </AppText>
          {item.description ? (
            <AppText variant="helper" tone="muted">
              {item.description}
            </AppText>
          ) : null}
          {item.unavailableReason ? (
            <AppText variant="helper" tone="muted">
              {item.unavailableReason}
            </AppText>
          ) : null}
        </View>
        {item.checked ? <Glyph name="check" color={colors.sem.selection.indicator} /> : null}
      </PressableBox>
      {unavailable && item.fix ? (
        <Pressable
          accessibilityRole="link"
          accessibilityLabel={item.fix.label}
          onPress={item.fix.onPress}
          style={{
            minHeight: spacing.size['touch-target-min'],
            justifyContent: 'center',
            paddingHorizontal: spacing.inline.sm,
          }}
        >
          <AppText
            variant="label"
            color={colors.sem.text.link}
            style={{ textDecorationLine: 'underline' }}
          >
            {item.fix.label}
          </AppText>
        </Pressable>
      ) : null}
    </View>
  );
}
