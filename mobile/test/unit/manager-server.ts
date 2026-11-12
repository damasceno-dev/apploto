// In-memory model of the shipped v67 Manager queue / review / approval / attribution contract for
// Mobile M7 provider/UI tests. The server owns every decision, version and refusal; the app under
// test only sends the explicit envelopes.
import { response } from './support';

export const mid = (n: number) => `80000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
export const ids = {
  box: mid(10),
  box2: mid(11),
  s1: mid(101),
  s2: mid(102),
  s3: mid(103),
  rejected: mid(104),
  draft: mid(105),
  disc: mid(201),
  discSession: mid(202),
  prod: mid(301),
};
type Status = 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
export interface FakeReview {
  id: string;
  version: number;
  status: Status;
  accountId: string;
  accountName: string;
  date: string;
  sequence: number;
  custodian: string;
  canApprove: boolean;
  canSingle: boolean;
  blocked: string | null;
  canReject: boolean;
  canReopen: boolean;
  variance: number | null;
}
export function managerServer() {
  const state = {
    today: '2026-10-27',
    reviews: new Map<string, FakeReview>(),
    fail: new Map<string, { status: number; body?: unknown }>(),
    backlog: { submittedCount: 0, rejectedCount: 0, draftCount: 0, oldest: null as string | null },
    notSubmitted: [] as unknown[],
    discrepancy: null as null | {
      version: number;
      revision: number;
      locked: boolean;
      targets: string[];
    },
    adjustments: [] as unknown[],
    decisions: [] as string[],
    keys: new Map<string, unknown>(),
    pendingLinks: 0,
    blockers: [] as unknown[],
    mapping: 0,
    unlockedDrafts: 0,
    lockDate: '2026-09-30',
    transactions: [
      {
        id: mid(901),
        description: 'Venda manual',
        value: 30,
        direction: 'In',
        generationKind: null as string | null,
      },
      {
        id: mid(902),
        description: 'Relatório do terminal',
        value: 20,
        direction: 'In',
        generationKind: 'SessionTerminalReport' as string | null,
      },
    ],
    /** Attempt evidence overrides (product identities / difference) for the explanation check. */
    attemptItems: null as null | { productId: string; productName: string; openingValue: number }[],
    attemptDifference: -5,
    manyTransactions: 0,
    /** `METHOD /path` routes whose responses never arrive (a pending read). */
    hold: new Set<string>(),
    /** Overrides what the SPECIFIC responsibility read allows (the review copy keeps `targets`). */
    responsibilityTargets: null as null | string[],
    /** Total adjustments returned page by page when more than the page size exist. */
    pagedAdjustments: 0,
    alertPeople: [] as unknown[],
    meanVariance: -3.5,
    reconciliationFails: false,
    /** `METHOD /path` routes whose NEXT response is lost after the server executed them. */
    dropAfter: new Set<string>(),
  };
  const add = (r: Partial<FakeReview> & { id: string }) => {
    state.reviews.set(r.id, {
      version: 10,
      status: 'Submitted',
      accountId: ids.box,
      accountName: 'Caixa 1',
      date: state.today,
      sequence: 1,
      custodian: 'Operadora A',
      canApprove: true,
      canSingle: false,
      blocked: null,
      canReject: true,
      canReopen: false,
      variance: -5,
      ...r,
    });
  };
  const closeRow = (r: FakeReview) => ({
    dailyCloseId: r.id,
    accountId: r.accountId,
    accountName: r.accountName,
    recordedByOperatorName: r.custodian,
    submittedByOperatorName: r.custodian,
    status: r.status,
    varianceValue: r.variance,
  });
  const queue = (date: string) => ({
    date,
    totalVariance: -7,
    meanVariance: state.meanVariance,
    pendingOperatorLinkRequestCount: state.pendingLinks,
    pendingApprovalCount: [...state.reviews.values()].filter((r) => r.status === 'Submitted')
      .length,
    closes: [...state.reviews.values()].filter((r) => r.date === date).map(closeRow),
    notSubmitted: state.notSubmitted,
    backlog: {
      submittedCount: state.backlog.submittedCount,
      rejectedCount: state.backlog.rejectedCount,
      draftCount: state.backlog.draftCount,
      oldestPendingDate: state.backlog.oldest,
    },
    unlockedDraftTransactionCount: state.unlockedDrafts,
    custody: {
      openSessions: [],
      pendingHandovers: [],
      openingDiscrepanciesAwaitingAttribution: state.discrepancy
        ? [
            {
              openingDiscrepancyId: ids.disc,
              sessionId: ids.discSession,
              sessionOpeningId: mid(203),
              accountId: ids.box,
              accountName: 'Caixa 1',
              date,
              custodianOperatorName: 'Operadora A',
              currentRevision: state.discrepancy.revision,
              currentTotalDelta: -3,
              attribution: 'Unattributed',
              version: state.discrepancy.version,
            },
          ]
        : [],
      unresolvedMappingIssueCount: state.mapping,
    },
    personalDifferences: {
      dateFrom: '2026-10-01',
      dateTo: '2026-10-31',
      asOfDate: date,
      currentShortageThreshold: null,
      automaticPayrollDeduction: false,
      unassignedSourceCount: 0,
      totalCount: 0,
      page: 1,
      pageSize: 20,
      people: state.alertPeople,
      entries: [],
    },
  });
  const review = (r: FakeReview) => ({
    id: r.id,
    version: r.version,
    date: `${r.date}T00:00:00`,
    status: r.status,
    accountId: r.accountId,
    accountName: r.accountName,
    branchId: '20000000-0000-4000-8000-000000000001',
    openedByUserId: mid(1),
    openedByUserName: 'Abertura',
    recordedByOperatorName: r.custodian,
    submittedByOperatorName: r.custodian,
    createdAt: '2026-10-27T10:00:00Z',
    custody: {
      mappingState: 'Mapped',
      origin: 'Native',
      custodianOperatorName: r.custodian,
      sequence: r.sequence,
      countRevision: 1,
      ledgerRevision: 1,
      openingRevision: 1,
      openingAcknowledged: true,
      custodyHistoryComplete: true,
    },
    count: {
      confirmationToken: 't',
      countRevision: 1,
      ledgerRevision: 1,
      openingRevision: 1,
      candidateState: 'Current',
      requiresStillObservedConfirmation: false,
      submittedAttemptId: mid(900),
      confirmedAttempt: {
        id: mid(900),
        sessionId: r.id,
        attemptNumber: 1,
        kind: 'Initial',
        countRevision: 1,
        ledgerRevision: 1,
        openingRevision: 1,
        totalOpening: 100,
        totalIn: 50,
        totalOut: 20,
        expectedClosing: 130,
        totalClosing: 125,
        difference: state.attemptDifference,
        conferredByUserId: mid(1),
        conferredByName: 'Conferente',
        conferredAt: '2026-10-27T11:00:00Z',
        items: (
          state.attemptItems ?? [
            { productId: ids.prod, productName: 'Bolão Mega', openingValue: 100 },
          ]
        ).map((i) => ({ ...i, countedValue: 125 })),
      },
    },
    allowedActions: {
      canSaveCount: false,
      canConferir: false,
      canRecount: false,
      canSubmit: false,
      canRecall: r.status === 'Submitted',
      canApprove: r.status === 'Submitted' && r.canApprove,
      canApproveAsSingleManager: r.status === 'Submitted' && r.canSingle,
      approvalBlockedReason: r.blocked,
      canReject: r.status === 'Submitted' && r.canReject,
      canReopen: r.canReopen || r.status === 'Approved',
    },
    closeFields: [],
    openingDiscrepancy:
      state.discrepancy && r.id === ids.discSession
        ? {
            id: ids.disc,
            version: state.discrepancy.version,
            sessionId: r.id,
            sessionOpeningId: mid(203),
            sourceKind: 'Handover',
            currentRevision: state.discrepancy.revision,
            currentTotalDelta: -3,
            attribution: 'Unattributed',
            countsTowardPerson: true,
            allowedTargets: state.discrepancy.targets,
            items: [{ productId: ids.prod, proposedValue: 10, acknowledgedValue: 7, delta: -3 }],
          }
        : null,
    items: [
      {
        productId: ids.prod,
        productName: 'Bolão Mega',
        displayOrder: 1,
        openingValue: 100,
        closingValue: -5,
        isCashVarianceProduct: true,
      },
    ],
  });
  /** Adjustments in pages of 20 by Sequence, with the server's `nextAfterSequence` cursor. */
  const pageOf = (after: number) => {
    const all = state.pagedAdjustments
      ? Array.from({ length: state.pagedAdjustments }, (_, i) => ({
          id: mid(5000 + i),
          discrepancyId: ids.disc,
          comparisonRevision: 1,
          sequence: i + 1,
          sessionId: ids.discSession,
          sourceDate: '2026-09-20T00:00:00',
          postedOn: '2026-10-01',
          target: 'Neither',
          previousSignedAmount: 0,
          signedAmount: 0,
          actorUserId: mid(1),
          createdAt: '2026-10-27T12:00:00Z',
          reason: `Ajuste ${i + 1}`,
        }))
      : (state.adjustments as { sequence: number }[]);
    const page = all.filter((a) => a.sequence > after).slice(0, 20);
    const more = all.filter((a) => a.sequence > after).length > page.length;
    return {
      adjustments: page,
      nextAfterSequence: more ? page[page.length - 1]!.sequence : null,
    };
  };
  const err = (status: number, ...messages: string[]) =>
    response({ errorMessages: messages }, status);
  function process(path: string, init: RequestInit, calls: { path: string; url: string }[]) {
    const method = init.method ?? 'GET';
    const headers = new Headers(init.headers);
    const url = new URL(calls[calls.length - 1]!.url);
    const body = typeof init.body === 'string' ? JSON.parse(init.body) : undefined;
    if (state.hold.has(`${method} ${path}`)) return new Promise<Response>(() => undefined);
    const injected = state.fail.get(`${method} ${path}`);
    if (injected)
      return Promise.resolve(
        response(injected.body ?? { errorMessages: ['Falha simulada.'] }, injected.status),
      );
    const out = (r: Response) => Promise.resolve(r);
    if (method === 'GET' && path === '/report/manager-queue')
      return out(response(queue(url.searchParams.get('Date')!)));
    if (method === 'GET' && path === '/setting')
      return out(
        response({
          id: mid(1),
          version: 1,
          lockDate: state.lockDate,
          dailyTargetHours: 8,
          lunchDeductionOver6H: 1,
          lunchDeductionOver4H: 0,
          branchId: mid(2),
          createdAt: '2026-01-01T00:00:00Z',
          active: true,
        }),
      );
    if (method === 'GET' && path.startsWith('/report/monthly-reconciliation'))
      return out(
        state.reconciliationFails
          ? err(500, 'Falha na conciliação.')
          : response({
              year: 2026,
              month: 10,
              lockReady: false,
              isMonthLocked: false,
              isMonthElapsed: false,
              days: [],
              blockers: state.blockers,
            }),
      );
    if (method === 'GET' && path === '/transaction') {
      const all = state.manyTransactions
        ? Array.from({ length: state.manyTransactions }, (_, i) => ({
            id: mid(8000 + i),
            description: `Lançamento ${i + 1}`,
            value: 1,
            direction: 'In',
            generationKind: null as string | null,
          }))
        : state.transactions;
      const size = Number(url.searchParams.get('PageSize') ?? 100);
      const page = Number(url.searchParams.get('Page') ?? 1);
      return out(
        response({
          items: all.slice((page - 1) * size, page * size).map((t) => ({
            ...t,
            date: state.today,
            accountId: ids.box,
            status: 'Active',
          })),
          totalCount: all.length,
          page,
          pageSize: size,
        }),
      );
    }
    if (method === 'GET' && path === '/operator-link-request')
      return out(response({ items: [], totalCount: state.pendingLinks, page: 1, pageSize: 5 }));
    const m = /^\/session\/([0-9a-f-]{36})\/(review|history|approve|reject|reopen)$/.exec(path);
    if (m) {
      const r = state.reviews.get(m[1]!);
      if (!r) return out(err(404, 'Fechamento não encontrado.'));
      if (m[2] === 'review') return out(response(review(r), 200, { etag: `"${r.version}"` }));
      if (m[2] === 'history')
        return out(
          response({
            dailyCloseId: r.id,
            isHistoryComplete: true,
            events: [],
            custodyEvents: [],
            handovers: [],
            isCustodyHistoryComplete: true,
            countAttempts: [review(r).count.confirmedAttempt],
          }),
        );
      if (m[2] === 'approve') {
        state.decisions.push(`approve ${r.id}`);
        // Deterministic replay: the same key returns the stored outcome without a second decision.
        const replay = state.keys.get(`approve ${headers.get('idempotency-key')}`);
        if (replay) return out(response(replay, 200));
        if (headers.get('if-match') !== `"${r.version}"`)
          return out(err(412, 'O fechamento mudou. Releia antes de aprovar.'));
        if (r.status !== 'Submitted')
          return out(err(409, 'Só é possível aprovar fechamentos enviados.'));
        if (!r.canApprove && !(r.canSingle && body?.mode === 'SingleActiveManager' && body?.reason))
          return out(err(422, r.blocked ?? 'Aprovação não permitida.'));
        r.status = 'Approved';
        r.version++;
        state.keys.set(`approve ${headers.get('idempotency-key')}`, review(r));
        return out(response(review(r), 200, { etag: `"${r.version}"` }));
      }
      if (m[2] === 'reject') {
        state.decisions.push(`reject ${r.id}`);
        if (r.status !== 'Submitted')
          return out(err(409, 'Só é possível reprovar fechamentos enviados.'));
        r.status = 'Rejected';
        r.version++;
        return out(response(review(r)));
      }
      state.decisions.push(`reopen ${r.id}`);
      r.status = 'Draft';
      r.version++;
      return out(response(review(r)));
    }
    if (method === 'POST' && path === '/session/approve-day') {
      state.decisions.push('approve-day');
      const lines: string[] = [];
      for (const s of body.sessions as { sessionId: string; version: number }[]) {
        const r = state.reviews.get(s.sessionId);
        if (!r || r.status !== 'Submitted') lines.push(`Sessão ${s.sessionId}: não está enviada.`);
        else if (String(s.version) !== String(r.version))
          lines.push(`Sessão ${s.sessionId}: versão desatualizada.`);
      }
      if (lines.length) return out(err(409, ...lines));
      const done = (body.sessions as { sessionId: string }[]).map((s) => {
        const r = state.reviews.get(s.sessionId)!;
        r.status = 'Approved';
        r.version++;
        return review(r);
      });
      return out(response({ sessions: done }));
    }
    if (
      method === 'GET' &&
      path === `/session/opening-discrepancy/${ids.disc}/responsibility` &&
      state.discrepancy
    )
      return out(
        response({
          discrepancy: {
            ...review(state.reviews.get(ids.discSession)!).openingDiscrepancy,
            ...(state.responsibilityTargets ? { allowedTargets: state.responsibilityTargets } : {}),
          },
          sourceDate: '2026-09-20T00:00:00',
          sourceLocked: state.discrepancy.locked,
          latestAdjustment: null,
          ...pageOf(Number(url.searchParams.get('AfterSequence') ?? 0)),
        }),
      );
    if (
      method === 'POST' &&
      path === `/session/opening-discrepancy/${ids.disc}/attribution` &&
      state.discrepancy
    ) {
      state.decisions.push('attribute');
      if (headers.get('if-match') !== `"${state.discrepancy.version}"`)
        return out(err(412, 'Versão desatualizada.'));
      if (body.discrepancyRevision !== state.discrepancy.revision)
        return out(err(409, 'A revisão da divergência mudou.'));
      state.discrepancy.version++;
      return out(response({ attribution: body.target }));
    }
    if (
      method === 'POST' &&
      path === '/session/opening-discrepancy-adjustment' &&
      state.discrepancy
    ) {
      state.decisions.push('adjust');
      const adjustment = {
        id: mid(777),
        discrepancyId: body.discrepancyId,
        comparisonRevision: body.comparisonRevision,
        sequence: state.adjustments.length + 1,
        sessionId: ids.discSession,
        sourceDate: '2026-09-20T00:00:00',
        postedOn: body.postedOn,
        target: body.target,
        previousSignedAmount: 0,
        signedAmount: -3,
        actorUserId: mid(1),
        createdAt: '2026-10-27T12:00:00Z',
        reason: body.reason,
      };
      state.adjustments.push(adjustment);
      return out(response(adjustment));
    }
    return Promise.resolve(undefined);
  }
  /** A lost response: the write is executed, then the transport fails before the client reads it. */
  function handle(path: string, init: RequestInit, calls: { path: string; url: string }[]) {
    const result = process(path, init, calls);
    const route = `${init.method ?? 'GET'} ${path}`;
    if (result && state.dropAfter.has(route)) {
      state.dropAfter.delete(route);
      return result.then(() => {
        throw new TypeError('Network request failed');
      });
    }
    return result;
  }
  return { state, handle, add, review };
}
