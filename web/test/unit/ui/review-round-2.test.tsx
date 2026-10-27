// @vitest-environment jsdom
// Regressions for the independent review of the Shared M3 / Web M3 7.4 batch (2026-10-27), each
// reproducing the reported failure with the real `@loto/core` adapter.
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { useState, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { calendarDate } from '@/components/ui/calendar';
import {
  DateField,
  DayStepper,
  invalidDateMessage,
  MonthField,
  type CalendarMonth,
} from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { FormatProvider, type CalendarDate } from '@/components/ui/format-port';
import { IdentifierInput } from '@/components/ui/input';
import { MoneyInput, type MoneyValue } from '@/components/ui/money-input';
import { coreFormat } from '@/lib/core-format';
import { installMatchMedia } from '../support/format-double';

beforeEach(() => installMatchMedia(false));
afterEach(cleanup);

function Core({ children }: { readonly children: ReactNode }) {
  return <FormatProvider format={coreFormat}>{children}</FormatProvider>;
}

describe('P2-1 — a read-only MoneyInput never changes', () => {
  function Money({
    readOnly = false,
    disabled = false,
  }: {
    readOnly?: boolean;
    disabled?: boolean;
  }) {
    const [value, setValue] = useState<MoneyValue>({ kind: 'amount', digits: '1234' });
    const [changes, setChanges] = useState(0);
    return (
      <Core>
        <FormField label="Abertura">
          <MoneyInput
            value={value}
            readOnly={readOnly}
            disabled={disabled}
            onValueChange={(next) => {
              setChanges((count) => count + 1);
              setValue(next);
            }}
          />
        </FormField>
        <p data-testid="state">{`${value.kind === 'blank' ? 'blank' : value.digits}/${changes}`}</p>
      </Core>
    );
  }
  const beforeInput = (input: HTMLElement, inputType: string, data: string | null = null) =>
    act(() => {
      input.dispatchEvent(
        new InputEvent('beforeinput', { inputType, data, cancelable: true, bubbles: true }),
      );
    });

  for (const lock of ['readOnly', 'disabled'] as const) {
    it(`${lock}: paste, typing, deletion and the change fallback emit nothing`, () => {
      render(<Money {...{ [lock]: true }} />);
      const input = screen.getByLabelText('Abertura') as HTMLInputElement;
      fireEvent.paste(input, { clipboardData: { getData: () => '999,99' } });
      beforeInput(input, 'insertText', '5');
      beforeInput(input, 'deleteContentBackward');
      fireEvent.change(input, { target: { value: '777' } });
      expect(screen.getByTestId('state').textContent).toBe('1234/0');
      expect(input.value).toBe('12,34');
    });
  }

  it('an editable field still accepts the same paste (control)', () => {
    render(<Money />);
    fireEvent.paste(screen.getByLabelText('Abertura'), {
      clipboardData: { getData: () => '999,99' },
    });
    expect(screen.getByTestId('state').textContent).toBe('99999/1');
  });
});

function Dates({
  initial = null,
  readOnly = false,
  today = calendarDate(2024, 2, 20),
}: {
  initial?: CalendarDate | null;
  readOnly?: boolean;
  today?: CalendarDate;
}) {
  const [value, setValue] = useState<CalendarDate | null>(initial);
  const [invalid, setInvalid] = useState(false);
  const [changes, setChanges] = useState(0);
  return (
    <Core>
      <FormField label="Data" error={invalid ? invalidDateMessage : undefined}>
        <DateField
          value={value}
          readOnly={readOnly}
          onValueChange={(next) => {
            setChanges((count) => count + 1);
            setValue(next);
          }}
          onInvalidChange={setInvalid}
          today={today}
        />
      </FormField>
      <p data-testid="value">{`${value ?? '-'}/${changes}`}</p>
    </Core>
  );
}

describe('P2-2 — a read-only DateField covers its calendar', () => {
  it('the calendar does not open and no date can be chosen', () => {
    render(<Dates initial={calendarDate(2024, 2, 20)} readOnly />);
    const opener = screen.getByRole('button', { name: 'Abrir calendário' }) as HTMLButtonElement;
    expect(opener.disabled).toBe(true);
    fireEvent.click(opener);
    expect(screen.queryByRole('grid')).toBeNull();
    const input = screen.getByLabelText('Data') as HTMLInputElement;
    expect(input.readOnly).toBe(true);
    fireEvent.blur(input);
    expect(screen.getByTestId('value').textContent).toBe('2024-02-20/0');
  });
});

describe('P2-3 — choosing the current date again replaces invalid typed text', () => {
  it('select 20/02/2024, type 99/99/9999, select 20/02/2024 again', () => {
    render(<Dates />);
    const input = screen.getByLabelText('Data') as HTMLInputElement;
    const choose = () => {
      fireEvent.click(screen.getByRole('button', { name: 'Abrir calendário' }));
      fireEvent.click(
        screen.getByRole('button', { name: 'terça-feira, 20 de fevereiro de 2024, hoje' }),
      );
    };
    choose();
    expect(input.value).toBe('20/02/2024');
    fireEvent.change(input, { target: { value: '99/99/9999' } });
    fireEvent.blur(input);
    expect(screen.getByText(invalidDateMessage)).toBeTruthy();
    choose();
    expect(input.value).toBe('20/02/2024');
    expect(screen.queryByText(invalidDateMessage)).toBeNull();
    expect(screen.getByTestId('value').textContent?.startsWith('2024-02-20/')).toBe(true);
  });
});

describe('P3-4 — calendar navigation stays within 0001–9999', () => {
  const focusedDate = () => (document.activeElement as HTMLElement).dataset.date;
  // jsdom reports an exception thrown in an event handler as a window error, not a test failure.
  const uncaught: unknown[] = [];
  const record = (event: ErrorEvent) => {
    uncaught.push(event.error);
    event.preventDefault();
  };
  beforeEach(() => {
    uncaught.length = 0;
    window.addEventListener('error', record);
  });
  afterEach(() => {
    window.removeEventListener('error', record);
    expect(uncaught).toEqual([]);
  });

  it('at 01/01/0001 every backward move stays put; forward moves still work', () => {
    render(<Dates initial={calendarDate(1, 1, 1)} today={calendarDate(1, 1, 1)} />);
    fireEvent.click(screen.getByRole('button', { name: 'Abrir calendário' }));
    const grid = screen.getByRole('grid');
    for (const key of ['ArrowLeft', 'ArrowUp', 'PageUp', 'Home']) {
      fireEvent.keyDown(grid, { key });
      expect(focusedDate()).toBe('0001-01-01');
    }
    fireEvent.keyDown(grid, { key: 'PageUp', shiftKey: true });
    fireEvent.click(screen.getByRole('button', { name: 'Mês anterior' }));
    expect(screen.getByText('janeiro de 0001')).toBeTruthy();
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'ArrowRight' });
    expect(focusedDate()).toBe('0001-01-02');
  });

  it('at 31/12/9999 every forward move stays put', () => {
    render(<Dates initial={calendarDate(9999, 12, 31)} today={calendarDate(9999, 12, 31)} />);
    fireEvent.click(screen.getByRole('button', { name: 'Abrir calendário' }));
    const grid = screen.getByRole('grid');
    for (const key of ['ArrowRight', 'ArrowDown', 'PageDown', 'End']) {
      fireEvent.keyDown(grid, { key });
      expect(focusedDate()).toBe('9999-12-31');
    }
    fireEvent.keyDown(grid, { key: 'PageDown', shiftKey: true });
    fireEvent.click(screen.getByRole('button', { name: 'Próximo mês' }));
    expect(screen.getByText('dezembro de 9999')).toBeTruthy();
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'ArrowLeft' });
    expect(focusedDate()).toBe('9999-12-30');
  });

  it('the day stepper and the month field stop at the same limits', () => {
    function Stepper({ initial }: { initial: CalendarDate }) {
      const [value, setValue] = useState(initial);
      return (
        <Core>
          <DayStepper label="Dia" value={value} onValueChange={setValue} />
          <p data-testid="day">{value}</p>
        </Core>
      );
    }
    const { unmount } = render(<Stepper initial={calendarDate(1, 1, 1)} />);
    const previous = screen.getByRole('button', { name: 'Dia anterior' });
    fireEvent.click(previous);
    expect(screen.getByTestId('day').textContent).toBe('0001-01-01');
    fireEvent.click(screen.getByRole('button', { name: 'Próximo dia' }));
    expect(screen.getByTestId('day').textContent).toBe('0001-01-02');
    unmount();
    render(<Stepper initial={calendarDate(9999, 12, 31)} />);
    fireEvent.click(screen.getByRole('button', { name: 'Próximo dia' }));
    expect(screen.getByTestId('day').textContent).toBe('9999-12-31');
    cleanup();

    function Month({ initial }: { initial: CalendarMonth }) {
      const [value, setValue] = useState<CalendarMonth | null>(initial);
      return (
        <Core>
          <FormField label="Mês">
            <MonthField value={value} onValueChange={setValue} />
          </FormField>
        </Core>
      );
    }
    for (const [initial, name] of [
      [{ year: 1, month: 1 }, 'Ano anterior'],
      [{ year: 9999, month: 12 }, 'Próximo ano'],
    ] as const) {
      render(<Month initial={initial} />);
      fireEvent.click(screen.getByLabelText('Mês'));
      const step = screen.getByRole('button', { name }) as HTMLButtonElement;
      expect(step.disabled || step.getAttribute('aria-disabled') === 'true').toBe(true);
      fireEvent.click(step);
      expect(screen.getByText(String(initial.year))).toBeTruthy();
      cleanup();
    }
  });
});

