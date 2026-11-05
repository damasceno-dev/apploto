// Request composition and presentation helpers for the M10 admin surfaces. Every rule here mirrors
// the shipped DTO/use-case behavior; permissions, uniqueness and ranges stay server-owned.
import {
  FiadoPaymentKind,
  MonthlyFinancialRole,
  SettlementInstrument,
  SettlementRule,
  type Direction,
  type RequestCreateBankAccountJson,
  type RequestCreateClientJson,
  type RequestCreateHolidaysJson,
  type RequestCreateTerminalAccountJson,
  type RequestCreateTransactionTypeJson,
  type RequestIssueInvitationJson,
  type RequestReissueInvitationJson,
  type RequestUpdateAccountJson,
  type RequestUpdateTransactionTypeJson,
  type ResponseAccountJson,
  type ResponseBranchUserJson,
  type ResponseClientJson,
  type ResponseInvitationJson,
  type ResponseOperatorAccountJson,
  type ResponseOperatorJson,
  type ResponseProductJson,
  type ResponseTransactionTypeJson,
  type Role,
  type TransactionTypeSystemRole,
} from '@loto/api';
import { formatDuration, identifierDigits, parseCalendarDate, type CalendarDate } from '@loto/core';

const text = (value: unknown): value is string => typeof value === 'string';
const exactKeys = (value: unknown, keys: readonly string[]): value is Record<string, unknown> =>
  !!value &&
  typeof value === 'object' &&
  !Array.isArray(value) &&
  Object.keys(value).length === keys.length &&
  keys.every((key) => key in value);

/* Clients ------------------------------------------------------------------------------------ */

export interface ClientInput {
  readonly name: string;
  readonly phone: string;
  readonly cpf: string;
  readonly cep: string;
  readonly address: string;
  readonly phoneSecondary: string;
  readonly email: string;
  readonly notes: string;
}
const clientKeys = [
  'name',
  'phone',
  'cpf',
  'cep',
  'address',
  'phoneSecondary',
  'email',
  'notes',
] as const;
export const emptyClient: ClientInput = Object.fromEntries(
  clientKeys.map((key) => [key, '']),
) as unknown as ClientInput;
export function clientInput(client: ResponseClientJson): ClientInput {
  return {
    name: client.name,
    phone: client.phone,
    cpf: client.cpf ?? '',
    cep: client.cep ?? '',
    address: client.address ?? '',
    phoneSecondary: client.phoneSecondary ?? '',
    email: client.email ?? '',
    notes: client.notes ?? '',
  };
}
export function isClientInput(value: unknown): value is ClientInput {
  return exactKeys(value, clientKeys) && clientKeys.every((key) => text(value[key]));
}
/**
 * Create and update carry the whole record: the server stores every optional field from the
 * request, turning blank text into null. CPF goes as digits (formatting §6); text the mask could
 * not hold stays as typed so the server's verbatim validation explains it.
 */
export function clientPayload(input: ClientInput): RequestCreateClientJson {
  const optional = (value: string) => (value.trim() === '' ? null : value);
  return {
    name: input.name,
    phone: input.phone,
    cpf: input.cpf.trim() === '' ? null : identifierDigits(input.cpf),
    cep: optional(input.cep),
    address: optional(input.address),
    phoneSecondary: optional(input.phoneSecondary),
    email: optional(input.email),
    notes: optional(input.notes),
  };
}
const searchable = (value: string) =>
  value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('pt-BR')
    .trim();
/** Local name search over the complete contracted list (the endpoint has no search filter). */
export function searchClients(
  clients: readonly ResponseClientJson[],
  query: string,
): readonly ResponseClientJson[] {
  const needle = searchable(query);
  return needle === '' ? clients : clients.filter((c) => searchable(c.name).includes(needle));
}
export function clientCountLabel(count: number): string {
  return count === 1 ? '1 cliente encontrado' : `${count} clientes encontrados`;
}

/* Operators ---------------------------------------------------------------------------------- */

