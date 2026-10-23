// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { Disclosure } from '@/components/ui/disclosure';
import { EmptyState } from '@/components/ui/empty-state';
import { Menu, MenuGroup, MenuItem, MenuRadioItem, MenuUnavailable } from '@/components/ui/menu';
import { ErrorMessages, Notice, UncertainNotice } from '@/components/ui/notice';
import { SaveBar, SaveStatus, saveStatusText, type SaveState } from '@/components/ui/save-status';
import { LoadingRegion, Skeleton } from '@/components/ui/skeleton';
import { Stepper } from '@/components/ui/stepper';
import { SegmentedControl, Tabs } from '@/components/ui/tabs';
import { ToastProvider, toastDurationMs, useToast } from '@/components/ui/toast';
import { parseThemePreference, themeCookie } from '@/theme/theme';
import { ThemeControl } from '@/theme/theme-control';
import { ThemeProvider } from '@/theme/theme-provider';
import { installMatchMedia, WithFormat } from '../support/format-double';

vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

beforeEach(() => installMatchMedia(false));
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

describe('Dialog', () => {
  function Harness({
    busy = false,
    onDismiss,
  }: {
    busy?: boolean;
    onDismiss: (reason: string) => void;
  }) {
    const [open, setOpen] = useState(false);
    return (
      <>
        <button type="button" onClick={() => setOpen(true)}>
          Novo cliente
        </button>
        <Dialog
          open={open}
          busy={busy}
          title="Novo cliente"
          description="O cliente fica disponível para todas as transações."
          onDismiss={(reason) => {
            onDismiss(reason);
            setOpen(false);
          }}
        >
          <label>
            Nome <input />
          </label>
        </Dialog>
      </>
    );
  }

  it('moves focus to the first field, is named and described, and returns focus to the trigger', () => {
    const onDismiss = vi.fn();
    render(<Harness onDismiss={onDismiss} />);
    const trigger = screen.getByRole('button', { name: 'Novo cliente' });
    trigger.focus();
    fireEvent.click(trigger);
    const dialog = screen.getByRole('dialog', { name: 'Novo cliente', hidden: true });
    expect(
      document.getElementById(dialog.getAttribute('aria-describedby') ?? '')?.textContent,
    ).toBe('O cliente fica disponível para todas as transações.');
    expect(document.activeElement).toBe(screen.getByLabelText('Nome'));
    expect(document.documentElement.style.overflow).toBe('hidden');
    fireEvent.click(screen.getByRole('button', { name: 'Fechar', hidden: true }));
    expect(onDismiss).toHaveBeenCalledWith('close');
    expect(document.activeElement).toBe(trigger);
    expect(document.documentElement.style.overflow).toBe('');
  });

  it('Escape (the cancel event) and the backdrop dismiss through the same path — not while busy', () => {
    const onDismiss = vi.fn();
    const { rerender } = render(<Harness onDismiss={onDismiss} />);
    fireEvent.click(screen.getByRole('button', { name: 'Novo cliente' }));
    const dialog = document.querySelector('dialog') as HTMLDialogElement;
    const cancel = new Event('cancel', { cancelable: true });
    act(() => {
      dialog.dispatchEvent(cancel);
    });
    expect(cancel.defaultPrevented).toBe(true);
    expect(onDismiss).toHaveBeenLastCalledWith('escape');
    rerender(<Harness busy onDismiss={onDismiss} />);
    fireEvent.click(screen.getByRole('button', { name: 'Novo cliente' }));
    const busyDialog = document.querySelector('dialog') as HTMLDialogElement;
    act(() => {
      busyDialog.dispatchEvent(new Event('cancel', { cancelable: true }));
    });
    fireEvent.pointerDown(busyDialog);
    expect(onDismiss).toHaveBeenCalledTimes(1);
    expect(
      screen.getByRole('button', { name: 'Fechar', hidden: true }).getAttribute('aria-disabled'),
    ).toBe('true');
  });

  it('a destructive confirmation names its action, uses the solid danger fill and focuses the title', () => {
    render(
      <ConfirmDialog
        open
        onDismiss={() => undefined}
        onConfirm={() => undefined}
        title="Cancelar esta transação?"
        consequence="A transação deixa de contar nos totais."
        confirmLabel="Cancelar transação"
        destructive
      />,
    );
    expect(document.activeElement?.textContent).toBe('Cancelar esta transação?');
    expect(
      screen.getByRole('button', { name: 'Cancelar transação', hidden: true }).className,
    ).toContain('bg-action-danger-solid-bg');
    expect(screen.getByRole('button', { name: 'Voltar', hidden: true })).toBeTruthy();
  });
});

