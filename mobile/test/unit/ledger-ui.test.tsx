import { act, configure, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { Component, useEffect as mockEffect, type ReactNode } from 'react';
import { Text } from 'react-native';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { ToastProvider } from '../../src/ui';
import { LedgerSurface, type LedgerSurfaceKey } from '../../src/ledger/surface';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig, deferred } from './support';
import { renderThemed } from './ui/support';
import { ledgerServer, L, type LedgerOptions } from './ledger-server';
import { focusManager } from '@tanstack/react-query';

// Full M6 screens (sheets, pickers, providers) load cold in a fresh worker; behavior is unchanged.
configure({ asyncUtilTimeout: 3000 });
jest.setTimeout(20000);

let mockPath = '/transacoes';
let mockParams: Record<string, string> = {};
const mockRouter = {
  replace: jest.fn(),
  push: jest.fn(),
  back: jest.fn(),
  dismissTo: jest.fn(),
  setParams: jest.fn((next: Record<string, string>) => {
    mockParams = { ...mockParams, ...next };
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
let mockKey = 0;
jest.mock('../../src/storage/native', () => ({
  ...jest.requireActual('../../src/storage/native'),
  secureUuid: () => `40000000-0000-4000-8000-${String(++mockKey).padStart(12, '0')}`,
  emailPartitionKey: async (email: string) => 'hash:' + email,
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));

beforeEach(async () => {
  mockPath = '/transacoes';
  mockParams = {};
  jest.clearAllMocks();
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
});

async function setup(options: LedgerOptions = {}) {
  const r = rig();
  r.role(options.role ?? 'Member');
  const server = ledgerServer(options);
  r.intercept((path, init) => server.handle(path, init, r.calls));
  await r.ready();
  await r.engine.refresh({ selfContext: true });
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
/** Renders and waits for the provider's own session restore to settle before any interaction. */
async function mount(
  r: ReturnType<typeof rig>,
  destination: LedgerSurfaceKey,
  path: string,
  params: Record<string, string> = {},
) {
  mockPath = path;
  mockParams = params;
  // The provider restarts the engine on mount (start → identity → branch); wait for that to finish.
  const statuses: string[] = [];
  const unsubscribe = r.engine.subscribe(() => statuses.push(r.engine.getSnapshot().status));
  const view = renderThemed(
    <Boundary>
      <ToastProvider>
        <SessionProvider engine={r.engine} lifecycle={false}>
          <ShellProvider>
            <LedgerSurface destination={destination} />
          </ShellProvider>
        </SessionProvider>
      </ToastProvider>
    </Boundary>,
  );
  await waitFor(() => {
    expect(statuses).toContain('starting');
    expect(r.engine.getSnapshot()).toMatchObject({
      status: 'branch',
      selfStatus: 'ready',
      writesPaused: false,
    });
  });
  unsubscribe();
  return view;
}
const until = async <T,>(read: () => T) => {
  let value!: T;
  await waitFor(() => {
    value = read();
  });
  return value;
};
const press = async (testID: string) => {
  const target = await until(() => {
    const node = screen.getByTestId(testID);
    if (node.props.accessibilityState?.disabled) throw new Error(`${testID} disabled`);
    return node;
  });
  await act(async () => fireEvent.press(target));
};
const choose = async (select: string, value: string) => {
  await press(select);
  await press(`${select}-option-${value}`);
};
const type = async (testID: string, text: string) => {
  const node = await screen.findByTestId(testID);
  await act(async () => fireEvent.changeText(node, text));
};
/** The confirmation's own button (the last element with that label: the dialog footer). */
const confirmLast = async (label: string) => {
  const target = await until(() => {
    const all = screen.getAllByLabelText(label);
    if (all.length < 2) throw new Error('dialog not open');
    return all.at(-1)!;
  });
  await act(async () => fireEvent.press(target));
};
/** Dates commit on blur/submit, like the native keyboard's Done. */
const typeDate = async (testID: string, text: string) => {
  const node = await screen.findByTestId(testID);
  await act(async () => fireEvent.changeText(node, text));
  await act(async () => fireEvent(node, 'blur'));
};
/** A captured write; its body is asserted structurally (property access on a known JSON shape). */
type Body = Record<string, unknown> & { items?: { expectedVersion: number }[] };
const writes = (
  r: ReturnType<typeof rig>,
  path: string,
): { body: Body; headers: Record<string, string>; path: string; url: string }[] =>
  r.calls.filter((c) => c.method !== 'GET' && c.path === path) as never;
const disabled = (testID: string) =>
  screen.getByTestId(testID).props.accessibilityState?.disabled === true;

describe('Transactions list', () => {
  it('reads the server page with the route filter; Members never send OperatorId; rows show Automática from structure', async () => {
    const { r, server } = await setup({ activeSession: true });
    const sale = server.add({
      transactionTypeId: L.sale,
      accountId: L.tab,
      value: 30,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    server.add({
      transactionTypeId: L.saleCounterpart,
      accountId: L.box,
      value: 30,
      date: '2026-10-27T00:00:00',
      generationKind: 'FiadoSale',
      generatedFromTransactionId: sale.id,
    });
    await mount(r, 'transactions', '/transacoes', {
      operatorId: L.otherOp,
      mine: 'false',
      from: '2026-10-27',
      to: '2026-10-27',
    });
    expect(await screen.findByText('2 transações')).toBeTruthy();
    const list = r.calls.filter((c) => c.path === '/transaction' && c.method === 'GET');
    expect(
      list.every((c) => !c.url.includes('OperatorId') && c.url.includes('DateFrom=2026-10-27')),
    ).toBe(true);
    expect(screen.getAllByText('Automática')).toHaveLength(1);
    expect(screen.queryByText(/Total/)).toBeNull();
  });
  it('the M5 Draft blocker shows the server own/other split and "nothing pending" only after a fresh read', async () => {
    const { r, server } = await setup({ activeSession: true });
    const mine = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 5,
      date: '2026-10-27T00:00:00',
      status: 'Draft',
    });
    server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 6,
      date: '2026-10-27T00:00:00',
      status: 'Draft',
      recordedByOperatorId: L.otherOp,
    });
    await mount(r, 'transactions', '/transacoes', {
      accountId: L.box,
      from: '2026-10-27',
      to: '2026-10-27',
      status: 'Draft',
    });
    await waitFor(() => expect(screen.getByTestId('list-blocker').props).toBeTruthy());
    expect(await screen.findByText(/1 sua · 1 de outros operadores/)).toBeTruthy();
    // Opening a row keeps the filter and the row for return focus.
    await act(async () =>
      fireEvent.press(screen.getByLabelText(new RegExp(`Abrir transação: Despesa, .*5,00`))),
    );
    expect(mockRouter.push.mock.calls.at(-1)?.[0]).toMatchObject({
      pathname: '/transacoes/[id]',
      params: { id: mine.id },
    });
  });
});

describe('Transaction — Create', () => {
  it('fast entry: complete candidate → fenced preview → one idempotent create → Draft receipt → explicit finalize with If-Match', async () => {
    const { r } = await setup({ activeSession: true });
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.expense);
    await type('create-value', '12,50');
    expect(await screen.findByTestId('create-preview-cash-blind')).toBeTruthy();
    expect(writes(r, '/transaction')).toHaveLength(0);
    await press('create-save-draft');
    expect(await screen.findByTestId('ledger-receipt')).toBeTruthy();
    const created = writes(r, '/transaction');
    expect(created).toHaveLength(1);
    expect(created[0]!.headers['idempotency-key']).toMatch(/^40000000-/);
    expect(created[0]!.body).toMatchObject({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 12.5,
      saveAsDraft: true,
    });
    expect(created[0]!.body.openingAcknowledgment).toBeUndefined();
    await press('ledger-receipt-finalize');
    await screen.findByTestId('ledger-receipt-finalized');
    const finalize = r.calls.find((c) => c.path.endsWith('/finalize'))!;
    expect(finalize.headers['if-match']).toBe('"1"');
    expect(writes(r, '/transaction')).toHaveLength(1);
  });
  it('excludes system and repayment types from the type list', async () => {
    const { r } = await setup({ activeSession: true });
    await mount(r, 'create', '/transacoes/nova');
    await press('create-type');
    expect(await screen.findByTestId(`create-type-option-${L.expense}`)).toBeTruthy();
    for (const hidden of [L.cashMethod, L.pixMethod, L.saleCounterpart, L.safeOut, L.conversion])
      expect(screen.queryByTestId(`create-type-option-${hidden}`)).toBeNull();
  });
  it('a late preview never overwrites the current candidate', async () => {
    const { r, server } = await setup({ activeSession: true });
    const slow = deferred<void>();
    server.state.previewDelay.set('10', slow.promise);
    await mount(r, 'create', '/transacoes/nova');
    await choose('create-type', L.sale);
    await choose('create-account', L.tab);
    await choose('create-client', L.ana);
    await type('create-value', '10,00');
    await waitFor(() => expect(r.calls.some((c) => c.path === '/transaction/preview')).toBe(true));
    await type('create-value', '20,00');
    await screen.findByText(/Fiado de Ana: \+R\$\s20,00/);
    await act(async () => slow.resolve());
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(screen.queryByText(/Fiado de Ana: \+R\$\s10,00/)).toBeNull();
    expect(writes(r, '/transaction')).toHaveLength(0);
  });
  it('no active turno: the opening is confirmed explicitly and travels atomically on the same create (no separate Start)', async () => {
    const { r, server } = await setup({ activeSession: false });
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.income);
    await type('create-value', '7,00');
    expect(await screen.findByTestId('create-opening')).toBeTruthy();
    await waitFor(() => expect(disabled('create-save')).toBe(true));
    await act(async () =>
      fireEvent(screen.getByTestId('create-opening-confirm'), 'checkedChange', true),
    );
    await press('create-save');
    await screen.findByTestId('ledger-receipt-started');
    const create = writes(r, '/transaction')[0]!;
    expect(create.body.openingAcknowledgment).toEqual({
      sourceKind: 'PreviousSession',
      sourceId: expect.any(String),
      sourceRevision: 4,
      explicitZero: false,
      items: [{ productId: L.product, value: 100 }],
    });
    expect(r.calls.some((c) => c.path === '/session/start')).toBe(false);
    expect(server.state.sessionsStarted).toBe(1);
  });
  it('an unknown outcome survives remount: nothing is sent until the explicit identical retry, which replays one row', async () => {
    const { r, server } = await setup({ activeSession: true });
    server.state.lose.add('POST /transaction');
    const view = await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.expense);
    await type('create-value', '3,00');
    await press('create-save');
    expect(await screen.findByTestId('create-intent-uncertain')).toBeTruthy();
    view.unmount();
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    expect(await screen.findByTestId('create-intent-uncertain')).toBeTruthy();
    expect(writes(r, '/transaction')).toHaveLength(1);
    await press('create-intent-retry');
    expect(await screen.findByTestId('ledger-receipt')).toBeTruthy();
    const sent = writes(r, '/transaction');
    expect(sent).toHaveLength(2);
    expect(sent[1]!.headers['idempotency-key']).toBe(sent[0]!.headers['idempotency-key']);
    expect(sent[1]!.body).toEqual(sent[0]!.body);
    expect([...server.state.rows.values()].filter((row) => row.value === 3)).toHaveLength(1);
  });
  it('a fiado sale writes its cashbox leg server-side; the form never offers a second saída', async () => {
    const { r, server } = await setup({ activeSession: true });
    await mount(r, 'create', '/transacoes/nova');
    await choose('create-type', L.sale);
    await choose('create-account', L.tab);
    expect(await screen.findByTestId('create-fiado-pair')).toBeTruthy();
    await choose('create-client', L.ana);
    await type('create-value', '40,00');
    await press('create-save');
    await screen.findByTestId('ledger-receipt-generated');
    expect(JSON.stringify(writes(r, '/transaction')[0]!.body)).not.toMatch(/HandsOut/);
    expect(
      [...server.state.rows.values()].filter((row) => row.generationKind === 'FiadoSale'),
    ).toHaveLength(1);
  });
});

