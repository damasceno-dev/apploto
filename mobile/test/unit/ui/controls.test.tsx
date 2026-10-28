import { fireEvent, screen } from '@testing-library/react-native';
import { parseCalendarDate, type CalendarDate } from '@loto/core';
import { useState } from 'react';
import { Dimensions, Text } from 'react-native';
import {
  Button,
  Checkbox,
  DateField,
  DayStepper,
  FormField,
  MonthField,
  MoneyInput,
  Radio,
  RadioGroup,
  Select,
  Switch,
  TextField,
  TimeField,
  INVALID_DATE,
} from '../../../src/ui';
import { light, renderThemed, styleOf } from './support';

const date = (text: string) => parseCalendarDate(text) as CalendarDate;

describe('Button', () => {
  test('disabled stays readable with its reason as hint and never fires', async () => {
    const onPress = jest.fn();
    renderThemed(
      <Button
        label="Salvar"
        disabled
        disabledReason="Nenhuma alteração desde o último salvamento"
        onPress={onPress}
      />,
    );
    const button = await screen.findByRole('button', { name: 'Salvar' });
    expect(button.props.accessibilityState).toMatchObject({ disabled: true });
    expect(button.props.accessibilityHint).toBe('Nenhuma alteração desde o último salvamento');
    expect(
      screen.getByText('Nenhuma alteração desde o último salvamento', {
        includeHiddenElements: true,
      }),
    ).toBeTruthy();
    fireEvent.press(button);
    expect(onPress).not.toHaveBeenCalled();
    expect(styleOf(button).backgroundColor).toBe(light.sem.action.disabled.bg);
  });

  test('loading shows the in-progress verb, is busy and blocks a second activation', async () => {
    const onPress = jest.fn();
    renderThemed(<Button label="Enviar" loading loadingLabel="Enviando…" onPress={onPress} />);
    expect(await screen.findByText('Enviando…')).toBeTruthy();
    // The spoken name follows the visible in-progress verb.
    const button = screen.getByRole('button', { name: 'Enviando…' });
    expect(button.props.accessibilityState).toMatchObject({ busy: true });
    fireEvent.press(button);
    expect(onPress).not.toHaveBeenCalled();
  });

  test('each variant paints its own token fill and a link announces as a link', async () => {
    renderThemed(
      <>
        <Button label="cta" variant="cta" />
        <Button label="danger" variant="danger" />
        <Button
          label="Abrir transação"
          variant="link"
          role="link"
          accessibilityLabel="Abrir transação 24/09/2026 R$ 943,10"
        />
      </>,
    );
    expect(styleOf(await screen.findByRole('button', { name: 'cta' })).backgroundColor).toBe(
      light.sem.action.cta.bg,
    );
    expect(styleOf(screen.getByRole('button', { name: 'danger' })).borderColor).toBe(
      light.sem.action.danger.border,
    );
    expect(screen.getByRole('link', { name: 'Abrir transação 24/09/2026 R$ 943,10' })).toBeTruthy();
  });
});

