// Manager Work Queue & Approvals request composition and presentation rules (Mobile M7). Everything
// here mirrors the shipped .NET contract (v67): the server owns variance, locks, permissions,
// approval policy and the official difference. The device only groups, orders and routes returned
// rows — it never computes an expected closing, a difference, a total or an authorization.
import {
  OpeningDiscrepancyAttribution,
  SessionApprovalMode,
  type DailyCloseStatus,
  type RequestApproveSessionBatchJson,
  type RequestApproveSessionJson,
  type ResponseDailyCloseReviewJson,
  type ResponseManagerQueueCloseJson,
  type ResponseManagerQueueJson,
  type ResponseManagerQueueNotSubmittedJson,
  type ResponseOpeningDiscrepancyJson,
  type ResponseSessionCountAttemptJson,
  type Role,
} from '@loto/api';
import { parseCalendarDate } from '@loto/core';

/* Dates --------------------------------------------------------------------------------------- */

/** `YYYY-MM-DD` of an API business date (`…T00:00:00` or plain); never parsed as an instant. */
export const calendarPart = (value: string) => value.slice(0, 10);
/** A valid selected queue date: calendar-valid and never after the branch-local today. */
export function selectedQueueDate(raw: string | undefined, today: string): string {
  if (!raw || !parseCalendarDate(raw) || raw > today) return today;
  return raw;
}

/** The server's "nothing locked yet" sentinel is the minimum date (0001-01-01), never a real lock. */
export const noLockSet = (lockDate: string): boolean => calendarPart(lockDate) <= '0001-01-01';

/* Authority ----------------------------------------------------------------------------------- */

/** Manager/Admin only. The queue never depends on an operator link; a Member is refused before read. */
export const managerRole = (role: Role | null | undefined): boolean =>
  role === 'Manager' || role === 'Admin';

/* Queue groups -------------------------------------------------------------------------------- */

export interface QueueGroups {
  /** Submitted — awaiting a decision, in returned order. */
  readonly pending: readonly ResponseManagerQueueCloseJson[];
  /** Rejected — awaiting the operator's correction. */
  readonly rejected: readonly ResponseManagerQueueCloseJson[];
  /** Expected but not submitted (no close, Draft or Rejected-without-submission). */
  readonly notSubmitted: readonly ResponseManagerQueueNotSubmittedJson[];
  /** Presentation order only: largest absolute returned variance first. Never recalculated. */
  readonly variance: readonly ResponseManagerQueueCloseJson[];
  /** Approved — readonly review. */
  readonly approved: readonly ResponseManagerQueueCloseJson[];
}
const absVariance = (row: ResponseManagerQueueCloseJson) => Math.abs(row.varianceValue ?? 0);
export function queueGroups(queue: ResponseManagerQueueJson): QueueGroups {
  const by = (status: DailyCloseStatus) => queue.closes.filter((c) => c.status === status);
  return {
    pending: by('Submitted'),
    rejected: by('Rejected'),
    approved: by('Approved'),
    notSubmitted: queue.notSubmitted,
    variance: queue.closes
      .filter(
        (c) => c.varianceValue !== null && c.varianceValue !== undefined && c.varianceValue !== 0,
      )
      .map((c, index) => ({ c, index }))
      // Unapproved work first; magnitude only orders rows within each of the two groups.
      .sort(
        (a, b) =>
          Number(a.c.status === 'Approved') - Number(b.c.status === 'Approved') ||
          absVariance(b.c) - absVariance(a.c) ||
          a.index - b.index,
      )
      .map(({ c }) => c),
  };
}
/** The queue is "clear" only when every returned fixed group is empty. */
export function allClear(queue: ResponseManagerQueueJson, blockers: number | null): boolean {
  // Unknown month blockers (a failed read) are never zero.
  if (blockers === null) return false;
  const g = queueGroups(queue);
  return (
    g.pending.length === 0 &&
    g.rejected.length === 0 &&
    g.notSubmitted.length === 0 &&
    g.variance.length === 0 &&
    blockers === 0 &&
    queue.backlog.submittedCount === 0 &&
    queue.backlog.rejectedCount === 0 &&
    queue.backlog.draftCount === 0 &&
    queue.unlockedDraftTransactionCount === 0 &&
    queue.pendingOperatorLinkRequestCount === 0 &&
    queue.custody.openingDiscrepanciesAwaitingAttribution.length === 0 &&
    queue.custody.pendingHandovers.length === 0 &&
    queue.custody.unresolvedMappingIssueCount === 0 &&
    !queue.custody.openSessions.some((s) => s.startedBeforeToday)
  );
}

/* Routing ------------------------------------------------------------------------------------- */