export interface OperatorInput {
  readonly name: string;
  readonly userId: string | null;
}
export function isOperatorInput(value: unknown): value is OperatorInput {
  return (
    exactKeys(value, ['name', 'userId']) &&
    text(value.name) &&
    (value.userId === null ||
      (text(value.userId) &&
        /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(value.userId)))
  );
}
/** Only the contracted fields. `userId: null` clears the login link and keeps the employee. */
export function operatorPayload(input: OperatorInput) {
  return { name: input.name, userId: input.userId };
}
export type LoginState =
  | { readonly kind: 'none' }
  | { readonly kind: 'member'; readonly member: ResponseBranchUserJson }
  | { readonly kind: 'unresolved' };
export function operatorLogin(
  operator: ResponseOperatorJson,
  members: readonly ResponseBranchUserJson[],
): LoginState {
  if (!operator.userId) return { kind: 'none' };
  const member = members.find((m) => m.active && m.userId === operator.userId);
  return member ? { kind: 'member', member } : { kind: 'unresolved' };
}
/** Active memberships not linked to another active operator, plus the edited row's own link. */
export function linkableMembers(
  members: readonly ResponseBranchUserJson[],
  operators: readonly ResponseOperatorJson[],
  editing: string | null,
): readonly ResponseBranchUserJson[] {
  const taken = new Set(
    operators.filter((o) => o.id !== editing && o.userId).map((o) => o.userId as string),
  );
  return members.filter((m) => m.active && !taken.has(m.userId));
}

/* Products ----------------------------------------------------------------------------------- */

export const SYSTEM_PRODUCT_NAME = 'Diferença Caixa';
export const SYSTEM_PRODUCT_REASON =
  'Produto do sistema: o nome é reservado e ele não pode ser desativado. A ordem pode mudar.';
export const isSystemProduct = (product: { readonly name: string }) =>
  product.name.trim() === SYSTEM_PRODUCT_NAME;
export interface ProductInput {
  readonly name: string;
  readonly displayOrder: string;
}
export function isProductInput(value: unknown): value is ProductInput {
  return exactKeys(value, ['name', 'displayOrder']) && text(value.name) && text(value.displayOrder);
}
/** The generated integer pattern only; the non-negative rule and conflicts stay server-side. */
export function productPayload(input: ProductInput) {
  const order = input.displayOrder.trim();
  if (!/^-?(?:0|[1-9]\d*)$/.test(order) || !Number.isSafeInteger(Number(order))) return null;
  return { name: input.name, displayOrder: Number(order) };
}
/** Server order: DisplayOrder, then id. */
export function countingOrder(
  items: readonly ResponseProductJson[],
): readonly ResponseProductJson[] {
  return [...items].sort(
    (a, b) => a.displayOrder - b.displayOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0),
  );
}
export interface ReorderStep {
  readonly id: string;
  readonly name: string;
  readonly from: number;
  readonly to: number;
}
/**
 * Individual `PUT /product/{id}` updates that produce `target` (ids in counting order). Distinct
 * neighbours swap their values; ties are renumbered in steps of 10 from the first value. Returns
 * `null` when `target` is not exactly the current set of products (one was added or retired since
 * the list was shown): that is a changed list to reconcile, never an already-satisfied order.
 */
export function reorderPlan(
  current: readonly ResponseProductJson[],
  target: readonly string[],
): readonly ReorderStep[] | null {
  const byId = new Map(current.map((p) => [p.id, p]));
  const ordered = target.map((id) => byId.get(id)).filter((p) => !!p);
  if (
    target.length !== current.length ||
    new Set(target).size !== target.length ||
    ordered.length !== current.length
  )
    return null;
  const values = countingOrder(current).map((p) => p.displayOrder);
  const distinct = new Set(values).size === values.length;
  const base = Math.max(0, values[0] ?? 0);
  return ordered.flatMap((product, index) => {
    const to = distinct ? values[index]! : base + index * 10;
    return product.displayOrder === to
      ? []
      : [{ id: product.id, name: product.name, from: product.displayOrder, to }];
  });
}
export function moveTarget(
  items: readonly ResponseProductJson[],
  id: string,
  direction: -1 | 1,
): readonly string[] | null {
  const ids = countingOrder(items).map((p) => p.id);
  const index = ids.indexOf(id);
  const next = index + direction;
  if (index < 0 || next < 0 || next >= ids.length) return null;
  [ids[index], ids[next]] = [ids[next]!, ids[index]!];
  return ids;
}

