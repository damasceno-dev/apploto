import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { Component, useEffect as mockEffect, type ReactNode } from 'react';
import { Text } from 'react-native';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { ToastProvider } from '../../src/ui';
import { ManagerSurface, type ManagerSurfaceKey } from '../../src/manager/surface';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig } from './support';
import { renderThemed } from './ui/support';
import { ids, managerServer } from './manager-server';

let mockPath = '/gestao/fila';
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
let mockKey = 0;
jest.mock('../../src/storage/native', () => ({
  ...jest.requireActual('../../src/storage/native'),
  secureUuid: () => `40000000-0000-4000-8000-${String(++mockKey).padStart(12, '0')}`,
  emailPartitionKey: async (email: string) => 'hash:' + email,
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));

beforeEach(async () => {
  mockPath = '/gestao/fila';
  mockParams = {};
  jest.clearAllMocks();
  jest
    .spyOn(jest.requireActual('react-native').AccessibilityInfo, 'isScreenReaderEnabled')
    .mockResolvedValue(false);
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
});

async function setup(role = 'Manager') {
  const r = rig();
  r.role(role);
  const server = managerServer();
  r.intercept((path, init) => server.handle(path, init, r.calls));
  await r.ready();
  return { r, server };
}
class Boundary extends Component<{ children: ReactNode }, { error: Error | null }> {
  state = { error: null as Error | null };
  static getDerivedStateFromError(error: Error) {
    return { error };
  }
  render() {
    return this.state.error ? (
      <Text testID="render-error">{String(this.state.error.stack)}</Text>
    ) : (
      this.props.children
    );
  }
}
function mount(
  r: ReturnType<typeof rig>,
  destination: ManagerSurfaceKey,
  preserveVerifiedView = false,
) {
  return renderThemed(
    <Boundary>
      <ToastProvider>
        <SessionProvider engine={r.engine} lifecycle={false}>
          <ShellProvider>
            <ManagerSurface destination={destination} preserveVerifiedView={preserveVerifiedView} />
          </ShellProvider>
        </SessionProvider>
      </ToastProvider>
    </Boundary>,
  );
}
const posts = (r: ReturnType<typeof rig>, suffix: string) =>
  r.calls.filter((c) => c.method === 'POST' && c.path.endsWith(suffix));
const press = async (testID: string) => {
  const target = await screen.findByTestId(testID);
  await waitFor(() =>
    expect(screen.getByTestId(testID).props.accessibilityState?.disabled).not.toBe(true),
  );
  await act(async () => fireEvent.press(target));
};

describe('Fila de trabalho', () => {
  it('refuses a Member before any read', async () => {
    const { r } = await setup('Member');
    mount(r, 'queue');
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    expect(r.calls.some((c) => c.path.startsWith('/report/manager-queue'))).toBe(false);
  });

  it('renders the fixed groups from ONE queue read and routes rows by status', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, status: 'Submitted', variance: -5 });
    server.add({ id: ids.s2, status: 'Approved', sequence: 2, variance: -2 });
    server.add({ id: ids.rejected, status: 'Rejected', variance: null });
    server.add({ id: ids.draft, status: 'Draft', variance: null });
    mount(r, 'queue');
    expect(await screen.findByTestId('queue-group-pending')).toBeTruthy();
    expect(r.calls.filter((c) => c.path === '/report/manager-queue')).toHaveLength(1);
    // Separate reads by design: lock date, reconciliation and requests are not embedded.
    expect(r.calls.some((c) => c.path === '/setting')).toBe(true);
    expect(r.calls.some((c) => c.path.startsWith('/report/monthly-reconciliation'))).toBe(true);
    expect(r.calls.some((c) => c.path === '/operator-link-request')).toBe(true);
    // Session-list reconstruction is forbidden.
    expect(r.calls.some((c) => c.path === '/session')).toBe(false);
    expect(screen.getByTestId('queue-variance-variance-' + ids.s1).props.children).toContain(
      'Diferença',
    );
    await press('queue-open-pending-' + ids.s1);
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(JSON.stringify(mockRouter.push.mock.calls[0])).toContain('conferencia');
    mockRouter.push.mockClear();
    await press('queue-open-rejected-' + ids.rejected);
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(JSON.stringify(mockRouter.push.mock.calls[0])).toContain('correcao');
  });

  it('a failed setting read is a partial failure, never a fabricated lock date', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.fail.set('GET /setting', {
      status: 500,
      body: { errorMessages: ['Falha ao ler.'] },
    });
    mount(r, 'queue');
    expect(await screen.findByText('Não foi possível ler a data de bloqueio.')).toBeTruthy();
    expect(screen.getByText('Falha ao ler.')).toBeTruthy();
    expect(screen.getByTestId('queue-lock-date').props.children).toBe(
      'Data de bloqueio indisponível.',
    );
    // The queue itself still rendered from its own read.
    expect(screen.getByTestId('queue-group-pending')).toBeTruthy();
  });

  it('backlog banner uses returned counts and jumps the selected date', async () => {
    const { r, server } = await setup();
    server.state.backlog = {
      submittedCount: 2,
      rejectedCount: 1,
      draftCount: 3,
      oldest: '2026-10-20T00:00:00',
    };
    mount(r, 'queue');
    expect(await screen.findByTestId('queue-backlog')).toBeTruthy();
    expect(screen.queryByTestId('queue-all-clear')).toBeNull();
    await press('queue-backlog-jump');
    await waitFor(() => expect(mockRouter.replace).toHaveBeenCalled());
    expect(JSON.stringify(mockRouter.replace.mock.calls.at(-1))).toContain('"date":"2026-10-20"');
  });

  it('shows all-clear only when every returned region is empty', async () => {
    const { r } = await setup();
    mount(r, 'queue');
    expect(await screen.findByTestId('queue-all-clear')).toBeTruthy();
  });
});

