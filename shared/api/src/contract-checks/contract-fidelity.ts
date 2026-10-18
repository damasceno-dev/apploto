// Compile-time contract gates over the generated root entry (Shared M2 2.6, 2.7, 2.8). Names and
// shapes are taken from the committed document; a regeneration that loses a guarantee fails
// `pnpm -r typecheck`.
import type * as Model from '../generated/model';
import {
  postTransaction,
  postTransactionInstallment,
  postTransactionTransactionIdFinalize,
  putTransactionTransactionId,
  getTransactionTransactionId,
  type DailyCloseStatus,
  type RequestCreateTransactionInstallmentJson,
  type RequestCreateTransactionJson,
  type RequestUpdateTransactionJson,
  type ResponseCreateBranchSessionJson,
  type ResponseErrorJson,
  type ResponseGetCurrentBranchSummaryJson,
  type ResponseTokenJson,
  type ResponseTransactionJson,
  type Role,
  type TransactionStatus,
} from '../index';
import type { Equal, Expect, IsNullable, IsOptional } from './assert';

// 2.6 — enum names, never numbers.
export type EnumNameChecks = [
  Expect<Equal<TransactionStatus, 'Draft' | 'Active' | 'Cancelled'>>,
  Expect<Equal<DailyCloseStatus, 'Draft' | 'Submitted' | 'Approved' | 'Rejected'>>,
  Expect<Equal<Role, 'Admin' | 'Manager' | 'Member'>>,
];

/** Every generated enum object (`export const X = { … } as const`) whose values are not all strings. */
type ModelValues = typeof Model;
type NonStringEnums = {
  [K in keyof ModelValues]: ModelValues[K] extends Record<string, unknown>
    ? ModelValues[K][keyof ModelValues[K]] extends string
      ? never
      : K
    : K;
}[keyof ModelValues];
export type NoNumericEnumChecks = [
  Expect<Equal<NonStringEnums, never>>,
  // The walk is not vacuous: the named enums are among the generated values.
  Expect<
    Equal<
      'TransactionStatus' | 'DailyCloseStatus' | 'Role' extends keyof ModelValues ? true : false,
      true
    >
  >,
];

export function enumNameGate(
  transaction: (value: TransactionStatus) => void,
  close: (value: DailyCloseStatus) => void,
  role: (value: Role) => void,
): void {
  transaction('Draft');
  close('Submitted');
  role('Manager');
  // @ts-expect-error A numeric literal is not a TransactionStatus.
  transaction(0);
  // @ts-expect-error A numeric literal is not a DailyCloseStatus.
  close(1);
  // @ts-expect-error A numeric literal is not a Role.
  role(2);
}

// 2.7 — required and nullable fidelity, dates as strings, numeric direction (server H3).
export type FidelityChecks = [
  Expect<Equal<ResponseTokenJson['token'], string>>,
  Expect<Equal<ResponseTokenJson['refreshToken'], string>>,
  Expect<Equal<IsOptional<ResponseTokenJson, 'token' | 'refreshToken'>, false>>,
  Expect<Equal<IsOptional<ResponseCreateBranchSessionJson, 'token'>, false>>,
  Expect<Equal<IsOptional<ResponseCreateBranchSessionJson, 'branch'>, false>>,
  Expect<Equal<ResponseErrorJson['errorMessages'], string[] | null>>,
  Expect<Equal<IsOptional<ResponseErrorJson, 'errorMessages'>, false>>,
  Expect<Equal<IsOptional<RequestCreateTransactionJson, 'clientId'>, true>>,
  Expect<Equal<IsNullable<RequestCreateTransactionJson['clientId']>, true>>,
  // Branch-local calendar values stay strings: the offset-free wall clock is never a Date.
  Expect<Equal<ResponseGetCurrentBranchSummaryJson['branchLocalDate'], string>>,
  Expect<Equal<ResponseGetCurrentBranchSummaryJson['branchLocalDateTime'], string>>,
  Expect<Equal<IsOptional<ResponseGetCurrentBranchSummaryJson, 'branchLocalDateTime'>, false>>,
  // Responses carry real numbers; requests keep the numeric strings binding accepts.
  Expect<Equal<ResponseTransactionJson['value'], number>>,
  Expect<Equal<ResponseTransactionJson['version'], number>>,
  Expect<Equal<RequestCreateTransactionJson['value'], number | string>>,
];

// 2.8 — financial headers are required arguments, and ETag is readable from raw results.
export function financialHeaderGate(
  create: RequestCreateTransactionJson,
  installment: RequestCreateTransactionInstallmentJson,
  update: RequestUpdateTransactionJson,
): void {
  void postTransaction(create, { 'Idempotency-Key': 'frozen-key' });
  // Ordinary RequestInit fields, headers included, stay available per call. A caller header that
  // conflicts with a generated one or carries Authorization is refused at runtime (tests cover it).
  void postTransaction(create, { 'Idempotency-Key': 'A' }, { headers: { 'X-Trace': 't' } });
  // @ts-expect-error Options never carry a body: the generated function owns it.
  void postTransaction(create, { 'Idempotency-Key': 'A' }, { body: '{}' });
  // @ts-expect-error POST /transaction requires the header argument.
  void postTransaction(create);
  // @ts-expect-error POST /transaction requires Idempotency-Key.
  void postTransaction(create, {});

  void postTransactionInstallment(installment, { 'Idempotency-Key': 'frozen-key' });
  // @ts-expect-error POST /transaction/installment requires Idempotency-Key.
  void postTransactionInstallment(installment, {});

  void putTransactionTransactionId('id', update, { 'If-Match': '"1"' });
  // @ts-expect-error PUT /transaction/{transactionId} requires If-Match.
  void putTransactionTransactionId('id', update, {});

  void postTransactionTransactionIdFinalize('id', { 'If-Match': '"1"' });
  // @ts-expect-error POST /transaction/{transactionId}/finalize requires If-Match.
  void postTransactionTransactionIdFinalize('id', {});
}

export type ETagReadabilityChecks = [
  Expect<Equal<Awaited<ReturnType<typeof getTransactionTransactionId>>['headers'], Headers>>,
  Expect<Equal<Awaited<ReturnType<typeof postTransaction>>['headers'], Headers>>,
  Expect<Equal<Awaited<ReturnType<typeof putTransactionTransactionId>>['headers'], Headers>>,
];
