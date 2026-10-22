import 'server-only';
import {
  ApiError,
  createApiClient,
  getBranchMyBranches,
  isApiError,
  postBranchSession,
  postUserLogin,
  postUserLogout,
  postUserRenewToken,
  type ApiClient,
  type RequestUserLoginJson,
  type ResponseBranchSummaryJson,
  type ResponseCreateBranchSessionJson,
  type ResponseTokenJson,
  type ResponseUserLoginJson,
} from '@loto/api';

export type BackendResult<T> =
  | { readonly ok: true; readonly data: T; readonly headers: Headers }
  | { readonly ok: false; readonly error: ApiError };

/** The session contracts the BFF itself calls. Every call uses a per-call client and one token. */
export interface AuthBackend {
  login(body: RequestUserLoginJson): Promise<BackendResult<ResponseUserLoginJson>>;
  renew(refreshToken: string): Promise<BackendResult<ResponseTokenJson>>;
  logout(identityToken: string, refreshToken: string): Promise<BackendResult<void>>;
  createBranchSession(
    identityToken: string,
    branchId: string,
  ): Promise<BackendResult<ResponseCreateBranchSessionJson>>;
  myBranches(identityToken: string): Promise<BackendResult<readonly ResponseBranchSummaryJson[]>>;
}

export interface AuthBackendConfig {
  readonly baseUrl: string;
  readonly fetch?: typeof globalThis.fetch;
}

export function createAuthBackend(config: AuthBackendConfig): AuthBackend {
  const client = (token: string | null): ApiClient =>
    createApiClient({
      baseUrl: config.baseUrl,
      getAccessToken: () => token,
      fetch: (input, init) =>
        (config.fetch ?? globalThis.fetch)(input, {
          ...init,
          cache: 'no-store',
          redirect: 'manual',
        }),
    });

  return {
    login: (body) => settle(postUserLogin(body, { client: client(null) })),
    renew: (refreshToken) =>
      settle(postUserRenewToken({ value: refreshToken }, { client: client(null) })),
    logout: (identityToken, refreshToken) =>
      settle(postUserLogout({ refreshToken }, { client: client(identityToken) })),
    createBranchSession: (identityToken, branchId) =>
      settle(postBranchSession({ branchId }, { client: client(identityToken) })),
    myBranches: async (identityToken) => {
      const result = await settle(getBranchMyBranches({ client: client(identityToken) }));
      return result.ok ? { ...result, data: result.data.branches } : result;
    },
  };
}

async function settle<T>(
  pending: Promise<{ data: T; headers: Headers }>,
): Promise<BackendResult<T>> {
  try {
    const { data, headers } = await pending;
    return { ok: true, data, headers };
  } catch (error) {
    if (isApiError(error)) return { ok: false, error };
    throw error;
  }
}

/** A definite HTTP answer below 500: the server decided, so it is final for that token. */
export function isDefinitiveRefusal(error: ApiError): boolean {
  return error.kind === 'http' && error.status !== undefined && error.status < 500;
}
