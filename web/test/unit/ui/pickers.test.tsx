// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  addDays,
  addMonths,
  calendarDate,
  isWallClockTime,
  monthWeeks,
  weekdayOf,
} from '@/components/ui/calendar';
import { DateField, DayStepper, invalidDateMessage } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { asCalendarDate, type CalendarDate } from '@/components/ui/format-port';
import {
  NativeSelect,
  SearchableSelect,
  Select,
  type OptionsStatus,
  type SelectOption,
} from '@/components/ui/select';
import { installMatchMedia, WithFormat } from '../support/format-double';

beforeEach(() => installMatchMedia(false));
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

const accounts: SelectOption[] = [
  { value: 'a1', label: 'Terminal 1', group: 'Terminais' },
  { value: 'a2', label: 'Terminal 2', group: 'Terminais', description: 'Lotérica Centro' },
  { value: 'b1', label: 'Banco do Brasil', group: 'Bancos' },
  { value: 'b2', label: 'Caixa Econômica', group: 'Bancos', inactive: true },
];

function Picker(props: {
  status?: OptionsStatus;
  options?: SelectOption[];
  missingLabel?: string;
  initial?: string | null;
}) {
  const [value, setValue] = useState<string | null>(props.initial ?? null);
  return (
    <FormField label="Conta">
      <Select
        options={props.options ?? accounts}
        value={value}
        onValueChange={setValue}
        status={props.status}
        missingLabel={props.missingLabel}
        emptyMessage="Nenhuma conta disponível para você"
      />
    </FormField>
  );
}

describe('Select (select-only combobox)', () => {
  it('no default reads “Escolha…”; arrows, Enter and Escape follow the combobox pattern', () => {
    render(<Picker />);
    const trigger = screen.getByRole('combobox', { name: 'Conta' });
    expect(trigger.textContent).toContain('Escolha…');
    trigger.focus();
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    const listbox = screen.getByRole('listbox');
    expect(trigger.getAttribute('aria-controls')).toBe(listbox.id);
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    const active = document.getElementById(trigger.getAttribute('aria-activedescendant') ?? '');
    expect(active?.textContent).toContain('Terminal 2');
    fireEvent.keyDown(trigger, { key: 'Enter' });
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(trigger.textContent).toContain('Terminal 2');
    expect(document.activeElement).toBe(trigger);
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    fireEvent.keyDown(trigger, { key: 'End' });
    fireEvent.keyDown(trigger, { key: 'Escape' });
    expect(trigger.textContent).toContain('Terminal 2');
  });

  it('groups, descriptions, the selected mark and an explicit inactive marker', () => {
    render(<Picker initial="b1" />);
    fireEvent.click(screen.getByRole('combobox'));
    expect(screen.getByText('Bancos')).toBeTruthy();
    expect(screen.getByText('Lotérica Centro')).toBeTruthy();
    expect(screen.getByText('Caixa Econômica (inativo)')).toBeTruthy();
    expect(
      screen.getByRole('option', { name: /Banco do Brasil/ }).getAttribute('aria-selected'),
    ).toBe('true');
  });

  it('type-ahead moves to the matching option, ignoring accents', () => {
    render(<Picker />);
    const trigger = screen.getByRole('combobox');
    fireEvent.keyDown(trigger, { key: 'c' });
    expect(
      document.getElementById(trigger.getAttribute('aria-activedescendant') ?? '')?.textContent,
    ).toContain('Caixa');
  });

  it('a carried-over value missing from the options stays visible and marked', () => {
    render(<Picker initial="gone" missingLabel="Terminal 9" />);
    expect(screen.getByRole('combobox').textContent).toContain(
      'Terminal 9 (fora das opções atuais)',
    );
  });

  it('loading, failed and empty options are states, never an empty field', () => {
    const retry = vi.fn();
    const { rerender } = render(<Picker status={{ kind: 'loading' }} />);
    expect(screen.getByRole('combobox').textContent).toContain('Carregando…');
    expect(screen.getByRole('combobox').getAttribute('aria-disabled')).toBe('true');
    rerender(<Picker status={{ kind: 'failed', retry }} />);
    expect(screen.getByRole('combobox').textContent).toContain(
      'Não foi possível carregar as opções',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(retry).toHaveBeenCalled();
    rerender(<Picker options={[]} />);
    expect(screen.getByText('Nenhuma conta disponível para você')).toBeTruthy();
  });

  it('native select for short static lists', () => {
    render(
      <FormField label="Situação">
        <NativeSelect
          options={[
            { value: '', label: 'Todas' },
            { value: 'Draft', label: 'Rascunho' },
          ]}
        />
      </FormField>,
    );
    expect(screen.getByRole('combobox', { name: 'Situação' }).tagName).toBe('SELECT');
  });

  it('searchable: filters the complete list locally, announces the count, Enter chooses', () => {
    vi.useFakeTimers();
    function Clients() {
      const [value, setValue] = useState<string | null>(null);
      return (
        <FormField label="Cliente">
          <SearchableSelect
            options={accounts}
            value={value}
            onValueChange={setValue}
            resultsLabel={(count) => `${count} contas`}
          />
        </FormField>
      );
    }
    render(<Clients />);
    const input = screen.getByRole('combobox', { name: 'Cliente' });
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: 'econo' } });
    expect(screen.getAllByRole('option')).toHaveLength(1);
    act(() => vi.advanceTimersByTime(600));
    expect(screen.getByRole('status').textContent).toBe('1 contas');
    fireEvent.keyDown(input, { key: 'Enter' });
    expect((input as HTMLInputElement).value).toBe('Caixa Econômica (inativo)');
  });
});

