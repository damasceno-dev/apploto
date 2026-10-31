import type { ReactNode } from 'react';
import { Modal } from 'react-native';
import { Stack, router } from 'expo-router';
import { renderRouter, screen, fireEvent, waitFor, act } from 'expo-router/testing-library';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { ToastProvider } from '../../src/ui';
import { ThemeProvider } from '../../src/theme/provider';
import { FakeAppearance, FakePlatform, MemoryPreferenceStore } from './ui/support';
import { nativeDeviceStore } from '../../src/storage/native';
import { branchId, otherBranch, rig, response, userId, deferred } from './support';
import type { ResponseTimeEntryJson } from '@loto/api';

let mockRevision = 0;
jest.mock('../../src/storage/native', () => ({
  ...jest.requireActual('../../src/storage/native'),
  secureUuid: () => `40000000-0000-4000-8000-${String(++mockRevision).padStart(12, '0')}`,
  emailPartitionKey: async (email: string) => 'hash:' + email,
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));
const id = '30000000-0000-4000-8000-000000000001';
const segmentId = '40000000-0000-4000-8000-000000000001';
const nextSegmentId = '40000000-0000-4000-8000-000000000002';
const metrics = {
  frame: { x: 0, y: 0, width: 375, height: 812 },
  insets: { top: 47, left: 0, right: 0, bottom: 34 },
};
afterEach(() => jest.useRealTimers());

async function journey() {
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
  const r = rig();
  r.role('Manager');
  await r.ready();
  let saved: ResponseTimeEntryJson | null = null;
  type Barrier = {
    entered: ReturnType<typeof deferred<void>>;
    release: ReturnType<typeof deferred<void>>;
  };
  let renewal: Barrier | null = null;
  let segmentWrite: Barrier | null = null;
  r.intercept(async (path, init) => {
    if (path === '/user/renew-token' && renewal) {
      renewal.entered.resolve();
      await renewal.release.promise;
      return undefined;
    }
    if (path === '/operator')
      return response({ operators: [{ id: userId, name: 'Pessoa teste', branchId }] });
    if (path === '/setting') return response({ lockDate: '2026-09-30' });
    if (path === '/timeentry' && init.method === 'GET')
      return response({
        items: saved ? [saved] : [],
        page: 1,
        pageSize: 20,
        totalCount: saved ? 1 : 0,
        totalPages: 1,
      });
    if (path === '/timeentry' && init.method === 'PUT') {
      const input = JSON.parse(String(init.body));
      saved = {
        ...input,
        id,
        branchId,
        operatorName: 'Pessoa teste',
        active: true,
        totalHours: saved ? 2.25 : 1,
        balanceHours: -5,
        isInProgress: false,
        createdAt: '2026-10-27T12:00:00Z',
        segments: input.segments.length
          ? input.segments.map((segment: object, index: number) => ({
              ...segment,
              id: index ? nextSegmentId : segmentId,
              active: true,
              createdAt: '2026-10-27T12:00:00Z',
            }))
          : [
              {
                id: segmentId,
                active: true,
                clockIn: '2026-10-27T06:00:00',
                clockOut: '2026-10-27T07:00:00',
                createdAt: '2026-10-27T12:00:00Z',
              },
            ],
      };
      return response(saved);
    }
    if (path === '/timeentry/segment/' + segmentId && init.method === 'PUT') {
      if (segmentWrite) {
        segmentWrite.entered.resolve();
        await segmentWrite.release.promise;
      }
      const input = JSON.parse(String(init.body));
      saved = { ...saved!, totalHours: 1.25, segments: [{ ...saved!.segments[0]!, ...input }] };
      return response(saved);
    }
    if (path === '/timeentry/' + id) return response(saved);
    return undefined;
  });
  function Layout() {
    return (
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          <Stack screenOptions={{ headerShown: false }} />
        </ShellProvider>
      </SessionProvider>
    );
  }
  const preference = new MemoryPreferenceStore(),
    appearance = new FakeAppearance(),
    platform = new FakePlatform();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <SafeAreaProvider initialMetrics={metrics}>
      <ThemeProvider store={preference} appearance={appearance} platform={platform}>
        <ToastProvider>{children}</ToastProvider>
      </ThemeProvider>
    </SafeAreaProvider>
  );
  const mounted = renderRouter(
    {
      _layout: Layout,
      index: MobileScreen,
      'gestao/ponto/index': MobileScreen,
      'gestao/ponto/[id]': MobileScreen,
      'turno/hoje': MobileScreen,
    },
    { initialUrl: `/gestao/ponto?section=new&operatorId=${userId}&date=2026-10-27`, wrapper },
  );
  await waitFor(() =>
    expect(screen.getByTestId('time-prepare').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-prepare'));
  await waitFor(() =>
    expect(screen.getByTestId('time-save-day').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-add'));
  fireEvent.changeText(screen.getByTestId('segment-in'), '27/10/2026 06:00');
  fireEvent.changeText(screen.getByTestId('segment-out'), '27/10/2026 07:00');
  fireEvent.press(screen.getByTestId('time-save-day'));
  await waitFor(() => expect(mounted.getPathname()).toBe('/gestao/ponto/' + id));
  await screen.findByTestId('time-resource-ready');
  await waitFor(() =>
    expect(screen.getByTestId('time-edit-segment-1').props.accessibilityState.disabled).toBe(false),
  );
  fireEvent.press(screen.getByTestId('time-edit-segment-1'));
  fireEvent.changeText(screen.getByTestId('segment-edit-in'), '27/10/2026 05:45');
  async function renewWhileEditing(waitForWriteReady = true) {
    renewal = { entered: deferred<void>(), release: deferred<void>() };
    let refreshing: Promise<void> | undefined;
    await act(async () => {
      r.expire();
      refreshing = r.engine.refresh();
      await renewal!.entered.promise;
    });
    try {
      expect(r.engine.getSnapshot().status).toBe('recovering');
      expect(screen.getByTestId('time-session-pause')).toBeTruthy();
      expect(
        screen.UNSAFE_queryAllByType(Modal).filter((modal) => modal.props.visible),
      ).toHaveLength(0);
      expect(
        screen.getByTestId('branch-content', { includeHiddenElements: true }).props.style,
      ).toMatchObject({ opacity: 0 });
      expect(screen.queryByTestId('segment-edit-in')).toBeNull();
      expect(
        screen.getByTestId('segment-edit-in', { includeHiddenElements: true }).props.value,
      ).toBe('27/10/2026 05:45');
      expect(
        screen.getByTestId('time-save-segment', { includeHiddenElements: true }).props
          .accessibilityState.disabled,
      ).toBe(true);
    } finally {
      await act(async () => {
        renewal!.release.resolve();
        await refreshing;
      });
      renewal = null;
    }
    if (waitForWriteReady)
      await waitFor(() =>
        expect(
          screen.getByTestId('time-save-segment', { includeHiddenElements: true }).props
            .accessibilityState.disabled,
        ).toBe(false),
      );
  }
  return {
    r,
    mounted,
    renewWhileEditing,
    holdWrite: () => {
      const barrier: Barrier = { entered: deferred<void>(), release: deferred<void>() };
      segmentWrite = barrier;
      return barrier;
    },
  };
}

it('keeps post-create editing and a confirmed write across renewal and real Router presentation updates', async () => {
  const j = await journey();
  await j.renewWhileEditing();
  await act(async () => router.setParams({ row: segmentId, focus: segmentId }));
  expect(screen.getByTestId('segment-edit-in').props.value).toBe('27/10/2026 05:45');
  const write = j.holdWrite();
  fireEvent.press(screen.getByTestId('time-save-segment'));
  await act(async () => write.entered.promise);
  // A second credential renewal occurs while the server-admitted write is still in flight.
  // The pending form stays mounted/blocked; no mutation is retried.
  await j.renewWhileEditing(false);
  await act(async () => {
    write.release.resolve();
  });
  await screen.findAllByText('Registro salvo no servidor.');
  await waitFor(() => expect(screen.getByText(/Horas: 1h 15min/)).toBeTruthy());
  expect(screen.getByText('27/10/2026 05:45')).toBeTruthy();
  expect(screen.queryByText('Há texto guardado neste dispositivo.')).toBeNull();
  await act(async () => router.setParams({ row: undefined, focus: undefined }));
  expect(screen.getByText(/Horas: 1h 15min/)).toBeTruthy();
  fireEvent.press(screen.getByTestId('time-add'));
  fireEvent.changeText(screen.getByTestId('segment-2-in'), '27/10/2026 08:00');
  fireEvent.press(screen.getByTestId('time-save-day'));
  await waitFor(() => expect(screen.getByText('27/10/2026 08:00')).toBeTruthy());
  const snapshots = j.r.calls.filter((call) => call.path === '/timeentry' && call.method === 'PUT');
  expect(snapshots).toHaveLength(2);
  expect(snapshots[1]!.body).toMatchObject({
    segments: [
      { id: segmentId, clockIn: '2026-10-27T05:45:00' },
      { id: null, clockIn: '2026-10-27T08:00:00' },
    ],
  });
  expect(
    j.r.calls.filter(
      (call) => call.path === '/timeentry/segment/' + segmentId && call.method === 'PUT',
    ),
  ).toHaveLength(1);
  j.mounted.unmount();
});

it('does not retain the verified view for a different branch claim or a revoked role', async () => {
  const j = await journey();
  const reads = j.r.calls.filter((call) => call.path.startsWith('/timeentry')).length;
  await act(async () => router.setParams({ branchId: otherBranch }));
  await screen.findByText('Este destino pertence a outra filial.');
  expect(screen.queryByTestId('segment-edit-in', { includeHiddenElements: true })).toBeNull();
  expect(j.r.calls.filter((call) => call.path.startsWith('/timeentry'))).toHaveLength(reads);
  await act(async () => {
    j.r.role('Member');
    await j.r.engine.refresh();
  });
  expect(screen.queryByTestId('time-edit-segment-1', { includeHiddenElements: true })).toBeNull();
  expect(j.r.calls.filter((call) => call.path.startsWith('/timeentry'))).toHaveLength(reads);
  j.mounted.unmount();
});

it('suspends and restores a picker and confirmation without stacking native modals during renewal', async () => {
  const j = await journey();
  fireEvent.press(screen.getByTestId('time-status'));
  expect(screen.UNSAFE_queryAllByType(Modal).filter((modal) => modal.props.visible)).toHaveLength(
    1,
  );
  await j.renewWhileEditing();
  expect(screen.UNSAFE_queryAllByType(Modal).filter((modal) => modal.props.visible)).toHaveLength(
    1,
  );
  fireEvent.press(screen.getByTestId('time-status-option-Present'));
  fireEvent.press(screen.getByTestId('time-remove-segment-1'));
  await screen.findByText('Remover segmento');
  await j.renewWhileEditing();
  await screen.findByText('Remover segmento');
  fireEvent.press(screen.getByRole('button', { name: 'Voltar' }));
  expect(screen.getByTestId('segment-edit-in').props.value).toBe('27/10/2026 05:45');
  expect(j.r.calls.filter((call) => call.method === 'DELETE')).toHaveLength(0);
  j.mounted.unmount();
});
