import type { RequestUserLoginJson, RequestCreateTransactionJson } from '@loto/api';
import { validFinancialPayload } from '../data/request-shape';
import { isServerId } from '../session/credentials';
export interface NativeFixture {
  readonly login: RequestUserLoginJson;
  readonly branchId: string;
  readonly notes: string;
  readonly payload: RequestCreateTransactionJson;
}
/** Input data only. Real generated API calls, credential storage and gates still run on-device. */
export async function readNativeFixture(baseUrl: string): Promise<NativeFixture> {
  if (
    !__DEV__ ||
    process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF !== '1' ||
    process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL !== '1'
  )
    throw new Error('Fixture técnica indisponível.');
  const url = new URL(baseUrl);
  if (url.protocol !== 'http:' || !['127.0.0.1', 'localhost', '10.0.2.2'].includes(url.hostname))
    throw new Error('Use somente a fixture local descartável.');
  const response = await globalThis.fetch(`${baseUrl}/__native-fixture`);
  if (!response.ok) throw new Error('Não foi possível ler a fixture local.');
  const value = (await response.json()) as NativeFixture;
  if (
    !value ||
    !value.login ||
    typeof value.login.email !== 'string' ||
    !value.login.email.endsWith('@example.invalid') ||
    typeof value.login.password !== 'string' ||
    !value.login.password ||
    !isServerId(value.branchId) ||
    typeof value.notes !== 'string' ||
    !validFinancialPayload('transaction', value.payload)
  )
    throw new Error('Fixture local inválida.');
  return value;
}
