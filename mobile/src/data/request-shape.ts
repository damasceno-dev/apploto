// Structural retained-input checks derive from .NET OpenAPI; no financial/domain validation.
import schemas from './request-shapes.generated.json';
import { parseCalendarDate, parseInstant, parseWallTime, formatWallDateTime } from '@loto/core';
interface Schema {
  readonly $ref?: string;
  readonly type?: string | readonly string[];
  readonly properties?: Readonly<Record<string, Schema>>;
  readonly required?: readonly string[];
  readonly items?: Schema;
  readonly enum?: readonly unknown[];
  readonly pattern?: string;
  readonly format?: string;
}
function shape(schema: Schema, value: unknown, depth = 0): boolean {
  if (depth > 32) return false;
  if (schema.$ref) {
    const name = schema.$ref.split('/').pop()!;
    const referenced = (schemas as unknown as Record<string, Schema>)[name];
    return !!referenced && shape(referenced, value, depth + 1);
  }
  const types = Array.isArray(schema.type) ? schema.type : [schema.type];
  const type = value === null ? 'null' : Array.isArray(value) ? 'array' : typeof value;
  if (
    !types.includes(type) &&
    !(type === 'number' && Number.isSafeInteger(value) && types.includes('integer'))
  )
    return false;
  if (schema.enum && !schema.enum.includes(value)) return false;
  if (type === 'object') {
    const record = value as Record<string, unknown>;
    return (
      !!schema.properties &&
      (schema.required ?? []).every((key) => key in record) &&
      Object.keys(record).every(
        (key) =>
          !!schema.properties?.[key] && shape(schema.properties[key]!, record[key], depth + 1),
      )
    );
  }
  if (type === 'array')
    return (
      !!schema.items && (value as unknown[]).every((item) => shape(schema.items!, item, depth + 1))
    );
  if (type === 'number') return Number.isFinite(value);
  if (type === 'string') {
    const text = value as string;
    if (schema.pattern && !new RegExp(schema.pattern).test(text)) return false;
    if (schema.format === 'uuid')
      return /^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(text);
    if (schema.format === 'date-time') {
      if (parseCalendarDate(text) || parseInstant(text) !== null) return true;
      try {
        formatWallDateTime(text);
        return true;
      } catch {
        return false;
      }
    }
    if (schema.format === 'time') return parseWallTime(text) !== null;
  }
  return true;
}
const roots = {
  transaction: 'RequestCreateTransactionJson',
  installment: 'RequestCreateTransactionInstallmentJson',
  'fiado-payment': 'RequestReceiveFiadoPaymentJson',
} as const;
export function validFinancialPayload(operation: keyof typeof roots, payload: unknown): boolean {
  return shape((schemas as unknown as Record<string, Schema>)[roots[operation]]!, payload);
}
