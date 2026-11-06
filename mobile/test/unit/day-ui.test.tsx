import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { Component, useEffect as mockEffect, type ReactNode } from 'react';
import { Text } from 'react-native';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { ToastProvider } from '../../src/ui';
import { DaySurface, type DaySurfaceKey } from '../../src/day/surface';
import { autosaveTiming } from '../../src/day/count';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig, deferred, userId, branchId } from './support';
import { renderThemed } from './ui/support';
import { dayServer, ids } from './day-server';

let mockPath = '/turno/hoje';
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
  mockPath = '/turno/hoje';
  mockParams = {};
  autosaveTiming.delayMs = 20;
  jest.clearAllMocks();
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
});

async function setup(options: Parameters<typeof dayServer>[0] = {}) {
  const r = rig();
  r.role(options.role ?? 'Member');
  const server = dayServer(options);
  r.intercept((path, init) => server.handle(path, init, r.calls));
  await r.ready();
  return { r, server };
}
/** Surfaces a render error as text so a crash fails with its stack instead of an empty tree. */
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
function mount(r: ReturnType<typeof rig>, destination: DaySurfaceKey) {
  return renderThemed(
    <Boundary>
      <ToastProvider>
        <SessionProvider engine={r.engine} lifecycle={false}>
          <ShellProvider>
            <DaySurface destination={destination} />
          </ShellProvider>
        </SessionProvider>
      </ToastProvider>
    </Boundary>,
  );
}
const writes = (r: ReturnType<typeof rig>, suffix: string) =>
  r.calls.filter((c) => c.method !== 'GET' && c.path.endsWith(suffix));
const enabled = async (testID: string) => {
  await waitFor(() =>
    expect(screen.getByTestId(testID).props.accessibilityState?.disabled).not.toBe(true),
  );
  return screen.getByTestId(testID);
};
const toggle = async (testID: string) => {
  const target = await screen.findByTestId(testID);
  await act(async () => fireEvent(target, 'valueChange', true));
};
const press = async (testID: string) => {
  const target = await enabled(testID);
  await act(async () => fireEvent.press(target));
};

