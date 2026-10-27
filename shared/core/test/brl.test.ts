import { describe, expect, it } from 'vitest';
import {
  asCents,
  centsFromMoneyDigits,
  centsFromWire,
  formatBrl,
  formatBrlAmount,
  formatBrlWire,
  formatMoneyDigits,
  formatSignedBrl,
  formatSignedBrlWire,
  isWireMoney,
  MAX_CENTS,
  MAX_MONEY_DIGITS,
  MINUS_SIGN,
  moneyDigitsFromCents,
  NO_BREAK_SPACE,
  normalizeMoneyDigits,
  parseBrl,
  wireFromCents,
} from '../src/index';
import { integer, MINUS, NBSP, NNBSP, seeded } from './support/random';

const c = asCents;

describe('code points (formatting §2, §3, §9)', () => {
  it('uses U+00A0 after R$ and U+2212 for the display minus, never ASCII look-alikes', () => {
    expect(NO_BREAK_SPACE.codePointAt(0)).toBe(0xa0);
    expect(MINUS_SIGN.codePointAt(0)).toBe(0x2212);
    const unsigned = formatBrl(c(123456));
    expect(unsigned).toBe(`R$${NBSP}1.234,56`);
    expect(unsigned.codePointAt(2)).toBe(0xa0);
    expect(unsigned).not.toContain(' ');
    const negative = formatSignedBrl(c(-1200));
    expect(negative).toBe(`${MINUS}R$${NBSP}12,00`);
    expect(negative.codePointAt(0)).toBe(0x2212);
    expect(negative).not.toContain('-');
  });
});

describe('unsigned display (§2)', () => {
  it.each([
    [0, '0,00'],
    [5, '0,05'],
    [50, '0,50'],
    [500, '5,00'],
    [123456, '1.234,56'],
    [123456789, '1.234.567,89'],
    [100000, '1.000,00'],
    [99999999999999, '999.999.999.999,99'],
  ])('%i cents → R$ %s', (cents, amount) => {
    expect(formatBrlAmount(c(cents))).toBe(amount);
    expect(formatBrl(c(cents))).toBe(`R$${NBSP}${amount}`);
  });

  it('refuses to hide a sign: a negative unsigned amount throws', () => {
    expect(() => formatBrl(c(-1))).toThrow(RangeError);
    expect(() => formatBrlAmount(c(-1))).toThrow(RangeError);
  });

  it('formats the wire number exactly, including values that are inexact as binary doubles', () => {
    expect(formatBrlWire(12.34)).toBe(`R$${NBSP}12,34`);
    expect(formatBrlWire(0.1)).toBe(`R$${NBSP}0,10`);
    expect(formatBrlWire(5)).toBe(`R$${NBSP}5,00`);
    expect(formatBrlWire(999999999999.99)).toBe(`R$${NBSP}999.999.999.999,99`);
  });
});

describe('signed display (§3)', () => {
  it.each([
    [1200, `+R$${NBSP}12,00`],
    [-1200, `${MINUS}R$${NBSP}12,00`],
    [0, `R$${NBSP}0,00`],
    [-0, `R$${NBSP}0,00`],
    [-2000, `${MINUS}R$${NBSP}20,00`],
    [-99999999999999, `${MINUS}R$${NBSP}999.999.999.999,99`],
  ])('%i → %s', (cents, text) => {
    expect(formatSignedBrl(c(cents))).toBe(text);
  });

  it('puts the sign before the symbol and gives zero no sign, also from the wire', () => {
    expect(formatSignedBrlWire(-12)).toBe(`${MINUS}R$${NBSP}12,00`);
    expect(formatSignedBrlWire(-0)).toBe(`R$${NBSP}0,00`);
    expect(formatSignedBrlWire(0.01)).toBe(`+R$${NBSP}0,01`);
  });
});

describe('wire ↔ cents boundary (numeric(14,2))', () => {
  it('converts exactly at two places and round-trips through JSON', () => {
    const random = seeded(7);
    for (let run = 0; run < 2000; run += 1) {
      const cents = integer(random, -MAX_CENTS, MAX_CENTS);
      const wire = wireFromCents(c(cents));
      // The decimal text of the JSON number is the cents with the point two places in.
      const sign = cents < 0 ? '-' : '';
      const digits = String(Math.abs(cents)).padStart(3, '0');
      const expected = `${sign}${digits.slice(0, -2)}.${digits.slice(-2)}`;
      expect(Number(JSON.stringify(wire))).toBe(Number(expected));
      expect(JSON.parse(JSON.stringify(wire))).toBe(wire);
      expect(centsFromWire(JSON.parse(JSON.stringify(wire)) as number)).toBe(
        cents === 0 ? 0 : cents,
      );
    }
  });

  it('reads the wire value from its decimal text, not by multiplying the double', () => {
    // 1.005 * 100 is 100.49999999999999 in binary; the wire value 1.005 is not numeric(14,2) at all.
    expect(isWireMoney(1.005)).toBe(false);
    expect(() => centsFromWire(1.005)).toThrow(RangeError);
    expect(centsFromWire(0.29)).toBe(29); // 0.29 * 100 = 28.999999999999996
    expect(centsFromWire(1.1)).toBe(110);
    expect(centsFromWire(4.35)).toBe(435); // 4.35 * 100 = 434.99999999999994
  });

  it('rejects contract violations instead of rounding them into a figure', () => {
    for (const value of [NaN, Infinity, -Infinity, 0.001, 12.345, 1e12, -1e12, 1e21, 1.5e-7]) {
      expect(isWireMoney(value)).toBe(false);
      expect(() => centsFromWire(value)).toThrow(RangeError);
    }
    expect(centsFromWire(999999999999.99)).toBe(MAX_CENTS);
    expect(centsFromWire(-999999999999.99)).toBe(-MAX_CENTS);
    expect(Object.is(centsFromWire(-0), 0)).toBe(true);
  });

  it('validates integer cents', () => {
    expect(() => asCents(1.5)).toThrow(RangeError);
    expect(() => asCents(MAX_CENTS + 1)).toThrow(RangeError);
    expect(() => asCents(Number.MAX_SAFE_INTEGER + 1)).toThrow(RangeError);
    expect(Object.is(asCents(-0), 0)).toBe(true);
    expect(Object.is(wireFromCents(c(-0)), 0)).toBe(true);
  });
});