describe('searchable select on compact layouts', () => {
  it('opens a bottom sheet with the search pinned on top; choosing closes it and returns focus', () => {
    installMatchMedia(true);
    function Clients() {
      const [value, setValue] = useState<string | null>(null);
      return (
        <FormField label="Cliente">
          <SearchableSelect
            options={accounts}
            value={value}
            onValueChange={setValue}
            resultsLabel={(count) => `${count} contas`}
            sheetTitle="Cliente"
          />
        </FormField>
      );
    }
    render(<Clients />);
    const trigger = screen.getByRole('button', { name: 'Cliente' });
    expect(trigger.getAttribute('aria-haspopup')).toBe('dialog');
    fireEvent.click(trigger);
    const search = screen.getByRole('combobox', { name: 'Cliente', hidden: true });
    expect(document.activeElement).toBe(search);
    fireEvent.change(search, { target: { value: 'banco' } });
    fireEvent.keyDown(search, { key: 'Enter' });
    expect(screen.queryByRole('dialog', { hidden: true })).toBeNull();
    expect(trigger.textContent).toContain('Banco do Brasil');
    expect(document.activeElement).toBe(trigger);
  });
});

describe('calendar arithmetic (no Date, no device clock)', () => {
  it('weekdays, month grids, leap years and month/year boundaries', () => {
    expect(weekdayOf(calendarDate(2026, 9, 24))).toBe(4); // quinta-feira
    expect(weekdayOf(calendarDate(2024, 2, 29))).toBe(4);
    expect(addDays(calendarDate(2024, 2, 28), 1)).toBe('2024-02-29');
    expect(addDays(calendarDate(2025, 2, 28), 1)).toBe('2025-03-01');
    expect(addDays(calendarDate(2026, 12, 31), 1)).toBe('2027-01-01');
    expect(addDays(calendarDate(2026, 1, 1), -1)).toBe('2025-12-31');
    expect(addDays(calendarDate(2026, 3, 15), -45)).toBe('2026-01-29');
    expect(addMonths(calendarDate(2026, 1, 31), 1)).toBe('2026-02-28');
    expect(addMonths(calendarDate(2026, 1, 15), -13)).toBe('2024-12-15');
    const weeks = monthWeeks(2026, 9);
    expect(weeks[0]?.slice(0, 3)).toEqual([null, null, '2026-09-01']);
    expect(weeks.flat().filter(Boolean)).toHaveLength(30);
    expect(asCalendarDate('2026-02-30')).toBeNull();
    expect([isWallClockTime('09:05'), isWallClockTime('24:00'), isWallClockTime('9:05')]).toEqual([
      true,
      false,
      false,
    ]);
  });

  it('the date sources never construct a JS Date or read the device clock', () => {
    for (const file of ['calendar.ts', 'date-picker.tsx', 'format-port.tsx']) {
      const source = readFileSync(`src/components/ui/${file}`, 'utf8');
      expect(source, file).not.toMatch(
        /new Date|Date\.now|Date\.parse|getTimezoneOffset|toLocaleDateString/,
      );
    }
  });
});