describe('Hoje (cockpit)', () => {
  it('one cashbox binds automatically; a successful empty read is Dia ainda não aberto', async () => {
    const { r } = await setup();
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-not-open')).toBeTruthy();
    expect(screen.getByText('Dia ainda não aberto')).toBeTruthy();
    expect(screen.getByTestId('cockpit-context').props.children).toContain('Caixa 1');
    expect(screen.queryByTestId('cockpit-choose')).toBeNull();
    // Today's movements and the Draft count come from the exact (cashbox, branch day).
    const drafts = r.calls.filter(
      (c) => c.path === '/transaction' && c.url.includes('Status=Draft'),
    );
    expect(
      drafts.every(
        (c) => c.url.includes(`AccountId=${ids.box}`) && c.url.includes('DateFrom=2026-10-27'),
      ),
    ).toBe(true);
    // No financial totals: the cockpit is an action hub.
    expect(screen.queryByText(/Total/)).toBeNull();
    await press('cockpit-next-cta');
  });
  it('a failed proposal read never becomes Dia ainda não aberto', async () => {
    const { r, server } = await setup();
    server.state.fail.set('GET /session/opening-proposal', { status: 500 });
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-unknown')).toBeTruthy();
    expect(screen.queryByText('Dia ainda não aberto')).toBeNull();
    server.state.fail.clear();
    await act(async () => fireEvent.press(screen.getByText('Tentar novamente')));
    expect(await screen.findByTestId('cockpit-next-not-open')).toBeTruthy();
  });
  it('several cashboxes ask once per branch day; the choice is kept per identity and branch', async () => {
    const { r } = await setup({ grants: [ids.box, ids.box2] });
    const view = mount(r, 'today');
    expect(await screen.findByTestId('cockpit-choose')).toBeTruthy();
    expect(r.calls.some((c) => c.path === '/session/opening-proposal')).toBe(false);
    await press(`cockpit-choose-${ids.box2}`);
    expect(await screen.findByTestId('cockpit-next-not-open')).toBeTruthy();
    expect(r.calls.some((c) => c.url.includes(`accountId=${ids.box2}`))).toBe(true);
    const stored = await nativeDeviceStore.getItem(
      `loto.workstation.v1:${userId}:hash:fixture@example.invalid:${branchId}`,
    );
    expect(JSON.parse(stored!)).toEqual({ v: 1, date: '2026-10-27', accountId: ids.box2 });
    view.unmount();
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-not-open')).toBeTruthy();
    expect(screen.queryByTestId('cockpit-choose')).toBeNull();
    // The next server day clears the choice (never the device midnight).
    await nativeDeviceStore.setItem(
      `loto.workstation.v1:${userId}:hash:fixture@example.invalid:${branchId}`,
      JSON.stringify({ v: 1, date: '2026-10-26', accountId: ids.box2 }),
    );
    view.unmount();
    mount(r, 'today');
    expect(await screen.findAllByTestId('cockpit-choose')).toBeTruthy();
  });
  it('an unlinked Member gets the full setup state and no pretended link request', async () => {
    const { r, server } = await setup({ linked: false });
    server.state.linkRequest = {
      id: ids.other,
      userId,
      userName: 'x',
      status: 'Pending',
      createdAt: '2026-10-27T10:00:00Z',
      hasUsableAccount: false,
    };
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-setup-link')).toBeTruthy();
    expect(await screen.findByTestId('cockpit-link-request')).toBeTruthy();
    expect(screen.getByTestId('cockpit-request-link').props.accessibilityState?.disabled).toBe(
      true,
    );
    expect(
      r.calls.some((c) => c.method === 'POST' && c.path.startsWith('/operator-link-request')),
    ).toBe(false);
    expect(r.calls.some((c) => c.path.startsWith('/session'))).toBe(false);
  });
  it('an active own Draft with drafts shows the terminal-wide blocker and its own split', async () => {
    const { r, server } = await setup();
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    server.state.drafts.set(ids.box, 3);
    server.state.mineDrafts.set(ids.box, 1);
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-drafts')).toBeTruthy();
    expect(screen.getByText('Resolver meus rascunhos')).toBeTruthy();
    expect(screen.getByText('Ver pendências de outros operadores')).toBeTruthy();
    expect(screen.getByTestId('cockpit-today')).toBeTruthy();
    expect(screen.getByTestId('cockpit-draft-split').props.children).toContain(
      '1 sua · 2 de outros',
    );
    expect(s.custodyState).toBe('Active');
  });
  it('another operator holding the cashbox is not Dia ainda não aberto', async () => {
    const { r, server } = await setup();
    server.start(ids.box, ids.other, {});
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-held')).toBeTruthy();
    expect(screen.queryByText('Dia ainda não aberto')).toBeNull();
  });
  it('a correction past the first page of the list still takes priority (every page is read)', async () => {
    const { r, server } = await setup();
    server.state.listPageSize = 1;
    // Page 1 of the own Draft list is a plain earlier Draft; the correction is on page 2.
    for (const [day, extra] of [
      ['2026-10-25', {}],
      ['2026-10-24', { rejectionReason: 'Recontar Raspadinha' }],
    ] as const) {
      const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
      Object.assign(s, {
        date: `${day}T00:00:00`,
        recordedBy: ids.me,
        firstRecordedAt: 'x',
        custodyState: 'Finished',
        ...extra,
      });
    }
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-correction')).toBeTruthy();
    expect(r.calls.some((c) => c.path === '/session' && c.url.includes('Page=2'))).toBe(true);
  });
  it('a pending offer addressed to me is the next action', async () => {
    const { r, server } = await setup();
    const theirs = server.start(ids.box, ids.other, {});
    Object.assign(theirs, { status: 'Submitted', firstRecordedAt: 'x' });
    server.offer(theirs.id, ids.me);
    mount(r, 'today');
    expect(await screen.findByTestId('cockpit-next-receive')).toBeTruthy();
  });
});

