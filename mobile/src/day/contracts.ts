// Operator Day request composition and presentation rules (Mobile M5). Everything here mirrors the
// shipped .NET session contract (v63); the server owns custody, the calculation, variance, locks,
// permissions and time. The device never computes an expected closing, a difference, a total or a
// next step from data that failed to load.
import {
  SessionCloseField,
  type ResponseDailyCloseJson,
  type ResponseDailyCloseReviewJson,
  type ResponseListDailyCloseItemJson,
  type ResponseOperatorAccountJson,
  type ResponseSelfContextJson,
  type ResponseSessionCloseFieldJson,
  type ResponseSessionCountAttemptJson,
  type ResponseSessionHandoverJson,
  type ResponseSessionOpeningProposalJson,
  type RequestPutDailyCloseItemsJson,
  type RequestSessionOpeningAcknowledgmentJson,
  type RequestSessionOpeningItemJson,
  type Role,
} from '@loto/api';
import {
  asCents,
  centsFromMoneyDigits,
  centsFromWire,
  moneyDigitsFromCents,
  normalizeMoneyDigits,
  parseCalendarDate,
  wireFromCents,
  type Cents,
} from '@loto/core';

/* Business dates ------------------------------------------------------------------------------ */

/**
 * The session/close `Date` is a calendar business date that the API serializes as
 * `YYYY-MM-DDT00:00:00`; the branch clock's `BranchLocalDate` is `YYYY-MM-DD`. Compare and route
 * only the calendar part — never parse either as an instant or apply a time zone.
 */
export const calendarPart = (value: string) => value.slice(0, 10);
export const sameBusinessDay = (a: string, b: string) => calendarPart(a) === calendarPart(b);

/* Money entry ---------------------------------------------------------------------------------- */

/** MoneyInput entry digits: `''` blank (não informado), `'0'` a recorded zero, else cents. */
export type Digits = string;
export const isDigits = (value: unknown): value is Digits =>
  typeof value === 'string' && (value === '' || normalizeMoneyDigits(value) === value);
export function digitsFromWire(value: number | null | undefined): Digits {
  if (value === null || value === undefined) return '';
  const cents = centsFromWire(value);
  return moneyDigitsFromCents(asCents(Math.abs(cents)));
}
export const wireFromDigits = (digits: Digits): number =>
  wireFromCents(centsFromMoneyDigits(digits === '' ? '0' : digits));

/** A close-form field: the magnitude typed in the field, the sign chosen beside it (Signed amount). */
export interface FieldEntry {
  readonly digits: Digits;
  readonly negative: boolean;
}
export const CLOSE_FIELDS = [
  SessionCloseField.TerminalReport,
  SessionCloseField.BolaoFee,
  SessionCloseField.SpecialFee,
] as const;
/** Only Relatório do terminal is signed; the two fees are never negative (server rule). */
export const signedField = (field: SessionCloseField) => field === 'TerminalReport';
/** Close-form fields have no blank state: a cleared field commits zero (Close day). */
export function fieldCents(entry: FieldEntry): Cents {
  const magnitude = centsFromMoneyDigits(entry.digits === '' ? '0' : entry.digits);
  return asCents(entry.negative && magnitude !== 0 ? -magnitude : magnitude);
}
export function fieldEntryFromWire(value: number): FieldEntry {
  const cents = centsFromWire(value);
  return { digits: moneyDigitsFromCents(asCents(Math.abs(cents))), negative: cents < 0 };
}

/* Count candidate ------------------------------------------------------------------------------ */

/**
 * The operator's input for one session: per review product (exact ProductId), Notes, the three
 * close-form fields and each field's raw arithmetic buffer. Stored on the device as typed; never a
 * token, version or confirmation precondition.
 */
