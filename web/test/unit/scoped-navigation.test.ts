// @vitest-environment jsdom
import { describe, expect, it } from 'vitest';
import { prepareOriginNavigation, rememberNavigation } from '@/lib/scoped-navigation';
import { navigationKey } from '@/lib/navigation-state';
const branch = '12345678-1234-1234-1234-123456789abc';
const scope = {
  identity: 'person@example.invalid',
  branchId: branch,
  role: 'Manager' as const,
  linked: true,
};
const source = {
  href: `/f/${branch}/transacoes/nova`,
  mode: 'shift' as const,
  origins: [],
  scroll: 50,
  focus: 'conteudo',
};
describe('future blocker return integration', () => {
  it('retains a complete parent chain without touching or saving isolated input', () => {
    const original = '{"v":1,"data":{"notes":"typed before blocker","count":"123"}}';
    localStorage.setItem('loto:draft:v1:hash:' + branch + ':close', original);
    const open = prepareOriginNavigation(
      scope,
      source,
      `/f/${branch}/fechamentos/abrir?conta=123&data=2026-10-28`,
    )!;
    expect(open.origins).toEqual([{ href: source.href, label: 'Nova transação', mode: 'shift' }]);
    expect(rememberNavigation(sessionStorage, scope, open)).toBe(true);
    const nested = prepareOriginNavigation(scope, open, `/f/${branch}/transacoes?status=Draft`)!;
    expect(nested.origins.map((origin) => origin.href)).toEqual([source.href, open.href]);
    expect(localStorage.getItem('loto:draft:v1:hash:' + branch + ':close')).toBe(original);
    expect(sessionStorage.getItem(navigationKey)).not.toContain('typed before blocker');
    expect(sessionStorage.getItem(navigationKey)).not.toContain('count');
  });
  it('rejects another branch and stale authority without preparing a return', () => {
    expect(
      prepareOriginNavigation(
        { ...scope, linked: false },
        source,
        `/f/${branch}/fechamentos/abrir`,
      ),
    ).toBeNull();
    expect(
      prepareOriginNavigation(scope, source, '/f/12345678-1234-1234-1234-123456789abd/transacoes'),
    ).toBeNull();
    expect(
      rememberNavigation(sessionStorage, scope, { ...source, href: source.href + '?etag=old' }),
    ).toBe(false);
  });
});
