// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PasswordLogin } from '@/components/entry/password-login';

const { login } = vi.hoisted(() => ({ login: vi.fn() }));
vi.mock('@/client/session/session-provider', () => ({
  useSession: () => ({ login, snapshot: { state: 'anonymous' } }),
}));
afterEach(() => {
  cleanup();
  vi.resetAllMocks();
});
function fill() {
  fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'ana@example.invalid' } });
  fireEvent.change(screen.getByLabelText('Senha'), { target: { value: 'temporary password' } });
}

describe('password entry', () => {
  it('uses password-manager hints, visibility, verbatim generic errors and clears only the password', async () => {
    login.mockResolvedValue({
      ok: false,
      status: 401,
      errorMessages: ['Credenciais inválidas.'],
      retryAfterSeconds: null,
    });
    render(<PasswordLogin />);
    fill();
    const password = screen.getByLabelText('Senha') as HTMLInputElement;
    expect(password.type).toBe('password');
    expect(password.autocomplete).toBe('current-password');
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }));
    expect(password.type).toBe('text');
    fireEvent.submit(password.closest('form')!);
    await screen.findByText('Credenciais inválidas.');
    expect(password.value).toBe('');
    expect((screen.getByLabelText('E-mail') as HTMLInputElement).value).toBe('ana@example.invalid');
    expect(document.activeElement?.getAttribute('role')).toBe('alert');
    expect(login).toHaveBeenCalledTimes(1);
  });
  it('suppresses duplicate submissions and permits a manual retry after verbatim 429', async () => {
    let settle!: (value: unknown) => void;
    login.mockReturnValue(
      new Promise((resolve) => {
        settle = resolve;
      }),
    );
    render(<PasswordLogin />);
    fill();
    const form = screen.getByLabelText('Senha').closest('form')!;
    fireEvent.submit(form);
    fireEvent.submit(form);
    expect(login).toHaveBeenCalledTimes(1);
    settle({ ok: false, status: 429, errorMessages: ['Tente mais tarde.'], retryAfterSeconds: 42 });
    await screen.findByText('Tente mais tarde.');
    expect(screen.queryByText(/42 segundos|Aguarde antes/)).toBeNull();
    expect(screen.getByRole('button', { name: 'Entrar' }).getAttribute('aria-busy')).toBeNull();
    expect(login).toHaveBeenCalledTimes(1);
  });
  it('does not call a lost response success or replay it', async () => {
    login.mockRejectedValue(new TypeError('network'));
    render(<PasswordLogin />);
    fill();
    fireEvent.submit(screen.getByLabelText('Senha').closest('form')!);
    await waitFor(() => expect(screen.getByText('Resultado não confirmado')).toBeTruthy());
    expect(login).toHaveBeenCalledTimes(1);
    expect((screen.getByLabelText('Senha') as HTMLInputElement).value).toBe('');
  });
});
