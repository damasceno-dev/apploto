import { describe, expect, it } from 'vitest';
import {
  evaluateMoneyExpression,
  MAX_CENTS,
  MAX_EXPRESSION_DEPTH,
  MAX_EXPRESSION_LENGTH,
  MAX_EXPRESSION_TOKENS,
  parseBrl,
} from '../src/index';
import { DIVIDE, integer, MINUS, NBSP, seeded, TIMES } from './support/random';

const ok = (cents: number, rounded = false, expression = true) => ({
  status: 'ok',
  cents,
  rounded,
  expression,
});
const invalid = (error: string, position: number | null = null) => ({
  status: 'invalid',
  error,
  position,
});
const evaluate = (text: string) => evaluateMoneyExpression(text);

/** A test-local, ungrouped pt-BR numeral for integer cents (independent of the formatters). */
const numeral = (cents: number) =>
  `${Math.floor(cents / 100)},${String(cents % 100).padStart(2, '0')}`;

describe('plain amounts and sums', () => {
  it.each([
    ['12,34', ok(1234, false, false)],
    ['0', ok(0, false, false)],
    ['0,00', ok(0, false, false)],
    ['1.234,56', ok(123456, false, false)],
    ['100+50+20', ok(17000)],
    ['100 + 50 + 20', ok(17000)],
    ['10,50 + 0,25', ok(1075)],
    ['1.000 + 1.000,01', ok(200001)],
    [`100 ${MINUS} 30`, ok(7000)],
    ['100 - 30', ok(7000)],
    [`2 ${TIMES} 3,50`, ok(700)],
    ['2 * 3,50', ok(700)],
    [`10 ${DIVIDE} 4`, ok(250)],
    ['10 / 4', ok(250)],
    [`${NBSP}5 +\t5 `, ok(1000)],
  ])('%j', (text, expected) => {
    expect(evaluate(text)).toEqual(expected);
  });

  it('uses the BRL parser grammar for every number', () => {
    for (const text of ['1.234,56', '1234,5', '1.500', '12', '0,05']) {
      const parsed = parseBrl(text);
      expect(parsed.ok).toBe(true);
      if (parsed.ok) expect(evaluate(text)).toEqual(ok(parsed.cents, false, false));
    }
  });
});

describe('precedence, parentheses and unary signs', () => {
  it.each([
    ['2 + 3 × 4', 1400],
    ['(2 + 3) × 4', 2000],
    ['10 − 2 − 3', 500], // left-associative: (10 − 2) − 3
    ['100 ÷ 10 ÷ 2', 500], // (100 ÷ 10) ÷ 2
    ['2 × (3 + (4 − 1))', 1200],
    ['((((5))))', 500],
    ['−5 + 8', 300],
    ['+5', 500],
    ['8 + −5', 300],
    ['8 − −5', 1300],
    ['2 × −3 + 10', 400],
    ['−(2 − 5)', 300],
    ['− 2 × − 3', 600],
  ])('%s = %i cents', (text, cents) => {
    const normalized = text.replaceAll('×', TIMES).replaceAll('÷', DIVIDE).replaceAll('−', MINUS);
    expect(evaluate(normalized)).toEqual(ok(cents));
  });
});

describe('exact division and final rounding (half away from zero, once)', () => {
  it.each([
    ['10 / 3', 333, true], // 3,3333… → 3,33
    ['20 / 3', 667, true], // 6,6666… → 6,67
    ['0,05 / 2', 3, true], // 0,025 → 0,03 (half away from zero)
    ['0,01 / 2', 1, true], // 0,005 → 0,01
    ['0,01 / 3', 0, true], // 0,0033… → 0,00
    ['1 / 8', 13, true], // 0,125 → 0,13
    ['10 / 3 * 3', 1000, false], // exact intermediate: no rounding before the end
    ['1 / 3 + 1 / 3 + 1 / 3', 100, false],
    ['100 / 7 * 7', 10000, false],
  ])('%s → %i cents (rounded %s)', (text, cents, rounded) => {
    expect(evaluate(text)).toEqual(ok(cents, rounded));
  });

  it('rounds a negative half away from zero when negatives are allowed', () => {
    expect(evaluateMoneyExpression('0 - 0,05 / 2', { allowNegative: true })).toEqual(ok(-3, true));
    expect(evaluateMoneyExpression('-10 / 3', { allowNegative: true })).toEqual(ok(-333, true));
  });

  it('agrees with an integer oracle on random sums, products and quotients (seeded)', () => {
    const random = seeded(2026);
    for (let run = 0; run < 1500; run += 1) {
      const a = integer(random, 0, 50_000_000_000_000);
      const b = integer(random, 0, 49_999_999_999_999);
      const k = integer(random, 1, 999);
      expect(evaluate(`${numeral(a)} + ${numeral(b)}`)).toEqual(ok(a + b));
      if (a >= b) expect(evaluate(`${numeral(a)} - ${numeral(b)}`)).toEqual(ok(a - b));
      const small = integer(random, 0, 99_999_999_999);
      expect(evaluate(`${numeral(small)} * ${k}`)).toEqual(ok(small * k));
      // Quotient oracle: integer division with remainder, halves away from zero.
      const quotient = Math.floor(a / k);
      const remainder = a % k;
      expect(evaluate(`${numeral(a)} / ${k}`)).toEqual(
        ok(remainder * 2 >= k ? quotient + 1 : quotient, remainder !== 0),
      );
    }
  });
});