describe('Conferência', () => {
  beforeEach(() => {
    mockPath = `/gestao/conferencia/${ids.s1}`;
    mockParams = { id: ids.s1 };
  });
  it('independent approval sends the current If-Match and one new key, then no second POST', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await press('review-approve');
    await press('approve-dialog-confirm');
    await screen.findByTestId('review-receipt');
    const sent = posts(r, `/${ids.s1}/approve`);
    expect(sent).toHaveLength(1);
    expect(sent[0]!.headers['if-match']).toBe('"10"');
    expect(sent[0]!.headers['idempotency-key']).toMatch(/^[0-9a-f-]{36}$/);
    expect(sent[0]!.body).toEqual({ mode: 'Independent' });
    // The decision is recorded: the approve control is gone, not re-sendable.
    expect(screen.queryByTestId('review-approve')).toBeNull();
  });

  it('shows the server blocked reason verbatim and offers no approval', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, canApprove: false, blocked: 'Quem enviou não pode aprovar.' });
    mount(r, 'approval');
    expect(await screen.findByText('Quem enviou não pode aprovar.')).toBeTruthy();
    expect(screen.getByTestId('review-approve').props.accessibilityState?.disabled).toBe(true);
    expect(posts(r, '/approve')).toHaveLength(0);
  });

  it('single-manager approval needs the reason and sends the reasoned mode', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, canApprove: false, canSingle: true });
    mount(r, 'approval');
    const reason = await screen.findByTestId('approve-reason');
    expect(screen.getByTestId('review-approve').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.changeText(reason, 'Único gestor ativo'));
    await press('review-approve');
    await press('approve-dialog-confirm');
    await screen.findByTestId('review-receipt');
    expect(posts(r, '/approve')[0]!.body).toEqual({
      mode: 'SingleActiveManager',
      reason: 'Único gestor ativo',
    });
  });

  it('a stale version is refused verbatim and nothing is approved', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await screen.findByTestId('review-approve');
    server.state.reviews.get(ids.s1)!.version = 11;
    await press('review-approve');
    await press('approve-dialog-confirm');
    expect(await screen.findByText('O fechamento mudou. Releia antes de aprovar.')).toBeTruthy();
    expect(server.state.reviews.get(ids.s1)!.status).toBe('Submitted');
    expect(screen.queryByTestId('review-receipt')).toBeNull();
  });

  it('rejection keeps its reason, sends it once and is never replayed after an unknown outcome', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await press('review-reject');
    await act(async () =>
      fireEvent.changeText(await screen.findByTestId('reject-reason'), 'Contagem errada'),
    );
    await press('reject-dialog-confirm');
    await screen.findByTestId('review-receipt');
    const sent = posts(r, '/reject');
    expect(sent).toHaveLength(1);
    expect(sent[0]!.body).toEqual({ rejectionReason: 'Contagem errada' });
  });

  it('reopen is a distinct verb without If-Match or key', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, status: 'Approved' });
    mount(r, 'approval');
    await press('review-reopen');
    await press('reopen-dialog-confirm');
    await screen.findByTestId('review-receipt');
    const sent = posts(r, '/reopen');
    expect(sent).toHaveLength(1);
    expect(sent[0]!.headers['idempotency-key']).toBeUndefined();
  });

  it('shows recorder, submitter and custodian separately and the server official difference', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    expect(await screen.findByTestId('review-recorder')).toBeTruthy();
    expect(screen.getByTestId('review-submitter')).toBeTruthy();
    expect(screen.getByTestId('review-custodian')).toBeTruthy();
    expect(screen.getByTestId('review-official-difference').props.children).toContain(
      'Diferença oficial',
    );
  });

  it('a Member cannot get manager content by deep link', async () => {
    const { r, server } = await setup('Member');
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    expect(await screen.findByText('Seu perfil não tem acesso a este destino.')).toBeTruthy();
    expect(r.calls.some((c) => c.path.endsWith('/review'))).toBe(false);
  });
});

