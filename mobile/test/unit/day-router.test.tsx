import type { ReactNode } from 'react';
import { Stack, router } from 'expo-router';
import { renderRouter, screen, fireEvent, waitFor, act } from 'expo-router/testing-library';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { SessionProvider } from '../../src/session/provider';
import { ShellProvider } from '../../src/shell/provider';
import { MobileScreen } from '../../src/shell/screen';
import { ToastProvider } from '../../src/ui';
import { ThemeProvider } from '../../src/theme/provider';
import { autosaveTiming } from '../../src/day/count';
import { FakeAppearance, FakePlatform, MemoryPreferenceStore } from './ui/support';
import { nativeDeviceStore } from '../../src/storage/native';
import { rig, deferred, otherBranch } from './support';
import { dayServer, ids } from './day-server';

let mockRevision = 0;
jest.mock('../../src/storage/native', () => ({
  ...jest.requireActual('../../src/storage/native'),
  secureUuid: () => `40000000-0000-4000-8000-${String(++mockRevision).padStart(12, '0')}`,
  emailPartitionKey: async (email: string) => 'hash:' + email,
}));
jest.mock('../../src/probes/entry-tools', () => ({ DevelopmentTools: () => null }));
afterEach(() => jest.useRealTimers());
const metrics = {
  frame: { x: 0, y: 0, width: 375, height: 812 },
  insets: { top: 47, left: 0, right: 0, bottom: 34 },
};

async function journey(role = 'Member') {
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
  autosaveTiming.delayMs = 20;
  const r = rig();
  r.role(role);
  const server = dayServer({ role });
  let renewal: {
    entered: ReturnType<typeof deferred<void>>;
    release: ReturnType<typeof deferred<void>>;
  } | null = null;
  r.intercept(async (path, init) => {
    if (path === '/user/renew-token' && renewal) {
      renewal.entered.resolve();
      await renewal.release.promise;
      return undefined;
    }
    return server.handle(path, init, r.calls);
  });
  await r.ready();
  const session = server.start(ids.box, ids.me, { [ids.cash]: 100, [ids.raspa]: 0 });
  function Layout() {
    return (
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          <Stack screenOptions={{ headerShown: false }} />
        </ShellProvider>
      </SessionProvider>
    );
  }
  const wrapper = ({ children }: { children: ReactNode }) => (
    <SafeAreaProvider initialMetrics={metrics}>
      <ThemeProvider
        store={new MemoryPreferenceStore()}
        appearance={new FakeAppearance()}
        platform={new FakePlatform()}
      >
        <ToastProvider>{children}</ToastProvider>
      </ThemeProvider>
    </SafeAreaProvider>
  );
  const mounted = renderRouter(
    {
      _layout: Layout,
      index: MobileScreen,
      'turno/hoje': MobileScreen,
      'gestao/fila': MobileScreen,
      'fechamentos/[id]': MobileScreen,
      'fechamentos/[id]/correcao': MobileScreen,
    },
    { initialUrl: `/fechamentos/${session.id}`, wrapper },
  );
  // renderRouter installs fake timers; the count's debounce/announcements need the real clock.
  jest.useRealTimers();
  await screen.findByTestId('close-day');
  return {
    r,
    server,
    session,
    mounted,
    holdRenewal: () => {
      renewal = { entered: deferred<void>(), release: deferred<void>() };
      return renewal;
    },
  };
}
const saves = (r: ReturnType<typeof rig>) =>
  r.calls.filter((c) => c.method === 'PUT' && c.path.endsWith('/items'));

it('renewal while counting keeps the verified close mounted, pauses writes and never loses input', async () => {
  const j = await journey();
  autosaveTiming.delayMs = 50;
  fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
  const barrier = j.holdRenewal();
  let refreshing: Promise<void> | undefined;
  await act(async () => {
    j.r.expire();
    refreshing = j.r.engine.refresh();
    await barrier.entered.promise;
  });
  try {
    expect(j.r.engine.getSnapshot().writesPaused).toBe(true);
    expect(screen.getByTestId('time-session-pause')).toBeTruthy();
    expect(
      screen.getByTestId(`close-input-${ids.cash}`, { includeHiddenElements: true }).props.value,
    ).toBe('150,00');
    await act(async () => new Promise((resolve) => setTimeout(resolve, 120)));
    // Autosave waits for authority: nothing reached the server during renewal.
    expect(saves(j.r)).toHaveLength(0);
  } finally {
    await act(async () => {
      barrier.release.resolve();
      await refreshing;
    });
  }
  await waitFor(() => expect(saves(j.r)).toHaveLength(1));
  expect(saves(j.r)[0]!.body).toMatchObject({ items: [{ productId: ids.cash, value: 150 }] });
  expect(saves(j.r)[0]!.headers['if-match']).toBe('"1"');
  expect(screen.getByTestId(`close-input-${ids.cash}`).props.value).toBe('150,00');
  j.mounted.unmount();
});

it('a different branch claim does not reuse the verified close or send its count', async () => {
  const j = await journey();
  fireEvent.changeText(screen.getByTestId(`close-input-${ids.cash}`), '150,00');
  await waitFor(() => expect(saves(j.r)).toHaveLength(1));
  await act(async () => router.setParams({ branchId: otherBranch }));
  await screen.findByText('Este destino pertence a outra filial.');
  expect(screen.queryByTestId('close-day', { includeHiddenElements: true })).toBeNull();
  expect(saves(j.r)).toHaveLength(1);
  j.mounted.unmount();
});

it('an unlinked Manager in Gestão reaches Close day without an operator link', async () => {
  for (const key of await nativeDeviceStore.getAllKeys()) await nativeDeviceStore.removeItem(key);
  const r = rig();
  r.role('Manager');
  const server = dayServer({ role: 'Manager', linked: false });
  r.intercept((path, init) => server.handle(path, init, r.calls));
  await r.ready();
  const session = server.start(ids.box, ids.other, { [ids.cash]: 100 });
  function Layout() {
    return (
      <SessionProvider engine={r.engine} lifecycle={false}>
        <ShellProvider>
          <Stack screenOptions={{ headerShown: false }} />
        </ShellProvider>
      </SessionProvider>
    );
  }
  const mounted = renderRouter(
    {
      _layout: Layout,
      index: MobileScreen,
      'gestao/fila': MobileScreen,
      'fechamentos/[id]': MobileScreen,
    },
    {
      initialUrl: `/fechamentos/${session.id}`,
      wrapper: ({ children }: { children: ReactNode }) => (
        <SafeAreaProvider initialMetrics={metrics}>
          <ThemeProvider
            store={new MemoryPreferenceStore()}
            appearance={new FakeAppearance()}
            platform={new FakePlatform()}
          >
            <ToastProvider>{children}</ToastProvider>
          </ThemeProvider>
        </SafeAreaProvider>
      ),
    },
  );
  jest.useRealTimers();
  expect(await screen.findByTestId('close-day')).toBeTruthy();
  expect(screen.getByTestId('close-context').props.children).toContain('Gestão');
  expect(screen.queryByText('Seu login ainda não está vinculado a um operador.')).toBeNull();
  mounted.unmount();
});