describe('typed outcomes: empty, in progress, rejected', () => {
  it('keeps blank, in-progress text and a typed zero distinct', () => {
    expect(evaluate('')).toEqual({ status: 'empty' });
    expect(evaluate('   ')).toEqual({ status: 'empty' });
    expect(evaluate('0')).toEqual(ok(0, false, false));
    for (const text of [
      '100 +',
      '100+',
      '(5',
      '(5 + 3',
      '12,',
      '1.',
      '1.23',
      '1.234,',
      '5 × (',
      '−',
      '+',
      '(',
    ]) {
      expect(evaluate(text.replaceAll('×', TIMES).replaceAll('−', MINUS))).toEqual({
        status: 'incomplete',
      });
    }
  });

  it.each([
    ['100 + R$ 5', 'unsupported-character', 6],
    ['2^3', 'unsupported-character', 1],
    ['10 % 3', 'unsupported-character', 3],
    ['2 x 3', 'unsupported-character', 2],
    ['1e3', 'unsupported-character', 1],
    ['alert(1)', 'unsupported-character', 0],
    ['Math.PI', 'unsupported-character', 0],
    ['1;2', 'unsupported-character', 1],
    ['1.5 + 2', 'malformed-number', 0],
    ['1234.56', 'malformed-number', 0],
    ['12,345 + 1', 'malformed-number', 0],
    ['1,2,3', 'malformed-number', 0],
    [',5 + 1', 'malformed-number', 0],
    ['1.23 + 4', 'malformed-number', 0],
    ['1 2', 'unexpected-token', 2],
    ['2(3)', 'unexpected-token', 1],
    ['()', 'unbalanced-parentheses', 1],
    ['5)', 'unbalanced-parentheses', 1],
    ['(5))', 'unbalanced-parentheses', 3],
    ['* 5', 'unexpected-token', 0],
    ['5 + * 3', 'unexpected-token', 4],
    ['10 / 0', 'division-by-zero', 3],
    ['10 / (2 - 2)', 'division-by-zero', 3],
    ['1 - 2', 'negative-result', null],
    ['1.000.000.000.000', 'out-of-range', 0],
    ['999.999.999.999,99 + 0,01', 'out-of-range', null],
    ['999999999999 * 2', 'out-of-range', null],
  ])('%j → %s', (text, error, position) => {
    expect(evaluate(text)).toEqual(invalid(error, position));
  });

  it('accepts the maximum magnitude and rejects one cent more', () => {
    expect(evaluate('999.999.999.999,99')).toEqual(ok(MAX_CENTS, false, false));
    expect(evaluate('999.999.999.999,98 + 0,01')).toEqual(ok(MAX_CENTS));
    expect(evaluate('999.999.999.999,99 + 0,01')).toEqual(invalid('out-of-range'));
    expect(evaluateMoneyExpression('0 - 999.999.999.999,99', { allowNegative: true })).toEqual(
      ok(-MAX_CENTS),
    );
    // Intermediate values may exceed the range when the result fits: arithmetic is exact.
    expect(evaluate('(999.999.999.999 * 1.000) / 1.000')).toEqual(ok(99_999_999_999_900));
  });

  it('allows a zero result from a negative expression without a sign (no −0)', () => {
    const result = evaluate('5 - 5');
    expect(result).toEqual(ok(0));
    if (result.status === 'ok') expect(Object.is(result.cents, 0)).toBe(true);
  });
});

describe('malicious and overlong input', () => {
  it('never executes anything', () => {
    let touched = false;
    const probe = globalThis as { __lotoProbe?: () => void };
    probe.__lotoProbe = () => {
      touched = true;
    };
    for (const text of [
      '__lotoProbe()',
      'constructor.constructor("return 1")()',
      '[]+{}',
      '`${1}`',
      '1//2',
      '/*1*/2',
    ]) {
      expect(evaluate(text).status).toBe('invalid');
    }
    expect(touched).toBe(false);
    delete probe.__lotoProbe;
  });

  it('limits length, tokens and nesting depth', () => {
    expect(MAX_EXPRESSION_LENGTH).toBe(256);
    expect(evaluate('1'.repeat(MAX_EXPRESSION_LENGTH + 1))).toEqual(invalid('too-long'));
    const manyTokens = Array.from({ length: 70 }, () => '1').join('+'); // 139 tokens, 139 chars
    expect(manyTokens.length).toBeLessThanOrEqual(MAX_EXPRESSION_LENGTH);
    expect(evaluate(manyTokens)).toEqual(invalid('too-complex', MAX_EXPRESSION_TOKENS * 1));
    const deep = `${'('.repeat(MAX_EXPRESSION_DEPTH + 1)}1${')'.repeat(MAX_EXPRESSION_DEPTH + 1)}`;
    expect(evaluate(deep)).toEqual(invalid('too-complex', MAX_EXPRESSION_DEPTH));
    const atLimit = `${'('.repeat(MAX_EXPRESSION_DEPTH)}1${')'.repeat(MAX_EXPRESSION_DEPTH)}`;
    expect(evaluate(atLimit)).toEqual(ok(100));
    expect(evaluate(`${'-'.repeat(40)}1`)).toMatchObject({
      status: 'invalid',
      error: 'too-complex',
    });
  });

  it('bounds the size of exact intermediate values', () => {
    const huge = Array.from({ length: 12 }, () => '999999999999,99').join('*');
    expect(huge.length).toBeLessThanOrEqual(MAX_EXPRESSION_LENGTH);
    expect(evaluate(huge)).toEqual(invalid('too-complex'));
  });

  it('reports the UTF-16 position of an unsupported symbol, including astral characters', () => {
    expect(evaluate('10 + 😀')).toEqual(invalid('unsupported-character', 5));
    expect(evaluate('10 – 5')).toEqual(invalid('unsupported-character', 3)); // en dash is not a minus
  });
});