describe('P3-5 — the identifier input mask punctuates while typing', () => {
  function Identifier({ kind }: { kind: 'cpf' | 'phone' }) {
    const [value, setValue] = useState('');
    return (
      <Core>
        <IdentifierInput
          aria-label="Documento"
          kind={kind}
          value={value}
          onValueChange={setValue}
        />
      </Core>
    );
  }
  /** One real edit: set the text and caret as the browser would, then fire its input event. */
  function edit(input: HTMLInputElement, text: string, caret: number, inputType: string) {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set;
    act(() => {
      setter?.call(input, text);
      input.setSelectionRange(caret, caret);
      input.dispatchEvent(new InputEvent('input', { inputType, bubbles: true }));
    });
  }
  /** Types `digit` at the current caret. */
  function typeAt(input: HTMLInputElement, digit: string) {
    const at = input.selectionStart ?? input.value.length;
    edit(input, input.value.slice(0, at) + digit + input.value.slice(at), at + 1, 'insertText');
  }

  it('typing 52998224725 shows each step punctuated, caret at the end', () => {
    render(<Identifier kind="cpf" />);
    const input = screen.getByLabelText('Documento') as HTMLInputElement;
    input.focus();
    const shown: string[] = [];
    for (const digit of '52998224725') {
      typeAt(input, digit);
      shown.push(input.value);
      expect(input.selectionStart).toBe(input.value.length);
    }
    expect(shown).toEqual([
      '5',
      '52',
      '529',
      '529.9',
      '529.98',
      '529.982',
      '529.982.2',
      '529.982.24',
      '529.982.247',
      '529.982.247-2',
      '529.982.247-25',
    ]);
  });

  it('Backspace over a separator deletes the digit before it; Delete deletes the one after', () => {
    render(<Identifier kind="cpf" />);
    const input = screen.getByLabelText('Documento') as HTMLInputElement;
    input.focus();
    for (const digit of '5299822') typeAt(input, digit); // 529.982.2
    // Backspace with the caret right after the second dot removes that dot from the text.
    edit(input, '529.9822', 7, 'deleteContentBackward');
    expect(input.value).toBe('529.982');
    expect(input.selectionStart).toBe(6); // after "529.98"
    // Delete with the caret before the first dot removes the 9 after it.
    edit(input, '529982', 3, 'deleteContentForward');
    expect(input.value).toBe('529.82');
    expect(input.selectionStart).toBe(3);
  });

  it('an inserted digit in the middle keeps the caret after it while punctuation moves', () => {
    render(<Identifier kind="cpf" />);
    const input = screen.getByLabelText('Documento') as HTMLInputElement;
    input.focus();
    for (const digit of '529982') typeAt(input, digit); // 529.982
    input.setSelectionRange(1, 1);
    typeAt(input, '1'); // 5|29.982 → 512.998.2
    expect(input.value).toBe('512.998.2');
    expect(input.selectionStart).toBe(2);
  });

  it('removing an invalid character keeps every digit (529.98a → 529.98)', () => {
    render(<Identifier kind="cpf" />);
    const input = screen.getByLabelText('Documento') as HTMLInputElement;
    input.focus();
    edit(input, '529.98a', 7, 'insertText');
    expect(input.value).toBe('529.98a'); // unsupported text stays verbatim
    edit(input, '529.98', 6, 'deleteContentBackward');
    expect(input.value).toBe('529.98');
    // Delete with the caret before the letter, in the middle of the text.
    edit(input, '529.a98', 4, 'insertText');
    edit(input, '529.98', 4, 'deleteContentForward');
    expect(input.value).toBe('529.98');
  });

  it('editing passthrough text deletes exactly what was deleted', () => {
    function Cnpj() {
      const [value, setValue] = useState('12.ABC.345/01DE-35');
      return (
        <Core>
          <IdentifierInput aria-label="CNPJ" kind="cnpj" value={value} onValueChange={setValue} />
        </Core>
      );
    }
    render(<Cnpj />);
    const cnpj = screen.getByLabelText('CNPJ') as HTMLInputElement;
    cnpj.focus();
    // Backspace over the slash of an unsupported (alphanumeric) CNPJ removes only the slash.
    edit(cnpj, '12.ABC.34501DE-35', 10, 'deleteContentBackward');
    expect(cnpj.value).toBe('12.ABC.34501DE-35');
    cleanup();
    // An overlong CPF is passthrough too: removing its dot keeps all 12 digits.
    render(<Identifier kind="cpf" />);
    const cpf = screen.getByLabelText('Documento') as HTMLInputElement;
    cpf.focus();
    edit(cpf, '529.982.247-251', 15, 'insertText');
    expect(cpf.value).toBe('529.982.247-251');
    edit(cpf, '529982.247-251', 3, 'deleteContentForward');
    expect(cpf.value).toBe('529982.247-251');
  });

  it('a phone moves from the landline to the mobile shape at the 11th digit', () => {
    render(<Identifier kind="phone" />);
    const input = screen.getByLabelText('Documento') as HTMLInputElement;
    input.focus();
    for (const digit of '6131234567') typeAt(input, digit);
    expect(input.value).toBe('(61) 3123-4567');
    typeAt(input, '8');
    expect(input.value).toBe('(61) 31234-5678');
  });
});
