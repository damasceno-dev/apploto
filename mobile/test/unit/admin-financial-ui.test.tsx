import { act, configure, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { useEffect as mockEffect } from 'react';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { ToastProvider } from '../../src/ui';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig, response, deferred, userId, branchId } from './support';
import { renderThemed } from './ui/support';

let mockPath = '/gestao/contas';
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
configure({ asyncUtilTimeout: 2000 });

const id = (n: number) => `70000000-0000-4000-8000-${String(n).padStart(12, '0')}`;
const account = (n: number, type: string, extra: Record<string, unknown> = {}) => ({
  id: id(n),
  type,
  name: `Conta ${n}`,
  branchId,
  ...extra,
});
const caixa = account(1, 'Terminal', { name: 'Caixa 1', tabAccountId: id(2), terminalId: id(80) });
const tab = account(2, 'Tab', { name: 'Fiado 1', terminalAccountId: id(1) });
const freeTab = account(3, 'Tab', { name: 'Fiado livre', terminalAccountId: null });
const lone = account(4, 'Terminal', { name: 'Caixa 2', tabAccountId: null, terminalId: id(81) });
const safe = account(5, 'Safe', { name: 'Cofre', institution: 'Interno' });
const accounts = [caixa, tab, freeTab, lone, safe];
const terminals = [
  { id: id(80), name: 'CAIXA 01', externalReference: 'T-01', active: true },
  { id: id(81), name: 'CAIXA 02', externalReference: null, active: true },
];
const operator = (n: number, name: string, linked: string | null = null) => ({
  id: id(n),
  name,
  branchId,
  userId: linked,
});
const link = (operatorId: string, a: ReturnType<typeof account>, primary = false) => ({
  id: `link-${a.id}`,
  operatorId,
  accountId: a.id,
  isPrimary: primary,
  accountType: a.type,
  accountName: a.name,
  accountInstitution: (a as { institution?: string }).institution ?? null,
  accountNumber: null,
  pairedTabAccountId: (a as { tabAccountId?: string | null }).tabAccountId ?? null,
});

