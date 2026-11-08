// Mobile M6 request composition and presentation derivations. Nothing here computes a financial
// outcome, balance, variance, allocation or permission: the server owns every rule and every
// figure; this module only shapes contracted requests from typed input and reads returned data.
import {
  centsFromMoneyDigits,
  centsFromWire,
  formatBrlWire,
  formatSignedBrlWire,
  parseCalendarDate,
  parseWallTime,
  wireFromCents,
} from '@loto/core';
import type {
  AccountType,
  ChequeLifecycleState,
  GetTransactionParams,
  RequestChequeConvertToFiadoJson,
  RequestChequeReturnJson,
  RequestCreateTransactionInstallmentJson,
  RequestCreateTransactionJson,
  RequestReceiveFiadoPaymentJson,
  RequestSafeDepositJson,
  RequestSessionOpeningAcknowledgmentJson,
  RequestUpdateTransactionJson,
  ResponseAccountJson,
  ResponseCategoryJson,
  ResponseReceivableJson,
  ResponseSelfContextJson,
  ResponseTransactionImpactJson,
  ResponseTransactionJson,
  ResponseTransactionTypeJson,
  Role,
  TransactionStatus,
} from '@loto/api';
import { isDigits, type Digits } from '@/day/contracts';

export const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);
const optionalId = (value: unknown) =>
  value === null || (typeof value === 'string' && uuid.test(value));
const optionalDate = (value: unknown) =>
  value === null || (typeof value === 'string' && parseCalendarDate(value) !== null);
const optionalTime = (value: unknown) =>
  value === null || (typeof value === 'string' && parseWallTime(value) !== null);
const text = (value: unknown, max: number) => typeof value === 'string' && value.length <= max;
const exactKeys = (value: Record<string, unknown>, keys: readonly string[]) =>
  Object.keys(value).sort().join(',') === [...keys].sort().join(',');
const buffersOk = (value: unknown) =>
  isRecord(value) &&
  Object.entries(value).every(
    ([key, raw]) =>
      /^[a-z][a-z0-9:-]{0,60}$/i.test(key) && typeof raw === 'string' && raw.length <= 256,
  );
/** Calendar part of an API date-time (`YYYY-MM-DDT00:00:00`). */
export const day = (value: string | null | undefined) => (value ? value.slice(0, 10) : null);
/** Wire value of typed money digits (blank is never sent: callers check completeness first). */
export const wire = (digits: Digits) => wireFromCents(centsFromMoneyDigits(digits));
const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim());
/** `HH:mm` typed wall time → the contract's `HH:mm:ss`. */
export const wireTime = (value: string | null) => {
  if (value === null) return null;
  const time = parseWallTime(value);
  if (!time) return null;
  const two = (n: number) => String(n).padStart(2, '0');
  return `${two(time.hour)}:${two(time.minute)}:${two(time.second)}`;
};

/* Accounts ------------------------------------------------------------------------------------ */

/** One account the caller may name, with the cashbox/Tab pairing the server reported. */
export interface AccountOption {
  readonly id: string;
  readonly name: string;
  readonly type: AccountType;
  /** Terminal → its paired Tab; Tab → its paired cashbox. Null when unpaired or unknown. */
  readonly pairedId: string | null;
}
/**
 * Accounts the caller may act on: a Member's explicit grants (self-context), never the admin
 * list; Manager/Admin the branch accounts. Membership in this list grants nothing by itself.
 */
export function memberAccounts(self: ResponseSelfContextJson | null | undefined): AccountOption[] {
  const grants = self?.availableAccounts ?? [];
  const seen = new Map<string, AccountOption>();
  for (const grant of grants) {
    if (seen.has(grant.accountId)) continue;
    const pairedTerminal =
      grant.accountType === 'Tab'
        ? (grants.find(
            (g) => g.accountType === 'Terminal' && g.pairedTabAccountId === grant.accountId,
          )?.accountId ?? null)
        : null;
    seen.set(grant.accountId, {
      id: grant.accountId,
      name: grant.accountName,
      type: grant.accountType,
      pairedId:
        grant.accountType === 'Terminal' ? (grant.pairedTabAccountId ?? null) : pairedTerminal,
    });
  }
  return [...seen.values()];
}
export function branchAccounts(accounts: readonly ResponseAccountJson[]): AccountOption[] {
  return accounts.map((account) => ({
    id: account.id,
    name: account.name,
    type: account.type,
    pairedId:
      account.type === 'Terminal'
        ? (account.tabAccountId ?? null)
        : account.type === 'Tab'
          ? (account.terminalAccountId ?? null)
          : null,
  }));
}

