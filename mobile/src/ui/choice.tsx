import { createContext, useContext, useEffect, useRef, type ReactNode } from 'react';
import { Pressable, Switch as NativeSwitch, View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import { FocusRing, useFocusState } from './focus';
import { useFieldControl } from './form-field';
import { ReadOnlyValue } from './input';
import { AppText } from './text';

const box = 20;

interface ChoiceRowProps {
  readonly label: string;
  readonly description?: string;
  readonly disabled: boolean;
  readonly onPress?: () => void;
  readonly accessibilityRole: 'checkbox' | 'radio' | 'switch';
  readonly checked: boolean | 'mixed';
  readonly hint?: string;
  readonly control: ReactNode;
  /** The label stays the accessible name but is not drawn (a table's selection column). */
  readonly labelHidden?: boolean;
  readonly testID?: string;
}

/** The whole label row is the hit area (choice-controls.md touch rules). */
function ChoiceRow({
  label,
  description,
  disabled,
  onPress,
  accessibilityRole,
  checked,
  hint,
  control,
  labelHidden = false,
  testID,
}: ChoiceRowProps) {
  const [focused, handlers] = useFocusState();
  return (
    <Pressable
      testID={testID}
      accessibilityRole={accessibilityRole}
      accessibilityLabel={label}
      accessibilityHint={hint}
      accessibilityState={{ checked, disabled }}
      onPress={() => {
        if (!disabled) onPress?.();
      }}
      {...handlers}
      style={{
        flexDirection: 'row',
        alignItems: 'flex-start',
        gap: spacing.inline.md,
        minHeight: spacing.size['touch-target-min'],
        paddingVertical: spacing.inline.sm,
      }}
    >
      <View style={{ paddingTop: 2 }}>
        {control}
        <FocusRing visible={focused} />
      </View>
      {labelHidden ? null : (
        <View style={{ flex: 1, gap: spacing.stack.xs }}>
          <AppText variant="control" tone={disabled ? 'disabled' : 'primary'}>
            {label}
          </AppText>
          {description ? (
            <AppText variant="helper" tone="muted">
              {description}
            </AppText>
          ) : null}
        </View>
      )}
    </Pressable>
  );
}

export interface CheckboxProps {
  readonly label: string;
  readonly description?: string;
  readonly checked: boolean;
  /** Group selection (some, not all): a bar mark; pressing selects all. */
  readonly indeterminate?: boolean;
  readonly onCheckedChange: (checked: boolean) => void;
  readonly disabled?: boolean;
  readonly invalid?: boolean;
  readonly readOnly?: boolean;
  /** Draw only the box; the label remains the accessible name (table selection column). */
  readonly labelHidden?: boolean;
  readonly testID?: string;
}

export function Checkbox({
  label,
  description,
  checked,
  indeterminate = false,
  onCheckedChange,
  labelHidden = false,
  testID,
  ...own
}: CheckboxProps) {
  const control = useFieldControl({ ...own, accessibilityLabel: label });
  const { colors } = useTheme();
  if (control.readOnly)
    return <ReadOnlyValue value={`${label}: ${checked ? 'Sim' : 'Não'}`} testID={testID} />;
  const on = checked || indeterminate;
  const disabled = control.disabled;
  return (
    <ChoiceRow
      testID={testID}
      label={label}
      description={description}
      disabled={disabled}
      accessibilityRole="checkbox"
      checked={indeterminate ? 'mixed' : checked}
      labelHidden={labelHidden}
      hint={control.accessibilityHint}
      onPress={() => onCheckedChange(indeterminate ? true : !checked)}
      control={
        <View
          style={{
            width: box,
            height: box,
            borderRadius: radius.xs,
            borderWidth: spacing.size['border-width'] + 1,
            borderColor: disabled
              ? colors.sem.action.disabled.border
              : control.invalid
                ? colors.sem.border['control-invalid']
                : on
                  ? colors.sem.selection.indicator
                  : colors.sem.border.control,
            backgroundColor: disabled
              ? colors.sem.action.disabled.bg
              : on
                ? colors.sem.selection.indicator
                : colors.sem.surface.default,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {indeterminate ? (
            <View
              style={{
                width: 10,
                height: 2,
                backgroundColor: disabled
                  ? colors.sem.action.disabled.fg
                  : colors.sem.selection['indicator-fg'],
              }}
            />
          ) : checked ? (
            <AppText
              variant="label"
              color={
                disabled ? colors.sem.action.disabled.fg : colors.sem.selection['indicator-fg']
              }
              style={{ lineHeight: 16, fontSize: 14 }}
            >
              {'✓'}
            </AppText>
          ) : null}
        </View>
      }
    />
  );
}

interface RadioContextValue {
  readonly value: string | null;
  readonly select: (value: string) => void;
  readonly disabled: boolean;
  readonly invalid: boolean;
}
const RadioContext = createContext<RadioContextValue | null>(null);

/**
 * A single choice with **no default** unless the screen passes one: `value` null renders nothing
 * selected, so the form cannot silently send a choice the user did not make.
 */
export function RadioGroup({
  value,
  onValueChange,
  children,
  testID,
  ...own
}: {
  readonly value: string | null;
  readonly onValueChange: (value: string) => void;
  readonly children: ReactNode;
  readonly disabled?: boolean;
  readonly invalid?: boolean;
  readonly readOnly?: boolean;
  readonly readOnlyText?: string;
  readonly accessibilityLabel?: string;
  readonly testID?: string;
}) {
  const control = useFieldControl(own);
  if (control.readOnly)
    return (
      <ReadOnlyValue value={own.readOnlyText ?? value ?? '—'} label={control.accessibilityLabel} />
    );
  return (
    <RadioContext.Provider
      value={{ value, select: onValueChange, disabled: control.disabled, invalid: control.invalid }}
    >
      <View
        testID={testID}
        accessibilityRole="radiogroup"
        accessibilityLabel={control.accessibilityLabel}
        accessibilityHint={control.accessibilityHint}
      >
        {children}
      </View>
    </RadioContext.Provider>
  );
}

export function Radio({
  value,
  label,
  description,
  disabled: own = false,
  testID,
}: {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
  readonly disabled?: boolean;
  readonly testID?: string;
}) {
  const group = useContext(RadioContext);
  const { colors } = useTheme();
  if (group === null) throw new Error('Radio requires RadioGroup.');
  const selected = group.value === value;
  const disabled = own || group.disabled;
  return (
    <ChoiceRow
      testID={testID}
      label={label}
      description={description}
      disabled={disabled}
      accessibilityRole="radio"
      checked={selected}
      onPress={() => group.select(value)}
      control={
        <View
          style={{
            width: box,
            height: box,
            borderRadius: radius.full,
            borderWidth: spacing.size['border-width'] + 1,
            borderColor: disabled
              ? colors.sem.action.disabled.border
              : group.invalid
                ? colors.sem.border['control-invalid']
                : selected
                  ? colors.sem.selection.indicator
                  : colors.sem.border.control,
            backgroundColor: disabled ? colors.sem.action.disabled.bg : colors.sem.surface.default,
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          {selected ? (
            <View
              style={{
                width: 10,
                height: 10,
                borderRadius: radius.full,
                backgroundColor: disabled
                  ? colors.sem.action.disabled.fg
                  : colors.sem.selection.indicator,
              }}
            />
          ) : null}
        </View>
      }
    />
  );
}

/**
 * An immediately applied, reversible device preference only — never a form value, a financial or
 * permission change. The state is also written (**Ativado** / **Desativado**).
 */
export function Switch({
  label,
  description,
  value,
  onValueChange,
  disabled: ownDisabled,
  disabledReason,
  testID,
}: {
  readonly label: string;
  readonly description?: string;
  readonly value: boolean;
  readonly onValueChange: (value: boolean) => void;
  readonly disabled?: boolean;
  readonly disabledReason?: string;
  readonly testID?: string;
}) {
  const control = useFieldControl({ disabled: ownDisabled, accessibilityLabel: label });
  const { colors } = useTheme();
  const disabled = control.disabled;
  const toggle = useRef<NativeSwitch>(null);
  const { registerFocus } = control;
  useEffect(() => {
    registerFocus(() => toggle.current?.focus?.());
    return () => registerFocus(null);
  }, [registerFocus]);
  return (
    <View
      style={{
        flexDirection: 'row',
        alignItems: 'center',
        gap: spacing.inline.md,
        minHeight: spacing.size['touch-target-min'],
      }}
    >
      <View
        style={{ flex: 1, gap: spacing.stack.xs }}
        importantForAccessibility="no-hide-descendants"
        accessibilityElementsHidden
      >
        <AppText variant="control" tone={disabled ? 'disabled' : 'primary'}>
          {label}
        </AppText>
        <AppText variant="helper" tone="muted">
          {value ? 'Ativado' : 'Desativado'}
          {description ? ` · ${description}` : ''}
        </AppText>
        {disabled && disabledReason ? (
          <AppText variant="helper" tone="muted">
            {disabledReason}
          </AppText>
        ) : null}
      </View>
      <NativeSwitch
        ref={toggle}
        testID={testID}
        value={value}
        onValueChange={(next) => {
          if (!disabled) onValueChange(next);
        }}
        disabled={disabled}
        accessibilityLabel={label}
        accessibilityHint={disabled ? disabledReason : description}
        accessibilityRole="switch"
        accessibilityState={{ checked: value, disabled }}
        trackColor={{ false: colors.sem.border.control, true: colors.sem.selection.indicator }}
        thumbColor={value ? colors.sem.selection['indicator-fg'] : colors.sem.surface.default}
        ios_backgroundColor={colors.sem.border.control}
      />
    </View>
  );
}
