import { useEffect, useRef, useState } from 'react';
import { TextInput, View } from 'react-native';
import {
  evaluateMoneyExpression,
  formatBrl,
  formatSignedBrl,
  moneyDigitsFromCents,
  asCents,
  centsFromMoneyDigits,
  MAX_EXPRESSION_LENGTH,
  type MoneyExpressionError,
  type MoneyExpressionResult,
} from '@loto/core';
import { useTheme } from '@/theme/provider';
import { spacing, textStyle } from '@/theme/style';
import { AppText, Button, MoneyInput, Radio, RadioGroup, useFieldControl } from '@/ui';
import { FocusRing } from '@/ui/focus';
import { useControlChrome } from '@/ui/input';
import { useRevealFocused } from '@/ui/screen';
import type { Digits } from './contracts';

/**
 * Design 4.9.7, mobile behavior (money-input.md, "Arithmetic entry"): the number pad has no
 * operators, so **Calcular** opens an inline expression buffer with its own operator keys. The
 * evaluated amount is shown before an explicit **Usar R$ …**; only that resulting number reaches the
 * field and the API. Nothing is executed: the public core parser is the only evaluator.
 */
export const expressionErrors: Record<MoneyExpressionError, string> = {
  'too-long': 'A conta é longa demais.',
  'too-complex': 'A conta é complexa demais.',
  'unsupported-character': 'Use só números, vírgula, + − × ÷ e parênteses.',
  'malformed-number': 'Confira o número digitado: use vírgula para os centavos (12,50).',
  'unexpected-token': 'Confira a conta: há um operador ou parêntese fora do lugar.',
  'unbalanced-parentheses': 'Confira os parênteses da conta.',
  'division-by-zero': 'Não é possível dividir por zero.',
  'out-of-range': 'O resultado passa do valor máximo aceito.',
  'negative-result': 'O resultado não pode ser negativo neste campo.',
};
const keys = [
  { text: '+', label: 'mais' },
  { text: '−', label: 'menos' },
  { text: '×', label: 'vezes' },
  { text: '÷', label: 'dividido por' },
  { text: '(', label: 'abre parênteses' },
  { text: ')', label: 'fecha parênteses' },
  { text: ',', label: 'vírgula' },
] as const;
/** The accepted magnitude/sign of a result; null while it cannot be accepted. */
export function acceptedResult(
  result: MoneyExpressionResult,
): { digits: Digits; negative: boolean } | null {
  if (result.status !== 'ok') return null;
  return {
    digits: moneyDigitsFromCents(asCents(Math.abs(result.cents))),
    negative: result.cents < 0,
  };
}
export function previewText(result: MoneyExpressionResult, signed: boolean): string {
  switch (result.status) {
    case 'empty':
      return 'Digite a conta, por exemplo 100 + 50 + 20.';
    case 'incomplete':
      return 'Continue digitando a conta.';
    case 'invalid':
      return expressionErrors[result.error];
    case 'ok':
      return `Resultado: ${signed ? formatSignedBrl(result.cents) : formatBrl(result.cents)}${result.rounded ? ' (arredondado para centavos)' : ''}`;
  }
}

