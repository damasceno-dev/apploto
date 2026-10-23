// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Button, ButtonLink } from '@/components/ui/button';
import { Checkbox, Radio, RadioGroup, Switch } from '@/components/ui/choice';
import { ErrorSummary } from '@/components/ui/notice';
import { Fieldset, FormField, ReadOnlyField } from '@/components/ui/form-field';
import {
  IdentifierInput,
  Input,
  PasswordInput,
  SearchInput,
  Textarea,
} from '@/components/ui/input';
import { blankMoney, MoneyInput, type MoneyValue } from '@/components/ui/money-input';
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

describe('Button', () => {
  it('paints each variant from its own action tokens', () => {
    render(
      <>
        <Button>Salvar</Button>
        <Button variant="cta">Abrir dia</Button>
        <Button variant="secondary">Voltar</Button>
        <Button variant="text">Abrir transação</Button>
        <Button variant="danger">Cancelar transação</Button>
        <Button variant="danger-solid">Remover acesso</Button>
      </>,
    );
    expect(screen.getByRole('button', { name: 'Salvar' }).className).toContain(
      'bg-action-primary-bg',
    );
    expect(screen.getByRole('button', { name: 'Abrir dia' }).className).toContain(
      'bg-action-cta-bg',
    );
    expect(screen.getByRole('button', { name: 'Voltar' }).className).toContain(
      'border-action-secondary-border',
    );
    expect(screen.getByRole('button', { name: 'Abrir transação' }).className).toContain(
      'text-action-ghost-fg',
    );
    // Outlined danger: translucent states composited over its own default fill.
    const danger = screen.getByRole('button', { name: 'Cancelar transação' }).className;
    expect(danger).toContain('bg-action-danger-bg');
    expect(danger).toContain('linear-gradient(var(--loto-sem-action-danger-bg-hover)');
    expect(screen.getByRole('button', { name: 'Remover acesso' }).className).toContain(
      'bg-action-danger-solid-bg',
    );
  });

  it('a disabled button stays focusable, explains why and cannot be activated — even by Enter in a form', () => {
    const onClick = vi.fn();
    const onSubmit = vi.fn((event: Event) => event.preventDefault());
    render(
      <form onSubmit={(event) => onSubmit(event.nativeEvent)}>
        <input aria-label="Valor" />
        <Button
          type="submit"
          disabled
          disabledReason="Nenhuma alteração desde o último salvamento"
          onClick={onClick}
        >
          Salvar
        </Button>
      </form>,
    );
    const button = screen.getByRole('button', { name: 'Salvar' });
    expect(button).not.toHaveProperty('disabled', true);
    expect(button.getAttribute('aria-disabled')).toBe('true');
    button.focus();
    expect(document.activeElement).toBe(button);
    const reason = screen.getByText('Nenhuma alteração desde o último salvamento');
    expect(button.getAttribute('aria-describedby')).toContain(reason.id);
    expect(button.className).toContain('bg-action-disabled-bg');
    fireEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('while loading: busy, the in-progress verb shown, a second activation blocked', () => {
    const onClick = vi.fn();
    render(
      <Button loading loadingLabel="Salvando…" onClick={onClick}>
        Salvar
      </Button>,
    );
    const button = screen.getByRole('button');
    expect(button.getAttribute('aria-busy')).toBe('true');
    // The idle label is hidden (kept only to hold the width).
    expect(screen.getByText('Salvar').className).toContain('invisible');
    expect(screen.getByText('Salvando…').className).not.toContain('invisible');
    fireEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it('a navigating link-variant is a real link', () => {
    render(<ButtonLink href={'/f/x/transacoes' as never}>Ver transações</ButtonLink>);
    expect(screen.getByRole('link', { name: 'Ver transações' }).getAttribute('href')).toBe(
      '/f/x/transacoes',
    );
  });
});

describe('FormField and its controls', () => {
  it('links label, helper and inline error to the control, and marks it invalid', () => {
    render(
      <FormField
        label="Valor"
        helper="O operador verá este valor"
        error="O valor da transação deve ser maior que zero"
        optional
      >
        <Input />
      </FormField>,
    );
    const input = screen.getByLabelText(/Valor/);
    expect(input.getAttribute('aria-invalid')).toBe('true');
    const describedBy = input.getAttribute('aria-describedby') ?? '';
    expect(describedBy.split(' ').map((id) => document.getElementById(id)?.textContent)).toEqual([
      'O operador verá este valor',
      'O valor da transação deve ser maior que zero',
    ]);
    expect(screen.getByText('(opcional)')).toBeTruthy();
  });

  it('a disabled field shows its reason in place of the helper', () => {
    render(
      <FormField label="Conta" helper="ajuda" disabledReason="Período bloqueado">
        <Input />
      </FormField>,
    );
    expect(screen.queryByText('ajuda')).toBeNull();
    const input = screen.getByLabelText('Conta');
    expect(input).toHaveProperty('disabled', true);
    expect(document.getElementById(input.getAttribute('aria-describedby') ?? '')?.textContent).toBe(
      'Período bloqueado',
    );
  });

  it('a fieldset describes its group; a read-only value is selectable text, not an input', () => {
    render(
      <>
        <Fieldset legend="Forma de pagamento" error="Escolha a forma de pagamento.">
          <span>opções</span>
        </Fieldset>
        <ReadOnlyField label="Abertura" value="R$ 400,00" reason="calculado pelo sistema" />
      </>,
    );
    const group = screen.getByRole('group', { name: 'Forma de pagamento' });
    expect(
      document.getElementById(group.getAttribute('aria-describedby') ?? '')?.textContent,
    ).toContain('Escolha a forma de pagamento.');
    expect(screen.getByRole('group', { name: 'Abertura' }).querySelector('input')).toBeNull();
  });

  it('the error summary takes focus once and links to the field it names', () => {
    render(
      <>
        <FormField label="Motivo" id="motivo">
          <Input />
        </FormField>
        <ErrorSummary
          title="Revise os campos"
          items={[
            { message: 'Informe o motivo', fieldId: 'motivo' },
            { message: 'Mensagem sem campo' },
          ]}
        />
      </>,
    );
    const summary = screen.getByRole('alert');
    expect(document.activeElement).toBe(summary);
    fireEvent.click(screen.getByRole('link', { name: 'Informe o motivo' }));
    expect(document.activeElement).toBe(screen.getByLabelText('Motivo'));
  });
});

describe('Input variants', () => {
  it('password: Mostrar/Ocultar senha toggle with pressed state and the right autocomplete hint', () => {
    render(
      <FormField label="Senha">
        <PasswordInput autoComplete="current-password" />
      </FormField>,
    );
    const input = screen.getByLabelText('Senha');
    expect(input.getAttribute('type')).toBe('password');
    expect(input.getAttribute('autocomplete')).toBe('current-password');
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar senha' }));
    expect(input.getAttribute('type')).toBe('text');
    expect(screen.getByRole('button', { name: 'Ocultar senha' }).getAttribute('aria-pressed')).toBe(
      'true',
    );
  });

  it('e-mail: no auto-capitalization or correction, e-mail keyboard', () => {
    render(<Input type="email" aria-label="E-mail" />);
    const input = screen.getByLabelText('E-mail');
    expect([input.getAttribute('autocapitalize'), input.getAttribute('inputmode')]).toEqual([
      'none',
      'email',
    ]);
  });

  it('textarea: the counter appears near the limit and is linked as the description', () => {
    function Notes() {
      const [value, setValue] = useState('');
      return (
        <Textarea
          aria-label="Observações"
          maxLength={10}
          value={value}
          onChange={(event) => setValue(event.target.value)}
        />
      );
    }
    render(<Notes />);
    const area = screen.getByLabelText('Observações');
    expect(area.getAttribute('aria-describedby')).toBeNull();
    fireEvent.change(area, { target: { value: '  12345678' } });
    const counter = screen.getByText('10/10');
    expect(area.getAttribute('aria-describedby')).toContain(counter.id);
    // Kept exactly as typed, leading spaces included.
    expect((area as HTMLTextAreaElement).value).toBe('  12345678');
  });

  it('search: clear empties and refocuses; the count is announced after typing pauses', () => {
    vi.useFakeTimers();
    function Search() {
      const [value, setValue] = useState('ana');
      return (
        <SearchInput
          aria-label="Buscar cliente"
          value={value}
          onValueChange={setValue}
          resultsAnnouncement={`${value.length} clientes encontrados`}
        />
      );
    }
    render(<Search />);
    const status = screen.getByRole('status');
    expect(status.textContent).toBe('');
    act(() => vi.advanceTimersByTime(700));
    expect(status.textContent).toBe('3 clientes encontrados');
    fireEvent.click(screen.getByRole('button', { name: 'Limpar busca' }));
    expect((screen.getByLabelText('Buscar cliente') as HTMLInputElement).value).toBe('');
    expect(document.activeElement).toBe(screen.getByLabelText('Buscar cliente'));
  });

  it('a masked identifier is masked by the core port only', () => {
    function Cpf() {
      const [value, setValue] = useState('');
      return <IdentifierInput aria-label="CPF" kind="cpf" value={value} onValueChange={setValue} />;
    }
    render(
      <WithFormat>
        <Cpf />
      </WithFormat>,
    );
    fireEvent.change(screen.getByLabelText('CPF'), { target: { value: '123' } });
    expect((screen.getByLabelText('CPF') as HTMLInputElement).value).toBe('cpf<123>');
  });
});

describe('MoneyInput', () => {
  function Money({
    initial = blankMoney,
    onChange,
  }: {
    initial?: MoneyValue;
    onChange?: (value: MoneyValue, source: string) => void;
  }) {
    const [value, setValue] = useState<MoneyValue>(initial);
    return (
      <WithFormat>
        <FormField label="Contagem de Raspadinha">
          <MoneyInput
            variant="count"
            value={value}
            onValueChange={(next, source) => {
              onChange?.(next, source);
              setValue(next);
            }}
          />
        </FormField>
      </WithFormat>
    );
  }
  const type = (input: HTMLElement, inputType: string, data: string | null = null) =>
    act(() => {
      input.dispatchEvent(
        new InputEvent('beforeinput', { inputType, data, cancelable: true, bubbles: true }),
      );
    });

  it('blank is “não informado”, distinct from a recorded zero; R$ is hidden from assistive tech', () => {
    const changes: MoneyValue[] = [];
    render(<Money onChange={(value) => changes.push(value)} />);
    const input = screen.getByLabelText('Contagem de Raspadinha') as HTMLInputElement;
    expect(input.value).toBe('');
    expect(input.placeholder).toBe('não informado');
    expect(screen.getByText('R$').getAttribute('aria-hidden')).toBe('true');
    const spoken = () =>
      document.getElementById(input.getAttribute('aria-describedby')?.split(' ').at(-1) ?? '')
        ?.textContent;
    expect(spoken()).toBe('não informado');
    type(input, 'insertText', '0');
    expect(changes.at(-1)).toEqual({ kind: 'amount', digits: '0' });
    expect(input.value).toBe('[0]');
    expect(spoken()).toBe('falado:0');
    expect(input.getAttribute('inputmode')).toBe('numeric');
  });

  it('digits build the value right to left; deleting removes the last digit, then returns to blank', () => {
    const changes: MoneyValue[] = [];
    render(<Money onChange={(value) => changes.push(value)} />);
    const input = screen.getByLabelText('Contagem de Raspadinha');
    for (const digit of ['1', '2', '3', '4']) type(input, 'insertText', digit);
    expect(changes.at(-1)).toEqual({ kind: 'amount', digits: '1234' });
    type(input, 'insertText', 'x');
    expect(changes).toHaveLength(4);
    for (let index = 0; index < 4; index += 1) type(input, 'deleteContentBackward');
    expect(changes.at(-1)).toEqual(blankMoney);
  });

  it('a leading zero carries nothing; pasting goes through the port and unparseable text changes nothing', () => {
    const changes: [MoneyValue, string][] = [];
    render(<Money onChange={(value, source) => changes.push([value, source])} />);
    const input = screen.getByLabelText('Contagem de Raspadinha');
    type(input, 'insertText', '0');
    type(input, 'insertText', '5');
    expect(changes.at(-1)?.[0]).toEqual({ kind: 'amount', digits: '5' });
    fireEvent.paste(input, { clipboardData: { getData: () => 'R$ 1.234,56' } });
    expect(changes.at(-1)).toEqual([{ kind: 'amount', digits: '123456' }, 'paste']);
    fireEvent.paste(input, { clipboardData: { getData: () => 'doze reais' } });
    expect(changes).toHaveLength(3);
  });

  it('does no money arithmetic and no parsing of its own', () => {
    const source = readFileSync('src/components/ui/money-input.tsx', 'utf8');
    expect(source).not.toMatch(/parseFloat|parseInt|Number\(|\* ?100|\/ ?100|toFixed|Intl\./);
  });
});

describe('Checkbox, Radio and Switch', () => {
  it('an indeterminate checkbox reports mixed; a radio group starts with no answer', () => {
    const onValueChange = vi.fn();
    render(
      <>
        <Checkbox label="Todas as parcelas restantes" indeterminate readOnly />
        <RadioGroup
          legend="Entregou mercadoria contada?"
          value={null}
          onValueChange={onValueChange}
        >
          <Radio value="sim" label="Sim" />
          <Radio value="nao" label="Não" />
        </RadioGroup>
      </>,
    );
    expect(screen.getByRole('checkbox').getAttribute('aria-checked')).toBe('mixed');
    expect(
      screen.getAllByRole('radio').every((radio) => !(radio as HTMLInputElement).checked),
    ).toBe(true);
    fireEvent.click(screen.getByLabelText('Não'));
    expect(onValueChange).toHaveBeenCalledWith('nao');
  });

  it('a switch is a switch, applies at once and writes its state', () => {
    function Pref() {
      const [on, setOn] = useState(false);
      return <Switch label="Reduzir animações" checked={on} onCheckedChange={setOn} />;
    }
    render(<Pref />);
    const control = screen.getByRole('switch', { name: 'Reduzir animações' });
    expect(control.getAttribute('aria-checked')).toBe('false');
    expect(screen.getByText('Desativado')).toBeTruthy();
    fireEvent.click(control);
    expect(control.getAttribute('aria-checked')).toBe('true');
    expect(screen.getByText('Ativado')).toBeTruthy();
  });
});
