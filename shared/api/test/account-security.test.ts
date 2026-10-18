import { describe, expect, it } from 'vitest';
import {
  ApiError,
  createApiClient,
  getUserProfile,
  postInvitationAccept,
  postOperatorLinkRequest,
  postUserForgotPassword,
  type AccessTokenRequest,
} from '../src/index';
import { fakeFetch, json } from './fake-fetch';

describe('M8 generated account and invitation contracts', () => {
  it('uses the generated anonymous/identity/branch tiers and keeps capability bodies separate from bearer authority', async () => {
    const observed: AccessTokenRequest[] = [];
    const transport = fakeFetch((call) =>
      call.url.endsWith('/user/forgot-password')
        ? new Response(null, { status: 202 })
        : json(200, { userId: 'caller-id', name: 'Pessoa', email: 'person@example.invalid' }),
    );
    const client = createApiClient({
      baseUrl: 'https://api.test',
      fetch: transport.fetch,
      getAccessToken: (context) => {
        observed.push(context);
        return context.authTier === 'anonymous' ? null : `${context.authTier}-authority`;
      },
    });
    expect((await getUserProfile({ client })).data.userId).toBe('caller-id');
    await postUserForgotPassword({ email: 'person@example.invalid' }, { client });
    await postInvitationAccept({ token: 'local-capability' }, { client });
    await postOperatorLinkRequest({ client });
    expect(observed).toEqual([
      { method: 'GET', path: '/user/profile', authTier: 'identity' },
      { method: 'POST', path: '/user/forgot-password', authTier: 'anonymous' },
      { method: 'POST', path: '/invitation/accept', authTier: 'identity' },
      { method: 'POST', path: '/operator-link-request', authTier: 'branch' },
    ]);
    expect(transport.calls.map((call) => call.headers.get('Authorization'))).toEqual([
      'Bearer identity-authority',
      null,
      'Bearer identity-authority',
      'Bearer branch-authority',
    ]);
    expect(transport.calls[2]?.body).toBe('{"token":"local-capability"}');
  });

  it('preserves verbatim resource errors and makes exactly one acceptance attempt', async () => {
    const transport = fakeFetch(() =>
      json(409, { errorMessages: ['O código é inválido, expirou ou já foi utilizado.'] }),
    );
    const client = createApiClient({
      baseUrl: 'https://api.test',
      fetch: transport.fetch,
      getAccessToken: () => 'identity',
    });
    await expect(
      postInvitationAccept({ token: 'expired-local-capability' }, { client }),
    ).rejects.toMatchObject({
      kind: 'http',
      status: 409,
      errorMessages: ['O código é inválido, expirou ou já foi utilizado.'],
    } satisfies Partial<ApiError>);
    expect(transport.calls).toHaveLength(1);
  });
});
