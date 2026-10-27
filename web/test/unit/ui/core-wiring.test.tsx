// @vitest-environment jsdom
// Web M3 7.4: the components with the REAL `@loto/core` adapter (`coreFormat`), on values outside
// any fixture table. The other UI suites keep the bracketed double to prove a component renders the
// port's answer; this suite proves the shipped answers.
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { readFileSync } from 'node:fs';
import { useState, type ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import * as api from '@loto/api';
import { domainEnums, domainLabels, StatusBadge } from '@/components/ui/badge';
import { calendarDate } from '@/components/ui/calendar';
import { DateField, invalidDateMessage, MonthField } from '@/components/ui/date-picker';
import { FormField } from '@/components/ui/form-field';
import { FormatProvider, type CalendarDate } from '@/components/ui/format-port';
import { IdentifierInput } from '@/components/ui/input';
import { blankMoney, MoneyInput, type MoneyValue } from '@/components/ui/money-input';
import { Pagination } from '@/components/ui/pagination';
import { coreFormat } from '@/lib/core-format';
import { installMatchMedia } from '../support/format-double';

const NBSP = String.fromCodePoint(0xa0);

beforeEach(() => installMatchMedia(false));
afterEach(cleanup);

function Core({ children }: { readonly children: ReactNode }) {
  return <FormatProvider format={coreFormat}>{children}</FormatProvider>;
}

describe('coreFormat adapter', () => {
  it('implements all 14 port callbacks with core', () => {
    expect(coreFormat.count(1234567)).toBe('1.234.567');
    expect(coreFormat.moneyDigits('98765432101')).toBe('987.654.321,01');
    expect(coreFormat.moneySpoken('100000001')).toBe('um milhão de reais e um centavo');
    expect(coreFormat.moneyFromText(`R$${NBSP}7.777,07`)).toBe('777707');
    expect(coreFormat.moneyFromText('−R$ 5,00')).toBeNull();
    expect(coreFormat.moneyFromText('1.000.000.000.000,00')).toBeNull();
    expect(coreFormat.date('2031-03-07' as CalendarDate)).toBe('07/03/2031');
    expect(coreFormat.dateFromInput('29/02/2028')).toBe('2028-02-29');
    expect(coreFormat.dateFromInput('29/02/2027')).toBeNull();
    expect(coreFormat.dateLong('2031-03-07' as CalendarDate)).toBe(
      'sexta-feira, 7 de março de 2031',
    );
    expect(coreFormat.monthYear(2031, 3)).toBe('março de 2031');
    expect(coreFormat.month(2031, 3)).toBe('03/2031');
    expect(coreFormat.monthName(12)).toBe('dezembro');
    expect(coreFormat.weekdayShort(6)).toBe('sáb.');
    expect(coreFormat.identifier('cnpj', '11222333000181')).toBe('11.222.333/0001-81');
    expect(coreFormat.identifier('phone', '1132345678')).toBe('(11) 3234-5678');
    expect(coreFormat.identifier('cep', '01310-100')).toBe('01310-100');
    expect(coreFormat.identifier('cpf', '123')).toBe('123');
    expect(coreFormat.identifierInput('cpf', '5299')).toBe('529.9');
    expect(coreFormat.identifierInput('phone', '613')).toBe('(61) 3');
    expect(coreFormat.identifierDigits('cpf', '529.982.247-25')).toBe('52998224725');
    expect(Object.isFrozen(coreFormat)).toBe(true);
  });

  it('is the formatter the app root provides, and no fixture formatter remains', () => {
    expect(readFileSync('src/client/app-providers.tsx', 'utf8')).toContain(
      '<FormatProvider format={coreFormat}>',
    );
    expect(() => readFileSync('src/app/styleguide/fixture-format.ts', 'utf8')).toThrow();
  });
});

describe('MoneyInput with core', () => {
  const type = (input: HTMLElement, inputType: string, data: string | null = null) =>
    act(() => {
      input.dispatchEvent(
        new InputEvent('beforeinput', { inputType, data, cancelable: true, bubbles: true }),
      );
    });
  function Money({
    initial = blankMoney,
    readOnly = false,
  }: {
    initial?: MoneyValue;
    readOnly?: boolean;
  }) {
    const [value, setValue] = useState<MoneyValue>(initial);
    return (
      <Core>
        <FormField label="Valor">
          <MoneyInput value={value} onValueChange={setValue} readOnly={readOnly} />
        </FormField>
        <p data-testid="state">{value.kind === 'blank' ? 'blank' : value.digits}</p>
      </Core>
    );
  }
  const spoken = (input: HTMLElement) =>
    document.getElementById(input.getAttribute('aria-describedby')?.split(' ').at(-1) ?? '')
      ?.textContent;

  it('formats and speaks arbitrary typed amounts, right to left', () => {
    render(<Money />);
    const input = screen.getByLabelText('Valor') as HTMLInputElement;
    for (const digit of '4815162342') type(input, 'insertText', digit);
    expect(input.value).toBe('48.151.623,42');
    expect(screen.getByTestId('state').textContent).toBe('4815162342');
    expect(spoken(input)).toBe(
      'quarenta e oito milhões cento e cinquenta e um mil seiscentos e vinte e três reais e quarenta e dois centavos',
    );
    type(input, 'deleteContentBackward');
    expect(input.value).toBe('4.815.162,34');
  });

  it('keeps blank, a typed zero, and a leading zero apart', () => {
    render(<Money />);
    const input = screen.getByLabelText('Valor') as HTMLInputElement;
    expect(spoken(input)).toBe('não informado');
    type(input, 'insertText', '0');
    expect(input.value).toBe('0,00');
    expect(spoken(input)).toBe('zero reais');
    type(input, 'insertText', '7');
    expect(input.value).toBe('0,07');
    expect(spoken(input)).toBe('sete centavos');
    type(input, 'deleteContentBackward');
    expect(screen.getByTestId('state').textContent).toBe('blank');
    expect(input.value).toBe('');
  });

  it('refuses the 15th digit instead of truncating, and pastes through the core parser', () => {
    render(<Money />);
    const input = screen.getByLabelText('Valor') as HTMLInputElement;
    for (const digit of '99999999999999') type(input, 'insertText', digit);
    expect(input.value).toBe('999.999.999.999,99');
    type(input, 'insertText', '9');
    expect(screen.getByTestId('state').textContent).toBe('99999999999999');
    fireEvent.paste(input, { clipboardData: { getData: () => 'R$ 31.415,92' } });
    expect(input.value).toBe('31.415,92');
    fireEvent.paste(input, { clipboardData: { getData: () => '-10,00' } });
    fireEvent.paste(input, { clipboardData: { getData: () => '1.5' } });
    expect(screen.getByTestId('state').textContent).toBe('3141592');
    // The onChange fallback (no beforeinput) is capped the same way.
    fireEvent.change(input, { target: { value: '1234567890123456' } });
    expect(screen.getByTestId('state').textContent).toBe('3141592');
  });

  it('a read-only amount still shows and speaks the core value', () => {
    render(<Money initial={{ kind: 'amount', digits: '250000' }} readOnly />);
    const input = screen.getByLabelText('Valor') as HTMLInputElement;
    expect(input.readOnly).toBe(true);
    expect(input.value).toBe('2.500,00');
    expect(spoken(input)).toBe('dois mil e quinhentos reais');
  });
});

describe('DateField and MonthField with core', () => {
  function Field({
    today,
    min = null,
    max = null,
  }: {
    today: CalendarDate;
    min?: CalendarDate | null;
    max?: CalendarDate | null;
  }) {
    const [value, setValue] = useState<CalendarDate | null>(null);
    const [invalid, setInvalid] = useState(false);
    return (
      <Core>
        <FormField label="Data" error={invalid ? invalidDateMessage : undefined}>
          <DateField
            value={value}
            onValueChange={setValue}
            onInvalidChange={setInvalid}
            today={today}
            min={min}
            max={max}
          />
        </FormField>
        <p data-testid="value">{value ?? '-'}</p>
      </Core>
    );
  }

  it('reads arbitrary typed dates, rejects impossible ones and keeps the text', () => {
    render(<Field today={calendarDate(2031, 3, 7)} />);
    const input = screen.getByLabelText('Data') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '29/02/2028' } });
    fireEvent.blur(input);
    expect(screen.getByTestId('value').textContent).toBe('2028-02-29');
    expect(input.value).toBe('29/02/2028');
    fireEvent.change(input, { target: { value: '29/02/2027' } });
    fireEvent.blur(input);
    expect(input.value).toBe('29/02/2027');
    expect(screen.getByText(invalidDateMessage)).toBeTruthy();
    expect(screen.getByTestId('value').textContent).toBe('2028-02-29');
    fireEvent.change(input, { target: { value: '31/12/2030' } });
    fireEvent.blur(input);
    expect(screen.getByTestId('value').textContent).toBe('2030-12-31');
  });

  it('names days and months in pt-BR across a year boundary, with the branch-clock today', () => {
    render(<Field today={calendarDate(2030, 12, 31)} max={calendarDate(2030, 12, 31)} />);
    fireEvent.click(screen.getByRole('button', { name: 'Abrir calendário' }));
    expect(screen.getByText('dezembro de 2030')).toBeTruthy();
    expect(screen.getAllByRole('columnheader').map((cell) => cell.textContent)).toEqual([
      'dom.',
      'seg.',
      'ter.',
      'qua.',
      'qui.',
      'sex.',
      'sáb.',
    ]);
    const today = document.activeElement as HTMLElement;
    expect(today.getAttribute('aria-label')).toBe('terça-feira, 31 de dezembro de 2030, hoje');
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'ArrowRight' });
    // Moving past the bound shows next month's grid; the day stays unavailable.
    expect(screen.getByText('janeiro de 2031')).toBeTruthy();
    const next = screen.getByRole('button', { name: 'quarta-feira, 1 de janeiro de 2031' });
    expect(next.getAttribute('aria-disabled')).toBe('true');
    fireEvent.keyDown(screen.getByRole('grid'), { key: 'PageDown' });
    expect(screen.getByText('fevereiro de 2031')).toBeTruthy();
  });

  it('formats the month field', () => {
    function Month() {
      const [value, setValue] = useState<{ year: number; month: number } | null>({
        year: 2031,
        month: 3,
      });
      return (
        <Core>
          <FormField label="Mês">
            <MonthField value={value} onValueChange={setValue} />
          </FormField>
        </Core>
      );
    }
    render(<Month />);
    expect(screen.getByText('03/2031')).toBeTruthy();
  });
});