beforeEach(async () => {
  mockPath = '/gestao/contas';
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
function serve(
  r: ReturnType<typeof rig>,
  handler: Handler,
  role = 'Admin',
  // Matches the rig's login baseline, so no test sees an unintended access change.
  self: { operatorId: string | null; accounts?: unknown[] } = { operatorId: userId },
) {
  r.intercept(async (path, raw) => {
    const init = { ...raw, method: raw.method ?? 'GET' };
    if (path === '/branch/my-branches')
      return response({ branches: [{ id: branchId, name: 'Filial', role }] });
    if (path === '/operator/self-context')
      return response({
        operatorId: self.operatorId,
        availableAccounts: self.accounts ?? [],
        primaryAccount: null,
      });
    const last = r.calls.at(-1)!;
    return handler(path, init, new URL(last.url));
  });
}
async function until<T>(probe: () => T, ms = 4000): Promise<T> {
  const end = Date.now() + ms;
  for (;;) {
    try {
      return probe();
    } catch (error) {
      if (Date.now() > end) throw error;
      await act(async () => new Promise((resolve) => setTimeout(resolve, 50)));
    }
  }
}
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
const reads = (r: ReturnType<typeof rig>, path: string) =>
  r.calls.filter((c) => c.path === path && c.method === 'GET').length;
/** Presses once the target exists and is enabled (actions stay disabled until their reads land). */
const press = async (testID: string) => {
  const target = await until(() => {
    const node = screen.getByTestId(testID);
    if (node.props.accessibilityState?.disabled) throw new Error(`${testID} disabled`);
    return node;
  });
  await act(async () => fireEvent.press(target));
};
/** Opens a category group of the types list (collapsed by default). */
const group = async (name: string) => {
  const header = await until(() => screen.getByText(name));
  await act(async () => fireEvent.press(header));
};
const choose = async (select: string, value: string) => {
  await press(select);
  await press(`${select}-option-${value}`);
};
const accountServer = (r: ReturnType<typeof rig>, extra?: Handler, role = 'Admin') =>
  serve(
    r,
    async (path, init, url) => {
      const special = await extra?.(path, init, url);
      if (special) return special;
      if (path === '/account' && init.method === 'GET') return response({ accounts });
      if (path === '/terminal' && init.method === 'GET') return response(terminals);
      return undefined;
    },
    role,
  );

describe('guards for the four remaining surfaces', () => {
  it.each([
    '/gestao/contas',
    '/gestao/contas-dos-operadores',
    '/gestao/categorias',
    '/gestao/membros',
  ])('Member direct entry to %s is refused before any admin read', async (path) => {
    mockPath = path;
    const r = rig();
    r.role('Member');
    await r.ready();
    serve(r, () => undefined, 'Member');
    mount(r);
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    expect(
      r.calls.some((c) =>
        [
          '/account',
          '/terminal',
          '/operator',
          '/category',
          '/transaction-type',
          '/branch/users',
          '/invitation',
        ].includes(c.path),
      ),
    ).toBe(false);
  });
  it.each(['Manager', 'Admin'])(
    '%s opens each surface with real reads and empty states',
    async (role) => {
      const r = rig();
      r.role(role);
      await r.ready();
      serve(
        r,
        (path) =>
          path === '/account'
            ? response({ accounts: [] })
            : path === '/terminal'
              ? response([])
              : path === '/operator'
                ? response({ operators: [] })
                : path === '/category'
                  ? response({ items: [] })
                  : path === '/transaction-type'
                    ? response({ items: [] })
                    : path === '/branch/users'
                      ? response({
                          branchUsers: [
                            {
                              id: id(90),
                              userId,
                              name: 'Eu',
                              email: 'e@x.invalid',
                              role,
                              active: true,
                            },
                          ],
                        })
                      : undefined,
        role,
      );
      for (const [path, text] of [
        ['/gestao/contas', 'Nenhuma conta ativa.'],
        ['/gestao/contas-dos-operadores', 'Escolha um operador para ver e mudar as contas dele.'],
        ['/gestao/categorias', 'Nenhuma categoria ativa.'],
        ['/gestao/membros', 'Nenhum outro membro nesta filial.'],
      ] as const) {
        mockPath = path;
        const view = mount(r);
        expect(await screen.findByText(text)).toBeTruthy();
        view.unmount();
      }
    },
  );
  it('a role revocation tears down the accounts surface without new reads', async () => {
    const r = rig();
    await r.ready();
    accountServer(r);
    await open(r);
    await screen.findByText('Caixa 1');
    r.role('Member');
    await act(async () => {
      await r.engine.refresh({ selfContext: true });
    });
    const before = reads(r, '/account');
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    expect(screen.queryByText('Caixa 1')).toBeNull();
    expect(reads(r, '/account')).toBe(before);
  });
});

describe('Accounts', () => {
  it('lists type, terminal CAIXA, pairing and the Safe explanation', async () => {
    const r = rig();
    await r.ready();
    accountServer(r);
    await open(r);
    expect(await screen.findByText('Pareado com Fiado 1')).toBeTruthy();
    expect(screen.getByTestId(`account-terminal-text-${caixa.id}`).props.children).toBe(
      'Terminal CAIXA: CAIXA 01 (T-01)',
    );
    expect(screen.getByText('Pareada com o caixa Caixa 1')).toBeTruthy();
    expect(screen.getByText('Sem conta fiado')).toBeTruthy();
    expect(screen.getByTestId(`account-type-${safe.id}`).props.children).toBe('Tipo: Cofre');
    expect(screen.getAllByText(/O cofre não tem terminal CAIXA/).length).toBeGreaterThan(0);
    // Pairing actions follow the actual pairing state.
    expect(screen.queryByTestId(`account-pair-${caixa.id}`)).toBeNull();
    expect(screen.getByTestId(`account-unpair-${caixa.id}`)).toBeTruthy();
    expect(screen.getByTestId(`account-pair-${lone.id}`)).toBeTruthy();
    expect(screen.queryByTestId(`account-terminal-${safe.id}`)).toBeNull();
  });
  it('a new caixa pairs a new Tab by default, once, and closes after the reread', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === '/account/terminal' && init.method === 'POST'
        ? response({ ...account(9, 'Terminal', { name: 'Caixa 3', tabAccountId: id(10) }) }, 201)
        : undefined,
    );
    await open(r);
    await press('account-new-Terminal');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Caixa 3');
    await act(async () => {
      fireEvent.press(screen.getByTestId('account-save'));
      fireEvent.press(screen.getByTestId('account-save'));
    });
    await waitFor(() => expect(writes(r, '/account/terminal', 'POST')).toHaveLength(1));
    expect(writes(r, '/account/terminal', 'POST')[0]!.body).toEqual({
      name: 'Caixa 3',
      institution: null,
      number: null,
      createTabAccount: true,
    });
    await waitFor(() => expect(screen.queryByTestId('account-editor')).toBeNull());
    expect(reads(r, '/account')).toBeGreaterThanOrEqual(2);
  });
  it('Sem conta fiado sends false; an existing Tab and terminal CAIXA are sent as chosen', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === '/account/terminal' && init.method === 'POST'
        ? response(account(9, 'Terminal'), 201)
        : undefined,
    );
    await open(r);
    await press('account-new-Terminal');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Sem fiado');
    await press('account-pairing-none');
    expect(screen.getAllByText(/não pode registrar vendas fiado no balcão/).length).toBeGreaterThan(
      0,
    );
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    await waitFor(() => expect(writes(r, '/account/terminal', 'POST')).toHaveLength(1));
    expect(writes(r, '/account/terminal', 'POST')[0]!.body).toMatchObject({
      createTabAccount: false,
    });
    await waitFor(() => expect(screen.queryByTestId('account-editor')).toBeNull());
    await press('account-new-Terminal');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Com fiado');
    await press('account-pairing-existing');
    await choose('account-existing-tab', freeTab.id);
    await choose('account-terminal-id', id(81));
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    await waitFor(() => expect(writes(r, '/account/terminal', 'POST')).toHaveLength(2));
    const body = writes(r, '/account/terminal', 'POST')[1]!.body as Record<string, unknown>;
    expect(body).toEqual({
      name: 'Com fiado',
      institution: null,
      number: null,
      existingTabAccountId: freeTab.id,
      terminalId: id(81),
    });
  });
  it('a Safe is created through its own route with no pairing fields', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === '/account/safe' && init.method === 'POST'
        ? response(account(9, 'Safe', { name: 'Cofre 2' }), 201)
        : undefined,
    );
    await open(r);
    await press('account-new-Safe');
    expect(screen.queryByTestId('account-pairing')).toBeNull();
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Cofre 2');
    fireEvent.changeText(screen.getByTestId('account-institution'), 'Banco X');
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    await waitFor(() => expect(writes(r, '/account/safe', 'POST')).toHaveLength(1));
    expect(writes(r, '/account/safe', 'POST')[0]!.body).toEqual({
      name: 'Cofre 2',
      institution: 'Banco X',
      number: null,
    });
    expect(writes(r, '/account/terminal')).toHaveLength(0);
  });
  it('an unknown create outcome is never resent; the reread lists possible matches', async () => {
    const r = rig();
    await r.ready();
    let list = accounts;
    accountServer(r, (path, init) => {
      if (path === '/account/bank' && init.method === 'POST') {
        list = [...accounts, account(9, 'BankAccount', { name: 'Banco novo' })];
        return response({ errorMessages: [] }, 502);
      }
      if (path === '/account' && init.method === 'GET') return response({ accounts: list });
      return undefined;
    });
    await open(r);
    await press('account-new-BankAccount');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Banco novo');
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    expect(await screen.findByText('Resultado não confirmado')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    expect(writes(r, '/account/bank', 'POST')).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getByText('Reler contas para conferir')));
    expect(
      await screen.findByText('Contas ativas com este nome: Banco novo (Conta bancária).'),
    ).toBeTruthy();
    expect(screen.getByTestId('account-name').props.value).toBe('Banco novo');
    expect(writes(r, '/account/bank', 'POST')).toHaveLength(1);
  });
  it('a confirmed create keeps its receipt when the reread fails', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    accountServer(r, (path, init) => {
      if (path === '/account' && init.method === 'GET')
        return ++lists === 1
          ? response({ accounts })
          : response({ errorMessages: ['Leitura indisponível.'] }, 503);
      if (path === '/account/bank' && init.method === 'POST')
        return response(account(9, 'BankAccount'), 201);
      return undefined;
    });
    await open(r);
    await press('account-new-BankAccount');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Banco');
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    expect(
      await screen.findByText('Conta criada no servidor. A conferência local não foi concluída.'),
    ).toBeTruthy();
    expect(screen.getByTestId('account-editor')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    expect(writes(r, '/account/bank', 'POST')).toHaveLength(1);
  });
  it('recovering a confirmed create never re-enables it and closes after a good reread', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    accountServer(r, (path, init) => {
      if (path === '/account' && init.method === 'GET')
        // Initial read, then the follow-up and the first recovery fail; the second recovery succeeds.
        return ++lists === 2 || lists === 3
          ? response({ errorMessages: ['Leitura indisponível.'] }, 503)
          : response({ accounts });
      if (path === '/account/bank' && init.method === 'POST')
        return response(account(9, 'BankAccount'), 201);
      return undefined;
    });
    await open(r);
    await press('account-new-BankAccount');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Banco');
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    const receipt = 'Conta criada no servidor. A conferência local não foi concluída.';
    expect(await screen.findByText(receipt)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler contas para conferir')));
    await until(() => expect(lists).toBe(3));
    expect(screen.getByText(receipt)).toBeTruthy();
    expect(screen.getByTestId('account-save').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler contas para conferir')));
    await waitFor(() => expect(screen.queryByTestId('account-editor')).toBeNull());
    expect(screen.queryByTestId('account-reconciled')).toBeNull();
    expect(writes(r, '/account/bank', 'POST')).toHaveLength(1);
  });
  it('a confirmed paired-dialog command closes on recovery instead of becoming sendable', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    accountServer(r, (path, init) => {
      if (path === '/account' && init.method === 'GET')
        return ++lists === 2
          ? response({ errorMessages: ['Leitura indisponível.'] }, 503)
          : response({ accounts });
      if (path === '/account/tab' && init.method === 'POST')
        return response(account(12, 'Tab', { terminalAccountId: lone.id }), 201);
      return undefined;
    });
    await open(r);
    await press(`account-tab-${lone.id}`);
    await act(async () => fireEvent.press(screen.getByText('Criar conta fiado')));
    expect(await screen.findByText(/A conferência local não foi concluída/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler contas e conferir')));
    await waitFor(() => expect(screen.queryByTestId('account-command-tab')).toBeNull());
    expect(writes(r, '/account/tab', 'POST')).toHaveLength(1);
  });
  it('a pairing conflict renders verbatim and blocks until a fresh read', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === '/account/pair-tab' && init.method === 'POST'
        ? response({ errorMessages: ['A conta fiado já está vinculada a outro terminal.'] }, 409)
        : undefined,
    );
    await open(r);
    await press(`account-pair-${lone.id}`);
    expect(screen.getAllByText(/Parear não dá acesso a ninguém/).length).toBeGreaterThan(0);
    await choose('account-pair-choice', freeTab.id);
    await act(async () => fireEvent.press(screen.getByText('Parear')));
    expect(
      await screen.findByText('A conta fiado já está vinculada a outro terminal.'),
    ).toBeTruthy();
    expect(writes(r, '/account/pair-tab', 'POST')[0]!.body).toEqual({
      terminalAccountId: lone.id,
      tabAccountId: freeTab.id,
    });
    const before = reads(r, '/account');
    await act(async () => fireEvent.press(screen.getByText('Reler contas e conferir')));
    await waitFor(() => expect(reads(r, '/account')).toBeGreaterThan(before));
    expect(writes(r, '/account/pair-tab', 'POST')).toHaveLength(1);
    expect(await screen.findByText('Parear')).toBeTruthy();
  });
  it('coordination busy is verbatim and a retry is explicit after rereading', async () => {
    const r = rig();
    await r.ready();
    let tries = 0;
    accountServer(r, (path, init) =>
      path === `/account/terminal/${caixa.id}/tab` && init.method === 'DELETE'
        ? ++tries === 1
          ? response({ errorMessages: ['Outra alteração de contas está em andamento.'] }, 409)
          : response({ ...caixa, tabAccountId: null })
        : undefined,
    );
    await open(r);
    await press(`account-unpair-${caixa.id}`);
    expect(screen.getByText(/Fiado 1 continua ativa, com o saldo e o histórico/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Desfazer pareamento')));
    expect(await screen.findByText('Outra alteração de contas está em andamento.')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler contas e conferir')));
    await act(async () => fireEvent.press(await screen.findByText('Desfazer pareamento')));
    await waitFor(() =>
      expect(writes(r, `/account/terminal/${caixa.id}/tab`, 'DELETE')).toHaveLength(2),
    );
  });
  it('creating a Tab for an unpaired caixa states that it grants nothing', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === '/account/tab' && init.method === 'POST'
        ? response(account(12, 'Tab', { terminalAccountId: lone.id }), 201)
        : undefined,
    );
    await open(r);
    await press(`account-tab-${lone.id}`);
    expect(screen.getByText(/Uma conta fiado nova nasce pareada a Caixa 2/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Criar conta fiado')));
    await waitFor(() => expect(writes(r, '/account/tab', 'POST')).toHaveLength(1));
    expect(writes(r, '/account/tab', 'POST')[0]!.body).toEqual({ terminalAccountId: lone.id });
  });
  it('deactivation states the pairing release and renders the active-custody refusal verbatim', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === `/account/${caixa.id}` && init.method === 'DELETE'
        ? response(
            { errorMessages: ['O caixa tem um turno aberto ou uma passagem pendente.'] },
            409,
          )
        : undefined,
    );
    await open(r);
    await press(`account-deactivate-${caixa.id}`);
    expect(
      screen.getByText(/A conta fiado Fiado 1 é liberada do pareamento na mesma ação/),
    ).toBeTruthy();
    expect(screen.getByText(/o servidor recusa a desativação/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Desativar conta')));
    expect(
      await screen.findByText('O caixa tem um turno aberto ou uma passagem pendente.'),
    ).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler contas e conferir')));
    expect(writes(r, `/account/${caixa.id}`, 'DELETE')).toHaveLength(1);
  });
  it('terminal CAIXA reassignment uses the contracted route and shows refusals verbatim', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === `/terminal/cashbox/${caixa.id}` && init.method === 'PUT'
        ? response(
            { errorMessages: ['O caixa tem custódia ativa; troque o terminal depois.'] },
            409,
          )
        : undefined,
    );
    await open(r);
    await press(`account-terminal-${caixa.id}`);
    expect(screen.getByText('Terminal atual: CAIXA 01 (T-01)')).toBeTruthy();
    await choose('account-terminal-choice', id(81));
    await act(async () => fireEvent.press(screen.getByText('Trocar terminal')));
    expect(
      await screen.findByText('O caixa tem custódia ativa; troque o terminal depois.'),
    ).toBeTruthy();
    expect(writes(r, `/terminal/cashbox/${caixa.id}`, 'PUT')[0]!.body).toEqual({
      terminalId: id(81),
    });
  });
  it('editing offers no type control and sends the descriptive fields', async () => {
    const r = rig();
    await r.ready();
    accountServer(r, (path, init) =>
      path === `/account/${safe.id}` && init.method === 'PUT'
        ? response({ ...safe, institution: null })
        : undefined,
    );
    await open(r);
    await press(`account-edit-${safe.id}`);
    expect(screen.getByTestId('account-type-readonly').props.children).toBe(
      'Tipo: Cofre (não muda depois de criada)',
    );
    fireEvent.changeText(screen.getByTestId('account-institution'), '');
    await act(async () => fireEvent.press(screen.getByTestId('account-save')));
    await waitFor(() => expect(writes(r, `/account/${safe.id}`, 'PUT')).toHaveLength(1));
    expect(writes(r, `/account/${safe.id}`, 'PUT')[0]!.body).toEqual({
      name: 'Cofre',
      institution: null,
      number: null,
    });
  });
  it('the detail is a fresh resource read; a missing account is explained', async () => {
    mockParams = { accountId: lone.id };
    const r = rig();
    await r.ready();
    accountServer(r, (path) =>
      path === `/account/${lone.id}`
        ? response({ errorMessages: ['Conta não encontrada.'] }, 404)
        : undefined,
    );
    await open(r);
    expect(await screen.findByText('Esta conta não está disponível nesta filial.')).toBeTruthy();
    expect(screen.getByText('Conta não encontrada.')).toBeTruthy();
  });
});