/* Types --------------------------------------------------------------------------------------- */

export type TypeWithDirection = ResponseTransactionTypeJson & {
  readonly direction: 'In' | 'Out' | null;
};
export function withDirection(
  types: readonly ResponseTransactionTypeJson[],
  categories: readonly ResponseCategoryJson[],
): TypeWithDirection[] {
  const directions = new Map(categories.map((c) => [c.id, c.defaultDirection]));
  return types.map((type) => ({ ...type, direction: directions.get(type.categoryId) ?? null }));
}
/** A fiado repayment type, by structure only (never by name): Receber pagamento records it. */
export const isRepaymentType = (type: TypeWithDirection) =>
  type.fiadoPaymentKind != null || (type.requiresTabAccountAndClient && type.direction === 'In');
/**
 * Types offered for manual creation: active, not system-generated (`SystemRole`), not a fiado
 * repayment, and with a known category direction. Everything else is refused by the server.
 */
export const creatableTypes = (types: readonly TypeWithDirection[]) =>
  types.filter(
    (type) =>
      type.active && type.systemRole == null && !isRepaymentType(type) && type.direction !== null,
  );
/** Repayment methods for Receber pagamento: configured `FiadoPaymentKind`, never a default. */
export const repaymentMethods = (types: readonly ResponseTransactionTypeJson[]) =>
  types.filter((type) => type.active && type.systemRole == null && type.fiadoPaymentKind != null);
export const isChequeRuleType = (type: Pick<ResponseTransactionTypeJson, 'settlementRule'>) =>
  type.settlementRule === 'OperatorEnteredCheque';
/** A cheque plan needs a cheque-rule type and is never a Tab sale (`..._REQUIRES_SINGLE_CREATE`). */
export const planTypes = (types: readonly TypeWithDirection[]) =>
  creatableTypes(types).filter(
    (type) => isChequeRuleType(type) && !type.requiresTabAccountAndClient,
  );
/** Accounts a generic create may name for a type: a fiado sale on a Tab, any other on a cashbox/bank. */
export function accountsForType(
  type: Pick<ResponseTransactionTypeJson, 'requiresTabAccountAndClient'> | null,
  accounts: readonly AccountOption[],
): AccountOption[] {
  if (!type)
    return accounts.filter(
      (a) => a.type === 'Terminal' || a.type === 'BankAccount' || a.type === 'Tab',
    );
  return type.requiresTabAccountAndClient
    ? accounts.filter((a) => a.type === 'Tab')
    : accounts.filter((a) => a.type === 'Terminal' || a.type === 'BankAccount');
}
/**
 * The cashbox whose turno the write touches: the cashbox itself, a fiado sale's (or cash
 * repayment's) paired cashbox, or none (bank). `unknown` when a Tab's pairing is not visible.
 */
export function cashboxOf(
  account: AccountOption | null | undefined,
):
  | { readonly kind: 'none' }
  | { readonly kind: 'cashbox'; readonly id: string }
  | { readonly kind: 'unpaired' } {
  if (!account) return { kind: 'none' };
  if (account.type === 'Terminal') return { kind: 'cashbox', id: account.id };
  if (account.type === 'Tab')
    return account.pairedId ? { kind: 'cashbox', id: account.pairedId } : { kind: 'unpaired' };
  return { kind: 'none' };
}

/* Session binding (inline opening / historical) ------------------------------------------------ */

/**
 * How a cashbox write is bound to a turno, decided per dispatch from fresh reads: the active
 * custody (nothing to send), the atomic inline opening acknowledgment, or a historical write that
 * names one Draft session with a reason (Manager/Admin). Never restored from device storage.
 */
export type SessionBinding =
  | { readonly kind: 'none' }
  | { readonly kind: 'opening'; readonly acknowledgment: RequestSessionOpeningAcknowledgmentJson }
  | { readonly kind: 'historical'; readonly sessionId: string; readonly reason: string };
