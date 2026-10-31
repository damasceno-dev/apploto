import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { useEffect as mockEffect } from 'react';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { ShellNavigationStore } from '../../src/shell/navigation';
import { nativeDeviceStore, emailPartitionKey } from '../../src/storage/native';
import { ScopedDeviceStore } from '../../src/storage/scoped';
import { BranchPicker } from '../../src/shell/entry';
import { rig, branch, branchId, otherBranch, profile, response, deferred, userId } from './support';
import { renderThemed } from './ui/support';

let mockPath = '/';
const mockRouter = {
  replace: jest.fn(),
  push: jest.fn(),
  back: jest.fn(),
  dismissAll: jest.fn(),
  canDismiss: () => true,
  canGoBack: () => false,
};
jest.mock('expo-router', () => ({
  useIsFocused: () => true,
  usePathname: () => mockPath,
  useGlobalSearchParams: () => ({}),
  useLocalSearchParams: () => ({}),
  useRouter: () => mockRouter,
  useFocusEffect: (callback: () => void | (() => void)) => mockEffect(callback, [callback]),
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));
beforeEach(async () => {
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
  mockPath = '/';
  jest.clearAllMocks();
});
function mount(r: ReturnType<typeof rig>, ui = <MobileScreen />) {
  return renderThemed(
    <SessionProvider engine={r.engine} lifecycle={false}>
      <ShellProvider>{ui}</ShellProvider>
    </SessionProvider>,
  );
}
it('real Login composition keeps e-mail and clears password after a verbatim invalid-credentials response', async () => {
  const r = rig();
  r.intercept(async (path) =>
    path === '/user/login'
      ? response({ errorMessages: ['Credenciais inválidas.'] }, 401)
      : undefined,
  );
  mount(r);
  const email = await screen.findByLabelText('E-mail');
  fireEvent.changeText(email, 'fixture@example.invalid');
  fireEvent.changeText(screen.getByLabelText('Senha'), 'private');
  fireEvent.press(screen.getByTestId('login-submit'));
  expect(await screen.findByText('Credenciais inválidas.')).toBeTruthy();
  expect(screen.getByLabelText('E-mail').props.value).toBe('fixture@example.invalid');
  expect(screen.getByLabelText('Senha').props.value).toBe('');
  expect(r.calls.filter((c) => c.path === '/user/login')).toHaveLength(1);
  expect(r.calls.filter((c) => c.path === '/branch/current')).toHaveLength(0);
});
it.each([0, 1, 2])(
  'renders %i memberships and auto-opens only the single branch',
  async (count) => {
    const r = rig();
    await r.login();
    r.intercept(async (path) =>
      path === '/branch/my-branches'
        ? response({ branches: [branch, { ...branch, id: otherBranch }].slice(0, count) })
        : undefined,
    );
    mount(r, <BranchPicker menu={null} />);
    if (count === 0)
      expect(await screen.findByText('Você ainda não tem acesso a uma lotérica.')).toBeTruthy();
    if (count === 1) await waitFor(() => expect(r.engine.getSnapshot().status).toBe('branch'));
    if (count === 2) {
      expect(await screen.findAllByText('Filial de teste')).toHaveLength(2);
      expect(r.engine.getSnapshot().status).toBe('identity');
    }
    expect(r.calls.filter((c) => c.path === '/branch/session')).toHaveLength(count === 1 ? 1 : 0);
  },
);
it('suppresses competing branch rows while a selection is pending', async () => {
  const r = rig();
  await r.login();
  const delayed = deferred<Response>();
  r.intercept(async (path) =>
    path === '/branch/my-branches'
      ? response({ branches: [branch, { ...branch, id: otherBranch }] })
      : path === '/branch/session'
        ? delayed.promise
        : undefined,
  );
  mount(r, <BranchPicker menu={null} />);
  fireEvent.press(await screen.findByTestId('branch-' + branchId));
  fireEvent.press(screen.getByTestId('branch-' + otherBranch));
  await waitFor(() => expect(r.calls.filter((c) => c.path === '/branch/session')).toHaveLength(1));
  delayed.resolve(response({ token: 'branch-1', branch }));
  await waitFor(() => expect(r.engine.getSnapshot().status).toBe('branch'));
  expect(r.calls.filter((c) => c.path === '/branch/session')).toHaveLength(1);
});
it.each(['Member', 'Manager', 'Admin'])('guards real shell composition for %s', async (role) => {
  const r = rig();
  r.role(role);
  await r.ready();
  mockPath = role === 'Member' ? '/turno/hoje' : '/gestao/fila';
  mount(r);
  expect(await screen.findByTestId('navigation-menu')).toBeTruthy();
  fireEvent.press(screen.getByTestId('navigation-menu'));
  if (role === 'Member') expect(screen.queryByText('Fila de trabalho')).toBeNull();
  else expect(await screen.findByText('Dashboard')).toBeTruthy();
});
it('keeps a missing link separate from a failed read and offers authorized setup destinations', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) =>
    path === '/operator/self-context'
      ? response({ operatorId: null, availableAccounts: [] })
      : undefined,
  );
  mockPath = '/turno/hoje';
  mount(r);
  expect(await screen.findByText('Seu login ainda não está vinculado a um operador.')).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Ir para Operadores' })).toBeTruthy();
  expect(screen.getByRole('button', { name: 'Contas dos operadores' })).toBeTruthy();
});
it('renders a forbidden destination without exposing its future feature content', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  mockPath = '/relatorios/dashboard';
  mount(r);
  expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
  expect(
    screen.queryByText(
      'Os dados e as ações de dashboard ainda não estão disponíveis neste aplicativo.',
    ),
  ).toBeNull();
});
it('Session menu hides modes for Members and performs a real uncertain logout while preserving theme', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  mockPath = '/turno/hoje';
  r.intercept(async (path) =>
    path === '/branch/my-branches'
      ? response({ branches: [branch] })
      : path === '/user/logout'
        ? response({ errorMessages: ['Servidor indisponível.'] }, 503)
        : undefined,
  );
  const rendered = mount(r);
  await screen.findByTestId('session-menu');
  await waitFor(() => expect(r.engine.getSnapshot().selfStatus).toBe('ready'));
  fireEvent.press(screen.getByTestId('session-menu'));
  expect(await screen.findByText('Sua conta')).toBeTruthy();
  expect(screen.queryByText('Modo de trabalho')).toBeNull();
  fireEvent.press(screen.getByRole('radio', { name: 'Escuro' }));
  fireEvent.press(screen.getByTestId('logout'));
  await waitFor(() => expect(r.engine.getSnapshot().status).toBe('anonymous'));
  expect(r.calls.filter((c) => c.path === '/user/logout')).toHaveLength(1);
  expect(r.engine.getSnapshot().recovery).toContain('Não foi possível confirmar');
  expect(rendered.store.values.get('loto.theme-preference.v1')).toBe('dark');
});
it('keeps branch refusal messages after the real engine replaces the scope and permits manual recovery', async () => {
  const r = rig();
  await r.login();
  mockPath = '/filiais';
  let refused = true;
  r.intercept(async (path) =>
    path === '/branch/my-branches'
      ? response({ branches: [branch, { ...branch, id: otherBranch }] })
      : path === '/branch/session' && refused
        ? response({ errorMessages: ['Filial indisponível.'] }, 403)
        : undefined,
  );
  mount(r);
  fireEvent.press(await screen.findByTestId('branch-' + branchId));
  expect(await screen.findByText('Filial indisponível.')).toBeTruthy();
  expect(r.engine.getSnapshot().status).toBe('identity');
  refused = false;
  fireEvent.press(screen.getByTestId('branch-' + otherBranch));
  await waitFor(() => expect(r.engine.getSnapshot().branch?.id).toBe(otherBranch));
});
it('restores an authorized shared destination in Meu turno without a competing default-home redirect', async () => {
  const r = rig();
  await r.ready();
  const nav = new ShellNavigationStore(nativeDeviceStore, emailPartitionKey);
  await nav.write(profile, branchId, {
    v: 1,
    path: '/transacoes',
    mode: 'shift',
    origins: ['/turno/hoje'],
  });
  mount(r);
  await waitFor(() =>
    expect(mockRouter.replace).toHaveBeenCalledWith(
      expect.objectContaining({ pathname: '/transacoes' }),
    ),
  );
  expect(mockRouter.replace.mock.calls.some(([href]) => href?.pathname === '/gestao/fila')).toBe(
    false,
  );
});
it('keeps a protected destination behind a fresh entry read before mounting its content', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/fila';
  const held = deferred<void>();
  const before = r.calls.filter((call) => call.path === '/branch/current').length;
  r.intercept(async (path) => {
    if (path === '/branch/current') {
      await held.promise;
      return response({
        branch,
        branchLocalDate: '2026-10-27',
        branchLocalDateTime: '2026-10-27T09:00:00',
      });
    }
    return undefined;
  });
  mount(r);
  await waitFor(() =>
    expect(r.calls.filter((call) => call.path === '/branch/current').length).toBeGreaterThan(
      before,
    ),
  );
  expect(
    screen.queryByText(
      'Os dados e as ações de fila de trabalho ainda não estão disponíveis neste aplicativo.',
    ),
  ).toBeNull();
  held.resolve();
  expect(
    await screen.findByText(
      'Os dados e as ações de fila de trabalho ainda não estão disponíveis neste aplicativo.',
    ),
  ).toBeTruthy();
});