export interface CountInput {
  readonly v: 1;
  readonly items: Readonly<Record<string, Digits>>;
  readonly notes: string;
  readonly fields: Readonly<Record<SessionCloseField, FieldEntry>>;
  /** Raw arithmetic editing buffers, keyed `item:{productId}` / `field:{field}`. Never sent. */
  readonly buffers: Readonly<Record<string, string>>;
}
const zeroField: FieldEntry = { digits: '0', negative: false };
function fieldsFrom(closeFields: readonly ResponseSessionCloseFieldJson[] | null | undefined) {
  const entries = Object.fromEntries(CLOSE_FIELDS.map((f) => [f, zeroField])) as Record<
    SessionCloseField,
    FieldEntry
  >;
  for (const field of closeFields ?? [])
    if ((CLOSE_FIELDS as readonly string[]).includes(field.field))
      entries[field.field] = fieldEntryFromWire(field.value);
  return entries;
}
/** The server's current count, as entry state, for exactly the review rows the screen shows. */
export function serverInput(review: ResponseDailyCloseReviewJson): CountInput {
  return {
    v: 1,
    items: Object.fromEntries(
      review.items
        .filter((row) => !row.isCashVarianceProduct)
        .map((row) => [row.productId, digitsFromWire(row.closingValue)]),
    ),
    notes: review.notes ?? '',
    fields: fieldsFrom(review.closeFields),
    buffers: {},
  };
}
/** The acknowledged snapshot of a save/Conferir response (active saved items only). */
export function sessionInput(
  session: ResponseDailyCloseJson,
  productIds: readonly string[],
): CountInput {
  const saved = new Map(session.items.map((item) => [item.productId, item.value]));
  return {
    v: 1,
    items: Object.fromEntries(
      productIds.map((id) => [id, saved.has(id) ? digitsFromWire(saved.get(id)!) : '']),
    ),
    notes: session.notes ?? '',
    fields: fieldsFrom(session.closeFields),
    buffers: {},
  };
}
export function sameCount(a: CountInput, b: CountInput): boolean {
  const ids = new Set([...Object.keys(a.items), ...Object.keys(b.items)]);
  for (const id of ids) if ((a.items[id] ?? '') !== (b.items[id] ?? '')) return false;
  return (
    a.notes === b.notes &&
    CLOSE_FIELDS.every((f) => fieldCents(a.fields[f]) === fieldCents(b.fields[f]))
  );
}
const fieldKeys = ['digits', 'negative'];
function isFieldEntry(value: unknown): value is FieldEntry {
  return (
    !!value &&
    typeof value === 'object' &&
    Object.keys(value).sort().join(',') === fieldKeys.join(',') &&
    isDigits((value as FieldEntry).digits) &&
    typeof (value as FieldEntry).negative === 'boolean'
  );
}
const isRecord = (value: unknown): value is Record<string, unknown> =>
  !!value && typeof value === 'object' && !Array.isArray(value);
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function isCountInput(value: unknown): value is CountInput {
  if (!isRecord(value) || Object.keys(value).sort().join(',') !== 'buffers,fields,items,notes,v')
    return false;
  const { items, fields, buffers } = value;
  return (
    value.v === 1 &&
    typeof value.notes === 'string' &&
    value.notes.length <= 1000 &&
    isRecord(items) &&
    Object.entries(items).every(([id, digits]) => uuid.test(id) && isDigits(digits)) &&
    isRecord(fields) &&
    Object.keys(fields).length === CLOSE_FIELDS.length &&
    CLOSE_FIELDS.every((f) => isFieldEntry(fields[f])) &&
    isRecord(buffers) &&
    Object.entries(buffers).every(
      ([key, raw]) =>
        /^(item:[0-9a-f-]{36}|field:(TerminalReport|BolaoFee|SpecialFee))$/i.test(key) &&
        typeof raw === 'string' &&
        raw.length <= 256,
    )
  );
}

/**
 * `PUT /session/{id}/items` body. The endpoint replaces the active item set: a blank row is omitted
 * (não informado), never sent as zero; the variance row is never sent. Only the rows the review
 * returned are considered — no product is added or reactivated. A close-form field is sent only
 * when its value differs from the acknowledged server value; an untouched field keeps its value.
 */
