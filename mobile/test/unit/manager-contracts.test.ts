import {
  allClear,
  approvalChoice,
  approveBody,
  batchBody,
  batchGroups,
  batchMode,
  closeRoute,
  notSubmittedRoute,
  queueGroups,
  selectedQueueDate,
  managerRole,
  noLockSet,
  explanationCheck,
  sameBatchSet,
  type BatchCandidate,
} from '../../src/manager/contracts';

const close = (over: Record<string, unknown>) =>
  ({
    dailyCloseId: 'a',
    accountId: 'box',
    accountName: 'Caixa',
    status: 'Submitted',
    varianceValue: null,
    ...over,
  }) as never;
const queue = (over: Record<string, unknown> = {}) =>
  ({
    closes: [],
    notSubmitted: [],
    backlog: { submittedCount: 0, rejectedCount: 0, draftCount: 0, oldestPendingDate: null },
    unlockedDraftTransactionCount: 0,
    pendingOperatorLinkRequestCount: 0,
    custody: {
      openSessions: [],
      pendingHandovers: [],
      openingDiscrepanciesAwaitingAttribution: [],
      unresolvedMappingIssueCount: 0,
    },
    ...over,
  }) as never;
const cand = (over: Partial<BatchCandidate> = {}): BatchCandidate => ({
  sessionId: 's',
  version: 1,
  accountId: 'box',
  accountName: 'Caixa',
  date: '2026-10-27',
  custodianName: null,
  sequence: 1,
  canApprove: true,
  canApproveAsSingleManager: false,
  blockedReason: null,
  ...over,
});

describe('queue groups', () => {
  it('orders variance by absolute returned value without recalculating', () => {
    const g = queueGroups(
      queue({
        closes: [
          close({ dailyCloseId: '1', varianceValue: -2 }),
          close({ dailyCloseId: '2', varianceValue: 9 }),
          close({ dailyCloseId: '3', varianceValue: 0 }),
          close({ dailyCloseId: '4', varianceValue: -9 }),
        ],
      }),
    );
    expect(g.variance.map((c) => c.dailyCloseId)).toEqual(['2', '4', '1']);
  });
  it('keeps sessions of one box and day distinct', () => {
    const g = queueGroups(
      queue({ closes: [close({ dailyCloseId: '1' }), close({ dailyCloseId: '2' })] }),
    );
    expect(g.pending).toHaveLength(2);
  });
  it('is all-clear only when every region is empty', () => {
    expect(allClear(queue(), 0)).toBe(true);
    expect(allClear(queue(), 1)).toBe(false);
    expect(
      allClear(queue({ backlog: { submittedCount: 1, rejectedCount: 0, draftCount: 0 } }), 0),
    ).toBe(false);
    expect(allClear(queue({ unlockedDraftTransactionCount: 1 }), 0)).toBe(false);
  });
});

describe('routing', () => {
  it('routes by status', () => {
    expect(closeRoute({ dailyCloseId: 'x', status: 'Submitted' })).toBe('/gestao/conferencia/x');
    expect(closeRoute({ dailyCloseId: 'x', status: 'Approved' })).toBe('/gestao/conferencia/x');
    expect(closeRoute({ dailyCloseId: 'x', status: 'Draft' })).toBe('/fechamentos/x');
    expect(closeRoute({ dailyCloseId: 'x', status: 'Rejected' })).toBe('/fechamentos/x/correcao');
  });
  it('a missing expected close opens the day for that cashbox and date', () => {
    expect(
      notSubmittedRoute(
        { accountId: 'b', accountName: 'B', operatorId: 'o', operatorName: 'O' },
        '2026-10-20',
      ),
    ).toBe('/fechamentos/abrir?accountId=b&date=2026-10-20');
  });
});

describe('selected date and authority', () => {
  it('defaults to today and never accepts a future or invalid date', () => {
    expect(selectedQueueDate(undefined, '2026-10-27')).toBe('2026-10-27');
    expect(selectedQueueDate('2026-10-28', '2026-10-27')).toBe('2026-10-27');
    expect(selectedQueueDate('2026-02-30', '2026-10-27')).toBe('2026-10-27');
    expect(selectedQueueDate('2026-10-20', '2026-10-27')).toBe('2026-10-20');
  });
  it('only Manager and Admin', () => {
    expect(managerRole('Manager')).toBe(true);
    expect(managerRole('Admin')).toBe(true);
    expect(managerRole('Member')).toBe(false);
  });
});