describe('Abrir dia', () => {
  it('starts with the echoed source and the full acknowledged vector; an unknown outcome retries identically', async () => {
    const { r, server } = await setup();
    const prior = server.start(ids.box, ids.me, {});
    Object.assign(prior, {
      items: { [ids.cash]: 100, [ids.raspa]: 5 },
      firstRecordedAt: 'x',
      custodyState: 'Finished',
      date: '2026-10-26T00:00:00',
      status: 'Approved',
    });
    mockPath = '/fechamentos/abrir';
    mockParams = { accountId: ids.box };
    mount(r, 'open');
    expect(await screen.findByTestId('open-start')).toBeTruthy();
    expect(screen.getByTestId('open-source').props.children).toContain('26/10/2026');
    await toggle('open-different');
    fireEvent.changeText(await screen.findByTestId(`open-observed-${ids.cash}`), '95,00');
    server.state.fail.set('POST /session/start', { status: 503, once: true });
    await press('open-submit');
    expect(await screen.findByTestId('open-command-uncertain')).toBeTruthy();
    const first = writes(r, '/session/start')[0]!;
    expect(first.body).toEqual({
      accountId: ids.box,
      opening: {
        sourceKind: 'PreviousSession',
        sourceId: prior.id,
        sourceRevision: prior.countRevision,
        explicitZero: false,
        items: [
          { productId: ids.cash, value: 95 },
          { productId: ids.raspa, value: 5 },
        ],
      },
    });
    await press('open-command-retry');
    expect(await screen.findByTestId('open-started')).toBeTruthy();
    const [a, b] = writes(r, '/session/start');
    expect(b!.headers['idempotency-key']).toBe(a!.headers['idempotency-key']);
    expect(b!.body).toEqual(a!.body);
    expect(screen.getByTestId('open-discrepancy')).toBeTruthy();
    expect(
      [...server.state.sessions.values()].filter((s) => s.custodyState === 'Active'),
    ).toHaveLength(1);
  });
  it('a missing proposal starts only with an explicit zero', async () => {
    const { r } = await setup();
    mockPath = '/fechamentos/abrir';
    mockParams = { accountId: ids.box };
    mount(r, 'open');
    expect(await screen.findByText('Não há abertura registrada para este caixa.')).toBeTruthy();
    expect(screen.getByTestId('open-submit').props.accessibilityState?.disabled).toBe(true);
    await press('open-explicit-zero');
    await press('open-submit');
    expect(await screen.findByTestId('open-started')).toBeTruthy();
    expect(writes(r, '/session/start')[0]!.body).toEqual({
      accountId: ids.box,
      opening: { explicitZero: true, items: [] },
    });
  });
  it('a changed source is refused verbatim, re-read, and the typed differences stay', async () => {
    const { r, server } = await setup();
    server.state.openingCount = {
      id: ids.other,
      revision: 1,
      items: { [ids.cash]: 50, [ids.raspa]: 0 },
    };
    mockPath = '/fechamentos/abrir';
    mockParams = { accountId: ids.box };
    mount(r, 'open');
    await toggle('open-different');
    fireEvent.changeText(await screen.findByTestId(`open-observed-${ids.cash}`), '40,00');
    server.state.openingCount = {
      id: ids.other,
      revision: 2,
      items: { [ids.cash]: 60, [ids.raspa]: 0 },
    };
    await press('open-submit');
    expect(await screen.findByText('A abertura proposta mudou. Confira de novo.')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getAllByText('Reler e conferir')[0]!));
    expect(await screen.findByTestId('open-source-changed')).toBeTruthy();
    expect(screen.getByTestId(`open-observed-${ids.cash}`).props.value).toBe('40,00');
    await press('open-submit');
    expect(await screen.findByTestId('open-started')).toBeTruthy();
    expect(writes(r, '/session/start')[1]!.body).toMatchObject({ opening: { sourceRevision: 2 } });
    expect(writes(r, '/session/start')[1]!.headers['idempotency-key']).not.toBe(
      writes(r, '/session/start')[0]!.headers['idempotency-key'],
    );
  });
  it('an offered handover is accepted with the handover version and observed differences only', async () => {
    const { r, server } = await setup();
    const outgoing = server.start(ids.box, ids.other, {});
    Object.assign(outgoing, {
      items: { [ids.cash]: 80, [ids.raspa]: 0 },
      firstRecordedAt: 'x',
      status: 'Submitted',
    });
    const h = server.offer(outgoing.id, ids.me);
    mockPath = '/fechamentos/abrir';
    mockParams = { accountId: ids.box };
    mount(r, 'open');
    expect(await screen.findByTestId('handover-receive')).toBeTruthy();
    await toggle('handover-different');
    fireEvent.changeText(await screen.findByTestId(`open-observed-${ids.cash}`), '70,00');
    await press('handover-accept-submit');
    expect(await screen.findByTestId('open-started')).toBeTruthy();
    const accept = writes(r, `/session/handover/${h.id}/accept`)[0]!;
    expect(accept.headers['if-match']).toBe('"1"');
    expect(accept.headers['idempotency-key']).toBeTruthy();
    expect(accept.body).toEqual({
      items: [
        { productId: ids.cash, value: 70 },
        { productId: ids.raspa, value: 0 },
      ],
      explicitZero: false,
    });
    expect(screen.getByTestId('open-discrepancy')).toBeTruthy();
    expect(server.state.sessions.get(outgoing.id)!.custodyState).toBe('HandedOver');
  });
});