describe('parseBrl — paste and typed text (§2 input)', () => {
  it.each([
    ['R$ 1.234,56', 123456],
    [`R$${NBSP}1.234,56`, 123456],
    [`R$${NNBSP}12,34`, 1234],
    ['R$1.234,56', 123456],
    ['  1.234,56  ', 123456],
    ['1234,56', 123456],
    ['1.234', 123400],
    ['1.234.567', 123456700],
    ['12', 1200],
    ['12,5', 1250],
    ['0,00', 0],
    ['0', 0],
    ['007,10', 710],
    ['999.999.999.999,99', MAX_CENTS],
    ['999999999999,99', MAX_CENTS],
  ])('%j → %i cents', (text, cents) => {
    expect(parseBrl(text)).toEqual({ ok: true, cents });
  });

  it.each([
    ['', 'empty'],
    ['   ', 'empty'],
    [NBSP, 'empty'],
    ['-12,00', 'signed'],
    [`${MINUS}R$${NBSP}12,00`, 'signed'],
    ['+R$ 12,00', 'signed'],
    ['R$ -12,00', 'signed'],
    ['1.000.000.000.000,00', 'out-of-range'],
    ['1000000000000', 'out-of-range'],
    ['1.5', 'malformed'],
    ['1234.56', 'malformed'],
    ['1.23,45', 'malformed'],
    ['12,345', 'malformed'],
    ['12,', 'malformed'],
    [',50', 'malformed'],
    ['1 234,56', 'malformed'],
    ['R$', 'malformed'],
    ['r$ 12,00', 'malformed'],
    ['US$ 12,00', 'malformed'],
    ['12,00 R$', 'malformed'],
    ['1e3', 'malformed'],
    ['١٢,٣٤', 'malformed'], // Arabic-Indic digits are not ASCII digits
    ['12,00abc', 'malformed'],
  ])('%j → %s', (text, error) => {
    expect(parseBrl(text)).toEqual({ ok: false, error });
  });

  it('round-trips every formatted amount (seeded)', () => {
    const random = seeded(11);
    for (let run = 0; run < 2000; run += 1) {
      const cents = integer(random, 0, MAX_CENTS);
      expect(parseBrl(formatBrl(c(cents)))).toEqual({ ok: true, cents });
    }
  });
});

describe('MoneyInput entry digits', () => {
  it('normalizes leading zeros, keeps a recorded zero and caps at 14 digits', () => {
    expect(MAX_MONEY_DIGITS).toBe(14);
    expect(normalizeMoneyDigits('005')).toBe('5');
    expect(normalizeMoneyDigits('0')).toBe('0');
    expect(normalizeMoneyDigits('000')).toBe('0');
    expect(normalizeMoneyDigits('99999999999999')).toBe('99999999999999');
    expect(normalizeMoneyDigits('0099999999999999')).toBe('99999999999999');
    expect(normalizeMoneyDigits('100000000000000')).toBeNull();
    expect(normalizeMoneyDigits('')).toBeNull();
    expect(normalizeMoneyDigits('12a')).toBeNull();
    expect(normalizeMoneyDigits('-1')).toBeNull();
  });

  it('formats right-to-left digits without the unit', () => {
    expect(formatMoneyDigits('1')).toBe('0,01');
    expect(formatMoneyDigits('12')).toBe('0,12');
    expect(formatMoneyDigits('123')).toBe('1,23');
    expect(formatMoneyDigits('1234')).toBe('12,34');
    expect(formatMoneyDigits('0')).toBe('0,00');
    expect(formatMoneyDigits('99999999999999')).toBe('999.999.999.999,99');
    expect(() => formatMoneyDigits('100000000000000')).toThrow(RangeError);
  });

  it('converts between digits and cents', () => {
    expect(centsFromMoneyDigits('1234')).toBe(1234);
    expect(centsFromMoneyDigits('0')).toBe(0);
    expect(moneyDigitsFromCents(c(1234))).toBe('1234');
    expect(moneyDigitsFromCents(c(0))).toBe('0');
    expect(() => moneyDigitsFromCents(c(-1))).toThrow(RangeError);
  });
});