describe('FormField state inheritance', () => {
  test('the field only adds: own disabled/required/invalid survive inside and outside a field', async () => {
    renderThemed(
      <>
        <FormField label="Dentro">
          <TextField
            value=""
            onChangeText={() => undefined}
            disabled
            required
            invalid
            testID="inside"
          />
        </FormField>
        <TextField
          value=""
          onChangeText={() => undefined}
          disabled
          required
          invalid
          accessibilityLabel="Fora"
          testID="outside"
        />
        <FormField
          label="Campo indisponível"
          disabled
          disabledReason="Escolha a filial primeiro"
          error="Obrigatório"
        >
          <TextField value="" onChangeText={() => undefined} testID="from-field" />
        </FormField>
      </>,
    );
    for (const id of ['inside', 'outside']) {
      const input = await screen.findByTestId(id);
      expect(input.props.editable).toBe(false);
      expect(input.props['aria-required']).toBe(true);
      expect(styleOf(input).color).toBe(light.sem.action.disabled.fg);
    }
    const fromField = screen.getByTestId('from-field');
    expect(fromField.props.editable).toBe(false);
    expect(fromField.props.accessibilityLabel).toBe('Campo indisponível');
    expect(fromField.props.accessibilityHint).toContain('Erro: Obrigatório');
    expect(fromField.props.accessibilityHint).toContain('Escolha a filial primeiro');
  });

  test('an explicit invalid=false from the control wins over the field error', async () => {
    renderThemed(
      <FormField label="Valor" error="Mensagem do servidor">
        <TextField value="" onChangeText={() => undefined} invalid={false} testID="control" />
      </FormField>,
    );
    const input = await screen.findByTestId('control');
    expect(input.parent?.parent).toBeTruthy();
    expect(screen.getByText('Mensagem do servidor')).toBeTruthy();
  });

  test('read-only renders selectable text, never an input', async () => {
    renderThemed(
      <FormField label="Data" readOnly readOnlyReason="Período bloqueado">
        <TextField value="24/09/2026" onChangeText={() => undefined} testID="ro" />
      </FormField>,
    );
    expect((await screen.findByText('24/09/2026')).props.selectable).toBe(true);
    expect(screen.getByText('Período bloqueado')).toBeTruthy();
    expect(screen.queryByDisplayValue('24/09/2026')).toBeNull();
  });

  test('password toggle names its action and search clear empties only its field', async () => {
    function Harness() {
      const [password, setPassword] = useState('segredo-teste');
      const [search, setSearch] = useState('ana');
      return (
        <>
          <TextField
            kind="password"
            value={password}
            onChangeText={setPassword}
            accessibilityLabel="Senha"
            testID="pw"
          />
          <TextField
            kind="search"
            value={search}
            onChangeText={setSearch}
            accessibilityLabel="Busca"
            testID="search"
          />
        </>
      );
    }
    renderThemed(<Harness />);
    expect((await screen.findByTestId('pw')).props.secureTextEntry).toBe(true);
    fireEvent.press(screen.getByRole('button', { name: 'Mostrar senha' }));
    expect(screen.getByTestId('pw').props.secureTextEntry).toBe(false);
    expect(screen.getByRole('button', { name: 'Ocultar senha' })).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Limpar busca' }));
    expect(screen.getByTestId('search').props.value).toBe('');
    expect(screen.getByTestId('pw').props.value).toBe('segredo-teste');
  });
});

