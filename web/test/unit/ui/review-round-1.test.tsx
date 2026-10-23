// @vitest-environment jsdom
// Web M3 independent review, round 1: each finding's reproduction, now a regression check.
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { calendarDate } from '@/components/ui/calendar';
import { DateField, DateRangeField, DayStepper, MonthField } from '@/components/ui/date-picker';
import { Dialog } from '@/components/ui/dialog';
import { FormField } from '@/components/ui/form-field';
import type { CalendarDate } from '@/components/ui/format-port';
import { Input, Textarea } from '@/components/ui/input';
import { Menu, MenuItem } from '@/components/ui/menu';
import { NativeSelect, SearchableSelect, Select, type SelectOption } from '@/components/ui/select';
import { ToastProvider, useToast } from '@/components/ui/toast';
import { installMatchMedia, WithFormat } from '../support/format-double';

beforeEach(() => installMatchMedia(false));
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

const options: SelectOption[] = [
  { value: 'a', label: 'Terminal 1' },
  { value: 'b', label: 'Terminal 2' },
];
const today = calendarDate(2026, 9, 24);

describe('P2-1: a control keeps its own disabled, required and invalid props', () => {
  it('inside and outside a FormField', () => {
    render(
      <>
        <FormField label="Dentro">
          <Input disabled required aria-invalid />
        </FormField>
        <Input aria-label="Fora" disabled required aria-invalid />
        <FormField label="Obs">
          <Textarea value="" readOnly disabled required />
        </FormField>
        <FormField label="Situação">
          <NativeSelect disabled required options={options} />
        </FormField>
      </>,
    );
    for (const name of ['Dentro', 'Fora', 'Obs', 'Situação']) {
      const control = screen.getByLabelText(name) as HTMLInputElement;
      expect(control.disabled, name).toBe(true);
      expect(control.required, name).toBe(true);
    }
    expect(screen.getByLabelText('Dentro').getAttribute('aria-invalid')).toBe('true');
    expect(screen.getByLabelText('Fora').getAttribute('aria-invalid')).toBe('true');
  });

  it('the field still adds its own state when the control sets none', () => {
    render(
      <FormField label="Valor" error="Obrigatório" required disabledReason="Período bloqueado">
        <Input />
      </FormField>,
    );
    const input = screen.getByLabelText('Valor') as HTMLInputElement;
    expect([input.disabled, input.required, input.getAttribute('aria-invalid')]).toEqual([
      true,
      true,
      'true',
    ]);
  });
});

describe('P2-2: a disabled select never opens or changes value', () => {
  it('listbox select in an unavailable field, by pointer and keyboard; and by its own prop', () => {
    const onValueChange = vi.fn();
    render(
      <>
        <FormField label="Conta" disabledReason="Somente o gerente altera">
          <Select options={options} value={null} onValueChange={onValueChange} />
        </FormField>
        <FormField label="Outra">
          <Select options={options} value={null} onValueChange={onValueChange} disabled />
        </FormField>
      </>,
    );
    for (const name of ['Conta', 'Outra']) {
      const trigger = screen.getByRole('combobox', { name });
      expect(trigger.getAttribute('aria-disabled')).toBe('true');
      fireEvent.click(trigger);
      for (const key of ['ArrowDown', 'Enter', ' ', 'Home', 't'])
        fireEvent.keyDown(trigger, { key });
      expect(trigger.getAttribute('aria-expanded')).toBe('false');
    }
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(onValueChange).not.toHaveBeenCalled();
  });

  it('searchable select, wide and compact, in an unavailable field', () => {
    const onValueChange = vi.fn();
    const field = (
      <FormField label="Cliente" disabledReason="Somente o gerente altera">
        <SearchableSelect
          options={options}
          value={null}
          onValueChange={onValueChange}
          resultsLabel={(n) => `${n}`}
        />
      </FormField>
    );
    const { unmount } = render(field);
    const input = screen.getByRole('combobox', { name: 'Cliente' }) as HTMLInputElement;
    expect(input.disabled).toBe(true);
    fireEvent.focus(input);
    fireEvent.keyDown(input, { key: 'ArrowDown' });
    expect(screen.queryByRole('listbox')).toBeNull();
    unmount();
    installMatchMedia(true);
    render(field);
    const trigger = screen.getByRole('button', { name: 'Cliente' });
    expect(trigger.getAttribute('aria-disabled')).toBe('true');
    fireEvent.click(trigger);
    expect(screen.queryByRole('dialog', { hidden: true })).toBeNull();
    expect(onValueChange).not.toHaveBeenCalled();
  });
});