it.each([false, true])('shows retained-work rail only for the caller (own=%s)', async (own) => {
  const r = rig();
  await r.ready();
  const user = r.engine.getSnapshot().user!;
  const emailKey = await emailPartitionKey(user.email);
  await nativeDeviceStore.setItem(
    `loto.draft.v1:${own ? user.userId : 'another-user'}:${emailKey}:${branchId}:native-note`,
    '{}',
  );
  const keys = jest.spyOn(ScopedDeviceStore.prototype, 'inspectOwnWork');
  mockPath = '/gestao/fila';
  mount(r);
  await screen.findByText(/Os dados e as ações de fila de trabalho/);
  await waitFor(() => expect(keys).toHaveBeenCalled());
  const message = /Há alterações ou operações guardadas neste dispositivo/;
  if (own) expect(await screen.findByText(message)).toBeTruthy();
  else expect(screen.queryByText(message)).toBeNull();
  keys.mockRestore();
});

it('keeps a long branch name, address, CNPJ and live role in one complete accessible row', async () => {
  const r = rig();
  await r.login();
  const name =
    'Lotérica da Praça Central e Atendimento dos Bairros Jardim das Palmeiras e Vila Esperança';
  const address = 'Avenida das Palmeiras, 1234, loja 567, Jardim das Palmeiras, São Paulo';
  r.intercept(async (path) =>
    path === '/branch/my-branches'
      ? response({
          branches: [
            { ...branch, name, address, cnpj: '12345678000190', role: 'Manager' },
            { ...branch, id: otherBranch },
          ],
        })
      : undefined,
  );
  mount(r, <BranchPicker menu={null} />);
  expect(
    await screen.findByRole('button', {
      name: `${name} — Gerente, ${address}, 12.345.678/0001-90`,
    }),
  ).toBeTruthy();
  expect(screen.getByText(name)).toBeTruthy();
  expect(screen.getByText(address)).toBeTruthy();
  expect(r.calls.filter((call) => call.path === '/branch/session')).toHaveLength(0);
});