describe('MoneyInput', () => {
  function Harness({
    initial = '',
    readOnly = false,
    disabled = false,
  }: {
    initial?: string;
    readOnly?: boolean;
    disabled?: boolean;
  }) {
    const [value, setValue] = useState(initial);
    return (
      <FormField label="Contagem de Raspadinha" readOnly={readOnly} disabled={disabled}>
        <MoneyInput value={value} onValueChange={setValue} testID="money" provenance="calculated" />
      </FormField>
    );
  }

  test('a malformed replacement keeps the amount; a well-formed one replaces it', async () => {
    renderThemed(<Harness initial="1234" />);
    for (const replacement of ['1.5', '1,2,3', '1|2']) {
      fireEvent.changeText(await screen.findByTestId('money'), replacement);
      expect(screen.getByTestId('money').props.value).toBe('12,34');
    }
    fireEvent.changeText(screen.getByTestId('money'), '1.234,56');
    expect(screen.getByTestId('money').props.value).toBe('1.234,56');
    fireEvent.changeText(screen.getByTestId('money'), '1.5');
    expect(screen.getByTestId('money').props.value).toBe('1.234,56');
    fireEvent.changeText(screen.getByTestId('money'), '876.543,21');
    fireEvent.changeText(screen.getByTestId('money'), '1.5');
    expect(screen.getByTestId('money').props.value).toBe('876.543,21');
  });

  test('types right to left, speaks money words, and deletion returns to blank (not zero)', async () => {
    renderThemed(<Harness />);
    const input = await screen.findByTestId('money');
    expect(input.props.accessibilityValue).toEqual({ text: 'não informado' });
    fireEvent.changeText(input, '1');
    fireEvent.changeText(screen.getByTestId('money'), '0,012');
    fireEvent.changeText(screen.getByTestId('money'), '0,123');
    fireEvent.changeText(screen.getByTestId('money'), '1,234');
    expect(screen.getByTestId('money').props.value).toBe('12,34');
    expect(screen.getByTestId('money').props.accessibilityValue.text).toBe(
      'doze reais e trinta e quatro centavos',
    );
    for (const shown of ['12,3', '1,2', '0,1', '0,0'])
      fireEvent.changeText(screen.getByTestId('money'), shown);
    expect(screen.getByTestId('money').props.value).toBe('');
    fireEvent.changeText(screen.getByTestId('money'), '0');
    expect(screen.getByTestId('money').props.value).toBe('0,00');
    expect(screen.getByTestId('money').props.accessibilityValue.text).toBe('zero reais');
  });

  test('disabled ignores typing, deletion and paste; read-only is text with provenance', async () => {
    const view = renderThemed(<Harness initial="1234" disabled />);
    const input = await screen.findByTestId('money');
    for (const raw of ['12,345', '12,3', 'R$ 9,99', '']) fireEvent.changeText(input, raw);
    expect(screen.getByTestId('money').props.value).toBe('12,34');
    expect(input.props.editable).toBe(false);
    view.unmount();
    renderThemed(<Harness initial="100000" readOnly />);
    expect(await screen.findByText('R$ 1.000,00')).toBeTruthy();
    expect(screen.getByText('calculado pelo sistema')).toBeTruthy();
    expect(screen.queryByTestId('money')).not.toBeNull();
  });
});

describe('Select', () => {
  const options = [
    { value: 'a', label: 'Árvore' },
    { value: 'b', label: 'Banco', inactive: true },
    { value: 'c', label: 'Caixa' },
  ];

  test('opens a list, chooses, and marks inactive only when the option says so', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <FormField label="Conta">
        <Select options={options} value={null} onValueChange={onValueChange} testID="sel" />
      </FormField>,
    );
    const trigger = await screen.findByTestId('sel');
    expect(trigger.props.accessibilityLabel).toBe('Conta, Escolha…');
    fireEvent.press(trigger);
    expect(screen.getByRole('button', { name: 'Banco, inativo' })).toBeTruthy();
    fireEvent.press(screen.getByTestId('sel-option-c'));
    expect(onValueChange).toHaveBeenCalledWith('c');
  });

  test('disabled (by field or by prop) never opens or emits', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <>
        <FormField label="Por campo" disabled>
          <Select
            options={options}
            value="a"
            onValueChange={onValueChange}
            testID="field-disabled"
          />
        </FormField>
        <Select
          options={options}
          value="a"
          onValueChange={onValueChange}
          disabled
          accessibilityLabel="Por prop"
          testID="prop-disabled"
        />
      </>,
    );
    for (const id of ['field-disabled', 'prop-disabled']) {
      fireEvent.press(await screen.findByTestId(id));
      expect(screen.queryByTestId(`${id}-option-c`)).toBeNull();
    }
    expect(onValueChange).not.toHaveBeenCalled();
  });

  test('searchable filters the complete list locally, ignoring accents and case', async () => {
    renderThemed(
      <Select
        options={options}
        value={null}
        onValueChange={() => undefined}
        searchable
        accessibilityLabel="Cliente"
        testID="s"
      />,
    );
    fireEvent.press(await screen.findByTestId('s'));
    fireEvent.changeText(screen.getByTestId('s-search'), 'ARV');
    expect(screen.getByTestId('s-option-a')).toBeTruthy();
    expect(screen.queryByTestId('s-option-c')).toBeNull();
  });

  test('a carried-over value missing from the options stays visible and marked', async () => {
    renderThemed(
      <Select
        options={options}
        value="z"
        missingLabel="Tarifa antiga"
        onValueChange={() => undefined}
        accessibilityLabel="Tipo"
        testID="m"
      />,
    );
    expect((await screen.findByTestId('m')).props.accessibilityLabel).toBe(
      'Tipo, Tarifa antiga (fora das opções atuais)',
    );
  });

  test('loading and failed states are not actionable and offer retry', async () => {
    const onRetry = jest.fn();
    renderThemed(
      <Select
        options={[]}
        value={null}
        state="failed"
        onRetry={onRetry}
        onValueChange={() => undefined}
        accessibilityLabel="Categoria"
        testID="f"
      />,
    );
    fireEvent.press(await screen.findByTestId('f'));
    expect(screen.queryByText('Nenhuma opção disponível')).toBeNull();
    fireEvent.press(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(onRetry).toHaveBeenCalled();
  });
});