describe('DateField and calendar', () => {
  const today = calendarDate(2026, 9, 24);
  function Field({ max = null }: { max?: CalendarDate | null }) {
    const [value, setValue] = useState<CalendarDate | null>(null);
    const [invalid, setInvalid] = useState(false);
    return (
      <WithFormat>
        <FormField label="Data" error={invalid ? invalidDateMessage : undefined}>
          <DateField
            value={value}
            onValueChange={setValue}
            onInvalidChange={setInvalid}
            today={today}
            max={max}
          />
        </FormField>
        <p data-testid="value">{value ?? '-'}</p>
      </WithFormat>
    );
  }

  it('keeps partial input as typed, reads it on blur through the port, and reports invalid text', () => {
    render(<Field />);
    const input = screen.getByLabelText('Data') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '24/09' } });
    fireEvent.blur(input);
    expect(input.value).toBe('24/09');
    expect(screen.getByText(invalidDateMessage)).toBeTruthy();
    fireEvent.change(input, { target: { value: '24/09/2026' } });
    fireEvent.blur(input);
    expect(screen.getByTestId('value').textContent).toBe('2026-09-24');
    expect(input.value).toBe('d:2026-09-24');
    expect(screen.queryByText(invalidDateMessage)).toBeNull();
  });

  it('the calendar grid: today is “hoje”, arrows/Page/Home/End move, Enter chooses, Escape returns focus', () => {
    render(<Field max={today} />);
    const opener = screen.getByRole('button', { name: 'Abrir calendário' });
    fireEvent.click(opener);
    const grid = screen.getByRole('grid');
    const focused = () => document.activeElement as HTMLElement;
    expect(focused().getAttribute('aria-label')).toBe('longo:2026-09-24, hoje');
    expect(focused().getAttribute('aria-current')).toBe('date');
    fireEvent.keyDown(grid, { key: 'ArrowLeft' });
    expect(focused().dataset.date).toBe('2026-09-23');
    fireEvent.keyDown(grid, { key: 'ArrowUp' });
    expect(focused().dataset.date).toBe('2026-09-16');
    fireEvent.keyDown(grid, { key: 'Home' });
    expect(focused().dataset.date).toBe('2026-09-13');
    fireEvent.keyDown(grid, { key: 'End' });
    expect(focused().dataset.date).toBe('2026-09-19');
    fireEvent.keyDown(grid, { key: 'PageUp' });
    expect(focused().dataset.date).toBe('2026-08-19');
    expect(screen.getByText('mês:8/2026')).toBeTruthy();
    fireEvent.keyDown(grid, { key: 'Enter' });
    fireEvent.click(focused());
    expect(screen.getByTestId('value').textContent).toBe('2026-08-19');
    expect(document.activeElement).toBe(opener);
    // Days after the bound are hinted unavailable and cannot be chosen.
    fireEvent.click(opener);
    // It reopens on the chosen value's month; the next month holds the bound.
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'PageDown' });
    const later = screen.getByRole('button', { name: 'longo:2026-09-25' });
    expect(later.getAttribute('aria-disabled')).toBe('true');
    fireEvent.click(later);
    expect(screen.getByTestId('value').textContent).toBe('2026-08-19');
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'Escape' });
    expect(screen.queryByRole('grid')).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it('without the branch date or a value the calendar waits (typing still works)', () => {
    function NoClock() {
      const [value, setValue] = useState<CalendarDate | null>(null);
      return (
        <WithFormat>
          <DateField aria-label="Vencimento" value={value} onValueChange={setValue} />
        </WithFormat>
      );
    }
    render(<NoClock />);
    const opener = screen.getByRole('button', { name: 'Abrir calendário' });
    expect(opener.getAttribute('aria-disabled')).toBe('true');
    fireEvent.click(opener);
    expect(screen.queryByRole('grid')).toBeNull();
  });

  it('the day stepper steps by calendar days and explains an unavailable next day', () => {
    function Queue() {
      const [value, setValue] = useState(today);
      return (
        <WithFormat>
          <DayStepper
            label="Dia da fila"
            value={value}
            onValueChange={setValue}
            today={today}
            max={today}
            nextUnavailableReason="Ainda não há dados de amanhã"
          />
          <p data-testid="day">{value}</p>
        </WithFormat>
      );
    }
    render(<Queue />);
    const next = screen.getByRole('button', { name: 'Próximo dia' });
    expect(next.getAttribute('aria-disabled')).toBe('true');
    expect(screen.getByText('Ainda não há dados de amanhã')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Dia anterior' }));
    expect(screen.getByTestId('day').textContent).toBe('2026-09-23');
    fireEvent.click(screen.getByRole('button', { name: 'Próximo dia' }));
    expect(screen.getByTestId('day').textContent).toBe('2026-09-24');
  });
});