describe('Account assignment', () => {
  const ops = [operator(30, 'Ana', userId), operator(31, 'Beto')];
  const assignmentServer = (
    r: ReturnType<typeof rig>,
    held: Record<string, ReturnType<typeof link>[]>,
    extra?: Handler,
    self: { operatorId: string | null } = { operatorId: userId },
  ) =>
    serve(
      r,
      async (path, init, url) => {
        const special = await extra?.(path, init, url);
        if (special) return special;
        if (path === '/operator' && init.method === 'GET') return response({ operators: ops });
        if (path === '/account' && init.method === 'GET') return response({ accounts });
        const match = /^\/operator\/([^/]+)\/accounts$/.exec(path);
        if (match && init.method === 'GET')
          return response({ operatorAccounts: held[match[1]!] ?? [] });
        return undefined;
      },
      'Admin',
      self,
    );
  it('an unknown operator id loads nothing; empty grants say so', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(99) };
    const r = rig();
    await r.ready();
    assignmentServer(r, {});
    const view = await open(r);
    expect(await screen.findByTestId('assignment-operator-missing')).toBeTruthy();
    expect(r.calls.some((c) => c.path === `/operator/${id(99)}/accounts`)).toBe(false);
    view.unmount();
    mockParams = { operatorId: id(31) };
    await open(r);
    expect(
      await screen.findByText(
        'Este operador ainda não tem contas. Sem um terminal atribuído, ele não consegue registrar transações.',
      ),
    ).toBeTruthy();
  });
  it('assigning a paired caixa does not grant its Tab; the Tab is a separate explicit command', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(31) };
    const r = rig();
    await r.ready();
    const held: Record<string, ReturnType<typeof link>[]> = { [id(31)]: [] };
    assignmentServer(r, held, (path, init) => {
      if (path === `/operator/${id(31)}/accounts` && init.method === 'POST') {
        const body = JSON.parse(String(init.body)) as { accountId: string };
        const a = accounts.find((x) => x.id === body.accountId)!;
        held[id(31)] = [...held[id(31)]!, link(id(31), a, held[id(31)]!.length === 0)];
        return response(held[id(31)]!.at(-1));
      }
      return undefined;
    });
    await open(r);
    await press('assignment-add');
    await choose('assignment-account-choice', caixa.id);
    expect(screen.getByTestId('assignment-tab-separate')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Atribuir conta')));
    await waitFor(() => expect(writes(r, `/operator/${id(31)}/accounts`, 'POST')).toHaveLength(1));
    expect(writes(r, `/operator/${id(31)}/accounts`, 'POST')[0]!.body).toEqual({
      accountId: caixa.id,
    });
    expect(
      await screen.findByText('Conta fiado pareada: Fiado 1 · não atribuída a este operador'),
    ).toBeTruthy();
    await press(`assignment-add-tab-${caixa.id}`);
    await act(async () => fireEvent.press(screen.getByText('Atribuir conta')));
    await waitFor(() => expect(writes(r, `/operator/${id(31)}/accounts`, 'POST')).toHaveLength(2));
    expect(writes(r, `/operator/${id(31)}/accounts`, 'POST')[1]!.body).toEqual({
      accountId: tab.id,
    });
    expect(
      await screen.findByText('Conta fiado pareada: Fiado 1 · também atribuída a este operador'),
    ).toBeTruthy();
  });
  it('set primary and remove are explicit commands on the exact grant', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(31) };
    const r = rig();
    await r.ready();
    const held = { [id(31)]: [link(id(31), caixa, true), link(id(31), safe)] };
    assignmentServer(r, held, (path, init) => {
      if (path === `/operator/${id(31)}/accounts/${safe.id}/primary` && init.method === 'PUT')
        return response({ ...held[id(31)]![1], isPrimary: true });
      if (path === `/operator/${id(31)}/accounts/${safe.id}` && init.method === 'DELETE')
        return response(held[id(31)]![1]);
      return undefined;
    });
    await open(r);
    expect(await screen.findByText('Principal')).toBeTruthy();
    await press(`assignment-primary-${safe.id}`);
    await act(async () => fireEvent.press(screen.getByText('Definir como principal')));
    await waitFor(() =>
      expect(writes(r, `/operator/${id(31)}/accounts/${safe.id}/primary`, 'PUT')).toHaveLength(1),
    );
    await waitFor(() => expect(screen.queryByTestId('assignment-command-primary')).toBeNull());
    await press(`assignment-remove-${safe.id}`);
    expect(writes(r, `/operator/${id(31)}/accounts/${safe.id}`, 'DELETE')).toHaveLength(0);
    await act(async () => fireEvent.press(screen.getByText('Remover conta')));
    await waitFor(() =>
      expect(writes(r, `/operator/${id(31)}/accounts/${safe.id}`, 'DELETE')).toHaveLength(1),
    );
  });
  it('an already-active grant renders verbatim and blocks until reread', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(31) };
    const r = rig();
    await r.ready();
    assignmentServer(r, {}, (path, init) =>
      path === `/operator/${id(31)}/accounts` && init.method === 'POST'
        ? response({ errorMessages: ['A conta já está atribuída a este operador.'] }, 409)
        : undefined,
    );
    await open(r);
    await press('assignment-add');
    await choose('assignment-account-choice', safe.id);
    await act(async () => fireEvent.press(screen.getByText('Atribuir conta')));
    expect(await screen.findByText('A conta já está atribuída a este operador.')).toBeTruthy();
    expect(screen.getByText('Reler e conferir')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler e conferir')));
    expect(writes(r, `/operator/${id(31)}/accounts`, 'POST')).toHaveLength(1);
  });
  it('a grant to the caller’s own operator rechecks self-context and keeps the receipt', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(30) };
    const r = rig();
    await r.ready();
    const held: Record<string, ReturnType<typeof link>[]> = { [id(30)]: [] };
    let granted = false;
    r.intercept(async (path, raw) => {
      const init = { ...raw, method: raw.method ?? 'GET' };
      if (path === '/branch/my-branches')
        return response({ branches: [{ id: branchId, name: 'Filial', role: 'Admin' }] });
      if (path === '/operator/self-context')
        return response({
          operatorId: id(30),
          availableAccounts: granted ? [link(id(30), caixa, true)] : [],
          primaryAccount: null,
        });
      if (path === '/operator' && init.method === 'GET') return response({ operators: ops });
      if (path === '/account' && init.method === 'GET') return response({ accounts });
      if (path === `/operator/${id(30)}/accounts` && init.method === 'GET')
        return response({ operatorAccounts: held[id(30)] });
      if (path === `/operator/${id(30)}/accounts` && init.method === 'POST') {
        granted = true;
        held[id(30)] = [link(id(30), caixa, true)];
        return response(held[id(30)]![0]);
      }
      return undefined;
    });
    await act(async () => {
      await r.engine.refresh({ selfContext: true });
    });
    await open(r);
    expect(await screen.findByText('Contas de Ana')).toBeTruthy();
    const selfReads = reads(r, '/operator/self-context');
    await press('assignment-add');
    await choose('assignment-account-choice', caixa.id);
    await act(async () => fireEvent.press(screen.getByText('Atribuir conta')));
    expect(await screen.findByTestId('assignment-self-receipt')).toBeTruthy();
    expect(reads(r, '/operator/self-context')).toBeGreaterThan(selfReads);
    expect(writes(r, `/operator/${id(30)}/accounts`, 'POST')).toHaveLength(1);
  });
  it('a row opens the account detail with the assignment as its validated origin', async () => {
    mockPath = '/gestao/contas-dos-operadores';
    mockParams = { operatorId: id(31) };
    const r = rig();
    await r.ready();
    assignmentServer(r, { [id(31)]: [link(id(31), safe, true)] });
    await open(r);
    await press(`assignment-account-${safe.id}`);
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(mockRouter.push.mock.calls.at(-1)![0]).toMatchObject({
      pathname: '/gestao/contas',
      params: { accountId: safe.id },
    });
    expect(mockRouter.setParams).toHaveBeenCalledWith({ row: safe.id, focus: safe.id });
  });
});