async function openClose(options: Parameters<typeof dayServer>[0] = {}, path = 'close') {
  const { r, server } = await setup(options);
  const s = server.start(ids.box, ids.me, { [ids.cash]: 100, [ids.raspa]: 20 });
  mockPath = path === 'close' ? `/fechamentos/${s.id}` : `/fechamentos/${s.id}/correcao`;
  mockParams = { id: s.id };
  const view = mount(r, path === 'close' ? 'close' : 'correction');
  expect(await screen.findByTestId('close-day')).toBeTruthy();
  return { r, server, s, view };
}

describe('Fechar dia — blind count, save and Conferir', () => {
  it('never shows a difference before Conferir; autosave sends the accepted value with If-Match', async () => {
    const { r, server, s } = await openClose();
    expect(screen.getByTestId('close-conference-NotConferred')).toBeTruthy();
    expect(screen.queryByText(/Diferença calculada/)).toBeNull();
    expect(screen.queryByText(/esperado/)).toBeNull();
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).not.toBe(true);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    const save = writes(r, '/items')[0]!;
    expect(save.headers['if-match']).toBe('"1"');
    expect(save.body).toEqual({ items: [{ productId: ids.cash, value: 150 }], notes: '' });
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).toBe(true);
    expect(server.state.sessions.get(s.id)!.version).toBe(2);
    expect(screen.queryByText(/Diferença calculada/)).toBeNull();
  });
  it('coalesces an edit made while a save is in flight; the older acknowledgement keeps the newer edit', async () => {
    const { r, server } = await openClose();
    const gate = deferred<void>();
    server.state.hold.set(
      `PUT /session/${[...server.state.sessions.keys()][0]}/items`,
      gate.promise,
    );
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '160,00');
    server.state.hold.clear();
    await act(async () => gate.resolve());
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(2));
    expect(writes(r, '/items')[1]!.body).toMatchObject({
      items: [{ productId: ids.cash, value: 160 }],
    });
    expect(writes(r, '/items')[1]!.headers['if-match']).toBe('"2"');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
  });
  it('arithmetic entry previews the result and sends only the accepted number', async () => {
    const { r } = await openClose();
    await press(`close-input-${ids.cash}-calc`);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}-expression`), '100');
    await press(`close-input-${ids.cash}-key-mais`);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}-expression`), '100+50');
    await press(`close-input-${ids.cash}-key-mais`);
    expect(screen.getByTestId(`close-input-${ids.cash}-preview`).props.children).toBe(
      'Continue digitando a conta.',
    );
    expect(
      screen.getByTestId(`close-input-${ids.cash}-accept`).props.accessibilityState?.disabled,
    ).toBe(true);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}-expression`), '100+50+20,5');
    expect(screen.getByTestId(`close-input-${ids.cash}-preview`).props.children).toBe(
      'Resultado: R$ 170,50',
    );
    // An open calculation is never autosaved or applied silently.
    await new Promise((resolve) => setTimeout(resolve, 60));
    expect(writes(r, '/items')).toHaveLength(0);
    await press(`close-input-${ids.cash}-accept`);
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    expect(writes(r, '/items')[0]!.body).toMatchObject({
      items: [{ productId: ids.cash, value: 170.5 }],
    });
    expect(JSON.stringify(writes(r, '/items')[0]!.body)).not.toContain('+');
  });
  it('the signed report takes a negative calculation as sign plus magnitude; fees are nonnegative', async () => {
    const { r } = await openClose();
    await press('close-field-TerminalReport-calc');
    fireEvent.changeText(screen.getByTestId('close-field-TerminalReport-expression'), '10-25');
    expect(screen.getByTestId('close-field-TerminalReport-preview').props.children).toBe(
      'Resultado: −R$ 15,00',
    );
    await press('close-field-TerminalReport-accept');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    expect(writes(r, '/items')[0]!.body).toEqual({
      items: [],
      notes: '',
      closeFields: [{ field: 'TerminalReport', value: -15 }],
    });
    await press('close-field-BolaoFee-calc');
    fireEvent.changeText(screen.getByTestId('close-field-BolaoFee-expression'), '10-25');
    expect(screen.getByTestId('close-field-BolaoFee-preview').props.children).toBe(
      'O resultado não pode ser negativo neste campo.',
    );
    expect(
      screen.getByTestId('close-field-BolaoFee-accept').props.accessibilityState?.disabled,
    ).toBe(true);
  });
  it('Repetir abertura copies the opening, Desfazer restores the exact blank and survives autosave', async () => {
    const { r } = await openClose();
    await press(`close-repeat-${ids.cash}`);
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('100,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    expect(await screen.findByTestId(`close-undo-${ids.cash}`)).toBeTruthy();
    await press(`close-undo-${ids.cash}`);
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(2));
    expect(writes(r, '/items')[1]!.body).toMatchObject({ items: [] });
  });
  it('a stale save keeps the buffer and requires deliberate reconciliation', async () => {
    const { r, server, s } = await openClose();
    server.concurrentSave(s.id, ids.cash, 999);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    expect(await screen.findByTestId('close-stale')).toBeTruthy();
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('150,00');
    await new Promise((resolve) => setTimeout(resolve, 60));
    expect(writes(r, '/items')).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getAllByTestId('close-reload')[0]!));
    expect(await screen.findByTestId('close-reconcile')).toBeTruthy();
    await press('close-keep-mine');
    await press('close-save');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(2));
    expect(writes(r, '/items')[1]!.headers['if-match']).toBe(`"${s.version - 1}"`);
  });
  it('Conferir sends the server token and revisions with If-Match and a key; Current reveals only server figures', async () => {
    const { r, server, s } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '110,00');
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.raspa}`), '20,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    const conferir = writes(r, '/conferir')[0]!;
    expect(conferir.body).toEqual({
      confirmationToken: 'confirm-2-1-1',
      countRevision: 2,
      ledgerRevision: 1,
      openingRevision: 1,
      confirmCountStillObserved: false,
    });
    expect(conferir.headers['idempotency-key']).toBeTruthy();
    expect(conferir.headers['if-match']).toBe('"2"');
    expect(screen.getByTestId('close-server-difference').props.children).toBe(
      'Diferença calculada pelo servidor: +R$ 10,00',
    );
    // The ledger changed after Conferir: Unconfirmed until explicitly confirmed (no forced recount).
    server.ledgerChange(s.id, 10);
    await act(async () => fireEvent.press(screen.getByTestId('close-submit-button')));
    // Another actor's ledger change also advances the session version: the submit is refused (409)
    // and rendered verbatim; nothing is resubmitted automatically.
    expect(await screen.findByTestId('close-submit-error')).toBeTruthy();
    expect(writes(r, '/submit')).toHaveLength(1);
    await act(async () => fireEvent.press(screen.getAllByText('Reler e conferir').at(-1)!));
    expect(await screen.findByTestId('close-conference-Unconfirmed')).toBeTruthy();
    expect(screen.getByTestId('close-unconfirmed')).toBeTruthy();
    expect(screen.getByTestId('close-conferir-button').props.accessibilityState?.disabled).toBe(
      true,
    );
    await press('close-still-observed');
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    expect(writes(r, '/conferir')[1]!.body).toMatchObject({ confirmCountStillObserved: true });
    await press('close-submit-button');
    await waitFor(() => expect(server.state.sessions.get(s.id)!.status).toBe('Submitted'));
    const submitted = writes(r, '/submit').at(-1)!;
    expect(submitted.body).toEqual({
      attemptId: server.state.sessions.get(s.id)!.attempts[1]!.id,
      countRevision: 2,
      ledgerRevision: 2,
      openingRevision: 1,
    });
    expect(await screen.findByTestId('close-custody')).toBeTruthy();
  });
  it('renders a negative server expected closing (more saídas than opening and entradas) with its sign', async () => {
    const { server, s } = await openClose();
    server.state.sessions.get(s.id)!.ledgerOut = 500;
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '10,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    expect(screen.getByText(/esperado \u2212R\$/)).toBeTruthy();
    expect(screen.queryByTestId('render-error')).toBeNull();
  });
  it('an unknown Conferir outcome is retried only explicitly with the same key; a replay returns the same attempt', async () => {
    const { r, server, s } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '100,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    server.state.fail.set(`POST /session/${s.id}/conferir`, { status: 504, once: true });
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conferir-uncertain')).toBeTruthy();
    expect(screen.queryByText(/Diferença calculada/)).toBeNull();
    await press('close-conferir-retry');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    const [a, b] = writes(r, '/conferir');
    expect(b!.headers['idempotency-key']).toBe(a!.headers['idempotency-key']);
    expect(b!.body).toEqual(a!.body);
    expect(server.state.sessions.get(s.id)!.attempts).toHaveLength(1);
  });
  it('Recontar hides the calculation until the next Conferir', async () => {
    const { r } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '100,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    await press('close-recount-button');
    expect(await screen.findByTestId('close-conference-Recounting')).toBeTruthy();
    expect(screen.queryByText(/Diferença calculada/)).toBeNull();
    expect(writes(r, '/recount')[0]!.body).toEqual({ previousAttemptId: expect.any(String) });
  });
  it('a 404 save refusal (gap AA) is never resent unchanged and keeps every typed value', async () => {
    const { r, server, s } = await openClose();
    server.state.fail.set(`PUT /session/${s.id}/items`, {
      status: 404,
      body: { errorMessages: ['Produto não encontrado ou inativo.'] },
    });
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    expect(await screen.findByTestId('close-unsupported')).toBeTruthy();
    expect(screen.getByText('Produto não encontrado ou inativo.')).toBeTruthy();
    await new Promise((resolve) => setTimeout(resolve, 80));
    expect(writes(r, '/items')).toHaveLength(1);
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).toBe(true);
    // The whole count is locked, not just that body: clearing the row and editing another field
    // (which would omit the retained value with a 200) is not possible; the typed value stays shown.
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.onChangeText).toBeUndefined();
    expect(screen.getByText('R$ 150,00')).toBeTruthy();
    expect(screen.getByTestId(`close-input-${ids.raspa}`).props.onChangeText).toBeUndefined();
    expect(screen.getByTestId('close-field-TerminalReport').props.onChangeText).toBeUndefined();
    expect(screen.queryByTestId(`close-input-${ids.cash}-calc`)).toBeNull();
    // A reread does not lift the lock.
    server.state.fail.delete(`PUT /session/${s.id}/items`);
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 80));
    });
    expect(writes(r, '/items')).toHaveLength(1);
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).toBe(true);
  });
  it('a stored row of a deactivated product (gap AA) locks the count before any save', async () => {
    const { r, server } = await setup();
    const retired = '0000000a-0000-4000-8000-0000000000aa';
    server.state.retired.set(retired, 'Tarifa antiga');
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    Object.assign(s, { firstRecordedAt: 'x', items: { [ids.cash]: 100, [retired]: 17 } });
    mockPath = `/fechamentos/${s.id}`;
    mockParams = { id: s.id };
    mount(r, 'close');
    expect(
      await screen.findByText('Esta contagem tem um valor de um produto desativado.'),
    ).toBeTruthy();
    expect(screen.getByTestId(`close-input-${retired}`).props.onChangeText).toBeUndefined();
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.onChangeText).toBeUndefined();
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).toBe(true);
    await act(async () => new Promise((resolve) => setTimeout(resolve, 80)));
    expect(writes(r, '/items')).toHaveLength(0);
  });
  it('an unreadable product list locks the count until it is read', async () => {
    const { r, server } = await setup();
    server.state.fail.set('GET /product', { status: 500, once: true });
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    mockPath = `/fechamentos/${s.id}`;
    mockParams = { id: s.id };
    mount(r, 'close');
    expect(await screen.findByTestId('close-products-unknown')).toBeTruthy();
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.onChangeText).toBeUndefined();
    await act(async () => fireEvent.press(screen.getByText('Tentar novamente')));
    await waitFor(() => expect(screen.queryByTestId('close-products-unknown')).toBeNull());
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
  });
  it('an open calculation blocks Conferir and Enviar; the saved value is not conferred meanwhile', async () => {
    const { r } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '100,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press(`close-input-${ids.cash}-calc`);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}-expression`), '500+');
    expect(screen.getByTestId('close-conferir-button').props.accessibilityState?.disabled).toBe(
      true,
    );
    await act(async () => fireEvent.press(screen.getByTestId('close-conferir-button')));
    expect(writes(r, '/conferir')).toHaveLength(0);
    await press(`close-input-${ids.cash}-discard`);
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    await press(`close-input-${ids.cash}-calc`);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}-expression`), '500+');
    expect(screen.getByTestId('close-submit-button').props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(screen.getByTestId('close-submit-button')));
    expect(writes(r, '/submit')).toHaveLength(0);
  });
  it('the still-observed confirmation belongs to the revisions shown; a new revision clears it', async () => {
    const { r, server, s } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '100,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conference-Current')).toBeTruthy();
    server.ledgerChange(s.id, 10);
    await act(async () => fireEvent.press(screen.getByTestId('close-submit-button')));
    await act(async () =>
      fireEvent.press((await screen.findAllByText('Reler e conferir')).at(-1)!),
    );
    expect(await screen.findByTestId('close-conference-Unconfirmed')).toBeTruthy();
    await press('close-still-observed');
    expect(screen.getByTestId('close-still-observed').props.accessibilityState?.checked).toBe(true);
    // Another movement change; Conferir meets 409 and the reread shows a newer ledger revision.
    server.ledgerChange(s.id, 5);
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conferir-error')).toBeTruthy();
    await act(async () => fireEvent.press(screen.getAllByText('Reler e conferir').at(-1)!));
    await waitFor(() =>
      expect(screen.getByTestId('close-still-observed').props.accessibilityState?.checked).toBe(
        false,
      ),
    );
    expect(screen.getByTestId('close-conferir-button').props.accessibilityState?.disabled).toBe(
      true,
    );
    expect(writes(r, '/conferir')).toHaveLength(2);
  });
  it('a retried unknown Conferir keeps its original If-Match after a later save', async () => {
    const { r, server, s } = await openClose();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '100,00');
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    server.state.fail.set(`POST /session/${s.id}/conferir`, { status: 504, once: true });
    await press('close-conferir-button');
    expect(await screen.findByTestId('close-conferir-uncertain')).toBeTruthy();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.raspa}`), '20,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(2));
    await waitFor(() =>
      expect(screen.getByTestId('close-save-status').props.children).toMatch(/^Salvo/),
    );
    await press('close-conferir-retry');
    await waitFor(() => expect(writes(r, '/conferir')).toHaveLength(2));
    const [a, b] = writes(r, '/conferir');
    expect(b!.headers['if-match']).toBe(a!.headers['if-match']);
    expect(b!.headers['idempotency-key']).toBe(a!.headers['idempotency-key']);
    expect(b!.body).toEqual(a!.body);
  });
  it('a pasted negative terminal report sets Negativo and keeps the magnitude', async () => {
    const { r } = await openClose();
    fireEvent.changeText(screen.getByTestId('close-field-TerminalReport'), '\u221215,00');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
    expect(writes(r, '/items')[0]!.body).toMatchObject({
      closeFields: [{ field: 'TerminalReport', value: -15 }],
    });
    fireEvent.changeText(screen.getByTestId('close-field-BolaoFee'), '-3,00');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 80)));
    expect(writes(r, '/items')).toHaveLength(1);
  });
});

