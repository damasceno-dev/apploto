import { describe, expect, it } from 'vitest';
import {
  EM_DASH,
  formatCep,
  formatCnpj,
  formatCount,
  formatCpf,
  formatDecimal,
  formatDuration,
  formatIdentifier,
  formatInstallment,
  formatOptional,
  formatPercent,
  formatPhone,
  formatSignedDuration,
  identifierDigits,
  isValidCnpj,
  isValidCpf,
  maskIdentifierInput,
} from '../src/index';
import { EM_DASH_CHAR, integer, MINUS, seeded } from './support/random';

describe('durations from decimal hours (§5)', () => {
  it.each([
    [7.33, '7h 20min'], // 439,8 → 440 (pinned)
    [0.25, '0h 15min'],
    [0, '0h 00min'],
    [8.5, '8h 30min'],
    [173.0833, '173h 05min'], // 10384,998 → 10385
    [0.75, '0h 45min'],
    [0.175, '0h 11min'], // exactly 10,5 min → 11; the binary product 0.175 × 60 is 10.499999999999998
    [1.0125, '1h 01min'], // exactly 60,75 min → 61
    [0.0083, '0h 00min'], // 0,498 min → 0
    [0.00833333, '0h 00min'],
    [24.999, '25h 00min'], // 1499,94 → 1500
    [1000, '1000h 00min'],
  ])('%d h → %s', (hours, text) => {
    expect(formatDuration(hours)).toBe(text);
  });

  it.each([
    [-1.5, `${MINUS}1h 30min`], // pinned
    [2.25, '+2h 15min'],
    [-2.25, `${MINUS}2h 15min`],
    [0, '0h 00min'],
    [-0, '0h 00min'],
    [-0.0083, '0h 00min'], // rounds to zero minutes: no sign
    [-0.175, `${MINUS}0h 11min`], // halves away from zero, sign reapplied
    [0.175, '+0h 11min'],
  ])('signed %d h → %s', (hours, text) => {
    expect(formatSignedDuration(hours)).toBe(text);
  });

  it('is unsigned unless asked and rejects non-finite input', () => {
    expect(() => formatDuration(-1)).toThrow(RangeError);
    expect(formatDuration(-0)).toBe('0h 00min');
    for (const value of [NaN, Infinity])
      expect(() => formatSignedDuration(value)).toThrow(RangeError);
  });

  it('agrees with integer minute arithmetic on exact two-decimal hours (seeded)', () => {
    const random = seeded(99);
    for (let run = 0; run < 2000; run += 1) {
      const hundredths = integer(random, 0, 1_000_000); // hours × 100
      // minutes = hundredths × 60 / 100 = hundredths × 3 / 5, halves away from zero
      const quotient = Math.floor((hundredths * 3) / 5);
      const remainder = (hundredths * 3) % 5;
      const minutes = remainder * 2 >= 5 ? quotient + 1 : quotient;
      const expected = `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}min`;
      expect(formatDuration(hundredths / 100)).toBe(expected);
    }
  });
});