describe('Transaction — Edit', () => {
  it('stale If-Match keeps the edit; an explicit reread shows the current version; adoption sends the new version', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const row = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 9,
      date: '2026-10-27T00:00:00',
    });
    await mount(r, 'edit', `/transacoes/${row.id}`, { id: row.id });
    await type('detail-description', 'meu texto');
    row.version = 5; // another actor changed it
    row.description = 'deles';
    await press('detail-save');
    expect(await screen.findByTestId('detail-refused')).toBeTruthy();
    expect(screen.getByTestId('detail-description').props.value).toBe('meu texto');
    expect(disabled('detail-save')).toBe(true);
    await press('detail-reconcile');
    expect(await screen.findByTestId('detail-stale')).toBeTruthy();
    await press('detail-stale-adopt');
    await press('detail-save');
    await screen.findByTestId('detail-receipt');
    const puts = r.calls.filter((c) => c.method === 'PUT');
    expect(puts.map((c) => c.headers['if-match'])).toEqual(['"1"', '"5"']);
    expect(puts[1]!.body).toEqual({
      description: 'meu texto',
      dueDate: '2026-10-27',
      clientId: null,
      transactionTime: null,
    });
  });
  it("generated rows and other operators' rows are read-only with their reason and source link", async () => {
    const { r, server } = await setup({ activeSession: true });
    const sale = server.add({
      transactionTypeId: L.sale,
      accountId: L.tab,
      value: 30,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    const leg = server.add({
      transactionTypeId: L.saleCounterpart,
      accountId: L.box,
      value: 30,
      date: '2026-10-27T00:00:00',
      generationKind: 'FiadoSale',
      generatedFromTransactionId: sale.id,
    });
    const view = await mount(r, 'edit', `/transacoes/${leg.id}`, { id: leg.id });
    expect(await screen.findByTestId('detail-generated')).toBeTruthy();
    expect(screen.queryByTestId('detail-edit')).toBeNull();
    expect(screen.getByTestId('detail-open-source')).toBeTruthy();
    view.unmount();
    const other = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 2,
      date: '2026-10-27T00:00:00',
      recordedByOperatorId: L.otherOp,
    });
    await mount(r, 'edit', `/transacoes/${other.id}`, { id: other.id });
    expect(
      await screen.findByText('Registrada por outro operador: somente leitura para você.'),
    ).toBeTruthy();
    expect(screen.queryByTestId('detail-cancel')).toBeNull();
  });
});