export function countRequest(
  review: Pick<ResponseDailyCloseReviewJson, 'items'>,
  input: CountInput,
  acknowledged: CountInput,
): RequestPutDailyCloseItemsJson {
  const closeFields = CLOSE_FIELDS.filter(
    (f) => fieldCents(input.fields[f]) !== fieldCents(acknowledged.fields[f]),
  ).map((field) => ({ field, value: wireFromCents(fieldCents(input.fields[field])) }));
  return {
    items: review.items
      .filter((row) => !row.isCashVarianceProduct && (input.items[row.productId] ?? '') !== '')
      .map((row) => ({
        productId: row.productId,
        value: wireFromDigits(input.items[row.productId]!),
      })),
    notes: input.notes,
    ...(closeFields.length ? { closeFields } : {}),
  };
}

/** Today's plain Draft (autosave pattern): same branch day, Draft, no rejection, no recheck. */
export function autosaveEligible(
  review: Pick<
    ResponseDailyCloseReviewJson,
    'date' | 'status' | 'rejectionReason' | 'openingRecheckRequiredAt' | 'allowedActions'
  >,
  today: string,
): boolean {
  return (
    sameBusinessDay(review.date, today) &&
    review.status === 'Draft' &&
    !review.rejectionReason &&
    !review.openingRecheckRequiredAt &&
    review.allowedActions.canSaveCount
  );
}
/** Fix & resubmit applies to a rejection, an opening recheck, or their correction Draft. */
export const correctionContext = (
  review: Pick<
    ResponseDailyCloseReviewJson,
    'status' | 'rejectionReason' | 'openingRecheckRequiredAt'
  >,
) => review.status === 'Rejected' || !!review.rejectionReason || !!review.openingRecheckRequiredAt;

/* Repetir abertura / Desfazer repetição ------------------------------------------------------- */

/** Row-local, one step, in memory only: the exact pre-copy value, blank or unsaved text included. */
export type RepeatUndo = Readonly<Record<string, Digits>>;
export function repeatOpening(
  items: Readonly<Record<string, Digits>>,
  undo: RepeatUndo,
  productId: string,
  opening: Digits,
): { items: Readonly<Record<string, Digits>>; undo: RepeatUndo } {
  const current = items[productId] ?? '';
  // A no-change repeat keeps the originally retained value (and adds no undo).
  if (current === opening) return { items, undo };
  return {
    items: { ...items, [productId]: opening },
    undo: productId in undo ? undo : { ...undo, [productId]: current },
  };
}
export function undoRepeat(
  items: Readonly<Record<string, Digits>>,
  undo: RepeatUndo,
  productId: string,
): { items: Readonly<Record<string, Digits>>; undo: RepeatUndo } {
  if (!(productId in undo)) return { items, undo };
  const rest = { ...undo };
  const previous = rest[productId]!;
  delete rest[productId];
  return { items: { ...items, [productId]: previous }, undo: rest };
}
/** A manual edit of a row ends its undo; other rows keep theirs. */
export function endUndo(undo: RepeatUndo, productId: string): RepeatUndo {
  if (!(productId in undo)) return undo;
  const rest = { ...undo };
  delete rest[productId];
  return rest;
}

/**
 * After Conferir, an attempt product with a server opening that the displayed rows omit makes the
 * visible opening incomplete (catalog: identity comparison, never device arithmetic).
 */
export function omittedOpeningProducts(
  attempt: Pick<ResponseSessionCountAttemptJson, 'items'> | null | undefined,
  rows: readonly { readonly productId: string }[],
) {
  if (!attempt) return [];
  const shown = new Set(rows.map((row) => row.productId));
  return attempt.items.filter((item) => item.openingValue != null && !shown.has(item.productId));
}

/* Opening acknowledgment ----------------------------------------------------------------------- */