describe('counts, decimals, percentages, installments, absent (§7, §8)', () => {
  it('groups counts and keeps decimals exact', () => {
    expect(formatCount(3)).toBe('3');
    expect(formatCount(42)).toBe('42');
    expect(formatCount(1234)).toBe('1.234');
    expect(formatCount(1234567)).toBe('1.234.567');
    expect(formatCount(0)).toBe('0');
    expect(formatCount(-1234)).toBe(`${MINUS}1.234`);
    expect(() => formatCount(1.5)).toThrow(RangeError);
    expect(formatDecimal(1.5)).toBe('1,5');
    expect(formatDecimal(1234.5)).toBe('1.234,5');
    expect(formatDecimal(0.1)).toBe('0,1');
    expect(formatDecimal(-0.25)).toBe(`${MINUS}0,25`);
    expect(formatDecimal(2)).toBe('2');
    expect(formatDecimal(1.005, 2)).toBe('1,01'); // decimal text 1.005, not the binary 1.00499…
    expect(formatDecimal(1.25, 1)).toBe('1,3');
    expect(formatDecimal(-1.25, 1)).toBe(`${MINUS}1,3`);
    expect(formatDecimal(-0.04, 1)).toBe('0');
    expect(formatDecimal(1.2, 3)).toBe('1,2');
    expect(formatPercent(12.5)).toBe('12,5%');
    expect(formatPercent(100)).toBe('100%');
    expect(formatPercent(33.3333, 1)).toBe('33,3%');
    expect(formatInstallment(3, 12)).toBe('3/12');
    expect(() => formatInstallment(0, 12)).toThrow(RangeError);
    expect(() => formatInstallment(13, 12)).toThrow(RangeError);
  });

  it('dashes only absent values: zero is a value', () => {
    expect(EM_DASH).toBe(EM_DASH_CHAR);
    expect(EM_DASH.codePointAt(0)).toBe(0x2014);
    expect(formatOptional(null, formatCount)).toBe(EM_DASH_CHAR);
    expect(formatOptional(undefined, formatCount)).toBe(EM_DASH_CHAR);
    expect(formatOptional(0, formatCount)).toBe('0');
  });
});

describe('identity and contact masks (§6)', () => {
  it('formats exact digit counts and keeps everything else verbatim', () => {
    expect(formatCpf('12345678909')).toBe('123.456.789-09');
    expect(formatCpf('123.456.789-09')).toBe('123.456.789-09');
    expect(formatCpf(' 123 456 789 09 ')).toBe('123.456.789-09');
    expect(formatCnpj('12345678000195')).toBe('12.345.678/0001-95');
    expect(formatPhone('61912345678')).toBe('(61) 91234-5678');
    expect(formatPhone('6131234567')).toBe('(61) 3123-4567');
    expect(formatPhone('(61) 3123-4567')).toBe('(61) 3123-4567');
    expect(formatCep('70040010')).toBe('70040-010');
    for (const [kind, value] of [
      ['cpf', '1234567890'],
      ['cpf', '123.456.789-0'],
      ['cpf', '123456789012'],
      ['cpf', 'abc'],
      ['cpf', ''],
      ['cpf', '١٢٣٤٥٦٧٨٩٠٩'],
      ['cnpj', '12.ABC.345/01DE-35'], // alphanumeric CNPJ: not covered by §6, verbatim
      ['cnpj', '1234567800019'],
      ['phone', '+55 61 91234-5678'],
      ['phone', '912345678'],
      ['phone', 'ramal 204'],
      ['cep', '7004001'],
      ['cep', '70040-0100'],
    ] as const) {
      expect(formatIdentifier(kind, value)).toBe(value);
    }
  });

  it('submits digits by removing only mask punctuation', () => {
    expect(identifierDigits('123.456.789-09')).toBe('12345678909');
    expect(identifierDigits('(61) 91234-5678')).toBe('61912345678');
    expect(identifierDigits('12.345.678/0001-95')).toBe('12345678000195');
    expect(identifierDigits('12a.345')).toBe('12a345');
  });

  it('validates CPF check digits and rejects repeated digits', () => {
    for (const valid of ['12345678909', '123.456.789-09', '52998224725', '11144477735']) {
      expect(isValidCpf(valid)).toBe(true);
    }
    for (const invalid of [
      '12345678900',
      '52998224724',
      '1234567890',
      '123456789091',
      '',
      'abcdefghijk',
    ]) {
      expect(isValidCpf(invalid)).toBe(false);
    }
    for (let digit = 0; digit <= 9; digit += 1)
      expect(isValidCpf(String(digit).repeat(11))).toBe(false);
  });

  it('validates numeric CNPJ check digits and rejects repeated digits', () => {
    for (const valid of ['12345678000195', '11.222.333/0001-81', '11444777000161']) {
      expect(isValidCnpj(valid)).toBe(true);
    }
    for (const invalid of [
      '12345678000190',
      '11222333000180',
      '1234567800019',
      '12.ABC.345/01DE-35',
    ]) {
      expect(isValidCnpj(invalid)).toBe(false);
    }
    for (let digit = 0; digit <= 9; digit += 1)
      expect(isValidCnpj(String(digit).repeat(14))).toBe(false);
  });

  it('a single changed digit always breaks a valid CPF (seeded)', () => {
    const random = seeded(17);
    const base = '52998224725';
    for (let run = 0; run < 200; run += 1) {
      const index = integer(random, 0, 10);
      const replacement = String((Number(base[index]) + integer(random, 1, 9)) % 10);
      const mutated = base.slice(0, index) + replacement + base.slice(index + 1);
      expect(isValidCpf(mutated)).toBe(false);
    }
  });
});