describe('Installment plan', () => {
  it('the server schedule must be accepted before saving; the plan finalizes atomically with every Draft id and version', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const view = await mount(r, 'plan-new', '/transacoes/cheque-pre-datado/novo');
    await choose('plan-type', L.cheque);
    await choose('plan-account', L.bank);
    await type('plan-value', '300,00');
    await act(async () => fireEvent.press(screen.getByLabelText('Gerar mensais')));
    await typeDate('plan-first-due', '27/11/2026');
    await type('plan-count', '3');
    expect(await screen.findByTestId('plan-preview-row-3')).toBeTruthy();
    expect(disabled('plan-save-draft')).toBe(true);
    await act(async () => fireEvent(screen.getByTestId('plan-accept'), 'checkedChange', true));
    await press('plan-save-draft');
    await screen.findByTestId('plan-receipt');
    const created = writes(r, '/transaction/installment');
    expect(created).toHaveLength(1);
    expect(created[0]!.body).toMatchObject({
      autoGenerateInstallments: true,
      installmentCount: 3,
      saveAsDraft: true,
    });
    view.unmount();
    const rows = [...server.state.rows.values()];
    const origin = rows[0]!.originTransactionId!;
    await mount(r, 'plan', `/transacoes/cheque-pre-datado/${origin}`, { id: origin });
    expect(await screen.findByText('Plano de cheques · Rascunho')).toBeTruthy();
    // A stale version refuses the whole plan: nothing becomes Active.
    rows[1]!.version = 7;
    await press('plan-finalize');
    await confirmLast('Finalizar plano');
    expect(await screen.findByTestId('plan-finalize-state')).toBeTruthy();
    expect(rows.every((row) => row.status === 'Draft')).toBe(true);
    await press('plan-reread');
    await waitFor(() => expect(screen.queryByTestId('plan-finalize-state')).toBeNull());
    await press('plan-finalize');
    await confirmLast('Finalizar plano');
    await screen.findByText('Plano de cheques · Ativo');
    const finalizes = r.calls.filter((c) => c.path.endsWith('/finalize'));
    expect(finalizes).toHaveLength(2);
    expect(finalizes[1]!.body).toEqual({
      installments: rows.map((row) => ({
        transactionId: row.id,
        version: row.id === rows[1]!.id ? 7 : 1,
      })),
    });
    expect(rows.every((row) => row.status === 'Active')).toBe(true);
    // No per-row finalize loop pretending atomicity.
    expect(r.calls.some((c) => /^\/transaction\/[0-9a-f-]{36}\/finalize$/.test(c.path))).toBe(
      false,
    );
  });
});

