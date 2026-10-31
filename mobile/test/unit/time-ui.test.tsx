import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { useEffect, useEffect as mockEffect } from 'react';
import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { getGetTimeentryTimeEntryIdQueryKey } from '@loto/api/queries';
import { DeviceDraftStore } from '../../src/storage/scoped';
import { SessionProvider } from '../../src/session/provider';
import { ShellNavigationStore } from '../../src/shell/navigation';
import { ShellProvider, useShell } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { Button, ToastProvider } from '../../src/ui';
import { TimeSurface } from '../../src/time/surface';
import { nativeDeviceStore, emailPartitionKey } from '../../src/storage/native';
import { rig, response, deferred, userId, branchId, profile } from './support';
import { renderThemed } from './ui/support';
import type { DestinationKey } from '../../src/shell/policy';
import type { ResponseTimeEntryJson } from '@loto/api';
let mockPath = '/turno/ponto';
let mockParams: Record<string, string> = {};
const mockRouter = {
  replace: jest.fn(),
  push: jest.fn(),
  back: jest.fn(),
  dismissTo: jest.fn(),
  setParams: jest.fn(),
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
const id = '30000000-0000-4000-8000-000000000001';
const segmentId = '40000000-0000-4000-8000-000000000001';
const current: ResponseTimeEntryJson = {
  id,
  operatorId: userId,
  operatorName: 'Pessoa teste',
  branchId,
  date: '2026-10-27',
  status: 'Present',
  totalHours: 1,
  balanceHours: -7,
  isInProgress: false,
  active: true,
  createdAt: '2026-10-27T12:00:00Z',
  segments: [
    {
      id: segmentId,
      clockIn: '2026-10-27T08:00:00',
      clockOut: '2026-10-27T09:00:00',
      active: true,
      createdAt: '2026-10-27T12:00:00Z',
    },
  ],
};
const envelope = (items: unknown[]) => ({
  items,
  totalCount: items.length,
  totalPages: 1,
  hasNext: false,
  hasPrevious: false,
  page: 1,
  pageSize: 20,
});
beforeEach(async () => {
  mockPath = '/turno/ponto';
  mockParams = {};
  jest.clearAllMocks();
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
});
function mount(r: ReturnType<typeof rig>, destination?: DestinationKey) {
  return renderThemed(
    <ToastProvider>
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          {destination ? <TimeSurface destination={destination} /> : <MobileScreen />}
        </ShellProvider>
      </SessionProvider>
    </ToastProvider>,
  );
}
async function readyClock(id = 'time-tap') {
  await screen.findByTestId(id);
  await waitFor(() => expect(screen.getByTestId(id).props.accessibilityState.disabled).toBe(false));
  return screen.getByTestId(id);
}
function setup(r: ReturnType<typeof rig>, items: unknown[] = []) {
  r.intercept(async (path, init) => {
    if (path === '/branch/my-branches')
      return response({ branches: [{ id: branchId, name: 'Filial', role: 'Member' }] });
    if (path === '/timeentry' && init.method === 'GET') return response(envelope(items));
    if (path === '/timeentry/' + id || (path === '/timeentry' && init.method === 'PUT'))
      return response(current);
    if (path === '/setting') return response({ lockDate: '2026-09-30' });
    if (path === '/operator')
      return response({ operators: [{ id: userId, name: 'Pessoa teste', branchId }] });
    return undefined;
  });
}
it.each(['Manager', 'Admin'])('forbids the %s tap before protected time reads', async (role) => {
  const r = rig();
  r.role(role);
  await r.ready();
  setup(r);
  mount(r);
  expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
  expect(r.calls.some((c) => c.path.startsWith('/timeentry'))).toBe(false);
});
it('cold entry with identity alone never dispatches branch time reads', async () => {
  const r = rig();
  await r.login();
  r.intercept(async (path) =>
    path === '/branch/my-branches' ? response({ branches: [] }) : undefined,
  );
  mount(r);
  await screen.findByText('Você ainda não tem acesso a uma lotérica.');
  expect(r.calls.some((c) => c.path.startsWith('/timeentry'))).toBe(false);
});
it('Member suppresses double pending taps and sends only the authoritative branch date/action', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const pending = deferred<Response>();
  setup(r);
  // Intercept writes while retaining the rig's real generated/client boundary.
  r.intercept(async (path, init) =>
    path === '/timeentry'
      ? init.method === 'GET'
        ? response(envelope([]))
        : pending.promise
      : undefined,
  );
  mount(r, 'clock');
  fireEvent.press(await readyClock());
  fireEvent.press(screen.getByTestId('time-tap'));
  await waitFor(() =>
    expect(r.calls.filter((c) => c.path === '/timeentry' && c.method === 'PUT')).toHaveLength(1),
  );
  expect(r.calls.find((c) => c.method === 'PUT')!.body).toEqual({
    operatorId: userId,
    date: '2026-10-27',
    status: 'Present',
    action: 'Open',
  });
  await act(async () => {
    pending.resolve(response(current));
  });
});
it('overnight choices use yesterday Close versus today Open', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const yesterday = { ...current, date: '2026-10-26', isInProgress: true };
  setup(r, [yesterday]);
  mount(r, 'clock');
  fireEvent.press(await readyClock('time-close-yesterday'));
  await waitFor(() =>
    expect(r.calls.find((c) => c.method === 'PUT')?.body).toMatchObject({
      date: '2026-10-26',
      action: 'Close',
    }),
  );
  expect(screen.queryByTestId('time-tap')).toBeNull();
  await waitFor(() =>
    expect(screen.getByTestId('time-close-yesterday').props.accessibilityState.busy).toBe(false),
  );
});
it('unknown tap outcome requires explicit reread and review without automatic retry', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  r.intercept(async (path, init) =>
    path === '/timeentry'
      ? init.method === 'GET'
        ? response(envelope([]))
        : response({ errorMessages: ['Resultado indisponível.'] }, 503)
      : undefined,
  );
  mount(r, 'clock');
  fireEvent.press(await readyClock());
  const reread = await screen.findByText('Reler registros');
  fireEvent.press(screen.getByTestId('time-tap'));
  expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
  await act(async () => fireEvent.press(reread));
  const reviewed = await screen.findByText('Revisei os registros; continuar');
  await act(async () => fireEvent.press(reviewed));
  await readyClock();
  expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
});
it('fresh detail displays wall times, snapshot identity and confirmed granular removal', async () => {
  const r = rig();
  await r.ready();
  setup(r, [current]);
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  mount(r, 'time-detail');
  const input = await screen.findByTestId('segment-out');
  await waitFor(() => expect(input.props.editable).not.toBe(false));
  expect(screen.getByTestId('segment-in').props.accessibilityLabel).toContain('Entrada'); // persisted entrada is plain readonly text
  fireEvent.changeText(input, '2026-10-27T10:00:00');
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-save-day'));
  await waitFor(() => expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1));
  expect(r.calls.find((c) => c.method === 'PUT')!.body).toMatchObject({
    segments: [
      { id: segmentId, clockIn: current.segments[0]!.clockIn, clockOut: '2026-10-27T10:00:00' },
    ],
  });
  expect(r.calls.find((c) => c.method === 'PUT')!.headers).not.toHaveProperty('if-match');
  await screen.findByText('Registro salvo no servidor.');
});
it('locks the editor read-only and leaves retained typed input available for review', async () => {
  const r = rig();
  await r.ready();
  mockParams = { id };
  mockPath = '/gestao/ponto/' + id;
  r.intercept(async (path) =>
    path === '/timeentry/' + id
      ? response(current)
      : path === '/setting'
        ? response({ lockDate: '2026-10-31' })
        : undefined,
  );
  mount(r, 'time-detail');
  await screen.findByText('Dia bloqueado para alterações');
  fireEvent.press(screen.getByTestId('time-save-day'));
  expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
});
it.each(['my-transactions', 'my-hours'] as const)(
  'unlinked Member reaches %s empty API short-circuit',
  async (key) => {
    const r = rig();
    r.role('Member');
    r.intercept(async (path) =>
      path === '/operator/self-context'
        ? response({ operatorId: null, availableAccounts: [] })
        : path === '/report/operator-summary'
          ? response({
              totalTransactionCount: 0,
              generatedTransactionCount: 0,
              totalInValue: 0,
              generatedInValue: 0,
              totalOutValue: 0,
              generatedOutValue: 0,
              netValue: 0,
              byCategory: [],
            })
          : path === '/report/timeentry-balance'
            ? response({ dateFrom: '2026-10-01', dateTo: '2026-10-31', operators: [] })
            : undefined,
    );
    await r.ready();
    mockPath = key === 'my-hours' ? '/turno/banco-de-horas' : '/turno/minhas-transacoes';
    mount(r);
    expect(
      await screen.findByText(
        key === 'my-hours' ? 'Nenhum registro no período.' : 'Nenhuma transação no período.',
      ),
    ).toBeTruthy();
  },
);
it('shows generated values as subsets with neutral financial copy and a source day period', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/operadores/' + userId + '/transacoes';
  mockParams = { id: userId, from: '2026-10-20', to: '2026-10-20' };
  r.intercept(async (path) =>
    path === '/report/operator-summary'
      ? response({
          operatorId: userId,
          operatorName: 'Pessoa teste',
          operatorActive: false,
          totalTransactionCount: 7,
          generatedTransactionCount: 2,
          totalInValue: 30,
          generatedInValue: 10,
          totalOutValue: 12,
          generatedOutValue: 5,
          netValue: 18,
          byCategory: [],
        })
      : undefined,
  );
  mount(r, 'operator-transactions');
  await screen.findByText('7 transações, 2 delas automáticas');
  expect(screen.getByText('Inativo')).toBeTruthy();
  expect(screen.getByText(/Entradas:.*30,00.*10,00 automáticas/)).toBeTruthy();
  const requested = jest
    .mocked(r.transport)
    .mock.calls.map(([url]) => new URL(String(url)))
    .find((url) => url.pathname === '/report/operator-summary')!;
  expect(requested.searchParams.get('DateFrom')).toBe('2026-10-20');
  expect(requested.searchParams.get('DateTo')).toBe('2026-10-20');
  expect(requested.searchParams.has('Mine')).toBe(false);
  fireEvent.press(screen.getByText('Ver transações'));
  await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
  expect(mockRouter.push.mock.calls[0]![0]).toMatchObject({
    pathname: '/transacoes',
    params: { operatorId: userId, from: '2026-10-20', to: '2026-10-20' },
  });
});
it('team balance preserves zero-entry and inactive history; single operator alone gets exact missing dates and target', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/banco-de-horas';
  mockParams = { from: '2026-10-26', to: '2026-10-27', operatorId: userId };
  r.intercept(async (path) =>
    path === '/report/timeentry-balance'
      ? response({
          dateFrom: '2026-10-26',
          dateTo: '2026-10-27',
          operators: [
            {
              operatorId: userId,
              operatorName: 'Pessoa teste',
              operatorActive: false,
              totalHours: 28,
              totalBalanceHours: -2,
              presentDays: 1,
              absentDays: 0,
              owingDays: 1,
              abonadoDays: 0,
              containsInProgress: true,
              days: [
                {
                  date: '2026-10-27',
                  status: 'Present',
                  totalHours: 6,
                  balanceHours: -2,
                  isInProgress: true,
                },
              ],
            },
          ],
        })
      : undefined,
  );
  mount(r, 'hours');
  await screen.findByText('Há registros em andamento');
  expect(screen.getByText('Sem registro')).toBeTruthy();
  expect(screen.getByText(/Meta do dia: 8h 00min/)).toBeTruthy();
  expect(screen.getByText(/Horas: 28h 00min/)).toBeTruthy();
  await act(async () => fireEvent.press(screen.getByText('Abrir ponto de 27/10/2026')));
  expect(mockRouter.setParams).toHaveBeenCalledWith({ row: '2026-10-27', focus: '2026-10-27' });
});

