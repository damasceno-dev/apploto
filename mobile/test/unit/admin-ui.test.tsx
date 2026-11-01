import { act, configure, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { useEffect as mockEffect } from 'react';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { ToastProvider } from '../../src/ui';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig, response, deferred, userId, branchId } from './support';
import { renderThemed } from './ui/support';

let mockPath = '/clientes';
let mockParams: Record<string, string> = {};
const mockRouter = {
  replace: jest.fn(),
  push: jest.fn(),
  back: jest.fn(),
  dismissTo: jest.fn(),
  setParams: jest.fn((values: Record<string, string>) => {
    mockParams = { ...mockParams, ...values };
  }),
  canGoBack: () => true,
  canDismiss: () => false,
};
jest.mock('expo-router', () => ({
  useIsFocused: () => true,
  usePathname: () => mockPath,
  useGlobalSearchParams: () => mockParams,
  useLocalSearchParams: () => mockParams,
  useRouter: () => mockRouter,
  useFocusEffect: (cb: () => void | (() => void)) => mockEffect(cb, [cb]),
}));
let mockRevision = 0;
jest.mock('../../src/storage/native', () => ({
  ...jest.requireActual('../../src/storage/native'),
  secureUuid: () => `40000000-0000-4000-8000-${String(++mockRevision).padStart(12, '0')}`,
  emailPartitionKey: async (email: string) => 'hash:' + email,
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));
// Some shared-route entries pass through a quiet session re-verification before mounting.
configure({ asyncUtilTimeout: 2000 });

const id = (n: number) => `50000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const client = (n: number, name: string, cpf: string | null = null) => ({
  id: id(n),
  name,
  phone: '61912345678',
  cpf,
  branchId,
});
const otherUser = '10000000-0000-4000-8000-000000000002';
const thirdUser = '10000000-0000-4000-8000-000000000003';
const members = [
  {
    id: 'm1',
    userId,
    name: 'Pessoa de teste',
    email: 'fixture@example.invalid',
    role: 'Admin',
    active: true,
  },
  {
    id: 'm2',
    userId: otherUser,
    name: 'Bia',
    email: 'bia@example.invalid',
    role: 'Member',
    active: true,
  },
  {
    id: 'm3',
    userId: thirdUser,
    name: 'Caio',
    email: 'caio@example.invalid',
    role: 'Member',
    active: true,
  },
];
const product = (n: number, name: string, displayOrder: number) => ({
  id: id(n),
  name,
  displayOrder,
  branchId,
  createdAt: '2026-10-01T00:00:00Z',
  active: true,
});
const setting = (version: number, dailyTargetHours = 8) => ({
  id: id(90),
  version,
  lockDate: '0001-01-01T00:00:00',
  dailyTargetHours,
  lunchDeductionOver6H: 1,
  lunchDeductionOver4H: 0.25,
  branchId,
  createdAt: '2026-01-01T00:00:00Z',
  active: true,
});
const envelope = (items: unknown[]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: items.length ? 1 : 0,
  hasNext: false,
  hasPrevious: false,
});

beforeEach(async () => {
  mockPath = '/clientes';
  mockParams = {};
  jest.clearAllMocks();
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
});
function mount(r: ReturnType<typeof rig>) {
  return renderThemed(
    <ToastProvider>
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          <MobileScreen />
        </ShellProvider>
      </SessionProvider>
    </ToastProvider>,
  );
}
type Handler = (
  path: string,
  init: RequestInit,
  url: URL,
) => Promise<Response | undefined> | Response | undefined;
function serve(r: ReturnType<typeof rig>, handler: Handler, role = 'Admin', tab = false) {
  r.intercept(async (path, raw) => {
    const init = { ...raw, method: raw.method ?? 'GET' };
    if (path === '/branch/my-branches')
      return response({ branches: [{ id: branchId, name: 'Filial', role }] });
    if (path === '/operator/self-context')
      return response({
        operatorId: userId,
        availableAccounts: tab
          ? [
              {
                id: 'x',
                operatorId: userId,
                accountId: id(70),
                accountName: 'Fiado',
                accountType: 'Tab',
                isPrimary: false,
              },
            ]
          : [],
        primaryAccount: null,
      });
    const last = r.calls.at(-1)!;
    return handler(path, init, new URL(last.url));
  });
}
/** Polls a synchronous query, letting React flush between attempts (session re-verification). */
async function until<T>(probe: () => T, ms = 4000): Promise<T> {
  const end = Date.now() + ms;
  for (;;) {
    try {
      return probe();
    } catch (error) {
      if (Date.now() > end) throw error;
      await act(async () => new Promise((resolve) => setTimeout(resolve, 300)));
    }
  }
}
/** Mounts and lets the guarded entry finish verification and its first reads inside act. */
async function open(r: ReturnType<typeof rig>) {
  const view = mount(r);
  await until(() => {
    if (screen.queryAllByText(/^(Verificando|Abrindo|Carregando)/).length)
      throw new Error('loading');
  });
  return view;
}
const writes = (r: ReturnType<typeof rig>, path: string, method?: string) =>
  r.calls.filter((c) => c.path === path && (method ? c.method === method : c.method !== 'GET'));

describe('role, mode and direct-entry guards', () => {
  it.each(['/gestao/operadores', '/gestao/produtos', '/gestao/feriados', '/gestao/configuracoes'])(
    'Member direct entry to %s is refused before any admin read',
    async (path) => {
      mockPath = path;
      const r = rig();
      r.role('Member');
      await r.ready();
      serve(r, () => undefined, 'Member');
      mount(r);
      expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
      expect(
        r.calls.some((c) =>
          ['/operator', '/product', '/holiday', '/setting', '/branch/users'].includes(c.path),
        ),
      ).toBe(false);
    },
  );
  it.each(['Manager', 'Admin'])(
    '%s opens each management admin surface with real reads',
    async (role) => {
      const r = rig();
      r.role(role);
      await r.ready();
      serve(
        r,
        (path) =>
          path === '/operator'
            ? response({ operators: [] })
            : path === '/branch/users'
              ? response({ branchUsers: members })
              : path === '/product'
                ? response({ items: [] })
                : path === '/holiday'
                  ? response(envelope([]))
                  : path === '/setting'
                    ? response(setting(3), 200, { etag: '"3"' })
                    : undefined,
        role,
      );
      for (const [path, text] of [
        ['/gestao/operadores', 'Nenhum operador ativo.'],
        ['/gestao/produtos', 'Nenhum produto cadastrado.'],
        ['/gestao/feriados', 'Nenhum feriado em 2026.'],
        ['/gestao/configuracoes', 'Nenhum mês bloqueado'],
      ] as const) {
        mockPath = path;
        const view = mount(r);
        expect(await screen.findByText(text)).toBeTruthy();
        view.unmount();
      }
    },
  );
  it('a role revocation re-guards the admin surface without new reads', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    serve(r, (path) =>
      path === '/product' ? response({ items: [product(1, 'Telesena', 10)] }) : undefined,
    );
    await open(r);
    await screen.findByText('1. Telesena');
    r.role('Member');
    await act(async () => {
      await r.engine.refresh({ selfContext: true });
    });
    const before = r.calls.filter((c) => c.path === '/product').length;
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    expect(r.calls.filter((c) => c.path === '/product').length).toBe(before);
  });
});

describe('Clients', () => {
  it('Member creates a client in Meu turno with CPF digits and no deactivate action', async () => {
    const r = rig();
    r.role('Member');
    await r.ready();
    const created = client(3, 'Nova Cliente', '52998224725');
    serve(
      r,
      (path, init) =>
        path === '/client'
          ? init.method === 'POST'
            ? response(created, 201)
            : response({ clients: [client(1, 'Ana'), client(2, 'Bruno')] })
          : undefined,
      'Member',
    );
    await open(r);
    await screen.findByText('Ana');
    expect(screen.queryByText('Desativar Ana')).toBeNull();
    expect(screen.getByText(/O extrato de fiado exige uma conta fiado/)).toBeTruthy();
    expect(screen.queryByText('Ver extrato de Ana')).toBeNull();
    fireEvent.press(screen.getByTestId('client-new'));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Nova Cliente');
    fireEvent.changeText(screen.getByTestId('client-phone'), '61912345678');
    fireEvent.changeText(screen.getByTestId('client-cpf'), '52998224725');
    expect(screen.getByTestId('client-cpf').props.value).toBe('529.982.247-25');
    await act(async () => {
      fireEvent.press(screen.getByTestId('client-save'));
      fireEvent.press(screen.getByTestId('client-save'));
    });
    await waitFor(() => expect(writes(r, '/client', 'POST')).toHaveLength(1));
    expect(writes(r, '/client', 'POST')[0]!.body).toEqual({
      name: 'Nova Cliente',
      phone: '(61) 91234-5678',
      cpf: '52998224725',
      cep: null,
      address: null,
      phoneSecondary: null,
      email: null,
      notes: null,
    });
    expect(await screen.findByText('Cliente Nova Cliente salvo no servidor.')).toBeTruthy();
  });
  it('local search filters the complete list and states the result count', async () => {
    const r = rig();
    await r.ready();
    serve(r, (path) =>
      path === '/client'
        ? response({ clients: [client(1, 'José'), client(2, 'Josefa'), client(3, 'Maria')] })
        : undefined,
    );
    await open(r);
    await screen.findByText('3 clientes');
    fireEvent.changeText(screen.getByTestId('client-search'), 'jose');
    expect(screen.getByTestId('client-count').props.children).toBe('2 clientes encontrados');
    expect(screen.queryByText('Maria')).toBeNull();
    expect(r.calls.filter((c) => c.path === '/client')).toHaveLength(1);
  });
  it('Manager deactivates only after confirmation; Member Tab holder sees the statement link', async () => {
    const r = rig();
    r.role('Manager');
    await r.ready();
    serve(
      r,
      (path, init) =>
        path === '/client'
          ? response({ clients: [client(1, 'Ana')] })
          : path === `/client/${id(1)}` && init.method === 'DELETE'
            ? response(client(1, 'Ana'))
            : undefined,
      'Manager',
    );
    await open(r);
    fireEvent.press(await until(() => screen.getByTestId(`client-deactivate-${id(1)}`)));
    expect(writes(r, `/client/${id(1)}`)).toHaveLength(0);
    await act(async () => fireEvent.press(screen.getByText('Desativar cliente')));
    await waitFor(() => expect(writes(r, `/client/${id(1)}`, 'DELETE')).toHaveLength(1));
  });
  it('a confirmed deactivation keeps its receipt and recovery when the slow reread fails', async () => {
    const r = rig();
    r.role('Manager');
    await r.ready();
    const slow = deferred<Response>();
    let lists = 0;
    serve(
      r,
      (path, init) =>
        path === '/client'
          ? ++lists === 1
            ? response({ clients: [client(1, 'Ana')] })
            : slow.promise
          : path === `/client/${id(1)}` && init.method === 'DELETE'
            ? response(client(1, 'Ana'))
            : undefined,
      'Manager',
    );
    await open(r);
    fireEvent.press(await until(() => screen.getByTestId(`client-deactivate-${id(1)}`)));
    await act(async () => fireEvent.press(screen.getByText('Desativar cliente')));
    await waitFor(() => expect(writes(r, `/client/${id(1)}`, 'DELETE')).toHaveLength(1));
    // Still open (busy) while the reread is pending.
    expect(screen.getAllByText('Desativar Ana')).toHaveLength(2);
    expect(screen.getByText('Desativando…')).toBeTruthy();
    await act(async () =>
      slow.resolve(response({ errorMessages: ['Leitura indisponível.'] }, 503)),
    );
    expect(
      await screen.findByText(
        'Cliente desativado no servidor. A conferência local não foi concluída.',
      ),
    ).toBeTruthy();
    fireEvent.press(screen.getByText('Desativar cliente'));
    expect(writes(r, `/client/${id(1)}`, 'DELETE')).toHaveLength(1);
    expect(screen.getByText('Reler dados para conferir')).toBeTruthy();
  });
  it('a known success followed by a reread failure keeps the receipt and never resends', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    serve(r, (path, init) => {
      if (path === '/client' && init.method === 'GET')
        return ++lists === 1
          ? response({ clients: [client(1, 'Ana')] })
          : response({ errorMessages: ['Leitura indisponível.'] }, 503);
      if (path === `/client/${id(1)}` && init.method === 'PUT')
        return response(client(1, 'Ana Maria'));
      return undefined;
    });
    await open(r);
    fireEvent.press(await screen.findByTestId(`client-edit-${id(1)}`));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Ana Maria');
    await act(async () => fireEvent.press(screen.getByTestId('client-save')));
    expect(
      await screen.findByText('Cliente salvo no servidor. A conferência local não foi concluída.'),
    ).toBeTruthy();
    expect(screen.queryByText('Não foi possível salvar.')).toBeNull();
    fireEvent.press(screen.getByTestId('client-save'));
    expect(writes(r, `/client/${id(1)}`, 'PUT')).toHaveLength(1);
  });
  it('an unknown create outcome blocks saving until an explicit reread shows the current list', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    serve(r, (path, init) => {
      if (path === '/client' && init.method === 'POST')
        return response({ errorMessages: ['Tempo esgotado.'] }, 504);
      if (path === '/client')
        return response({
          clients: ++lists > 1 ? [client(1, 'Ana'), client(5, 'Nova')] : [client(1, 'Ana')],
        });
      return undefined;
    });
    await open(r);
    await screen.findByText('Ana');
    fireEvent.press(screen.getByTestId('client-new'));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Nova');
    fireEvent.changeText(screen.getByTestId('client-phone'), '61912345678');
    await act(async () => fireEvent.press(screen.getByTestId('client-save')));
    await screen.findByText('Reler dados para conferir');
    expect(screen.getByTestId('client-save').props.accessibilityState.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler dados para conferir')));
    expect(
      await screen.findByText('A lista atual já tem cliente com este nome ou CPF.'),
    ).toBeTruthy();
    expect(writes(r, '/client', 'POST')).toHaveLength(1);
  });
  it('a CPF conflict renders verbatim and keeps the typed input', async () => {
    const r = rig();
    await r.ready();
    serve(r, (path, init) =>
      path === '/client'
        ? init.method === 'POST'
          ? response(
              { errorMessages: ['Já existe um cliente ativo com este CPF nesta filial'] },
              409,
            )
          : response({ clients: [] })
        : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId('client-new'));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Duplicada');
    fireEvent.changeText(screen.getByTestId('client-phone'), '6133334444');
    fireEvent.changeText(screen.getByTestId('client-cpf'), '52998224725');
    await act(async () => fireEvent.press(screen.getByTestId('client-save')));
    expect(
      await screen.findByText('Já existe um cliente ativo com este CPF nesta filial'),
    ).toBeTruthy();
    expect(screen.getByTestId('client-name').props.value).toBe('Duplicada');
    expect(screen.getByTestId('client-save').props.accessibilityState.disabled).toBe(false);
  });
  it('retained editor input is offered after a fresh check and restoring never submits', async () => {
    const r = rig();
    await r.ready();
    serve(r, (path) => (path === '/client' ? response({ clients: [] }) : undefined));
    const first = mount(r);
    fireEvent.press(await screen.findByTestId('client-new'));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Guardada');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    first.unmount();
    await open(r);
    fireEvent.press(await screen.findByTestId('client-new'));
    {
      const target = await screen.findByText('Restaurar para revisão');
      await act(async () => fireEvent.press(target));
    }
    expect(screen.getByTestId('client-name').props.value).toBe('Guardada');
    expect(writes(r, '/client', 'POST')).toHaveLength(0);
  });
  it('an explicit cancel removes the unsent device copy', async () => {
    const r = rig();
    await r.ready();
    serve(r, (path) => (path === '/client' ? response({ clients: [] }) : undefined));
    const first = await open(r);
    fireEvent.press(await screen.findByTestId('client-new'));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Abandonado');
    await act(async () => fireEvent.press(screen.getByText('Cancelar')));
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    first.unmount();
    await open(r);
    fireEvent.press(await screen.findByTestId('client-new'));
    await screen.findByTestId('client-name');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 50)));
    expect(screen.queryByText('Restaurar para revisão')).toBeNull();
  });
  it('the statement route is guarded for a Member without a Tab grant before any statement read', async () => {
    mockPath = `/clientes/${id(1)}/extrato`;
    const r = rig();
    r.role('Member');
    await r.ready();
    serve(r, () => undefined, 'Member');
    await open(r);
    expect(await screen.findByText('Extrato de fiado indisponível para seu perfil.')).toBeTruthy();
    expect(r.calls.some((c) => c.path.includes('fiado-statement'))).toBe(false);
  });
  it('a Member with a Tab grant reaches the statement destination', async () => {
    const r = rig();
    r.role('Member');
    await r.ready();
    serve(
      r,
      (path) => (path === '/client' ? response({ clients: [client(1, 'Ana')] }) : undefined),
      'Member',
      true,
    );
    await open(r);
    fireEvent.press(await screen.findByText('Ver extrato de Ana'));
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(mockRouter.push.mock.calls.at(-1)![0]).toMatchObject({
      pathname: '/clientes/[id]/extrato',
      params: { id: id(1) },
    });
  });
});

describe('renewal and retained work', () => {
  it('an open client editor survives same-authority renewal without dispatch or replay', async () => {
    const r = rig();
    await r.ready();
    const entered = deferred<void>();
    const release = deferred<void>();
    let holding = false;
    r.intercept(async (path) => {
      if (path === '/user/renew-token' && holding) {
        entered.resolve();
        await release.promise;
        return undefined;
      }
      if (path === '/branch/my-branches')
        return response({ branches: [{ id: branchId, name: 'Filial', role: 'Admin' }] });
      if (path === '/client') return response({ clients: [] });
      return undefined;
    });
    await open(r);
    fireEvent.press(await until(() => screen.getByTestId('client-new')));
    fireEvent.changeText(await screen.findByTestId('client-name'), 'Durante renovação');
    holding = true;
    let refreshing: Promise<void> | undefined;
    await act(async () => {
      r.expire();
      refreshing = r.engine.refresh();
      await entered.promise;
    });
    expect(r.engine.getSnapshot().status).toBe('recovering');
    expect(screen.getByTestId('time-session-pause')).toBeTruthy();
    expect(screen.queryByTestId('client-name')).toBeNull();
    await act(async () => {
      release.resolve();
      await refreshing;
    });
    holding = false;
    expect((await screen.findByTestId('client-name')).props.value).toBe('Durante renovação');
    expect(writes(r, '/client', 'POST')).toHaveLength(0);
  });
});

describe('Operators', () => {
  const operator = (n: number, name: string, user: string | null) => ({
    id: n === 0 ? userId : id(n),
    name,
    branchId,
    userId: user,
  });
  function operatorServer(r: ReturnType<typeof rig>, extra?: Handler) {
    serve(r, async (path, init, url) => {
      const special = await extra?.(path, init, url);
      if (special) return special;
      if (path === '/operator')
        return response({
          operators: [
            operator(0, 'Eu mesmo', userId),
            operator(1, 'Bia operadora', otherUser),
            operator(2, 'Sem login', null),
          ],
        });
      if (path === '/branch/users') return response({ branchUsers: members });
      return undefined;
    });
  }
  it('shows linked identities, none, and você from the self-context operator id', async () => {
    mockPath = '/gestao/operadores';
    const r = rig();
    await r.ready();
    operatorServer(r);
    await open(r);
    expect(await screen.findByText('você')).toBeTruthy();
    expect(screen.getAllByText('você')).toHaveLength(1);
    expect(screen.getByTestId(`operator-login-${id(1)}`).props.children).toBe(
      'Login: Bia · bia@example.invalid · Membro',
    );
    expect(screen.getByTestId(`operator-login-${id(2)}`).props.children).toBe('Login: Sem login');
  });
  it('a link conflict renders verbatim, keeps input and sends only contracted fields', async () => {
    mockPath = '/gestao/operadores';
    const r = rig();
    await r.ready();
    operatorServer(r, (path, init) =>
      path === `/operator/${id(2)}` && init.method === 'PUT'
        ? response(
            {
              errorMessages: ['Este usuário já está vinculado a outro operador ativo nesta filial'],
            },
            409,
          )
        : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId(`operator-edit-${id(2)}`));
    fireEvent.press(await screen.findByTestId('operator-login'));
    fireEvent.press(await screen.findByText('Caio · caio@example.invalid · Membro'));
    await act(async () => fireEvent.press(screen.getByTestId('operator-save')));
    expect(
      await screen.findByText('Este usuário já está vinculado a outro operador ativo nesta filial'),
    ).toBeTruthy();
    expect(writes(r, `/operator/${id(2)}`, 'PUT')[0]!.body).toEqual({
      name: 'Sem login',
      userId: thirdUser,
    });
    expect(screen.getByTestId('operator-name').props.value).toBe('Sem login');
  });
  it('unlinking the caller requires confirmation, sends null and re-reads self-context', async () => {
    mockPath = '/gestao/operadores';
    const r = rig();
    await r.ready();
    operatorServer(r, (path, init) =>
      path === `/operator/${userId}` && init.method === 'PUT'
        ? response({ id: userId, name: 'Eu mesmo', branchId, userId: null })
        : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId(`operator-edit-${userId}`));
    fireEvent.press(await screen.findByTestId('operator-unlink'));
    expect(screen.getAllByText(/Remover o login desfaz somente o vínculo/).length).toBeGreaterThan(
      0,
    );
    fireEvent.press(screen.getByTestId('operator-save'));
    expect(writes(r, `/operator/${userId}`, 'PUT')).toHaveLength(0);
    const before = r.calls.filter((c) => c.path === '/operator/self-context').length;
    {
      const target = await screen.findByText('Remover login e salvar');
      await act(async () => fireEvent.press(target));
    }
    await waitFor(() => expect(writes(r, `/operator/${userId}`, 'PUT')).toHaveLength(1));
    expect(writes(r, `/operator/${userId}`, 'PUT')[0]!.body).toEqual({
      name: 'Eu mesmo',
      userId: null,
    });
    await waitFor(() =>
      expect(r.calls.filter((c) => c.path === '/operator/self-context').length).toBeGreaterThan(
        before,
      ),
    );
    expect(
      await screen.findByText('Seu vínculo de operador nesta filial foi atualizado no servidor.'),
    ).toBeTruthy();
  });
  it('a row opens the real M9 operator summary with the row as its return focus', async () => {
    mockPath = '/gestao/operadores';
    const r = rig();
    await r.ready();
    operatorServer(r);
    await open(r);
    {
      const target = await screen.findByTestId(`operator-summary-${id(1)}`);
      await act(async () => fireEvent.press(target));
    }
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(mockRouter.push.mock.calls.at(-1)![0]).toMatchObject({
      pathname: '/gestao/operadores/[id]/transacoes',
      params: { id: id(1) },
    });
    expect(mockRouter.setParams).toHaveBeenCalledWith({ row: id(1), focus: id(1) });
  });
  it('guided employee flows keep their truthful M11 destinations', async () => {
    mockPath = '/gestao/funcionarios/novo';
    const r = rig();
    await r.ready();
    operatorServer(r);
    await open(r);
    expect(
      await screen.findByText('Esta função estará disponível em uma próxima etapa.'),
    ).toBeTruthy();
    expect(r.calls.some((c) => c.path === '/operator' && c.method === 'POST')).toBe(false);
  });
});

describe('Products', () => {
  const list = [
    product(1, 'Telesena', 10),
    product(2, 'Raspadinha', 20),
    product(3, 'Diferença Caixa', 30),
  ];
  it('the reserved row explains its protection and cannot be deactivated', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    serve(r, (path) => (path === '/product' ? response({ items: list }) : undefined));
    await open(r);
    await screen.findByText('3. Diferença Caixa');
    expect(screen.getByText(/Produto do sistema: o nome é reservado/)).toBeTruthy();
    expect(
      screen.getByTestId(`product-deactivate-${id(3)}`).props.accessibilityState.disabled,
    ).toBe(true);
    expect(screen.getByTestId(`product-up-${id(3)}`).props.accessibilityState.disabled).toBe(false);
  });
  it('a partial reorder reports per-product outcomes and invents no rollback', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    serve(r, (path, init) => {
      if (path === '/product') return response({ items: list });
      if (path === `/product/${id(2)}` && init.method === 'PUT')
        return response({ ...list[1], displayOrder: 10 });
      if (path === `/product/${id(1)}` && init.method === 'PUT')
        return response({ errorMessages: ['Já existe um produto ativo com este nome'] }, 409);
      return undefined;
    });
    await open(r);
    {
      const target = await screen.findByTestId(`product-up-${id(2)}`);
      await act(async () => fireEvent.press(target));
    }
    expect(await screen.findByText('A nova ordem não foi concluída.')).toBeTruthy();
    expect(screen.getByText('Raspadinha: ordem 20 → 10 · salvo')).toBeTruthy();
    expect(screen.getByText('Telesena: ordem 10 → 20 · recusado')).toBeTruthy();
    expect(r.calls.filter((c) => c.method === 'PUT').map((c) => [c.path, c.body])).toEqual([
      [`/product/${id(2)}`, { name: 'Raspadinha', displayOrder: 10 }],
      [`/product/${id(1)}`, { name: 'Telesena', displayOrder: 20 }],
    ]);
  });
  it('an unknown reorder step blocks further steps until the list is reread', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    serve(r, (path, init) => {
      if (path === '/product') return response({ items: list });
      if (init.method === 'PUT') return response({ errorMessages: [] }, 502);
      return undefined;
    });
    await open(r);
    {
      const target = await screen.findByTestId(`product-down-${id(1)}`);
      await act(async () => fireEvent.press(target));
    }
    expect(
      await screen.findByText('Raspadinha: ordem 20 → 10 · resultado não confirmado'),
    ).toBeTruthy();
    expect(screen.getByText('Concluir esta ordem').parent).toBeTruthy();
    fireEvent.press(screen.getByText('Concluir esta ordem'));
    expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getByText('Reler lista de produtos')));
    await act(async () => fireEvent.press(screen.getByText('Concluir esta ordem')));
    await waitFor(() => expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(2));
  });
  it('a list that changed since it was shown sends nothing and requires a reread', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    let reads = 0;
    const added = product(4, 'Novo concorrente', 15);
    serve(r, (path) =>
      path === '/product'
        ? response({ items: ++reads === 1 ? list : [...list, added] })
        : undefined,
    );
    await open(r);
    const up = await until(() => screen.getByTestId(`product-up-${id(2)}`));
    await act(async () => fireEvent.press(up));
    expect(await screen.findByTestId('product-list-changed')).toBeTruthy();
    expect(screen.queryByText('Ordem de contagem salva no servidor.')).toBeNull();
    expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
    expect(screen.getByTestId(`product-up-${id(1)}`).props.accessibilityState.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler lista de produtos')));
    await waitFor(() =>
      expect(screen.getByTestId(`product-up-${id(2)}`).props.accessibilityState.disabled).toBe(
        false,
      ),
    );
    expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
  });
  it('deactivation promises only shipped consequences', async () => {
    mockPath = '/gestao/produtos';
    const r = rig();
    await r.ready();
    serve(r, (path, init) =>
      path === '/product'
        ? response({ items: list })
        : init.method === 'DELETE'
          ? response(null, 204)
          : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId(`product-deactivate-${id(1)}`));
    const text = await screen.findByText(/O produto sai da lista de produtos/);
    expect(text.props.children).not.toMatch(/editáve|reativ.*poss|histórico continua editável/);
    await act(async () => fireEvent.press(screen.getByText('Desativar produto')));
    await waitFor(() => expect(writes(r, `/product/${id(1)}`, 'DELETE')).toHaveLength(1));
  });
});

describe('Holidays', () => {
  const preview = (source: string, alreadyExists = false) => ({
    year: 2026,
    includesOptionalFederal: false,
    source,
    items: [
      {
        date: '2026-11-20',
        description: 'Consciência Negra',
        type: 'National',
        alreadyExists,
        source: 'Canonical',
      },
    ],
  });
  it('defaults the year from the branch date and pages through the server envelope', async () => {
    mockPath = '/gestao/feriados';
    const r = rig();
    r.date('2026-10-31');
    await r.ready();
    serve(r, (path) =>
      path === '/holiday'
        ? response({
            ...envelope([
              {
                id: id(1),
                date: '2026-11-02T00:00:00',
                description: null,
                branchId,
                createdAt: '2026-01-01T00:00:00Z',
                active: true,
              },
            ]),
            totalCount: 25,
            totalPages: 2,
            hasNext: true,
          })
        : undefined,
    );
    await open(r);
    await screen.findByText('25 feriados em 2026');
    const query = new URL(r.calls.find((c) => c.path === '/holiday')!.url).searchParams;
    expect([query.get('Year'), query.get('Page'), query.get('PageSize')]).toEqual([
      '2026',
      '1',
      '20',
    ]);
    expect(screen.getByText('Sem descrição')).toBeTruthy();
    expect(screen.queryByText(/Fonte/)).toBeNull();
  });
  it('a changed option discards the preview; import uses the confirmed options and shows results', async () => {
    mockPath = '/gestao/feriados';
    const r = rig();
    await r.ready();
    const late = deferred<Response>();
    serve(r, (path, init, url) => {
      if (path === '/holiday') return response(envelope([]));
      if (path === '/holiday/import-br/2026/preview')
        return url.searchParams.get('source') === 'BrasilApi'
          ? late.promise
          : response(preview(url.searchParams.get('source')!));
      if (path === '/holiday/import-br/2026' && init.method === 'POST')
        return response({
          ...preview('Canonical'),
          items: [
            {
              date: '2026-11-20',
              description: 'Consciência Negra',
              type: 'National',
              status: 'Imported',
              source: 'Canonical',
            },
          ],
          importedCount: 1,
          skippedCount: 0,
        });
      return undefined;
    });
    await open(r);
    fireEvent.press(await screen.findByTestId('holiday-import'));
    fireEvent.press(await screen.findByTestId('holiday-source-BrasilApi'));
    fireEvent.press(screen.getByTestId('holiday-preview'));
    fireEvent.press(screen.getByTestId('holiday-source-Canonical'));
    await act(async () => late.resolve(response(preview('BrasilApi'))));
    expect(screen.queryByTestId('holiday-preview-list')).toBeNull();
    await act(async () => fireEvent.press(screen.getByTestId('holiday-preview')));
    expect(
      await screen.findByText('2026 · Fonte: Calendário nacional · sem pontos facultativos'),
    ).toBeTruthy();
    fireEvent.press(screen.getByTestId('holiday-optional'));
    expect(screen.queryByTestId('holiday-preview-list')).toBeNull();
    fireEvent.press(screen.getByTestId('holiday-optional'));
    await act(async () => fireEvent.press(screen.getByTestId('holiday-preview')));
    fireEvent.press(await screen.findByTestId('holiday-import-confirm'));
    {
      const target = await screen.findByText('Importar feriados');
      await act(async () => fireEvent.press(target));
    }
    expect(await screen.findByText('1 importados · 0 ignorados')).toBeTruthy();
    const post = r.calls.find((c) => c.method === 'POST' && c.path === '/holiday/import-br/2026')!;
    expect(Object.fromEntries(new URL(post.url).searchParams)).toEqual({
      includeOptionalFederal: 'false',
      source: 'Canonical',
    });
    expect(
      screen.getByText(/Consciência Negra · Nacional · Calendário nacional · Importado/),
    ).toBeTruthy();
  });
  it('a single-source outage renders verbatim and retries only on request', async () => {
    mockPath = '/gestao/feriados';
    const r = rig();
    await r.ready();
    serve(r, (path) =>
      path === '/holiday'
        ? response(envelope([]))
        : path === '/holiday/import-br/2026/preview'
          ? response(
              { errorMessages: ['Não foi possível consultar a fonte externa de feriados'] },
              502,
            )
          : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId('holiday-import'));
    fireEvent.press(await screen.findByTestId('holiday-source-Nager'));
    await act(async () => fireEvent.press(screen.getByTestId('holiday-preview')));
    expect(
      await screen.findByText('Não foi possível consultar a fonte externa de feriados'),
    ).toBeTruthy();
    expect(r.calls.filter((c) => c.path.endsWith('/preview'))).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getByText('Tentar a prévia de novo')));
    expect(r.calls.filter((c) => c.path.endsWith('/preview'))).toHaveLength(2);
  });
  it('manual batch posts every row once and explains the immutable date on edit', async () => {
    mockPath = '/gestao/feriados';
    const r = rig();
    await r.ready();
    const row = {
      id: id(1),
      date: '2026-12-08T00:00:00',
      description: 'Padroeira',
      branchId,
      createdAt: '2026-01-01T00:00:00Z',
      active: true,
    };
    serve(r, (path, init) =>
      path === '/holiday'
        ? init.method === 'POST'
          ? response({ items: [row] }, 201)
          : response(envelope([row]))
        : undefined,
    );
    await open(r);
    fireEvent.press(await screen.findByTestId('holiday-add'));
    const date = await screen.findByTestId('holiday-date');
    fireEvent.changeText(date, '08/12/2026');
    fireEvent(date, 'blur');
    fireEvent.changeText(screen.getByTestId('holiday-description'), 'Padroeira');
    await act(async () => fireEvent.press(screen.getByTestId('holiday-batch-save')));
    await waitFor(() => expect(writes(r, '/holiday', 'POST')).toHaveLength(1));
    expect(writes(r, '/holiday', 'POST')[0]!.body).toEqual({
      holidays: [{ date: '2026-12-08', description: 'Padroeira' }],
    });
    expect(await screen.findByText('1 feriado salvo no servidor.')).toBeTruthy();
    fireEvent.press(screen.getByText('Fechar'));
    fireEvent.press(await screen.findByTestId(`holiday-edit-${id(1)}`));
    expect(await screen.findByText('Para mudar a data, desative e crie de novo.')).toBeTruthy();
  });
});

describe('Settings', () => {
  function settingsServer(
    r: ReturnType<typeof rig>,
    put: (init: RequestInit) => Response | Promise<Response>,
  ) {
    let version = 7;
    serve(r, (path, init) => {
      if (path !== '/setting') return undefined;
      if (init.method === 'PUT') return put(init);
      return response(setting(version), 200, { etag: `"${version}"` });
    });
    return { bump: () => (version += 1) };
  }
  const type = (field: string, hours: string, minutes: string) => {
    fireEvent.changeText(screen.getByTestId(`setting-${field}-hours`), hours);
    fireEvent.changeText(screen.getByTestId(`setting-${field}-minutes`), minutes);
  };
  it('a value-identical form sends nothing and LockDate is read-only', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    await r.ready();
    settingsServer(r, () => response({}, 500));
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('dailyTargetHours', '8', '00');
    expect(screen.getByText('Nenhuma alteração para salvar.')).toBeTruthy();
    expect(screen.getByTestId('setting-save').props.accessibilityState.disabled).toBe(true);
    expect(screen.getByText('Ir para fechamento mensal')).toBeTruthy();
    expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
  });
  it('a target change confirms with branch date and old/new values, then sends strong If-Match', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    r.date('2026-10-31');
    await r.ready();
    settingsServer(r, () => response({ ...setting(8, 7.5) }, 200, { etag: '"8"' }));
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('dailyTargetHours', '7', '30');
    await act(async () => fireEvent.press(screen.getByTestId('setting-save')));
    expect(await screen.findByText('Confirmar alteração de meta?')).toBeTruthy();
    expect(
      screen.getByText(
        'A nova meta de 7h 30min será aplicada a partir de hoje, 31/10/2026. Os dias anteriores deste mês permanecerão calculados sob a meta anterior (8h 00min).',
      ),
    ).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByTestId('setting-confirm')));
    await waitFor(() => expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1));
    const put = r.calls.find((c) => c.method === 'PUT')!;
    expect(put.headers['if-match']).toBe('"7"');
    expect(put.body).toEqual({ dailyTargetHours: '7.50' });
    expect(
      (await screen.findAllByText('Configurações salvas no servidor.')).length,
    ).toBeGreaterThan(0);
    expect(screen.getByText(/Valem a partir de 31\/10\/2026/)).toBeTruthy();
  });
  it('a lunch-only change uses its own confirmation', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    await r.ready();
    settingsServer(r, () => response(setting(8), 200, { etag: '"8"' }));
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('lunchDeductionOver6H', '0', '45');
    await act(async () => fireEvent.press(screen.getByTestId('setting-save')));
    expect(await screen.findByText('Confirmar alteração das regras de jornada?')).toBeTruthy();
    expect(
      screen.getByText(/As novas regras de jornada valem a partir de hoje, 27\/10\/2026/),
    ).toBeTruthy();
  });
  it('a stale ETag keeps the candidate and reconciles explicitly without replay', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    await r.ready();
    let puts = 0;
    const server = settingsServer(r, () =>
      ++puts === 1
        ? response(
            {
              errorMessages: [
                'As configurações foram alteradas por outra operação; recarregue antes de salvar',
              ],
            },
            409,
          )
        : response(setting(9, 7.5), 200, { etag: '"9"' }),
    );
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('dailyTargetHours', '7', '30');
    await act(async () => fireEvent.press(screen.getByTestId('setting-save')));
    {
      const target = await screen.findByTestId('setting-confirm');
      await act(async () => fireEvent.press(target));
    }
    expect(
      await screen.findByText(
        'As configurações foram alteradas por outra operação; recarregue antes de salvar',
      ),
    ).toBeTruthy();
    expect(screen.getByTestId('setting-dailyTargetHours-minutes').props.value).toBe('30');
    expect(screen.getByTestId('setting-save').props.accessibilityState.disabled).toBe(true);
    server.bump();
    await act(async () => fireEvent.press(screen.getByTestId('setting-reload')));
    await screen.findByTestId('setting-fresh');
    expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getByTestId('setting-adopt')));
    await act(async () => fireEvent.press(screen.getByTestId('setting-save')));
    {
      const target = await screen.findByTestId('setting-confirm');
      await act(async () => fireEvent.press(target));
    }
    await waitFor(() => expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(2));
    expect(r.calls.filter((c) => c.method === 'PUT')[1]!.headers['if-match']).toBe('"8"');
  });
  it('a branch-day rollover before dispatch re-confirms the new effective date first', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    r.date('2026-10-31');
    await r.ready();
    settingsServer(r, () => response(setting(8, 7.5), 200, { etag: '"8"' }));
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('dailyTargetHours', '7', '30');
    await act(async () => fireEvent.press(screen.getByTestId('setting-save')));
    await screen.findByText(/a partir de hoje, 31\/10\/2026/);
    r.date('2026-11-01');
    await act(async () => fireEvent.press(screen.getByTestId('setting-confirm')));
    expect(await screen.findByText('A data da filial mudou para 01/11/2026.')).toBeTruthy();
    expect(screen.getByText(/a partir de hoje, 01\/11\/2026/)).toBeTruthy();
    expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
  });
  it('an unsupported precision is refused locally instead of rounded', async () => {
    mockPath = '/gestao/configuracoes';
    const r = rig();
    await r.ready();
    settingsServer(r, () => response({}, 500));
    await open(r);
    await screen.findByText('Nenhum mês bloqueado');
    type('dailyTargetHours', '7', '20');
    expect(screen.getByText(/Use minutos divisíveis por 3/)).toBeTruthy();
    expect(screen.getByTestId('setting-save').props.accessibilityState.disabled).toBe(true);
  });
});