describe('Client statement and Receber pagamento', () => {
  it('a Member without a Tab grant gets the explanation and no statement request', async () => {
    const { r } = await setup({ grants: [L.box] });
    await mount(r, 'statement', `/clientes/${L.ana}/extrato`, { id: L.ana });
    expect(await screen.findByText('Extrato de fiado indisponível para seu perfil.')).toBeTruthy();
    expect(r.calls.some((c) => c.path.includes('fiado-statement'))).toBe(false);
  });
  it('no default method: confirming without one is refused locally; PIX has no cashbox leg; Dinheiro writes it', async () => {
    const { r, server } = await setup({ activeSession: true });
    server.add({
      transactionTypeId: L.sale,
      accountId: L.tab,
      value: 50,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    const view = await mount(r, 'payment', `/clientes/${L.ana}/receber`, { id: L.ana });
    expect(await screen.findByTestId('payment-balance')).toBeTruthy();
    await choose('payment-account', L.tab);
    await type('payment-value', '20,00');
    await press('payment-confirm');
    expect(await screen.findByText('Escolha a forma de pagamento.')).toBeTruthy();
    expect(writes(r, '/transaction/fiado-payment')).toHaveLength(0);
    await press(`payment-method-${L.pixMethod}`);
    expect(await screen.findByText('Não entra no caixa')).toBeTruthy();
    await press('payment-confirm');
    await screen.findByTestId('payment-after-balance');
    expect(writes(r, '/transaction/fiado-payment')[0]!.body).not.toHaveProperty(
      'openingAcknowledgment',
    );
    expect(
      [...server.state.rows.values()].some((row) => row.generationKind === 'FiadoCashRepayment'),
    ).toBe(false);
    view.unmount();
    await mount(r, 'payment', `/clientes/${L.ana}/receber`, { id: L.ana });
    await choose('payment-account', L.tab);
    await type('payment-value', '10,00');
    await press(`payment-method-${L.cashMethod}`);
    expect(await screen.findByText('Dinheiro entra no caixa')).toBeTruthy();
    await press('payment-confirm');
    await screen.findByTestId('payment-after-balance');
    expect(
      [...server.state.rows.values()].filter((row) => row.generationKind === 'FiadoCashRepayment'),
    ).toHaveLength(1);
    expect(screen.getByTestId('payment-after-balance').props.children).toMatch(/20,00 · em aberto/);
  });
});

describe('Depósito Dinheiro', () => {
  it('one protected two-leg transfer: cashbox out and generated cofre in, never two manual rows', async () => {
    const { r, server } = await setup({ activeSession: true });
    await mount(r, 'safe-deposit', '/transacoes/deposito-cofre', { accountId: L.box });
    await choose('safe-destination', L.safe);
    await type('safe-value', '100,00');
    await press('safe-confirm');
    expect(await screen.findByTestId('safe-receipt')).toBeTruthy();
    expect(writes(r, '/transaction/safe-deposit')).toHaveLength(1);
    expect(writes(r, '/transaction')).toHaveLength(0);
    expect(screen.getByTestId('safe-open-in')).toBeTruthy();
    expect([...server.state.rows.values()].map((row) => row.generationKind)).toEqual([
      null,
      'SafeCashDeposit',
    ]);
  });
});

describe('Cheque lifecycle', () => {
  it('custody → deposit → return → re-present from the exact cheque id and current version', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 80,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    expect(await screen.findByText('No caixa')).toBeTruthy();
    await press('cheque-action-custody');
    await choose('cheque-sheet-account', L.safe);
    await press('cheque-sheet-confirm');
    expect(await screen.findByText('No cofre')).toBeTruthy();
    await press('cheque-action-deposit');
    await choose('cheque-sheet-account', L.bank);
    await press('cheque-sheet-confirm');
    expect(await screen.findByTestId('cheque-result')).toBeTruthy();
    expect(screen.getByTestId('cheque-result').props).toBeTruthy();
    await screen.findByText('Depositado');
    await press('cheque-action-return');
    await typeDate('cheque-sheet-next', '10/11/2026');
    await type('cheque-sheet-reason', '11');
    await press('cheque-sheet-confirm');
    await screen.findByText('Devolvido');
    await press('cheque-action-represent');
    await press('cheque-sheet-confirm');
    await screen.findByText('Reapresentado (aguardando depósito)');
    const commands = r.calls.filter((c) => c.path.startsWith('/cheque/'));
    expect(commands.map((c) => c.path)).toEqual([
      '/cheque/custody',
      '/cheque/deposit',
      `/cheque/${cheque.id}/return`,
      `/cheque/${cheque.id}/represent`,
    ]);
    expect(
      commands.map((c) => {
        const body = c.body as { items?: { expectedVersion: number }[]; expectedVersion?: number };
        return body.items?.[0]?.expectedVersion ?? body.expectedVersion;
      }),
    ).toEqual([0, 1, 2, 3]);
    expect(commands[2]!.body).toMatchObject({
      clientId: L.ana,
      nextExpectedDepositDate: '2026-11-10',
      returnReason: '11',
    });
    expect(new Set(commands.map((c) => c.headers['idempotency-key'])).size).toBe(4);
  });
  it('conversion ends the cheque and creates one fiado debt without a cashbox leg', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 60,
      date: '2026-10-27T00:00:00',
      clientId: L.bruno,
    });
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    await press('cheque-action-convert');
    await choose('cheque-sheet-account', L.tab);
    await press('cheque-sheet-confirm');
    await screen.findByText('Convertido em fiado');
    const conversions = [...server.state.rows.values()].filter(
      (row) => row.generationKind === 'ChequeFiadoConversion',
    );
    expect(conversions).toHaveLength(1);
    expect(conversions[0]).toMatchObject({ accountId: L.tab, clientId: L.bruno, value: 60 });
    expect([...server.state.rows.values()].filter((row) => row.accountId === L.box)).toHaveLength(
      1,
    );
  });
});

