import { centsFromMoneyDigits, formatBrl, spellBrl } from '@loto/core';
import { forwardRef, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { TextInput, View } from 'react-native';
import { spacing, textStyle } from '@/theme/style';
import { FocusRing } from './focus';
import { useFieldControl } from './form-field';
import { ReadOnlyValue, useControlChrome, type FieldControlProps, type InputHandle } from './input';
import { displayMoneyDigits, editMoneyText } from './input-logic';
import { AppText } from './text';

export const BLANK_MONEY_LABEL = 'não informado';
const provenanceLabels = {
  calculated: 'calculado pelo sistema',
  informed: 'informado pelo operador',
} as const;

export interface MoneyInputProps extends FieldControlProps {
  /** Exact entry state: `''` blank (**não informado**), `'0'` a recorded zero, else cent digits. */
  readonly value: string;
  readonly onValueChange: (digits: string) => void;
  /** Read-only amounts name where the figure came from (copy §7). */
  readonly provenance?: keyof typeof provenanceLabels;
}

/** The spoken value: money words from core, **não informado** for blank, **zero reais** for zero. */
export function spokenMoney(digits: string): string {
  return digits === '' ? BLANK_MONEY_LABEL : spellBrl(centsFromMoneyDigits(digits));
}

/**
 * MoneyInput (money-input.md): cents typed right to left on the number pad, blank ≠ zero, the
 * `R$` unit a fixed adornment hidden from assistive tech, the caret kept at the end. Parsing and
 * formatting are core's; the component holds no float arithmetic. Read-only and disabled never
 * change the value — not by typing, deletion or paste.
 */
export const MoneyInput = forwardRef<InputHandle, MoneyInputProps>(function MoneyInput(
  { value, onValueChange, provenance, testID, ...own },
  ref,
) {
  const control = useFieldControl(own);
  const input = useRef<TextInput>(null);
  const [focused, setFocused] = useState(false);
  const chrome = useControlChrome({
    focused,
    invalid: control.invalid,
    disabled: control.disabled,
  });
  const { registerFocus } = control;
  useImperativeHandle(ref, () => ({ focus: () => input.current?.focus() }), []);
  useEffect(() => {
    registerFocus(() => input.current?.focus());
    return () => registerFocus(null);
  }, [registerFocus]);

  const displayed = displayMoneyDigits(value);
  // The last entry emitted while the prop still read `from` (keystrokes outrun re-renders).
  const emitted = useRef<{ from: string; digits: string } | null>(null);
  if (control.readOnly) {
    const text = value === '' ? BLANK_MONEY_LABEL : formatBrl(centsFromMoneyDigits(value));
    return (
      <View style={{ gap: spacing.stack.xs }}>
        <ReadOnlyValue
          value={text}
          label={control.accessibilityLabel}
          align="right"
          role="money"
          testID={testID}
        />
        {provenance ? (
          <AppText variant="helper" tone="muted" align="right">
            {provenanceLabels[provenance]}
          </AppText>
        ) : null}
      </View>
    );
  }
  return (
    <View style={chrome.container}>
      <AppText
        variant="money"
        tone="muted"
        accessibilityElementsHidden
        importantForAccessibility="no-hide-descendants"
        style={{ paddingLeft: spacing.inset['control-x'] }}
      >
        R$
      </AppText>
      <TextInput
        ref={input}
        testID={testID}
        value={displayed}
        // Caret pinned to the end only while editing: an unfocused Android EditText given a
        // selection asks its ScrollView to bring the caret on screen and pins the scroll.
        selection={focused ? { start: displayed.length, end: displayed.length } : undefined}
        editable={!control.disabled}
        keyboardType="number-pad"
        contextMenuHidden={false}
        placeholder={BLANK_MONEY_LABEL}
        placeholderTextColor={chrome.placeholder}
        selectionColor={chrome.selection}
        accessibilityLabel={control.accessibilityLabel}
        accessibilityHint={control.accessibilityHint}
        accessibilityValue={{ text: spokenMoney(value) }}
        accessibilityState={{ disabled: control.disabled }}
        aria-required={control.required}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onChangeText={(raw) => {
          if (control.disabled) return;
          // Compare with what this field last emitted: the prop can lag behind fast keystrokes.
          const previous = emitted.current?.from === value ? emitted.current.digits : value;
          const next = editMoneyText(previous, displayed, raw);
          emitted.current = { from: value, digits: next };
          if (next !== value) onValueChange(next);
        }}
        allowFontScaling
        style={[chrome.text, textStyle('money'), { color: chrome.text.color, textAlign: 'right' }]}
      />
      <FocusRing visible={focused} />
    </View>
  );
});