describe('Aprovação do dia', () => {
  it('sends the exact SessionId+Version list under one key and approves all', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, sequence: 1 });
    server.add({ id: ids.s2, sequence: 2 });
    mount(r, 'queue');
    await press(`batch-open-${ids.box}`);
    await press('batch-dialog-confirm');
    await screen.findByTestId('queue-batch-receipt');
    const sent = posts(r, '/approve-day');
    expect(sent).toHaveLength(1);
    expect(sent[0]!.headers['idempotency-key']).toBeTruthy();
    expect(sent[0]!.body).toEqual({
      sessions: [
        { sessionId: ids.s1, version: 10 },
        { sessionId: ids.s2, version: 10 },
      ],
      mode: 'Independent',
    });
  });

  it('one stale row refuses the whole action: nothing is approved and the lines stay verbatim', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, sequence: 1 });
    server.add({ id: ids.s2, sequence: 2 });
    mount(r, 'queue');
    await press(`batch-open-${ids.box}`);
    await screen.findByTestId(`batch-session-${ids.s2}`);
    server.state.reviews.get(ids.s2)!.version = 11;
    await press('batch-dialog-confirm');
    expect(await screen.findByText(`Sessão ${ids.s2}: versão desatualizada.`)).toBeTruthy();
    expect(server.state.reviews.get(ids.s1)!.status).toBe('Submitted');
    expect(server.state.reviews.get(ids.s2)!.status).toBe('Submitted');
    expect(posts(r, '/approve')).toHaveLength(0);
  });

  it('a row the server does not allow blocks the whole group (no silent subset)', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, sequence: 1 });
    server.add({
      id: ids.s2,
      sequence: 2,
      canApprove: false,
      blocked: 'Quem enviou não pode aprovar.',
    });
    mount(r, 'queue');
    await press(`batch-open-${ids.box}`);
    expect(await screen.findByText('Quem enviou não pode aprovar.')).toBeTruthy();
    expect(screen.getByTestId('batch-dialog-confirm').props.accessibilityState?.disabled).toBe(
      true,
    );
    expect(posts(r, '/approve-day')).toHaveLength(0);
  });
});

