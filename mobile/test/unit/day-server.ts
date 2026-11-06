// In-memory model of the shipped v63 session contract for Mobile M5 provider/UI tests. Rules mirror
// SessionStarter, PutDailyCloseItemsUseCase, SessionCountService/AllowedActionsResolver, the
// handover/finish use cases and the strong If-Match/Idempotency-Key envelopes. The test server
// owns the calculation exactly like the real server; the app under test never computes it.
import { response, userId, branchId } from './support';

export const uid = (n: number) => `70000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
export const ids = {
  me: userId,
  other: uid(2),
  cash: uid(10),
  raspa: uid(11),
  variance: uid(12),
  box: uid(20),
  box2: uid(21),
  terminal: uid(30),
  tab: uid(22),
};
type Status = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
interface Attempt {
  id: string;
  number: number;
  kind: 'Initial' | 'Recount' | 'StillObserved' | 'Correction';
  count: number;
  ledger: number;
  opening: number;
  items: Record<string, number | null>;
  totalOpening: number;
  totalIn: number;
  totalOut: number;
  totalClosing: number;
  expected: number;
  difference: number;
}
export interface FakeSession {
  id: string;
  version: number;
  date: string;
  status: Status;
  accountId: string;
  custodian: string;
  custodianName: string;
  custodyState: 'Active' | 'HandedOver' | 'Finished';
  sequence: number;
  opening: Record<string, number>;
  items: Record<string, number>;
  notes: string | null;
  fields: Record<'TerminalReport' | 'BolaoFee' | 'SpecialFee', number>;
  firstRecordedAt: string | null;
  recordedBy: string | null;
  countRevision: number;
  ledgerRevision: number;
  openingRevision: number;
  attempts: Attempt[];
  recounting: boolean;
  submittedAttemptId: string | null;
  submittedAt: string | null;
  rejectionReason: string | null;
  openingRecheckRequiredAt: string | null;
  updatedAt: string | null;
  ledgerIn: number;
  ledgerOut: number;
  events: unknown[];
}
export interface FakeHandover {
  id: string;
  version: number;
  outgoing: string;
  incoming: string;
  incomingName: string;
  state: 'Offered' | 'Accepted' | 'Disputed' | 'Withdrawn';
  countRevision: number;
  items: { productId: string; value: number }[];
  reason: string | null;
}
const products = [
  { id: ids.cash, name: 'Dinheiro', order: 1 },
  { id: ids.raspa, name: 'Raspadinha', order: 2 },
];
export function dayServer(options: { role?: string; grants?: string[]; linked?: boolean } = {}) {
  const state = {
    today: '2026-10-27',
    grants: options.grants ?? [ids.box],
    linked: options.linked ?? true,
    sessions: new Map<string, FakeSession>(),
    handovers: new Map<string, FakeHandover>(),
    openingCount: null as null | { id: string; revision: number; items: Record<string, number> },
    drafts: new Map<string, number>(),
    mineDrafts: new Map<string, number>(),
    replays: new Map<string, { status: number; body: unknown; fingerprint: string }>(),
    /** Stored rows of products no longer active (gap AA): kept in review, absent from /product. */
    retired: new Map<string, string>(),
    /** Session list page size (the cockpit must walk every page). */
    listPageSize: 50,
    seq: 100,
    fail: new Map<string, { status: number; body?: unknown; once?: boolean }>(),
    hold: new Map<string, Promise<void>>(),
    linkRequest: null as unknown,
  };
  const next = () => uid(++state.seq);
  const lastUrl = (calls: { url: string }[]) => new URL(calls.at(-1)!.url);
  const etag = (version: number) => ({ etag: `"${version}"` });
  const err = (status: number, message: string) => response({ errorMessages: [message] }, status);
  const latestCounted = (accountId: string) =>
    [...state.sessions.values()]
      .filter((s) => s.accountId === accountId && s.firstRecordedAt)
      .sort((a, b) => b.sequence - a.sequence)[0];
  const proposal = (accountId: string) => {
    const active = [...state.sessions.values()].find(
      (s) => s.accountId === accountId && s.custodyState === 'Active',
    );
    const pending = active
      ? [...state.handovers.values()].find(
          (h) => h.outgoing === active.id && (h.state === 'Offered' || h.state === 'Disputed'),
        )
      : undefined;
    const source = latestCounted(accountId);
    const values: Record<string, number> | null = source
      ? source.items
      : state.openingCount
        ? state.openingCount.items
        : null;
    return {
      accountId,
      accountName: accountId === ids.box ? 'Caixa 1' : 'Caixa 2',
      terminalId: ids.terminal,
      sourceKind: source ? 'PreviousSession' : state.openingCount ? 'OpeningCount' : null,
      sourceId: source?.id ?? state.openingCount?.id ?? null,
      sourceRevision: source?.countRevision ?? state.openingCount?.revision ?? null,
      sourceDate: source?.date ?? (state.openingCount ? '2026-10-01' : null),
      isMissing: !values,
      activeSessionId: active?.id ?? null,
      pendingHandoverId: pending?.id ?? null,
      hasUnresolvedHistory: false,
      canStart: !active,
      items: products.map((p) => ({
        productId: p.id,
        productName: p.name,
        displayOrder: p.order,
        value: values ? (values[p.id] ?? 0) : null,
      })),
    };
  };
  const sum = (values: Record<string, number | null>) =>
    Object.values(values).reduce<number>((t, v) => t + (v ?? 0), 0);
  const calc = (s: FakeSession) => {
    const totalOpening = sum(s.opening);
    const totalIn =
      s.ledgerIn + Math.max(0, s.fields.TerminalReport) + s.fields.BolaoFee + s.fields.SpecialFee;
    const totalOut = s.ledgerOut + Math.max(0, -s.fields.TerminalReport);
    const totalClosing = sum(s.items);
    const expected = totalOpening + totalIn - totalOut;
    return {
      totalOpening,
      totalIn,
      totalOut,
      totalClosing,
      expected,
      difference: totalClosing - expected,
    };
  };
  const candidate = (s: FakeSession) => {
    const last = s.attempts.at(-1);
    if (s.recounting) return 'Recounting';
    if (!last) return 'NotConferred';
    if (last.count !== s.countRevision) return 'Outdated';
    if (last.ledger !== s.ledgerRevision || last.opening !== s.openingRevision)
      return 'Unconfirmed';
    return 'Current';
  };
  const attemptJson = (s: FakeSession, a: Attempt) => ({
    id: a.id,
    sessionId: s.id,
    attemptNumber: a.number,
    kind: a.kind,
    countRevision: a.count,
    ledgerRevision: a.ledger,
    openingRevision: a.opening,
    totalOpening: a.totalOpening,
    totalIn: a.totalIn,
    totalOut: a.totalOut,
    expectedClosing: a.expected,
    totalClosing: a.totalClosing,
    difference: a.difference,
    conferredByUserId: ids.me,
    conferredByName: 'Pessoa teste',
    conferredAt: '2026-10-27T20:00:00Z',
    items: Object.entries(a.items).map(([productId, countedValue]) => ({
      productId,
      productName: products.find((p) => p.id === productId)?.name ?? 'Tarifa antiga',
      openingValue: s.opening[productId] ?? null,
      countedValue,
    })),
  });
  const count = (s: FakeSession) => {
    const c = candidate(s);
    const last = s.attempts.at(-1);
    const revisionLabel = `${s.countRevision}-${s.ledgerRevision}-${s.openingRevision}`;
    return {
      confirmationToken: `confirm-${revisionLabel}`,
      countRevision: s.countRevision,
      ledgerRevision: s.ledgerRevision,
      openingRevision: s.openingRevision,
      candidateState: c,
      requiresStillObservedConfirmation: c === 'Unconfirmed',
      latestAttemptId: last?.id ?? null,
      latestAttemptNumber: last?.number ?? null,
      submittedAttemptId: s.submittedAttemptId,
      confirmedAttempt:
        last && (c === 'Current' || c === 'Unconfirmed') ? attemptJson(s, last) : null,
      unconfirmedRecalculation:
        c === 'Unconfirmed'
          ? {
              countRevision: s.countRevision,
              ledgerRevision: s.ledgerRevision,
              openingRevision: s.openingRevision,
              ...(() => {
                const r = calc(s);
                return { ...r, expectedClosing: r.expected };
              })(),
            }
          : null,
    };
  };
  const allowed = (s: FakeSession) => {
    const editable = s.status === 'Draft' || s.status === 'Rejected';
    const c = candidate(s);
    return {
      canSaveCount: editable,
      canConferir: editable && !!s.firstRecordedAt && c !== 'Current',
      canRecount: editable && s.attempts.length > 0 && c !== 'Recounting',
      canSubmit: editable && c === 'Current',
      canRecall: s.status === 'Submitted',
      canApprove: false,
      canApproveAsSingleManager: false,
      canReject: false,
      canReopen: false,
    };
  };
  const closeFields = (s: FakeSession) =>
    (['TerminalReport', 'BolaoFee', 'SpecialFee'] as const).map((field) => ({
      field,
      value: s.fields[field],
      revision: 1,
      transactionId: s.fields[field] !== 0 ? uid(800) : null,
    }));
  const sessionJson = (s: FakeSession) => ({
    id: s.id,
    version: s.version,
    date: s.date,
    status: s.status,
    accountId: s.accountId,
    accountName: s.accountId === ids.box ? 'Caixa 1' : 'Caixa 2',
    branchId,
    openedByUserId: ids.me,
    openedByUserName: 'Pessoa teste',
    recordedByUserId: s.recordedBy,
    recordedByUserName: s.recordedBy ? 'Pessoa teste' : null,
    submittedAt: s.submittedAt,
    rejectionReason: s.rejectionReason,
    notes: s.notes,
    itemsFirstRecordedAt: s.firstRecordedAt,
    openingRecheckRequiredAt: s.openingRecheckRequiredAt,
    createdAt: '2026-10-27T11:00:00Z',
    updatedAt: s.updatedAt,
    custody: {
      mappingState: 'Ready',
      origin: 'Started',
      custodyState: s.custodyState,
      sequence: s.sequence,
      custodianOperatorId: s.custodian,
      custodianOperatorName: s.custodianName,
      terminalId: ids.terminal,
      startedAt: '2026-10-27T11:00:00Z',
      countRevision: s.countRevision,
      ledgerRevision: s.ledgerRevision,
      openingRevision: s.openingRevision,
      openingAcknowledged: true,
      custodyHistoryComplete: true,
    },
    count: count(s),
    allowedActions: allowed(s),
    closeFields: closeFields(s),
    items: Object.entries(s.items).map(([productId, value]) => ({
      id: uid(900),
      productId,
      productName: products.find((p) => p.id === productId)?.name ?? '',
      value,
      createdAt: 'x',
    })),
  });
  const reviewJson = (s: FakeSession) => ({
    ...sessionJson(s),
    openingDiscrepancy: null,
    items: [
      ...products.map((p) => ({
        productId: p.id,
        productName: p.name,
        displayOrder: p.order,
        openingValue: s.opening[p.id] ?? 0,
        closingValue: s.items[p.id] ?? null,
        isCashVarianceProduct: false,
      })),
      ...[...state.retired]
        .filter(([id]) => id in s.items)
        .map(([id, name]) => ({
          productId: id,
          productName: name,
          displayOrder: 50,
          openingValue: s.opening[id] ?? null,
          closingValue: s.items[id] ?? null,
          isCashVarianceProduct: false,
        })),
      {
        productId: ids.variance,
        productName: 'Diferença Caixa',
        displayOrder: 99,
        openingValue: null,
        closingValue:
          s.status === 'Draft' || s.status === 'Rejected'
            ? null
            : (s.attempts.at(-1)?.difference ?? null),
        isCashVarianceProduct: true,
      },
    ],
  });
  const listRow = (s: FakeSession) => {
    const j = sessionJson(s);
    return {
      id: j.id,
      date: j.date,
      status: j.status,
      accountId: j.accountId,
      accountName: j.accountName,
      recordedByUserId: j.recordedByUserId,
      submittedAt: j.submittedAt,
      createdAt: j.createdAt,
      custody: j.custody,
    };
  };
  const handoverJson = (h: FakeHandover) => ({
    id: h.id,
    version: h.version,
    accountId: state.sessions.get(h.outgoing)!.accountId,
    outgoingSessionId: h.outgoing,
    intendedIncomingOperatorId: h.incoming,
    intendedIncomingOperatorName: h.incomingName,
    state: h.state,
    offeredCountRevision: h.countRevision,
    offeredByUserId: ids.me,
    offeredAt: '2026-10-27T21:00:00Z',
    reason: h.reason,
    items: h.items,
  });
  /** Idempotency-Key replay with canonical-body comparison (§6.1). */
  const idempotent = (init: RequestInit, endpoint: string, body: unknown, run: () => Response) => {
    const key = new Headers(init.headers).get('idempotency-key');
    if (!key) return err(400, 'A chave de idempotência é obrigatória.');
    const fingerprint = JSON.stringify(body);
    const prior = state.replays.get(endpoint + key);
    if (prior)
      return prior.fingerprint === fingerprint
        ? response(prior.body, prior.status)
        : err(409, 'A chave de idempotência já foi usada com outro conteúdo.');
    const result = run();
    if (result.status < 300)
      void result
        .clone()
        .json()
        .then((json) =>
          state.replays.set(endpoint + key, { status: result.status, body: json, fingerprint }),
        );
    return result;
  };
  const ifMatch = (init: RequestInit) => {
    const value = new Headers(init.headers).get('if-match');
    return value && /^"\d+"$/.test(value) ? Number(value.slice(1, -1)) : null;
  };
  const start = (accountId: string, custodian: string, opening: Record<string, number>) => {
    const s: FakeSession = {
      id: next(),
      version: 1,
      // The API serializes the business date as a date-time at midnight (actual server shape).
      date: `${state.today}T00:00:00`,
      status: 'Draft',
      accountId,
      custodian,
      custodianName: custodian === ids.me ? 'Pessoa teste' : 'Ana',
      custodyState: 'Active',
      sequence: state.seq,
      opening,
      items: {},
      notes: null,
      fields: { TerminalReport: 0, BolaoFee: 0, SpecialFee: 0 },
      firstRecordedAt: null,
      recordedBy: null,
      countRevision: 1,
      ledgerRevision: 1,
      openingRevision: 1,
      attempts: [],
      recounting: false,
      submittedAttemptId: null,
      submittedAt: null,
      rejectionReason: null,
      openingRecheckRequiredAt: null,
      updatedAt: null,
      ledgerIn: 0,
      ledgerOut: 0,
      events: [],
    };
    state.sessions.set(s.id, s);
    return s;
  };
  async function handle(path: string, init: RequestInit, calls: { url: string }[]) {
    const method = init.method ?? 'GET';
    const url = lastUrl(calls);
    const q = url.searchParams;
    const body = typeof init.body === 'string' ? JSON.parse(init.body) : undefined;
    const failure = state.fail.get(`${method} ${path}`);
    if (failure) {
      if (failure.once) state.fail.delete(`${method} ${path}`);
      return response(failure.body ?? { errorMessages: ['Falha simulada.'] }, failure.status);
    }
    const held = state.hold.get(`${method} ${path}`);
    if (held) await held;
    if (path === '/branch/my-branches')
      return response({
        branches: [{ id: branchId, name: 'Filial', role: options.role ?? 'Member' }],
      });
    if (path === '/operator/self-context')
      return response(
        state.linked
          ? {
              operatorId: ids.me,
              operatorName: 'Pessoa teste',
              primaryAccount: null,
              availableAccounts: [
                ...state.grants.map((accountId, i) => ({
                  id: uid(600 + i),
                  operatorId: ids.me,
                  accountId,
                  isPrimary: i === 0,
                  accountType: 'Terminal',
                  accountName: accountId === ids.box ? 'Caixa 1' : 'Caixa 2',
                  pairedTabAccountId: ids.tab,
                })),
                {
                  id: uid(650),
                  operatorId: ids.me,
                  accountId: ids.tab,
                  isPrimary: false,
                  accountType: 'Tab',
                  accountName: 'Fiado 1',
                },
              ],
            }
          : { operatorId: null, availableAccounts: [], primaryAccount: null },
      );
    if (path === '/operator-link-request/self') return response({ request: state.linkRequest });
    if (path === '/operator')
      return response({
        operators: [
          { id: ids.me, name: 'Pessoa teste', branchId },
          { id: ids.other, name: 'Ana', branchId },
        ],
      });
    if (path === '/account')
      return response({
        accounts: [
          { id: ids.box, type: 'Terminal', name: 'Caixa 1', branchId, terminalId: ids.terminal },
        ],
      });
    if (path === '/product')
      return response({
        items: products.map((p) => ({
          id: p.id,
          name: p.name,
          displayOrder: p.order,
          branchId,
          createdAt: 'x',
          active: true,
        })),
      });
    if (path === '/terminal') return response([{ id: ids.terminal, name: 'T-01', active: true }]);
    if (path === '/timeentry')
      return response({
        items: [],
        page: 1,
        pageSize: 1,
        totalCount: 0,
        totalPages: 0,
        hasNext: false,
        hasPrevious: false,
      });
    if (path === '/transaction' && method === 'GET') {
      const account = q.get('AccountId') ?? '';
      const total =
        q.get('Status') === 'Draft'
          ? ((q.get('Mine') === 'true' ? state.mineDrafts : state.drafts).get(account) ?? 0)
          : 0;
      return response({
        items: [],
        page: 1,
        pageSize: 1,
        totalCount: total,
        totalPages: total ? 1 : 0,
        hasNext: false,
        hasPrevious: false,
      });
    }
    if (path === '/session/opening-proposal') return response(proposal(q.get('accountId')!));
    if (path === '/session' && method === 'GET') {
      const rows = [...state.sessions.values()].filter(
        (s) =>
          (!q.get('AccountId') || s.accountId === q.get('AccountId')) &&
          (!q.get('Status') || s.status === q.get('Status')) &&
          (!q.get('DateFrom') || s.date.slice(0, 10) >= q.get('DateFrom')!) &&
          (!q.get('DateTo') || s.date.slice(0, 10) <= q.get('DateTo')!) &&
          (q.get('Mine') !== 'true' || s.recordedBy === ids.me) &&
          (options.role !== 'Member' || state.grants.includes(s.accountId)),
      );
      const size = state.listPageSize;
      const page = Number(q.get('Page') ?? 1);
      const pages = Math.max(1, Math.ceil(rows.length / size));
      return response({
        items: rows.slice((page - 1) * size, page * size).map(listRow),
        page,
        pageSize: size,
        totalCount: rows.length,
        totalPages: pages,
        hasNext: page < pages,
        hasPrevious: page > 1,
      });
    }
    if (path === '/session/start' && method === 'POST')
      return idempotent(init, 'start', body, () => {
        const p = proposal(body.accountId);
        if (!p.canStart) return err(409, 'Este caixa já tem um turno ativo.');
        const opening = body.opening;
        if (
          (opening.sourceKind ?? null) !== p.sourceKind ||
          (opening.sourceId ?? null) !== p.sourceId ||
          (opening.sourceRevision ?? null) !== p.sourceRevision
        )
          return err(
            409,
            p.isMissing && opening.sourceKind
              ? 'A abertura proposta não existe mais.'
              : 'A abertura proposta mudou. Confira de novo.',
          );
        const items: Record<string, number> = Object.fromEntries(
          (opening.items ?? []).map((i: { productId: string; value: number }) => [
            i.productId,
            i.value,
          ]),
        );
        if (p.isMissing && !opening.explicitZero)
          return err(409, 'Sem abertura registrada: confirme a abertura zero.');
        if (!p.isMissing && p.items.some((i) => !(i.productId in items)))
          return err(400, 'Informe todos os produtos da abertura.');
        if (!p.isMissing && Object.values(items).every((v) => v === 0) && !opening.explicitZero)
          return err(409, 'Confirme a abertura zero.');
        if (
          opening.custodianOperatorId &&
          opening.custodianOperatorId !== ids.me &&
          !opening.reason
        )
          return err(400, 'Informe o motivo para iniciar o turno de outro operador.');
        const s = start(body.accountId, opening.custodianOperatorId ?? ids.me, items);
        const delta = p.isMissing
          ? 0
          : sum(items) - sum(Object.fromEntries(p.items.map((i) => [i.productId, i.value ?? 0])));
        return response({
          session: sessionJson(s),
          sessionOpeningId: uid(500),
          openingDiscrepancy: delta
            ? {
                id: uid(501),
                version: 1,
                sessionId: s.id,
                sessionOpeningId: uid(500),
                sourceKind: p.sourceKind,
                currentRevision: 1,
                currentTotalDelta: delta,
                attribution: 'Unattributed',
                countsTowardPerson: false,
                allowedTargets: [],
                items: [],
              }
            : null,
        });
      });
    const match = /^\/session\/([0-9a-f-]{36})(?:\/([a-z-]+))?$/.exec(path);
    const handoverMatch = /^\/session\/handover\/([0-9a-f-]{36})\/(accept|dispute|withdraw)$/.exec(
      path,
    );
    if (handoverMatch) {
      const h = state.handovers.get(handoverMatch[1]!);
      if (!h) return err(404, 'Passagem não encontrada.');
      if (ifMatch(init) !== h.version)
        return err(409, 'A passagem mudou. Releia antes de continuar.');
      if (handoverMatch[2] === 'accept')
        return idempotent(init, 'accept' + h.id, body, () => {
          const outgoing = state.sessions.get(h.outgoing)!;
          outgoing.custodyState = 'HandedOver';
          outgoing.version++;
          const items: Record<string, number> = Object.fromEntries(
            body.items.map((i: { productId: string; value: number }) => [i.productId, i.value]),
          );
          const incoming = start(outgoing.accountId, ids.me, items);
          h.state = 'Accepted';
          h.version++;
          const delta =
            sum(items) - sum(Object.fromEntries(h.items.map((i) => [i.productId, i.value])));
          return response({
            handover: handoverJson(h),
            outgoingSession: sessionJson(outgoing),
            incoming: {
              session: sessionJson(incoming),
              sessionOpeningId: uid(502),
              openingDiscrepancy: delta
                ? {
                    id: uid(503),
                    version: 1,
                    sessionId: incoming.id,
                    sessionOpeningId: uid(502),
                    sourceKind: 'PreviousSession',
                    currentRevision: 1,
                    currentTotalDelta: delta,
                    attribution: 'Unattributed',
                    countsTowardPerson: false,
                    allowedTargets: [],
                    items: [],
                  }
                : null,
            },
          });
        });
      if (!body?.reason) return err(400, 'Informe o motivo.');
      h.state = handoverMatch[2] === 'dispute' ? 'Disputed' : 'Withdrawn';
      h.reason = body.reason;
      h.version++;
      return response(handoverJson(h));
    }
    if (!match) return undefined;
    const s = state.sessions.get(match[1]!);
    if (!s) return err(404, 'Turno não encontrado.');
    const action = match[2];
    if (method === 'GET' && !action) return response(sessionJson(s), 200, etag(s.version));
    if (method === 'GET' && action === 'review')
      return response(reviewJson(s), 200, etag(s.version));
    if (method === 'GET' && action === 'history')
      return response({
        dailyCloseId: s.id,
        isHistoryComplete: true,
        events: s.events,
        custodyEvents: [],
        handovers: [...state.handovers.values()]
          .filter((h) => h.outgoing === s.id)
          .map(handoverJson),
        isCustodyHistoryComplete: true,
        countAttempts: s.attempts.map((a) => attemptJson(s, a)),
      });
    const guarded = [
      'items',
      'conferir',
      'recount',
      'submit',
      'finish',
      'handover',
      'custody-exception',
    ];
    if (guarded.includes(action ?? '') && ifMatch(init) === null)
      return err(400, 'O cabeçalho If-Match é obrigatório.');
    if (guarded.includes(action ?? '') && ifMatch(init) !== s.version)
      return err(409, 'O fechamento foi alterado por outra pessoa. Recarregue antes de salvar.');
    if (method === 'PUT' && action === 'items') {
      if (s.status !== 'Draft' && s.status !== 'Rejected')
        return err(409, 'Este fechamento não pode ser editado.');
      const items: Record<string, number> = Object.fromEntries(
        body.items.map((i: { productId: string; value: number }) => [i.productId, i.value]),
      );
      if (Object.keys(items).some((id) => !products.some((p) => p.id === id)))
        return err(404, 'Produto não encontrado ou inativo.');
      if (
        (body.closeFields ?? []).some(
          (f: { field: string; value: number }) => f.field !== 'TerminalReport' && f.value < 0,
        )
      )
        return err(400, 'A tarifa não pode ser negativa.');
      const changed = JSON.stringify(items) !== JSON.stringify(s.items) || !s.firstRecordedAt;
      s.items = items;
      if (body.notes !== undefined && body.notes !== null)
        s.notes = body.notes === '' ? null : body.notes;
      for (const f of body.closeFields ?? []) {
        if (s.fields[f.field as 'BolaoFee'] !== f.value) {
          s.fields[f.field as 'BolaoFee'] = f.value;
          s.ledgerRevision++;
        }
      }
      if (changed) {
        s.countRevision++;
        s.recounting = false;
      }
      if (!s.firstRecordedAt) {
        s.firstRecordedAt = '2026-10-27T19:00:00Z';
        s.recordedBy = ids.me;
      }
      if (s.status === 'Rejected') s.status = 'Draft';
      s.version++;
      s.updatedAt = '2026-10-27T19:05:00Z';
      return response(
        { dailyClose: sessionJson(s), affectedSuccessor: null },
        200,
        etag(s.version),
      );
    }
    if (method === 'POST' && action === 'conferir')
      return idempotent(init, 'conferir' + s.id, body, () => {
        if (!allowed(s).canConferir) return err(409, 'Não é possível conferir agora.');
        if (body.confirmationToken !== count(s).confirmationToken)
          return err(409, 'A conferência está desatualizada. Recarregue.');
        if (candidate(s) === 'Unconfirmed' && !body.confirmCountStillObserved)
          return err(409, 'Confirme que a contagem ainda representa o caixa ou reconte.');
        const r = calc(s);
        const a: Attempt = {
          id: next(),
          number: s.attempts.length + 1,
          kind:
            candidate(s) === 'Unconfirmed' ? 'StillObserved' : s.recounting ? 'Recount' : 'Initial',
          count: s.countRevision,
          ledger: s.ledgerRevision,
          opening: s.openingRevision,
          items: { ...s.items },
          ...r,
        };
        s.attempts.push(a);
        s.recounting = false;
        s.version++;
        return response(
          { attempt: attemptJson(s, a), replayed: false, session: sessionJson(s) },
          200,
          etag(s.version),
        );
      });
    if (method === 'POST' && action === 'recount')
      return idempotent(init, 'recount' + s.id, body, () => {
        if (body.previousAttemptId !== s.attempts.at(-1)?.id)
          return err(409, 'A conferência mudou.');
        s.recounting = true;
        s.version++;
        return response(sessionJson(s), 200, etag(s.version));
      });
    if (method === 'POST' && action === 'submit') {
      const last = s.attempts.at(-1);
      if (
        candidate(s) !== 'Current' ||
        body.attemptId !== last?.id ||
        body.ledgerRevision !== s.ledgerRevision
      )
        return err(409, 'Confira a contagem antes de enviar: o movimento ou a contagem mudou.');
      if ((state.drafts.get(s.accountId) ?? 0) > 0)
        return err(409, 'Há movimentações em rascunho neste caixa.');
      s.status = 'Submitted';
      s.submittedAttemptId = last!.id;
      s.submittedAt = '2026-10-27T21:00:00Z';
      s.version++;
      return response(sessionJson(s), 200, etag(s.version));
    }
    if (method === 'POST' && action === 'recall') {
      s.status = 'Draft';
      s.submittedAt = null;
      s.version++;
      return response(sessionJson(s), 200, etag(s.version));
    }
    if (method === 'POST' && action === 'finish')
      return idempotent(init, 'finish' + s.id, body, () => {
        if (s.status !== 'Submitted')
          return err(409, 'Envie o fechamento antes de encerrar o turno.');
        s.custodyState = 'Finished';
        s.version++;
        return response(sessionJson(s), 200, etag(s.version));
      });
    if (method === 'POST' && action === 'handover')
      return idempotent(init, 'handover' + s.id, body, () => {
        if (s.status !== 'Submitted') return err(409, 'Envie a contagem antes de passar o caixa.');
        if (body.countRevision !== s.countRevision) return err(409, 'A contagem mudou.');
        const h: FakeHandover = {
          id: next(),
          version: 1,
          outgoing: s.id,
          incoming: body.incomingOperatorId,
          incomingName: body.incomingOperatorId === ids.other ? 'Ana' : 'Pessoa teste',
          state: 'Offered',
          countRevision: s.countRevision,
          items: Object.entries(s.items).map(([productId, value]) => ({ productId, value })),
          reason: body.reason ?? null,
        };
        state.handovers.set(h.id, h);
        return response(handoverJson(h));
      });
    return undefined;
  }
  return {
    state,
    handle,
    start,
    /** Another actor's ledger change after Conferir (ledger revision only, count unchanged). */
    ledgerChange: (sessionId: string, value: number) => {
      const s = state.sessions.get(sessionId)!;
      s.ledgerIn += value;
      s.ledgerRevision++;
      s.version++;
    },
    /** Another device saves this close (a newer version). */
    concurrentSave: (sessionId: string, productId: string, value: number) => {
      const s = state.sessions.get(sessionId)!;
      s.items = { ...s.items, [productId]: value };
      s.countRevision++;
      s.version++;
    },
    offer: (sessionId: string, incoming: string) => {
      const s = state.sessions.get(sessionId)!;
      const h: FakeHandover = {
        id: next(),
        version: 1,
        outgoing: s.id,
        incoming,
        incomingName: 'Pessoa teste',
        state: 'Offered',
        countRevision: s.countRevision,
        items: Object.entries(s.items).map(([productId, value]) => ({ productId, value })),
        reason: null,
      };
      state.handovers.set(h.id, h);
      return h;
    },
  };
}