describe('IdentifierInput and counts with core', () => {
  it('punctuates a CPF while it is typed and leaves an overlong one verbatim', () => {
    function Cpf() {
      const [value, setValue] = useState('');
      return (
        <Core>
          <IdentifierInput aria-label="CPF" kind="cpf" value={value} onValueChange={setValue} />
        </Core>
      );
    }
    render(<Cpf />);
    const input = screen.getByLabelText('CPF') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '5299' } });
    expect(input.value).toBe('529.9');
    fireEvent.change(input, { target: { value: '5299822472' } });
    expect(input.value).toBe('529.982.247-2');
    fireEvent.change(input, { target: { value: '529.982.247-25' } });
    expect(input.value).toBe('529.982.247-25');
    fireEvent.change(input, { target: { value: '529.982.247-251' } });
    expect(input.value).toBe('529.982.247-251');
  });

  it('pagination counts are dot-grouped by core', () => {
    render(
      <Core>
        <Pagination
          envelope={{
            page: 3,
            pageSize: 50,
            totalCount: 12345,
            totalPages: 247,
            hasNext: true,
            hasPrevious: true,
          }}
          onPageChange={() => undefined}
          listName="clientes"
        />
      </Core>,
    );
    expect(screen.getByText('101–150 de 12.345')).toBeTruthy();
  });
});

describe('StatusBadge labels from core', () => {
  it('labels every value of every domain from copy §5 and keeps unknown values raw', () => {
    for (const [domain, values] of Object.entries(domainEnums)) {
      if (domain === 'direction') continue;
      for (const value of Object.values(values)) {
        const { container, unmount } = render(
          <StatusBadge domain={domain as 'aging-bucket'} value={value as 'Current'} />,
        );
        expect(container.textContent).toBe(
          (domainLabels as Record<string, Record<string, string>>)[domain]?.[value],
        );
        expect(container.textContent?.length).toBeGreaterThan(0);
        unmount();
      }
    }
    const { container } = render(<StatusBadge domain="daily-close-status" value="Archived" />);
    expect(container.textContent).toBe('Archived');
    expect(container.querySelector('[data-domain]')).toBeNull();
  });

  it('the label maps key off the same generated enums as the colors', () => {
    const generated = api as unknown as Record<string, Record<string, string>>;
    expect(Object.keys(domainLabels['daily-close-status']).sort()).toEqual(
      Object.values(generated.DailyCloseStatus ?? {}).sort(),
    );
    expect(screen.queryByText('undefined')).toBeNull();
  });
});
