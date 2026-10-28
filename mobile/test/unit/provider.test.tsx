import { Text } from 'react-native';
import { act, render, screen, waitFor } from '@testing-library/react-native';
import { useGetBranchCurrent } from '@loto/api/queries';
import { SessionProvider, useSession, useScopedApiClient } from '../../src/session/provider';
import { sessionGate, platformGate } from '../../src/session/gate';
import { branchId, otherBranch, rig, response, profile } from './support';
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