describe('Categories and transaction types', () => {
  const categories = [
    {
      id: id(40),
      name: 'Recebimentos',
      defaultDirection: 'In',
      branchId,
      createdAt: 'x',
      active: true,
    },
    {
      id: id(41),
      name: 'Despesas',
      defaultDirection: 'Out',
      branchId,
      createdAt: 'x',
      active: true,
    },
    {
      id: id(42),
      name: 'Automáticos',
      defaultDirection: 'Out',
      branchId,
      createdAt: 'x',
      active: true,
    },
  ];
  const type = (n: number, extra: Record<string, unknown> = {}) => ({
    id: id(n),
    name: `Tipo ${n}`,
    settlementRule: 'SameDay',
    requiresTabAccountAndClient: false,
    fiadoPaymentKind: null,
    settlementInstrument: 'None',
    monthlyFinancialRole: 'None',
    financialClass: 'OperatingExpenseAndLoss',
    systemRole: null,
    categoryId: id(41),
    categoryName: 'Despesas',
    createdAt: 'x',
    active: true,
    ...extra,
  });
  const types = [
    type(50, {
      name: 'Dinheiro',
      categoryId: id(40),
      categoryName: 'Recebimentos',
      requiresTabAccountAndClient: true,
      fiadoPaymentKind: 'CountedCash',
    }),
    type(51, { name: 'Aluguel' }),
    type(52, {
      name: 'Saída de caixa — venda fiado',
      categoryId: id(42),
      categoryName: 'Automáticos',
      systemRole: 'FiadoSaleCounterpart',
    }),
  ];
  const typeServer = (r: ReturnType<typeof rig>, extra?: Handler) =>
    serve(r, async (path, init, url) => {
      const special = await extra?.(path, init, url);
      if (special) return special;
      if (path === '/category' && init.method === 'GET') return response({ items: categories });
      if (path === '/transaction-type' && init.method === 'GET') return response({ items: types });
      return undefined;
    });
  beforeEach(() => {
    mockPath = '/gestao/categorias';
  });
  it('a new category has no default direction; rename sends the name only', async () => {
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === '/category' && init.method === 'POST'
        ? response({ ...categories[0], id: id(43), name: 'Nova' }, 201)
        : path === `/category/${id(41)}` && init.method === 'PUT'
          ? response({ ...categories[1], name: 'Gastos' })
          : undefined,
    );
    await open(r);
    await press('category-new');
    fireEvent.changeText(await screen.findByTestId('category-name'), 'Nova');
    expect(screen.getByTestId('category-save').props.accessibilityState?.disabled).toBe(true);
    await press('category-direction-Out');
    await act(async () => fireEvent.press(screen.getByTestId('category-save')));
    await waitFor(() => expect(writes(r, '/category', 'POST')).toHaveLength(1));
    expect(writes(r, '/category', 'POST')[0]!.body).toEqual({
      name: 'Nova',
      defaultDirection: 'Out',
    });
    await waitFor(() => expect(screen.queryByTestId('category-editor')).toBeNull());
    await press(`category-edit-${id(41)}`);
    expect(screen.getByTestId('category-direction-readonly')).toBeTruthy();
    fireEvent.changeText(screen.getByTestId('category-name'), 'Gastos');
    await act(async () => fireEvent.press(screen.getByTestId('category-save')));
    await waitFor(() => expect(writes(r, `/category/${id(41)}`, 'PUT')).toHaveLength(1));
    expect(writes(r, `/category/${id(41)}`, 'PUT')[0]!.body).toEqual({ name: 'Gastos' });
  });
  it('recovering a confirmed category create never re-enables it', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    typeServer(r, (path, init) => {
      if (path === '/category' && init.method === 'GET')
        return ++lists === 2 || lists === 3
          ? response({ errorMessages: ['Leitura indisponível.'] }, 503)
          : response({ items: categories });
      if (path === '/category' && init.method === 'POST')
        return response({ ...categories[0], id: id(43), name: 'Nova' }, 201);
      return undefined;
    });
    await open(r);
    await press('category-new');
    fireEvent.changeText(await screen.findByTestId('category-name'), 'Nova');
    await press('category-direction-Out');
    await act(async () => fireEvent.press(screen.getByTestId('category-save')));
    expect(await screen.findByText(/A conferência local não foi concluída/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler dados para conferir')));
    await until(() => expect(lists).toBe(3));
    expect(screen.getByTestId('category-save').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler dados para conferir')));
    await waitFor(() => expect(screen.queryByTestId('category-editor')).toBeNull());
    expect(writes(r, '/category', 'POST')).toHaveLength(1);
  });
  it('a category with system types cannot be deactivated; others state the cascade', async () => {
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === `/category/${id(41)}` && init.method === 'DELETE' ? response(null, 204) : undefined,
    );
    await open(r);
    expect(await screen.findByTestId(`category-system-${id(42)}`)).toBeTruthy();
    expect(
      screen.getByTestId(`category-deactivate-${id(42)}`).props.accessibilityState?.disabled,
    ).toBe(true);
    await press(`category-deactivate-${id(41)}`);
    expect(screen.getByText(/todos os tipos ativos dela saem das listas/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Desativar categoria')));
    await waitFor(() => expect(writes(r, `/category/${id(41)}`, 'DELETE')).toHaveLength(1));
  });
  it('system types are read-only with their source; others offer edit and deactivate', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r);
    await open(r);
    await group('Automáticos');
    await group('Despesas');
    await group('Recebimentos');
    expect(await screen.findByTestId(`type-system-${id(52)}`)).toBeTruthy();
    expect(screen.getByText('Automática')).toBeTruthy();
    expect(screen.getByText(/Escrito pela venda fiado no balcão/)).toBeTruthy();
    expect(screen.queryByTestId(`type-edit-${id(52)}`)).toBeNull();
    expect(screen.queryByTestId(`type-deactivate-${id(52)}`)).toBeNull();
    expect(screen.getByTestId(`type-edit-${id(51)}`)).toBeTruthy();
    expect(screen.getByTestId(`type-fiado-${id(50)}`).props.children).toBe(
      'Forma de recebimento de fiado: Dinheiro (entra no caixa)',
    );
  });
  it('types are grouped by category, collapsed, with the focused type’s group open', async () => {
    mockParams = { tab: 'tipos', focus: id(51) };
    const r = rig();
    await r.ready();
    typeServer(r);
    await open(r);
    expect(await until(() => screen.getByTestId(`type-edit-${id(51)}`))).toBeTruthy();
    expect(screen.queryByTestId(`type-row-${id(50)}`)).toBeNull();
    expect(screen.getByLabelText('Recebimentos, 1 tipo · Entrada')).toBeTruthy();
    await group('Despesas');
    expect(screen.queryByTestId(`type-edit-${id(51)}`)).toBeNull();
  });
  it('create excludes system categories and needs an explicit repayment answer', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === '/transaction-type' && init.method === 'POST'
        ? response(type(53, { name: 'Cartão fiado' }), 201)
        : undefined,
    );
    await open(r);
    await press('type-new');
    await press('type-category');
    expect(screen.queryByTestId(`type-category-option-${id(42)}`)).toBeNull();
    await press(`type-category-option-${id(40)}`);
    fireEvent.changeText(screen.getByTestId('type-name'), 'Cartão fiado');
    await choose('type-settlement', 'NextBusinessDay');
    await act(async () => fireEvent(screen.getByTestId('type-requires-tab'), 'valueChange', true));
    expect(screen.getByTestId('type-save').props.accessibilityState?.disabled).toBe(true);
    expect(screen.getByText('Diga se este tipo é uma forma de recebimento de fiado.')).toBeTruthy();
    await press('type-fiado-NonCash');
    await choose('type-instrument', 'CreditCard');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    await waitFor(() => expect(writes(r, '/transaction-type', 'POST')).toHaveLength(1));
    expect(writes(r, '/transaction-type', 'POST')[0]!.body).toEqual({
      categoryId: id(40),
      name: 'Cartão fiado',
      settlementRule: 'NextBusinessDay',
      requiresTabAccountAndClient: true,
      fiadoPaymentKind: 'NonCash',
      settlementInstrument: 'CreditCard',
      monthlyFinancialRole: 'None',
      financialClass: 'Unclassified',
    });
  });
  it('recovering a confirmed type create never re-enables it', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    let lists = 0;
    typeServer(r, (path, init) => {
      if (path === '/transaction-type' && init.method === 'GET')
        return ++lists === 2 || lists === 3
          ? response({ errorMessages: ['Leitura indisponível.'] }, 503)
          : response({ items: types });
      if (path === '/transaction-type' && init.method === 'POST')
        return response(type(53, { name: 'Taxa' }), 201);
      return undefined;
    });
    await open(r);
    await press('type-new');
    await choose('type-category', id(41));
    fireEvent.changeText(screen.getByTestId('type-name'), 'Taxa');
    await choose('type-settlement', 'SameDay');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    expect(await screen.findByText(/A conferência local não foi concluída/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler dados para conferir')));
    await until(() => expect(lists).toBe(3));
    expect(screen.getByTestId('type-save').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler dados para conferir')));
    await waitFor(() => expect(screen.queryByTestId('type-editor')).toBeNull());
    expect(writes(r, '/transaction-type', 'POST')).toHaveLength(1);
  });
  it('an out-of-direction monthly role is explained and blocks saving', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r);
    await open(r);
    await group('Despesas');
    await press(`type-edit-${id(51)}`);
    await choose('type-monthly', 'BolaoServiceFee');
    expect(
      screen.getAllByText(/Tarifa de bolão e Tarifa especial só valem em categoria de entrada/)
        .length,
    ).toBeGreaterThan(0);
    expect(screen.getByTestId('type-save').props.accessibilityState?.disabled).toBe(true);
  });
  it('a classification edit confirms the future-postings-only policy and sends every field', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === `/transaction-type/${id(51)}`
        ? init.method === 'PUT'
          ? response(
              type(51, {
                settlementInstrument: 'None',
                monthlyFinancialRole: 'CommercialLeftover',
              }),
            )
          : response(types[1])
        : undefined,
    );
    await open(r);
    await group('Despesas');
    await press(`type-edit-${id(51)}`);
    await choose('type-monthly', 'CommercialLeftover');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    expect(await screen.findByTestId('type-reclassify-confirm')).toBeTruthy();
    // v62/v63: classifications freeze at posting; saving never re-labels posted or locked rows.
    expect(screen.getAllByText(/vale só para os próximos lançamentos/).length).toBeGreaterThan(0);
    expect(screen.queryByText(/inclusive em meses bloqueados/)).toBeNull();
    expect(screen.getAllByText(/Reclassificar período aberto/).length).toBeGreaterThan(0);
    expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(0);
    await act(async () => fireEvent.press(screen.getByText('Confirmar e salvar')));
    await waitFor(() => expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(1));
    expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')[0]!.body).toEqual({
      name: 'Aluguel',
      settlementRule: 'SameDay',
      requiresTabAccountAndClient: false,
      fiadoPaymentKind: null,
      settlementInstrument: 'None',
      monthlyFinancialRole: 'CommercialLeftover',
      financialClass: 'OperatingExpenseAndLoss',
    });
    // The separate audited reclassification command is never part of a type save.
    expect(r.calls.some((c) => c.path.includes('/reclassif'))).toBe(false);
  });
  it('a financial class change alone asks for the same confirmation and sends the class', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === `/transaction-type/${id(51)}`
        ? init.method === 'PUT'
          ? response(type(51, { name: 'Aluguel', financialClass: 'InternalTransfer' }))
          : response(types[1])
        : undefined,
    );
    await open(r);
    await group('Despesas');
    await press(`type-edit-${id(51)}`);
    await choose('type-financial-class', 'InternalTransfer');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    expect(await screen.findByTestId('type-reclassify-confirm')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Confirmar e salvar')));
    await waitFor(() => expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(1));
    expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')[0]!.body).toMatchObject({
      financialClass: 'InternalTransfer',
    });
  });
  it('a type changed by someone else since the form opened is shown and nothing is sent', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === `/transaction-type/${id(51)}` && init.method === 'GET'
        ? response({ ...types[1], name: 'Aluguel loja' })
        : undefined,
    );
    await open(r);
    await group('Despesas');
    await press(`type-edit-${id(51)}`);
    fireEvent.changeText(screen.getByTestId('type-name'), 'Aluguel sede');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    expect(await screen.findByTestId('type-changed')).toBeTruthy();
    expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(0);
    await press('type-adopt-server');
    expect(screen.getByTestId('type-name').props.value).toBe('Aluguel loja');
  });
  it.each([
    ['adopting the newer server values and then reverting the role', 'type-adopt-server'],
    ['keeping the form values over a newer server role', 'type-keep-mine'],
  ])(
    'the reclassification confirmation compares with the accepted reference: %s',
    async (_, action) => {
      mockParams = { tab: 'tipos' };
      const r = rig();
      await r.ready();
      const newer = type(51, { name: 'Aluguel', monthlyFinancialRole: 'CommercialLeftover' });
      typeServer(r, (path, init) =>
        path === `/transaction-type/${id(51)}`
          ? init.method === 'PUT'
            ? response(type(51, { name: 'Aluguel sede' }))
            : response(newer)
          : undefined,
      );
      await open(r);
      await group('Despesas');
      await press(`type-edit-${id(51)}`);
      fireEvent.changeText(screen.getByTestId('type-name'), 'Aluguel sede');
      await act(async () => fireEvent.press(screen.getByTestId('type-save')));
      expect(await screen.findByTestId('type-changed')).toBeTruthy();
      await press(action);
      if (action === 'type-adopt-server') {
        expect(screen.getByTestId('type-name').props.value).toBe('Aluguel');
        await choose('type-monthly', 'None');
      }
      // The form now says None while the accepted server value is CommercialLeftover.
      await act(async () => fireEvent.press(screen.getByTestId('type-save')));
      expect(await screen.findByTestId('type-reclassify-confirm')).toBeTruthy();
      expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(0);
      await act(async () => fireEvent.press(screen.getByText('Confirmar e salvar')));
      await waitFor(() => expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(1));
      expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')[0]!.body).toMatchObject({
        monthlyFinancialRole: 'None',
      });
    },
  );
  it('an adopted classification left unchanged saves without the reclassification step', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    const newer = type(51, { name: 'Aluguel', monthlyFinancialRole: 'CommercialLeftover' });
    typeServer(r, (path, init) =>
      path === `/transaction-type/${id(51)}`
        ? init.method === 'PUT'
          ? response({ ...newer, name: 'Aluguel sede' })
          : response(newer)
        : undefined,
    );
    await open(r);
    await group('Despesas');
    await press(`type-edit-${id(51)}`);
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    expect(await screen.findByTestId('type-changed')).toBeTruthy();
    await press('type-adopt-server');
    fireEvent.changeText(screen.getByTestId('type-name'), 'Aluguel sede');
    await act(async () => fireEvent.press(screen.getByTestId('type-save')));
    await waitFor(() => expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')).toHaveLength(1));
    expect(screen.queryByTestId('type-reclassify-confirm')).toBeNull();
    expect(writes(r, `/transaction-type/${id(51)}`, 'PUT')[0]!.body).toMatchObject({
      name: 'Aluguel sede',
      monthlyFinancialRole: 'CommercialLeftover',
    });
  });
  it('deactivating a type explains retained history with no reactivation', async () => {
    mockParams = { tab: 'tipos' };
    const r = rig();
    await r.ready();
    typeServer(r, (path, init) =>
      path === `/transaction-type/${id(51)}` && init.method === 'DELETE'
        ? response(null, 204)
        : undefined,
    );
    await open(r);
    await group('Despesas');
    await press(`type-deactivate-${id(51)}`);
    expect(screen.getByText(/Esta tela não oferece reativação/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Desativar tipo')));
    await waitFor(() => expect(writes(r, `/transaction-type/${id(51)}`, 'DELETE')).toHaveLength(1));
  });
});