describe('P2-3: a disabled date control blocks the whole composite', () => {
  it('DateField in an unavailable field: no calendar, no new date', () => {
    const onValueChange = vi.fn();
    render(
      <WithFormat>
        <FormField label="Data" disabledReason="Período bloqueado">
          <DateField value={today} onValueChange={onValueChange} today={today} />
        </FormField>
      </WithFormat>,
    );
    expect((screen.getByLabelText('Data') as HTMLInputElement).disabled).toBe(true);
    const opener = screen.getByRole('button', { name: 'Abrir calendário' }) as HTMLButtonElement;
    expect(opener.disabled).toBe(true);
    fireEvent.click(opener);
    expect(screen.queryByRole('grid')).toBeNull();
    expect(onValueChange).not.toHaveBeenCalled();
  });

  it('range, day stepper and month field honor disabled too', () => {
    const changed = vi.fn();
    render(
      <WithFormat>
        <DateRangeField
          legend="Período"
          from={null}
          to={null}
          onRangeChange={changed}
          today={today}
          disabled
        />
        <DayStepper label="Dia" value={today} onValueChange={changed} today={today} disabled />
        <FormField label="Mês" disabledReason="Mês bloqueado">
          <MonthField
            value={{ year: 2026, month: 9 }}
            onValueChange={changed}
            max={{ year: 2026, month: 9 }}
          />
        </FormField>
      </WithFormat>,
    );
    fireEvent.click(screen.getByRole('button', { name: 'Escolher período' }));
    fireEvent.click(screen.getByRole('button', { name: 'Dia anterior' }));
    fireEvent.click(screen.getByRole('button', { name: 'Próximo dia' }));
    fireEvent.click(screen.getByRole('button', { name: 'Mês' }));
    expect(screen.queryByRole('grid')).toBeNull();
    expect(screen.queryByRole('button', { name: 'setembro' })).toBeNull();
    expect(changed).not.toHaveBeenCalled();
  });

  it('a month field without a known month waits instead of guessing a year', () => {
    render(
      <WithFormat>
        <FormField label="Mês">
          <MonthField value={null} onValueChange={() => undefined} />
        </FormField>
      </WithFormat>,
    );
    const trigger = screen.getByRole('button', { name: 'Mês' });
    expect(trigger.getAttribute('aria-disabled')).toBe('true');
    fireEvent.click(trigger);
    expect(screen.queryByRole('button', { name: 'Próximo ano' })).toBeNull();
  });
});

describe('P2-4: each toast keeps its own minimum time and pause', () => {
  function Buttons() {
    const toast = useToast();
    return (
      <>
        <button type="button" onClick={() => toast.showSuccess('Transação salva')}>
          primeiro
        </button>
        <button type="button" onClick={() => toast.showSuccess('Fechamento enviado')}>
          segundo
        </button>
      </>
    );
  }

  it('a toast replaced after 4 s does not shorten the next one', () => {
    vi.useFakeTimers();
    render(
      <ToastProvider>
        <Buttons />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByText('primeiro'));
    act(() => vi.advanceTimersByTime(4000));
    fireEvent.click(screen.getByText('segundo'));
    act(() => vi.advanceTimersByTime(4900));
    expect(screen.getByText('Fechamento enviado')).toBeTruthy();
    act(() => vi.advanceTimersByTime(200));
    expect(screen.queryByText('Fechamento enviado')).toBeNull();
  });

  it('closing a focused toast leaves no pause behind for the next one', () => {
    vi.useFakeTimers();
    render(
      <ToastProvider>
        <Buttons />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByText('primeiro'));
    const close = screen.getByRole('button', { name: 'Fechar' });
    fireEvent.focus(close);
    fireEvent.click(close);
    expect(screen.queryByText('Transação salva')).toBeNull();
    fireEvent.click(screen.getByText('segundo'));
    act(() => vi.advanceTimersByTime(5100));
    expect(screen.queryByText('Fechamento enviado')).toBeNull();
  });

  it('a pause belongs to its message and resumes with the unread time', () => {
    vi.useFakeTimers();
    render(
      <ToastProvider>
        <Buttons />
      </ToastProvider>,
    );
    fireEvent.click(screen.getByText('primeiro'));
    act(() => vi.advanceTimersByTime(3000));
    const surface = screen.getByText('Transação salva').parentElement as HTMLElement;
    fireEvent.pointerEnter(surface);
    act(() => vi.advanceTimersByTime(20_000));
    expect(screen.getByText('Transação salva')).toBeTruthy();
    fireEvent.pointerLeave(surface);
    act(() => vi.advanceTimersByTime(1900));
    expect(screen.getByText('Transação salva')).toBeTruthy();
    act(() => vi.advanceTimersByTime(200));
    expect(screen.queryByText('Transação salva')).toBeNull();
  });
});

describe('P3-5: pickers inside a dialog never open a second modal', () => {
  it('at compact width, calendar, searchable list and menu use the popover layer inside a dialog', () => {
    installMatchMedia(true);
    function Form() {
      const [date, setDate] = useState<CalendarDate | null>(null);
      const [client, setClient] = useState<string | null>(null);
      return (
        <WithFormat>
          <Dialog open title="Novo cliente" onDismiss={() => undefined}>
            <FormField label="Vencimento">
              <DateField value={date} onValueChange={setDate} today={today} />
            </FormField>
            <FormField label="Cliente">
              <SearchableSelect
                options={options}
                value={client}
                onValueChange={setClient}
                resultsLabel={(n) => `${n}`}
              />
            </FormField>
            <Menu title="Ações" trigger="Ações">
              <MenuItem onSelect={() => undefined} label="Duplicar" />
            </Menu>
          </Dialog>
        </WithFormat>
      );
    }
    render(<Form />);
    const dialogs = () => document.querySelectorAll('dialog').length;
    fireEvent.click(screen.getByRole('button', { name: 'Abrir calendário', hidden: true }));
    expect(screen.getByRole('grid', { hidden: true })).toBeTruthy();
    expect(dialogs()).toBe(1);
    fireEvent.keyDown(screen.getByRole('grid', { hidden: true }), { key: 'Escape' });
    // The searchable list stays an inline combobox (no sheet trigger) inside the dialog.
    const search = screen.getByRole('combobox', { name: 'Cliente', hidden: true });
    expect(search.tagName).toBe('INPUT');
    fireEvent.focus(search);
    expect(screen.getByRole('listbox', { hidden: true })).toBeTruthy();
    expect(dialogs()).toBe(1);
    fireEvent.click(screen.getByRole('button', { name: 'Ações', hidden: true }));
    expect(screen.getByRole('menu', { hidden: true })).toBeTruthy();
    expect(dialogs()).toBe(1);
  });
});