describe('retention and rollover', () => {
  it('input kept on the device is offered after a restart, restored for review only and never sent by itself', async () => {
    const { r, s, view } = await openClose();
    autosaveTiming.delayMs = 60000;
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 50)));
    view.unmount();
    autosaveTiming.delayMs = 20;
    mockPath = `/fechamentos/${s.id}`;
    mockParams = { id: s.id };
    mount(r, 'close');
    expect(await screen.findByText('Há texto guardado neste dispositivo.')).toBeTruthy();
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('');
    await act(async () => fireEvent.press(screen.getByText('Restaurar para revisão')));
    expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('150,00');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 80)));
    expect(writes(r, '/items')).toHaveLength(0);
    expect(screen.getByTestId('close-save').props.accessibilityState?.disabled).not.toBe(true);
    await press('close-save');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
  });
  it('a server day change keeps the original session and stops its autosave', async () => {
    const { r } = await openClose();
    await act(async () => {
      r.date('2026-10-28');
      await r.engine.refresh();
    });
    expect(r.engine.getSnapshot().dayChanged).toBe(true);
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 80)));
    expect(writes(r, '/items')).toHaveLength(0);
    await act(async () => r.engine.acknowledgeDayChange());
    // Yesterday's Draft is no longer today's plain Draft: explicit Salvar only, same resource.
    await act(async () => new Promise((resolve) => setTimeout(resolve, 80)));
    expect(writes(r, '/items')).toHaveLength(0);
    expect(screen.getByTestId('close-save-status').props.children).toBe(
      'Alterações não salvas no servidor',
    );
    expect(screen.getByTestId('close-context').props.children).toContain('27/10/2026');
  });
});