describe('Branch members and invitations', () => {
  const me = { id: id(60), userId, name: 'Eu', email: 'eu@x.invalid', role: 'Admin', active: true };
  const admin2 = {
    id: id(61),
    userId: id(161),
    name: 'Dora',
    email: 'd@x.invalid',
    role: 'Admin',
    active: true,
  };
  const manager = {
    id: id(62),
    userId: id(162),
    name: 'Gil',
    email: 'g@x.invalid',
    role: 'Manager',
    active: true,
  };
  const member = {
    id: id(63),
    userId: id(163),
    name: 'Rui',
    email: 'r@x.invalid',
    role: 'Member',
    active: true,
  };
  const invitation = (n: number, extra: Record<string, unknown> = {}) => ({
    id: id(n),
    email: `c${n}@x.invalid`,
    role: 'Member',
    operatorId: null,
    status: 'Pending',
    expiresAt: '2026-11-10T15:00:00Z',
    deliveryStatus: 'Sent',
    ...extra,
  });
  const ops = [operator(30, 'Ana', userId), operator(31, 'Beto'), operator(32, 'Caio')];
  const memberServer = (
    r: ReturnType<typeof rig>,
    list: unknown[],
    extra?: Handler,
    role = 'Admin',
    invitations: unknown[] = [],
  ) =>
    serve(
      r,
      async (path, init, url) => {
        const special = await extra?.(path, init, url);
        if (special) return special;
        if (path === '/branch/users' && init.method === 'GET')
          return response({ branchUsers: list });
        if (path === '/operator' && init.method === 'GET') return response({ operators: ops });
        if (path === '/invitation' && init.method === 'GET')
          return response({
            items: invitations,
            totalCount: invitations.length,
            page: 1,
            pageSize: 50,
          });
        return undefined;
      },
      role,
    );
  beforeEach(() => {
    mockPath = '/gestao/membros';
  });
  it('own row carries você with a reason; Remover acesso opens the guided destination only', async () => {
    const r = rig();
    await r.ready();
    memberServer(r, [me, admin2, member]);
    await open(r);
    expect(await screen.findByText('você')).toBeTruthy();
    expect(screen.getByTestId(`member-reason-${me.id}`)).toBeTruthy();
    expect(
      screen.getByTestId(`member-role-change-${me.id}`).props.accessibilityState?.disabled,
    ).toBe(true);
    await press(`member-remove-${member.id}`);
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(mockRouter.push.mock.calls.at(-1)![0]).toMatchObject({
      pathname: '/gestao/membros/[id]/desligar',
      params: { id: member.id },
    });
    expect(r.calls.some((c) => c.method === 'DELETE')).toBe(false);
  });
  it('a Manager cannot target an Admin or assign Admin', async () => {
    const r = rig();
    r.role('Manager');
    await r.ready();
    memberServer(r, [{ ...me, role: 'Manager' }, admin2, manager, member], undefined, 'Manager');
    await open(r);
    expect(
      await screen.findByText(
        'Somente um administrador pode alterar ou remover o acesso de um administrador.',
      ),
    ).toBeTruthy();
    expect(
      screen.getByTestId(`member-remove-${admin2.id}`).props.accessibilityState?.disabled,
    ).toBe(true);
    await press(`member-role-change-${member.id}`);
    expect(screen.queryByTestId('member-role-Admin')).toBeNull();
    expect(
      screen.getByTestId(`member-role-change-${manager.id}`).props.accessibilityState?.disabled,
    ).toBe(false);
    expect(screen.getByTestId('member-role-Manager')).toBeTruthy();
  });
  it('the last active Admin is protected distinctly from self', async () => {
    const r = rig();
    await r.ready();
    const only = { ...admin2 };
    memberServer(r, [{ ...me, role: 'Manager' }, only, member]);
    await open(r);
    expect(await screen.findByTestId(`member-reason-${only.id}`)).toBeTruthy();
    expect(screen.getByTestId(`member-reason-${only.id}`).props.children).toMatch(
      /única pessoa administradora ativa/,
    );
    expect(screen.getByTestId(`member-remove-${only.id}`).props.accessibilityState?.disabled).toBe(
      true,
    );
  });
  it('a role change rechecks the membership and sends the exact branch membership id', async () => {
    const r = rig();
    await r.ready();
    memberServer(r, [me, admin2, member], (path, init) =>
      path === `/branch/users/${member.id}/role` && init.method === 'PUT'
        ? response({ branchUser: { ...member, role: 'Manager' } })
        : undefined,
    );
    await open(r);
    await press(`member-role-change-${member.id}`);
    await press('member-role-Manager');
    const before = reads(r, '/branch/users');
    await act(async () => fireEvent.press(screen.getByText('Salvar função')));
    await waitFor(() =>
      expect(writes(r, `/branch/users/${member.id}/role`, 'PUT')).toHaveLength(1),
    );
    expect(reads(r, '/branch/users')).toBeGreaterThan(before);
    expect(writes(r, `/branch/users/${member.id}/role`, 'PUT')[0]!.body).toEqual({
      role: 'Manager',
    });
  });
  it('a membership changed elsewhere is reported and nothing is sent', async () => {
    const r = rig();
    await r.ready();
    let lists = 0;
    memberServer(r, [], (path, init) =>
      path === '/branch/users' && init.method === 'GET'
        ? response({
            branchUsers: ++lists === 1 ? [me, member] : [me, { ...member, role: 'Manager' }],
          })
        : undefined,
    );
    await open(r);
    await press(`member-role-change-${member.id}`);
    await press('member-role-Manager');
    await act(async () => fireEvent.press(screen.getByText('Salvar função')));
    expect(
      await screen.findByText('A função mudou para Gerente desde a última leitura.'),
    ).toBeTruthy();
    expect(writes(r, `/branch/users/${member.id}/role`)).toHaveLength(0);
  });
  it('coordination busy is verbatim with an explicit reread before retry', async () => {
    const r = rig();
    await r.ready();
    memberServer(r, [me, member], (path, init) =>
      path === `/branch/users/${member.id}/role` && init.method === 'PUT'
        ? response(
            { errorMessages: ['Outra alteração de acesso está em andamento. Tente novamente.'] },
            409,
          )
        : undefined,
    );
    await open(r);
    await press(`member-role-change-${member.id}`);
    await press('member-role-Manager');
    await act(async () => fireEvent.press(screen.getByText('Salvar função')));
    expect(
      await screen.findByText('Outra alteração de acesso está em andamento. Tente novamente.'),
    ).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler membros e conferir')));
    expect(await screen.findByText('Salvar função')).toBeTruthy();
    expect(writes(r, `/branch/users/${member.id}/role`, 'PUT')).toHaveLength(1);
  });
  it('invitations default to Pendente, show validity and offer no cancel action', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    await r.ready();
    memberServer(r, [me], undefined, 'Admin', [
      invitation(70, { operatorId: id(31) }),
      invitation(71, { deliveryStatus: 'Failed', role: 'Manager' }),
    ]);
    await open(r);
    expect(await screen.findByText('c70@x.invalid')).toBeTruthy();
    const list = r.calls.find((c) => c.path === '/invitation')!;
    expect(new URL(list.url).searchParams.get('Status')).toBe('Pending');
    expect(screen.getByTestId(`invitation-operator-${id(70)}`).props.children).toBe(
      'Operador: Beto',
    );
    expect(screen.getByText('Falha no envio')).toBeTruthy();
    expect(screen.getAllByText(/Não pode ser cancelado; vale até/).length).toBe(2);
    expect(screen.queryByText(/Cancelar convite/)).toBeNull();
    expect(screen.getByTestId(`invitation-reissue-${id(71)}`)).toBeTruthy();
  });
  it('a failed delivery is reported as failed, never as Convite enviado', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    r.role('Manager');
    await r.ready();
    memberServer(
      r,
      [{ ...me, role: 'Manager' }],
      (path, init) =>
        path === '/invitation' && init.method === 'POST'
          ? response(
              invitation(72, {
                email: 'novo@x.invalid',
                deliveryStatus: 'Failed',
                operatorId: id(32),
              }),
              201,
            )
          : undefined,
      'Manager',
    );
    await open(r);
    await press('invitation-new');
    expect(screen.queryByTestId('invitation-role-Admin')).toBeNull();
    fireEvent.changeText(
      await until(() => screen.getByTestId('invitation-email')),
      'novo@x.invalid',
    );
    await press('invitation-role-Member');
    await press('invitation-operator');
    // Only active operators without a login are offered.
    expect(screen.queryByTestId(`invitation-operator-option-${id(30)}`)).toBeNull();
    await press(`invitation-operator-option-${id(32)}`);
    await act(async () => fireEvent.press(screen.getByTestId('invitation-send')));
    await waitFor(() => expect(writes(r, '/invitation', 'POST')).toHaveLength(1));
    expect(writes(r, '/invitation', 'POST')[0]!.body).toEqual({
      email: 'novo@x.invalid',
      role: 'Member',
      operatorId: id(32),
    });
    // Shown as the persistent notice and announced as a toast.
    expect((await screen.findByTestId('invitation-receipt')).props).toBeTruthy();
    expect(
      screen.getAllByText(
        'O convite para novo@x.invalid foi criado, mas o envio do e-mail falhou.',
      ),
    ).toHaveLength(2);
    expect(screen.queryByText(/^Convite enviado/)).toBeNull();
  });
  it('an accepted SMTP delivery is reported without claiming inbox arrival', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    await r.ready();
    memberServer(r, [me], (path, init) =>
      path === '/invitation' && init.method === 'POST'
        ? response(invitation(73, { email: 'ok@x.invalid', role: 'Admin' }), 201)
        : undefined,
    );
    await open(r);
    await press('invitation-new');
    fireEvent.changeText(await until(() => screen.getByTestId('invitation-email')), 'ok@x.invalid');
    await press('invitation-role-Admin');
    expect(screen.queryByTestId('invitation-operator')).toBeNull();
    await act(async () => fireEvent.press(screen.getByTestId('invitation-send')));
    await screen.findByTestId('invitation-receipt');
    expect(screen.getAllByText('Convite enviado para ok@x.invalid.')).toHaveLength(2);
    expect(screen.getByText(/isso não confirma a chegada na caixa de entrada/)).toBeTruthy();
    expect(writes(r, '/invitation', 'POST')[0]!.body).toMatchObject({ operatorId: null });
  });
  it('an unknown issue outcome rereads pending invitations and never issues a second one', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    await r.ready();
    memberServer(r, [me], (path, init) => {
      if (path === '/invitation' && init.method === 'POST')
        return response({ errorMessages: [] }, 502);
      if (path === '/invitation' && init.method === 'GET')
        return response({
          items: [invitation(74, { email: 'talvez@x.invalid' })],
          totalCount: 1,
          page: 1,
          pageSize: 50,
        });
      return undefined;
    });
    await open(r);
    await press('invitation-new');
    fireEvent.changeText(
      await until(() => screen.getByTestId('invitation-email')),
      'talvez@x.invalid',
    );
    await press('invitation-role-Member');
    await act(async () => fireEvent.press(screen.getByTestId('invitation-send')));
    expect(await screen.findByText('Resultado não confirmado')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler convites para conferir')));
    expect(await screen.findByTestId('invitation-reconciled')).toBeTruthy();
    expect(screen.getByText(/Já existe convite pendente para este e-mail/)).toBeTruthy();
    expect(writes(r, '/invitation', 'POST')).toHaveLength(1);
  });
  it('recovering a confirmed invitation never issues a second one', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    await r.ready();
    let lists = 0;
    memberServer(r, [me], (path, init) => {
      if (path === '/invitation' && init.method === 'GET')
        return ++lists === 2 || lists === 3
          ? response({ errorMessages: ['Leitura indisponível.'] }, 503)
          : response({ items: [], totalCount: 0, page: 1, pageSize: 50 });
      if (path === '/invitation' && init.method === 'POST')
        return response(invitation(76, { email: 'nova@x.invalid' }), 201);
      return undefined;
    });
    await open(r);
    await press('invitation-new');
    fireEvent.changeText(
      await until(() => screen.getByTestId('invitation-email')),
      'nova@x.invalid',
    );
    await press('invitation-role-Member');
    await act(async () => fireEvent.press(screen.getByTestId('invitation-send')));
    expect(await screen.findByText(/A conferência local não foi concluída/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reler convites para conferir')));
    await until(() => expect(lists).toBe(3));
    expect(screen.getByTestId('invitation-send').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByText('Reler convites para conferir')));
    await waitFor(() => expect(screen.queryByTestId('invitation-editor')).toBeNull());
    expect(screen.queryByTestId('invitation-reconciled')).toBeNull();
    expect(writes(r, '/invitation', 'POST')).toHaveLength(1);
  });
  it('reissue keeps the operator by omitting the body and clears it with an explicit null', async () => {
    mockParams = { section: 'convites' };
    const r = rig();
    await r.ready();
    const pending = invitation(75, { operatorId: id(31) });
    memberServer(
      r,
      [me],
      (path, init) =>
        path === `/invitation/${id(75)}/reissue` && init.method === 'POST'
          ? response({ ...pending, deliveryStatus: 'Sent' })
          : undefined,
      'Admin',
      [pending],
    );
    await open(r);
    await press(`invitation-reissue-${id(75)}`);
    expect(screen.getByText(/O link anterior deixa de valer/)).toBeTruthy();
    await act(async () => fireEvent.press(screen.getByText('Reenviar convite')));
    await waitFor(() => expect(writes(r, `/invitation/${id(75)}/reissue`, 'POST')).toHaveLength(1));
    expect(writes(r, `/invitation/${id(75)}/reissue`, 'POST')[0]!.body).toBeUndefined();
    await screen.findByTestId('invitation-receipt');
    expect(screen.getAllByText('Convite reenviado para c75@x.invalid.').length).toBeGreaterThan(0);
    await waitFor(() => expect(screen.queryByTestId('invitation-reissue-dialog')).toBeNull());
    await press(`invitation-reissue-${id(75)}`);
    await choose('invitation-reissue-operator', 'none');
    await act(async () => fireEvent.press(screen.getByText('Reenviar convite')));
    await waitFor(() => expect(writes(r, `/invitation/${id(75)}/reissue`, 'POST')).toHaveLength(2));
    expect(writes(r, `/invitation/${id(75)}/reissue`, 'POST')[1]!.body).toEqual({
      operatorId: null,
    });
  });
});

