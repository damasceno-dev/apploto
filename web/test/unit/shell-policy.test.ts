import { describe, expect, it } from 'vitest';
import {
  destinations,
  canEnter,
  defaultMode,
  destinationMode,
  destinationHref,
  matchDestination,
} from '@/lib/destinations';
import { validateNavigation } from '@/lib/navigation-state';
const branch = '12345678-1234-1234-1234-123456789abc';
const other = '12345678-1234-1234-1234-123456789abd';
const date = '2026-10-28';
const state = {
  href: `/f/${branch}/transacoes`,
  mode: 'shift',
  origins: [],
  scroll: 25,
  focus: 'conteudo',
};
describe('catalog destination policy', () => {
  it('has unique keys/templates and exactly nine management reports', () => {
    expect(new Set(destinations.map((entry) => entry.key)).size).toBe(destinations.length);
    expect(new Set(destinations.map((entry) => entry.path)).size).toBe(destinations.length);
    expect(destinations.filter((entry) => entry.group === 'Relatórios')).toHaveLength(9);
    expect(
      destinations
        .filter((entry) => entry.group === 'Relatórios')
        .every((entry) => entry.owner === 'management'),
    ).toBe(true);
  });
  for (const role of ['Member', 'Manager', 'Admin'] as const) {
    it(`resolves ${role} home, shift clock and management access`, () => {
      expect(defaultMode(role)).toBe(role === 'Member' ? 'shift' : 'management');
      for (const entry of destinations) {
        expect(canEnter(entry, role)).toBe(
          !(entry.owner === 'management' && role === 'Member') &&
            !(entry.memberOnly && role !== 'Member'),
        );
      }
      for (const key of ['open', 'close', 'correction']) {
        const entry = destinations.find((entry) => entry.key === key)!;
        expect(entry.owner).toBe('shared');
        expect(canEnter(entry, role)).toBe(true);
      }
    });
  }
  for (const entry of destinations) {
    it(`recognizes exact ${entry.key} route`, () => {
      const path = entry.path
        .replace(':id', other)
        .replace(':year', '2026')
        .replace(':month', '10');
      expect(matchDestination(path)?.key).toBe(entry.key);
      expect(matchDestination(path + '/extra')).toBeNull();
    });
  }
  it('rejects malformed resources and unknown paths', () => {
    for (const path of [
      '/transacoes/wrong',
      '/gestao/conferencia/../../x',
      '/gestao/unknown',
      '/relatorios/financeiro-mensal/0000/10',
      '/relatorios/fechamento-mensal/2026/13',
    ])
      expect(matchDestination(path)).toBeNull();
  });
  it('preserves shared mode and activates owner mode', () => {
    expect(destinationMode(matchDestination('/transacoes')!, 'Admin', 'shift')).toBe('shift');
    expect(destinationMode(matchDestination('/relatorios/dashboard')!, 'Admin', 'shift')).toBe(
      'management',
    );
    expect(destinationMode(matchDestination('/transacoes')!, 'Member', 'management')).toBe('shift');
    expect(destinationHref('month-close', branch, date)).toBe(
      `/f/${branch}/relatorios/fechamento-mensal/2026/10`,
    );
  });
});
describe('untrusted tab restoration', () => {
  it('restores linked manager shared mode without granting authority', () => {
    expect(validateNavigation(state, branch, 'Manager', true)).toEqual(state);
    expect(validateNavigation(state, branch, 'Manager', false)).toBeNull();
    expect(validateNavigation(state, other, 'Manager', true)).toBeNull();
    expect(validateNavigation({ ...state, mode: 'management' }, branch, 'Member', true)).toBeNull();
  });
  it('rejects secrets, foreign origins, stale resource origins and malformed scroll/focus', () => {
    for (const change of [
      { href: state.href + '?password=secret' },
      { href: 'https://evil.invalid' },
      { origins: [{ href: `/f/${other}/transacoes`, label: 'Other' }] },
      { origins: [{ href: `/f/${branch}/transacoes/${other}`, label: 'Unverified resource' }] },
      { scroll: Infinity },
      { scroll: -1 },
      { focus: 'x" onclick="bad' },
      { mode: 'superuser' },
      { origins: new Array(9).fill({ href: state.href, label: 'loop' }) },
    ])
      expect(validateNavigation({ ...state, ...change }, branch, 'Admin', true)).toBeNull();
  });
  it('never restores a removed role destination', () => {
    expect(
      validateNavigation({ ...state, href: `/f/${branch}/gestao/fila` }, branch, 'Member', true),
    ).toBeNull();
  });
});