describe('M6 edge matrix', () => {
  it('a turno started by someone else after the opening was confirmed refuses locally, keeps the form and sends nothing', async () => {
    const { r, server } = await setup({ activeSession: false });
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.income);
    await type('create-value', '8,00');
    await act(async () =>
      fireEvent(await screen.findByTestId('create-opening-confirm'), 'checkedChange', true),
    );
    server.state.activeSession = '90000000-0000-4000-8000-000000000777';
    await press('create-save');
    expect(await screen.findByTestId('create-opening-problem')).toBeTruthy();
    expect(writes(r, '/transaction')).toHaveLength(0);
    expect(screen.getByTestId('create-value').props.value).toBe('8,00');
  });
  it('a refused atomic opening keeps every typed value and starts nothing', async () => {
    const { r, server } = await setup({ activeSession: false });
    server.state.fail.set('POST /transaction', {
      status: 409,
      message: 'A abertura proposta mudou.',
    });
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.income);
    await type('create-value', '9,00');
    await act(async () =>
      fireEvent(await screen.findByTestId('create-opening-confirm'), 'checkedChange', true),
    );
    await press('create-save');
    expect(await screen.findByText('A abertura proposta mudou.')).toBeTruthy();
    expect(server.state.sessionsStarted).toBe(0);
    expect(screen.getByTestId('create-value').props.value).toBe('9,00');
  });
  it('the confirmed receipt stays when the follow-up reads fail; nothing is re-sent', async () => {
    const { r, server } = await setup({ activeSession: true });
    await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
    await choose('create-type', L.expense);
    await type('create-value', '4,00');
    server.state.fail.set('GET /transaction', { status: 500, message: 'Falha.' });
    await press('create-save');
    expect(await screen.findByTestId('ledger-receipt')).toBeTruthy();
    await new Promise((resolve) => setTimeout(resolve, 30));
    expect(screen.getByTestId('ledger-receipt')).toBeTruthy();
    expect(writes(r, '/transaction')).toHaveLength(1);
  });
  it('an unlinked Manager never sends Mine (a server no-op that would look like "mine")', async () => {
    const { r } = await setup({ role: 'Manager', linked: false });
    await mount(r, 'transactions', '/transacoes', { mine: 'true' });
    await screen.findByTestId('list-heading');
    await waitFor(() => expect(r.calls.some((c) => c.path === '/transaction')).toBe(true));
    expect(
      r.calls.filter((c) => c.path === '/transaction').every((c) => !c.url.includes('Mine=')),
    ).toBe(true);
    expect(screen.queryByTestId('list-mine')).toBeNull();
  });
  it('a Member saves an own same-day correction without the Manager-only impact preview', async () => {
    const { r, server } = await setup({ activeSession: true });
    const row = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 3,
      date: '2026-10-27T00:00:00',
    });
    await mount(r, 'edit', `/transacoes/${row.id}`, { id: row.id });
    await type('detail-description', 'corrigido');
    await new Promise((resolve) => setTimeout(resolve, 600));
    expect(r.calls.some((c) => c.path.endsWith('/edit-preview'))).toBe(false);
    await press('detail-save');
    await screen.findByTestId('detail-receipt');
    expect(r.calls.filter((c) => c.method === 'PUT')).toHaveLength(1);
  });
  it('the statement maps a negative balance to client credit, never re-signed', async () => {
    const { r, server } = await setup({ activeSession: true });
    server.add({
      transactionTypeId: L.sale,
      accountId: L.tab,
      value: 80,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    server.add({
      transactionTypeId: L.pixMethod,
      accountId: L.tab,
      value: 90,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'statement', `/clientes/${L.ana}/extrato`, { id: L.ana });
    const outstanding = await screen.findByTestId('statement-outstanding');
    expect(outstanding.props.children).toMatch(/\+R\$\s10,00 · crédito do cliente/);
    expect(screen.getByTestId('statement-oldest').props.children).toMatch(/nenhuma/);
  });
  it('an unknown cheque deposit is verified by the identical retry: one bank receipt, never a second deposit', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 70,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    server.state.lose.add('POST /cheque/deposit');
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    await press('cheque-action-deposit');
    await choose('cheque-sheet-account', L.bank);
    await press('cheque-sheet-confirm');
    expect(await screen.findByTestId('cheque-sheet-intent-uncertain')).toBeTruthy();
    await press('cheque-sheet-intent-retry');
    await screen.findByText('Depositado');
    const deposits = r.calls.filter((c) => c.path === '/cheque/deposit');
    expect(deposits).toHaveLength(2);
    expect(deposits[0]!.headers['idempotency-key']).toBe(deposits[1]!.headers['idempotency-key']);
    expect(server.state.receipts.size).toBe(1);
  });
  it('a receivable with settlement events refuses cancellation verbatim; no compensating row is written', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 50,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    await press('cheque-action-custody');
    await choose('cheque-sheet-account', L.safe);
    await press('cheque-sheet-confirm');
    await screen.findByText('No cofre');
    const before = server.state.rows.size;
    await press('detail-cancel');
    await type('detail-cancel-reason', 'erro');
    await confirmLast('Cancelar transação');
    expect(
      await screen.findByText('O recebível já tem eventos e não pode ser cancelado.'),
    ).toBeTruthy();
    expect(server.state.rows.size).toBe(before);
    expect(cheque.status).toBe('Active');
  });
  it('the Draft blocker clears only after a fresh read shows none', async () => {
    const { r, server } = await setup({ activeSession: true });
    const draft = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 5,
      date: '2026-10-27T00:00:00',
      status: 'Draft',
    });
    const view = await mount(r, 'transactions', '/transacoes', {
      accountId: L.box,
      from: '2026-10-27',
      to: '2026-10-27',
      status: 'Draft',
    });
    expect(await screen.findByText(/Rascunhos que bloqueiam/)).toBeTruthy();
    draft.status = 'Active';
    // Nothing changes on screen until the list is actually read again.
    expect(screen.queryByText(/Nenhum rascunho pendente em/)).toBeNull();
    view.unmount();
    await mount(r, 'transactions', '/transacoes', {
      accountId: L.box,
      from: '2026-10-27',
      to: '2026-10-27',
      status: 'Draft',
    });
    expect(await screen.findByText(/Nenhum rascunho pendente em Caixa 1/)).toBeTruthy();
  });
});