describe('approval choice', () => {
  const review = (a: Record<string, unknown>) =>
    ({
      status: 'Submitted',
      allowedActions: { canApprove: false, canApproveAsSingleManager: false, ...a },
    }) as never;
  it('independent when the server allows it', () => {
    const c = approvalChoice(review({ canApprove: true }), '');
    expect(approveBody(c)).toEqual({ mode: 'Independent' });
  });
  it('single-manager needs a reason and is only offered by the server flag', () => {
    expect(approvalChoice(review({ canApproveAsSingleManager: true }), '  ').kind).toBe('blocked');
    const c = approvalChoice(review({ canApproveAsSingleManager: true }), ' ok ');
    expect(approveBody(c)).toEqual({ mode: 'SingleActiveManager', reason: 'ok' });
  });
  it('never approves without a server offer', () => {
    expect(approveBody(approvalChoice(review({ approvalBlockedReason: 'x' }), 'r'))).toBeNull();
  });
});

describe('cashbox-day batch', () => {
  it('groups per box and date, one candidate per session', () => {
    const groups = batchGroups([
      cand({ sessionId: 'a' }),
      cand({ sessionId: 'b' }),
      cand({ sessionId: 'c', accountId: 'o' }),
    ]);
    expect(groups.map((g) => g.sessions.length)).toEqual([2, 1]);
  });
  it('one non-approvable row means no batch mode', () => {
    expect(batchMode([cand(), cand({ canApprove: false })])).toBe('none');
    expect(batchMode([cand(), cand()])).toBe('independent');
    expect(batchMode([cand(), cand({ canApprove: false, canApproveAsSingleManager: true })])).toBe(
      'single-manager',
    );
  });
  it('sends exactly the fetched sessions and versions', () => {
    const body = batchBody(
      [cand({ sessionId: 'a', version: 7 }), cand({ sessionId: 'b', version: 9 })],
      'independent',
      '',
    );
    expect(body).toEqual({
      sessions: [
        { sessionId: 'a', version: 7 },
        { sessionId: 'b', version: 9 },
      ],
      mode: 'Independent',
    });
    expect(batchBody([cand()], 'single-manager', '')).toBeNull();
  });
});

describe('lock date', () => {
  it('treats the minimum date as no lock', () => {
    expect(noLockSet('0001-01-01T00:00:00')).toBe(true);
    expect(noLockSet('2026-09-30')).toBe(false);
  });
});

describe('independent review corrections', () => {
  it('orders unapproved variance before approved, then by magnitude', () => {
    const g = queueGroups(
      queue({
        closes: [
          close({ dailyCloseId: 'big-approved', status: 'Approved', varianceValue: -90 }),
          close({ dailyCloseId: 'small-submitted', status: 'Submitted', varianceValue: -1 }),
          close({ dailyCloseId: 'big-submitted', status: 'Submitted', varianceValue: 20 }),
        ],
      }),
    );
    expect(g.variance.map((c) => c.dailyCloseId)).toEqual([
      'big-submitted',
      'small-submitted',
      'big-approved',
    ]);
  });
  it('is never all-clear while month blockers are unknown', () => {
    expect(allClear(queue(), 0)).toBe(true);
    expect(allClear(queue(), null)).toBe(false);
  });
  it('detects an omitted opening product by identity and a differing official value', () => {
    const review = {
      items: [{ productId: 'a', isCashVarianceProduct: true, closingValue: -5 }],
    } as never;
    const attempt = (items: unknown[], difference: number) => ({ items, difference }) as never;
    expect(
      explanationCheck(
        review,
        attempt([{ productId: 'a', productName: 'A', openingValue: 1 }], -5),
      ),
    ).toEqual({ omitted: [], differs: false });
    const withFee = explanationCheck(
      review,
      attempt([{ productId: 'fee', productName: 'Taxa', openingValue: 7 }], -5),
    );
    expect(withFee.omitted).toEqual([{ productId: 'fee', productName: 'Taxa', openingValue: 7 }]);
    expect(explanationCheck(review, attempt([], -4)).differs).toBe(true);
    // A null opening value names nothing to omit.
    expect(
      explanationCheck(
        review,
        attempt([{ productId: 'x', productName: 'X', openingValue: null }], -5),
      ).omitted,
    ).toEqual([]);
  });
  it('compares reviewed batch sets by ids, versions and offered actions', () => {
    const a = [cand({ sessionId: '1', version: 10 })];
    expect(sameBatchSet(a, [cand({ sessionId: '1', version: 10 })])).toBe(true);
    expect(sameBatchSet(a, [cand({ sessionId: '1', version: 11 })])).toBe(false);
    expect(sameBatchSet(a, [cand({ sessionId: '1', version: 10, canApprove: false })])).toBe(false);
  });
});