/* Holidays ----------------------------------------------------------------------------------- */

export interface HolidayRow {
  /** `YYYY-MM-DD` from the date field, or empty while unset. */
  readonly date: string;
  readonly description: string;
}
export interface HolidayBatch {
  readonly rows: readonly HolidayRow[];
}
export function isHolidayBatch(value: unknown): value is HolidayBatch {
  return (
    exactKeys(value, ['rows']) &&
    Array.isArray(value.rows) &&
    value.rows.length <= 50 &&
    value.rows.every(
      (row: unknown) =>
        exactKeys(row, ['date', 'description']) && text(row.date) && text(row.description),
    )
  );
}
export function holidayPayload(
  batch: HolidayBatch,
): { ok: true; payload: RequestCreateHolidaysJson } | { ok: false; rows: readonly number[] } {
  const dates = batch.rows.map((row) => parseCalendarDate(row.date));
  const invalid = dates.flatMap((date, index) => (date ? [] : [index]));
  if (invalid.length || batch.rows.length === 0) return { ok: false, rows: invalid };
  return {
    ok: true,
    payload: {
      holidays: batch.rows.map((row, index) => ({
        date: dates[index]!,
        description: row.description.trim() === '' ? null : row.description,
      })),
    },
  };
}
export const holidayYearRange = { min: 1900, max: 2200 } as const;
export function holidayYear(value: string | undefined, today: CalendarDate): number {
  const year = Number(value);
  return value &&
    /^\d{4}$/.test(value) &&
    year >= holidayYearRange.min &&
    year <= holidayYearRange.max
    ? year
    : Number(today.slice(0, 4));
}
export const datePart = (value: string) => parseCalendarDate(value.slice(0, 10));

/* Settings ----------------------------------------------------------------------------------- */

export interface DurationText {
  readonly hours: string;
  readonly minutes: string;
}
export interface SettingsInput {
  readonly dailyTargetHours: DurationText;
  readonly lunchDeductionOver6H: DurationText;
  readonly lunchDeductionOver4H: DurationText;
}
export const settingFields = [
  'dailyTargetHours',
  'lunchDeductionOver6H',
  'lunchDeductionOver4H',
] as const;
export type SettingField = (typeof settingFields)[number];
export function durationText(hours: number): DurationText {
  // Display units come from the shared formatter so web/mobile never disagree by a minute.
  const match = /^(\d+)h (\d{2})min$/.exec(formatDuration(hours));
  return match ? { hours: match[1]!, minutes: match[2]! } : { hours: '', minutes: '' };
}
export function isSettingsInput(value: unknown): value is SettingsInput {
  return (
    exactKeys(value, settingFields) &&
    settingFields.every(
      (key) =>
        exactKeys(value[key], ['hours', 'minutes']) &&
        text((value[key] as Record<string, unknown>).hours) &&
        text((value[key] as Record<string, unknown>).minutes),
    )
  );
}
export type DurationValue =
  | { readonly kind: 'unchanged' }
  | { readonly kind: 'changed'; readonly wire: string; readonly display: string }
  | { readonly kind: 'invalid' }
  | { readonly kind: 'imprecise' };
/**
 * Converts only display units. The server stores hours with two decimals, so a changed value is
 * sent only when `h + min/60` is exact in hundredths (whole minutes divisible by 3); anything
 * else is refused locally instead of being rounded.
 */