export interface OpeningInput {
  readonly v: 1;
  readonly accountId: string;
  /** Only the products the operator observed as different; every other row is the proposal. */
  readonly different: Readonly<Record<string, Digits>>;
  readonly explicitZero: boolean;
  readonly reason: string;
  readonly buffers: Readonly<Record<string, string>>;
}
export function isOpeningInput(value: unknown): value is OpeningInput {
  if (
    !isRecord(value) ||
    Object.keys(value).sort().join(',') !== 'accountId,buffers,different,explicitZero,reason,v'
  )
    return false;
  return (
    value.v === 1 &&
    typeof value.accountId === 'string' &&
    uuid.test(value.accountId) &&
    typeof value.explicitZero === 'boolean' &&
    typeof value.reason === 'string' &&
    value.reason.length <= 500 &&
    isRecord(value.different) &&
    Object.entries(value.different).every(([id, d]) => uuid.test(id) && isDigits(d)) &&
    isRecord(value.buffers) &&
    Object.entries(value.buffers).every(
      ([key, raw]) =>
        /^item:[0-9a-f-]{36}$/i.test(key) && typeof raw === 'string' && raw.length <= 256,
    )
  );
}
/** The identity of the proposal the input was typed against (a changed source invalidates it). */
export const proposalSource = (
  proposal: Pick<ResponseSessionOpeningProposalJson, 'sourceKind' | 'sourceId' | 'sourceRevision'>,
) => ({
  kind: proposal.sourceKind ?? null,
  id: proposal.sourceId ?? null,
  revision: proposal.sourceRevision ?? null,
});
/** Each row's acknowledged entry: the observed difference when typed, else the proposed value. */
export function acknowledgedDigits(
  proposal: Pick<ResponseSessionOpeningProposalJson, 'items' | 'isMissing'>,
  input: Pick<OpeningInput, 'different'>,
): Record<string, Digits> {
  return Object.fromEntries(
    proposal.items.map((row) => [
      row.productId,
      input.different[row.productId] !== undefined && input.different[row.productId] !== ''
        ? input.different[row.productId]!
        : proposal.isMissing
          ? '0'
          : digitsFromWire(row.value ?? 0),
    ]),
  );
}
/** The server refuses an all-zero acknowledgment unless it is explicit (and a missing proposal). */
export function zeroAcknowledgmentRequired(
  proposal: Pick<ResponseSessionOpeningProposalJson, 'items' | 'isMissing'>,
  input: Pick<OpeningInput, 'different'>,
): boolean {
  if (proposal.isMissing) return true;
  return Object.values(acknowledgedDigits(proposal, input)).every(
    (d) => centsFromMoneyDigits(d === '' ? '0' : d) === 0,
  );
}
/**
 * `RequestSessionOpeningAcknowledgmentJson` for Start/handover: the echoed source, the full
 * acknowledged vector (the server requires every proposed product) and explicit zero. A missing
 * proposal acknowledges only an explicit zero, with no items and no echoed source.
 */
export function openingAcknowledgment(
  proposal: ResponseSessionOpeningProposalJson,
  input: OpeningInput,
  custodianOperatorId: string | null,
): RequestSessionOpeningAcknowledgmentJson {
  const reason = input.reason.trim();
  const base = {
    ...(custodianOperatorId ? { custodianOperatorId } : {}),
    ...(reason ? { reason } : {}),
  };
  if (proposal.isMissing) return { ...base, explicitZero: true, items: [] };
  return {
    ...base,
    sourceKind: proposal.sourceKind ?? null,
    sourceId: proposal.sourceId ?? null,
    sourceRevision: proposal.sourceRevision ?? null,
    explicitZero: zeroAcknowledgmentRequired(proposal, input) ? input.explicitZero : false,
    items: openingItems(acknowledgedDigits(proposal, input)),
  };
}
const openingItems = (digits: Record<string, Digits>): RequestSessionOpeningItemJson[] =>
  Object.entries(digits).map(([productId, d]) => ({ productId, value: wireFromDigits(d) }));
/** Incoming acknowledgment of an offered handover: its immutable vector unless observed otherwise. */
export function handoverAcceptance(
  handover: Pick<ResponseSessionHandoverJson, 'items'>,
  input: Pick<OpeningInput, 'different' | 'explicitZero' | 'reason'>,
) {
  const digits = Object.fromEntries(
    handover.items.map((item) => [
      item.productId,
      input.different[item.productId]
        ? input.different[item.productId]!
        : digitsFromWire(item.value),
    ]),
  );
  const allZero = Object.values(digits).every((d) => centsFromMoneyDigits(d) === 0);
  const reason = input.reason.trim();
  return {
    items: openingItems(digits),
    explicitZero: allZero ? input.explicitZero : false,
    ...(reason ? { reason } : {}),
  };
}