export function bindingFields(binding: SessionBinding) {
  switch (binding.kind) {
    case 'none':
      return {};
    case 'opening':
      return { openingAcknowledgment: binding.acknowledgment };
    case 'historical':
      return { sessionId: binding.sessionId, historicalReason: binding.reason.trim() };
  }
}

/* Create --------------------------------------------------------------------------------------- */

export interface CreateInput {
  readonly v: 1;
  readonly typeId: string | null;
  readonly accountId: string | null;
  readonly value: Digits;
  readonly date: string | null;
  readonly time: string | null;
  readonly clientId: string | null;
  readonly dueDate: string | null;
  readonly description: string;
  readonly operatorId: string | null;
  /** Open arithmetic buffers by field key (raw text only). */
  readonly buffers: Readonly<Record<string, string>>;
}
const createKeys = [
  'v',
  'typeId',
  'accountId',
  'value',
  'date',
  'time',
  'clientId',
  'dueDate',
  'description',
  'operatorId',
  'buffers',
];
export function isCreateInput(value: unknown): value is CreateInput {
  return (
    isRecord(value) &&
    exactKeys(value, createKeys) &&
    value.v === 1 &&
    optionalId(value.typeId) &&
    optionalId(value.accountId) &&
    isDigits(value.value) &&
    optionalDate(value.date) &&
    optionalTime(value.time) &&
    optionalId(value.clientId) &&
    optionalDate(value.dueDate) &&
    text(value.description, 500) &&
    optionalId(value.operatorId) &&
    buffersOk(value.buffers)
  );
}
export const emptyCreate = (today: string, time: string | null): CreateInput => ({
  v: 1,
  typeId: null,
  accountId: null,
  value: '',
  date: today,
  time,
  clientId: null,
  dueDate: null,
  description: '',
  operatorId: null,
  buffers: {},
});
/** Missing pieces, as words; an empty list means the candidate is complete enough to send. */
export function createMissing(
  input: CreateInput,
  type: Pick<ResponseTransactionTypeJson, 'requiresTabAccountAndClient'> | null,
  options: { readonly operatorRequired: boolean },
): string[] {
  const missing: string[] = [];
  if (!input.typeId || !type) missing.push('o tipo');
  if (!input.accountId) missing.push('a conta');
  if (input.value === '' || centsFromMoneyDigits(input.value) <= 0) missing.push('o valor');
  if (!input.date) missing.push('a data');
  if (type?.requiresTabAccountAndClient && !input.clientId) missing.push('o cliente');
  if (options.operatorRequired && !input.operatorId) missing.push('o operador responsável');
  if (Object.keys(input.buffers).length) missing.push('usar ou descartar a conta aberta');
  return missing;
}
export function createRequest(
  input: CreateInput,
  binding: SessionBinding,
  saveAsDraft: boolean,
): RequestCreateTransactionJson {
  return {
    date: input.date!,
    value: wire(input.value),
    description: blankToNull(input.description),
    transactionTime: wireTime(input.time),
    transactionTypeId: input.typeId!,
    accountId: input.accountId!,
    clientId: input.clientId,
    dueDate: input.dueDate,
    ...(input.operatorId ? { recordedByOperatorId: input.operatorId } : {}),
    saveAsDraft,
    ...bindingFields(binding),
  };
}
/**
 * Re-checking retained input against the current lookups: a choice no longer offered (type
 * retired, grant lost, client inactive) is cleared and named, never silently reinterpreted.
 */
export function reconcileChoices<
  T extends {
    readonly typeId: string | null;
    readonly accountId: string | null;
    readonly clientId: string | null;
  },
>(
  input: T,
  offered: {
    readonly types: ReadonlySet<string>;
    readonly accounts: ReadonlySet<string>;
    readonly clients: ReadonlySet<string> | null;
  },
): { readonly input: T; readonly cleared: readonly string[] } {
  const cleared: string[] = [];
  let next = input;
  if (input.typeId && !offered.types.has(input.typeId)) {
    next = { ...next, typeId: null };
    cleared.push('tipo');
  }
  if (input.accountId && !offered.accounts.has(input.accountId)) {
    next = { ...next, accountId: null };
    cleared.push('conta');
  }
  if (input.clientId && offered.clients && !offered.clients.has(input.clientId)) {
    next = { ...next, clientId: null };
    cleared.push('cliente');
  }
  return { input: next, cleared };
}

