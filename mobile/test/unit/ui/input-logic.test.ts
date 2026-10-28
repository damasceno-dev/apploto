import { formatMoneyDigits, maskIdentifierInput, MAX_MONEY_DIGITS } from '@loto/core';
import {
  caretAfterDigits,
  displayMoneyDigits,
  editIdentifier,
  editMoneyDigits,
  identifierSubmitValue,
  editMoneyText,
} from '../../../src/ui/input-logic';

const end = (text: string) => ({ start: text.length, end: text.length });

/** Simulates typing `chars` one by one at the end, as the native field reports each change. */
function typeIdentifier(kind: 'cpf' | 'cnpj' | 'phone' | 'cep', chars: string) {
  let text = '';
  for (const char of chars) text = editIdentifier(kind, text, text + char, end(text)).text;
  return text;
}

describe('progressive identifier masks', () => {
  test('punctuate as digits arrive and equal the display mask when complete', () => {
    expect(typeIdentifier('cpf', '5299')).toBe('529.9');
    expect(typeIdentifier('cpf', '52998224725')).toBe('529.982.247-25');
    expect(typeIdentifier('cnpj', '11222333000181')).toBe('11.222.333/0001-81');
    expect(typeIdentifier('cep', '01310100')).toBe('01310-100');
    expect(typeIdentifier('phone', '1133334444')).toBe('(11) 3333-4444');
    expect(typeIdentifier('phone', '11912345678')).toBe('(11) 91234-5678');
  });

  test('backspace over a generated separator deletes the neighbouring digit, caret after it', () => {
    const previous = '529.982';
    // Caret right after the dot: Backspace removes the dot → the digit before it goes instead.
    const result = editIdentifier('cpf', previous, '529982', { start: 4, end: 4 });
    expect(result.text).toBe('529.82');
    expect(result.text).toBe(maskIdentifierInput('cpf', '52982'));
    expect(result.caret).toBe(caretAfterDigits(result.text, 2));
  });

  test('forward delete over a separator deletes the following digit', () => {
    const result = editIdentifier('cpf', '529.982', '529982', { start: 3, end: 3 });
    expect(result.text).toBe(maskIdentifierInput('cpf', '52982'));
  });

  test('removing an invalid character or editing passthrough text keeps every digit', () => {
    // `529.98a` is passthrough (a letter); deleting the letter must not also eat the 8.
    expect(editIdentifier('cpf', '529.98a', '529.98', end('529.98a')).text).toBe('529.98');
    // An overlong CPF stays verbatim; removing a dot keeps all 12 digits.
    const overlong = '529.982.247-251';
    const removed = editIdentifier('cpf', overlong, '529982.247-251', { start: 4, end: 4 });
    expect(removed.text.replace(/\D/g, '')).toBe('529982247251');
    // Alphanumeric CNPJ is not in the contract: verbatim, and deleting its slash removes only it.
    const alpha = '12.ABC.345/01DE-35';
    expect(editIdentifier('cnpj', alpha, '12.ABC.34501DE-35', { start: 11, end: 11 }).text).toBe(
      '12.ABC.34501DE-35',
    );
  });

  test('a mid-value insertion keeps the caret after the inserted digit', () => {
    const result = editIdentifier('cpf', '529.982.247', '5291.982.247', { start: 3, end: 3 });
    expect(result.text).toBe(maskIdentifierInput('cpf', '5291982247'));
    expect(result.caret).toBe(caretAfterDigits(result.text, 4));
  });

  test('submit value strips mask punctuation only for a well-formed mask', () => {
    expect(identifierSubmitValue('cpf', '529.982.247-25')).toBe('52998224725');
    expect(identifierSubmitValue('cpf', '529.98a')).toBe('529.98a');
  });

  test('arbitrary digit strings type into exactly the core mask', () => {
    let seed = 7;
    const next = () => (seed = (seed * 1103515245 + 12345) % 2147483648);
    for (let run = 0; run < 300; run += 1) {
      const digits = Array.from({ length: (next() % 11) + 1 }, () => String(next() % 10)).join('');
      expect(typeIdentifier('cpf', digits)).toBe(maskIdentifierInput('cpf', digits));
    }
  });
});

