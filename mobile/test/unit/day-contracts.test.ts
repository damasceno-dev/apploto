import type {
  ResponseDailyCloseJson,
  ResponseDailyCloseReviewJson,
  ResponseListDailyCloseItemJson,
  ResponseSessionOpeningProposalJson,
} from '@loto/api';
import { evaluateMoneyExpression } from '@loto/core';
import {
  autosaveEligible,
  cashboxGrants,
  correctionContext,
  countRequest,
  digitsFromWire,
  endUndo,
  fieldCents,
  handoverAcceptance,
  isCountInput,
  isOpeningInput,
  isWorkstationChoice,
  nextAction,
  omittedOpeningProducts,
  openingAcknowledgment,
  repeatOpening,
  resolveWorkstation,
  sameCount,
  serverInput,
  sessionInput,
  undoRepeat,
  zeroAcknowledgmentRequired,
  type CountInput,
  type OpeningInput,
  type Read,
  type WorkstationFacts,
} from '../../src/day/contracts';
import { acceptedResult, previewText } from '../../src/day/arithmetic';

const uid = (n: number) => `70000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const me = uid(1);
const other = uid(2);
const cash = uid(10);
const raspa = uid(11);
const variance = uid(12);
const box = uid(20);
const box2 = uid(21);
const custody = (extra: Partial<ResponseDailyCloseJson['custody']> = {}) => ({
  mappingState: 'Ready' as const,
  origin: 'Started' as const,
  custodyState: 'Active' as const,
  sequence: 1,
  custodianOperatorId: me,
  custodianOperatorName: 'Eu',
  terminalId: uid(30),
  startedAt: '2026-11-05T11:00:00Z',
  countRevision: 1,
  ledgerRevision: 1,
  openingRevision: 1,
  openingAcknowledged: true,
  custodyHistoryComplete: true,
  ...extra,
});
const count = {
  confirmationToken: 'server-token',
  countRevision: 1,
  ledgerRevision: 1,
  openingRevision: 1,
  candidateState: 'NotConferred' as const,
  requiresStillObservedConfirmation: false,
};
const allowed = {
  canSaveCount: true,
  canConferir: false,
  canRecount: false,
  canSubmit: false,
  canRecall: false,
  canApprove: false,
  canApproveAsSingleManager: false,
  canReject: false,
  canReopen: false,
};
export const review = (
  extra: Partial<ResponseDailyCloseReviewJson> = {},
): ResponseDailyCloseReviewJson => ({
  id: uid(40),
  version: 3,
  date: '2026-11-05',
  status: 'Draft',
  accountId: box,
  accountName: 'Caixa 1',
  branchId: uid(50),
  openedByUserId: me,
  openedByUserName: 'Eu',
  createdAt: '2026-11-05T11:00:00Z',
  custody: custody(),
  count,
  allowedActions: allowed,
  closeFields: [
    { field: 'TerminalReport', value: -12.5, revision: 1 },
    { field: 'BolaoFee', value: 0, revision: 0 },
    { field: 'SpecialFee', value: 3, revision: 1 },
  ],
  items: [
    {
      productId: cash,
      productName: 'Dinheiro',
      displayOrder: 1,
      openingValue: 100,
      closingValue: null,
      isCashVarianceProduct: false,
    },
    {
      productId: raspa,
      productName: 'Raspadinha',
      displayOrder: 2,
      openingValue: 0,
      closingValue: 0,
      isCashVarianceProduct: false,
    },
    {
      productId: variance,
      productName: 'Diferença Caixa',
      displayOrder: 99,
      openingValue: null,
      closingValue: null,
      isCashVarianceProduct: true,
    },
  ],
  ...extra,
});

describe('count candidate', () => {
  it('keeps blank distinct from a recorded zero and reads signed close fields', () => {
    const input = serverInput(review());
    expect(input.items[cash]).toBe('');
    expect(input.items[raspa]).toBe('0');
    expect(input.items[variance]).toBeUndefined();
    expect(input.fields.TerminalReport).toEqual({ digits: '1250', negative: true });
    expect(fieldCents(input.fields.TerminalReport)).toBe(-1250);
    expect(fieldCents({ digits: '0', negative: true })).toBe(0);
    expect(fieldCents({ digits: '', negative: false })).toBe(0);
    expect(isCountInput(input)).toBe(true);
  });
  it('sends non-blank rows only, never the variance row, and only changed close fields', () => {
    const r = review();
    const ack = serverInput(r);
    const edited: CountInput = {
      ...ack,
      items: { ...ack.items, [cash]: '15050' },
      fields: { ...ack.fields, BolaoFee: { digits: '700', negative: false } },
    };
    expect(countRequest(r, edited, ack)).toEqual({
      items: [
        { productId: cash, value: 150.5 },
        { productId: raspa, value: 0 },
      ],
      notes: '',
      closeFields: [{ field: 'BolaoFee', value: 7 }],
    });
    // An untouched field is omitted (it keeps its server value); a blank row is omitted, not zero.
    const blank: CountInput = { ...ack, items: { ...ack.items, [raspa]: '' } };
    expect(countRequest(r, blank, ack)).toEqual({ items: [], notes: '' });
    // Clearing a fee field commits zero (it cancels the generated row).
    const cleared = {
      ...ack,
      fields: { ...ack.fields, SpecialFee: { digits: '', negative: false } },
    };
    expect(countRequest(r, cleared, ack).closeFields).toEqual([{ field: 'SpecialFee', value: 0 }]);
    // The sign alone is a change of the signed report.
    const positive = {
      ...ack,
      fields: { ...ack.fields, TerminalReport: { digits: '1250', negative: false } },
    };
    expect(countRequest(r, positive, ack).closeFields).toEqual([
      { field: 'TerminalReport', value: 12.5 },
    ]);
  });
  it('acknowledges a save response by exact product identity', () => {
    const r = review();
    const session = {
      ...r,
      items: [{ id: uid(60), productId: cash, productName: 'Dinheiro', value: 10, createdAt: 'x' }],
    } as unknown as ResponseDailyCloseJson;
    const ack = sessionInput(session, [cash, raspa]);
    expect(ack.items).toEqual({ [cash]: '1000', [raspa]: '' });
    expect(sameCount(ack, { ...ack, buffers: { [`item:${cash}`]: '1+1' } })).toBe(true);
    expect(sameCount(ack, { ...ack, notes: 'x' })).toBe(false);
  });
  it('rejects device candidates with secrets, unknown keys or malformed digits', () => {
    const input = serverInput(review());
    expect(isCountInput({ ...input, token: 'x' })).toBe(false);
    expect(isCountInput({ ...input, items: { [cash]: '01' } })).toBe(false);
    expect(isCountInput({ ...input, buffers: { 'item:x': '1' } })).toBe(false);
    expect(isCountInput({ ...input, notes: 'n'.repeat(1001) })).toBe(false);
  });
  it('autosaves only today’s plain Draft the caller may save', () => {
    const today = '2026-11-05';
    expect(autosaveEligible(review(), today)).toBe(true);
    // The API's business date-time at midnight is the same branch day.
    expect(autosaveEligible(review({ date: '2026-11-05T00:00:00' }), today)).toBe(true);
    expect(autosaveEligible(review({ date: '2026-11-04' }), today)).toBe(false);
    expect(autosaveEligible(review({ rejectionReason: 'x' }), today)).toBe(false);
    expect(autosaveEligible(review({ openingRecheckRequiredAt: 'x' }), today)).toBe(false);
    expect(autosaveEligible(review({ status: 'Rejected' }), today)).toBe(false);
    expect(
      autosaveEligible(review({ allowedActions: { ...allowed, canSaveCount: false } }), today),
    ).toBe(false);
    expect(correctionContext(review({ status: 'Rejected' }))).toBe(true);
    expect(correctionContext(review({ openingRecheckRequiredAt: 'x' }))).toBe(true);
    expect(correctionContext(review())).toBe(false);
  });
});

describe('Repetir abertura / Desfazer repetição', () => {
  it('keeps the exact pre-copy value, ignores a no-change repeat and is row-local', () => {
    let items: Record<string, string> = { [cash]: '', [raspa]: '12' };
    let undo = {};
    ({ items, undo } = repeatOpening(items, undo, cash, '10000'));
    expect(items[cash]).toBe('10000');
    expect(undo).toEqual({ [cash]: '' });
    // Repeating the same value again keeps the original blank as the undo target.
    const again = repeatOpening(items, undo, cash, '10000');
    expect(again.undo).toEqual({ [cash]: '' });
    expect(again.items).toBe(items);
    // A no-change repeat on a row already equal to the opening adds no undo.
    expect(repeatOpening({ [raspa]: '0' }, {}, raspa, '0').undo).toEqual({});
    ({ items, undo } = undoRepeat(items, undo, cash));
    expect(items).toEqual({ [cash]: '', [raspa]: '12' });
    expect(undo).toEqual({});
    expect(endUndo({ [cash]: '5', [raspa]: '' }, cash)).toEqual({ [raspa]: '' });
  });
});

describe('opening acknowledgment', () => {
  const proposal = (
    extra: Partial<ResponseSessionOpeningProposalJson> = {},
  ): ResponseSessionOpeningProposalJson => ({
    accountId: box,
    accountName: 'Caixa 1',
    terminalId: uid(30),
    sourceKind: 'PreviousSession',
    sourceId: uid(41),
    sourceRevision: 7,
    sourceDate: '2026-11-04',
    isMissing: false,
    hasUnresolvedHistory: false,
    canStart: true,
    items: [
      { productId: cash, productName: 'Dinheiro', displayOrder: 1, value: 100 },
      { productId: raspa, productName: 'Raspadinha', displayOrder: 2, value: 0 },
    ],
    ...extra,
  });
  const input = (extra: Partial<OpeningInput> = {}): OpeningInput => ({
    v: 1,
    accountId: box,
    different: {},
    explicitZero: false,
    reason: '',
    buffers: {},
    ...extra,
  });
  it('echoes the source and the full vector, changing only observed differences', () => {
    expect(
      openingAcknowledgment(proposal(), input({ different: { [cash]: '9500' } }), null),
    ).toEqual({
      sourceKind: 'PreviousSession',
      sourceId: uid(41),
      sourceRevision: 7,
      explicitZero: false,
      items: [
        { productId: cash, value: 95 },
        { productId: raspa, value: 0 },
      ],
    });
  });
  it('never assumes zero: a missing or all-zero proposal needs explicit zero', () => {
    const missing = proposal({
      isMissing: true,
      sourceKind: null,
      sourceId: null,
      sourceRevision: null,
      items: proposal().items.map((i) => ({ ...i, value: null })),
    });
    expect(zeroAcknowledgmentRequired(missing, input())).toBe(true);
    expect(openingAcknowledgment(missing, input({ explicitZero: true }), null)).toEqual({
      explicitZero: true,
      items: [],
    });
    const zeros = proposal({ items: proposal().items.map((i) => ({ ...i, value: 0 })) });
    expect(zeroAcknowledgmentRequired(zeros, input())).toBe(true);
    expect(zeroAcknowledgmentRequired(proposal(), input())).toBe(false);
    expect(openingAcknowledgment(zeros, input({ explicitZero: true }), null).explicitZero).toBe(
      true,
    );
  });
  it('names the custodian and reason only for a manager exception', () => {
    const body = openingAcknowledgment(proposal(), input({ reason: '  ausente  ' }), other);
    expect(body).toMatchObject({ custodianOperatorId: other, reason: 'ausente' });
    expect(isOpeningInput(input({ reason: 'x' }))).toBe(true);
    expect(isOpeningInput({ ...input(), sourceRevision: 7 })).toBe(false);
  });
  it('accepts a handover with its immutable vector unless observed otherwise', () => {
    const handover = {
      items: [
        { productId: cash, value: 80 },
        { productId: raspa, value: 0 },
      ],
    };
    expect(handoverAcceptance(handover, input())).toEqual({
      items: [
        { productId: cash, value: 80 },
        { productId: raspa, value: 0 },
      ],
      explicitZero: false,
    });
    expect(
      handoverAcceptance(handover, input({ different: { [cash]: '7000' }, reason: 'faltou' })),
    ).toMatchObject({
      items: [
        { productId: cash, value: 70 },
        { productId: raspa, value: 0 },
      ],
      reason: 'faltou',
    });
    expect(
      handoverAcceptance({ items: [{ productId: cash, value: 0 }] }, input({ explicitZero: true }))
        .explicitZero,
    ).toBe(true);
  });
});

describe('workstation', () => {
  const grant = (accountId: string, accountType: 'Terminal' | 'Tab' = 'Terminal') => ({
    id: uid(90),
    operatorId: me,
    accountId,
    isPrimary: false,
    accountType,
    accountName: accountId,
  });
  it('binds one cashbox, asks among several and ignores Tab, other days and lost grants', () => {
    const self = { operatorId: me, availableAccounts: [grant(box), grant(uid(22), 'Tab')] };
    expect(cashboxGrants(self).map((g) => g.accountId)).toEqual([box]);
    expect(resolveWorkstation(cashboxGrants(self), null, '2026-11-05')).toMatchObject({
      accountId: box,
    });
    const two = cashboxGrants({ operatorId: me, availableAccounts: [grant(box), grant(box2)] });
    expect(resolveWorkstation(two, null, '2026-11-05')).toBe('choose');
    expect(
      resolveWorkstation(two, { v: 1, date: '2026-11-05', accountId: box2 }, '2026-11-05'),
    ).toMatchObject({ accountId: box2 });
    expect(
      resolveWorkstation(two, { v: 1, date: '2026-11-04', accountId: box2 }, '2026-11-05'),
    ).toBe('choose');
    expect(
      resolveWorkstation(two, { v: 1, date: '2026-11-05', accountId: uid(99) }, '2026-11-05'),
    ).toBe('choose');
    expect(resolveWorkstation([], null, '2026-11-05')).toBe('none');
    expect(isWorkstationChoice({ v: 1, date: '2026-11-05', accountId: box })).toBe(true);
    expect(isWorkstationChoice({ v: 1, date: '2026-13-05', accountId: box })).toBe(false);
  });
});

describe('next action', () => {
  const ok = <T>(data: T): Read<T> => ({ status: 'ok', data });
  const failed: Read<never> = { status: 'error', error: new Error('x'), retry: () => undefined };
  const row = (
    extra: Partial<ResponseListDailyCloseItemJson> = {},
  ): ResponseListDailyCloseItemJson => ({
    id: uid(40),
    date: '2026-11-05',
    status: 'Draft',
    accountId: box,
    accountName: 'Caixa 1',
    createdAt: '2026-11-05T11:00:00Z',
    custody: custody(),
    ...extra,
  });
  const facts = (extra: Partial<WorkstationFacts> = {}): WorkstationFacts => ({
    proposal: {
      accountId: box,
      accountName: 'Caixa 1',
      terminalId: uid(30),
      isMissing: false,
      hasUnresolvedHistory: false,
      canStart: true,
      items: [],
    },
    active: null,
    today: [],
    handoverForMe: null,
    ...extra,
  });
  const base = {
    operatorId: me,
    role: 'Member' as const,
    correction: ok([]),
    workstation: ok(facts()),
    drafts: null,
    others: ok([]),
  };
  it('P1 correction wins and a failed P1 read never yields a guessed lower step', () => {
    const fix = {
      id: uid(41),
      date: '2026-11-03',
      accountName: 'Caixa 1',
      rejectionReason: 'conferir',
      openingRecheck: false,
    };
    expect(nextAction({ ...base, correction: ok([fix]) })).toMatchObject({ kind: 'correction' });
    expect(nextAction({ ...base, correction: failed })).toMatchObject({ kind: 'unknown' });
    expect(nextAction({ ...base, correction: { status: 'pending' } })).toBeNull();
  });
  it('P2 not open only from a successful workstation read; failures are not "Dia ainda não aberto"', () => {
    expect(nextAction(base)).toMatchObject({ kind: 'not-open', canStart: true, blocked: null });
    expect(nextAction({ ...base, workstation: failed })).toMatchObject({ kind: 'unknown' });
    expect(
      nextAction({
        ...base,
        workstation: ok(
          facts({ proposal: { ...facts().proposal, hasUnresolvedHistory: true, canStart: false } }),
        ),
      }),
    ).toMatchObject({ kind: 'not-open', blocked: 'unresolved-history' });
  });
  it('P3/P4 depend on the exact draft count read; elevated split is presentation only', () => {
    const active = { ...review(), items: [] } as unknown as ResponseDailyCloseJson;
    const withActive = { ...base, workstation: ok(facts({ active })) };
    expect(nextAction(withActive)).toBeNull();
    expect(nextAction({ ...withActive, drafts: failed })).toMatchObject({ kind: 'unknown' });
    expect(nextAction({ ...withActive, drafts: ok({ total: 2, mine: 1 }) })).toMatchObject({
      kind: 'drafts',
      counts: { total: 2, mine: 1 },
    });
    expect(nextAction({ ...withActive, drafts: ok({ total: 0, mine: 0 }) })).toMatchObject({
      kind: 'open',
      custodyActive: true,
    });
  });
  it('another custodian holds the cashbox unless a handover is offered to me', () => {
    const active = {
      ...review(),
      custody: custody({ custodianOperatorId: other, custodianOperatorName: 'Ana' }),
    } as unknown as ResponseDailyCloseJson;
    expect(nextAction({ ...base, workstation: ok(facts({ active })) })).toMatchObject({
      kind: 'held',
      custodian: 'Ana',
    });
    const handover = { id: uid(70) } as never;
    expect(
      nextAction({ ...base, workstation: ok(facts({ active, handoverForMe: handover })) }),
    ).toMatchObject({ kind: 'receive' });
  });
  it('P5 another open cashbox precedes P6/P7, and the others read must succeed first', () => {
    const submitted = row({
      status: 'Submitted',
      submittedAt: '2026-11-05T20:00:00Z',
      custody: custody({ custodyState: 'Finished' }),
    });
    const today = ok(facts({ today: [submitted] }));
    const open = row({ id: uid(42), accountId: box2, accountName: 'Caixa 2' });
    expect(
      nextAction({ ...base, workstation: today, others: ok([submitted, open]) }),
    ).toMatchObject({ kind: 'other-open' });
    expect(nextAction({ ...base, workstation: today, others: failed })).toMatchObject({
      kind: 'unknown',
    });
    expect(nextAction({ ...base, workstation: today })).toMatchObject({
      kind: 'submitted',
      custodyActive: false,
    });
    const approved = row({ status: 'Approved', custody: custody({ custodyState: 'Finished' }) });
    expect(nextAction({ ...base, workstation: ok(facts({ today: [approved] })) })).toMatchObject({
      kind: 'approved',
    });
  });
  it('sessions of other operators on the same cashbox and day are not mine', () => {
    const theirs = row({
      status: 'Submitted',
      custody: custody({ custodianOperatorId: other, custodyState: 'Finished' }),
    });
    expect(nextAction({ ...base, workstation: ok(facts({ today: [theirs] })) })).toMatchObject({
      kind: 'not-open',
    });
  });
});

describe('omitted opening products after Conferir', () => {
  it('is an identity comparison against the displayed rows', () => {
    const attempt = {
      items: [
        { productId: cash, productName: 'Dinheiro', openingValue: 100, countedValue: 100 },
        { productId: uid(13), productName: 'Tarifa antiga', openingValue: 100, countedValue: null },
        { productId: uid(14), productName: 'Sem abertura', openingValue: null, countedValue: null },
      ],
    };
    expect(omittedOpeningProducts(attempt, review().items).map((i) => i.productName)).toEqual([
      'Tarifa antiga',
    ]);
    expect(omittedOpeningProducts(null, review().items)).toEqual([]);
  });
});

describe('arithmetic money entry (Design 4.9.7, mobile)', () => {
  it('shows the result before acceptance and accepts only a complete valid expression', () => {
    const r = evaluateMoneyExpression('100 + 50 + 20');
    expect(previewText(r, false)).toBe('Resultado: R$ 170,00');
    expect(acceptedResult(r)).toEqual({ digits: '17000', negative: false });
    expect(acceptedResult(evaluateMoneyExpression('100 +'))).toBeNull();
    expect(previewText(evaluateMoneyExpression('100 +'), false)).toBe(
      'Continue digitando a conta.',
    );
    expect(previewText(evaluateMoneyExpression('10 ÷ 0'), false)).toBe(
      'Não é possível dividir por zero.',
    );
    expect(previewText(evaluateMoneyExpression('10 − 20'), false)).toBe(
      'O resultado não pode ser negativo neste campo.',
    );
    expect(previewText(evaluateMoneyExpression('99999999999999'), false)).toBe(
      'O resultado passa do valor máximo aceito.',
    );
    expect(previewText(evaluateMoneyExpression('10 ÷ 3'), false)).toContain(
      'arredondado para centavos',
    );
    expect(acceptedResult(evaluateMoneyExpression(''))).toBeNull();
  });
  it('the signed report accepts a negative result as sign plus magnitude', () => {
    const r = evaluateMoneyExpression('(10,50 × 2) − 100', { allowNegative: true });
    expect(acceptedResult(r)).toEqual({ digits: '7900', negative: true });
    expect(previewText(r, true)).toBe('Resultado: −R$ 79,00');
  });
  it('digits from wire keep magnitude only (the sign lives in the control)', () => {
    expect(digitsFromWire(null)).toBe('');
    expect(digitsFromWire(0)).toBe('0');
    expect(digitsFromWire(-12.5)).toBe('1250');
  });
});