describe('Corrigir e reenviar', () => {
  it('shows dated rejection and opening events with honest fallbacks, and saves only explicitly', async () => {
    const { r, server } = await setup();
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    Object.assign(s, {
      status: 'Rejected',
      rejectionReason: 'Recontar Raspadinha',
      openingRecheckRequiredAt: '2026-10-27T12:00:00Z',
      firstRecordedAt: 'x',
      items: { [ids.cash]: 100 },
      events: [
        {
          id: ids.other,
          type: 'Rejected',
          occurredAt: '2026-10-27T15:00:00Z',
          actorUserId: ids.other,
          actorUserName: 'Gerente',
          rejectionReason: 'Recontar Raspadinha',
          items: [],
        },
      ],
    });
    mockPath = `/fechamentos/${s.id}/correcao`;
    mockParams = { id: s.id };
    mount(r, 'correction');
    expect(await screen.findByTestId('close-causes')).toBeTruthy();
    expect(screen.getByText('Observação da gerência: Recontar Raspadinha')).toBeTruthy();
    expect(
      screen.getByText('Detalhes da alteração indisponíveis para este fechamento'),
    ).toBeTruthy();
    fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '120,00');
    await new Promise((resolve) => setTimeout(resolve, 80));
    expect(writes(r, '/items')).toHaveLength(0);
    expect(screen.getByTestId('close-save-status').props.children).toBe(
      'Alterações não salvas no servidor',
    );
    await press('close-save');
    await waitFor(() => expect(writes(r, '/items')).toHaveLength(1));
  });
});