describe('progressive input masks (§6: the input mask auto-punctuates)', () => {
  it.each([
    ['cpf', '5', '5'],
    ['cpf', '529', '529'],
    ['cpf', '5299', '529.9'],
    ['cpf', '529982', '529.982'],
    ['cpf', '5299822', '529.982.2'],
    ['cpf', '529982247', '529.982.247'],
    ['cpf', '5299822472', '529.982.247-2'],
    ['cpf', '52998224725', '529.982.247-25'],
    ['cpf', '529.982.2472', '529.982.247-2'], // re-punctuated after an edit in the middle
    ['cnpj', '112', '11.2'],
    ['cnpj', '11222333', '11.222.333'],
    ['cnpj', '112223330001', '11.222.333/0001'],
    ['cnpj', '1122233300018', '11.222.333/0001-8'],
    ['cep', '01310', '01310'],
    ['cep', '013101', '01310-1'],
    ['phone', '6', '(6'],
    ['phone', '61', '(61'],
    ['phone', '613', '(61) 3'],
    ['phone', '6131234', '(61) 3123-4'],
    ['phone', '6131234567', '(61) 3123-4567'],
    ['phone', '61912345678', '(61) 91234-5678'], // the 11th digit moves to the mobile shape
    ['cpf', '', ''],
    ['cpf', '.', ''],
    ['phone', '(', ''],
  ] as const)('%s %j → %j', (kind, typed, shown) => {
    expect(maskIdentifierInput(kind, typed)).toBe(shown);
  });

  it('keeps unsupported text and overlong values verbatim, never truncated', () => {
    for (const [kind, typed] of [
      ['cpf', '529982247251'],
      ['cpf', '529.982.247-251'],
      ['cpf', '52a'],
      ['cnpj', '12.ABC.345/01DE-35'],
      ['phone', '+55 61 91234-5678'],
      ['phone', '619123456789'],
      ['cep', '013101000'],
      ['cpf', '١٢٣'],
    ] as const) {
      expect(maskIdentifierInput(kind, typed)).toBe(typed);
    }
  });

  it('a complete value is exactly the display mask, and masking is idempotent (seeded)', () => {
    const random = seeded(23);
    const sizes = { cpf: 11, cnpj: 14, cep: 8, phone: 11 } as const;
    for (let run = 0; run < 400; run += 1) {
      for (const kind of ['cpf', 'cnpj', 'cep', 'phone'] as const) {
        const length = integer(random, 1, sizes[kind]);
        const digits = Array.from({ length }, () => String(integer(random, 0, 9))).join('');
        const shown = maskIdentifierInput(kind, digits);
        expect(identifierDigits(shown)).toBe(digits);
        expect(maskIdentifierInput(kind, shown)).toBe(shown);
        if (length === sizes[kind] || (kind === 'phone' && length === 10)) {
          expect(shown).toBe(formatIdentifier(kind, digits));
        }
      }
    }
  });
});
