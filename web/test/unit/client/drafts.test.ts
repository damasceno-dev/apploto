import { describe, expect, it } from 'vitest';
import {
  clearDraft,
  clearPartitionDrafts,
  loadDraft,
  saveDraft,
} from '@/client/drafts/draft-store';
import { partitionFor, sha256Hex } from '@/client/storage/partition';
import { anonymousSnapshot } from '@/lib/bff-protocol';
import { MemoryStorage } from '../support/memory-storage';

const ana = { userKey: 'ana', branchId: 'b1' };
const anaOtherBranch = { userKey: 'ana', branchId: 'b2' };
const bia = { userKey: 'bia', branchId: 'b1' };

describe('device drafts', () => {
  it('isolates input by identity and branch, and keeps blanks distinct from zero', () => {
    const storage = new MemoryStorage();
    expect(
      saveDraft(storage, ana, 'close-day:acc-1:2026-10-21', { count: '', notes: '0,5' }),
    ).toEqual({ ok: true });
    expect(loadDraft(storage, ana, 'close-day:acc-1:2026-10-21')).toEqual({
      status: 'found',
      data: { count: '', notes: '0,5' },
    });
    expect(loadDraft(storage, anaOtherBranch, 'close-day:acc-1:2026-10-21')).toEqual({
      status: 'none',
    });
    expect(loadDraft(storage, bia, 'close-day:acc-1:2026-10-21')).toEqual({ status: 'none' });
  });

  it('refuses passwords, tokens, Google proof and capability values', () => {
    const storage = new MemoryStorage();
    const refused = [
      { password: 'x' },
      { form: { currentPassword: 'x' } },
      { refresh_token: 'x' },
      { idToken: 'x' },
      { challengeId: 'x' },
      { note: 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln' },
      { link: 'https://app.example/reset-password#token=abc' },
      { link: 'https://app.example/x?code=abc&state=1' },
      { items: [{ ok: 1 }, { Token: 'x' }] },
    ];
    for (const data of refused) {
      expect(saveDraft(storage, ana, 'form', data)).toMatchObject({ ok: false, reason: 'privacy' });
    }
    expect(storage.keys()).toEqual([]);
    expect(
      saveDraft(storage, ana, 'form', { notes: 'Troco de R$ 50,00', status: 'Draft' }),
    ).toEqual({ ok: true });
  });

  it('reports corruption and unavailability without deleting anything', () => {
    const storage = new MemoryStorage();
    storage.setItem('loto:draft:v1:ana:b1:form', '{not json');
    expect(loadDraft(storage, ana, 'form')).toEqual({ status: 'corrupt' });
    expect(storage.getItem('loto:draft:v1:ana:b1:form')).toBe('{not json');
    storage.failReads = true;
    expect(loadDraft(storage, ana, 'form')).toEqual({ status: 'unavailable' });
    storage.failReads = false;
    storage.failWrites = true;
    expect(saveDraft(storage, ana, 'form', { a: 1 })).toEqual({ ok: false, reason: 'unavailable' });
    expect(saveDraft(null, ana, 'form', { a: 1 })).toEqual({ ok: false, reason: 'unavailable' });
  });

  it('clears only its own context or partition, never other users, branches or apps', () => {
    const storage = new MemoryStorage();
    saveDraft(storage, ana, 'a', { v: 1 });
    saveDraft(storage, ana, 'b', { v: 2 });
    saveDraft(storage, anaOtherBranch, 'a', { v: 3 });
    saveDraft(storage, bia, 'a', { v: 4 });
    storage.setItem('other-app', 'keep');
    storage.setItem('loto:draft:v1:ana:b1x:a', 'lookalike');
    expect(clearDraft(storage, ana, 'a')).toBe(true);
    expect(loadDraft(storage, ana, 'b').status).toBe('found');
    expect(clearPartitionDrafts(storage, ana)).toBe(1);
    expect(storage.keys().sort()).toEqual(
      [
        'loto:draft:v1:ana:b2:a',
        'loto:draft:v1:bia:b1:a',
        'loto:draft:v1:ana:b1x:a',
        'other-app',
      ].sort(),
    );
  });

  it('rejects contexts that could escape their partition', () => {
    const storage = new MemoryStorage();
    expect(() => saveDraft(storage, ana, '../bia', {})).toThrow(RangeError);
    expect(() => saveDraft(storage, ana, 'a?token=1', {})).toThrow(RangeError);
  });

  it('derives a partition only for an authorized branch session, from the returned e-mail', async () => {
    expect(await partitionFor(anonymousSnapshot)).toBeNull();
    expect(
      await partitionFor({
        ...anonymousSnapshot,
        state: 'choose-branch',
        user: { userId: 'u', name: 'A', email: 'a@x' },
      }),
    ).toBeNull();
    const partition = await partitionFor({
      ...anonymousSnapshot,
      state: 'branch',
      user: { userId: 'u', name: 'A', email: 'ana@example.invalid' },
      branch: { id: 'b1', name: 'B', role: 'Member' },
    });
    expect(partition).toEqual({ userKey: await sha256Hex('ana@example.invalid'), branchId: 'b1' });
    expect(partition?.userKey).not.toContain('ana');
  });
});
