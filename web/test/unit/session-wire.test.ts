import { describe, expect, it } from 'vitest';
import { isSessionSnapshot } from '@/client/session/session-client';
import { anonymousSnapshot } from '@/lib/bff-protocol';
describe('credential-free session wire', () => {
  it('accepts the known anonymous envelope', () => {
    expect(isSessionSnapshot(anonymousSnapshot)).toBe(true);
  });
  it('rejects malformed 2xx envelopes instead of treating them as login success', () => {
    for (const value of [
      null,
      {},
      [],
      { ...anonymousSnapshot, state: 'success' },
      { ...anonymousSnapshot, messages: 'wrong' },
      { ...anonymousSnapshot, state: 'branch' },
      { ...anonymousSnapshot, user: { email: 'missing identity' } },
      { ...anonymousSnapshot, scope: 42 },
      { ...anonymousSnapshot, branches: [{}] },
    ])
      expect(isSessionSnapshot(value)).toBe(false);
  });
});
