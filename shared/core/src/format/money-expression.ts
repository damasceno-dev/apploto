// Restricted arithmetic money input (server M9.6 Phase 8, plan A9). Access let operators type
// `100+50+20` in a value field and evaluated it with `Eval`; this is the safe replacement. It is a
// tokenizer and a recursive-descent evaluator over exact rationals — nothing is ever executed — and
// it is INPUT arithmetic only: the operator's own typed figures, never ledger, receivable or report
// data. The screen shows the evaluated amount before acceptance and sends only the resulting number.
//
// Grammar (whitespace allowed between tokens, never inside a number):
//   expression := term (('+' | '−') term)*
//   term       := unary (('×' | '÷') unary)*
//   unary      := ('+' | '−') unary | primary
//   primary    := number | '(' expression ')'
//   number     := pt-BR numeral, the BRL parser's grammar: digits with optional dot grouping in
//                 threes and an optional comma with 1–2 decimals (`1.234,56`, `1234,5`, `1.500`)
// Aliases for keys a physical keyboard has: `-` (hyphen-minus) for `−`, `*` for `×`, `/` for `÷`.
// Precedence: unary signs, then × ÷, then + −; binary operators are left-associative. Every
// intermediate value is exact; only the final value is rounded to cents, halves away from zero.
import { asCents, centsOfNumeral, MAX_CENTS, type Cents } from './brl';

/** Longest accepted text, in UTF-16 code units. */
export const MAX_EXPRESSION_LENGTH = 256;
/** Most tokens (numbers, operators, parentheses). */
export const MAX_EXPRESSION_TOKENS = 128;
/** Deepest nesting of parentheses and unary signs together. */
export const MAX_EXPRESSION_DEPTH = 32;
/** Largest numerator/denominator during evaluation, in bits; beyond it the input is too complex. */
const MAX_BITS = 512;

export type MoneyExpressionError =
  | 'too-long'
  | 'too-complex'
  | 'unsupported-character'
  | 'malformed-number'
  | 'unexpected-token'
  | 'unbalanced-parentheses'
  | 'division-by-zero'
  | 'out-of-range'
  | 'negative-result';

export type MoneyExpressionResult =
  /** Nothing typed (blank or whitespace): distinct from a typed zero. */
  | { readonly status: 'empty' }
  /** Text that can still become valid by typing more (`100 +`, `(5`, `12,`, `1.23`). */
  | { readonly status: 'incomplete' }
  /** Rejected; `position` is the UTF-16 index of the offending token, when there is one. */
  | {
      readonly status: 'invalid';
      readonly error: MoneyExpressionError;
      readonly position: number | null;
    }
  | {
      readonly status: 'ok';
      /** The accepted amount in integer cents, within numeric(14,2). */
      readonly cents: Cents;
      /** True when the exact value had more than two decimals and was rounded. */
      readonly rounded: boolean;
      /**
       * True when the text was an expression (an operator, sign or parenthesis), not a plain
       * amount: the screen shows the evaluated amount before accepting it.
       */
      readonly expression: boolean;
    };

export interface MoneyExpressionOptions {
  /** Money fields are unsigned (MoneyInput has no signed variant), so a negative result is rejected unless allowed. */
  readonly allowNegative?: boolean;
}

type Operator = '+' | '-' | '*' | '/';
type Token =
  | { readonly kind: 'number'; readonly value: Rational; readonly position: number }
  | { readonly kind: 'partial-number'; readonly position: number }
  | { readonly kind: 'operator'; readonly operator: Operator; readonly position: number }
  | { readonly kind: '(' | ')'; readonly position: number };

interface Rational {
  readonly n: bigint;
  readonly d: bigint;
}

const ZERO = BigInt(0);
const ONE = BigInt(1);
const TWO = BigInt(2);
const HUNDRED = BigInt(100);
const LIMIT = TWO ** BigInt(MAX_BITS);

