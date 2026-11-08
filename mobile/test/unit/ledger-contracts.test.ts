import {
  accountsForType,
  branchAccounts,
  cashboxOf,
  chequeActions,
  createMissing,
  createRequest,
  creatableTypes,
  draftBlocker,
  editGate,
  editInput,
  editRequest,
  emptyCreate,
  emptyPlan,
  filterParams,
  identifiedClient,
  impactLines,
  isCreateInput,
  isEditInput,
  isPaymentInput,
  isPlanInput,
  isSafeInput,
  isWhole,
  listQuery,
  memberAccounts,
  parseListFilter,
  paymentMissing,
  paymentRequest,
  planMissing,
  planRequest,
  planSumDifference,
  planTypes,
  reconcileChoices,
  repaymentMethods,
  safeRequest,
  signedValue,
  withDirection,
  wireTime,
  type TypeWithDirection,
} from '../../src/ledger/contracts';
import { assertDeviceSafe } from '../../src/storage/privacy';
import { formatSignedBrlWire } from '@loto/core';
import { validFinancialPayload } from '../../src/data/request-shape';

const id = (n: number) => `80000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const today = '2026-11-06';
const type = (overrides: Partial<TypeWithDirection>): TypeWithDirection => ({
  id: id(1),
  name: 'Tipo',
  settlementRule: 'SameDay',
  requiresTabAccountAndClient: false,
  systemRole: null,
  fiadoPaymentKind: null,
  settlementInstrument: 'None',
  monthlyFinancialRole: 'Unclassified' as never,
  financialClass: 'Unclassified',
  categoryId: id(90),
  categoryName: 'Categoria',
  createdAt: '2026-01-01T00:00:00Z',
  active: true,
  direction: 'Out',
  ...overrides,
});

describe('type offering (structure only, never names)', () => {
  const sale = type({
    id: id(1),
    name: 'Venda fiado',
    requiresTabAccountAndClient: true,
    direction: 'Out',
  });
  const tabIn = type({
    id: id(2),
    name: 'Qualquer nome',
    requiresTabAccountAndClient: true,
    direction: 'In',
  });
  const method = type({
    id: id(3),
    name: 'Dinheiro',
    fiadoPaymentKind: 'CountedCash',
    requiresTabAccountAndClient: true,
    direction: 'In',
  });
  const system = type({
    id: id(4),
    name: 'Saída de caixa — venda fiado',
    systemRole: 'FiadoSaleCounterpart',
  });
  const cheque = type({
    id: id(5),
    name: 'Cheque',
    settlementRule: 'OperatorEnteredCheque',
    direction: 'In',
  });
  const retired = type({ id: id(6), active: false });
  const expense = type({ id: id(7), name: 'Despesa' });
  const unknownDirection = type({ id: id(8), direction: null });
  const all = [sale, tabIn, method, system, cheque, retired, expense, unknownDirection];
  it('excludes system, repayment (Tab In or FiadoPaymentKind), retired and unknown-direction types', () => {
    expect(creatableTypes(all).map((t) => t.id)).toEqual([id(1), id(5), id(7)]);
  });
  it('repayment methods are exactly the configured FiadoPaymentKind types; plans need a cheque rule and no Tab', () => {
    expect(repaymentMethods(all).map((t) => t.id)).toEqual([id(3)]);
    expect(planTypes(all).map((t) => t.id)).toEqual([id(5)]);
    expect(
      planTypes([
        type({ settlementRule: 'OperatorEnteredCheque', requiresTabAccountAndClient: true }),
      ]),
    ).toEqual([]);
  });
  it('attaches the category default direction from the category list', () => {
    const [joined] = withDirection(
      [{ ...expense, direction: undefined } as never],
      [
        {
          id: id(90),
          name: 'C',
          defaultDirection: 'In',
          branchId: id(99),
          createdAt: '',
          active: true,
        },
      ],
    );
    expect(joined!.direction).toBe('In');
  });
});

describe('accounts and pairing', () => {
  const self = {
    operatorId: id(50),
    availableAccounts: [
      {
        id: id(60),
        operatorId: id(50),
        accountId: id(10),
        isPrimary: true,
        accountType: 'Terminal' as const,
        accountName: 'Caixa 1',
        pairedTabAccountId: id(20),
      },
      {
        id: id(61),
        operatorId: id(50),
        accountId: id(20),
        isPrimary: false,
        accountType: 'Tab' as const,
        accountName: 'Fiado 1',
      },
      {
        id: id(62),
        operatorId: id(50),
        accountId: id(21),
        isPrimary: false,
        accountType: 'Tab' as const,
        accountName: 'Fiado sem caixa',
      },
      {
        id: id(63),
        operatorId: id(50),
        accountId: id(30),
        isPrimary: false,
        accountType: 'Safe' as const,
        accountName: 'Cofre',
      },
    ],
  };
  it('a Member Tab pairs only with a granted cashbox; an ungranted pairing reads as unpaired', () => {
    const accounts = memberAccounts(self);
    expect(cashboxOf(accounts.find((a) => a.id === id(20)))).toEqual({
      kind: 'cashbox',
      id: id(10),
    });
    expect(cashboxOf(accounts.find((a) => a.id === id(21)))).toEqual({ kind: 'unpaired' });
    expect(cashboxOf(accounts.find((a) => a.id === id(10)))).toEqual({
      kind: 'cashbox',
      id: id(10),
    });
    expect(cashboxOf(accounts.find((a) => a.id === id(30)))).toEqual({ kind: 'none' });
  });
  it('branch accounts carry the server pairing for Manager/Admin', () => {
    const accounts = branchAccounts([
      { id: id(10), type: 'Terminal', name: 'Caixa', branchId: id(99), tabAccountId: id(20) },
      { id: id(20), type: 'Tab', name: 'Fiado', branchId: id(99), terminalAccountId: id(10) },
      { id: id(40), type: 'BankAccount', name: 'Banco', branchId: id(99) },
    ]);
    expect(cashboxOf(accounts[1])).toEqual({ kind: 'cashbox', id: id(10) });
    expect(cashboxOf(accounts[2])).toEqual({ kind: 'none' });
  });
  it('a fiado type lists only Tabs; every other type lists cashboxes and banks, never a cofre', () => {
    const accounts = memberAccounts(self);
    expect(
      accountsForType({ requiresTabAccountAndClient: true }, accounts).map((a) => a.type),
    ).toEqual(['Tab', 'Tab']);
    expect(
      accountsForType({ requiresTabAccountAndClient: false }, accounts).map((a) => a.type),
    ).toEqual(['Terminal']);
  });
});

describe('create composition', () => {
  const input = {
    ...emptyCreate(today, '09:15'),
    typeId: id(1),
    accountId: id(10),
    value: '1250',
    description: '  bolão  ',
  };
  it('blank is distinct from zero; a fiado type needs a client; an unlinked elevated caller names the operator', () => {
    expect(createMissing({ ...input, value: '' }, type({}), { operatorRequired: false })).toContain(
      'o valor',
    );
    expect(
      createMissing({ ...input, value: '0' }, type({}), { operatorRequired: false }),
    ).toContain('o valor');
    expect(
      createMissing(input, type({ requiresTabAccountAndClient: true }), {
        operatorRequired: false,
      }),
    ).toEqual(['o cliente']);
    expect(createMissing(input, type({}), { operatorRequired: true })).toEqual([
      'o operador responsável',
    ]);
    expect(
      createMissing({ ...input, buffers: { value: '10+' } }, type({}), { operatorRequired: false }),
    ).toEqual(['usar ou descartar a conta aberta']);
  });
  it('sends exactly the contract fields: cents to wire, HH:mm:ss, trimmed text and no HandsOutCountedStock', () => {
    const request = createRequest(input, { kind: 'none' }, false);
    expect(request).toEqual({
      date: today,
      value: 12.5,
      description: 'bolão',
      transactionTime: '09:15:00',
      transactionTypeId: id(1),
      accountId: id(10),
      clientId: null,
      dueDate: null,
      saveAsDraft: false,
    });
    expect(validFinancialPayload('transaction', request)).toBe(true);
    expect(JSON.stringify(request)).not.toMatch(/HandsOut|PaidAt|recordedByOperatorId/);
  });
  it('the inline opening travels on the same request; a historical write names its session and reason', () => {
    const opening = createRequest(
      input,
      { kind: 'opening', acknowledgment: { explicitZero: true, items: [] } },
      true,
    );
    expect(opening.openingAcknowledgment).toEqual({ explicitZero: true, items: [] });
    expect(opening.saveAsDraft).toBe(true);
    expect(validFinancialPayload('transaction', opening)).toBe(true);
    const historical = createRequest(
      input,
      { kind: 'historical', sessionId: id(70), reason: ' esquecido ' },
      false,
    );
    expect(historical).toMatchObject({ sessionId: id(70), historicalReason: 'esquecido' });
    expect(historical.openingAcknowledgment).toBeUndefined();
    // Nullable references (`oneOf`) are checked structurally, both null and object branches.
    expect(validFinancialPayload('transaction', { ...opening, openingAcknowledgment: null })).toBe(
      true,
    );
    expect(
      validFinancialPayload('transaction', {
        ...opening,
        openingAcknowledgment: { explicitZero: 'yes' },
      }),
    ).toBe(false);
    expect(
      validFinancialPayload('transaction', {
        ...opening,
        openingAcknowledgment: { sourceKind: 'Invented', items: [] },
      }),
    ).toBe(false);
  });
  it('retained input validates exactly and refuses secrets, tokens and versions', () => {
    expect(isCreateInput(input)).toBe(true);
    expect(isCreateInput({ ...input, extra: 1 })).toBe(false);
    expect(isCreateInput({ ...input, value: '01' })).toBe(false);
    expect(isCreateInput({ ...input, accountId: 'x' })).toBe(false);
    expect(() => assertDeviceSafe({ ...input, version: 3 })).toThrow();
    expect(() => assertDeviceSafe({ ...input, etag: '"3"' })).toThrow();
    expect(() => assertDeviceSafe(input)).not.toThrow();
  });
  it('a lost choice is cleared and named, never reinterpreted', () => {
    const result = reconcileChoices(
      { ...input, clientId: id(80) },
      { types: new Set([id(1)]), accounts: new Set([id(11)]), clients: new Set() },
    );
    expect(result.cleared).toEqual(['conta', 'cliente']);
    expect(result.input).toMatchObject({
      typeId: id(1),
      accountId: null,
      clientId: null,
      value: '1250',
    });
  });
  it('wire time accepts HH:mm and rejects garbage', () => {
    expect(wireTime('07:05')).toBe('07:05:00');
    expect(wireTime('25:00')).toBeNull();
    expect(wireTime(null)).toBeNull();
  });
});

describe('edit gate and request', () => {
  const row = {
    status: 'Active' as const,
    generationKind: null,
    recordedByOperatorId: id(50),
    date: `${today}T00:00:00`,
  };
  it.each([
    ['Manager', id(51), row, true],
    ['Admin', null, { ...row, date: '2026-10-01T00:00:00' }, true],
    ['Member', id(50), row, true],
    ['Member', id(51), row, false],
    ['Member', id(50), { ...row, date: '2026-11-05T00:00:00' }, false],
    ['Member', null, row, false],
    ['Manager', id(50), { ...row, generationKind: 'FiadoSale' as const }, false],
    ['Admin', id(50), { ...row, status: 'Cancelled' as const }, false],
  ] as const)(
    '%s (operator %s) editable=%s mirrors the §6.11 guard',
    (role, operatorId, candidate, editable) => {
      expect(editGate(candidate, { role, operatorId, today }).editable).toBe(editable);
    },
  );
  it('sends only Description/DueDate/ClientId/TransactionTime — never value, account, type, date or PaidAt', () => {
    const loaded = {
      id: id(1),
      version: 7,
      date: `${today}T00:00:00`,
      value: 10,
      description: null,
      transactionTime: '10:30:00',
      transactionTypeId: id(2),
      categoryId: id(3),
      direction: 'Out' as const,
      accountId: id(4),
      clientId: id(5),
      dueDate: `${today}T00:00:00`,
      recordedByOperatorId: id(6),
      createdByUserId: id(7),
      status: 'Active' as const,
      branchId: id(8),
      createdAt: '2026-11-06T12:00:00Z',
    };
    const input = editInput(loaded);
    expect(isEditInput(input)).toBe(true);
    expect(editRequest({ ...input, description: ' novo ' })).toEqual({
      description: 'novo',
      dueDate: today,
      clientId: id(5),
      transactionTime: '10:30:00',
    });
  });
});

describe('plan composition', () => {
  const plan = { ...emptyPlan(today, null), typeId: id(1), accountId: id(10), value: '30000' };
  it('manual rows need dates/values; the sum hint is integer cents and never blocks', () => {
    expect(planMissing(plan, { operatorRequired: false })).toEqual([
      'o vencimento e o valor de cada parcela',
    ]);
    const rows = {
      ...plan,
      rows: [
        { dueDate: '2026-12-06', value: '10000' },
        { dueDate: '2027-01-06', value: '19999' },
      ],
    };
    expect(planMissing(rows, { operatorRequired: false })).toEqual([]);
    expect(planSumDifference(rows)).toBe(-1);
    expect(planRequest(rows, { kind: 'none' }, false).installments).toEqual([
      { dueDate: '2026-12-06', value: 100 },
      { dueDate: '2027-01-06', value: 199.99 },
    ]);
    expect(isPlanInput(rows)).toBe(true);
  });
  it('auto-generation sends only first due date and count; the server derives dates and rounding', () => {
    const auto = { ...plan, mode: 'auto' as const, firstDue: '2026-12-06', count: '3' };
    expect(planMissing({ ...auto, count: '1' }, { operatorRequired: false })).toEqual([
      'a quantidade de parcelas (2 a 24)',
    ]);
    const request = planRequest(auto, { kind: 'none' }, true);
    expect(request).toMatchObject({
      autoGenerateInstallments: true,
      dueDate: '2026-12-06',
      installmentCount: 3,
      installments: [],
      saveAsDraft: true,
    });
    expect(validFinancialPayload('installment', request)).toBe(true);
  });
});

describe('Receber pagamento and Depósito Dinheiro', () => {
  it('the method has no default and is never part of the completeness list (refused on confirm)', () => {
    const input = {
      v: 1 as const,
      accountId: id(20),
      methodId: null,
      value: '500',
      date: today,
      time: null,
      description: '',
      operatorId: null,
      buffers: {},
    };
    expect(paymentMissing(input, { operatorRequired: false })).toEqual([]);
    expect(isPaymentInput(input)).toBe(true);
    const request = paymentRequest({ ...input, methodId: id(3) }, id(80), { kind: 'none' });
    expect(request).toEqual({
      date: today,
      value: 5,
      transactionTime: null,
      description: null,
      transactionTypeId: id(3),
      accountId: id(20),
      clientId: id(80),
    });
    expect(validFinancialPayload('fiado-payment', request)).toBe(true);
  });
  it('the cofre deposit names source cashbox and destination cofre once', () => {
    const input = {
      v: 1 as const,
      accountId: id(10),
      safeAccountId: id(30),
      value: '10000',
      date: today,
      time: '18:00',
      description: '',
      operatorId: null,
      buffers: {},
    };
    expect(isSafeInput(input)).toBe(true);
    const request = safeRequest(input, { kind: 'none' });
    expect(request).toEqual({
      date: today,
      value: 100,
      transactionTime: '18:00:00',
      description: null,
      accountId: id(10),
      safeAccountId: id(30),
    });
    expect(validFinancialPayload('safe-deposit', request)).toBe(true);
  });
});

describe('transactions list filter', () => {
  const ctx = { today, role: 'Manager' as const, shift: true, linked: true };
  it('defaults to today; Mine on only for a linked elevated caller from Meu turno navigation', () => {
    expect(parseListFilter({}, ctx)).toMatchObject({
      from: today,
      to: today,
      mine: true,
      page: 1,
      pageSize: 50,
    });
    expect(parseListFilter({}, { ...ctx, shift: false }).mine).toBe(false);
    expect(parseListFilter({}, { ...ctx, role: 'Member' }).mine).toBe(false);
    // Unlinked: the server ignores Mine and would list the whole branch, so it is never sent.
    expect(parseListFilter({ mine: 'true' }, { ...ctx, linked: false }).mine).toBe(false);
  });
  it('Members never send OperatorId; Mine and OperatorId are exclusive', () => {
    expect(
      parseListFilter({ operatorId: id(1) }, { ...ctx, role: 'Member' }).operatorId,
    ).toBeNull();
    expect(parseListFilter({ operatorId: id(1), mine: 'true' }, ctx)).toMatchObject({
      operatorId: id(1),
      mine: false,
    });
  });
  it('the M5 Draft-blocker link is one cashbox, one day, Draft; the query matches the contract', () => {
    const filter = parseListFilter(
      { accountId: id(10), from: today, to: today, status: 'Draft', mine: 'true' },
      { ...ctx, role: 'Member' },
    );
    expect(draftBlocker(filter)).toBe(true);
    expect(listQuery(filter)).toEqual({
      DateFrom: today,
      DateTo: today,
      Status: 'Draft',
      AccountId: id(10),
      Mine: true,
      Page: 1,
      PageSize: 50,
    });
    expect(filterParams(filter)).toMatchObject({
      accountId: id(10),
      status: 'Draft',
      mine: 'true',
      page: '1',
    });
  });
  it('malformed parameters fall back instead of reaching the server', () => {
    expect(
      parseListFilter(
        { status: 'Paid', page: '-2', pageSize: '1000', accountId: 'x', from: '2026-13-01' },
        ctx,
      ),
    ).toMatchObject({
      status: null,
      page: 1,
      pageSize: 50,
      accountId: null,
      from: null,
    });
  });
  it('signed row values come from the direction only; nothing is summed', () => {
    expect(signedValue(12.5, 'In')).toBe(formatSignedBrlWire(12.5));
    expect(signedValue(12.5, 'Out')).toBe(formatSignedBrlWire(-12.5));
    expect(signedValue(12.5, 'Out').startsWith('−')).toBe(true);
  });
});

describe('impact preview lines', () => {
  const base = {
    receivableImpact: {
      rowAppearsInOpenReceivables: false,
      rowDisappearsFromOpenReceivables: false,
    },
    fiadoBalanceImpact: { deltas: [] },
    cashVarianceImpact: { varianceDelta: 0 },
  };
  it('a non-cashbox account says so instead of a zero difference; a Draft close stays blind', () => {
    expect(impactLines(base, {}, {}).map((l) => l.key)).toEqual(['cash-none']);
    const draft = impactLines(
      {
        ...base,
        cashVarianceImpact: {
          accountId: id(10),
          dailyCloseStatus: 'Draft',
          currentVariance: -5,
          projectedVariance: -2,
          varianceDelta: 3,
        },
      },
      {},
      {},
    );
    expect(draft.map((l) => l.text).join(' ')).not.toMatch(/R\$/);
  });
  it('fiado deltas and receivable entry come from the server values', () => {
    const lines = impactLines(
      {
        ...base,
        receivableImpact: {
          rowAppearsInOpenReceivables: true,
          rowDisappearsFromOpenReceivables: false,
          bucketAfter: 'Current' as never,
        },
        fiadoBalanceImpact: {
          deltas: [{ clientId: id(1), clientName: 'Ana', outstandingDelta: 40 }],
        },
      },
      { Current: 'A vencer' },
      {},
    );
    expect(lines.map((l) => l.text)).toEqual(
      expect.arrayContaining([
        'Passa a constar em valores a receber (A vencer).',
        `Fiado de Ana: ${formatSignedBrlWire(40)} no saldo em aberto.`,
      ]),
    );
  });
});

describe('cheque lifecycle offering', () => {
  it.each([
    ['Received', ['custody', 'deposit', 'convert']],
    ['InSafe', ['custody', 'deposit', 'convert']],
    ['Deposited', ['return']],
    ['Returned', ['custody', 'represent', 'convert']],
    ['AwaitingRedeposit', ['custody', 'deposit', 'convert']],
    ['ConvertedToFiado', []],
    ['SettledBeforeEvents', []],
  ] as const)('%s offers %j', (state, actions) => {
    expect(chequeActions(state)).toEqual(actions);
  });
  it('the identified client is the latest return event, else the row client; whole means face value outstanding', () => {
    const receivable = {
      clientId: id(1),
      value: 100,
      outstandingValue: 100,
      events: [
        { kind: 'ChequeDeposited', clientId: null },
        { kind: 'ChequeReturned', clientId: id(2) },
      ],
    } as never;
    expect(identifiedClient(receivable)).toBe(id(2));
    expect(identifiedClient({ clientId: id(1), events: [] } as never)).toBe(id(1));
    expect(isWhole({ value: 100, outstandingValue: 100 })).toBe(true);
    expect(isWhole({ value: 100, outstandingValue: 99.99 })).toBe(false);
  });
});

it('clearing filters overwrites the merged route parameters (the router keeps omitted keys)', () => {
  const ctx = { today: '2026-11-06', role: 'Manager' as const, shift: false, linked: true };
  const id = '80000000-0000-4000-8000-000000000001';
  const before = filterParams(
    parseListFilter(
      { status: 'Draft', accountId: id, operatorId: id, clientId: id, typeId: id },
      ctx,
    ),
  );
  const cleared = parseListFilter({}, ctx);
  const merged = { ...before, ...filterParams(cleared) };
  expect(parseListFilter(merged, ctx)).toMatchObject({
    status: null,
    accountId: null,
    operatorId: null,
    clientId: null,
    typeId: null,
  });
  expect(listQuery(parseListFilter(merged, ctx))).not.toHaveProperty('Status');
});