describe('Menu', () => {
  function SessionMenu({ onSair }: { onSair: () => void }) {
    const [mode, setMode] = useState('gestao');
    return (
      <Menu
        title="Conta"
        trigger="Ricardo Moura"
        triggerLabel="Conta de Ricardo Moura, Gestão"
        header={<p>ricardo@exemplo.com.br</p>}
      >
        <MenuGroup label="Modo de trabalho">
          <MenuRadioItem
            checked={mode === 'gestao'}
            onSelect={() => setMode('gestao')}
            label="Gestão"
          />
          <MenuUnavailable
            label="Meu turno"
            reason="Indisponível: seu login não está vinculado a um operador desta filial."
            fix={{ label: 'Resolver em Operadores', href: '/f/x/gestao/operadores' }}
          />
        </MenuGroup>
        <MenuGroup>
          <MenuItem onSelect={onSair} label="Sair" />
        </MenuGroup>
      </Menu>
    );
  }

  it('menu button: ArrowDown opens on the first item, arrows wrap, type-ahead, Escape returns focus', () => {
    render(<SessionMenu onSair={() => undefined} />);
    const trigger = screen.getByRole('button', { name: 'Conta de Ricardo Moura, Gestão' });
    expect(trigger.getAttribute('aria-haspopup')).toBe('menu');
    trigger.focus();
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    const menu = screen.getByRole('menu', { name: 'Conta' });
    expect(document.activeElement?.textContent).toContain('Gestão');
    expect(screen.getByRole('menuitemradio', { name: /Gestão/ }).getAttribute('aria-checked')).toBe(
      'true',
    );
    fireEvent.keyDown(menu, { key: 'ArrowUp' });
    expect(document.activeElement?.textContent).toBe('Sair');
    fireEvent.keyDown(menu, { key: 'ArrowDown' });
    expect(document.activeElement?.textContent).toContain('Gestão');
    fireEvent.keyDown(menu, { key: 'm' });
    const unavailable = document.activeElement as HTMLElement;
    expect(unavailable.textContent).toBe('Meu turno');
    expect(unavailable.getAttribute('aria-disabled')).toBe('true');
    expect(
      document.getElementById(unavailable.getAttribute('aria-describedby') ?? '')?.textContent,
    ).toContain('Indisponível');
    fireEvent.keyDown(menu, { key: 'Escape' });
    expect(screen.queryByRole('menu')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('choosing a command runs it and closes; on compact layouts the same items open in a sheet', () => {
    const onSair = vi.fn();
    render(<SessionMenu onSair={onSair} />);
    fireEvent.click(screen.getByRole('button', { name: 'Conta de Ricardo Moura, Gestão' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Sair' }));
    expect(onSair).toHaveBeenCalled();
    expect(screen.queryByRole('menu')).toBeNull();
    cleanup();
    installMatchMedia(true);
    render(<SessionMenu onSair={onSair} />);
    const trigger = screen.getByRole('button', { name: 'Conta de Ricardo Moura, Gestão' });
    expect(trigger.getAttribute('aria-haspopup')).toBe('dialog');
    fireEvent.click(trigger);
    expect(screen.getByRole('dialog', { name: 'Conta', hidden: true })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Sair', hidden: true })).toBeTruthy();
  });
});

describe('Tabs, segmented control and disclosure', () => {
  it('tabs: arrows select the next tab and its panel is labelled by it', () => {
    function Balances() {
      const [tab, setTab] = useState('debt');
      return (
        <Tabs
          label="Saldo fiado"
          items={[
            { id: 'debt', label: 'Dívidas em aberto', count: '12' },
            { id: 'credit', label: 'Saldo crédito', count: '3' },
          ]}
          selected={tab}
          onSelect={setTab}
          panel={<p>painel {tab}</p>}
        />
      );
    }
    render(<Balances />);
    const first = screen.getByRole('tab', { name: 'Dívidas em aberto (12)' });
    first.focus();
    fireEvent.keyDown(screen.getByRole('tablist'), { key: 'ArrowRight' });
    const second = screen.getByRole('tab', { name: 'Saldo crédito (3)' });
    expect(second.getAttribute('aria-selected')).toBe('true');
    expect(document.activeElement).toBe(second);
    expect(screen.getByRole('tabpanel', { name: 'Saldo crédito (3)' }).textContent).toBe(
      'painel credit',
    );
  });

  it('segmented control is a radio group marked by more than color', () => {
    function Density() {
      const [value, setValue] = useState<'a' | 'b'>('a');
      return (
        <SegmentedControl
          label="Densidade"
          value={value}
          onValueChange={setValue}
          options={[
            { value: 'a', label: 'Uma' },
            { value: 'b', label: 'Duas' },
          ]}
        />
      );
    }
    render(<Density />);
    const group = screen.getByRole('radiogroup', { name: 'Densidade' });
    fireEvent.keyDown(group, { key: 'ArrowRight' });
    const chosen = screen.getByRole('radio', { name: 'Duas' });
    expect(chosen.getAttribute('aria-checked')).toBe('true');
    expect(chosen.className).toContain('font-semibold');
    expect(chosen.querySelector('svg')).not.toBeNull();
  });

  it('disclosure toggles aria-expanded and its region, never navigating', () => {
    render(
      <Disclosure label="Ajuda sobre diferença de caixa">
        <p>Como a diferença é calculada</p>
      </Disclosure>,
    );
    const toggle = screen.getByRole('button', { name: 'Ajuda sobre diferença de caixa' });
    const region = document.getElementById(
      toggle.getAttribute('aria-controls') ?? '',
    ) as HTMLElement;
    expect([toggle.getAttribute('aria-expanded'), region.hidden]).toEqual(['false', true]);
    fireEvent.click(toggle);
    expect([toggle.getAttribute('aria-expanded'), region.hidden]).toEqual(['true', false]);
  });
});

describe('Stepper', () => {
  it('shows confirmed statuses only; the current step carries aria-current and failures alert', () => {
    render(
      <WithFormat>
        <Stepper
          label="Adicionar funcionário"
          steps={[
            { title: 'Dados do operador', status: 'done', detail: 'Operador criado: Ana Lima' },
            { title: 'Terminal', status: 'current' },
            { title: 'Convite', status: 'failed', detail: 'O e-mail já foi convidado.' },
            { title: 'Acesso', status: 'uncertain' },
            { title: 'Revisão', status: 'skipped' },
          ]}
        />
      </WithFormat>,
    );
    expect(screen.getByText('Terminal').closest('li')?.getAttribute('aria-current')).toBe('step');
    expect(screen.getByText('Operador criado: Ana Lima')).toBeTruthy();
    expect(screen.getByRole('alert').textContent).toContain('O e-mail já foi convidado.');
    expect(screen.getByText('Resultado não confirmado')).toBeTruthy();
    expect(screen.getByText('Opcional — não feito')).toBeTruthy();
    expect(screen.getByText('#2')).toBeTruthy();
  });
});

describe('Notice, Toast, SaveStatus, Skeleton and EmptyState', () => {
  it('backend messages stay verbatim, in order, duplicates included; danger interrupts', () => {
    render(
      <Notice
        kind="danger"
        title="Não foi possível salvar a transação"
        messages={['Conta inativa', 'Conta inativa', 'Data bloqueada']}
      />,
    );
    expect(screen.getByRole('alert')).toBeTruthy();
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      'Conta inativa',
      'Conta inativa',
      'Data bloqueada',
    ]);
    cleanup();
    render(<ErrorMessages messages={[]} />);
    expect(document.body.textContent).toBe('');
  });

  it('an uncertain result is never success: its fixed copy, the messages and only the allowed recovery', () => {
    render(
      <UncertainNotice
        messages={['Erro desconhecido']}
        actions={<Button>Verificar e tentar novamente</Button>}
      />,
    );
    const notice = screen.getByRole('alert');
    expect(notice.textContent).toContain('Resultado não confirmado');
    expect(notice.textContent).toContain('Não foi possível confirmar se a ação foi concluída.');
    expect(notice.textContent).toContain('Erro desconhecido');
    expect(notice.getAttribute('data-kind')).toBe('warning');
  });

  it('a toast is a success echo: at least 5 s, paused while hovered, replaced by the next', () => {
    vi.useFakeTimers();
    expect(toastDurationMs('Transação salva')).toBe(5000);
    expect(toastDurationMs('x'.repeat(100))).toBe(8600);
    function Trigger() {
      const toast = useToast();
      return (
        <>
          <button type="button" onClick={() => toast.showSuccess('Transação salva')}>
            um
          </button>
          <button type="button" onClick={() => toast.showSuccess('Fechamento enviado')}>
            dois
          </button>
        </>
      );
    }
    render(
      <ToastProvider>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByText('um'));
    expect(screen.getByRole('status').textContent).toContain('Transação salva');
    fireEvent.click(screen.getByText('dois'));
    expect(screen.queryByText('Transação salva')).toBeNull();
    fireEvent.pointerEnter(screen.getByText('Fechamento enviado').parentElement as HTMLElement);
    act(() => vi.advanceTimersByTime(9000));
    expect(screen.getByText('Fechamento enviado')).toBeTruthy();
    fireEvent.pointerLeave(screen.getByText('Fechamento enviado').parentElement as HTMLElement);
    act(() => vi.advanceTimersByTime(5100));
    expect(screen.queryByText('Fechamento enviado')).toBeNull();
    expect(document.activeElement).toBe(document.body);
  });

  it('save status: every state truthfully, with times only as passed in', () => {
    const states: SaveState[] = [
      { kind: 'saved', time: '14:32' },
      { kind: 'saved', time: '18:42', date: '23/09' },
      { kind: 'saved' },
      { kind: 'never-recorded' },
      { kind: 'dirty' },
      { kind: 'saving' },
      { kind: 'unsaved-on-server' },
      { kind: 'failed' },
      { kind: 'uncertain' },
      { kind: 'stale' },
      { kind: 'device-only' },
      { kind: 'storage-failed' },
    ];
    expect(states.map(saveStatusText)).toEqual([
      'Salvo às 14:32',
      'Salvo em 23/09 às 18:42',
      'Salvo',
      'Contagem ainda não salva',
      'Alterações não salvas',
      'Salvando…',
      'Alterações não salvas no servidor',
      'Não salvo — tentar novamente',
      'Resultado não confirmado',
      'Este fechamento mudou em outro lugar',
      'Rascunho salvo neste dispositivo — ainda não salvo no servidor',
      'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.',
    ]);
    render(<SaveStatus state={{ kind: 'failed' }} />);
    expect(screen.getByRole('alert').textContent).toBe('Não salvo — tentar novamente');
    cleanup();
    render(<SaveBar state={{ kind: 'saved', time: '14:32' }} clean onSave={() => undefined} />);
    expect(screen.getByRole('button', { name: 'Salvar' }).getAttribute('aria-disabled')).toBe(
      'true',
    );
    expect(screen.getByText('Nenhuma alteração desde o último salvamento')).toBeTruthy();
  });

  it('skeletons: a busy region announced once, shapes hidden from assistive technology', () => {
    const { rerender } = render(
      <LoadingRegion loading skeleton={<Skeleton />}>
        pronto
      </LoadingRegion>,
    );
    const region = screen.getByRole('status').parentElement as HTMLElement;
    expect(region.getAttribute('aria-busy')).toBe('true');
    expect(screen.getByRole('status').textContent).toBe('Carregando…');
    expect(region.querySelector('[aria-hidden="true"]')).not.toBeNull();
    rerender(
      <LoadingRegion loading={false} skeleton={<Skeleton />}>
        pronto
      </LoadingRegion>,
    );
    expect(region.getAttribute('aria-busy')).toBeNull();
    expect(region.textContent).toBe('pronto');
  });

  it('empty states state one fact plus the next action; an all-clear stays neutral', () => {
    render(<EmptyState kind="all-clear" statement="Tudo certo — nada pendente de aprovação." />);
    const statement = screen.getByText('Tudo certo — nada pendente de aprovação.');
    expect(statement.className).toContain('text-text-primary');
    expect(statement.className).not.toMatch(/success/);
  });
});

describe('Theme preference', () => {
  it('Sistema by default; a host-only Secure cookie outside the session', () => {
    expect([
      parseThemePreference(undefined),
      parseThemePreference('dark'),
      parseThemePreference('x'),
    ]).toEqual(['system', 'dark', 'system']);
    expect(themeCookie('dark')).toMatch(
      /^__Host-loto-theme=dark; Path=\/; Max-Age=\d+; SameSite=Lax; Secure$/,
    );
  });

  it('choosing Escuro updates <html> at once and persists on the device', () => {
    render(
      <ThemeProvider initial="system">
        <ThemeControl />
      </ThemeProvider>,
    );
    expect(document.documentElement.getAttribute('data-theme-preference')).toBe('system');
    fireEvent.click(screen.getByRole('radio', { name: 'Escuro' }));
    expect(document.documentElement.getAttribute('data-theme-preference')).toBe('dark');
    expect(screen.getByRole('radio', { name: 'Escuro' }).getAttribute('aria-checked')).toBe('true');
  });
});