// Thrown internally to end evaluation with an outcome; never escapes `evaluateMoneyExpression`.
class Stop {
  readonly result: Extract<MoneyExpressionResult, { status: 'incomplete' | 'invalid' }>;
  constructor(result: Stop['result']) {
    this.result = result;
  }
}

const invalid = (error: MoneyExpressionError, position: number | null) =>
  new Stop({ status: 'invalid', error, position });
const incomplete = () => new Stop({ status: 'incomplete' });

const operators: Readonly<Record<string, Operator>> = {
  '+': '+',
  '\u2212': '-',
  '-': '-',
  '\u00D7': '*',
  '*': '*',
  '\u00F7': '/',
  '/': '/',
};
const numeralChar = /[\d.,]/;
const whitespace = /\s/u;
// A numeral that more typing can still complete: `12,`, `1.`, `1.23`, `1.234,`.
const partialNumeral = /^(?:\d+,|\d{1,3}(?:\.\d{3})*\.\d{0,2}|\d{1,3}(?:\.\d{3})+,)$/;

function tokenize(text: string): Token[] {
  const tokens: Token[] = [];
  let index = 0;
  while (index < text.length) {
    const char = text[index] as string;
    if (whitespace.test(char)) {
      index += 1;
      continue;
    }
    if (tokens.length >= MAX_EXPRESSION_TOKENS) throw invalid('too-complex', index);
    const operator = operators[char];
    if (operator !== undefined) {
      tokens.push({ kind: 'operator', operator, position: index });
      index += 1;
    } else if (char === '(' || char === ')') {
      tokens.push({ kind: char, position: index });
      index += 1;
    } else if (numeralChar.test(char)) {
      const start = index;
      while (index < text.length && numeralChar.test(text[index] as string)) index += 1;
      const numeral = text.slice(start, index);
      const atEnd = text.slice(index).trim().length === 0;
      const cents = centsOfNumeral(numeral);
      if (cents !== null) {
        if (cents > BigInt(MAX_CENTS)) throw invalid('out-of-range', start);
        tokens.push({ kind: 'number', value: reduce(cents, HUNDRED), position: start });
      } else if (atEnd && partialNumeral.test(numeral)) {
        tokens.push({ kind: 'partial-number', position: start });
      } else {
        throw invalid('malformed-number', start);
      }
    } else {
      throw invalid('unsupported-character', index);
    }
  }
  return tokens;
}

function gcd(a: bigint, b: bigint): bigint {
  let [x, y] = [a < ZERO ? -a : a, b];
  while (y !== ZERO) [x, y] = [y, x % y];
  return x;
}

function reduce(n: bigint, d: bigint): Rational {
  const divisor = gcd(n, d);
  const value = divisor === ZERO ? { n: ZERO, d: ONE } : { n: n / divisor, d: d / divisor };
  if ((value.n < ZERO ? -value.n : value.n) >= LIMIT || value.d >= LIMIT) {
    throw invalid('too-complex', null);
  }
  return value;
}

function apply(operator: Operator, a: Rational, b: Rational, position: number): Rational {
  switch (operator) {
    case '+':
      return reduce(a.n * b.d + b.n * a.d, a.d * b.d);
    case '-':
      return reduce(a.n * b.d - b.n * a.d, a.d * b.d);
    case '*':
      return reduce(a.n * b.n, a.d * b.d);
    case '/':
      if (b.n === ZERO) throw invalid('division-by-zero', position);
      return b.n < ZERO ? reduce(-a.n * b.d, a.d * -b.n) : reduce(a.n * b.d, a.d * b.n);
  }
}

class Parser {
  private readonly tokens: readonly Token[];
  private index = 0;
  usedExpression = false;

  constructor(tokens: readonly Token[]) {
    this.tokens = tokens;
  }