describe('Divergência de abertura', () => {
  beforeEach(() => {
    mockPath = `/gestao/conferencia/${ids.discSession}`;
    mockParams = { id: ids.discSession };
  });
  it('current period attribution sends the current revision, If-Match and a reason', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.discSession });
    server.state.discrepancy = {
      version: 4,
      revision: 2,
      locked: false,
      targets: ['Incoming', 'Neither'],
    };
    mount(r, 'approval');
    await screen.findByTestId('discrepancy-panel');
    // Outgoing is not offered, so no such option exists.
    expect(screen.queryByTestId('discrepancy-target-Outgoing')).toBeNull();
    await act(async () =>
      fireEvent.press(await screen.findByTestId('discrepancy-target-Incoming')),
    );
    await act(async () =>
      fireEvent.changeText(screen.getByTestId('discrepancy-reason'), 'Contado na entrada'),
    );
    await press('discrepancy-submit');
    await screen.findByTestId('discrepancy-receipt');
    const sent = posts(r, `/${ids.disc}/attribution`);
    expect(sent).toHaveLength(1);
    expect(sent[0]!.headers['if-match']).toBe('"4"');
    expect(sent[0]!.body).toEqual({
      discrepancyRevision: 2,
      target: 'Incoming',
      reason: 'Contado na entrada',
    });
  });
  it('a locked source uses the M9.7 adjustment with an open PostedOn, not the old attribution', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.discSession });
    server.state.discrepancy = {
      version: 4,
      revision: 2,
      locked: true,
      targets: ['Incoming', 'Outgoing'],
    };
    mount(r, 'approval');
    await screen.findByTestId('adjust-posted-on');
    expect(screen.getByTestId('discrepancy-source-date').props.children).toContain('bloqueado');
    await act(async () =>
      fireEvent.press(await screen.findByTestId('discrepancy-target-Outgoing')),
    );
    await act(async () =>
      fireEvent.changeText(screen.getByTestId('discrepancy-reason'), 'Correção tardia'),
    );
    await press('discrepancy-submit');
    await screen.findByTestId('discrepancy-receipt');
    expect(posts(r, '/attribution')).toHaveLength(0);
    const sent = posts(r, '/opening-discrepancy-adjustment');
    expect(sent).toHaveLength(1);
    expect(sent[0]!.headers['idempotency-key']).toBeTruthy();
    expect(sent[0]!.body).toMatchObject({
      discrepancyId: ids.disc,
      expectedVersion: 4,
      comparisonRevision: 2,
      expectedAdjustmentSequence: 0,
      target: 'Outgoing',
      postedOn: '2026-10-27',
      reason: 'Correção tardia',
    });
  });
});