/* Workstation ---------------------------------------------------------------------------------- */

/** Cashboxes (AccountType Terminal) the caller holds a grant for; Tab and Bank never count. */
export function cashboxGrants(self: ResponseSelfContextJson | null | undefined) {
  const seen = new Set<string>();
  const result: ResponseOperatorAccountJson[] = [];
  for (const grant of self?.availableAccounts ?? [])
    if (grant.accountType === 'Terminal' && !seen.has(grant.accountId)) {
      seen.add(grant.accountId);
      result.push(grant);
    }
  return result;
}
export interface WorkstationChoice {
  readonly v: 1;
  readonly date: string;
  readonly accountId: string;
}
export function isWorkstationChoice(value: unknown): value is WorkstationChoice {
  return (
    isRecord(value) &&
    Object.keys(value).sort().join(',') === 'accountId,date,v' &&
    value.v === 1 &&
    typeof value.date === 'string' &&
    parseCalendarDate(value.date) !== null &&
    typeof value.accountId === 'string' &&
    uuid.test(value.accountId)
  );
}
/**
 * One cashbox binds automatically; several need the per-device, per-branch-day choice, valid only
 * for the server's current day and a still-granted cashbox. `PrimaryAccount` never guesses.
 */
export function resolveWorkstation(
  grants: readonly ResponseOperatorAccountJson[],
  choice: WorkstationChoice | null,
  today: string,
): ResponseOperatorAccountJson | 'choose' | 'none' {
  if (grants.length === 0) return 'none';
  if (grants.length === 1) return grants[0]!;
  if (choice && choice.date === today) {
    const chosen = grants.find((g) => g.accountId === choice.accountId);
    if (chosen) return chosen;
  }
  return 'choose';
}

/* Next action (P1–P7) -------------------------------------------------------------------------- */

export type Read<T> =
  | { readonly status: 'pending' }
  | { readonly status: 'error'; readonly error: unknown; readonly retry: () => void }
  | { readonly status: 'ok'; readonly data: T };
export interface CorrectionCandidate {
  readonly id: string;
  readonly date: string;
  readonly accountName: string;
  readonly rejectionReason: string | null;
  readonly openingRecheck: boolean;
}
export interface WorkstationFacts {
  readonly proposal: ResponseSessionOpeningProposalJson;
  /** The cashbox's current Active custody (any day), when there is one. */
  readonly active: ResponseDailyCloseJson | null;
  /** The cashbox's sessions with business date today. */
  readonly today: readonly ResponseListDailyCloseItemJson[];
  /** Pending offer addressed to this caller, from the active session's history. */
  readonly handoverForMe: ResponseSessionHandoverJson | null;
}
export interface DraftCounts {
  readonly total: number;
  readonly mine: number;
}
export type NextAction =
  | { readonly kind: 'unknown'; readonly what: string; readonly retry: () => void }
  | { readonly kind: 'correction'; readonly session: CorrectionCandidate }
  | { readonly kind: 'not-open'; readonly canStart: boolean; readonly blocked: StartBlock | null }
  | { readonly kind: 'receive'; readonly handover: ResponseSessionHandoverJson }
  | { readonly kind: 'held'; readonly custodian: string | null; readonly sessionId: string }
  | { readonly kind: 'drafts'; readonly sessionId: string; readonly counts: DraftCounts }
  | { readonly kind: 'open'; readonly sessionId: string; readonly custodyActive: boolean }
  | { readonly kind: 'other-open'; readonly session: ResponseListDailyCloseItemJson }
  | {
      readonly kind: 'submitted';
      readonly session: Pick<ResponseListDailyCloseItemJson, 'id' | 'submittedAt' | 'accountName'>;
      readonly custodyActive: boolean;
    }
  | {
      readonly kind: 'approved';
      readonly session: Pick<ResponseListDailyCloseItemJson, 'id' | 'accountName'>;
      readonly custodyActive: boolean;
    };
