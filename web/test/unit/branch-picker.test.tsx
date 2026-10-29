// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BranchPicker } from '@/components/entry/branch-picker';
import { anonymousSnapshot, type SessionSnapshot } from '@/lib/bff-protocol';
const { read, select } = vi.hoisted(() => ({ read: vi.fn(), select: vi.fn() }));
vi.mock('@/client/session/session-provider', () => ({
  useSession: () => ({
    snapshot: { user: { name: 'Ana', email: 'ana@example.invalid' } },
    readBranchChoice: read,
    selectBranch: select,
  }),
}));
vi.mock('@loto/api/queries', () => ({
  useGetUserProfile: () => ({ data: { data: { isPlatformOperator: false } } }),
}));
vi.mock('@/client/query/scoped-query-provider', () => ({ useScopedApiClient: () => ({}) }));
vi.mock('@/components/entry/identity-sheet', () => ({
  IdentitySheet: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}));
vi.mock('@/components/shell/account-menu', () => ({ AccountMenu: () => <span>Sua conta</span> }));
const branches = [
  {
    id: '12345678-1234-1234-1234-123456789abc',
    name: 'Filial Alfa',
    role: 'Admin' as const,
    address: 'Rua Alfa, 1',
    cnpj: '11222333000181',
  },
  { id: '12345678-1234-1234-1234-123456789abd', name: 'Filial Beta', role: 'Member' as const },
];
const choice: SessionSnapshot = { ...anonymousSnapshot, state: 'choose-branch', branches };
afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});
describe('membership choice UI', () => {
  it('renders the successful zero membership setup without branch data', async () => {
    read.mockResolvedValue({
      ok: true,
      snapshot: { ...choice, state: 'no-memberships', branches: [] },
    });
    render(<BranchPicker />);
    expect(await screen.findByText('Você ainda não tem acesso a uma lotérica.')).toBeTruthy();
    expect(screen.queryByText('Filial Alfa')).toBeNull();
    expect(read).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Ana · ana@example.invalid')).toBeTruthy();
  });
  it('uses whole-row buttons including role/address/CNPJ and suppresses competing selection', async () => {
    read.mockResolvedValue({ ok: true, snapshot: choice });
    let settle!: (value: unknown) => void;
    select.mockReturnValue(
      new Promise((resolve) => {
        settle = resolve;
      }),
    );
    render(<BranchPicker />);
    const alfa = await screen.findByRole('button', { name: /Filial Alfa.*Administrador/ });
    expect(screen.getByText('Rua Alfa, 1')).toBeTruthy();
    expect(screen.getByText('11.222.333/0001-81')).toBeTruthy();
    fireEvent.click(alfa);
    fireEvent.click(screen.getByRole('button', { name: /Filial Beta/ }));
    expect(select).toHaveBeenCalledTimes(1);
    expect(screen.getByRole('button', { name: 'Abrindo…' }).getAttribute('aria-busy')).toBe('true');
    await act(async () => settle({ ok: false, errorMessages: ['Filial não encontrada.'] }));
    expect(screen.getByText('Filial não encontrada.')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Atualizar filiais' }));
    expect(read).toHaveBeenCalledTimes(2);
  });
  it('does not treat a failed membership read as an empty membership list', async () => {
    read.mockRejectedValue(new Error('offline'));
    render(<BranchPicker />);
    await screen.findByText('Não foi possível abrir a filial');
    expect(screen.queryByText('Você ainda não tem acesso a uma lotérica.')).toBeNull();
    expect(select).not.toHaveBeenCalled();
  });
});