export interface CountMoneyFieldProps {
  readonly label: string;
  readonly value: Digits;
  readonly onValueChange: (digits: Digits) => void;
  /** Signed amount variant (Relatório do terminal): the sign lives beside the field. */
  readonly sign?: { readonly negative: boolean; readonly onNegativeChange: (n: boolean) => void };
  /** The retained raw buffer; `undefined` when no calculation is open. */
  readonly buffer: string | undefined;
  readonly onBufferChange: (raw: string | undefined) => void;
  /** Close-form fields have no blank state; a cleared field reads as zero. */
  readonly placeholderZero?: boolean;
  readonly disabled?: boolean;
  readonly readOnly?: boolean;
  readonly testID: string;
}
export function CountMoneyField({
  label,
  value,
  onValueChange,
  sign,
  buffer,
  onBufferChange,
  placeholderZero = false,
  disabled = false,
  readOnly = false,
  testID,
}: CountMoneyFieldProps) {
  const { colors } = useTheme();
  const control = useFieldControl({ disabled, readOnly });
  const locked = control.disabled || control.readOnly;
  const result =
    buffer === undefined ? null : evaluateMoneyExpression(buffer, { allowNegative: !!sign });
  const accepted = result ? acceptedResult(result) : null;
  const input = useRef<TextInput>(null);
  const [focused, setFocused] = useState(false);
  // Keys edit the buffer from outside the native field: remount it with the new text and refocus,
  // so the native text never diverges from the buffer (reproduced on iOS while typing on).
  const [edits, setEdits] = useState(0);
  const reveal = useRevealFocused();
  useEffect(() => {
    if (edits === 0) return;
    input.current?.focus();
    // Focus moved inside an already open keyboard: bring the buffer back above it (Android).
    const timer = setTimeout(reveal, 50);
    return () => clearTimeout(timer);
  }, [edits, reveal]);
  const chrome = useControlChrome({
    focused,
    invalid: result?.status === 'invalid',
    disabled: control.disabled,
  });
  const shown = placeholderZero && value === '' ? '0' : value;
  const signText = sign
    ? sign.negative && shown !== '' && centsFromMoneyDigits(shown) !== 0
      ? 'menos '
      : ''
    : '';
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <MoneyInput
        testID={testID}
        value={shown}
        accessibilityLabel={sign ? `${label}, ${signText || 'positivo'}` : label}
        readOnly={control.readOnly}
        disabled={control.disabled}
        onValueChange={(digits) => onValueChange(digits)}
        onNegativePaste={
          sign
            ? (digits) => {
                onValueChange(digits);
                sign.onNegativeChange(true);
              }
            : undefined
        }
      />
      {sign ? (
        <RadioGroup
          testID={`${testID}-sign`}
          accessibilityLabel={`Sinal do ${label}`}
          value={sign.negative ? 'negative' : 'positive'}
          onValueChange={(next) => sign.onNegativeChange(next === 'negative')}
          disabled={locked}
        >
          <Radio value="positive" label="Positivo" testID={`${testID}-positive`} />
          <Radio value="negative" label="Negativo" testID={`${testID}-negative`} />
        </RadioGroup>
      ) : null}
      {!locked && buffer === undefined ? (
        <Button
          label="Calcular"
          variant="link"
          accessibilityLabel={`Calcular ${label}`}
          testID={`${testID}-calc`}
          onPress={() => {
            onBufferChange('');
            setTimeout(() => input.current?.focus(), 0);
          }}
        />
      ) : null}
      {buffer !== undefined ? (
        <View
          testID={`${testID}-calculator`}
          style={{ gap: spacing.stack.xs }}
          accessibilityLabel={`Conta para ${label}`}
        >
          <View
            accessibilityRole="toolbar"
            style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}
          >
            {keys.map((key) => (
              <Button
                key={key.text}
                label={key.text}
                variant="secondary"
                accessibilityLabel={key.label}
                testID={`${testID}-key-${key.label.replace(/\s/g, '-')}`}
                disabled={locked || buffer.length >= MAX_EXPRESSION_LENGTH}
                onPress={() => {
                  onBufferChange(buffer + key.text);
                  setEdits((n) => n + 1);
                }}
              />
            ))}
            <Button
              label="⌫"
              variant="secondary"
              accessibilityLabel="apagar"
              testID={`${testID}-key-apagar`}
              disabled={locked || buffer.length === 0}
              onPress={() => {
                onBufferChange(buffer.slice(0, -1));
                setEdits((n) => n + 1);
              }}
            />
          </View>
          <AppText
            variant="helper"
            testID={`${testID}-preview`}
            accessibilityLiveRegion="polite"
            tone="muted"
            color={result?.status === 'invalid' ? colors.sem.feedback.danger.fg : undefined}
          >
            {result ? previewText(result, !!sign) : ''}
          </AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
            <Button
              label={
                accepted && result?.status === 'ok'
                  ? `Usar ${sign ? formatSignedBrl(result.cents) : formatBrl(result.cents)}`
                  : 'Usar resultado'
              }
              testID={`${testID}-accept`}
              disabled={locked || !accepted}
              disabledReason={accepted ? undefined : 'A conta ainda não tem um resultado válido.'}
              onPress={() => {
                if (!accepted) return;
                onValueChange(accepted.digits);
                if (sign) sign.onNegativeChange(accepted.negative);
                onBufferChange(undefined);
              }}
            />
            <Button
              label="Descartar conta"
              variant="link"
              testID={`${testID}-discard`}
              disabled={locked}
              onPress={() => onBufferChange(undefined)}
            />
          </View>
          <View style={chrome.container}>
            <TextInput
              key={edits}
              ref={input}
              testID={`${testID}-expression`}
              value={buffer}
              editable={!locked}
              // Digits and the decimal comma from the device keypad. The operator keys, the result and
              // Usar sit above the buffer, so the on-screen keypad never hides them (seen on iOS).
              keyboardType="decimal-pad"
              maxLength={MAX_EXPRESSION_LENGTH}
              placeholder="100 + 50 + 20"
              placeholderTextColor={chrome.placeholder}
              selectionColor={chrome.selection}
              accessibilityLabel={`Conta de ${label}`}
              accessibilityHint="O resultado aparece acima e só é usado ao tocar em Usar."
              onFocus={() => {
                setFocused(true);
                setTimeout(reveal, 50);
              }}
              onBlur={() => setFocused(false)}
              onChangeText={(raw) => onBufferChange(raw)}
              allowFontScaling
              style={[
                chrome.text,
                textStyle('money'),
                { color: chrome.text.color, textAlign: 'right' },
              ]}
            />
            <FocusRing visible={focused} />
          </View>
        </View>
      ) : null}
    </View>
  );
}