  parse(): Rational {
    const value = this.expression(0);
    const extra = this.tokens[this.index];
    if (extra !== undefined) {
      throw invalid(
        extra.kind === ')' ? 'unbalanced-parentheses' : 'unexpected-token',
        extra.position,
      );
    }
    return value;
  }

  private peekOperator(...wanted: Operator[]): Extract<Token, { kind: 'operator' }> | null {
    const token = this.tokens[this.index];
    return token?.kind === 'operator' && wanted.includes(token.operator) ? token : null;
  }

  private expression(depth: number): Rational {
    let value = this.term(depth);
    for (let op = this.peekOperator('+', '-'); op !== null; op = this.peekOperator('+', '-')) {
      this.index += 1;
      this.usedExpression = true;
      value = apply(op.operator, value, this.term(depth), op.position);
    }
    return value;
  }

  private term(depth: number): Rational {
    let value = this.unary(depth);
    for (let op = this.peekOperator('*', '/'); op !== null; op = this.peekOperator('*', '/')) {
      this.index += 1;
      this.usedExpression = true;
      value = apply(op.operator, value, this.unary(depth), op.position);
    }
    return value;
  }

  private unary(depth: number): Rational {
    const sign = this.peekOperator('+', '-');
    if (sign === null) return this.primary(depth);
    if (depth + 1 > MAX_EXPRESSION_DEPTH) throw invalid('too-complex', sign.position);
    this.index += 1;
    this.usedExpression = true;
    const value = this.unary(depth + 1);
    return sign.operator === '-' ? { n: -value.n, d: value.d } : value;
  }

  private primary(depth: number): Rational {
    const token = this.tokens[this.index];
    if (token === undefined || token.kind === 'partial-number') throw incomplete();
    if (token.kind === 'number') {
      this.index += 1;
      return token.value;
    }
    if (token.kind === '(') {
      if (depth + 1 > MAX_EXPRESSION_DEPTH) throw invalid('too-complex', token.position);
      this.index += 1;
      this.usedExpression = true;
      const value = this.expression(depth + 1);
      const close = this.tokens[this.index];
      if (close === undefined) throw incomplete();
      if (close.kind !== ')') throw invalid('unexpected-token', close.position);
      this.index += 1;
      return value;
    }
    throw invalid(
      token.kind === ')' ? 'unbalanced-parentheses' : 'unexpected-token',
      token.position,
    );
  }
}

/**
 * Evaluates typed money input: a plain amount (`12,34`) or an expression (`100 + 50 + 20`,
 * `(10,50 × 3) ÷ 2`). Returns typed outcomes and error codes; the app supplies pt-BR copy.
 */
export function evaluateMoneyExpression(
  text: string,
  options: MoneyExpressionOptions = {},
): MoneyExpressionResult {
  if (text.length > MAX_EXPRESSION_LENGTH)
    return { status: 'invalid', error: 'too-long', position: null };
  if (text.trim().length === 0) return { status: 'empty' };
  try {
    const parser = new Parser(tokenize(text));
    const value = parser.parse();
    // Final rounding to cents: |value| × 100, halves away from zero, sign reapplied.
    const negative = value.n < ZERO;
    const scaled = (negative ? -value.n : value.n) * HUNDRED;
    const quotient = scaled / value.d;
    const remainder = scaled % value.d;
    const magnitude = remainder * TWO >= value.d ? quotient + ONE : quotient;
    if (magnitude > BigInt(MAX_CENTS))
      return { status: 'invalid', error: 'out-of-range', position: null };
    const cents = Number(magnitude);
    if (negative && cents !== 0 && options.allowNegative !== true) {
      return { status: 'invalid', error: 'negative-result', position: null };
    }
    return {
      status: 'ok',
      cents: asCents(negative ? -cents : cents),
      rounded: remainder !== ZERO,
      expression: parser.usedExpression,
    };
  } catch (error) {
    if (error instanceof Stop) return error.result;
    throw error;
  }
}
