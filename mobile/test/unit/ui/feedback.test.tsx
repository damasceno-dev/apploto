import { act, fireEvent, screen } from '@testing-library/react-native';
import { asCents, formatBrl } from '@loto/core';
import { useState } from 'react';
import { AccessibilityInfo, Keyboard, Platform } from 'react-native';
import {
  Badge,
  Button,
  DataTable,
  EmptyState,
  Notice,
  Pagination,
  SaveStatus,
  Stepper,
  SummaryStrip,
  ToastProvider,
  boundedPage,
  pageRangeText,
  saveStatusText,
  toastDuration,
  useToast,
  NO_BODY_FALLBACK,
  type PageEnvelope,
  type SaveState,
  type ToastClock,
} from '../../../src/ui';
import { dark, FakeAppearance, light, renderThemed, styleOf } from './support';

class ManualClock implements ToastClock {
  time = 0;
  private timers: { at: number; callback: () => void; id: number }[] = [];
  private nextId = 1;
  now = () => this.time;
  setTimeout = (callback: () => void, ms: number) => {
    const id = this.nextId++;
    this.timers.push({ at: this.time + ms, callback, id });
    return id;
  };
  clearTimeout = (handle: unknown) => {
    this.timers = this.timers.filter((timer) => timer.id !== handle);
  };
  advance(ms: number) {
    this.time += ms;
    const due = this.timers.filter((timer) => timer.at <= this.time);
    this.timers = this.timers.filter((timer) => timer.at > this.time);
    due.forEach((timer) => act(() => timer.callback()));
  }
}

function Trigger() {
  const toast = useToast();
  return (
    <>
      <Button label="primeiro" onPress={() => toast.show('Transação salva')} />
      <Button label="segundo" onPress={() => toast.show('Fechamento enviado')} />
    </>
  );
}

describe('Toast', () => {
  test('starts above a keyboard that was already open and keeps it when replaced', async () => {
    const visible = jest.spyOn(Keyboard, 'isVisible').mockReturnValue(true);
    const metrics = jest
      .spyOn(Keyboard, 'metrics')
      .mockReturnValue({ height: 320, width: 375, screenX: 0, screenY: 492 });
    try {
      renderThemed(
        <ToastProvider clock={new ManualClock()}>
          <Trigger />
        </ToastProvider>,
      );
      fireEvent.press(await screen.findByText('primeiro'));
      const first = styleOf(screen.getByTestId('toast')).bottom as number;
      expect(first).toBeGreaterThan(320);
      fireEvent.press(screen.getByText('segundo'));
      expect(screen.getByText('Fechamento enviado')).toBeTruthy();
      expect(styleOf(screen.getByTestId('toast')).bottom).toBe(first);
    } finally {
      visible.mockRestore();
      metrics.mockRestore();
    }
  });

  test('stays at least 5 s, pauses while touched, and the remaining time resumes', async () => {
    const clock = new ManualClock();
    renderThemed(
      <ToastProvider clock={clock}>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'primeiro' }));
    expect(screen.getByTestId('toast')).toBeTruthy();
    clock.advance(4000);
    const surface = screen.getByLabelText('Transação salva');
    fireEvent(surface, 'pressIn');
    clock.advance(60_000);
    expect(screen.queryByTestId('toast')).not.toBeNull();
    fireEvent(surface, 'pressOut');
    clock.advance(999);
    expect(screen.queryByTestId('toast')).not.toBeNull();
    clock.advance(1);
    expect(screen.queryByTestId('toast')).toBeNull();
  });

  test('a replacement starts its own full budget; the old one charges nothing', async () => {
    const clock = new ManualClock();
    renderThemed(
      <ToastProvider clock={clock}>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'primeiro' }));
    clock.advance(4000);
    fireEvent.press(screen.getByRole('button', { name: 'segundo' }));
    expect(screen.queryByText('Transação salva')).toBeNull();
    clock.advance(4999);
    expect(screen.getByText('Fechamento enviado')).toBeTruthy();
    clock.advance(1);
    expect(screen.queryByTestId('toast')).toBeNull();
  });

  test('long text stays longer, Fechar dismisses, and the toast never takes focus', async () => {
    expect(toastDuration('x'.repeat(40))).toBe(5000);
    expect(toastDuration('x'.repeat(90))).toBeGreaterThan(5000);
    const clock = new ManualClock();
    const focus = jest.spyOn(AccessibilityInfo, 'sendAccessibilityEvent');
    renderThemed(
      <ToastProvider clock={clock}>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'primeiro' }));
    expect(focus).not.toHaveBeenCalled();
    fireEvent.press(screen.getByRole('button', { name: 'Fechar' }));
    expect(screen.queryByTestId('toast')).toBeNull();
    focus.mockRestore();
  });

  test('iOS announces the message once; Android relies on the polite live region', async () => {
    const announce = jest
      .spyOn(AccessibilityInfo, 'announceForAccessibility')
      .mockImplementation(() => undefined);
    // The preset's AccessibilityInfo is a shared jest.fn: forget calls from earlier tests.
    announce.mockClear();
    const clock = new ManualClock();
    renderThemed(
      <ToastProvider clock={clock}>
        <Trigger />
      </ToastProvider>,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'primeiro' }));
    const toastAnnouncements = announce.mock.calls.filter(
      ([message]) => message === 'Transação salva',
    );
    expect(toastAnnouncements).toHaveLength(Platform.OS === 'ios' ? 1 : 0);
    expect(screen.getByLabelText('Transação salva').props.accessibilityLiveRegion).toBe('polite');
    announce.mockRestore();
  });
});