describe('DatePicker family', () => {
  test('invalid text is kept with the contract message; a calendar choice rewrites it', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <DateField
        value={date('2024-02-20')}
        onValueChange={onValueChange}
        accessibilityLabel="Data"
        testID="d"
      />,
    );
    const input = await screen.findByTestId('d');
    fireEvent.changeText(input, '99/99/9999');
    fireEvent(input, 'blur');
    expect(screen.getByText(INVALID_DATE)).toBeTruthy();
    expect(screen.getByTestId('d').props.value).toBe('99/99/9999');
    expect(onValueChange).not.toHaveBeenCalled();
    fireEvent.press(screen.getByTestId('d-calendar'));
    fireEvent.press(screen.getByTestId('day-2024-02-20'));
    expect(screen.getByTestId('d').props.value).toBe('20/02/2024');
    expect(screen.queryByText(INVALID_DATE)).toBeNull();
    expect(onValueChange).toHaveBeenLastCalledWith('2024-02-20');
  });

  test('days after the branch today are unavailable; leap days follow the real calendar', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <DateField
        value={date('2028-02-29')}
        today={date('2028-02-28')}
        max={date('2028-02-28')}
        onValueChange={onValueChange}
        accessibilityLabel="Data"
        testID="d"
      />,
    );
    fireEvent.press(await screen.findByTestId('d-calendar'));
    const later = screen.getByTestId('day-2028-02-29');
    expect(later.props.accessibilityState).toMatchObject({ disabled: true });
    fireEvent.press(later);
    expect(onValueChange).not.toHaveBeenCalled();
    expect(screen.getByTestId('day-2028-02-28').props.accessibilityLabel).toBe(
      'segunda-feira, 28 de fevereiro de 2028, hoje',
    );
    const typed = screen.getByTestId('d');
    fireEvent.changeText(typed, '29/02/2027');
    fireEvent(typed, 'blur');
    expect(screen.getByText(INVALID_DATE)).toBeTruthy();
  });

  test('disabled and read-only never open the calendar nor emit', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <>
        <FormField label="Vencimento" disabled>
          <DateField value={date('2026-09-24')} onValueChange={onValueChange} testID="dis" />
        </FormField>
        <FormField label="Registrada" readOnly>
          <DateField value={date('2026-09-24')} onValueChange={onValueChange} testID="ro" />
        </FormField>
      </>,
    );
    fireEvent.press(await screen.findByTestId('dis-calendar'));
    expect(screen.queryByTestId('day-2026-09-24')).toBeNull();
    expect(screen.queryByTestId('ro-calendar')).toBeNull();
    expect(screen.getAllByText('24/09/2026').length).toBeGreaterThan(0);
    expect(onValueChange).not.toHaveBeenCalled();
  });

  test('calendar navigation stops at 0001-01 and the day stepper at both calendar ends', async () => {
    const onValueChange = jest.fn();
    renderThemed(
      <>
        <DateField
          value={date('0001-01-01')}
          onValueChange={() => undefined}
          accessibilityLabel="Antiga"
          testID="old"
        />
        <DayStepper value={date('9999-12-31')} onValueChange={onValueChange} testID="last" />
      </>,
    );
    fireEvent.press(await screen.findByTestId('old-calendar'));
    const previous = screen.getByRole('button', { name: 'Mês anterior' });
    expect(previous.props.accessibilityState).toMatchObject({ disabled: true });
    expect(() => fireEvent.press(previous)).not.toThrow();
    fireEvent.press(screen.getByRole('button', { name: 'Próximo dia' }));
    expect(onValueChange).not.toHaveBeenCalled();
    fireEvent.press(screen.getByRole('button', { name: 'Dia anterior' }));
    expect(onValueChange).toHaveBeenCalledWith('9999-12-30');
  });

  test('month field waits without a reference instead of guessing a year; time validates HH:mm', async () => {
    const onMonth = jest.fn();
    const onTime = jest.fn();
    renderThemed(
      <>
        <MonthField value={null} onValueChange={onMonth} />
        <TimeField value={null} onValueChange={onTime} accessibilityLabel="Horário" testID="t" />
      </>,
    );
    expect(await screen.findByText('Mês de referência indisponível')).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Próximo mês' }));
    expect(onMonth).not.toHaveBeenCalled();
    fireEvent.changeText(screen.getByTestId('t'), '24:00');
    fireEvent(screen.getByTestId('t'), 'blur');
    expect(onTime).not.toHaveBeenCalled();
    fireEvent.changeText(screen.getByTestId('t'), '09:05');
    fireEvent(screen.getByTestId('t'), 'blur');
    expect(onTime).toHaveBeenCalledWith('09:05');
  });
  test('day stepper and month field obey a disabled or read-only FormField, even by callback', async () => {
    const onDay = jest.fn();
    const onMonth = jest.fn();
    renderThemed(
      <>
        <FormField label="Dia" disabled>
          <DayStepper value={date('2026-09-24')} onValueChange={onDay} testID="day" />
        </FormField>
        <FormField label="Competência" disabled>
          <MonthField value={{ year: 2026, month: 9 }} onValueChange={onMonth} />
        </FormField>
        <FormField label="Dia fechado" readOnly>
          <DayStepper value={date('2026-09-23')} onValueChange={onDay} testID="day-ro" />
        </FormField>
        <FormField label="Competência fechada" readOnly>
          <MonthField value={{ year: 2026, month: 8 }} onValueChange={onMonth} testID="month-ro" />
        </FormField>
      </>,
    );
    await screen.findByTestId('day');
    const buttons = screen.UNSAFE_getAllByType(Button);
    expect(buttons).toHaveLength(6); // only the disabled stepper (2) and month field (4) have buttons
    for (const button of buttons) {
      expect(button.props.disabled).toBe(true);
      button.props.onPress?.();
    }
    expect(onDay).not.toHaveBeenCalled();
    expect(onMonth).not.toHaveBeenCalled();
    expect(screen.getByTestId('day-ro').props.accessibilityLabel).toBe('Dia fechado: 23/09/2026');
    expect(screen.getByTestId('month-ro').props.accessibilityLabel).toBe(
      'Competência fechada: 08/2026',
    );
  });

  test('time field follows an external value change and keeps a partial edit otherwise', async () => {
    const onTime = jest.fn();
    const view = (value: string | null) => (
      <TimeField value={value} onValueChange={onTime} accessibilityLabel="Horário" testID="t" />
    );
    const { rerenderThemed } = renderThemed(view('13:47'));
    const input = await screen.findByTestId('t');
    rerenderThemed(view('08:05'));
    expect(screen.getByTestId('t').props.value).toBe('08:05');
    fireEvent(screen.getByTestId('t'), 'blur');
    expect(onTime).toHaveBeenLastCalledWith('08:05');
    fireEvent.changeText(input, '09:');
    rerenderThemed(view('08:05'));
    expect(screen.getByTestId('t').props.value).toBe('09:');
    rerenderThemed(view(null));
    expect(screen.getByTestId('t').props.value).toBe('');
  });

  test('enlarged text puts the month title on its own line above wrapping month buttons', async () => {
    const calendarAt = async (fontScale: number) => {
      const original = Dimensions.get('window');
      Dimensions.set({ window: { width: 1024, height: 1366, scale: 2, fontScale } });
      try {
        const view = renderThemed(
          <DateField
            value={date('2026-09-24')}
            onValueChange={() => undefined}
            accessibilityLabel="Data"
            testID="d"
          />,
        );
        fireEvent.press(await screen.findByTestId('d-calendar'));
        let row = screen.getByRole('button', { name: 'Próximo mês' }).parent;
        while (row && styleOf(row).flexWrap === undefined) row = row.parent;
        const result = {
          wraps: row ? styleOf(row).flexWrap : undefined,
          titleInRow: (row?.findAllByProps({ testID: 'calendar-month' }).length ?? 0) > 0,
          yearWidth: styleOf(screen.getByLabelText('Ano')).minWidth,
          gridWidth: styleOf(screen.getByTestId('calendar-grid')).width,
          tenth: screen.getByTestId('day-2026-09-10').findByType(Text).props.numberOfLines,
          fullWeekdays: screen.queryByText('dom.') !== null,
        };
        view.unmount();
        return result;
      } finally {
        Dimensions.set({ window: original });
      }
    };
    expect(await calendarAt(1)).toEqual({
      wraps: 'wrap',
      titleInRow: true,
      yearWidth: 72,
      gridWidth: '100%',
      tenth: 1,
      fullWeekdays: true,
    });
    const large = await calendarAt(3.1);
    expect(large).toMatchObject({
      wraps: 'wrap',
      titleInRow: false,
      tenth: 1,
      fullWeekdays: false,
    });
    expect(large.yearWidth).toBeCloseTo(72 * 3.1);
    // Seven cells each wide enough for two digits at the enlarged size; the grid scrolls sideways.
    expect(large.gridWidth).toBeGreaterThan(7 * 2 * 0.6 * 14 * 3.1);
  });
});

