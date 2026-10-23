import { describe, expect, it } from 'vitest';
import { anonymousSnapshot, type SessionSnapshot, type SessionState } from '@/lib/bff-protocol';
import { branchPath, parseBranchPath } from '@/lib/branch-route';
import {
  decideGate,
  gateDestinations,
  landingFor,
  routeBranch,
  routeTier,
  validateIntentPath,
} from '@/lib/route-policy';
import { B1, B2 } from '../support/session-fixtures';

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
    expect(routeTier('/e2e/session')).toBe('public');
  });

  it('puts every branch destination under /f/{branchId}, accepting only real branch ids', () => {
    expect(routeTier(`/f/${B1}/gestao/fila`)).toBe('branch');
    expect(routeTier(`/f/${B1}`)).toBe('branch');
    expect(routeTier(`/f/${B1}/e2e/read`)).toBe('branch');
    expect(routeBranch(`/f/${B1}/transacoes/nova`)).toBe(B1);
    expect(routeBranch('/transacoes/nova')).toBeNull();
    for (const path of [
      '/f/b1/gestao',
      '/f/../gestao',
      '/f/ABCDEF12-1111-4111-8111-111111111111/gestao',
      '/f',
    ]) {
      expect(routeTier(path), path).not.toBe('branch');
      expect(parseBranchPath(path), path).toBeNull();
    }
    expect(parseBranchPath(`/f/${B1}/gestao/fila`)).toEqual({ branchId: B1, rest: '/gestao/fila' });
    expect(branchPath(B2, '/turno/hoje')).toBe(`/f/${B2}/turno/hoje`);
    expect(() => branchPath('../x', '/gestao')).toThrow();
  });

  it('a branch address this identity cannot open is a 403, never a redirect into another branch', () => {
    const refused = decideGate(
      'branch',
      snapshot('branch-unavailable', { messages: ['Filial não encontrada.'] }),
      { branchId: B1 },
    );
    expect(refused).toEqual({ kind: 'forbidden', messages: ['Filial não encontrada.'] });
    expect(decideGate('branch', snapshot('branch'), { branchId: B1 })).toEqual({ kind: 'allow' });
    // An unscoped branch address carries no tenant: it waits for a chosen branch.
    expect(decideGate('branch', snapshot('branch'))).toEqual({
      kind: 'redirect',
      to: gateDestinations.branchPicker,
      holdIntent: true,
    });
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
    expect(decideGate('branch', snapshot('branch'), { branchId: B1 })).toEqual({ kind: 'allow' });
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

  it('lands on the role home of the opened branch or the authorized intent, in its namespace', () => {
    const member = snapshot('branch', { branch: { id: B1, name: 'B', role: 'Member' } });
    expect(landingFor(member)).toBe(`/f/${B1}/turno/hoje`);
    expect(landingFor({ ...member, branch: { id: B1, name: 'B', role: 'Manager' } })).toBe(
      `/f/${B1}/gestao/fila`,
    );
    expect(landingFor({ ...member, resume: '/transacoes/nova' })).toBe(`/f/${B1}/transacoes/nova`);
    expect(landingFor({ ...member, resume: `/f/${B2}/transacoes` })).toBe(`/f/${B2}/transacoes`);
    expect(landingFor({ ...member, resume: '/conta' })).toBe('/conta');
    expect(landingFor(snapshot('choose-branch'))).toBe('/filiais');
    expect(landingFor(snapshot('choose-branch', { resume: `/f/${B2}/gestao/fila` }))).toBe(
      `/f/${B2}/gestao/fila`,
    );
  });
});

describe('navigation intent', () => {
  it('accepts only same-origin identity/branch paths', () => {
    expect(validateIntentPath('/transacoes?de=2026-10-01&ate=2026-10-21')).toBe(
      '/transacoes?de=2026-10-01&ate=2026-10-21',
    );
    expect(validateIntentPath('/conta')).toBe('/conta');
    expect(validateIntentPath(`/f/${B1}/gestao/fila?data=2026-10-21`)).toBe(
      `/f/${B1}/gestao/fila?data=2026-10-21`,
    );
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
      '/f/not-a-branch/gestao',
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
      `/f/${B1}/transacoes?token=x`,
    ]) {
      expect(validateIntentPath(value), value).toBeNull();
    }
    expect(validateIntentPath('/transacoes?de=2026-10-01&conta=7&status=Draft&page=2')).toBe(
      '/transacoes?de=2026-10-01&conta=7&status=Draft&page=2',
    );
  });
});