/* Edit ----------------------------------------------------------------------------------------- */

export interface EditInput {
  readonly v: 1;
  readonly description: string;
  readonly dueDate: string | null;
  readonly clientId: string | null;
  readonly time: string | null;
}
export function isEditInput(value: unknown): value is EditInput {
  return (
    isRecord(value) &&
    exactKeys(value, ['v', 'description', 'dueDate', 'clientId', 'time']) &&
    value.v === 1 &&
    text(value.description, 500) &&
    optionalDate(value.dueDate) &&
    optionalId(value.clientId) &&
    optionalTime(value.time)
  );
}
export const editInput = (row: ResponseTransactionJson): EditInput => ({
  v: 1,
  description: row.description ?? '',
  dueDate: day(row.dueDate),
  clientId: row.clientId ?? null,
  time: row.transactionTime ? row.transactionTime.slice(0, 5) : null,
});
export const sameEdit = (a: EditInput, b: EditInput) =>
  a.description.trim() === b.description.trim() &&
  a.dueDate === b.dueDate &&
  a.clientId === b.clientId &&
  a.time === b.time;
export const editRequest = (input: EditInput): RequestUpdateTransactionJson => ({
  description: blankToNull(input.description),
  dueDate: input.dueDate!,
  clientId: input.clientId,
  transactionTime: wireTime(input.time),
});
/**
 * `[derived]` presentation gate mirroring the §6.11 mutation guard: generated and cancelled rows
 * are read-only for everyone; a Member edits only their own rows on the same branch day. The
 * server re-checks everything (scope, lock date, frozen session) and its refusal is shown verbatim.
 */
export function editGate(
  row: Pick<ResponseTransactionJson, 'status' | 'generationKind' | 'recordedByOperatorId' | 'date'>,
  context: { readonly role: Role; readonly operatorId: string | null; readonly today: string },
): { readonly editable: boolean; readonly reason: string | null } {
  if (row.generationKind)
    return {
      editable: false,
      reason:
        'Movimentação gerada automaticamente: só muda pela operação de origem, que leva esta junto.',
    };
  if (row.status === 'Cancelled')
    return { editable: false, reason: 'Transação cancelada: somente leitura.' };
  if (context.role === 'Member') {
    if (!context.operatorId || row.recordedByOperatorId !== context.operatorId)
      return {
        editable: false,
        reason: 'Registrada por outro operador: somente leitura para você.',
      };
    if (day(row.date) !== context.today)
      return { editable: false, reason: 'Correções de dias anteriores são feitas pela gerência.' };
  }
  return { editable: true, reason: null };
}

/* Plan ----------------------------------------------------------------------------------------- */