describe('Resultado desconhecido e escopo', () => {
  beforeEach(() => {
    mockPath = `/gestao/conferencia/${ids.s1}`;
    mockParams = { id: ids.s1 };
  });
  it('an approval whose response is lost is verified with the SAME key and If-Match, never a new decision', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.dropAfter.add(`POST /session/${ids.s1}/approve`);
    mount(r, 'approval');
    await press('review-approve');
    await press('approve-dialog-confirm');
    await screen.findByTestId('approve-uncertain');
    // The server executed it: nothing may enable a second, different decision.
    expect(server.state.reviews.get(ids.s1)!.status).toBe('Approved');
    await press('approve-retry');
    await screen.findByTestId('review-receipt');
    const sent = posts(r, `/${ids.s1}/approve`);
    expect(sent).toHaveLength(2);
    expect(sent[1]!.headers['idempotency-key']).toBe(sent[0]!.headers['idempotency-key']);
    expect(sent[1]!.headers['if-match']).toBe(sent[0]!.headers['if-match']);
  });

  it('a rejection whose response is lost is NEVER replayed: only a fresh read resolves it', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.dropAfter.add(`POST /session/${ids.s1}/reject`);
    mount(r, 'approval');
    await press('review-reject');
    await act(async () =>
      fireEvent.changeText(await screen.findByTestId('reject-reason'), 'Motivo mantido'),
    );
    await press('reject-dialog-confirm');
    await screen.findByTestId('reject-uncertain');
    expect(screen.queryByTestId('reject-retry')).toBeNull();
    await press('reject-reread');
    await waitFor(() => expect(screen.queryByTestId('review-reject')).toBeNull());
    expect(posts(r, '/reject')).toHaveLength(1);
    expect(server.state.reviews.get(ids.s1)!.status).toBe('Rejected');
  });

  it('a confirmed decision survives a failed reread and is never sent again', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await screen.findByTestId('review-approve');
    server.state.fail.set(`GET /session/${ids.s1}/review`, { status: 500 });
    await press('review-approve');
    await press('approve-dialog-confirm');
    await waitFor(() => expect(posts(r, `/${ids.s1}/approve`)).toHaveLength(1));
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(posts(r, `/${ids.s1}/approve`)).toHaveLength(1);
    expect(server.state.reviews.get(ids.s1)!.status).toBe('Approved');
  });

  it('a transient foreground change keeps the review, the open dialog and the typed reason', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    // As the shell mounts a verified view: kept (paused) through a same-authority change.
    mount(r, 'approval', true);
    await press('review-reject');
    await act(async () =>
      fireEvent.changeText(await screen.findByTestId('reject-reason'), 'Motivo digitado'),
    );
    await act(async () => {
      await r.engine.foreground(false);
    });
    expect(screen.queryByText('Seu perfil não tem acesso a este destino.')).toBeNull();
    expect(screen.getByTestId('reject-reason').props.value).toBe('Motivo digitado');
    await act(async () => {
      await r.engine.foreground(true);
    });
    expect(screen.getByTestId('reject-reason').props.value).toBe('Motivo digitado');
    expect(posts(r, '/reject')).toHaveLength(0);
  });

  it('a credential renewal keeps the review, the open dialog and the typed reason', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval', true);
    await press('review-reject');
    await act(async () =>
      fireEvent.changeText(await screen.findByTestId('reject-reason'), 'Motivo renovado'),
    );
    // Identity and branch tokens expire; the engine renews them while the dialog stays open.
    r.expire();
    r.advance(120000);
    await act(async () => {
      await r.engine.refresh();
    });
    expect(screen.queryByText('Seu perfil não tem acesso a este destino.')).toBeNull();
    expect(screen.getByTestId('reject-reason').props.value).toBe('Motivo renovado');
    expect(posts(r, '/reject')).toHaveLength(0);
  });

  it('a role reduction removes manager content and issues no further manager read', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mockPath = '/gestao/fila';
    mockParams = {};
    mount(r, 'queue');
    await screen.findByTestId('queue-group-pending');
    const before = r.calls.filter((c) => c.path === '/report/manager-queue').length;
    r.role('Member');
    await act(async () => {
      await r.engine.selectBranch(r.engine.getSnapshot().branch!.id);
    });
    await screen.findByText('Seu perfil não tem acesso a este destino.');
    expect(screen.queryByTestId('queue-group-pending')).toBeNull();
    expect(r.calls.filter((c) => c.path === '/report/manager-queue').length).toBe(before);
  });
});