export function durationValue(loaded: number, input: DurationText): DurationValue {
  const hours = input.hours.trim();
  const minutes = input.minutes.trim();
  if (!/^\d{1,3}$/.test(hours) || !/^\d{1,2}$/.test(minutes) || Number(minutes) > 59)
    return { kind: 'invalid' };
  const total = Number(hours) * 60 + Number(minutes);
  const display = `${Number(hours)}h ${String(Number(minutes)).padStart(2, '0')}min`;
  if (display === formatDuration(loaded)) return { kind: 'unchanged' };
  if (total % 3 !== 0) return { kind: 'imprecise' };
  const hundredths = (total * 5) / 3;
  const wire = `${Math.floor(hundredths / 100)}.${String(hundredths % 100).padStart(2, '0')}`;
  return { kind: 'changed', wire, display };
}

/* Accounts ----------------------------------------------------------------------------------- */

/** The three create forms; a Tab is created only for an existing caixa (`POST /account/tab`). */
export type AccountKind = 'Terminal' | 'BankAccount' | 'Safe';
/** Caixa pairing on create: new Tab (server default, sent explicitly), an existing Tab, or none. */
export type CashboxPairing = 'new' | 'existing' | 'none';
export interface AccountInput {
  readonly kind: AccountKind;
  readonly name: string;
  readonly institution: string;
  readonly number: string;
  readonly pairing: CashboxPairing;
  readonly tabAccountId: string;
  /** '' lets the server create the terminal CAIXA reference named after the caixa. */
  readonly terminalId: string;
}
const accountKeys = [
  'kind',
  'name',
  'institution',
  'number',
  'pairing',
  'tabAccountId',
  'terminalId',
] as const;
export const emptyAccount = (kind: AccountKind): AccountInput => ({
  kind,
  name: '',
  institution: '',
  number: '',
  pairing: 'new',
  tabAccountId: '',
  terminalId: '',
});
export function isAccountInput(value: unknown): value is AccountInput {
  return (
    exactKeys(value, accountKeys) &&
    ['Terminal', 'BankAccount', 'Safe'].includes(value.kind as string) &&
    ['new', 'existing', 'none'].includes(value.pairing as string) &&
    accountKeys.slice(1).every((key) => text(value[key]))
  );
}
const blankToNull = (value: string) => (value.trim() === '' ? null : value);
export type AccountCreateRequest =
  | { readonly route: 'bank' | 'safe'; readonly data: RequestCreateBankAccountJson }
  | { readonly route: 'terminal'; readonly data: RequestCreateTerminalAccountJson };
/**
 * Exactly one pairing choice reaches the server: a new Tab sends `createTabAccount: true`, an
 * existing one only its `existingTabAccountId`, and **Sem conta fiado** an explicit `false` (an
 * omitted flag would create a Tab). Existing and new together are never composed.
 */
export function accountCreateRequest(
  input: AccountInput,
): { ok: true; request: AccountCreateRequest } | { ok: false; reason: string } {
  const base = {
    name: input.name,
    institution: blankToNull(input.institution),
    number: blankToNull(input.number),
  };
  if (input.kind === 'BankAccount') return { ok: true, request: { route: 'bank', data: base } };
  if (input.kind === 'Safe') return { ok: true, request: { route: 'safe', data: base } };
  if (input.pairing === 'existing' && input.tabAccountId === '')
    return { ok: false, reason: 'Escolha a conta fiado existente.' };
  const pairing: Pick<
    RequestCreateTerminalAccountJson,
    'createTabAccount' | 'existingTabAccountId'
  > =
    input.pairing === 'new'
      ? { createTabAccount: true }
      : input.pairing === 'existing'
        ? { existingTabAccountId: input.tabAccountId }
        : { createTabAccount: false };
  return {
    ok: true,
    request: {
      route: 'terminal',
      data: {
        ...base,
        ...pairing,
        ...(input.terminalId === '' ? {} : { terminalId: input.terminalId }),
      },
    },
  };
}
export interface AccountEdit {
  readonly name: string;
  readonly institution: string;
  readonly number: string;
}
export const isAccountEdit = (value: unknown): value is AccountEdit =>
  exactKeys(value, ['name', 'institution', 'number']) &&
  text(value.name) &&
  text(value.institution) &&
  text(value.number);
