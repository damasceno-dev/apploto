import { isApiError } from '@loto/api';

export type ShellRead<T> =
  | { readonly data: T; readonly failure: null }
  | { readonly data: null; readonly failure: readonly string[] };
/** Optional shell metadata cannot veto a branch gate or an otherwise permitted destination. */
export async function optionalShellRead<T>(read: () => Promise<T>): Promise<ShellRead<T>> {
  try {
    return { data: await read(), failure: null };
  } catch (error) {
    if (!isApiError(error)) throw error; // Preserve Next redirects and unexpected programming errors.
    return { data: null, failure: error.errorMessages };
  }
}
