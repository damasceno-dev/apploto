import { describe, expect, it } from 'vitest';
import { anonymousSnapshot, type SessionSnapshot, type SessionState } from '@/lib/bff-protocol';
import {
  decideGate,
  gateDestinations,
  landingFor,
  routeTier,
  validateIntentPath,
} from '@/lib/route-policy';

const snapshot = (state: SessionState, extra: Partial<SessionSnapshot> = {}): SessionSnapshot => ({
  ...anonymousSnapshot,
  state,
  ...extra,
});

describe('session gate', () => {
  it('classifies the blueprint route families', () => {
    expect(routeTier('/entrar')).toBe('anonymous');
    expect(routeTier('/filiais')).toBe('identity');
    expect(routeTier('/conta/senha')).toBe('identity');
    expect(routeTier('/gestao/fila')).toBe('branch');
    expect(routeTier('/transacoes/abc')).toBe('branch');
    expect(routeTier('/gestaox')).toBe('public');
    expect(routeTier('/e2e/branch/read')).toBe('public');
    expect(routeTier('/e2e/branch/read', { fixtures: true })).toBe('branch');
  });

  it('resolves every authority state before a branch route renders', () => {
    const branchRoute = (state: SessionState) => decideGate('branch', snapshot(state));
    expect(branchRoute('anonymous')).toEqual({
      kind: 'redirect',
      to: gateDestinations.login,
      holdIntent: true,
    });
    expect(branchRoute('renewal-unconfirmed')).toEqual({
      kind: 'redirect',
      to: gateDestinations.login,
      holdIntent: true,
    });
    for (const state of ['no-memberships', 'choose-branch', 'branch-unavailable'] as const) {
      expect(branchRoute(state)).toEqual({
        kind: 'redirect',
        to: gateDestinations.branchPicker,
        holdIntent: true,
      });
    }
    expect(branchRoute('branch')).toEqual({ kind: 'allow' });
  });

  it('lets identity-only states reach identity routes, and only identity', () => {
    expect(decideGate('identity', snapshot('choose-branch'))).toEqual({ kind: 'allow' });
    expect(decideGate('identity', snapshot('no-memberships'))).toEqual({ kind: 'allow' });
    expect(decideGate('identity', snapshot('anonymous'))).toMatchObject({
      to: '/entrar',
      holdIntent: false,
    });
    expect(decideGate('anonymous', snapshot('anonymous'))).toEqual({ kind: 'allow' });
  });

  it('lands on the live role home or the authorized intent, never inferring role from mode', () => {
    const member = snapshot('branch', { branch: { id: 'b', name: 'B', role: 'Member' } });
    expect(landingFor(member)).toBe('/turno/hoje');
    expect(landingFor({ ...member, branch: { id: 'b', name: 'B', role: 'Manager' } })).toBe(
      '/gestao/fila',
    );
    expect(landingFor({ ...member, resume: '/transacoes/nova' })).toBe('/transacoes/nova');
    expect(landingFor(snapshot('choose-branch'))).toBe('/filiais');
  });
});

describe('navigation intent', () => {
  it('accepts only same-origin identity/branch paths', () => {
    expect(validateIntentPath('/transacoes?de=2026-10-01&ate=2026-10-21')).toBe(
      '/transacoes?de=2026-10-01&ate=2026-10-21',
    );
    expect(validateIntentPath('/conta')).toBe('/conta');
    for (const value of [
      'https://evil.example/gestao',
      '//evil.example/gestao',
      '/\\evil.example',
      '/gestao/fila#x',
      '/entrar',
      '/reset-password',
      '/invitation',
      '/bff/api/transaction',
      '/',
      'gestao',
      `/gestao/${'a'.repeat(600)}`,
      '/gestao/\u0000',
      42,
    ]) {
      expect(validateIntentPath(value), String(value)).toBeNull();
    }
  });
});

describe('navigation intent privacy (review P2-8)', () => {
  it('drops an intent whose query could carry a credential, keeps ordinary filters', () => {
    for (const value of [
      '/transacoes?token=capability-secret',
      '/transacoes?de=2026-10-01&access_token=x',
      '/gestao/fila?code=abc&state=xyz',
      '/conta?senha=1',
      '/transacoes?ref=eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2ln',
      '/transacoes?X-Api-Key=1',
    ]) {
      expect(validateIntentPath(value), value).toBeNull();
    }
    expect(validateIntentPath('/transacoes?de=2026-10-01&conta=7&status=Draft&page=2')).toBe(
      '/transacoes?de=2026-10-01&conta=7&status=Draft&page=2',
    );
  });
});
