// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ApplicationFrame } from '@/components/shell/application-frame';
import { installMatchMedia } from './support/format-double';
import { ThemeProvider } from '@/theme/theme-provider';
import type { Role } from '@loto/api';
const fixtures = vi.hoisted(() => ({
  recovering: false,
  dayChanged: false,
  retained: false,
  selfError: false,
  fetching: false,
  noSelf: false,
  role: 'Admin' as Role,
  path: '/f/12345678-1234-1234-1234-123456789abc/gestao/fila',
  self: { operatorId: null as string | null, availableAccounts: [] },
  refresh: vi.fn(),
  refetch: vi.fn(),
}));
vi.mock('next/navigation', () => ({ usePathname: () => fixtures.path }));
vi.mock('@/client/storage/retained-input', () => ({
  hasScopedRetainedInput: async () => fixtures.retained,
}));
vi.mock('@/client/session/session-provider', () => ({
  beforeGateEvent: 'before-gate',
  useSession: () => ({
    snapshot: {
      scope: 'identity.branch',
      state: 'branch',
      user: { userId: 'user-ana', name: 'Ana', email: 'ana@example.invalid' },
      branch: {
        id: '12345678-1234-1234-1234-123456789abc',
        name: 'Filial Alfa',
        role: fixtures.role,
      },
    },
    refresh: fixtures.refresh,
    recovering: fixtures.recovering,
  }),
}));
vi.mock('@/client/query/scoped-query-provider', () => ({ useScopedApiClient: () => ({}) }));
vi.mock('@loto/api/queries', () => ({
  useGetBranchMyBranches: () => ({ data: undefined, isError: false, refetch: vi.fn() }),
  useGetOperatorSelfContext: () => ({
    data: fixtures.noSelf ? undefined : { data: fixtures.self },
    isError: fixtures.selfError,
    error: new Error('offline'),
    isFetching: fixtures.fetching,
    refetch: fixtures.refetch,
  }),
}));
vi.mock('@/client/clock/use-branch-clock', () => ({
  useBranchClock: () => ({
    state: {
      status: 'ready',
      clock: {
        liveRole: fixtures.role,
        branchLocalDate: '2026-10-28',
        branchLocalDateTime: '2026-10-28T10:15:00',
      },
      dayChanged: fixtures.dayChanged,
    },
    refresh: fixtures.refresh,
  }),
}));
vi.mock('@/components/shell/account-menu', () => ({
  AccountMenu: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
}));
vi.mock('@/components/ui/menu', () => ({
  MenuGroup: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  MenuRadioItem: ({ label }: { label: string }) => <span>{label}</span>,
  MenuUnavailable: ({ reason }: { reason: string }) => <p>{reason}</p>,
}));
beforeEach(() => {
  installMatchMedia(false);
  fixtures.role = 'Admin';
  fixtures.fetching = false;
  fixtures.noSelf = false;
  fixtures.recovering = false;
  fixtures.dayChanged = false;
  fixtures.retained = false;
  fixtures.selfError = false;
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
  fixtures.path = '/f/12345678-1234-1234-1234-123456789abc/gestao/fila';
  fixtures.self = { operatorId: null, availableAccounts: [] };
  window.scrollTo = vi.fn();
  fixtures.refresh.mockResolvedValue(undefined);
  fixtures.refetch.mockResolvedValue({ isError: false, data: { data: fixtures.self } });
});
afterEach(() => {
  cleanup();
  sessionStorage.clear();
  vi.clearAllMocks();
});
function frame() {
  return render(
    <ThemeProvider initial="light">
      <ApplicationFrame
        initialRole={fixtures.role}
        initialSelf={fixtures.self}
        initialDate="2026-10-28"
        multiple
      />
    </ThemeProvider>,
  );
}
describe('live application frame', () => {
  it('renders all nine management reports, server-owned time, identity context and skip link', async () => {
    frame();
    expect(screen.getByRole('link', { name: 'Pular para o conteúdo' }).getAttribute('href')).toBe(
      '#conteudo',
    );
    for (const label of [
      'Dashboard',
      'Movimento da conta',
      'Saldo fiado',
      'Fiado por idade',
      'Cheques em aberto',
      'A receber',
      'Diferenças de caixa',
      'Fechamento mensal',
      'Relatório financeiro mensal',
    ])
      expect(screen.getByRole('link', { name: label })).toBeTruthy();
    expect(screen.getAllByText(/28\/10\/2026.*10:15/).length).toBeGreaterThan(0);
    expect(
      screen.getByRole('link', { name: 'Fila de trabalho' }).getAttribute('aria-current'),
    ).toBe('page');
    expect(screen.queryByRole('link', { name: 'Ponto' })).toBeNull();
    await waitFor(() =>
      expect(screen.getByRole('heading', { name: 'Fila de trabalho' })).toBeTruthy(),
    );
  });
  it('shows Member-only clock and setup guidance without an elevated mode group', () => {
    fixtures.role = 'Member';
    fixtures.path = fixtures.path.replace('gestao/fila', 'turno/hoje');
    frame();
    expect(screen.getByRole('link', { name: 'Ponto' })).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Dashboard' })).toBeNull();
    expect(screen.getByText('Seu login ainda não está vinculado a um operador.')).toBeTruthy();
    expect(screen.queryByText('Modo de trabalho')).toBeNull();
  });
  it('shows the complete unlinked-manager explanation with reachable setup destinations', () => {
    fixtures.role = 'Manager';
    fixtures.path = fixtures.path.replace('gestao/fila', 'turno/hoje');
    frame();
    expect(screen.getByText('Meu turno não está disponível para você nesta filial.')).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Ir para Operadores' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Contas dos operadores' })).toBeTruthy();
    expect(screen.getByRole('link', { name: 'Voltar para a fila de trabalho' })).toBeTruthy();
    expect(fixtures.refetch).toHaveBeenCalled();
  });
});

