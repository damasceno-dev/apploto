// In-memory model of the shipped v63/v64 transaction, fiado, cofre and cheque contracts for Mobile
// M6 provider/UI tests. It mirrors ListTransactionsUseCase (scope, Mine, ordering, paging),
// Create/Preview (system/repayment refusal, fiado pair, inline opening), Update/Finalize/Cancel
// (strong If-Match, generated read-only, settlement-event refusal), installment preview/create/
// finalize (all-or-nothing), fiado statement/payment, safe-deposit and cheque commands with
// Idempotency-Key replay. The app under test never computes any of these outcomes.
import { response, userId } from './support';

export const lid = (n: number) => `90000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
export const L = {
  me: userId,
  op: lid(1),
  otherOp: lid(2),
  box: lid(10),
  tab: lid(20),
  tabUnpaired: lid(21),
  safe: lid(30),
  bank: lid(40),
  sale: lid(50),
  expense: lid(51),
  cheque: lid(52),
  cashMethod: lid(53),
  pixMethod: lid(54),
  saleCounterpart: lid(55),
  safeOut: lid(56),
  safeIn: lid(57),
  conversion: lid(58),
  income: lid(59),
  catIn: lid(60),
  catOut: lid(61),
  ana: lid(70),
  bruno: lid(71),
  product: lid(80),
};
interface Row {
  id: string;
  version: number;
  date: string;
  value: number;
  description: string | null;
  transactionTime: string | null;
  transactionTypeId: string;
  categoryId: string;
  direction: 'In' | 'Out';
  accountId: string;
  clientId: string | null;
  dueDate: string;
  originTransactionId: string | null;
  generationKind: string | null;
  generatedFromTransactionId: string | null;
  generatedTransactionId: string | null;
  sessionId: string | null;
  recordedByOperatorId: string;
  createdByUserId: string;
  status: 'Draft' | 'Active' | 'Cancelled';
  cancelledAt: string | null;
  cancellationReason: string | null;
  createdAt: string;
  updatedAt: string | null;
  seq: number;
}
interface Event {
  id: string;
  sequence: number;
  kind: string;
  amount: number;
  clientId: string | null;
  custodyToAccountId: string | null;
  bankReceiptId: string | null;
  nextExpectedDepositDate: string | null;
  returnReason: string | null;
  convertedTransactionId: string | null;
}
const types = [
  { id: L.sale, name: 'Venda fiado', rule: 'SameDay', tab: true, cat: L.catOut },
  { id: L.expense, name: 'Despesa', rule: 'SameDay', tab: false, cat: L.catOut },
  { id: L.income, name: 'Venda balcão', rule: 'SameDay', tab: false, cat: L.catIn },
  {
    id: L.cheque,
    name: 'Cheque pré',
    rule: 'OperatorEnteredCheque',
    tab: false,
    cat: L.catIn,
    instrument: 'Cheque',
  },
  {
    id: L.cashMethod,
    name: 'Dinheiro',
    rule: 'SameDay',
    tab: true,
    cat: L.catIn,
    kind: 'CountedCash',
  },
  { id: L.pixMethod, name: 'PIX', rule: 'SameDay', tab: true, cat: L.catIn, kind: 'NonCash' },
  {
    id: L.saleCounterpart,
    name: 'Saída de caixa — venda fiado',
    rule: 'SameDay',
    tab: false,
    cat: L.catOut,
    role: 'FiadoSaleCounterpart',
  },
  {
    id: L.safeOut,
    name: 'Depósito no cofre',
    rule: 'SameDay',
    tab: false,
    cat: L.catOut,
    role: 'SafeDepositOut',
  },
  {
    id: L.safeIn,
    name: 'Entrada no cofre',
    rule: 'SameDay',
    tab: false,
    cat: L.catIn,
    role: 'SafeDepositIn',
  },
  {
    id: L.conversion,
    name: 'Cheque convertido em fiado',
    rule: 'SameDay',
    tab: true,
    cat: L.catOut,
    role: 'ChequeFiadoConversion',
  },
];
const accounts = [
  { id: L.box, type: 'Terminal', name: 'Caixa 1', tabAccountId: L.tab },
  { id: L.tab, type: 'Tab', name: 'Fiado 1', terminalAccountId: L.box },
  { id: L.tabUnpaired, type: 'Tab', name: 'Fiado avulso' },
  { id: L.safe, type: 'Safe', name: 'Cofre' },
  { id: L.bank, type: 'BankAccount', name: 'Banco' },
];
export interface LedgerOptions {
  readonly role?: 'Member' | 'Manager' | 'Admin';
  readonly linked?: boolean;
  /** Member grants (account ids). */
  readonly grants?: readonly string[];
  readonly activeSession?: boolean;
  /** No counted predecessor: the opening needs an explicit zero. */
  readonly missingProposal?: boolean;
}
export function ledgerServer(options: LedgerOptions = {}) {
  const role = options.role ?? 'Member';
  const state = {
    today: '2026-10-27',
    linked: options.linked ?? true,
    grants: options.grants ?? [L.box, L.tab, L.safe],
    rows: new Map<string, Row>(),
    events: new Map<string, Event[]>(),
    receipts: new Map<string, { id: string; amount: number; bankAccountId: string }>(),
    activeSession: options.activeSession ? lid(900) : (null as string | null),
    sessionsStarted: 0,
    replays: new Map<string, { fingerprint: string; status: number; body: unknown }>(),
    seq: 1000,
    /** Requests to answer once with a lost response after committing (uncertain outcome). */
    lose: new Set<string>(),
    hold: new Map<string, Promise<void>>(),
    previewDelay: new Map<string, Promise<void>>(),
    fail: new Map<string, { status: number; message: string }>(),
  };
  const next = () => lid(++state.seq);
  const err = (status: number, message: string) => response({ errorMessages: [message] }, status);
  const own = () => (state.linked ? L.op : null);
  const typeOf = (id: string) => types.find((t) => t.id === id);
  const direction = (t: (typeof types)[number]) =>
    (t.cat === L.catIn ? 'In' : 'Out') as 'In' | 'Out';
  const accountOf = (id: string) => accounts.find((a) => a.id === id);
  const inScope = (accountId: string) => role !== 'Member' || state.grants.includes(accountId);
  const json = (row: Row) => {
    const t = typeOf(row.transactionTypeId)!;
    return {
      ...row,
      seq: undefined,
      classification:
        row.status === 'Draft'
          ? null
          : {
              settlementInstrument: (t as { instrument?: string }).instrument ?? 'None',
              monthlyFinancialRole: 'Unclassified',
              financialClass: 'CaixaAndClientMoney',
              provenance: 'Posting',
              revision: 1,
              classifiedAt: '2026-10-27T12:00:00Z',
            },
      branchId: lid(999),
    };
  };
  const listItem = (row: Row) => ({
    ...json(row),
    accountName: accountOf(row.accountId)!.name,
    clientName: row.clientId === L.ana ? 'Ana' : row.clientId === L.bruno ? 'Bruno' : null,
    transactionTypeName: typeOf(row.transactionTypeId)!.name,
  });
  const add = (
    partial: Partial<Row> & Pick<Row, 'transactionTypeId' | 'accountId' | 'value' | 'date'>,
  ): Row => {
    const t = typeOf(partial.transactionTypeId)!;
    const row: Row = {
      id: next(),
      version: 1,
      description: null,
      transactionTime: null,
      categoryId: t.cat,
      direction: direction(t),
      clientId: null,
      dueDate: partial.date,
      originTransactionId: null,
      generationKind: null,
      generatedFromTransactionId: null,
      generatedTransactionId: null,
      sessionId: accountOf(partial.accountId)!.type === 'Terminal' ? state.activeSession : null,
      recordedByOperatorId: own() ?? L.otherOp,
      createdByUserId: userId,
      status: 'Active',
      cancelledAt: null,
      cancellationReason: null,
      createdAt: `2026-10-27T12:${String(state.seq % 60).padStart(2, '0')}:00Z`,
      updatedAt: null,
      seq: ++state.seq,
      ...partial,
    };
    state.rows.set(row.id, row);
    return row;
  };
  const idempotent = (
    endpoint: string,
    init: RequestInit,
    body: unknown,
    run: () => { status: number; body: unknown } | Response,
  ): Response => {
    const key = new Headers(init.headers).get('idempotency-key');
    if (!key) return err(400, 'Idempotency-Key é obrigatório.');
    const fingerprint = JSON.stringify(body);
    const stored = state.replays.get(`${endpoint}:${key}`);
    if (stored) {
      if (stored.fingerprint !== fingerprint)
        return err(409, 'Chave de idempotência reutilizada com outro pedido.');
      return response(stored.body, stored.status);
    }
    const result = run();
    if (result instanceof Response) return result;
    state.replays.set(`${endpoint}:${key}`, { fingerprint, ...result });
    if (state.lose.delete(endpoint)) throw new TypeError('response lost after commit');
    return response(result.body, result.status);
  };
  /** Session binding of a cashbox row (DailyCloseLedgerGuard) — preview never starts anything. */
  const bind = (
    body: { date: string; openingAcknowledgment?: unknown; sessionId?: string | null },
    preview: boolean,
  ) => {
    if (body.sessionId)
      return role === 'Member' ? err(403, 'Somente a gerência registra em turno anterior.') : null;
    if (!state.activeSession) {
      if (body.date !== state.today) return err(409, 'Lançamento retroativo exige o turno do dia.');
      if (!body.openingAcknowledgment)
        return err(409, 'Confira a abertura do caixa antes de registrar.');
      if (!preview) {
        state.activeSession = lid(900 + ++state.sessionsStarted);
        return 'started';
      }
      return null;
    }
    if (body.openingAcknowledgment) return err(409, 'O caixa já tem turno ativo.');
    return null;
  };
  const createTransaction = (body: Record<string, unknown>, preview: boolean) => {
    const t = typeOf(body.transactionTypeId as string);
    const account = accountOf(body.accountId as string);
    if (!t || !account) return err(404, 'Não encontrado.');
    if ((t as { role?: string }).role) return err(409, 'Tipo exclusivo do sistema.');
    if ((t as { kind?: string }).kind || (t.tab && direction(t) === 'In'))
      return err(409, 'Recebimento de fiado só pelo comando de pagamento.');
    if (!inScope(account.id)) return err(403, 'Conta fora do seu escopo.');
    if (role === 'Member' && body.recordedByOperatorId)
      return err(400, 'Membro não informa o operador.');
    if (role !== 'Member' && !state.linked && !body.recordedByOperatorId)
      return err(400, 'Informe o operador.');
    let cashbox: string | null = account.type === 'Terminal' ? account.id : null;
    if (t.tab) {
      if (!body.clientId) return err(409, 'Venda fiado exige cliente.');
      cashbox = (account as { terminalAccountId?: string }).terminalAccountId ?? null;
      if (!cashbox) return err(409, 'Conta fiado sem caixa pareado.');
      if (!inScope(cashbox)) return err(403, 'Caixa pareado fora do seu escopo.');
    }
    if (!cashbox && (body.openingAcknowledgment || body.sessionId))
      return err(400, 'Turno só em conta de caixa.');
    let started = false;
    if (cashbox) {
      const bound = bind(body as never, preview);
      if (bound instanceof Response) return bound;
      started = bound === 'started';
    }
    if (preview)
      return {
        status: 200,
        body: {
          impact: {
            receivableImpact: {
              rowAppearsInOpenReceivables: false,
              rowDisappearsFromOpenReceivables: false,
            },
            fiadoBalanceImpact: {
              deltas: t.tab
                ? [
                    {
                      clientId: body.clientId,
                      clientName: 'Ana',
                      outstandingDelta: Number(body.value),
                    },
                  ]
                : [],
            },
            // With an inline opening the hypothetical resolves no session (DailyCloseLedgerGuard).
            cashVarianceImpact:
              cashbox && state.activeSession
                ? {
                    accountId: cashbox,
                    date: body.date,
                    dailyCloseStatus: 'Draft',
                    currentVariance: null,
                    projectedVariance: null,
                    varianceDelta: 0,
                  }
                : { varianceDelta: 0 },
          },
          warnings: [],
        },
      };
    const status = body.saveAsDraft ? 'Draft' : 'Active';
    const row = add({
      transactionTypeId: t.id,
      accountId: account.id,
      value: Number(body.value),
      date: `${body.date}T00:00:00`,
      dueDate: `${(body.dueDate as string) ?? body.date}T00:00:00`,
      description: (body.description as string) ?? null,
      clientId: (body.clientId as string) ?? null,
      transactionTime: (body.transactionTime as string) ?? null,
      status,
      sessionId: account.type === 'Terminal' ? state.activeSession : null,
    });
    if (t.tab && cashbox) {
      const leg = add({
        transactionTypeId: L.saleCounterpart,
        accountId: cashbox,
        value: row.value,
        date: row.date,
        status,
        generationKind: 'FiadoSale',
        generatedFromTransactionId: row.id,
        sessionId: state.activeSession,
      });
      row.generatedTransactionId = leg.id;
    }
    return {
      status: 201,
      body: {
        ...json(row),
        startedSessionId: started ? state.activeSession : null,
        openingDiscrepancyId: null,
      },
    };
  };
  const balanceOf = (clientId: string) =>
    [...state.rows.values()]
      .filter(
        (r) =>
          r.clientId === clientId &&
          r.status === 'Active' &&
          accountOf(r.accountId)!.type === 'Tab',
      )
      .reduce((sum, r) => sum + (r.direction === 'Out' ? r.value : -r.value), 0);
  const receivable = (row: Row) => {
    const events = state.events.get(row.id) ?? [];
    let chequeState = 'Received';
    let custody: string | null = null;
    let deposit: string | null = null;
    let outstanding = row.value;
    for (const e of events) {
      if (e.kind === 'CustodyMoved') {
        chequeState =
          chequeState === 'Returned' || chequeState === 'AwaitingRedeposit'
            ? chequeState
            : 'InSafe';
        custody = e.custodyToAccountId;
      }
      if (e.kind === 'ChequeDeposited') {
        chequeState = 'Deposited';
        deposit = e.id;
        outstanding = 0;
      }
      if (e.kind === 'ChequeReturned') {
        chequeState = 'Returned';
        outstanding = row.value;
      }
      if (e.kind === 'ChequeRepresented') chequeState = 'AwaitingRedeposit';
      if (e.kind === 'ChequeConvertedToFiado') {
        chequeState = 'ConvertedToFiado';
        outstanding = 0;
      }
    }
    return {
      transactionId: row.id,
      originTransactionId: row.originTransactionId,
      accountId: row.accountId,
      clientId: row.clientId,
      date: row.date,
      dueDate: row.dueDate,
      value: row.value,
      settlementInstrument: 'Cheque',
      isCheque: true,
      cutoffBasis: 'Posted',
      cutoffDate: `${state.today}T00:00:00`,
      outstandingValue: outstanding,
      settledValue: row.value - outstanding,
      status: outstanding === 0 ? 'Settled' : 'Open',
      chequeState,
      custodyAccountId: custody,
      currentDepositEventId: chequeState === 'Deposited' ? deposit : null,
      depositAttempts: events.filter((e) => e.kind === 'ChequeDeposited').length,
      version: events.length,
      events: events.map((e) => ({
        ...e,
        outstandingDelta: 0,
        occurredOn: `${state.today}T00:00:00`,
        postedOn: `${state.today}T00:00:00`,
        isLatePosting: false,
        postingLagDays: 0,
        countsAtCutoff: true,
        provenance: 'Command',
        migrationIssues: [],
        settlementInstrument: 'Cheque',
        createdAt: '2026-10-27T13:00:00Z',
      })),
    };
  };
  const chequeEvent = (row: Row, kind: string, extra: Partial<Event> = {}) => {
    const list = state.events.get(row.id) ?? [];
    const event: Event = {
      id: next(),
      sequence: list.length + 1,
      kind,
      amount: row.value,
      clientId: null,
      custodyToAccountId: null,
      bankReceiptId: null,
      nextExpectedDepositDate: null,
      returnReason: null,
      convertedTransactionId: null,
      ...extra,
    };
    state.events.set(row.id, [...list, event]);
    return event;
  };
  const loadCheques = (items: { transactionId: string; expectedVersion: number }[]) => {
    const rows: Row[] = [];
    for (const item of items) {
      const row = state.rows.get(item.transactionId);
      if (!row) return err(404, 'Transação não encontrada.');
      if (row.status !== 'Active') return err(409, 'O cheque não está ativo.');
      if (receivable(row).version !== Number(item.expectedVersion))
        return err(409, 'O cheque mudou. Releia antes de continuar.');
      rows.push(row);
    }
    return rows;
  };
  async function handle(
    path: string,
    init: RequestInit,
    calls: { url: string }[],
  ): Promise<Response | undefined> {
    const url = new URL(calls.at(-1)!.url);
    const method = init.method ?? 'GET';
    const body =
      typeof init.body === 'string' ? (JSON.parse(init.body) as Record<string, unknown>) : {};
    const failure = state.fail.get(`${method} ${path}`);
    if (failure) {
      state.fail.delete(`${method} ${path}`);
      return err(failure.status, failure.message);
    }
    const held = state.hold.get(`${method} ${path}`);
    if (held) await held;
    if (path === '/operator/self-context')
      return response({
        operatorId: own(),
        operatorName: state.linked ? 'Operadora Teste' : null,
        primaryAccount: null,
        availableAccounts:
          role === 'Member' && state.linked
            ? state.grants.map((accountId, i) => ({
                id: lid(500 + i),
                operatorId: L.op,
                accountId,
                isPrimary: i === 0,
                accountType: accountOf(accountId)!.type,
                accountName: accountOf(accountId)!.name,
                pairedTabAccountId: accountId === L.box ? L.tab : null,
              }))
            : [],
      });
    if (path === '/transaction-type' && method === 'GET')
      return response({
        items: types.map((t) => ({
          id: t.id,
          name: t.name,
          settlementRule: t.rule,
          requiresTabAccountAndClient: t.tab,
          systemRole: (t as { role?: string }).role ?? null,
          fiadoPaymentKind: (t as { kind?: string }).kind ?? null,
          settlementInstrument: (t as { instrument?: string }).instrument ?? 'None',
          monthlyFinancialRole: 'Unclassified',
          financialClass: 'CaixaAndClientMoney',
          categoryId: t.cat,
          categoryName: t.cat === L.catIn ? 'Entradas' : 'Saídas',
          createdAt: '2026-01-01T00:00:00Z',
          active: true,
        })),
      });
    if (path === '/category' && method === 'GET')
      return response({
        items: [
          {
            id: L.catIn,
            name: 'Entradas',
            defaultDirection: 'In',
            branchId: lid(999),
            createdAt: '',
            active: true,
          },
          {
            id: L.catOut,
            name: 'Saídas',
            defaultDirection: 'Out',
            branchId: lid(999),
            createdAt: '',
            active: true,
          },
        ],
      });
    if (path === '/account' && method === 'GET')
      return role === 'Member'
        ? err(403, 'Sem permissão.')
        : response({ accounts: accounts.map((a) => ({ ...a, branchId: lid(999) })) });
    if (path === '/operator' && method === 'GET')
      return role === 'Member'
        ? err(403, 'Sem permissão.')
        : response({
            operators: [
              { id: L.op, name: 'Operadora Teste', branchId: lid(999) },
              { id: L.otherOp, name: 'Outro', branchId: lid(999) },
            ],
          });
    if (path === '/client' && method === 'GET')
      return response({
        clients: [
          { id: L.ana, name: 'Ana', phone: '11999990000', branchId: lid(999) },
          { id: L.bruno, name: 'Bruno', phone: '11999990001', branchId: lid(999) },
        ],
      });
    const client = /^\/client\/([0-9a-f-]{36})$/.exec(path);
    if (client && method === 'GET')
      return response({
        id: client[1],
        name: client[1] === L.ana ? 'Ana' : 'Bruno',
        phone: '11999990000',
        branchId: lid(999),
      });
    const statement = /^\/client\/([0-9a-f-]{36})\/fiado-statement$/.exec(path);
    if (statement) {
      if (role === 'Member' && !state.grants.some((g) => accountOf(g)?.type === 'Tab'))
        return err(403, 'Extrato de fiado exige conta fiado atribuída.');
      const rows = [...state.rows.values()]
        .filter(
          (r) =>
            r.clientId === statement[1] &&
            r.status === 'Active' &&
            accountOf(r.accountId)!.type === 'Tab',
        )
        .sort((a, b) => b.seq - a.seq);
      let running = balanceOf(statement[1]!);
      const items = rows.map((r) => {
        const item = {
          transactionId: r.id,
          date: r.date,
          direction: r.direction,
          value: r.value,
          accountId: r.accountId,
          accountName: accountOf(r.accountId)!.name,
          transactionTypeName: typeOf(r.transactionTypeId)!.name,
          description: r.description,
          runningBalance: running,
        };
        running -= r.direction === 'Out' ? r.value : -r.value;
        return item;
      });
      const pageSize = Number(url.searchParams.get('PageSize') ?? 50);
      return response({
        clientId: statement[1],
        clientName: 'Ana',
        asOfDate: `${state.today}T00:00:00`,
        outstandingTotal: balanceOf(statement[1]!),
        oldestUnpaidDate: balanceOf(statement[1]!) > 0 ? `${state.today}T00:00:00` : null,
        dateFrom: url.searchParams.get('DateFrom'),
        dateTo: `${url.searchParams.get('DateTo') ?? state.today}T00:00:00`,
        openingBalance: 0,
        closingBalance: balanceOf(statement[1]!),
        items: items.slice(0, pageSize),
        page: 1,
        pageSize,
        totalCount: items.length,
        totalPages: Math.max(1, Math.ceil(items.length / pageSize)),
        hasNext: items.length > pageSize,
        hasPrevious: false,
      });
    }
    if (path === '/session/opening-proposal')
      return response({
        accountId: url.searchParams.get('accountId'),
        accountName: 'Caixa 1',
        terminalId: lid(990),
        sourceKind: 'PreviousSession',
        sourceId: lid(991),
        sourceRevision: 4,
        sourceDate: '2026-10-26T00:00:00',
        isMissing: !!options.missingProposal,
        activeSessionId: state.activeSession,
        pendingHandoverId: null,
        hasUnresolvedHistory: false,
        canStart: !state.activeSession,
        items: [
          {
            productId: L.product,
            productName: 'Dinheiro',
            displayOrder: 1,
            value: options.missingProposal ? null : 100,
          },
        ],
      });
    if (path === '/transaction' && method === 'GET') {
      const q = url.searchParams;
      const page = Number(q.get('Page') ?? 1);
      const size = Number(q.get('PageSize') ?? 20);
      let operator = role === 'Member' ? null : q.get('OperatorId');
      if (q.get('Mine') === 'true' && state.linked) operator = L.op;
      const rows = [...state.rows.values()]
        .filter((r) =>
          role === 'Member' ? state.linked && state.grants.includes(r.accountId) : true,
        )
        .filter((r) => !q.get('AccountId') || r.accountId === q.get('AccountId'))
        .filter((r) => !q.get('Status') || r.status === q.get('Status'))
        .filter((r) => !q.get('DateFrom') || r.date.slice(0, 10) >= q.get('DateFrom')!)
        .filter((r) => !q.get('DateTo') || r.date.slice(0, 10) <= q.get('DateTo')!)
        .filter((r) => !q.get('ClientId') || r.clientId === q.get('ClientId'))
        .filter(
          (r) => !q.get('TransactionTypeId') || r.transactionTypeId === q.get('TransactionTypeId'),
        )
        .filter(
          (r) =>
            !q.get('OriginTransactionId') || r.originTransactionId === q.get('OriginTransactionId'),
        )
        .filter((r) => !operator || r.recordedByOperatorId === operator)
        .sort((a, b) => b.date.localeCompare(a.date) || b.seq - a.seq);
      const items = rows.slice((page - 1) * size, page * size).map(listItem);
      const totalPages = Math.ceil(rows.length / size);
      return response({
        items,
        page,
        pageSize: size,
        totalCount: rows.length,
        totalPages,
        hasNext: page < totalPages,
        hasPrevious: page > 1,
      });
    }
    if (path === '/transaction/preview') {
      const delay = state.previewDelay.get(String(body.value));
      if (delay) await delay;
      const result = createTransaction(body, true);
      return result instanceof Response ? result : response(result.body, result.status);
    }
    if (path === '/transaction' && method === 'POST')
      return idempotent('POST /transaction', init, body, () => createTransaction(body, false));
    if (path === '/transaction/fiado-payment')
      return idempotent('POST /transaction/fiado-payment', init, body, () => {
        const t = typeOf(body.transactionTypeId as string) as { kind?: string } | undefined;
        if (!t?.kind) return err(400, 'Escolha a forma de pagamento.');
        const tab = accountOf(body.accountId as string)!;
        if (!inScope(tab.id)) return err(403, 'Conta fora do seu escopo.');
        const cash = t.kind === 'CountedCash';
        const cashbox = cash
          ? ((tab as { terminalAccountId?: string }).terminalAccountId ?? null)
          : null;
        if (!cash && (body.openingAcknowledgment || body.sessionId))
          return err(400, 'Turno só em conta de caixa.');
        let started = false;
        if (cash) {
          if (!cashbox) return err(409, 'Conta fiado sem caixa pareado.');
          const bound = bind(body as never, false);
          if (bound instanceof Response) return bound;
          started = bound === 'started';
        }
        const row = add({
          transactionTypeId: body.transactionTypeId as string,
          accountId: tab.id,
          value: Number(body.value),
          date: `${body.date}T00:00:00`,
          clientId: body.clientId as string,
        });
        if (cash) {
          const leg = add({
            transactionTypeId: L.income,
            accountId: cashbox!,
            value: row.value,
            date: row.date,
            generationKind: 'FiadoCashRepayment',
            generatedFromTransactionId: row.id,
            sessionId: state.activeSession,
          });
          row.generatedTransactionId = leg.id;
        }
        return {
          status: 201,
          body: { ...json(row), startedSessionId: started ? state.activeSession : null },
        };
      });
    if (path === '/transaction/safe-deposit')
      return idempotent('POST /transaction/safe-deposit', init, body, () => {
        if (!inScope(body.accountId as string) || !inScope(body.safeAccountId as string))
          return err(403, 'Conta fora do seu escopo.');
        if (accountOf(body.safeAccountId as string)?.type !== 'Safe')
          return err(404, 'Cofre não encontrado.');
        const bound = bind(body as never, false);
        if (bound instanceof Response) return bound;
        const out = add({
          transactionTypeId: L.safeOut,
          accountId: body.accountId as string,
          value: Number(body.value),
          date: `${body.date}T00:00:00`,
          sessionId: state.activeSession,
        });
        const into = add({
          transactionTypeId: L.safeIn,
          accountId: body.safeAccountId as string,
          value: out.value,
          date: out.date,
          generationKind: 'SafeCashDeposit',
          generatedFromTransactionId: out.id,
        });
        out.generatedTransactionId = into.id;
        return {
          status: 201,
          body: {
            ...json(out),
            custodyTransferId: next(),
            startedSessionId: bound === 'started' ? state.activeSession : null,
          },
        };
      });
    if (
      path === '/transaction/installment/preview' ||
      (path === '/transaction/installment' && method === 'POST')
    ) {
      const preview = path.endsWith('/preview');
      const t = typeOf(body.transactionTypeId as string);
      if (t?.rule !== 'OperatorEnteredCheque') return err(400, 'O plano exige um tipo de cheque.');
      const rows = (
        body.autoGenerateInstallments
          ? Array.from({ length: Number(body.installmentCount) }, (_, i) => ({
              dueDate: `2026-${String(11 + i).padStart(2, '0')}-27`,
              value:
                i === Number(body.installmentCount) - 1
                  ? Number(body.value) -
                    Math.floor(Number(body.value) / Number(body.installmentCount)) *
                      (Number(body.installmentCount) - 1)
                  : Math.floor(Number(body.value) / Number(body.installmentCount)),
            }))
          : (body.installments as { dueDate: string; value: number }[])
      ) as { dueDate: string; value: number }[];
      const total = rows.reduce((s, r) => s + Number(r.value), 0);
      if (Math.abs(total - Number(body.value)) > 0.001)
        return err(400, 'A soma das parcelas deve ser igual ao valor.');
      if (preview)
        return response({
          totalValue: total,
          installmentCount: rows.length,
          rows: rows.map((r, i) => ({
            index: i + 1,
            dueDate: `${r.dueDate}T00:00:00`,
            value: r.value,
            description: `CH PRE (${i + 1}/${rows.length})`,
          })),
          impact: {
            openChequeAgingImpact: {
              groupAppearsInOpenCheques: true,
              outstandingTotal: total,
              openRowCount: rows.length,
              totalRowCount: rows.length,
              rows: [],
            },
            fiadoBalanceImpact: { deltas: [] },
            cashVarianceImpact: { varianceDelta: 0 },
          },
        });
      return idempotent('POST /transaction/installment', init, body, () => {
        if (accountOf(body.accountId as string)?.type === 'Terminal') {
          const bound = bind(body as never, false);
          if (bound instanceof Response) return bound;
        }
        const created: Row[] = [];
        rows.forEach((r) => {
          const row = add({
            transactionTypeId: t.id,
            accountId: body.accountId as string,
            value: Number(r.value),
            date: `${body.date}T00:00:00`,
            dueDate: `${r.dueDate}T00:00:00`,
            status: body.saveAsDraft ? 'Draft' : 'Active',
            description: `CH PRE (${created.length + 1}/${rows.length})`,
          });
          created.push(row);
        });
        created.forEach((row) => (row.originTransactionId = created[0]!.id));
        return { status: 201, body: { version: 1, installments: created.map(json) } };
      });
    }
    const plan = /^\/transaction\/installment\/([0-9a-f-]{36})(\/finalize)?$/.exec(path);
    if (plan) {
      const siblings = [...state.rows.values()]
        .filter((r) => r.originTransactionId === plan[1])
        .sort((a, b) => a.seq - b.seq);
      if (!siblings.length) return err(404, 'Plano não encontrado.');
      const summary = () => {
        const drafts = siblings.filter((r) => r.status === 'Draft').length;
        const active = siblings.filter((r) => r.status === 'Active').length;
        const cancelled = siblings.filter((r) => r.status === 'Cancelled').length;
        return {
          originTransactionId: plan[1],
          status:
            drafts === siblings.length
              ? 'Draft'
              : active === siblings.length
                ? 'Active'
                : cancelled === siblings.length
                  ? 'Cancelled'
                  : drafts
                    ? 'Mixed'
                    : 'Active',
          accountId: siblings[0]!.accountId,
          date: siblings[0]!.date,
          draftCount: drafts,
          activeCount: active,
          cancelledCount: cancelled,
          totalValue: siblings
            .filter((r) => r.status !== 'Cancelled')
            .reduce((s, r) => s + r.value, 0),
          installments: siblings.map(json),
        };
      };
      if (!plan[2]) return response(summary());
      const listed = body.installments as { transactionId: string; version: number }[];
      const drafts = siblings.filter((r) => r.status === 'Draft');
      if (summary().status === 'Mixed' && !body.confirmMixedPlan)
        return err(409, 'Confirme as parcelas restantes do plano.');
      if (
        listed.length !== drafts.length ||
        drafts.some(
          (d) => !listed.some((l) => l.transactionId === d.id && Number(l.version) === d.version),
        )
      )
        return err(409, 'O plano mudou. Releia antes de finalizar.');
      drafts.forEach((r) => {
        r.status = 'Active';
        r.version++;
      });
      return response(summary());
    }
    const one = /^\/transaction\/([0-9a-f-]{36})(\/finalize|\/cancel|\/edit-preview)?$/.exec(path);
    if (one) {
      const row = state.rows.get(one[1]!);
      if (!row) return err(404, 'Transação não encontrada.');
      if (role === 'Member' && !state.grants.includes(row.accountId))
        return err(403, 'Conta fora do seu escopo.');
      if (method === 'GET') return response(json(row), 200, { etag: `"${row.version}"` });
      if (one[2] === '/edit-preview')
        return role === 'Member'
          ? err(403, 'Somente a gerência vê o impacto.')
          : response({
              transactionId: row.id,
              impact: {
                receivableImpact: {
                  rowAppearsInOpenReceivables: false,
                  rowDisappearsFromOpenReceivables: false,
                },
                fiadoBalanceImpact: { deltas: [] },
                cashVarianceImpact: { varianceDelta: 0 },
              },
              warnings: [],
            });
      if (row.generationKind)
        return err(409, 'Movimentação automática: altere pela operação de origem.');
      const match = new Headers(init.headers).get('if-match');
      if (!match) return err(428, 'If-Match é obrigatório.');
      if (match !== `"${row.version}"`)
        return err(409, 'A transação foi alterada por outra pessoa. Releia antes de salvar.');
      if (
        role === 'Member' &&
        (row.recordedByOperatorId !== L.op || row.date.slice(0, 10) !== state.today)
      )
        return err(403, 'Você só altera as suas transações do dia.');
      const counterpart = row.generatedTransactionId
        ? state.rows.get(row.generatedTransactionId)
        : undefined;
      if (one[2] === '/finalize') {
        if (row.status !== 'Draft') return err(409, 'Só rascunhos são finalizados.');
        if (row.originTransactionId) return err(409, 'Parcelas de plano são finalizadas no plano.');
        row.status = 'Active';
        row.version++;
        if (counterpart) counterpart.status = 'Active';
        return response(json(row), 200, { etag: `"${row.version}"` });
      }
      if (one[2] === '/cancel') {
        if ((state.events.get(row.id) ?? []).length)
          return err(409, 'O recebível já tem eventos e não pode ser cancelado.');
        row.status = 'Cancelled';
        row.cancellationReason = String(body.cancellationReason);
        row.cancelledAt = '2026-10-27T15:00:00Z';
        row.version++;
        if (counterpart) counterpart.status = 'Cancelled';
        return response(json(row), 200, { etag: `"${row.version}"` });
      }
      if (method === 'PUT') {
        if (body.clientId !== row.clientId && (state.events.get(row.id) ?? []).length)
          return err(409, 'O cliente do recebível já foi identificado por um evento.');
        Object.assign(row, {
          description: body.description ?? null,
          dueDate: `${body.dueDate}T00:00:00`,
          clientId: body.clientId ?? null,
          transactionTime: body.transactionTime ?? null,
          version: row.version + 1,
          updatedAt: '2026-10-27T15:00:00Z',
        });
        return response(json(row), 200, { etag: `"${row.version}"` });
      }
    }
    const receivableRead = /^\/receivable\/([0-9a-f-]{36})$/.exec(path);
    if (receivableRead) {
      if (role === 'Member') return err(403, 'Sem permissão.');
      const row = state.rows.get(receivableRead[1]!);
      return row ? response(receivable(row)) : err(404, 'Não encontrado.');
    }
    if (path.startsWith('/cheque/')) {
      if (role === 'Member') return err(403, 'Sem permissão.');
      return idempotent(`${method} ${path}`, init, body, () => {
        if (path === '/cheque/custody' || path === '/cheque/deposit') {
          const loaded = loadCheques(body.items as never);
          if (loaded instanceof Response) return loaded;
          const custody = path === '/cheque/custody';
          for (const row of loaded) {
            const s = receivable(row).chequeState;
            if (
              custody
                ? !['Received', 'InSafe', 'Returned', 'AwaitingRedeposit'].includes(s)
                : !['Received', 'InSafe', 'AwaitingRedeposit'].includes(s)
            )
              return err(409, 'O cheque não está em situação para esta ação.');
          }
          let receipt: { id: string; amount: number; bankAccountId: string } | null = null;
          if (!custody) {
            receipt = {
              id: next(),
              amount: loaded.reduce((s, r) => s + r.value, 0),
              bankAccountId: body.bankAccountId as string,
            };
            state.receipts.set(receipt.id, receipt);
          }
          for (const row of loaded)
            chequeEvent(
              row,
              custody ? 'CustodyMoved' : 'ChequeDeposited',
              custody
                ? { custodyToAccountId: body.safeAccountId as string }
                : { bankReceiptId: receipt!.id },
            );
          return {
            status: 200,
            body: {
              receivables: loaded.map(receivable),
              bankReceipt: receipt
                ? {
                    id: receipt.id,
                    amount: receipt.amount,
                    bankAccountId: receipt.bankAccountId,
                    bankAccountName: 'Banco',
                    status: 'Pending',
                    sourceKind: 'ChequeDeposit',
                    feeEvidenceVersion: 0,
                    feeEvidenceHistory: [],
                    feeUnknown: false,
                    isDebit: false,
                    version: 1,
                    occurredOn: '',
                    postedOn: '',
                    returnedAmount: 0,
                    expectedAmount: receipt.amount,
                    createdByUserId: userId,
                    createdAt: '',
                    allocations: [],
                  }
                : null,
            },
          };
        }
        const target = /^\/cheque\/([0-9a-f-]{36})\/(return|represent|convert-to-fiado)$/.exec(
          path,
        );
        if (!target) return err(404, 'Não encontrado.');
        const loaded = loadCheques([
          { transactionId: target[1]!, expectedVersion: Number(body.expectedVersion) },
        ]);
        if (loaded instanceof Response) return loaded;
        const row = loaded[0]!;
        const s = receivable(row).chequeState;
        if (target[2] === 'return') {
          if (s !== 'Deposited') return err(409, 'Só um cheque depositado pode ser devolvido.');
          if (receivable(row).currentDepositEventId !== body.depositEventId)
            return err(409, 'O depósito informado não é o atual.');
          chequeEvent(row, 'ChequeReturned', {
            clientId: body.clientId as string,
            nextExpectedDepositDate: body.nextExpectedDepositDate as string,
            returnReason: (body.returnReason as string) ?? null,
          });
        } else if (target[2] === 'represent') {
          if (s !== 'Returned') return err(409, 'Só um cheque devolvido pode ser reapresentado.');
          chequeEvent(row, 'ChequeRepresented');
        } else {
          if (!['Received', 'InSafe', 'Returned', 'AwaitingRedeposit'].includes(s))
            return err(409, 'O cheque não pode ser convertido.');
          const debt = add({
            transactionTypeId: L.conversion,
            accountId: body.tabAccountId as string,
            value: row.value,
            date: `${state.today}T00:00:00`,
            clientId: body.clientId as string,
            generationKind: 'ChequeFiadoConversion',
            generatedFromTransactionId: row.id,
          });
          chequeEvent(row, 'ChequeConvertedToFiado', {
            clientId: body.clientId as string,
            convertedTransactionId: debt.id,
          });
          return {
            status: 200,
            body: { receivables: [receivable(row)], convertedTransactionId: debt.id },
          };
        }
        return { status: 200, body: { receivables: [receivable(row)] } };
      });
    }
    return undefined;
  }
  return { state, handle, add, receivable };
}