it('renders a real fractional branch wall clock without timezone conversion', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const open = {
    ...current,
    isInProgress: true,
    segments: [{ ...current.segments[0]!, clockIn: '2026-10-27T08:15:00.1234567', clockOut: null }],
  };
  r.intercept(async (path, init) =>
    path === '/timeentry' && init.method === 'GET'
      ? response(envelope([open]))
      : path === '/timeentry/' + id
        ? response(open)
        : undefined,
  );
  mount(r, 'clock');
  expect(await screen.findByText('Em andamento desde 08:15')).toBeTruthy();
  expect(screen.getByText('Entrada 08:15 · Saída Em andamento')).toBeTruthy();
});

it('refuses a stale manager snapshot without overwriting typed time', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  let changed = false;
  r.intercept(async (path) =>
    path === '/timeentry/' + id
      ? response(changed ? { ...current, updatedAt: '2026-10-27T13:00:00Z' } : current)
      : path === '/setting'
        ? response({ lockDate: '2026-09-30' })
        : undefined,
  );
  mount(r, 'time-detail');
  const field = await screen.findByTestId('segment-out');
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.changeText(field, '27/10/2026 10:30');
  changed = true;
  fireEvent.press(screen.getByTestId('time-save-day'));
  expect(
    await screen.findByText(
      'O registro mudou. Confira a versão atual e seu texto antes de continuar.',
    ),
  ).toBeTruthy();
  expect(screen.getByTestId('segment-out').props.value).toBe('27/10/2026 10:30');
  expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
  expect(
    (await nativeDeviceStore.getAllKeys()).some((key) => key.startsWith('loto.draft.v1:')),
  ).toBe(true);
});
it('requires exact segment confirmation and keeps day removal separate from status', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  r.intercept(async (path, init) =>
    path === '/setting'
      ? response({ lockDate: '2026-09-30' })
      : path === '/timeentry/' + id
        ? response(current)
        : path === '/timeentry/segment/' + segmentId && init.method === 'DELETE'
          ? response({ ...current, segments: [] })
          : undefined,
  );
  mount(r, 'time-detail');
  fireEvent.press(await screen.findByText('Remover segmento 1'));
  await screen.findByText(
    'O segmento de 27/10/2026 08:00 a 27/10/2026 09:00 será removido. Essa ação não pode ser desfeita.',
  );
  expect(r.calls.some((c) => c.method === 'DELETE')).toBe(false);
  fireEvent.press(screen.getByText('Remover', { exact: true }));
  await screen.findByText('Registro salvo no servidor.');
  expect(r.calls.filter((c) => c.method === 'DELETE').map((c) => c.path)).toEqual([
    '/timeentry/segment/' + segmentId,
  ]);
  fireEvent.press(screen.getByText('Remover registro do dia'));
  expect(
    await screen.findByText(
      'O registro de 27/10/2026 de Pessoa teste será removido. O dia passa a contar como sem registro e sai do banco de horas. Essa ação não pode ser desfeita.',
    ),
  ).toBeTruthy();
});
it('unknown manager outcome blocks another snapshot and reconciles without replacing typed input', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  r.intercept(async (path, init) =>
    path === '/timeentry/' + id
      ? response(current)
      : path === '/setting'
        ? response({ lockDate: '2026-09-30' })
        : path === '/timeentry' && init.method === 'PUT'
          ? response({ errorMessages: ['Resposta perdida.'] }, 503)
          : undefined,
  );
  mount(r, 'time-detail');
  const field = await screen.findByTestId('segment-out');
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.changeText(field, '27/10/2026 10:00');
  fireEvent.press(screen.getByTestId('time-save-day'));
  fireEvent.press(await screen.findByText('Reler registro para reconciliar'));
  await screen.findByText('Registro atual para revisão');
  expect(screen.getByTestId('segment-out').props.value).toBe('27/10/2026 10:00');
  fireEvent.press(screen.getByTestId('time-save-day'));
  expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
});
it('uses backend paging and ordering, preserves an applied operator and row on the detail hop', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto';
  mockParams = {
    from: '2026-10-01',
    to: '2026-10-31',
    page: '2',
    operatorId: userId,
    inProgress: 'true',
  };
  setup(r, [current]);
  mount(r, 'time');
  fireEvent.press(await screen.findByTestId('time-open-' + id));
  await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
  const url = jest
    .mocked(r.transport)
    .mock.calls.map(([url]) => new URL(String(url)))
    .find((url) => url.pathname === '/timeentry')!;
  expect(Object.fromEntries(url.searchParams)).toMatchObject({
    Page: '2',
    OperatorId: userId,
    InProgressFirst: 'true',
    IsInProgress: 'true',
    DateFrom: '2026-10-01',
  });
  expect(mockRouter.setParams).toHaveBeenCalledWith({ row: id, focus: id });
});
it('team roll-up retains active zero-entry operators and inactive history without a global target', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/banco-de-horas';
  const operator = {
    operatorId: userId,
    operatorName: 'Operador sem entradas',
    operatorActive: true,
    totalHours: 0,
    totalBalanceHours: 0,
    presentDays: 0,
    absentDays: 0,
    owingDays: 0,
    abonadoDays: 0,
    containsInProgress: false,
    days: [],
  };
  r.intercept(async (path) =>
    path === '/report/timeentry-balance'
      ? response({
          dateFrom: '2026-10-01',
          dateTo: '2026-10-31',
          operators: [
            operator,
            {
              ...operator,
              operatorId: id,
              operatorName: 'Operador histórico',
              operatorActive: false,
            },
          ],
        })
      : undefined,
  );
  mount(r, 'hours');
  await screen.findByText('Operador sem entradas');
  await screen.findByText('Operador histórico');
  expect(screen.getByText('Inativo')).toBeTruthy();
  expect(screen.queryByText(/Meta do dia/)).toBeNull();
  expect(screen.queryByText('Sem registro')).toBeNull();
  const url = jest
    .mocked(r.transport)
    .mock.calls.map(([url]) => new URL(String(url)))
    .find((url) => url.pathname === '/report/timeentry-balance')!;
  expect(url.searchParams.has('Mine')).toBe(false);
  expect(url.searchParams.has('OperatorId')).toBe(false);
});
it('manager own hours sends Segments and no Member Action', async () => {
  const r = rig();
  r.role('Manager');
  await r.ready();
  mockPath = '/gestao/ponto';
  mockParams = { section: 'new', operatorId: userId };
  r.intercept(async (path, init) =>
    path === '/operator'
      ? response({ operators: [{ id: userId, name: 'Pessoa teste', branchId }] })
      : path === '/timeentry/' + id
        ? response(current)
        : path === '/setting'
          ? response({ lockDate: '2026-09-30' })
          : path === '/timeentry'
            ? init.method === 'GET'
              ? response(envelope([]))
              : response(current)
            : undefined,
  );
  mount(r, 'time');
  await screen.findByTestId('time-prepare');
  await waitFor(() =>
    expect(screen.getByTestId('time-prepare').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-prepare'));
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  let outgoingButton = screen.getByTestId('time-save-day');
  while (typeof outgoingButton.props.onPress !== 'function')
    outgoingButton = outgoingButton.parent!;
  const outgoingSave = outgoingButton.props.onPress;
  fireEvent.press(screen.getByTestId('time-save-day'));
  await waitFor(() =>
    expect(mockRouter.replace).toHaveBeenCalledWith(
      expect.objectContaining({ pathname: '/gestao/ponto/[id]' }),
    ),
  );
  expect(screen.queryByTestId('time-save-day')).toBeNull();
  expect(screen.queryByTestId('time-edit-segment-1')).toBeNull();
  await act(async () => outgoingSave());
  expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
  const write = r.calls.find((c) => c.method === 'PUT')!;
  expect(write.body).toEqual({
    operatorId: userId,
    date: '2026-10-27',
    status: 'Present',
    segments: [],
  });
});
it('foreground loss pauses product writes and late old-scope reads cannot supply a new branch', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const pending = deferred<Response>();
  r.intercept(async (path) => (path === '/timeentry' ? pending.promise : undefined));
  mount(r, 'clock');
  await act(async () => {
    await r.engine.foreground(false);
  });
  expect(screen.queryByTestId('time-tap')).toBeNull();
  await act(async () => {
    pending.resolve(response(envelope([current])));
  });
  expect(screen.queryByText('Registros de hoje')).toBeNull();
  expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
});

function ReturnAction() {
  const shell = useShell();
  return (
    <Button
      label="Retornar ao contexto"
      disabled={!shell.ready}
      onPress={() => void shell.back()}
    />
  );
}
it.each([
  '/gestao/ponto?from=2026-10-27&to=2026-10-27&operatorId=' + userId + '&row=' + id,
  '/turno/banco-de-horas?from=2026-10-27&to=2026-10-27',
])(
  'returns to the freshly validated opener rather than an arbitrary native history entry: %s',
  async (origin) => {
    const r = rig();
    await r.ready();
    setup(r);
    mockPath = '/gestao/ponto/' + id;
    mockParams = { id };
    const store = new ShellNavigationStore(nativeDeviceStore, emailPartitionKey);
    await store.write(profile, branchId, {
      v: 1,
      path: mockPath,
      mode: 'management',
      origins: [origin],
    });
    renderThemed(
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          <ReturnAction />
        </ShellProvider>
      </SessionProvider>,
    );
    const button = await screen.findByText('Retornar ao contexto');
    await waitFor(() =>
      expect(
        screen.getByRole('button', { name: 'Retornar ao contexto' }).props.accessibilityState
          .disabled,
      ).toBe(false),
    );
    fireEvent.press(button);
    await waitFor(() => expect(mockRouter.dismissTo).toHaveBeenCalled());
    expect(mockRouter.dismissTo.mock.calls[0]![0]).toMatchObject({
      pathname: origin.split('?')[0],
      params: { from: '2026-10-27', to: '2026-10-27', branchId },
    });
    expect(mockRouter.back).not.toHaveBeenCalled();
  },
);
it('uses the persisted audit precision after a confirmed granular write before the next snapshot', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  let persisted = { ...current };
  r.intercept(async (path, init) => {
    if (path === '/setting') return response({ lockDate: '2026-09-30' });
    if (path === '/timeentry/' + id) return response(persisted);
    if (path === '/timeentry/segment/' + segmentId && init.method === 'DELETE') {
      persisted = { ...persisted, segments: [], updatedAt: '2026-10-27T13:00:00.123457Z' };
      return response({ ...persisted, updatedAt: '2026-10-27T13:00:00.1234567Z' });
    }
    if (path === '/timeentry' && init.method === 'PUT') {
      persisted = { ...persisted, status: 'DayOff' };
      return response(persisted);
    }
    return undefined;
  });
  mount(r, 'time-detail');
  await waitFor(() =>
    expect(screen.getByTestId('time-remove-segment-1').props.accessibilityState.disabled).toBe(
      false,
    ),
  );
  fireEvent.press(screen.getByTestId('time-remove-segment-1'));
  fireEvent.press(await screen.findByText('Remover', { exact: true }));
  await screen.findByText('Registro salvo no servidor.');
  fireEvent.press(screen.getByTestId('time-status'));
  fireEvent.press(await screen.findByTestId('time-status-option-DayOff'));
  fireEvent.press(screen.getByTestId('time-save-day'));
  await waitFor(() => expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1));
  expect(
    screen.queryByText('O registro mudou. Confira a versão atual e seu texto antes de continuar.'),
  ).toBeNull();
  await screen.findByText('Registro salvo no servidor.');
});