export interface PlanRowInput {
  readonly dueDate: string | null;
  readonly value: Digits;
}
export interface PlanInput {
  readonly v: 1;
  readonly typeId: string | null;
  readonly accountId: string | null;
  readonly value: Digits;
  readonly date: string | null;
  readonly time: string | null;
  readonly clientId: string | null;
  readonly description: string;
  readonly operatorId: string | null;
  readonly mode: 'manual' | 'auto';
  readonly rows: readonly PlanRowInput[];
  readonly firstDue: string | null;
  readonly count: string;
  readonly buffers: Readonly<Record<string, string>>;
}
export function isPlanInput(value: unknown): value is PlanInput {
  return (
    isRecord(value) &&
    exactKeys(value, [
      'v',
      'typeId',
      'accountId',
      'value',
      'date',
      'time',
      'clientId',
      'description',
      'operatorId',
      'mode',
      'rows',
      'firstDue',
      'count',
      'buffers',
    ]) &&
    value.v === 1 &&
    optionalId(value.typeId) &&
    optionalId(value.accountId) &&
    isDigits(value.value) &&
    optionalDate(value.date) &&
    optionalTime(value.time) &&
    optionalId(value.clientId) &&
    text(value.description, 500) &&
    optionalId(value.operatorId) &&
    (value.mode === 'manual' || value.mode === 'auto') &&
    Array.isArray(value.rows) &&
    value.rows.length <= 24 &&
    value.rows.every(
      (row) =>
        isRecord(row) &&
        exactKeys(row, ['dueDate', 'value']) &&
        optionalDate(row.dueDate) &&
        isDigits(row.value),
    ) &&
    optionalDate(value.firstDue) &&
    typeof value.count === 'string' &&
    /^\d{0,2}$/.test(value.count) &&
    buffersOk(value.buffers)
  );
}
export const emptyPlan = (today: string, time: string | null): PlanInput => ({
  v: 1,
  typeId: null,
  accountId: null,
  value: '',
  date: today,
  time,
  clientId: null,
  description: '',
  operatorId: null,
  mode: 'manual',
  rows: [
    { dueDate: null, value: '' },
    { dueDate: null, value: '' },
  ],
  firstDue: null,
  count: '',
  buffers: {},
});
export function planMissing(
  input: PlanInput,
  options: { readonly operatorRequired: boolean },
): string[] {
  const missing: string[] = [];
  if (!input.typeId) missing.push('o tipo de cheque');
  if (!input.accountId) missing.push('a conta');
  if (input.value === '' || centsFromMoneyDigits(input.value) <= 0) missing.push('o valor total');
  if (!input.date) missing.push('a data');
  if (options.operatorRequired && !input.operatorId) missing.push('o operador responsável');
  if (input.mode === 'manual') {
    if (input.rows.length < 2) missing.push('ao menos duas parcelas');
    if (
      input.rows.some(
        (row) => !row.dueDate || row.value === '' || centsFromMoneyDigits(row.value) <= 0,
      )
    )
      missing.push('o vencimento e o valor de cada parcela');
  } else {
    if (!input.firstDue) missing.push('o primeiro vencimento');
    const count = Number(input.count);
    if (!input.count || !Number.isInteger(count) || count < 2 || count > 24)
      missing.push('a quantidade de parcelas (2 a 24)');
  }
  if (Object.keys(input.buffers).length) missing.push('usar ou descartar a conta aberta');
  return missing;
}
/** `Σ rows − Value` in integer cents while editing manual rows (a hint; the server enforces it). */
export function planSumDifference(input: PlanInput): number | null {
  if (input.mode !== 'manual' || input.value === '' || input.rows.some((row) => row.value === ''))
    return null;
  return (
    input.rows.reduce((sum, row) => sum + centsFromMoneyDigits(row.value), 0) -
    centsFromMoneyDigits(input.value)
  );
}
export function planRequest(
  input: PlanInput,
  binding: SessionBinding,
  saveAsDraft: boolean,
): RequestCreateTransactionInstallmentJson {
  const base = {
    date: input.date!,
    value: wire(input.value),
    description: blankToNull(input.description),
    transactionTime: wireTime(input.time),
    transactionTypeId: input.typeId!,
    accountId: input.accountId!,
    clientId: input.clientId,
    ...(input.operatorId ? { recordedByOperatorId: input.operatorId } : {}),
    saveAsDraft,
    ...bindingFields(binding),
  };
  return input.mode === 'manual'
    ? {
        ...base,
        installments: input.rows.map((row) => ({ dueDate: row.dueDate!, value: wire(row.value) })),
      }
    : {
        ...base,
        installments: [],
        autoGenerateInstallments: true,
        dueDate: input.firstDue,
        installmentCount: Number(input.count),
      };
}

/* Receber pagamento ---------------------------------------------------------------------------- */

