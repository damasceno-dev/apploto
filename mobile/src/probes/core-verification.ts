// App-owned portable cases. Executes on the calling VM; no test-runner/platform imports.
import * as c from '@loto/core';
import labelReference from './label-reference.json';

type Reading = { name: string; actual: unknown; expected: unknown; pass: boolean };
export function verifyCore() {
  const readings: Reading[] = [];
  const eq = (name: string, actual: unknown, expected: unknown) => {
    readings.push({
      name,
      actual,
      expected,
      pass: JSON.stringify(actual) === JSON.stringify(expected),
    });
  };
  const throws = (name: string, action: () => unknown) => {
    let actual = 'did not throw';
    try {
      action();
    } catch (error) {
      actual = error instanceof RangeError ? 'RangeError' : String(error);
    }
    eq(name, actual, 'RangeError');
  };
  const date = (s: string) => {
    const d = c.parseCalendarDate(s);
    if (!d) throw new Error(s);
    return d;
  };
  const cents = c.asCents;
  eq('wire max', c.centsFromWire(999999999999.99), 99999999999999);
  eq('wire from cents', c.wireFromCents(cents(3141592)), 31415.92);
  eq('wire shortest decimal', c.centsFromWire(0.29), 29);
  eq('wire validation', [c.isWireMoney(1.001), c.isWireMoney(1.01)], [false, true]);
  for (const v of [NaN, Infinity, 0.001, 1000000000000])
    throws(`wire rejects ${v}`, () => c.centsFromWire(v));
  throws('cent overflow', () => cents(100000000000000));
  throws('unsigned negative', () => c.formatBrl(cents(-1)));
  eq(
    'money constants',
    [c.MAX_CENTS, c.MAX_MONEY_DIGITS, c.NO_BREAK_SPACE.codePointAt(0), c.MINUS_SIGN.codePointAt(0)],
    [99999999999999, 14, 160, 8722],
  );
  eq('BRL', c.formatBrl(cents(123456)), 'R$\u00a01.234,56');
  eq('BRL wire', c.formatBrlWire(1234.56), 'R$\u00a01.234,56');
  eq('amount', c.formatBrlAmount(cents(123456)), '1.234,56');
  eq('signed cents', c.formatSignedBrl(cents(-1200)), '−R$\u00a012,00');
  eq('signed wire', c.formatSignedBrlWire(12), '+R$\u00a012,00');
  eq('zero', c.formatSignedBrlWire(-0), 'R$\u00a00,00');
  eq('spoken', c.spellBrl(cents(1401)), 'catorze reais e um centavo');
  eq('spoken signed', c.spellSignedBrl(cents(-1)), 'menos um centavo');
  eq('spoken million', c.spellBrl(cents(100000000)), 'um milhão de reais');
  eq('debt presentation', c.presentFiadoBalance(cents(29000)).text, 'R$\u00a0290,00 · em aberto');
  eq(
    'credit presentation',
    c.presentFiadoBalanceWire(-10).text,
    '+R$\u00a010,00 · crédito do cliente',
  );
  eq('paid presentation', c.presentFiadoBalanceWire(0).text, 'quitado');
  eq('paste', c.parseBrl(' R$\u202f1.234,56 '), { ok: true, cents: 123456 });
  eq('paste malformed', c.parseBrl('1.5'), { ok: false, error: 'malformed' });
  eq('digits max', c.normalizeMoneyDigits('999999999999999'), null);
  eq('digits normalized', c.normalizeMoneyDigits('001234'), '1234');
  eq('digits cents', c.centsFromMoneyDigits('1234'), 1234);
  eq('digits back', c.moneyDigitsFromCents(cents(1234)), '1234');
  eq('digits display', c.formatMoneyDigits('1234'), '12,34');
  for (const [input, value, rounded] of [
    ['0,10+0,20', 30, false],
    ['2+3*4', 1400, false],
    ['(2+3)×4', 2000, false],
    ['1/3+1/3+1/3', 100, false],
    ['1÷8', 13, true],
    ['-1/8', -13, true],
    ['999.999.999.999,99', 99999999999999, false],
  ] as const) {
    const result = c.evaluateMoneyExpression(input, { allowNegative: true });
    eq(`BigInt ${input}`, result.status === 'ok' ? [result.cents, result.rounded] : result, [
      value,
      rounded,
    ]);
  }
  for (const input of [
    '1/0',
    '1e3',
    '2x3',
    '2(3)',
    '1'.repeat(257),
    '('.repeat(33) + '1' + ')'.repeat(33),
    '999999999999*999999999999',
  ]) {
    eq(`parser rejects ${input.slice(0, 36)}`, c.evaluateMoneyExpression(input).status, 'invalid');
  }
  eq('parser empty', c.evaluateMoneyExpression('').status, 'empty');
  eq('parser incomplete', c.evaluateMoneyExpression('1+').status, 'incomplete');
  eq(
    'parser limits',
    [c.MAX_EXPRESSION_LENGTH, c.MAX_EXPRESSION_TOKENS, c.MAX_EXPRESSION_DEPTH],
    [256, 128, 32],
  );
  eq('calendar bounds', [c.MIN_CALENDAR_YEAR, c.MAX_CALENDAR_YEAR], [1, 9999]);
  eq('leap rule', [1900, 2000, 2024, 2100].map(c.isLeapYear), [false, true, true, false]);
  eq('invalid Gregorian', c.parseCalendarDate('1900-02-29'), null);
  eq('parts', c.calendarDateParts(date('2024-02-29')), { year: 2024, month: 2, day: 29 });
  eq('from parts', c.calendarDateFromParts(26, 1, 5), '0026-01-05');
  eq('month length', c.daysInMonth(2024, 2), 29);
  eq('leap boundary', c.addDays(date('2024-02-28'), 1), '2024-02-29');
  eq('clamped month', c.addMonths(date('2024-01-31'), 1), '2024-02-29');
  eq('days distance', c.daysBetween(date('2024-01-01'), date('2025-01-01')), 366);
  eq('compare', c.compareCalendarDates(date('2024-01-01'), date('2025-01-01')), -1);
  eq('weekday', c.weekdayOf(date('1970-01-01')), 4);
  throws('lower calendar edge', () => c.addDays(date('0001-01-01'), -1));
  throws('upper calendar edge', () => c.addDays(date('9999-12-31'), 1));
  eq('date', c.formatDate(date('0026-01-05')), '05/01/0026');
  eq('date input', c.parseDateInput('29/02/2024'), '2024-02-29');
  eq('long date', c.formatLongDate(date('2026-07-08')), '8 de julho de 2026');
  eq(
    'weekday date',
    c.formatDateWithWeekday(date('2026-09-24')),
    'quinta-feira, 24 de setembro de 2026',
  );
  eq('month year', c.formatMonthYear(2026, 3), 'março de 2026');
  eq('month field', c.formatMonthField(2026, 9), '09/2026');
  eq('month name', c.monthName(12), 'dezembro');
  eq('weekday name', c.weekdayName(2), 'terça-feira');
  eq('weekday short', c.weekdayShortName(2), 'ter.');
  eq('wall time', c.formatWallTime('23:59:59.999'), '23:59');
  eq('wall parse invalid', c.parseWallTime('24:00'), null);
  eq('wall datetime', c.formatWallDateTime('2026-07-08T14:30:59'), '08/07/2026 14:30');
  throws('wall rejects offset', () => c.formatWallDateTime('2026-07-08T14:30:00Z'));
  for (const [input, zone, expected] of [
    ['2026-07-08T17:30:00Z', 'America/Sao_Paulo', '08/07/2026 14:30'],
    ['2026-03-08T06:59:00Z', 'America/New_York', '08/03/2026 01:59'],
    ['2026-03-08T07:00:00Z', 'America/New_York', '08/03/2026 03:00'],
    ['2026-11-01T05:30:00Z', 'America/New_York', '01/11/2026 01:30'],
    ['2026-11-01T06:30:00Z', 'America/New_York', '01/11/2026 01:30'],
    ['2026-07-08T17:30:00Z', 'Asia/Kolkata', '08/07/2026 23:00'],
    ['2026-07-08T17:30:00Z', 'Pacific/Chatham', '09/07/2026 06:15'],
    ['0001-01-01T00:00:00Z', 'UTC', '01/01/0001 00:00'],
    ['1582-10-04T00:00:00Z', 'UTC', '04/10/1582 00:00'],
    ['1582-10-15T00:00:00Z', 'UTC', '15/10/1582 00:00'],
    ['1880-01-01T12:00:00Z', 'America/New_York', '01/01/1880 07:03'],
    ['9999-12-31T23:59:00Z', 'UTC', '31/12/9999 23:59'],
  ]) {
    try {
      eq(`instant ${input} ${zone}`, c.formatInstant(input!, zone!), expected);
      eq(
        `instant date ${zone} ${input}`,
        c.formatInstantDate(input!, zone!),
        expected!.slice(0, 10),
      );
      eq(`instant time ${zone} ${input}`, c.formatInstantTime(input!, zone!), expected!.slice(11));
    } catch (error) {
      eq(`instant ${input} ${zone}`, String(error), expected);
    }
  }
  throws('BC era rejected', () => c.formatInstant('0001-01-01T00:00:00Z', 'Etc/GMT+1'));
  throws('AD upper edge rejected', () => c.formatInstant('9999-12-31T23:00:00Z', 'Asia/Tokyo'));
  eq('instant offset required', c.parseInstant('2026-07-08T14:30:00'), null);
  eq('instant epoch', c.parseInstant('1970-01-01T00:00:00Z'), 0);
  eq('duration', c.formatDuration(7.33), '7h 20min');
  eq('duration half minute', c.formatDuration(0.175), '0h 11min');
  eq('signed duration', c.formatSignedDuration(-1.5), '−1h 30min');
  eq('count', c.formatCount(1234), '1.234');
  eq('decimal', c.formatDecimal(1.5), '1,5');
  eq('percent', c.formatPercent(12.5), '12,5%');
  eq('installment', c.formatInstallment(3, 12), '3/12');
  eq(
    'absent',
    [c.EM_DASH, c.formatOptional(null, String), c.formatOptional(0, String)],
    ['—', '—', '0'],
  );
  eq('CPF', c.formatCpf('52998224725'), '529.982.247-25');
  eq('CNPJ', c.formatCnpj('12345678000195'), '12.345.678/0001-95');
  eq('phone', c.formatPhone('6131234567'), '(61) 3123-4567');
  eq('CEP', c.formatCep('70040010'), '70040-010');
  eq('identifier', c.formatIdentifier('cpf', '52998224725'), '529.982.247-25');
  eq('progressive', c.maskIdentifierInput('cpf', '5299'), '529.9');
  eq('passthrough', c.formatPhone('ramal 12'), 'ramal 12');
  eq('digits', c.identifierDigits('529.982.247-25'), '52998224725');
  eq(
    'CPF checks',
    [c.isValidCpf('52998224725'), c.isValidCpf('11111111111'), c.isValidCpf('52998224726')],
    [true, false, false],
  );
  eq(
    'CNPJ checks',
    [c.isValidCnpj('12345678000195'), c.isValidCnpj('11111111111111')],
    [true, false],
  );
  eq('enum groups', Object.keys(c.enumLabels).sort(), Object.keys(labelReference).sort());
  for (const [name, expected] of Object.entries(labelReference)) {
    const map = c.enumLabels[name as keyof typeof c.enumLabels];
    eq(`enum ${name}`, map, expected);
    const exportName = `${name.charAt(0).toLowerCase()}${name.slice(1)}Labels`;
    eq(`public map ${exportName}`, (c as unknown as Record<string, unknown>)[exportName], expected);
    for (const [value, label] of Object.entries(expected))
      eq(`${name} known ${value}`, c.enumLabel(map as Readonly<Record<string, string>>, value), {
        known: true,
        label,
      });
    for (const key of ['__proto__', 'toString', 'FutureValue'])
      eq(`${name} unknown ${key}`, c.enumLabel(map as Readonly<Record<string, string>>, key), {
        known: false,
        label: key,
      });
  }
  eq('movement marker', c.generatedMovementMarker, 'Automática');
  return { total: readings.length, passed: readings.filter((r) => r.pass).length, readings };
}