it('a missing proposal needs the explicit zero, then the confirmation, then sends explicitZero only', async () => {
  const { r } = await setup({ activeSession: false, missingProposal: true });
  await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
  await choose('create-type', L.income);
  await type('create-value', '6,00');
  await act(async () => fireEvent.press(await screen.findByTestId('create-opening-zero')));
  await waitFor(() =>
    expect(screen.getByTestId('create-opening-zero').props.accessibilityState?.checked).toBe(true),
  );
  // Until the opening is confirmed the preview would only be refused: nothing is asked.
  await new Promise((resolve) => setTimeout(resolve, 700));
  expect(r.calls.some((c) => c.path === '/transaction/preview')).toBe(false);
  await act(async () => fireEvent.press(screen.getByTestId('create-opening-confirm')));
  expect(await screen.findByTestId('create-preview-cash-opening')).toBeTruthy();
  expect(screen.queryByTestId('create-preview-cash-none')).toBeNull();
  const preview = r.calls.find((c) => c.path === '/transaction/preview')!;
  expect((preview.body as { openingAcknowledgment: unknown }).openingAcknowledgment).toEqual({
    explicitZero: true,
    items: [],
  });
  await press('create-save');
  await screen.findByTestId('ledger-receipt-started');
  expect(writes(r, '/transaction')[0]!.body.openingAcknowledgment).toEqual({
    explicitZero: true,
    items: [],
  });
});

it('Nova transação after a receipt starts a fresh form whose input is retained on the device again', async () => {
  const { r } = await setup({ activeSession: true });
  await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
  await choose('create-type', L.expense);
  await type('create-value', '2,00');
  await press('create-save');
  await press('ledger-receipt-new');
  await choose('create-type', L.expense);
  await type('create-value', '7,00');
  await waitFor(async () => {
    const keys = await nativeDeviceStore.getAllKeys();
    const values = await Promise.all(keys.map((k) => nativeDeviceStore.getItem(k)));
    expect(values.some((v) => v?.includes('"value":"700"'))).toBe(true);
  });
});

it('a cancelled preview request is asked again, never shown as unavailable', async () => {
  const { r, server } = await setup({ activeSession: true });
  let first = true;
  const original = server.handle;
  r.intercept(async (path, init) => {
    if (path === '/transaction/preview' && first) {
      first = false;
      const error = new Error('aborted');
      error.name = 'AbortError';
      throw error;
    }
    return original(path, init, r.calls);
  });
  await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
  await choose('create-type', L.expense);
  await type('create-value', '5,00');
  expect(await screen.findByTestId('create-preview')).toBeTruthy();
  expect(screen.queryByTestId('create-preview-error')).toBeNull();
  expect(r.calls.filter((c) => c.path === '/transaction/preview').length).toBeGreaterThanOrEqual(2);
});