export interface PaymentInput {
  readonly v: 1;
  readonly accountId: string | null;
  readonly methodId: string | null;
  readonly value: Digits;
  readonly date: string | null;
  readonly time: string | null;
  readonly description: string;
  readonly operatorId: string | null;
  readonly buffers: Readonly<Record<string, string>>;
}
export function isPaymentInput(value: unknown): value is PaymentInput {
  return (
    isRecord(value) &&
    exactKeys(value, [
      'v',
      'accountId',
      'methodId',
      'value',
      'date',
      'time',
      'description',
      'operatorId',
      'buffers',
    ]) &&
    value.v === 1 &&
    optionalId(value.accountId) &&
    optionalId(value.methodId) &&
    isDigits(value.value) &&
    optionalDate(value.date) &&
    optionalTime(value.time) &&
    text(value.description, 500) &&
    optionalId(value.operatorId) &&
    buffersOk(value.buffers)
  );
}
export function paymentMissing(
  input: PaymentInput,
  options: { readonly operatorRequired: boolean },
) {
  const missing: string[] = [];
  if (!input.accountId) missing.push('a conta fiado que recebe');
  if (input.value === '' || centsFromMoneyDigits(input.value) <= 0) missing.push('o valor');
  if (!input.date) missing.push('a data');
  if (options.operatorRequired && !input.operatorId) missing.push('o operador responsável');
  if (Object.keys(input.buffers).length) missing.push('usar ou descartar a conta aberta');
  return missing;
}
export function paymentRequest(
  input: PaymentInput,
  clientId: string,
  binding: SessionBinding,
): RequestReceiveFiadoPaymentJson {
  return {
    date: input.date!,
    value: wire(input.value),
    transactionTime: wireTime(input.time),
    description: blankToNull(input.description),
    transactionTypeId: input.methodId!,
    accountId: input.accountId!,
    clientId,
    ...(input.operatorId ? { recordedByOperatorId: input.operatorId } : {}),
    ...bindingFields(binding),
  };
}

/* Depósito Dinheiro ---------------------------------------------------------------------------- */

export interface SafeInput {
  readonly v: 1;
  readonly accountId: string | null;
  readonly safeAccountId: string | null;
  readonly value: Digits;
  readonly date: string | null;
  readonly time: string | null;
  readonly description: string;
  readonly operatorId: string | null;
  readonly buffers: Readonly<Record<string, string>>;
}
export function isSafeInput(value: unknown): value is SafeInput {
  return (
    isRecord(value) &&
    exactKeys(value, [
      'v',
      'accountId',
      'safeAccountId',
      'value',
      'date',
      'time',
      'description',
      'operatorId',
      'buffers',
    ]) &&
    value.v === 1 &&
    optionalId(value.accountId) &&
    optionalId(value.safeAccountId) &&
    isDigits(value.value) &&
    optionalDate(value.date) &&
    optionalTime(value.time) &&
    text(value.description, 500) &&
    optionalId(value.operatorId) &&
    buffersOk(value.buffers)
  );
}
export function safeMissing(input: SafeInput, options: { readonly operatorRequired: boolean }) {
  const missing: string[] = [];
  if (!input.accountId) missing.push('o caixa de origem');
  if (!input.safeAccountId) missing.push('o cofre de destino');
  if (input.value === '' || centsFromMoneyDigits(input.value) <= 0) missing.push('o valor');
  if (!input.date) missing.push('a data');
  if (options.operatorRequired && !input.operatorId) missing.push('o operador responsável');
  if (Object.keys(input.buffers).length) missing.push('usar ou descartar a conta aberta');
  return missing;
}
export const safeRequest = (input: SafeInput, binding: SessionBinding): RequestSafeDepositJson => ({
  date: input.date!,
  value: wire(input.value),
  transactionTime: wireTime(input.time),
  description: blankToNull(input.description),
  accountId: input.accountId!,
  safeAccountId: input.safeAccountId!,
  ...(input.operatorId ? { recordedByOperatorId: input.operatorId } : {}),
  ...bindingFields(binding),
});

/* Transactions list ---------------------------------------------------------------------------- */

export interface ListFilter {
  readonly from: string | null;
  readonly to: string | null;
  readonly status: TransactionStatus | null;
  readonly accountId: string | null;
  readonly operatorId: string | null;
  readonly clientId: string | null;
  readonly typeId: string | null;
  readonly mine: boolean;
  readonly page: number;
  readonly pageSize: number;
}
export const PAGE_SIZES = [20, 50, 100] as const;
const statuses: readonly string[] = ['Draft', 'Active', 'Cancelled'];
/**
 * The applied filter from the route's allowlisted parameters. Defaults: today, all statuses, page
 * 1; `Mine` defaults on only for a linked Manager/Admin entering from Meu turno navigation with no
 * explicit filter. Members never send `OperatorId` (the server discards it) and an unlinked caller
 * never sends `Mine` (a server no-op that would read as "mine" while listing the whole branch).
 */