export type StartBlock = 'unresolved-history' | 'no-terminal';
const latest = (rows: readonly ResponseListDailyCloseItemJson[]) =>
  [...rows].sort(
    (a, b) =>
      (b.custody.sequence ?? 0) - (a.custody.sequence ?? 0) ||
      b.createdAt.localeCompare(a.createdAt),
  )[0] ?? null;
/** The workstation session "Ver fechamento de hoje" opens: the active custody, else the latest today. */
export function todaysSession(facts: WorkstationFacts | null, today: string) {
  if (!facts) return null;
  if (facts.active && sameBusinessDay(facts.active.date, today))
    return { id: facts.active.id, status: facts.active.status };
  const row = latest(facts.today);
  return row ? { id: row.id, status: row.status } : null;
}
/**
 * The first matching row wins, from successful reads only: a failed higher-priority read yields
 * `unknown` and never a guessed lower step (cockpit partial-failure state).
 */
export function nextAction(input: {
  readonly operatorId: string;
  readonly role: Role;
  readonly correction: Read<readonly CorrectionCandidate[]>;
  readonly workstation: Read<WorkstationFacts>;
  readonly drafts: Read<DraftCounts> | null;
  readonly others: Read<readonly ResponseListDailyCloseItemJson[]>;
}): NextAction | null {
  const { correction, workstation, drafts, others, operatorId } = input;
  if (correction.status === 'pending') return null;
  if (correction.status === 'error')
    return { kind: 'unknown', what: 'se há fechamentos para corrigir', retry: correction.retry };
  if (correction.data.length) return { kind: 'correction', session: correction.data[0]! };
  if (workstation.status === 'pending') return null;
  if (workstation.status === 'error')
    return { kind: 'unknown', what: 'o turno deste caixa', retry: workstation.retry };
  const facts = workstation.data;
  const active = facts.active;
  if (active && active.custody.custodianOperatorId !== operatorId) {
    if (facts.handoverForMe) return { kind: 'receive', handover: facts.handoverForMe };
    return {
      kind: 'held',
      custodian: active.custody.custodianOperatorName ?? null,
      sessionId: active.id,
    };
  }
  if (active && (active.status === 'Draft' || active.status === 'Rejected')) {
    if (!drafts || drafts.status === 'pending') return null;
    if (drafts.status === 'error')
      return { kind: 'unknown', what: 'as movimentações em rascunho', retry: drafts.retry };
    if (drafts.data.total > 0) return { kind: 'drafts', sessionId: active.id, counts: drafts.data };
    return { kind: 'open', sessionId: active.id, custodyActive: true };
  }
  const mineToday = facts.today.filter((row) => row.custody.custodianOperatorId === operatorId);
  const own = active ?? latest(mineToday);
  if (!own) {
    const blocked: StartBlock | null = facts.proposal.hasUnresolvedHistory
      ? 'unresolved-history'
      : !facts.proposal.terminalId
        ? 'no-terminal'
        : null;
    return { kind: 'not-open', canStart: facts.proposal.canStart, blocked };
  }
  if (own.status === 'Draft' || own.status === 'Rejected')
    return { kind: 'open', sessionId: own.id, custodyActive: false };
  if (others.status === 'pending') return null;
  if (others.status === 'error')
    return { kind: 'unknown', what: 'seus outros caixas de hoje', retry: others.retry };
  const otherOpen = others.data.find(
    (row) =>
      row.accountId !== facts.proposal.accountId &&
      row.custody.custodianOperatorId === operatorId &&
      (row.custody.custodyState === 'Active' || row.status === 'Draft'),
  );
  if (otherOpen) return { kind: 'other-open', session: otherOpen };
  const custodyActive = own.custody.custodyState === 'Active';
  if (own.status === 'Submitted') return { kind: 'submitted', session: own, custodyActive };
  return { kind: 'approved', session: own, custodyActive };
}

/* Formatting helpers --------------------------------------------------------------------------- */

/** Audit instants render in the device time zone (formatting §4); business dates never do. */
export function deviceZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}
export const pluralDrafts = (n: number) =>
  `${n} ${n === 1 ? 'movimentação em rascunho' : 'movimentações em rascunho'}`;