describe('money entry', () => {
  const type = (current: string, char: string) =>
    editMoneyDigits(current, displayMoneyDigits(current), displayMoneyDigits(current) + char);

  test('blank and recorded zero stay distinct', () => {
    expect(displayMoneyDigits('')).toBe('');
    expect(type('', '0')).toBe('0');
    expect(displayMoneyDigits('0')).toBe('0,00');
    // Deleting the last digit returns to blank, not to zero.
    expect(editMoneyDigits('0', '0,00', '0,0')).toBe('');
    expect(editMoneyDigits('5', '0,05', '0,0')).toBe('');
  });

  test('digits build cents right to left; deletion removes the last cent digit', () => {
    let state = '';
    for (const char of '1234') state = type(state, char);
    expect(state).toBe('1234');
    expect(displayMoneyDigits(state)).toBe('12,34');
    expect(editMoneyDigits(state, '12,34', '12,3')).toBe('123');
    expect(type('0', '5')).toBe('5');
  });

  test('the 15th digit is refused and non-digits are ignored', () => {
    const full = '9'.repeat(MAX_MONEY_DIGITS);
    expect(type(full, '9')).toBe(full);
    expect(type('12', ',')).toBe('12');
    expect(type('12', '-')).toBe('12');
  });

  test('pastes parse through core; signed, malformed and out-of-range pastes are ignored', () => {
    expect(editMoneyDigits('', '', 'R$ 31.415,92')).toBe('3141592');
    expect(editMoneyDigits('12', '0,12', '0,12R$ 1.234,56')).toBe('123456');
    expect(editMoneyDigits('12', '0,12', '0,12-5,00')).toBe('12');
    expect(editMoneyDigits('12', '0,12', '0,121.5')).toBe('12');
    expect(editMoneyDigits('12', '0,12', '0,121234567890123')).toBe('12');
  });

  test('arbitrary typed values round-trip through the displayed text', () => {
    let seed = 11;
    const next = () => (seed = (seed * 1103515245 + 12345) % 2147483648);
    for (let run = 0; run < 200; run += 1) {
      const digits = String(next())
        .slice(0, (next() % 13) + 1)
        .replace(/^0+(?=\d)/, '');
      let state = '';
      for (const char of digits) state = type(state, char);
      expect(state).toBe(digits);
      expect(displayMoneyDigits(state)).toBe(formatMoneyDigits(digits));
    }
  });
});

describe('native money changes (editMoneyText)', () => {
  /** Replays native reports; the field is re-rendered only when `render(step)` is true. */
  function replay(reports: readonly string[], render: (step: number) => boolean, start = '') {
    let value = start; // the prop the field was last rendered with
    let previous = start; // the last entry the field emitted
    reports.forEach((raw, step) => {
      previous = editMoneyText(previous, displayMoneyDigits(value), raw);
      if (render(step)) value = previous;
    });
    return previous;
  }

  test('fast typing outrunning re-renders builds 1234, never a paste (reproduced on iOS 26.5)', () => {
    expect(replay(['1', '12', '123', '1234'], () => false)).toBe('1234');
    expect(replay(['1', '0,012', '0,123', '1,234'], () => true)).toBe('1234');
    expect(replay(['1', '12', '0,123', '1,234'], (step) => step % 2 === 1)).toBe('1234');
  });

  test('fast deletion outrunning re-renders reaches blank, losing no keystroke', () => {
    expect(replay(['12,3', '12,', '12', '1', ''], () => false, '1234')).toBe('');
    expect(replay(['12,3', '1,2', '0,1', '0,0'], () => true, '1234')).toBe('');
    expect(replay(['12,3', '12,', '0,1', '0,'], (step) => step === 1, '1234')).toBe('');
  });

  test('blank versus recorded zero, ignored separators, cap and paste', () => {
    expect(editMoneyText('', '', '0')).toBe('0');
    expect(editMoneyText('0', '0,00', '0,000')).toBe('0');
    expect(editMoneyText('0', '0,00', '0,0')).toBe('');
    expect(editMoneyText('12', '0,12', '0,12,')).toBe('12');
    const full = '9'.repeat(14);
    expect(editMoneyText(full, displayMoneyDigits(full), `${displayMoneyDigits(full)}9`)).toBe(
      full,
    );
    expect(editMoneyText('', '', 'R$ 31.415,92')).toBe('3141592');
    expect(editMoneyText('12', '0,12', '0,12R$ 1,00')).toBe('100');
    expect(editMoneyText('12', '0,12', '0,12-5,00')).toBe('12');
  });

  test('a malformed change is rejected, keeping the previous entry', () => {
    const malformed = [
      ...['-1', '+1', 'abc1', 'R$ -1,00', '1e3', 'R1', '$1', '1R', '1$', '1,,', '.,1'],
      ...['1.5', '1,2,3', '1|2', '|', '1 2', 'R$ 1.5'],
    ];
    for (const pasted of malformed)
      for (const previous of ['', '0', '1234']) {
        const displayed = displayMoneyDigits(previous);
        expect(editMoneyText(previous, displayed, pasted)).toBe(previous);
        expect(editMoneyText(previous, displayed, displayed + pasted)).toBe(previous);
      }
    expect(editMoneyText('', '', ' R$\u00a01.234,56 ')).toBe('123456');
    // The caret stays at the end, so text changed elsewhere is a replacement, not an edit.
    expect(editMoneyText('123456', '1.234,56', '.234,56')).toBe('123456');
    expect(editMoneyText('123456', '1.234,56', '1.234,5')).toBe('12345');
    expect(editMoneyText('123456', '1.234,56', '1.234,567')).toBe('1234567');
    // Whole replacements of R$ 12,34: malformed ones keep it, well-formed ones parse.
    for (const replacement of ['1.5', '1,2,3', '1|2', '12,3,4'])
      expect(editMoneyText('1234', '12,34', replacement)).toBe('1234');
    // …including separators the shown amount already has.
    for (const [previous, replacement] of [
      ['123456', '1.5'],
      ['87654321', '1.5'],
      ['87654321', '8.76'],
      ['87654321', '876.5,4'],
      ['123456', '1.2345'],
    ] as const)
      expect(editMoneyText(previous, displayMoneyDigits(previous), replacement)).toBe(previous);
    expect(editMoneyText('1234', '12,34', '1.234,56')).toBe('123456');
    expect(editMoneyText('1234', '12,34', ' 5,00 ')).toBe('500');
    expect(editMoneyText('1234', '12,34', 'R$ 7')).toBe('700');
  });
});