it('a client created in Clientes comes back selected and is not cleared by the list read before it existed', async () => {
  const { r, server } = await setup({ activeSession: true });
  void server;
  let created = false;
  const original = (path: string, init: RequestInit) => server.handle(path, init, r.calls);
  r.intercept(async (path, init) => {
    if (path === '/client' && (init.method ?? 'GET') === 'GET' && created)
      return new Response(
        JSON.stringify({
          clients: [
            { id: L.ana, name: 'Ana', phone: '1', branchId: 'b' },
            {
              id: '90000000-0000-4000-8000-000000000072',
              name: 'Nova Cliente',
              phone: '1',
              branchId: 'b',
            },
          ],
        }),
        { status: 200, headers: { 'content-type': 'application/json' } },
      );
    return original(path, init);
  });
  await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
  await choose('create-type', L.expense);
  await screen.findByTestId('create-client');
  // Back from Clientes with the fresh client in the route; any render now reads it.
  created = true;
  mockParams = { accountId: L.box, clientId: '90000000-0000-4000-8000-000000000072' };
  await type('create-value', '1,00');
  await waitFor(() =>
    expect(screen.getByTestId('create-client').props.accessibilityLabel).toMatch(/Nova Cliente/),
  );
  expect(screen.queryByTestId('create-cleared')).toBeNull();
});

it('back from Clientes on a fresh form instance, the typed values come back with the new client', async () => {
  const { r } = await setup({ activeSession: true });
  const first = await mount(r, 'create', '/transacoes/nova', { accountId: L.box });
  await choose('create-type', L.expense);
  await type('create-value', '9,90');
  // Let the device copy be written, then the router opens a new instance with the client.
  await new Promise((resolve) => setTimeout(resolve, 300));
  first.unmount();
  await mount(r, 'create', '/transacoes/nova', { accountId: L.box, clientId: L.ana });
  await waitFor(() => expect(screen.getByTestId('create-value').props.value).toBe('9,90'));
  expect(screen.getByTestId('create-client').props.accessibilityLabel).toMatch(/Ana/);
  expect(screen.queryByText('Há texto guardado neste dispositivo.')).toBeNull();
  expect(writes(r, '/transaction')).toHaveLength(0);
});

it('a self-context re-check during Receber pagamento keeps the screen (and its receipt) mounted', async () => {
  const { r, server } = await setup({ activeSession: true });
  server.add({
    transactionTypeId: L.sale,
    accountId: L.tab,
    value: 50,
    date: '2026-10-27T00:00:00',
    clientId: L.ana,
  });
  await mount(r, 'payment', `/clientes/${L.ana}/receber`, { id: L.ana });
  await choose('payment-account', L.tab);
  await type('payment-value', '10,00');
  await press(`payment-method-${L.pixMethod}`);
  // An authority re-check (as before a protected send) must not tear the form down.
  await act(async () => {
    await r.engine.recheckSelfContext();
  });
  expect(screen.getByTestId('payment-value').props.value).toBe('10,00');
  await press('payment-confirm');
  expect(await screen.findByTestId('payment-after-balance')).toBeTruthy();
});

describe('M6 review corrections', () => {
  const fillPlan = async (value: string, account = L.bank) => {
    await choose('plan-type', L.cheque);
    await choose('plan-account', account);
    await type('plan-value', value);
    await act(async () => fireEvent.press(screen.getByLabelText('Gerar mensais')));
    await typeDate('plan-first-due', '27/11/2026');
    await type('plan-count', '3');
  };
  it('a plan restored on return from Clientes sends the value edited after the restore', async () => {
    const { r } = await setup({ role: 'Manager', activeSession: true });
    const first = await mount(r, 'plan-new', '/transacoes/cheque-pre-datado/novo');
    await fillPlan('300,00');
    await new Promise((resolve) => setTimeout(resolve, 300));
    first.unmount();
    await mount(r, 'plan-new', '/transacoes/cheque-pre-datado/novo', { clientId: L.ana });
    await waitFor(() => expect(screen.getByTestId('plan-value').props.value).toBe('300,00'));
    await type('plan-value', '600,00');
    await screen.findByText(/total R\$\s600,00/);
    await act(async () => fireEvent(screen.getByTestId('plan-accept'), 'checkedChange', true));
    await press('plan-save-draft');
    await screen.findByTestId('plan-receipt');
    expect(writes(r, '/transaction/installment')[0]!.body).toMatchObject({
      value: 600,
      clientId: L.ana,
    });
  });
  it('an edit made while the turno is being checked is not sent without accepting it', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    await mount(r, 'plan-new', '/transacoes/cheque-pre-datado/novo');
    await fillPlan('300,00', L.box);
    await screen.findByTestId('plan-preview-row-3');
    await act(async () => fireEvent(screen.getByTestId('plan-accept'), 'checkedChange', true));
    const gate = deferred<void>();
    server.state.hold.set('GET /session/opening-proposal', gate.promise);
    await act(async () => fireEvent.press(screen.getByTestId('plan-save-draft')));
    await type('plan-value', '600,00');
    server.state.hold.delete('GET /session/opening-proposal');
    await act(async () => gate.resolve());
    expect(await screen.findByTestId('plan-changed')).toBeTruthy();
    expect(writes(r, '/transaction/installment')).toHaveLength(0);
  });
  it('after a payment the balance shown is a fresh read; a failed reread never shows the old balance', async () => {
    const { r, server } = await setup({ activeSession: true });
    server.add({
      transactionTypeId: L.sale,
      accountId: L.tab,
      value: 50,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'payment', `/clientes/${L.ana}/receber`, { id: L.ana });
    await screen.findByTestId('payment-balance');
    await choose('payment-account', L.tab);
    await type('payment-value', '20,00');
    await press(`payment-method-${L.pixMethod}`);
    server.state.fail.set(`GET /client/${L.ana}/fiado-statement`, {
      status: 503,
      message: 'Indisponível.',
    });
    await press('payment-confirm');
    expect(await screen.findByTestId('payment-after-error')).toBeTruthy();
    expect(screen.queryByTestId('payment-after-balance')).toBeNull();
    expect(screen.queryByText(/50,00 · em aberto/)).toBeNull();
    await act(async () => fireEvent.press(screen.getByLabelText('Reler saldo')));
    expect((await screen.findByTestId('payment-after-balance')).props.children).toMatch(
      /30,00 · em aberto/,
    );
  });
  it('a failed cheque reread after a confirmed command keeps the receipt and offers no new action', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 80,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    await press('cheque-action-custody');
    await choose('cheque-sheet-account', L.safe);
    // Every reread fails from now on (the detail screen and the card share that read).
    r.intercept(async (path, init) =>
      path === `/receivable/${cheque.id}` && (init.method ?? 'GET') === 'GET'
        ? new Response(JSON.stringify({ errorMessages: ['Indisponível.'] }), {
            status: 503,
            headers: { 'content-type': 'application/json' },
          })
        : server.handle(path, init, r.calls),
    );
    await press('cheque-sheet-confirm');
    expect(await screen.findByTestId('cheque-result')).toBeTruthy();
    expect(await screen.findByTestId('cheque-error')).toBeTruthy();
    expect(screen.queryByTestId('cheque-action-deposit')).toBeNull();
    expect(r.calls.filter((c) => c.path === '/cheque/custody')).toHaveLength(1);
  });
});

