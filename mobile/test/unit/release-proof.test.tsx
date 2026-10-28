import { render } from '@testing-library/react-native';
import { readNativeFixture } from '../../src/probes/native-fixture';
import { SessionHarness } from '../../src/probes/session-harness';
import { loseNextFixtureResponse, sessionTransport } from '../../src/probes/session-transport';
import { response } from './support';
jest.mock('../../src/session/provider', () => ({ useOptionalSession: () => null }));
jest.mock('../../src/storage/native', () => ({ secureUuid: jest.fn() }));
it('release disables technical UI and lost-response injection even when proof flags are supplied', async () => {
  const dev = Object.getOwnPropertyDescriptor(globalThis, '__DEV__')!;
  const flag = process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF;
  const prefill = process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL;
  const transport = jest
    .spyOn(globalThis, 'fetch')
    .mockResolvedValue(response({ fixture: 'confirmed' }, 201));
  try {
    Object.defineProperty(globalThis, '__DEV__', {
      value: false,
      configurable: true,
      writable: true,
    });
    process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF = '1';
    process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL = '1';
    expect(render(<SessionHarness />).toJSON()).toBeNull();
    await expect(readNativeFixture('http://127.0.0.1:5000')).rejects.toThrow(
      'Fixture técnica indisponível.',
    );
    loseNextFixtureResponse();
    const result = await sessionTransport('http://127.0.0.1:5000/transaction', { method: 'POST' });
    expect(result.status).toBe(201);
    expect(transport).toHaveBeenCalledTimes(1);
  } finally {
    Object.defineProperty(globalThis, '__DEV__', dev);
    if (flag === undefined) delete process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF;
    else process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF = flag;
    if (prefill === undefined) delete process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL;
    else process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL = prefill;
    transport.mockRestore();
  }
});
