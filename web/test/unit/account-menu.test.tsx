// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AccountMenu } from '@/components/shell/account-menu';
import { ThemeProvider } from '@/theme/theme-provider';
import { installMatchMedia } from './support/format-double';
const data = vi.hoisted(() => ({ logout: vi.fn(), password: true, platform: false }));
vi.mock('@/client/session/session-provider', () => ({
  beforeGateEvent: 'before-gate',
  useSession: () => ({
    snapshot: {
      user: { userId: 'user', name: 'Ana', email: 'ana@example.invalid' },
      branch: { name: 'Filial Alfa', role: 'Admin' },
    },
    logout: data.logout,
  }),
}));
vi.mock('@/client/query/scoped-query-provider', () => ({ useScopedApiClient: () => ({}) }));
vi.mock('@/client/storage/partition', () => ({
  sha256Hex: async () => 'hash',
  deviceStorage: () => localStorage,
}));
vi.mock('@loto/api/queries', () => ({
  useGetUserProfile: () => ({
    data: {
      data: {
        name: 'Ana atualizada',
        email: 'ana@example.invalid',
        hasPassword: data.password,
        isPlatformOperator: data.platform,
      },
    },
  }),
}));
vi.mock('@/components/ui/menu', () => ({
  Menu: ({ children, header }: { children: React.ReactNode; header: React.ReactNode }) => (
    <div>
      {header}
      {children}
    </div>
  ),
  MenuItem: ({ label, onSelect }: { label: string; onSelect: () => void }) => (
    <button onClick={onSelect}>{label}</button>
  ),
  MenuLink: ({ label, href }: { label: string; href: string }) => <a href={href}>{label}</a>,
}));
beforeEach(() => {
  installMatchMedia(false);
  localStorage.clear();
  data.password = true;
  data.platform = false;
  data.logout.mockResolvedValue({ ok: false, errorMessages: ['Não foi possível encerrar.'] });
});
afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});
function menu() {
  return render(
    <ThemeProvider initial="light">
      <AccountMenu />
    </ThemeProvider>,
  );
}
describe('core account actions', () => {
  it('uses fresh profile flags and caller name', () => {
    data.password = false;
    data.platform = true;
    menu();
    expect(screen.getByText('Ana atualizada')).toBeTruthy();
    expect(screen.queryByRole('link', { name: 'Alterar senha' })).toBeNull();
    expect(screen.getByRole('link', { name: 'Plataforma' })).toBeTruthy();
  });
  it('signs out directly when no retained input exists and preserves theme preferences', async () => {
    localStorage.setItem('theme-preference', 'dark');
    menu();
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }));
    await screen.findByText('Não foi possível encerrar.');
    expect(data.logout).toHaveBeenCalledTimes(1);
    expect(localStorage.getItem('theme-preference')).toBe('dark');
    expect(screen.queryByRole('dialog')).toBeNull();
  });
  it('requires confirmation only for actual retained input and never discards it', async () => {
    const key = 'loto:draft:v1:hash:branch:counts';
    localStorage.setItem(key, '{"v":1,"data":{"notes":"kept"}}');
    menu();
    fireEvent.click(screen.getByRole('button', { name: 'Sair' }));
    await screen.findByRole('dialog', { name: 'Sair com dados guardados?' });
    expect(data.logout).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole('button', { name: 'Continuar aqui' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(localStorage.getItem(key)).toContain('kept');
  });
});

it('uses the exact retained-input copy and adds verification guidance for an uncertain operation', async () => {
  localStorage.setItem(
    'loto:op:v1:hash:branch:transaction',
    '{"status":"uncertain","dispatches":1,"everUncertain":true}',
  );
  menu();
  fireEvent.click(screen.getByRole('button', { name: 'Sair' }));
  await screen.findByRole('dialog', { name: 'Sair com dados guardados?' });
  expect(
    screen.getByText(
      'Há alterações guardadas neste dispositivo. Elas continuam guardadas e voltam quando você entrar de novo com este e-mail.',
    ),
  ).toBeTruthy();
  expect(
    screen.getByText(
      'O resultado da operação ainda precisa ser conferido depois que você entrar novamente.',
    ),
  ).toBeTruthy();
});