it('a failed parent transaction reread after a confirmed cheque command keeps the receipt and blocks commands', async () => {
  const { r, server } = await setup({ role: 'Manager', activeSession: true });
  const cheque = server.add({
    transactionTypeId: L.cheque,
    accountId: L.box,
    value: 80,
    date: '2026-10-27T00:00:00',
    clientId: L.ana,
  });
  await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
  await press('cheque-action-custody');
  await choose('cheque-sheet-account', L.safe);
  // From now on every read of the transaction itself fails (the receivable read still works).
  r.intercept(async (path, init) =>
    path === `/transaction/${cheque.id}` && (init.method ?? 'GET') === 'GET'
      ? new Response(JSON.stringify({ errorMessages: ['Indisponível.'] }), {
          status: 503,
          headers: { 'content-type': 'application/json' },
        })
      : server.handle(path, init, r.calls),
  );
  await press('cheque-sheet-confirm');
  expect(await screen.findByTestId('detail-read-failed')).toBeTruthy();
  expect(screen.getByTestId('cheque-result')).toBeTruthy();
  expect(screen.queryByTestId('detail-error')).toBeNull();
  expect(disabled('cheque-action-deposit')).toBe(true);
  expect(disabled('detail-cancel')).toBe(true);
  expect(r.calls.filter((c) => c.path === '/cheque/custody')).toHaveLength(1);
});

describe('open dialogs after a failed transaction reread', () => {
  const failTransactionReads = (
    r: ReturnType<typeof rig>,
    server: ReturnType<typeof ledgerServer>,
    id: string,
  ) =>
    r.intercept(async (path, init) =>
      path === `/transaction/${id}` && (init.method ?? 'GET') === 'GET'
        ? new Response(JSON.stringify({ errorMessages: ['Indisponível.'] }), {
            status: 503,
            headers: { 'content-type': 'application/json' },
          })
        : server.handle(path, init, r.calls),
    );
  /** The app re-reads stale queries when it regains focus. */
  const refocus = async () => {
    await act(async () => {
      focusManager.setFocused(false);
      focusManager.setFocused(true);
    });
  };
  it('an open cancel dialog with a reason cannot send once the transaction reread fails', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const row = server.add({
      transactionTypeId: L.expense,
      accountId: L.box,
      value: 9,
      date: '2026-10-27T00:00:00',
    });
    await mount(r, 'edit', `/transacoes/${row.id}`, { id: row.id });
    await press('detail-cancel');
    await type('detail-cancel-reason', 'erro de lançamento');
    failTransactionReads(r, server, row.id);
    await refocus();
    expect(await screen.findByTestId('detail-read-failed')).toBeTruthy();
    const confirm = screen.getByTestId('detail-cancel-dialog-confirm');
    expect(confirm.props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(confirm));
    expect(r.calls.filter((c) => c.path.endsWith('/cancel'))).toHaveLength(0);
    expect(row.status).toBe('Active');
  });
  it('an open cheque sheet cannot send once the transaction reread fails', async () => {
    const { r, server } = await setup({ role: 'Manager', activeSession: true });
    const cheque = server.add({
      transactionTypeId: L.cheque,
      accountId: L.box,
      value: 80,
      date: '2026-10-27T00:00:00',
      clientId: L.ana,
    });
    await mount(r, 'edit', `/transacoes/${cheque.id}`, { id: cheque.id });
    await press('cheque-action-custody');
    await choose('cheque-sheet-account', L.safe);
    failTransactionReads(r, server, cheque.id);
    await refocus();
    expect(await screen.findByTestId('detail-read-failed')).toBeTruthy();
    const confirm = screen.getByTestId('cheque-sheet-confirm');
    expect(confirm.props.accessibilityState?.disabled).toBe(true);
    await act(async () => fireEvent.press(confirm));
    expect(r.calls.filter((c) => c.path === '/cheque/custody')).toHaveLength(0);
  });
});
