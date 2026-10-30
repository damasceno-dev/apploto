import { useEffect } from 'react';
import { Button, Text } from 'react-native';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { useGetBranchCurrent, usePutTimeentry } from '@loto/api/queries';
import { SessionProvider, useSession, useScopedApiClient } from '../../src/session/provider';
import { sessionGate, platformGate } from '../../src/session/gate';
import { branchId, otherBranch, rig, response, profile } from './support';
import { useQueryClient, type QueryClient } from '@tanstack/react-query';
import { configureApiClient } from '@loto/api';

jest.mock('@loto/api', () => {
  const actual = jest.requireActual('@loto/api');
  return { ...actual, configureApiClient: jest.fn(actual.configureApiClient) };
});

jest.mock('@react-native-community/netinfo', () => ({ addEventListener: jest.fn(() => () => {}) }));
jest.mock('../../src/storage/native', () => ({
  nativeDeviceStore: {
    getItem: async () => null,
    setItem: async () => {},
    removeItem: async () => {},
    getAllKeys: async () => [],
  },
  emailPartitionKey: async () => 'email-hash',
  secureUuid: () => '40000000-0000-4000-8000-000000000001',
}));
function Probe({ defaultClient = false }: { defaultClient?: boolean }) {
  const { snapshot } = useSession();
  const client = useScopedApiClient();
  const gate = sessionGate(snapshot, 'branch');
  const query = useGetBranchCurrent({
    query: { enabled: gate === 'allow' },
    request: defaultClient ? undefined : { client },
  });
  return (
    <>
      <Text testID="gate">{gate}</Text>
      <Text testID="branch">{query.data?.data.branch.id ?? 'none'}</Text>
    </>
  );
}
it('uses the actual generated hook under one app-owned scoped query provider and resets on branch change', async () => {
  const r = rig();
  render(
    <SessionProvider engine={r.engine} lifecycle={false}>
      <Probe />
    </SessionProvider>,
  );
  await waitFor(() => expect(screen.getByTestId('gate').props.children).toBe('login'));
  await act(async () => r.login());
  expect(screen.getByTestId('gate').props.children).toBe('branch');
  expect(r.calls.some((c) => c.path === '/branch/current')).toBe(false);
  await act(async () => r.engine.selectBranch(branchId));
  await waitFor(() => expect(screen.getByTestId('branch').props.children).toBe(branchId));
  await act(async () => r.engine.selectBranch(otherBranch));
  await waitFor(() => expect(screen.getByTestId('branch').props.children).toBe(otherBranch));
  await act(async () => r.engine.logout());
  expect(screen.getByTestId('gate').props.children).toBe('login');
  expect(screen.getByTestId('branch').props.children).toBe('none');
});
it('an abandoned render cannot publish a default API client', () => {
  const r = rig();
  jest.mocked(configureApiClient).mockClear();
  function Abandoned(): never {
    throw new Error('render abandoned');
  }
  expect(() =>
    render(
      <SessionProvider engine={r.engine} lifecycle={false}>
        <Abandoned />
      </SessionProvider>,
    ),
  ).toThrow('render abandoned');
  expect(configureApiClient).not.toHaveBeenCalled();
});
it('committed default configuration is ready before generated query effects', async () => {
  const r = rig();
  await r.ready();
  render(
    <SessionProvider engine={r.engine} lifecycle={false}>
      <Probe defaultClient />
    </SessionProvider>,
  );
  await waitFor(() => expect(screen.getByTestId('branch').props.children).toBe(branchId));
});
it('navigation mode cannot confer role or platform authority', async () => {
  const r = rig();
  await r.ready();
  r.role('Member');
  await r.engine.refresh();
  expect(sessionGate(r.engine.getSnapshot(), 'branch', ['Admin', 'Manager'])).toBe('forbidden');
  expect(sessionGate(r.engine.getSnapshot(), 'identity')).toBe('allow');
  expect(platformGate(r.engine.getSnapshot())).toBe('forbidden');
  r.intercept(async (path) =>
    path === '/user/profile' ? response({ ...profile, isPlatformOperator: true }) : undefined,
  );
  await r.engine.refresh();
  expect(platformGate(r.engine.getSnapshot())).toBe('allow');
});

it('destroys mutation GC timers when a scope unmounts without changing their default lifetime', async () => {
  const r = rig();
  await r.ready();
  r.intercept(async (path) => (path === '/timeentry' ? response({ id: 'confirmed' }) : undefined));
  let cache: QueryClient | undefined;
  function Mutator() {
    const query = useQueryClient();
    useEffect(() => {
      cache = query;
    }, [query]);
    const client = useScopedApiClient();
    const mutation = usePutTimeentry({ request: { client } });
    return (
      <>
        <Button
          title="Write"
          onPress={() =>
            mutation.mutate({
              data: {
                operatorId: profile.userId,
                date: '2026-10-27',
                status: 'Present',
                action: 'Open',
              },
            })
          }
        />
        <Text>{mutation.isSuccess ? 'confirmed' : 'waiting'}</Text>
      </>
    );
  }
  const view = render(
    <SessionProvider engine={r.engine} lifecycle={false}>
      <Mutator />
    </SessionProvider>,
  );
  await waitFor(() => expect(r.engine.getSnapshot().writesPaused).toBe(false));
  fireEvent.press(screen.getByText('Write'));
  await screen.findByText('confirmed');
  const mutation = cache!.getMutationCache().getAll()[0]!;
  const dispose = jest.spyOn(mutation, 'destroy');
  const scheduled = jest.spyOn(globalThis, 'setTimeout');
  const cleared = jest.spyOn(globalThis, 'clearTimeout');
  try {
    await act(async () => view.unmount());
    expect(dispose).toHaveBeenCalled();
    const gc = scheduled.mock.calls.flatMap((args, index) =>
      args[1] === 300000 ? [scheduled.mock.results[index]!.value] : [],
    );
    expect(gc.length).toBeGreaterThan(0);
    for (const timer of gc) expect(cleared).toHaveBeenCalledWith(timer);
    expect(cache!.getMutationCache().getAll()).toHaveLength(0);
  } finally {
    scheduled.mockRestore();
    cleared.mockRestore();
    dispose.mockRestore();
  }
});