export function parseListFilter(
  params: Readonly<Record<string, string | string[] | undefined>>,
  context: {
    readonly today: string;
    readonly role: Role;
    readonly shift: boolean;
    readonly linked: boolean;
  },
): ListFilter {
  const one = (key: string) => {
    const value = params[key];
    return typeof value === 'string' ? value : null;
  };
  const date = (key: string) => {
    const value = one(key);
    return value && parseCalendarDate(value) ? value : null;
  };
  const id = (key: string) => {
    const value = one(key);
    return value && uuid.test(value) ? value : null;
  };
  const explicit = [
    'from',
    'to',
    'status',
    'accountId',
    'operatorId',
    'clientId',
    'typeId',
    'mine',
  ].some((key) => one(key) !== null);
  const status = one('status');
  const page = Number(one('page') ?? '1');
  const size = Number(one('pageSize') ?? '50');
  const operatorId = context.role === 'Member' ? null : id('operatorId');
  const mineParam = one('mine');
  const mine =
    context.linked &&
    !operatorId &&
    (mineParam !== null
      ? mineParam === 'true'
      : !explicit && context.shift && context.role !== 'Member');
  return {
    from: explicit ? date('from') : context.today,
    to: explicit ? date('to') : context.today,
    status: status && statuses.includes(status) ? (status as TransactionStatus) : null,
    accountId: id('accountId'),
    operatorId,
    clientId: id('clientId'),
    typeId: id('typeId'),
    mine,
    page: Number.isSafeInteger(page) && page >= 1 ? page : 1,
    pageSize: (PAGE_SIZES as readonly number[]).includes(size) ? size : 50,
  };
}
/**
 * Route parameters for a filter: every key, a cleared filter as `''`. The router merges params,
 * so omitting a removed filter would silently keep the previous value in later queries.
 */
export function filterParams(filter: ListFilter): Record<string, string> {
  const params: Record<string, string> = {
    from: filter.from ?? '',
    to: filter.to ?? '',
    page: String(filter.page),
    pageSize: String(filter.pageSize),
    mine: filter.mine ? 'true' : 'false',
    status: filter.status ?? '',
    accountId: filter.accountId ?? '',
    operatorId: filter.operatorId ?? '',
    clientId: filter.clientId ?? '',
    typeId: filter.typeId ?? '',
  };
  return params;
}
export const filterPath = (filter: ListFilter) =>
  '/transacoes?' +
  new URLSearchParams(Object.entries(filterParams(filter)).filter(([, v]) => v !== '')).toString();
export function listQuery(filter: ListFilter): GetTransactionParams {
  return {
    ...(filter.from ? { DateFrom: filter.from } : {}),
    ...(filter.to ? { DateTo: filter.to } : {}),
    ...(filter.status ? { Status: filter.status } : {}),
    ...(filter.accountId ? { AccountId: filter.accountId } : {}),
    ...(filter.operatorId ? { OperatorId: filter.operatorId } : {}),
    ...(filter.clientId ? { ClientId: filter.clientId } : {}),
    ...(filter.typeId ? { TransactionTypeId: filter.typeId } : {}),
    ...(filter.mine ? { Mine: true } : {}),
    Page: filter.page,
    PageSize: filter.pageSize,
  };
}
/** A Draft-blocker context from Fechar dia / Hoje: one cashbox, one day, Draft rows. */
export const draftBlocker = (filter: ListFilter) =>
  filter.status === 'Draft' && !!filter.accountId && !!filter.from && filter.from === filter.to;
/** Signed display of a row value by its direction (Entrada `+`, Saída `−`); never summed. */
export const signedValue = (value: number, direction: 'In' | 'Out') =>
  formatSignedBrlWire(direction === 'In' ? value : -value);
export const amount = (value: number) => formatBrlWire(value);

/* Impact preview ------------------------------------------------------------------------------- */

export interface ImpactLine {
  readonly key: string;
  readonly text: string;
}
/**
 * Lines for the server's hypothetical impact, omitting inapplicable sections. A Draft close's
 * variance stays blind (the count has not been conferred), and a non-cashbox account has no
 * counted cash — said in words, never rendered as a zero difference.
 */