const enc = encodeURIComponent;
export const queuePath = (date?: string | null, row?: string | null) => {
  const q = new URLSearchParams();
  if (date) q.set('date', date);
  if (row) {
    q.set('row', row);
    q.set('focus', row);
  }
  return '/gestao/fila' + (q.size ? '?' + q.toString() : '');
};
export const reviewPath = (id: string) => `/gestao/conferencia/${id}`;
/** Status-routed row: Submitted/Approved → review, Draft → Close day, Rejected → Fix & resubmit. */
export function closeRoute(row: { dailyCloseId: string; status: DailyCloseStatus }): string {
  switch (row.status) {
    case 'Submitted':
    case 'Approved':
      return reviewPath(row.dailyCloseId);
    case 'Draft':
      return `/fechamentos/${row.dailyCloseId}`;
    case 'Rejected':
      return `/fechamentos/${row.dailyCloseId}/correcao`;
  }
}
/** Missing expected close → supported Open day for the cashbox/date; existing row → its status route. */
export function notSubmittedRoute(row: ResponseManagerQueueNotSubmittedJson, date: string): string {
  if (row.dailyCloseId && row.status)
    return closeRoute({ dailyCloseId: row.dailyCloseId, status: row.status });
  const q = new URLSearchParams({ accountId: row.accountId, date });
  return '/fechamentos/abrir?' + q.toString();
}
/** Unlocked Draft transactions resolve in the real M6 list, filtered to Draft. */
/** Independent window: the unlocked-draft count has no start date tied to the pending closes. */
export const draftTransactionsPath = () =>
  '/transacoes?' + new URLSearchParams({ status: 'Draft' });
/** The real operators screen; the per-request resolution itself belongs to M11. */
export const linkRequestsPath = () => '/gestao/operadores';
/** Validated custody exception/offer links reuse the real M5 session routes. */
export const sessionPath = (id: string) => `/fechamentos/${id}`;
export const discrepancyReviewPath = (sessionId: string, discrepancyId: string) => {
  const q = new URLSearchParams({ section: 'discrepancy', row: discrepancyId });
  return reviewPath(sessionId) + '?' + q.toString();
};

/* Explanation check ---------------------------------------------------------------------------- */

export interface ExplanationCheck {
  /** Attempt opening products (non-null OpeningValue) that the comparison rows omit, by identity. */
  readonly omitted: readonly { productId: string; productName: string; openingValue: number }[];
  /** True when the submitted attempt's difference is not the persisted official value. */
  readonly differs: boolean;
}
/**
 * Identity and equality only (never arithmetic): a product named by the attempt but absent from the
 * rows omits part of the opening, and the attempt's difference must equal the official cash row.
 */
export function explanationCheck(
  review: Pick<ResponseDailyCloseReviewJson, 'items'>,
  attempt: Pick<ResponseSessionCountAttemptJson, 'items' | 'difference'>,
): ExplanationCheck {
  const shown = new Set(review.items.map((i) => i.productId));
  const omitted = attempt.items
    .filter(
      (i) => i.openingValue !== null && i.openingValue !== undefined && !shown.has(i.productId),
    )
    .map((i) => ({
      productId: i.productId,
      productName: i.productName,
      openingValue: i.openingValue as number,
    }));
  const official = review.items.find((i) => i.isCashVarianceProduct)?.closingValue;
  return {
    omitted,
    differs:
      official === null || official === undefined || !Object.is(official, attempt.difference),
  };
}

/* Approval ------------------------------------------------------------------------------------ */

export type ApprovalChoice =
  | { kind: 'independent' }
  | { kind: 'single-manager'; reason: string }
  | { kind: 'blocked'; reason: string | null }
  | { kind: 'not-allowed' };
/**
 * What the server's own `AllowedActions` offers. A role/menu/local actor comparison is never
 * authority, and the people count is never used: only the returned flags decide.
 */
export function approvalChoice(
  review: Pick<ResponseDailyCloseReviewJson, 'status' | 'allowedActions'>,
  reason: string,
): ApprovalChoice {
  if (review.status !== 'Submitted') return { kind: 'not-allowed' };
  const a = review.allowedActions;
  if (a.canApprove) return { kind: 'independent' };
  if (a.canApproveAsSingleManager) {
    const trimmed = reason.trim();
    return trimmed
      ? { kind: 'single-manager', reason: trimmed }
      : { kind: 'blocked', reason: null };
  }
  return { kind: 'blocked', reason: a.approvalBlockedReason ?? null };
}
export function approveBody(choice: ApprovalChoice): RequestApproveSessionJson | null {
  if (choice.kind === 'independent') return { mode: SessionApprovalMode.Independent };
  if (choice.kind === 'single-manager')
    return { mode: SessionApprovalMode.SingleActiveManager, reason: choice.reason };
  return null;
}
/** Single-manager is only ever offered when the server says canApproveAsSingleManager. */
export const offersSingleManager = (
  review: Pick<ResponseDailyCloseReviewJson, 'allowedActions'>,
): boolean => !review.allowedActions.canApprove && review.allowedActions.canApproveAsSingleManager;
export const canReject = (r: Pick<ResponseDailyCloseReviewJson, 'status' | 'allowedActions'>) =>
  r.status === 'Submitted' && r.allowedActions.canReject;