describe('Encerrar turno e Passar o caixa', () => {
  async function submitted(options: Parameters<typeof dayServer>[0] = {}) {
    const { r, server } = await setup(options);
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    Object.assign(s, {
      status: 'Submitted',
      firstRecordedAt: 'x',
      items: { [ids.cash]: 100 },
      submittedAt: '2026-10-27T21:00:00Z',
    });
    mockPath = `/fechamentos/${s.id}`;
    mockParams = { id: s.id };
    mount(r, 'close');
    expect(await screen.findByTestId('close-custody')).toBeTruthy();
    return { r, server, s };
  }
  it('the Member custodian finishes with If-Match and a key; offering needs a list the Member does not have', async () => {
    const { r, server, s } = await submitted();
    expect(screen.getByTestId('close-offer-unavailable')).toBeTruthy();
    expect(screen.queryByTestId('close-offer-start')).toBeNull();
    await press('close-finish-button');
    await waitFor(() => expect(server.state.sessions.get(s.id)!.custodyState).toBe('Finished'));
    const finish = writes(r, '/finish')[0]!;
    expect(finish.headers['if-match']).toBe(`"${s.version - 1}"`);
    expect(finish.headers['idempotency-key']).toBeTruthy();
    expect(finish.body).toEqual({});
  });
  it('a failed history read is unknown custody: no Encerrar, Passar or Retirar is offered', async () => {
    const { r, server } = await setup({ role: 'Manager' });
    const s = server.start(ids.box, ids.me, { [ids.cash]: 100 });
    Object.assign(s, {
      status: 'Submitted',
      firstRecordedAt: 'x',
      items: { [ids.cash]: 100 },
      submittedAt: '2026-10-27T21:00:00Z',
    });
    server.offer(s.id, ids.other);
    server.state.fail.set(`GET /session/${s.id}/history`, { status: 500 });
    mockPath = `/fechamentos/${s.id}`;
    mockParams = { id: s.id };
    mount(r, 'close');
    expect(await screen.findByTestId('close-custody-unknown')).toBeTruthy();
    expect(screen.queryByTestId('close-finish-button')).toBeNull();
    expect(screen.queryByTestId('close-offer-start')).toBeNull();
    expect(screen.queryByTestId('close-withdraw-button')).toBeNull();
    server.state.fail.delete(`GET /session/${s.id}/history`);
    await act(async () =>
      fireEvent.press(
        within(screen.getByTestId('close-custody-unknown')).getByText('Tentar novamente'),
      ),
    );
    expect(await screen.findByTestId('close-pending-offer')).toBeTruthy();
    expect(screen.getByTestId('close-withdraw-button')).toBeTruthy();
    expect(screen.queryByTestId('close-finish-button')).toBeNull();
  });
  it('a linked manager offers the cashbox with the count revision; custody stays until acceptance', async () => {
    const { r, server, s } = await submitted({ role: 'Manager' });
    await press('close-offer-start');
    await act(async () => fireEvent.press(await screen.findByTestId('close-offer-incoming')));
    await act(async () => fireEvent.press(await screen.findByText('Ana')));
    await press('close-offer-button');
    await waitFor(() => expect(writes(r, '/handover')).toHaveLength(1));
    expect(writes(r, '/handover')[0]!.body).toEqual({
      incomingOperatorId: ids.other,
      countRevision: s.countRevision,
    });
    expect(await screen.findByTestId('close-pending-offer')).toBeTruthy();
    expect(server.state.sessions.get(s.id)!.custodyState).toBe('Active');
    // Withdrawing needs a reason.
    expect(screen.getByTestId('close-withdraw-button').props.accessibilityState?.disabled).toBe(
      true,
    );
  });
});
