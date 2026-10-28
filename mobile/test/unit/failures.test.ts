import { ApiError } from '@loto/api';
import { actionFailure } from '../../src/data/failures';
it('quarantines an unreadable successful financial response rather than allowing a fresh create', () => {
  const failure = actionFailure(
    new ApiError({
      kind: 'invalid-response',
      method: 'POST',
      path: '/transaction',
      dispatched: true,
      status: 201,
    }),
  );
  expect(failure.kind).toBe('uncertain');
});
it('keeps server rejection messages and Retry-After separate from recovery copy', () => {
  const failure = actionFailure(
    new ApiError({
      kind: 'http',
      method: 'POST',
      path: '/transaction',
      dispatched: true,
      status: 429,
      errorMessages: ['Aguarde.', 'Aguarde.'],
      headers: new Headers({ 'Retry-After': '7' }),
    }),
  );
  expect(failure).toMatchObject({
    kind: 'rate-limited',
    messages: ['Aguarde.', 'Aguarde.'],
    retryAfter: '7',
    recovery: '',
  });
});