describe('Notice and SaveStatus', () => {
  test('server messages render verbatim, in order, duplicates kept; no-body fallback copy', async () => {
    const messages = ['B primeiro', 'A segundo', 'B primeiro'];
    renderThemed(
      <>
        <Notice kind="danger" title="Não foi possível salvar" messages={messages} testID="n" />
        <Notice kind="danger" title="Erro" testID="fallback" />
      </>,
    );
    const rendered = (await screen.findAllByText(/^(A|B) /)).map((node) => node.props.children);
    expect(rendered).toEqual(messages);
    expect(screen.getByText(NO_BODY_FALLBACK)).toBeTruthy();
    expect(screen.getByTestId('n').props.accessibilityLiveRegion).toBe('assertive');
  });

  test('every SaveStatus state has its contract text; Salvar is disabled only when clean or saving', async () => {
    const states: readonly SaveState[] = [
      'saved',
      'never',
      'dirty',
      'saving',
      'unsaved-server',
      'failed',
      'uncertain',
      'stale',
      'device-only',
      'storage-failed',
    ];
    expect(saveStatusText('saved', { time: '14:32' })).toBe('Salvo às 14:32');
    expect(saveStatusText('saved', { time: '18:42', date: '23/09' })).toBe(
      'Salvo em 23/09 às 18:42',
    );
    expect(saveStatusText('saved')).toBe('Salvo');
    function Harness() {
      const [state, setState] = useState<SaveState>('saved');
      return (
        <>
          {states.map((value) => (
            <Button key={value} label={`set-${value}`} onPress={() => setState(value)} />
          ))}
          <SaveStatus state={state} onSave={jest.fn()} onReload={jest.fn()} onRetry={jest.fn()} />
        </>
      );
    }
    renderThemed(<Harness />);
    for (const state of states) {
      fireEvent.press(await screen.findByRole('button', { name: `set-${state}` }));
      // While saving, the button's spoken name is the in-progress verb.
      const save = screen.getByRole('button', {
        name: state === 'saving' ? 'Salvando…' : 'Salvar',
      });
      expect(Boolean(save.props.accessibilityState.disabled)).toBe(
        state === 'saved' || state === 'saving',
      );
      expect(screen.getAllByLabelText(saveStatusText(state)).length).toBeGreaterThan(0);
    }
    fireEvent.press(screen.getByRole('button', { name: 'set-uncertain' }));
    expect(screen.getByRole('button', { name: 'Recarregar e conferir' })).toBeTruthy();
  });
});