export const accountEdit = (account: ResponseAccountJson): AccountEdit => ({
  name: account.name,
  institution: account.institution ?? '',
  number: account.number ?? '',
});
/** `PUT /account/{id}` stores all three descriptive fields as sent; there is no type field. */
export const accountUpdatePayload = (input: AccountEdit): RequestUpdateAccountJson => ({
  name: input.name,
  institution: blankToNull(input.institution),
  number: blankToNull(input.number),
});
export type Pairing =
  | { readonly kind: 'paired'; readonly with: ResponseAccountJson | null; readonly id: string }
  | { readonly kind: 'unpaired' }
  | { readonly kind: 'none' };
/** `TabAccountId` on a caixa / `TerminalAccountId` on a Tab, resolved against the same read. */
export function accountPairing(
  account: ResponseAccountJson,
  accounts: readonly ResponseAccountJson[],
): Pairing {
  if (account.type !== 'Terminal' && account.type !== 'Tab') return { kind: 'none' };
  // Nullable on the wire: an omitted field on a caixa or Tab still means unpaired.
  const other = account.type === 'Terminal' ? account.tabAccountId : account.terminalAccountId;
  if (!other) return { kind: 'unpaired' };
  return { kind: 'paired', id: other, with: accounts.find((a) => a.id === other) ?? null };
}
/** Tabs no caixa holds in the current read; the server still refuses a race verbatim. */
export const unpairedTabs = (accounts: readonly ResponseAccountJson[]) =>
  accounts.filter((a) => a.type === 'Tab' && !a.terminalAccountId);
export const accountDetail = (account: { institution?: string | null; number?: string | null }) =>
  [account.institution, account.number]
    .filter((v): v is string => !!v && v.trim() !== '')
    .join(' · ');

/* Account assignment ------------------------------------------------------------------------- */

/** Active accounts this operator does not hold yet; each grant is its own command. */
export function assignableAccounts(
  accounts: readonly ResponseAccountJson[],
  assigned: readonly ResponseOperatorAccountJson[],
) {
  const held = new Set(assigned.map((a) => a.accountId));
  return accounts.filter((a) => !held.has(a.id));
}

/* Categories and transaction types ----------------------------------------------------------- */

export interface CategoryInput {
  readonly name: string;
  /** '' until chosen: the direction is immutable after creation, so it has no default. */
  readonly defaultDirection: Direction | '';
}
export const isCategoryInput = (value: unknown): value is CategoryInput =>
  exactKeys(value, ['name', 'defaultDirection']) &&
  text(value.name) &&
  ['', 'In', 'Out'].includes(value.defaultDirection as string);
/** 'none' is the explicit "not a repayment method"; '' is unchosen (no default). */
export type FiadoKindChoice = FiadoPaymentKind | 'none' | '';
export interface TypeInput {
  readonly categoryId: string;
  readonly name: string;
  readonly settlementRule: SettlementRule | '';
  readonly requiresTabAccountAndClient: boolean;
  readonly fiadoPaymentKind: FiadoKindChoice;
  readonly settlementInstrument: SettlementInstrument;
  readonly monthlyFinancialRole: MonthlyFinancialRole;
}
const typeKeys = [
  'categoryId',
  'name',
  'settlementRule',
  'requiresTabAccountAndClient',
  'fiadoPaymentKind',
  'settlementInstrument',
  'monthlyFinancialRole',
] as const;
export function isTypeInput(value: unknown): value is TypeInput {
  return (
    exactKeys(value, typeKeys) &&
    text(value.categoryId) &&
    text(value.name) &&
    ['', ...Object.values(SettlementRule)].includes(value.settlementRule as string) &&
    typeof value.requiresTabAccountAndClient === 'boolean' &&
    ['', 'none', ...Object.values(FiadoPaymentKind)].includes(value.fiadoPaymentKind as string) &&
    Object.values(SettlementInstrument).includes(value.settlementInstrument as never) &&
    Object.values(MonthlyFinancialRole).includes(value.monthlyFinancialRole as never)
  );
}
export const emptyType = (categoryId = ''): TypeInput => ({
  categoryId,
  name: '',
  settlementRule: '',
  requiresTabAccountAndClient: false,
  fiadoPaymentKind: '',
  settlementInstrument: 'None',
  monthlyFinancialRole: 'None',
});
export const typeInput = (type: ResponseTransactionTypeJson): TypeInput => ({
  categoryId: type.categoryId,
  name: type.name,
  settlementRule: type.settlementRule,
  requiresTabAccountAndClient: type.requiresTabAccountAndClient,
  fiadoPaymentKind: type.fiadoPaymentKind ?? 'none',
  settlementInstrument: type.settlementInstrument,
  monthlyFinancialRole: type.monthlyFinancialRole,
});
/** The server's own predicate (TransactionTypeFiadoRules): an entrada that requires Tab and client. */
export const fiadoKindAllowed = (direction: Direction | undefined, requiresTab: boolean) =>
  direction === 'In' && requiresTab;
