import { ApiError } from '@loto/api';
import { act, renderHook } from '@testing-library/react-native';
import { useSession } from '../../src/session/provider';
import { useDeviceDraft } from '../../src/data/use-device-draft';
import { deferred } from './support';
jest.mock('../../src/session/provider', () => ({ useSession: jest.fn() }));
function services() {
  const value = {
    store: { scope: jest.fn(async () => ({})) },
    drafts: {
      save: jest.fn(async (_scope: unknown, _resource: string, input: unknown, base: unknown) => ({
        revision: 'durable',
        input,
        base,
      })),
      restore: jest.fn(async () => ({ status: 'none' })),
      acknowledge: jest.fn(async () => {}),
    },
  };
  jest.mocked(useSession).mockReturnValue(value as unknown as ReturnType<typeof useSession>);
  return value;
}
it('retains failed input in memory with explicit storage recovery copy and allows retry', async () => {
  const s = services();
  s.drafts.save.mockRejectedValueOnce(new Error('device full'));
  const { result } = renderHook(() => useDeviceDraft<{ notes: string }>('record:one'));
  await act(async () => {
    expect(await result.current.save({ notes: 'rascunho' }, {})).toEqual({ ok: false });
  });
  expect(result.current.memory?.input).toEqual({ notes: 'rascunho' });
  expect(result.current.problem).toBe(
    'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.',
  );
  await act(async () => {
    await result.current.save({ notes: 'rascunho' }, {});
  });
  expect(result.current.memory?.revision).toBe('durable');
  expect(result.current.problem).toBeNull();
});
it('a late save failure for another resource cannot disclose or replace the current form', async () => {
  const s = services();
  const held = deferred<never>();
  s.drafts.save.mockImplementationOnce(() => held.promise);
  const { result, rerender } = renderHook(
    ({ resource }: { resource: string }) => useDeviceDraft<{ notes: string }>(resource),
    { initialProps: { resource: 'record:one' } },
  );
  let pending!: Promise<unknown>;
  await act(async () => {
    pending = result.current.save({ notes: 'primeiro' }, {});
    await Promise.resolve();
  });
  rerender({ resource: 'record:two' });
  expect(result.current.memory).toBeNull();
  await act(async () => {
    await result.current.save({ notes: 'segundo' }, {});
  });
  await act(async () => {
    held.reject(new Error('old save failed'));
    await pending;
  });
  expect(result.current.memory?.input).toEqual({ notes: 'segundo' });
  expect(result.current.problem).toBeNull();
  rerender({ resource: 'record:one' });
  expect(result.current.memory).toBeNull();
});
it('does not disguise a failed fresh authorization check as a device-storage failure', async () => {
  const s = services();
  const refusal = new ApiError({
    kind: 'http',
    method: 'GET',
    path: '/branch/current',
    status: 403,
    dispatched: true,
    errorMessages: ['Vínculo removido.'],
  });
  s.drafts.restore.mockRejectedValueOnce(refusal);
  const { result } = renderHook(() => useDeviceDraft<{ notes: string }>('record:one'));
  await act(async () => {
    await expect(
      result.current.restore(
        (v): v is { notes: string } => typeof v === 'object' && v !== null,
        async () => true,
      ),
    ).rejects.toBe(refusal);
  });
  expect(result.current.problem).toBeNull();
});