describe('Badge', () => {
  test('known values use their domain token in each theme; unknown values are raw, neutral and reported', async () => {
    const onUnknown = jest.fn();
    const view = renderThemed(
      <>
        <Badge kind="status" domain="daily-close-status" value="Submitted" />
        <Badge
          kind="status"
          domain="daily-close-status"
          value="Archived"
          onUnknownValue={onUnknown}
          context="Situação"
        />
        <Badge kind="automatic" />
      </>,
    );
    const submitted = await screen.findByText('Enviado');
    expect(styleOf(submitted).color).toBe(light.dom['daily-close-status'].Submitted.fg);
    expect(screen.getByLabelText('Situação: Archived')).toBeTruthy();
    expect(styleOf(screen.getByText('Archived')).color).toBe(light.sem.feedback.neutral.fg);
    expect(onUnknown).toHaveBeenCalledWith('daily-close-status', 'Archived');
    expect(screen.getByText('Automática')).toBeTruthy();
    view.unmount();
    const appearance = new FakeAppearance();
    appearance.os = 'dark';
    renderThemed(<Badge kind="status" domain="daily-close-status" value="Submitted" />, {
      appearance,
    });
    expect(styleOf(await screen.findByText('Enviado')).color).toBe(
      dark.dom['daily-close-status'].Submitted.fg,
    );
  });
});