it('ordinary focus and clock refresh do not insert the session-verification rail item', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/fila';
  mount(r);
  await screen.findByText(/Os dados e as ações de fila de trabalho/);
  const reads = r.calls.filter((call) => call.path === '/operator/self-context').length;
  // SessionProvider starts/recreates the engine; each real creation gets one context read.
  expect(reads).toBe(r.calls.filter((call) => call.path === '/branch/session').length);
  await act(async () => {
    await r.engine.refresh();
  });
  expect(r.calls.filter((call) => call.path === '/operator/self-context')).toHaveLength(reads);
  expect(screen.queryByText('Sua sessão está sendo verificada…')).toBeNull();
  expect(r.engine.getSnapshot().selfStatus).toBe('ready');
  expect(r.engine.getSnapshot().writesPaused).toBe(false);
});

it('menu opening refreshes self-context with a quiet internal write hold', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/fila';
  mount(r);
  await screen.findByText(/Os dados e as ações de fila de trabalho/);
  const entered = deferred<void>();
  const wait = deferred<Response>();
  r.intercept(async (path) => {
    if (path === '/operator/self-context') {
      entered.resolve(undefined);
      return wait.promise;
    }
    return undefined;
  });
  await act(async () => {
    fireEvent.press(screen.getByTestId('session-menu'));
    await entered.promise;
  });
  expect(r.engine.getSnapshot()).toMatchObject({ selfStatus: 'ready', writesPaused: true });
  expect(screen.queryByText('Sua sessão está sendo verificada…')).toBeNull();
  expect(screen.queryByText('Verificando…')).toBeNull();
  await act(async () => {
    wait.resolve(response({ operatorId: userId, availableAccounts: [], primaryAccount: null }));
  });
  await waitFor(() => expect(r.engine.getSnapshot().writesPaused).toBe(false));
});
it('enables Meu turno after a Manager is linked mid-session and opens the menu again', async () => {
  const r = rig();
  r.role('Manager');
  let linked = false;
  r.intercept(async (path) =>
    path === '/operator/self-context'
      ? response({
          operatorId: linked ? userId : null,
          availableAccounts: [],
          primaryAccount: null,
        })
      : path === '/branch/my-branches'
        ? response({ branches: [{ ...branch, role: 'Manager' }] })
        : undefined,
  );
  await r.ready();
  mockPath = '/gestao/fila';
  mount(r);
  await screen.findByText(/Os dados e as ações de fila de trabalho/);
  fireEvent.press(screen.getByTestId('session-menu'));
  expect(
    (await screen.findByRole('radio', { name: 'Meu turno' })).props.accessibilityState.disabled,
  ).toBe(true);
  await waitFor(() => expect(r.engine.getSnapshot().writesPaused).toBe(false));
  fireEvent.press(screen.getByRole('button', { name: 'Fechar' }));
  linked = true;
  fireEvent.press(screen.getByTestId('session-menu'));
  await waitFor(() => expect(r.engine.getSnapshot().selfContext?.operatorId).toBe(userId));
  // Changed authority replaces the scope and closes its old modal; reopen the verified menu.
  await screen.findByTestId('session-menu');
  fireEvent.press(screen.getByTestId('session-menu'));
  expect(
    (await screen.findByRole('radio', { name: 'Meu turno' })).props.accessibilityState.disabled,
  ).toBe(false);
});