describe('renewal, retention and late responses', () => {
  it('an open type editor survives same-authority renewal without dispatch or replay', async () => {
    mockPath = '/gestao/categorias';
    mockParams = { tab: 'tipos' };
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
      if (path === '/category')
        return response({
          items: [
            {
              id: id(40),
              name: 'Receita',
              defaultDirection: 'In',
              branchId,
              createdAt: 'x',
              active: true,
            },
          ],
        });
      if (path === '/transaction-type') return response({ items: [] });
      return undefined;
    });
    await open(r);
    await press('type-new');
    fireEvent.changeText(await until(() => screen.getByTestId('type-name')), 'Durante renovação');
    holding = true;
    let refreshing: Promise<void> | undefined;
    await act(async () => {
      r.expire();
      refreshing = r.engine.refresh();
      await entered.promise;
    });
    expect(screen.getByTestId('time-session-pause')).toBeTruthy();
    await act(async () => {
      release.resolve();
      await refreshing;
    });
    holding = false;
    expect((await until(() => screen.getByTestId('type-name'))).props.value).toBe(
      'Durante renovação',
    );
    expect(writes(r, '/transaction-type', 'POST')).toHaveLength(0);
  });
  it('retained account input is offered after a fresh check and restoring never submits', async () => {
    const r = rig();
    await r.ready();
    accountServer(r);
    const first = await open(r);
    await press('account-new-BankAccount');
    fireEvent.changeText(await until(() => screen.getByTestId('account-name')), 'Guardada');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    first.unmount();
    await open(r);
    await press('account-new-BankAccount');
    const restore = await until(() => screen.getByText('Restaurar para revisão'));
    await act(async () => fireEvent.press(restore));
    expect(screen.getByTestId('account-name').props.value).toBe('Guardada');
    expect(writes(r, '/account/bank')).toHaveLength(0);
  });
  it('an explicit cancel removes the unsent invitation copy', async () => {
    mockPath = '/gestao/membros';
    const r = rig();
    await r.ready();
    serve(r, (path) =>
      path === '/branch/users'
        ? response({ branchUsers: [] })
        : path === '/operator'
          ? response({ operators: [] })
          : undefined,
    );
    const first = await open(r);
    await press('invitation-new');
    fireEvent.changeText(await until(() => screen.getByTestId('invitation-email')), 'x@x.invalid');
    await act(async () => fireEvent.press(screen.getByText('Cancelar')));
    await act(async () => new Promise((resolve) => setTimeout(resolve, 20)));
    first.unmount();
    await open(r);
    await press('invitation-new');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 50)));
    expect(screen.queryByText('Restaurar para revisão')).toBeNull();
  });
  it('a late account list from a revoked scope never renders', async () => {
    const r = rig();
    await r.ready();
    const late = deferred<Response>();
    accountServer(r, (path, init) =>
      path === '/account' && init.method === 'GET' ? late.promise : undefined,
    );
    mount(r);
    await waitFor(() => expect(reads(r, '/account')).toBe(1));
    r.role('Member');
    await act(async () => {
      await r.engine.refresh({ selfContext: true });
    });
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    await act(async () => late.resolve(response({ accounts })));
    expect(screen.queryByText('Caixa 1')).toBeNull();
  });
});