describe('data display', () => {
  test('pagination ranges, bounds and envelope-driven buttons', async () => {
    const envelope: PageEnvelope = {
      page: 2,
      pageSize: 20,
      totalCount: 137,
      totalPages: 7,
      hasNext: true,
      hasPrevious: true,
    };
    expect(pageRangeText(envelope)).toBe('21–40 de 137');
    expect(pageRangeText({ ...envelope, page: 7 })).toBe('121–137 de 137');
    expect(pageRangeText({ ...envelope, page: 1, totalCount: 12_345 })).toBe('1–20 de 12.345');
    expect(boundedPage(9, 7)).toBe(7);
    expect(boundedPage(3, 0)).toBe(1);
    const onPageChange = jest.fn();
    const view = renderThemed(
      <Pagination
        envelope={{ ...envelope, hasNext: false }}
        onPageChange={onPageChange}
        listName="transações"
      />,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'Próxima página' }));
    expect(onPageChange).not.toHaveBeenCalled();
    fireEvent.press(screen.getByRole('button', { name: 'Página anterior' }));
    expect(onPageChange).toHaveBeenCalledWith(1);
    view.unmount();
    // A page past TotalPages after a write moves to the last valid page.
    const fix = jest.fn();
    renderThemed(
      <Pagination envelope={{ ...envelope, page: 9 }} onPageChange={fix} listName="transações" />,
    );
    await screen.findByText(/Página/);
    expect(fix).toHaveBeenCalledWith(7);
  });

  test('a row press opens the record; nested actions and selection never do; the link names the row', async () => {
    const onOpen = jest.fn();
    const onNested = jest.fn();
    const onToggle = jest.fn();
    const rows = [{ id: '1', value: 943_10 }];
    renderThemed(
      <DataTable
        testID="t"
        caption="Transações"
        rows={rows}
        rowKey={(row) => row.id}
        columns={[
          {
            key: 'value',
            header: 'Valor',
            numeric: true,
            minWidth: 120,
            render: (row) => formatBrl(asCents(row.value)),
          },
        ]}
        link={{ label: 'Abrir transação', accessibleName: (row) => `R$ ${row.value}`, onOpen }}
        actions={() => (
          <Button label="Ver parcelas" variant="link" onPress={onNested} testID="nested" />
        )}
        selection={{ selected: new Set(), label: () => 'Selecionar', onToggle }}
      />,
    );
    fireEvent.press(await screen.findByTestId('nested'));
    fireEvent.press(screen.getByRole('checkbox', { name: 'Selecionar' }));
    // The selection column draws only the box; its label is the accessible name, not drawn text.
    expect(screen.queryByText('Selecionar', { includeHiddenElements: true })).toBeNull();
    expect(onOpen).not.toHaveBeenCalled();
    expect(onNested).toHaveBeenCalledTimes(1);
    expect(onToggle).toHaveBeenCalledWith('1', true);
    fireEvent.press(screen.getByTestId('t-rowpress-1', { includeHiddenElements: true }));
    expect(onOpen).toHaveBeenCalledTimes(1);
    fireEvent.press(screen.getByRole('link', { name: 'Abrir transação R$ 94310' }));
    expect(onOpen).toHaveBeenCalledTimes(2);
    expect(screen.getByText('R$ 943,10').props.style).toEqual(
      expect.arrayContaining([expect.objectContaining({ textAlign: 'right' })]),
    );
    expect(screen.getByLabelText('Tabela Transações, role horizontalmente')).toBeTruthy();
  });

  test('table states: loading is busy, empty is scoped, error keeps a retry with verbatim messages', async () => {
    const onRetry = jest.fn();
    const base = {
      caption: 'Transações',
      rows: [] as { id: string }[],
      rowKey: (row: { id: string }) => row.id,
      columns: [{ key: 'a', header: 'A', minWidth: 80, render: () => 'x' }],
    };
    const view = renderThemed(
      <DataTable
        {...base}
        state={{ kind: 'empty', empty: 'no-matches', statement: 'Nenhuma transação neste filtro.' }}
      />,
    );
    expect(await screen.findByText('Nenhuma transação neste filtro.')).toBeTruthy();
    view.unmount();
    renderThemed(
      <DataTable
        {...base}
        state={{ kind: 'error', title: 'Falhou', messages: ['Mensagem do servidor'], onRetry }}
      />,
    );
    fireEvent.press(await screen.findByRole('button', { name: 'Tentar novamente' }));
    expect(onRetry).toHaveBeenCalled();
    expect(screen.getByText('Mensagem do servidor')).toBeTruthy();
  });

  test('summary figures never fabricate zero; signed values speak sign and word', async () => {
    renderThemed(
      <SummaryStrip
        items={[
          { label: 'Entradas', value: null },
          {
            label: 'Diferença',
            value: '−R$ 12,00 · falta',
            tone: { group: 'cash-variance', value: 'shortage' },
            spoken: 'menos doze reais, falta',
          },
        ]}
      />,
    );
    expect(await screen.findByLabelText('Entradas: —, Não foi possível carregar')).toBeTruthy();
    expect(screen.queryByText('R$ 0,00')).toBeNull();
    expect(screen.getByLabelText('Diferença: menos doze reais, falta')).toBeTruthy();
    expect(styleOf(screen.getByText('−R$ 12,00 · falta')).color).toBe(
      light.dom['cash-variance'].shortage.fg,
    );
  });

  test('stepper speaks confirmed statuses; all-clear empty state is neutral', async () => {
    renderThemed(
      <>
        <Stepper
          steps={[
            { title: 'Dados', status: 'done', detail: 'Operador criado: Ana Lima' },
            { title: 'Acesso', status: 'current' },
          ]}
        />
        <EmptyState kind="all-clear" statement="Tudo certo — nada pendente de aprovação." />
      </>,
    );
    // Compact (375 dp): "Passo n de N" plus the full list behind a disclosure.
    expect(await screen.findByText('Passo 2 de 2 · Acesso')).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Ver todos os passos' }));
    expect(screen.getByLabelText('Passo 1 de 2: Dados, Operador criado: Ana Lima')).toBeTruthy();
    expect(screen.getByLabelText('Passo 2 de 2: Acesso, passo atual').props['aria-current']).toBe(
      'step',
    );
    expect(styleOf(screen.getByText('Tudo certo — nada pendente de aprovação.')).color).toBe(
      light.sem.text.primary,
    );
  });
});
