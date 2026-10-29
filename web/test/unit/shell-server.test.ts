import { beforeEach, describe, expect, it, vi } from 'vitest';
import DestinationPage from '@/app/f/[branchId]/[[...destination]]/page';
import { ApplicationFrame } from '@/components/shell/application-frame';
const state = vi.hoisted(() => ({ fail: 'self' as 'self' | 'branches', reads: [] as string[] }));
vi.mock('@/components/shell/application-frame', () => ({ ApplicationFrame: () => null }));
vi.mock('@/server/gate/guards', () => ({
  requireBranchSession: async () => {
    state.reads.push('gate');
  },
  gateRedirect: () => {
    throw new Error('redirect');
  },
}));
vi.mock('@/server/api', () => ({
  serverRead: async (_scope: unknown, read: (client: unknown) => Promise<unknown>) => read({}),
}));
vi.mock('@loto/api', async (importOriginal) => {
  const api = await importOriginal<typeof import('@loto/api')>();
  const failure = () =>
    new api.ApiError({
      kind: 'http',
      method: 'GET',
      path: '/optional',
      dispatched: true,
      status: 503,
      errorMessages: ['Serviço indisponível.'],
    });
  return {
    ...api,
    getBranchCurrent: async () => {
      state.reads.push('current');
      return { data: { branch: { role: 'Manager' }, branchLocalDate: '2026-10-28' } };
    },
    getOperatorSelfContext: async () => {
      if (state.fail === 'self') throw failure();
      return { data: { operatorId: null, availableAccounts: [] } };
    },
    getBranchMyBranches: async () => {
      if (state.fail === 'branches') throw failure();
      return { data: { branches: [] } };
    },
  };
});
beforeEach(() => {
  state.reads = [];
});
describe('optional shell reads never block permitted management pages', () => {
  for (const failing of ['self', 'branches'] as const)
    for (const destination of [
      ['gestao', 'fila'],
      ['relatorios', 'dashboard'],
      ['fechamentos', 'abrir'],
      ['fechamentos', '12345678-1234-1234-1234-123456789abc'],
      ['fechamentos', '12345678-1234-1234-1234-123456789abc', 'correcao'],
    ]) {
      it(`${failing} failure preserves the frame for /${destination.join('/')}`, async () => {
        state.fail = failing;
        const element = await DestinationPage({
          params: Promise.resolve({
            branchId: '12345678-1234-1234-1234-123456789abc',
            destination,
          }),
        });
        expect(element.type).toBe(ApplicationFrame);
        expect(state.reads.slice(0, 2)).toEqual(['gate', 'current']);
        if (failing === 'self') {
          expect(element.props.initialSelf).toBeNull();
          expect(element.props.initialSelfFailure).toEqual(['Serviço indisponível.']);
        } else {
          expect(element.props.multiple).toBeNull();
          expect(element.props.initialBranchFailure).toEqual(['Serviço indisponível.']);
        }
      });
    }
});