export const canReopen = (r: Pick<ResponseDailyCloseReviewJson, 'allowedActions'>) =>
  r.allowedActions.canReopen;
export const canRecall = (r: Pick<ResponseDailyCloseReviewJson, 'allowedActions'>) =>
  r.allowedActions.canRecall;

/* Cashbox-day batch --------------------------------------------------------------------------- */

export interface BatchCandidate {
  readonly sessionId: string;
  readonly version: number;
  readonly accountId: string;
  readonly accountName: string;
  readonly date: string;
  readonly custodianName: string | null;
  readonly sequence: number | null;
  readonly canApprove: boolean;
  readonly canApproveAsSingleManager: boolean;
  readonly blockedReason: string | null;
}
export const toBatchCandidate = (r: ResponseDailyCloseReviewJson): BatchCandidate => ({
  sessionId: r.id,
  version: r.version,
  accountId: r.accountId,
  accountName: r.accountName,
  date: calendarPart(r.date),
  custodianName: r.custody.custodianOperatorName ?? null,
  sequence: r.custody.sequence ?? null,
  canApprove: r.status === 'Submitted' && r.allowedActions.canApprove,
  canApproveAsSingleManager:
    r.status === 'Submitted' &&
    !r.allowedActions.canApprove &&
    r.allowedActions.canApproveAsSingleManager,
  blockedReason: r.allowedActions.approvalBlockedReason ?? null,
});
export interface BatchGroup {
  readonly key: string;
  readonly accountId: string;
  readonly accountName: string;
  readonly date: string;
  readonly sessions: readonly BatchCandidate[];
}
/** One group per current cashbox/business date; every session stays its own row. */
export function batchGroups(candidates: readonly BatchCandidate[]): BatchGroup[] {
  const map = new Map<string, BatchCandidate[]>();
  for (const c of candidates) {
    const key = `${c.accountId}|${c.date}`;
    map.set(key, [...(map.get(key) ?? []), c]);
  }
  return [...map.entries()].map(([key, sessions]) => ({
    key,
    accountId: sessions[0]!.accountId,
    accountName: sessions[0]!.accountName,
    date: sessions[0]!.date,
    sessions,
  }));
}
/** The reviewed selection is replaced only on an explicit accept: ids, versions and offered actions must all match. */
export function sameBatchSet(a: readonly BatchCandidate[], b: readonly BatchCandidate[]): boolean {
  return (
    a.length === b.length &&
    a.every((x, i) => {
      const y = b[i]!;
      return (
        x.sessionId === y.sessionId &&
        x.version === y.version &&
        x.canApprove === y.canApprove &&
        x.canApproveAsSingleManager === y.canApproveAsSingleManager
      );
    })
  );
}
export type BatchMode = 'independent' | 'single-manager' | 'none';
/**
 * The whole fetched group must be selectable under ONE mode the server offers for every row; a
 * mixed or fully blocked group cannot be submitted (no silent subset, no looped single approvals).
 */
export function batchMode(sessions: readonly BatchCandidate[]): BatchMode {
  if (sessions.length === 0) return 'none';
  if (sessions.every((s) => s.canApprove)) return 'independent';
  if (sessions.every((s) => s.canApprove || s.canApproveAsSingleManager)) return 'single-manager';
  return 'none';
}
/** The server's approve-day envelope: explicit SessionId + Version list, mode and optional reason. */
export function batchBody(
  sessions: readonly BatchCandidate[],
  mode: BatchMode,
  reason: string,
): RequestApproveSessionBatchJson | null {
  if (mode === 'none' || sessions.length === 0) return null;
  if (mode === 'single-manager' && !reason.trim()) return null;
  return {
    sessions: sessions.map((s) => ({ sessionId: s.sessionId, version: s.version })),
    mode:
      mode === 'independent'
        ? SessionApprovalMode.Independent
        : SessionApprovalMode.SingleActiveManager,
    ...(mode === 'single-manager' ? { reason: reason.trim() } : {}),
  };
}

/* Opening discrepancy attribution ------------------------------------------------------------- */

export type AttributionTarget = Exclude<OpeningDiscrepancyAttribution, 'Unattributed'>;
export const attributionOptions = (
  d: Pick<ResponseOpeningDiscrepancyJson, 'allowedTargets'>,
): AttributionTarget[] =>
  d.allowedTargets.filter(
    (t): t is AttributionTarget => t !== OpeningDiscrepancyAttribution.Unattributed,
  );
/** An unavailable outgoing target never becomes an option; the server's AllowedTargets decide. */
export const targetOffered = (
  d: Pick<ResponseOpeningDiscrepancyJson, 'allowedTargets'>,
  target: AttributionTarget,
) => d.allowedTargets.includes(target);

/** Session ids in the form a route may carry. */
export const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export const isId = (value: unknown): value is string =>
  typeof value === 'string' && uuidPattern.test(value);
export { enc as encodeQuery };