describe('choice controls', () => {
  test('indeterminate group selects all; radio has no default; disabled switch never toggles', async () => {
    const onChecked = jest.fn();
    const onRadio = jest.fn();
    const onSwitch = jest.fn();
    renderThemed(
      <>
        <Checkbox label="Todas" checked={false} indeterminate onCheckedChange={onChecked} />
        <RadioGroup
          value={null}
          onValueChange={onRadio}
          accessibilityLabel="Entregou mercadoria contada?"
        >
          <Radio value="yes" label="Sim" />
          <Radio value="no" label="Não" />
        </RadioGroup>
        <Switch
          label="Vibrar"
          value={false}
          onValueChange={onSwitch}
          disabled
          disabledReason="Não suportado"
          testID="sw"
        />
      </>,
    );
    const all = await screen.findByRole('checkbox', { name: 'Todas' });
    expect(all.props.accessibilityState.checked).toBe('mixed');
    fireEvent.press(all);
    expect(onChecked).toHaveBeenCalledWith(true);
    for (const name of ['Sim', 'Não'])
      expect(screen.getByRole('radio', { name }).props.accessibilityState.checked).toBe(false);
    fireEvent.press(screen.getByRole('radio', { name: 'Não' }));
    expect(onRadio).toHaveBeenCalledWith('no');
    fireEvent(screen.getByTestId('sw'), 'valueChange', true);
    expect(onSwitch).not.toHaveBeenCalled();
    expect(screen.getByText('Desativado', { includeHiddenElements: true })).toBeTruthy();
  });
});