it('prioritizes recovery/day notices and collapses the remaining offline/retained input notices', async () => {
  fixtures.recovering = true;
  fixtures.dayChanged = true;
  fixtures.retained = true;
  frame();
  Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
  fireEvent(window, new Event('offline'));
  const expand = await screen.findByRole('button', { name: '+2 avisos' });
  expect(screen.getByText('Sua sessão está sendo renovada…')).toBeTruthy();
  expect(
    screen.getByText('A data da filial mudou. Confira os dados antes de continuar.'),
  ).toBeTruthy();
  expect(screen.queryByText('Há dados guardados neste dispositivo.')).toBeNull();
  fireEvent.click(expand);
  expect(screen.getByText('Há dados guardados neste dispositivo.')).toBeTruthy();
  expect(screen.getByText('Sem conexão atualizada com a filial')).toBeTruthy();
});
it('does not turn a failed self-context read into a missing-link explanation', () => {
  fixtures.role = 'Manager';
  fixtures.selfError = true;
  fixtures.noSelf = true;
  fixtures.path = fixtures.path.replace('gestao/fila', 'turno/hoje');
  frame();
  expect(screen.getByText('Não foi possível verificar seu vínculo')).toBeTruthy();
  expect(screen.queryByText('Meu turno não está disponível para você nesta filial.')).toBeNull();
});

it('keeps an unlinked shift-owned setup page stable while self-context refetches', () => {
  fixtures.role = 'Manager';
  fixtures.fetching = true;
  fixtures.path = fixtures.path.replace('gestao/fila', 'turno/hoje');
  frame();
  expect(screen.getByText('Meu turno não está disponível para você nesta filial.')).toBeTruthy();
  expect(screen.queryByText('Esta função estará disponível em uma próxima etapa.')).toBeNull();
});
for (const rest of [
  'transacoes',
  'fechamentos/abrir',
  'fechamentos/12345678-1234-1234-1234-123456789abc',
  'fechamentos/12345678-1234-1234-1234-123456789abc/correcao',
]) {
  it(`falls back to management after link loss on shared /${rest}`, async () => {
    fixtures.role = 'Manager';
    fixtures.self = { operatorId: 'linked', availableAccounts: [] };
    const target = fixtures.path.replace('gestao/fila', rest);
    fixtures.path = fixtures.path.replace('gestao/fila', 'turno/hoje');
    const view = frame();
    fixtures.path = target;
    view.rerender(
      <ThemeProvider initial="light">
        <ApplicationFrame
          initialRole="Manager"
          initialSelf={fixtures.self}
          initialDate="2026-10-28"
          multiple
        />
      </ThemeProvider>,
    );
    await waitFor(() =>
      expect(screen.getAllByText(/Gerente · Meu turno/).length).toBeGreaterThan(0),
    );
    fixtures.self = { operatorId: null, availableAccounts: [] };
    view.rerender(
      <ThemeProvider initial="light">
        <ApplicationFrame
          initialRole="Manager"
          initialSelf={fixtures.self}
          initialDate="2026-10-28"
          multiple
        />
      </ThemeProvider>,
    );
    expect(screen.getAllByText(/Gerente · Gestão/).length).toBeGreaterThan(0);
    expect(screen.queryByText('Meu turno não está disponível para você nesta filial.')).toBeNull();
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeTruthy();
    fixtures.self = { operatorId: 'linked-again', availableAccounts: [] };
    view.rerender(
      <ThemeProvider initial="light">
        <ApplicationFrame
          initialRole="Manager"
          initialSelf={fixtures.self}
          initialDate="2026-10-28"
          multiple
        />
      </ThemeProvider>,
    );
    expect(screen.getAllByText(/Gerente · Gestão/).length).toBeGreaterThan(0);
  });
}