it('starting today keeps yesterday open and exposes today’s Saída without an automatic close', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const yesterday = {
    ...current,
    id: '60000000-0000-4000-8000-000000000001',
    date: '2026-10-26',
    isInProgress: true,
  };
  const today = {
    ...current,
    isInProgress: true,
    segments: [{ ...current.segments[0]!, clockOut: null }],
  };
  let started = false;
  r.intercept(async (path, init) => {
    if (path === '/timeentry' && init.method === 'GET')
      return response(envelope(started ? [yesterday, today] : [yesterday]));
    if (path === '/timeentry' && init.method === 'PUT') {
      started = true;
      return response(today);
    }
    if (path === '/timeentry/' + id) return response(today);
    return undefined;
  });
  mount(r, 'clock');
  fireEvent.press(await readyClock('time-open-today'));
  await screen.findByText('Saída', { exact: true });
  expect(screen.getByText('Seu turno de ontem ainda está aberto.')).toBeTruthy();
  expect(screen.queryByTestId('time-open-today')).toBeNull();
  expect(r.calls.filter((c) => c.method === 'PUT').map((c) => c.body)).toEqual([
    { operatorId: userId, date: '2026-10-27', status: 'Present', action: 'Open' },
  ]);
});
it('requires an observed time for a new segment instead of saving a guessed punch', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto';
  mockParams = { section: 'new', operatorId: userId };
  setup(r);
  mount(r, 'time');
  await screen.findByTestId('time-prepare');
  await waitFor(() =>
    expect(screen.getByTestId('time-prepare').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-prepare'));
  await waitFor(() =>
    expect(screen.getByTestId('time-add').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-add'));
  fireEvent.press(screen.getByTestId('time-save-day'));
  expect(
    await screen.findByText(
      'Confira a data e os horários dos segmentos. Use data e hora da filial.',
    ),
  ).toBeTruthy();
  expect(r.calls.some((c) => c.method === 'PUT')).toBe(false);
});

it('retains an uncertain yesterday tap through restart/rollover and rereads its original day without replay', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  const yesterday = { ...current, date: '2026-10-26', isInProgress: true };
  r.intercept(async (path, init) =>
    path === '/timeentry'
      ? init.method === 'GET'
        ? response(envelope([yesterday]))
        : response({ errorMessages: ['Resposta perdida.'] }, 503)
      : path === '/timeentry/' + id
        ? response({ ...yesterday, segments: [] })
        : undefined,
  );
  const first = mount(r, 'clock');
  fireEvent.press(await readyClock('time-close-yesterday'));
  await screen.findByText('Reler registros');
  first.unmount();
  await act(async () => {
    r.date('2026-10-28');
    await r.engine.refresh();
  });
  mount(r, 'clock');
  expect(
    await screen.findByText(
      'Saída de 26/10/2026 precisa de conferência. Nenhum toque será reenviado automaticamente.',
    ),
  ).toBeTruthy();
  await act(async () => fireEvent.press(screen.getByText('Reler registros')));
  await screen.findByText('Registro relido de 26/10/2026');
  const targetReads = jest
    .mocked(r.transport)
    .mock.calls.map(([url]) => new URL(String(url)))
    .filter(
      (url) =>
        url.pathname === '/timeentry' &&
        url.searchParams.get('DateFrom') === '2026-10-26' &&
        url.searchParams.get('DateTo') === '2026-10-26',
    );
  expect(targetReads.length).toBeGreaterThan(0);
  expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
});

it('waits for a fresh canonical detail before exposing an editor, then accepts the first edit tap', async () => {
  const r = rig();
  r.role('Manager');
  await r.ready();
  const pending = deferred<Response>();
  let failReads = false;
  let client: QueryClient | undefined;
  function Capture() {
    const query = useQueryClient();
    useEffect(() => {
      client = query;
    }, [query]);
    return null;
  }
  function Host({ detail = false }: { detail?: boolean }) {
    return (
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ToastProvider>
          <ShellProvider>
            <Capture />
            {detail ? <TimeSurface destination="time-detail" /> : null}
          </ShellProvider>
        </ToastProvider>
      </SessionProvider>
    );
  }
  const view = renderThemed(<Host />);
  await waitFor(() => expect(client).toBeDefined());
  await act(async () => {
    client!.setQueryData(getGetTimeentryTimeEntryIdQueryKey(id), { data: current });
  });
  const corrected = {
    ...current,
    totalHours: 1.25,
    segments: [{ ...current.segments[0]!, clockIn: '2026-10-27T05:45:00' }],
  };
  r.intercept(async (path) =>
    path === '/timeentry/' + id
      ? failReads
        ? response({ errorMessages: ['Leitura temporariamente indisponível.'] }, 503)
        : pending.promise.then(() => response(corrected))
      : path === '/setting'
        ? response({ lockDate: '2026-09-30' })
        : undefined,
  );
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  view.rerenderThemed(<Host detail />);
  expect(screen.queryByTestId('time-edit-segment-1')).toBeNull();
  await act(async () => pending.resolve(response(corrected)));
  await screen.findByTestId('time-resource-ready');
  await waitFor(() =>
    expect(screen.getByTestId('time-edit-segment-1').props.accessibilityState.disabled).toBe(false),
  );
  expect(screen.getByText('27/10/2026 05:45')).toBeTruthy();
  fireEvent.press(screen.getByTestId('time-edit-segment-1'));
  expect(screen.getByTestId('segment-edit-in').props.value).toBe('27/10/2026 05:45');
  fireEvent.changeText(screen.getByTestId('segment-edit-in'), '27/10/2026 05:30');
  failReads = true;
  await act(async () => {
    await client!.invalidateQueries({ queryKey: getGetTimeentryTimeEntryIdQueryKey(id) });
  });
  await screen.findByText('Leitura temporariamente indisponível.');
  expect(screen.getByTestId('segment-edit-in').props.value).toBe('27/10/2026 05:30');
  expect(screen.getByTestId('time-save-segment').props.accessibilityState.disabled).toBe(true);
  failReads = false;
  fireEvent.press(screen.getByText('Tentar novamente'));
  await waitFor(() =>
    expect(screen.getByTestId('time-save-segment').props.accessibilityState.disabled).toBe(false),
  );
  expect(screen.getByTestId('segment-edit-in').props.value).toBe('27/10/2026 05:30');
});

it('reports acknowledgement failure after a confirmed write as local reconciliation, never save failure or replay', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  r.intercept(async (path) =>
    path === '/timeentry/' + id || path === '/timeentry'
      ? response(current)
      : path === '/setting'
        ? response({ lockDate: '2026-09-30' })
        : undefined,
  );
  mount(r, 'time-detail');
  await screen.findByTestId('time-resource-ready');
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  const ack = jest
    .spyOn(DeviceDraftStore.prototype, 'acknowledge')
    .mockRejectedValueOnce(new Error('Armazenamento indisponível.'));
  try {
    fireEvent.press(screen.getByTestId('time-save-day'));
    await screen.findByText('Registro salvo no servidor. A conferência local não foi concluída.');
    expect(screen.queryByText('Não foi possível salvar.')).toBeNull();
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(true);
    fireEvent.press(screen.getByText('Reler registro para reconciliar'));
    await screen.findByText('Registro atual para revisão');
    expect(r.calls.filter((call) => call.method === 'PUT')).toHaveLength(1);
  } finally {
    ack.mockRestore();
  }
});

it('keeps the created editor retired if navigation fails, and retries only opening the confirmed resource', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto';
  mockParams = { section: 'new', operatorId: userId };
  r.intercept(async (path, init) =>
    path === '/operator'
      ? response({ operators: [{ id: userId, name: 'Pessoa teste', branchId }] })
      : path === '/setting'
        ? response({ lockDate: '2026-09-30' })
        : path === '/timeentry/' + id
          ? response(current)
          : path === '/timeentry'
            ? init.method === 'GET'
              ? response(envelope([]))
              : response(current)
            : undefined,
  );
  mockRouter.replace.mockImplementationOnce(() => {
    throw new Error('Navegação indisponível.');
  });
  mount(r, 'time');
  await waitFor(() =>
    expect(screen.getByTestId('time-prepare').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-prepare'));
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-save-day'));
  await screen.findByText('Navegação indisponível.');
  expect(screen.queryByText('Não foi possível salvar.')).toBeNull();
  expect(screen.queryByTestId('time-edit-segment-1')).toBeNull();
  expect(screen.queryByTestId('time-save-day')).toBeNull();
  await act(async () => fireEvent.press(screen.getByTestId('time-open-confirmed')));
  expect(mockRouter.replace).toHaveBeenCalledTimes(2);
  expect(r.calls.filter((call) => call.method === 'PUT')).toHaveLength(1);
});

it('keeps a confirmed Member tap distinct from an acknowledgement failure and never repeats it', async () => {
  const r = rig();
  r.role('Member');
  await r.ready();
  r.intercept(async (path, init) =>
    path === '/timeentry'
      ? init.method === 'GET'
        ? response(envelope([]))
        : response(current)
      : undefined,
  );
  const ack = jest
    .spyOn(DeviceDraftStore.prototype, 'acknowledge')
    .mockRejectedValueOnce(new Error('Confirmação local indisponível.'));
  try {
    mount(r, 'clock');
    fireEvent.press(await readyClock());
    await screen.findByText('Ponto registrado no servidor. A conferência local não foi concluída.');
    expect(screen.queryByText('Não foi possível registrar o ponto.')).toBeNull();
    fireEvent.press(screen.getByText('Reler registros'));
    await screen.findByText('Registro relido de 27/10/2026');
    expect(r.calls.filter((call) => call.method === 'PUT')).toHaveLength(1);
  } finally {
    ack.mockRestore();
  }
});

it('reconciles a failed reread after a successful snapshot without calling the write a failure', async () => {
  const r = rig();
  await r.ready();
  mockPath = '/gestao/ponto/' + id;
  mockParams = { id };
  let written = false;
  r.intercept(async (path, init) => {
    if (path === '/setting') return response({ lockDate: '2026-09-30' });
    if (path === '/timeentry' && init.method === 'PUT') {
      written = true;
      return response(current);
    }
    if (path === '/timeentry/' + id)
      return written
        ? response({ errorMessages: ['Releitura indisponível.'] }, 503)
        : response(current);
    return undefined;
  });
  mount(r, 'time-detail');
  await screen.findByTestId('time-resource-ready');
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-save-day'));
  await screen.findByText('Registro salvo no servidor. A conferência local não foi concluída.');
  expect(screen.getByText('Releitura indisponível.')).toBeTruthy();
  expect(screen.queryByText('Não foi possível salvar.')).toBeNull();
  expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(true);
  written = false;
  fireEvent.press(screen.getByText('Reler registro para reconciliar'));
  await screen.findByText('Registro atual para revisão');
  expect(r.calls.filter((call) => call.method === 'PUT')).toHaveLength(1);
});