describe('Retorno com foco', () => {
  afterEach(() => jest.restoreAllMocks());

  it('asks the screen reader for focus on the returned row once the fresh read arrives', async () => {
    const { AccessibilityInfo } = jest.requireActual('react-native');
    const enabled = jest.spyOn(AccessibilityInfo, 'isScreenReaderEnabled').mockResolvedValue(true);
    const send = jest.spyOn(AccessibilityInfo, 'sendAccessibilityEvent').mockImplementation();
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mockParams = { focus: ids.s1 };
    mount(r, 'queue');
    await screen.findByTestId('queue-group-pending');
    await waitFor(() => expect(send).toHaveBeenCalledWith(expect.anything(), 'focus'));
    enabled.mockRestore();
    send.mockRestore();
  });
});

describe('Revisão independente M7', () => {
  beforeEach(() => {
    mockParams = { id: ids.s1 };
  });

  it('a server mean with many decimals is shown exactly and never breaks the queue', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.meanVariance = -1 / 3;
    mount(r, 'queue');
    await screen.findByTestId('queue-summary');
    expect(screen.queryByTestId('render-error')).toBeNull();
    expect(screen.getByText(/0,3333333333333333/)).toBeTruthy();
  });

  it('warns by identity when the attempt names an opening product the rows omit', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.attemptItems = [
      { productId: ids.prod, productName: 'Bolão Mega', openingValue: 100 },
      { productId: ids.s3, productName: 'Taxa retirada', openingValue: 7 },
    ];
    mount(r, 'approval');
    expect(await screen.findByTestId('review-explanation-differs')).toBeTruthy();
    expect(screen.getByText(/Abertura não exibida: Taxa retirada/)).toBeTruthy();
    // Ordinary permissions stay.
    expect(screen.getByTestId('review-approve').props.accessibilityState?.disabled).not.toBe(true);
  });

  it('warns when the attempt difference is not the official value, and stays quiet otherwise', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.attemptDifference = -4;
    mount(r, 'approval');
    expect(await screen.findByTestId('review-explanation-differs')).toBeTruthy();
  });

  it('shows no explanation warning when identities and the official value agree', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await screen.findByTestId('review-comparison');
    expect(screen.queryByTestId('review-explanation-differs')).toBeNull();
  });

  it('lists the day ledger with the Automática marker', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    expect(await screen.findByTestId(`review-ledger-${ids.s1.replace('101', '902')}`)).toBeTruthy();
    expect(screen.getByText('Venda manual')).toBeTruthy();
    expect(screen.getAllByText('Automática').length).toBeGreaterThan(0);
  });

  it('keeps the confirmed receipt visible when the reread fails', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await screen.findByTestId('review-approve');
    server.state.fail.set(`GET /session/${ids.s1}/review`, { status: 500 });
    await press('review-approve');
    await press('approve-dialog-confirm');
    expect(await screen.findByTestId('review-receipt')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('review-error')).toBeTruthy());
    expect(screen.getByTestId('review-receipt')).toBeTruthy();
  });

  it('an unknown rejection outcome blocks Aprovar until a fresh read', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.dropAfter.add(`POST /session/${ids.s1}/reject`);
    mount(r, 'approval');
    await press('review-reject');
    await act(async () =>
      fireEvent.changeText(await screen.findByTestId('reject-reason'), 'Motivo'),
    );
    await press('reject-dialog-confirm');
    await screen.findByTestId('reject-uncertain');
    expect(screen.getByTestId('review-approve').props.accessibilityState?.disabled).toBe(true);
    expect(posts(r, '/approve')).toHaveLength(0);
  });

  it('the confirmed batch is never silently replaced by a newer read, and a failed read blocks it', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, sequence: 1 });
    server.add({ id: ids.s2, sequence: 2 });
    server.state.dropAfter.add('POST /session/approve-day');
    mount(r, 'queue');
    await press(`batch-open-${ids.box}`);
    await screen.findByTestId(`batch-session-${ids.s2}`);
    await press('batch-dialog-confirm');
    await screen.findByTestId('batch-uncertain');
    // The queue keeps its previous rows when its own refresh fails, so the dialog stays open.
    server.state.fail.set('GET /report/manager-queue', { status: 500 });
    await act(async () => fireEvent.press(await screen.findByText('Reler e conferir')));
    // The executed batch changed what is approvable: the new list needs an explicit review.
    await screen.findByTestId('batch-changed');
    expect(screen.getByTestId('batch-dialog-confirm').props.accessibilityState?.disabled).toBe(
      true,
    );
    expect(posts(r, '/approve-day')).toHaveLength(1);
  });

  it('a failed batch reread keeps Aprovar todas disabled', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1, sequence: 1 });
    server.add({ id: ids.s2, sequence: 2 });
    server.state.dropAfter.add('POST /session/approve-day');
    mount(r, 'queue');
    await press(`batch-open-${ids.box}`);
    await screen.findByTestId(`batch-session-${ids.s2}`);
    await press('batch-dialog-confirm');
    await screen.findByTestId('batch-uncertain');
    server.state.fail.set('GET /report/manager-queue', { status: 500 });
    server.state.fail.set(`GET /session/${ids.s2}/review`, { status: 500 });
    await act(async () => fireEvent.press(await screen.findByText('Reler e conferir')));
    await screen.findByTestId('batch-read-error');
    expect(screen.getByTestId('batch-dialog-confirm').props.accessibilityState?.disabled).toBe(
      true,
    );
    expect(posts(r, '/approve-day')).toHaveLength(1);
  });

  it('responsibility follows the current specific read, not the review copy', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.discSession });
    mockParams = { id: ids.discSession };
    server.state.discrepancy = {
      version: 4,
      revision: 2,
      locked: false,
      targets: ['Incoming', 'Outgoing', 'Neither'],
    };
    server.state.responsibilityTargets = ['Neither'];
    mount(r, 'approval');
    await screen.findByTestId('discrepancy-target-Neither');
    expect(screen.queryByTestId('discrepancy-target-Outgoing')).toBeNull();
    expect(screen.queryByTestId('discrepancy-target-Incoming')).toBeNull();
  });

  it('loads further adjustments through nextAfterSequence instead of truncating at 20', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.discSession });
    mockParams = { id: ids.discSession };
    server.state.discrepancy = { version: 4, revision: 2, locked: true, targets: ['Incoming'] };
    server.state.pagedAdjustments = 25;
    mount(r, 'approval');
    await screen.findByText(/Ajuste 20 · /);
    expect(screen.queryByText(/Ajuste 21 · /)).toBeNull();
    await press('adjustments-more');
    expect(await screen.findByText(/Ajuste 25 · /)).toBeTruthy();
  });

  it('a failed reconciliation read is unknown, never zero or all-clear', async () => {
    const { r, server } = await setup();
    server.state.reconciliationFails = true;
    mount(r, 'queue');
    await screen.findByTestId('queue-blockers-unknown');
    expect(screen.queryByText('Bloqueios do mês (0)')).toBeNull();
    expect(screen.queryByTestId('queue-all-clear')).toBeNull();
  });

  it('the selected day stays put when the branch clock crosses midnight', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'queue');
    await screen.findByTestId('queue-summary');
    const reads = () => r.calls.filter((c) => c.path === '/report/manager-queue');
    expect(reads()[0]!.url).toContain('2026-10-27');
    r.date('2026-10-28');
    await act(async () => {
      await r.engine.foreground(false);
      await r.engine.foreground(true);
    });
    expect(reads().every((c) => c.url.includes('2026-10-27'))).toBe(true);
  });

  it('returns people at the threshold with their values and never hides them', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.alertPeople = [
      {
        operatorId: ids.s3,
        operatorName: 'Maria',
        operatorActive: true,
        shortage: 40,
        overage: 5,
        signedNet: -35,
        thresholdShortageBasis: 40,
        sourceCount: 3,
        currentThresholdAlert: true,
      },
    ];
    mount(r, 'queue');
    expect(await screen.findByText('Limite de faltas atingido')).toBeTruthy();
    expect(screen.getByText(/Maria/)).toBeTruthy();
    expect(screen.getByText(/Faltas consideradas no limite: R\$.40,00/)).toBeTruthy();
  });

  it('opens Draft transactions without a start tied to the oldest pending close', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.unlockedDrafts = 2;
    server.state.backlog = {
      submittedCount: 1,
      rejectedCount: 0,
      draftCount: 0,
      oldest: '2026-10-20',
    };
    mount(r, 'queue');
    await press('queue-open-drafts');
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    const target = JSON.stringify(mockRouter.push.mock.calls[0]);
    expect(target).toContain('"status":"Draft"');
    expect(target).not.toContain('"from"');
  });

  it('does not offer a dead link for link requests', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.pendingLinks = 1;
    mount(r, 'queue');
    await press('queue-open-requests');
    await waitFor(() => expect(mockRouter.push).toHaveBeenCalled());
    expect(JSON.stringify(mockRouter.push.mock.calls[0])).not.toContain('tab=requests');
    expect(screen.getByTestId('queue-requests-boundary')).toBeTruthy();
  });

  it('the official difference is the persisted row, and a failed history read cannot be cleared by the fallback', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.attemptDifference = -4;
    server.state.fail.set(`GET /session/${ids.s1}/history`, { status: 500 });
    mount(r, 'approval');
    expect(await screen.findByTestId('review-explanation-unchecked')).toBeTruthy();
    expect(screen.getByTestId('review-official-difference').props.children).toContain('5,00');
    expect(screen.queryByTestId('review-explanation-differs')).toBeNull();
  });

  it('paginates the day ledger instead of stopping at the first 100', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    server.state.manyTransactions = 130;
    mount(r, 'approval');
    await screen.findByText('Lançamento 100');
    expect(screen.queryByText('Lançamento 101')).toBeNull();
    await press('review-ledger-load-more');
    expect(await screen.findByText('Lançamento 130')).toBeTruthy();
    expect(screen.queryByTestId('review-ledger-load-more')).toBeNull();
  });

  it('keeps attribution disabled when the responsibility refresh fails', async () => {
    const { r, server } = await setup();
    mockParams = { id: ids.discSession };
    server.add({ id: ids.discSession });
    server.state.discrepancy = { version: 4, revision: 2, locked: false, targets: ['Incoming'] };
    mount(r, 'approval');
    await screen.findByTestId('discrepancy-target-Incoming');
    server.state.fail.set(`GET /session/opening-discrepancy/${ids.disc}/responsibility`, {
      status: 500,
    });
    await act(async () => fireEvent.press(await screen.findByTestId('review-refresh')));
    await waitFor(() =>
      expect(screen.getByTestId('discrepancy-submit').props.accessibilityState?.disabled).toBe(
        true,
      ),
    );
    expect(posts(r, '/attribution')).toHaveLength(0);
  });

  it('a failed reconciliation refresh never shows a cached all-clear', async () => {
    const { r, server } = await setup();
    mount(r, 'queue');
    await screen.findByTestId('queue-all-clear');
    server.state.reconciliationFails = true;
    await act(async () => fireEvent.press(await screen.findByTestId('queue-refresh')));
    await screen.findByTestId('queue-blockers-unknown');
    expect(screen.queryByTestId('queue-all-clear')).toBeNull();
  });

  it('a pending history refresh cannot establish agreement', async () => {
    const { r, server } = await setup();
    server.add({ id: ids.s1 });
    mount(r, 'approval');
    await screen.findByTestId('review-comparison');
    expect(screen.queryByTestId('review-explanation-unchecked')).toBeNull();
    server.state.hold.add(`GET /session/${ids.s1}/history`);
    await act(async () => fireEvent.press(await screen.findByTestId('review-refresh')));
    expect(await screen.findByTestId('review-explanation-unchecked')).toBeTruthy();
  });
});