/** TransactionTypeMonthlyFinancialRules: fees only on entrada, Sobra only on saída. */
export function monthlyRoleAllowed(role: MonthlyFinancialRole, direction: Direction | undefined) {
  if (role === 'BolaoServiceFee' || role === 'SpecialDrawServiceFee') return direction === 'In';
  if (role === 'CommercialLeftover') return direction === 'Out';
  return true;
}
export type TypeProblem =
  'name' | 'category' | 'settlement' | 'fiado-unchosen' | 'fiado-condition' | 'monthly-direction';
export function typeProblems(input: TypeInput, direction: Direction | undefined): TypeProblem[] {
  const problems: TypeProblem[] = [];
  if (input.name.trim() === '') problems.push('name');
  if (input.categoryId === '' || !direction) problems.push('category');
  if (input.settlementRule === '') problems.push('settlement');
  const allowed = fiadoKindAllowed(direction, input.requiresTabAccountAndClient);
  if (allowed && input.fiadoPaymentKind === '') problems.push('fiado-unchosen');
  if (
    !allowed &&
    (input.fiadoPaymentKind === 'CountedCash' || input.fiadoPaymentKind === 'NonCash')
  )
    problems.push('fiado-condition');
  if (!monthlyRoleAllowed(input.monthlyFinancialRole, direction))
    problems.push('monthly-direction');
  return problems;
}
/**
 * Create and update send every field the form shows. The update endpoint keeps an omitted
 * classification but stores the request's `FiadoPaymentKind`, settlement rule and fiado flag as
 * sent, so nothing here is left to an accidental default; explicit `None` clears a classification.
 */
export function typePayload(input: TypeInput): RequestUpdateTransactionTypeJson {
  return {
    name: input.name,
    settlementRule: input.settlementRule as SettlementRule,
    requiresTabAccountAndClient: input.requiresTabAccountAndClient,
    fiadoPaymentKind:
      input.fiadoPaymentKind === 'CountedCash' || input.fiadoPaymentKind === 'NonCash'
        ? input.fiadoPaymentKind
        : null,
    settlementInstrument: input.settlementInstrument,
    monthlyFinancialRole: input.monthlyFinancialRole,
  };
}
export const typeCreatePayload = (input: TypeInput): RequestCreateTransactionTypeJson => ({
  categoryId: input.categoryId,
  ...typePayload(input),
});
/**
 * A report classification change against the accepted reference (the server's current values the
 * editor last confirmed or adopted); it re-labels that type's past rows.
 */
export const reclassifies = (
  reference: Pick<TypeInput, 'settlementInstrument' | 'monthlyFinancialRole'>,
  input: TypeInput,
) =>
  reference.settlementInstrument !== input.settlementInstrument ||
  reference.monthlyFinancialRole !== input.monthlyFinancialRole;
