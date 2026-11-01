// Request composition and presentation helpers for the M10 admin surfaces. Every rule here mirrors
// the shipped DTO/use-case behavior; permissions, uniqueness and ranges stay server-owned.
import type {
  ResponseBranchUserJson,
  ResponseClientJson,
  ResponseOperatorJson,
  ResponseProductJson,
  RequestCreateClientJson,
  RequestCreateHolidaysJson,
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