export function impactLines(
  impact: ResponseTransactionImpactJson,
  bucketLabels: Readonly<Record<string, string>>,
  closeLabels: Readonly<Record<string, string>>,
): ImpactLine[] {
  const lines: ImpactLine[] = [];
  const receivable = impact.receivableImpact;
  if (receivable.rowAppearsInOpenReceivables)
    lines.push({
      key: 'receivable-in',
      text: `Passa a constar em valores a receber${receivable.bucketAfter ? ` (${bucketLabels[receivable.bucketAfter] ?? receivable.bucketAfter})` : ''}.`,
    });
  if (receivable.rowDisappearsFromOpenReceivables)
    lines.push({ key: 'receivable-out', text: 'Deixa de constar em valores a receber.' });
  for (const delta of impact.fiadoBalanceImpact.deltas)
    lines.push({
      key: `fiado-${delta.clientId}`,
      text: `Fiado de ${delta.clientName}: ${formatSignedBrlWire(delta.outstandingDelta)} no saldo em aberto.`,
    });
  const cash = impact.cashVarianceImpact;
  if (!cash.accountId)
    lines.push({ key: 'cash-none', text: 'Sem efeito em caixa contado (conta sem fechamento).' });
  else if (!cash.dailyCloseStatus || cash.dailyCloseStatus === 'Draft')
    lines.push({
      key: 'cash-blind',
      text: 'Entra no caixa do turno. A diferença só aparece depois da conferência da contagem.',
    });
  else
    lines.push({
      key: 'cash-frozen',
      text: `Fechamento ${(closeLabels[cash.dailyCloseStatus] ?? cash.dailyCloseStatus).toLowerCase()}: diferença atual ${cash.currentVariance == null ? 'indisponível' : formatSignedBrlWire(cash.currentVariance)}, projetada ${cash.projectedVariance == null ? 'indisponível' : formatSignedBrlWire(cash.projectedVariance)}.`,
    });
  return lines;
}

/* Cheque lifecycle ------------------------------------------------------------------------------ */

export type ChequeAction = 'custody' | 'deposit' | 'return' | 'represent' | 'convert';
/**
 * Commands that apply to a cheque in its current projected state (the server's own state rules,
 * used only to choose which commands to offer; the server re-checks state, version and chronology).
 */
export function chequeActions(state: ChequeLifecycleState): ChequeAction[] {
  switch (state) {
    case 'Received':
    case 'AwaitingRedeposit':
      return ['custody', 'deposit', 'convert'];
    case 'InSafe':
      return ['custody', 'deposit', 'convert'];
    case 'Deposited':
      return ['return'];
    case 'Returned':
      return ['custody', 'represent', 'convert'];
    default:
      return [];
  }
}
/** Whole-cheque commands (deposit, conversion) need the full face value outstanding. */
export const isWhole = (receivable: Pick<ResponseReceivableJson, 'outstandingValue' | 'value'>) =>
  centsFromWire(receivable.outstandingValue) === centsFromWire(receivable.value);
/** Once a return identified a client, that immutable event wins over the row's editable client. */
export function identifiedClient(receivable: ResponseReceivableJson): string | null {
  const returned = [...receivable.events]
    .reverse()
    .find((event) => event.kind === 'ChequeReturned');
  return returned?.clientId ?? receivable.clientId ?? null;
}
export interface ChequeInput {
  readonly occurredOn: string | null;
  readonly accountId: string | null;
  readonly clientId: string | null;
  readonly nextDate: string | null;
  readonly reason: string;
  readonly description: string;
}
export const returnRequest = (
  receivable: ResponseReceivableJson,
  input: ChequeInput,
): RequestChequeReturnJson => ({
  depositEventId: receivable.currentDepositEventId!,
  expectedVersion: receivable.version,
  occurredOn: input.occurredOn!,
  clientId: input.clientId!,
  nextExpectedDepositDate: input.nextDate!,
  returnReason: blankToNull(input.reason),
});
export const convertRequest = (
  receivable: ResponseReceivableJson,
  input: ChequeInput,
): RequestChequeConvertToFiadoJson => ({
  expectedVersion: receivable.version,
  clientId: input.clientId!,
  tabAccountId: input.accountId!,
  occurredOn: input.occurredOn!,
  description: blankToNull(input.description),
});