/** Mirrors IsSystemCategoryAsNoTracking over the active types read (server stays final). */
const systemCategoryRoles: readonly TransactionTypeSystemRole[] = [
  'FiadoSaleCounterpart',
  'FiadoCashRepaymentCounterpart',
  'SafeDepositIn',
];
export const isSystemCategory = (
  categoryId: string,
  types: readonly ResponseTransactionTypeJson[],
) =>
  types.some(
    (t) =>
      t.categoryId === categoryId && t.systemRole && systemCategoryRoles.includes(t.systemRole),
  );
export const hostsSystemType = (
  categoryId: string,
  types: readonly ResponseTransactionTypeJson[],
) => types.some((t) => t.categoryId === categoryId && t.systemRole);

/* Branch members and invitations ------------------------------------------------------------- */

/** BranchUserPermissionRules.CanAssignRole: an Admin assigns any role, a Manager never Admin. */
export const assignableRoles = (actor: Role): readonly Role[] =>
  actor === 'Admin'
    ? ['Admin', 'Manager', 'Member']
    : actor === 'Manager'
      ? ['Manager', 'Member']
      : [];
export type MemberRoleState =
  | { readonly kind: 'allowed'; readonly roles: readonly Role[] }
  | { readonly kind: 'self' | 'admin-target' | 'denied'; readonly reason: string };
export const SELF_ROLE_REASON =
  'Você não pode alterar o próprio cargo nem remover o próprio acesso.';
/** Preemptive UI gate; the server enforces self, role matrix and last-active-Admin rules. */
export function memberRoleState(
  actor: Role,
  actorUserId: string | undefined,
  member: ResponseBranchUserJson,
): MemberRoleState {
  if (member.userId === actorUserId) return { kind: 'self', reason: SELF_ROLE_REASON };
  if (actor === 'Manager' && member.role === 'Admin')
    return {
      kind: 'admin-target',
      reason: 'Somente um administrador pode alterar ou remover o acesso de um administrador.',
    };
  if (actor !== 'Admin' && actor !== 'Manager')
    return { kind: 'denied', reason: 'Seu perfil não gerencia membros.' };
  return { kind: 'allowed', roles: assignableRoles(actor) };
}
export const LAST_ADMIN_REASON =
  'Esta é a única pessoa administradora ativa da filial. Promova outra pessoa a administrador antes de mudar este cargo ou remover este acesso.';
export const isLastActiveAdmin = (
  member: ResponseBranchUserJson,
  members: readonly ResponseBranchUserJson[],
) => member.role === 'Admin' && members.filter((m) => m.active && m.role === 'Admin').length <= 1;
export interface InvitationInput {
  readonly email: string;
  readonly role: Role | '';
  readonly operatorId: string;
}
export const isInvitationInput = (value: unknown): value is InvitationInput =>
  exactKeys(value, ['email', 'role', 'operatorId']) &&
  text(value.email) &&
  ['', 'Admin', 'Manager', 'Member'].includes(value.role as string) &&
  text(value.operatorId);
/** An operator preselection exists only on a Member invitation (INVITATION_OPERATOR_ROLE_INVALID). */
export const invitationPayload = (input: InvitationInput): RequestIssueInvitationJson => ({
  email: input.email.trim(),
  role: input.role as Role,
  operatorId: input.role === 'Member' && input.operatorId !== '' ? input.operatorId : null,
});
/** 'keep' omits the body (preserves the choice); 'none' clears it; an id replaces it. */
export type ReissueChoice = 'keep' | 'none' | string;
export const reissueBody = (choice: ReissueChoice): RequestReissueInvitationJson | undefined =>
  choice === 'keep' ? undefined : { operatorId: choice === 'none' ? null : choice };
/** Active operators without a login link (RequireAvailableOperator). */
export const unlinkedOperators = (operators: readonly ResponseOperatorJson[]) =>
  operators.filter((o) => !o.userId);
export const reissuable = (invitation: ResponseInvitationJson) => invitation.status !== 'Accepted';
