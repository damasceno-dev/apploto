import { act, fireEvent, screen } from '@testing-library/react-native';
import { useRef, useState } from 'react';
import { AccessibilityInfo, Modal } from 'react-native';
import {
  Button,
  ConfirmDialog,
  FilterBar,
  FormField,
  Menu,
  Overlay,
  Select,
  TextField,
} from '../../../src/ui';
import { renderThemed } from './support';

beforeEach(() => jest.useFakeTimers());
afterEach(() => jest.useRealTimers());

function DialogWithPicker({ onSave }: { onSave: () => void }) {
  const [open, setOpen] = useState(false);
  const [value, setValue] = useState<string | null>(null);
  const trigger = useRef(null);
  return (
    <>
      <Button ref={trigger} label="Novo cliente" onPress={() => setOpen(true)} testID="open" />
      <Overlay
        visible={open}
        onDismiss={() => setOpen(false)}
        title="Novo cliente"
        returnFocus={trigger}
        testID="dialog"
        footer={<Button label="Salvar cliente" onPress={onSave} />}
      >
        <FormField label="Conta">
          <Select
            options={[
              { value: 'a', label: 'Terminal 1' },
              { value: 'b', label: 'Terminal 2' },
            ]}
            value={value}
            onValueChange={setValue}
            testID="pick"
          />
        </FormField>
      </Overlay>
    </>
  );
}

const modals = () => screen.UNSAFE_queryAllByType(Modal).filter((modal) => modal.props.visible);

test('a picker inside a dialog opens as an in-modal layer, never a second native modal', async () => {
  renderThemed(<DialogWithPicker onSave={jest.fn()} />);
  fireEvent.press(await screen.findByTestId('open'));
  expect(modals()).toHaveLength(1);
  fireEvent.press(screen.getByTestId('pick'));
  expect(modals()).toHaveLength(1);
  expect(screen.getByTestId('pick-option-b')).toBeTruthy();
  // Android back closes the topmost layer first, then the dialog.
  act(() => modals()[0]!.props.onRequestClose());
  expect(screen.queryByTestId('pick-option-b')).toBeNull();
  expect(modals()).toHaveLength(1);
  act(() => modals()[0]!.props.onRequestClose());
  expect(modals()).toHaveLength(0);
});

test('choosing inside the nested layer closes only the layer and keeps the dialog', async () => {
  renderThemed(<DialogWithPicker onSave={jest.fn()} />);
  fireEvent.press(await screen.findByTestId('open'));
  fireEvent.press(screen.getByTestId('pick'));
  fireEvent.press(screen.getByTestId('pick-option-b'));
  expect(modals()).toHaveLength(1);
  expect(screen.getByTestId('pick').props.accessibilityLabel).toBe('Conta, Terminal 2');
});

test('close button, backdrop and Android back use one dismissal path; focus returns to the trigger', async () => {
  const focus = jest
    .spyOn(AccessibilityInfo, 'sendAccessibilityEvent')
    .mockImplementation(() => undefined);
  // Focus is moved for screen-reader users only.
  const reader = jest.spyOn(AccessibilityInfo, 'isScreenReaderEnabled').mockResolvedValue(true);
  renderThemed(<DialogWithPicker onSave={jest.fn()} />);
  for (const dismiss of [
    () => fireEvent.press(screen.getByRole('button', { name: 'Fechar' })),
    () => fireEvent.press(screen.getByTestId('dialog-backdrop', { includeHiddenElements: true })),
    () => act(() => modals()[0]!.props.onRequestClose()),
  ]) {
    fireEvent.press(await screen.findByTestId('open'));
    expect(modals()).toHaveLength(1);
    focus.mockClear();
    dismiss();
    expect(modals()).toHaveLength(0);
    await act(async () => {
      jest.advanceTimersByTime(100);
    });
    // Let the screen-reader check resolve.
    await act(async () => {
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(focus).toHaveBeenCalled();
  }
  focus.mockRestore();
  reader.mockRestore();
});

test('a busy confirmation cannot be dismissed by back, backdrop or close; confirm names the object', async () => {
  const onDismiss = jest.fn();
  const onConfirm = jest.fn();
  renderThemed(
    <ConfirmDialog
      visible
      busy
      title="Cancelar esta transação?"
      consequence="A transação sai dos totais do dia."
      confirmLabel="Cancelar transação"
      busyLabel="Cancelando…"
      destructive
      onConfirm={onConfirm}
      onDismiss={onDismiss}
      testID="confirm"
    />,
  );
  expect(await screen.findByText('Cancelando…')).toBeTruthy();
  act(() => modals()[0]!.props.onRequestClose());
  fireEvent.press(screen.getByTestId('confirm-backdrop', { includeHiddenElements: true }));
  fireEvent.press(screen.getByRole('button', { name: 'Fechar' }));
  fireEvent.press(screen.getByRole('button', { name: 'Voltar' }));
  expect(onDismiss).not.toHaveBeenCalled();
  expect(onConfirm).not.toHaveBeenCalled();
  expect(screen.getByText('A transação sai dos totais do dia.')).toBeTruthy();
});

test('the first meaningful field receives focus on open', async () => {
  const focus = jest
    .spyOn(AccessibilityInfo, 'sendAccessibilityEvent')
    .mockImplementation(() => undefined);
  function Form() {
    const field = useRef(null);
    const [value, setValue] = useState('');
    return (
      <Overlay visible onDismiss={() => undefined} title="Motivo" initialFocus={field}>
        <FormField label="Motivo">
          <TextField ref={field} value={value} onChangeText={setValue} testID="reason" />
        </FormField>
      </Overlay>
    );
  }
  renderThemed(<Form />);
  await screen.findByTestId('reason');
  act(() => jest.advanceTimersByTime(100));
  // The target is the field's imperative handle (its own focus), not the dialog title, and no
  // accessibility event is sent to a non-host handle.
  expect(focus).not.toHaveBeenCalled();
  expect(screen.getByTestId('reason')).toBeTruthy();
  focus.mockRestore();
});

test('menu: mode items are radios, unavailable items stay readable with a reason, selection closes it', async () => {
  const onLeave = jest.fn();
  const onPlatform = jest.fn();
  renderThemed(
    <Menu
      testID="menu"
      triggerLabel="Ana"
      title="Conta"
      groups={[
        {
          heading: 'Modo',
          items: [{ key: 'g', label: 'Gestão', kind: 'radio', checked: true, onSelect: jest.fn() }],
        },
        {
          items: [
            {
              key: 'p',
              label: 'Plataforma',
              kind: 'link',
              unavailableReason: 'Disponível apenas para operadores da plataforma',
              onSelect: onPlatform,
            },
            { key: 's', label: 'Sair', onSelect: onLeave },
          ],
        },
      ]}
    />,
  );
  fireEvent.press(await screen.findByTestId('menu'));
  expect(screen.getByRole('radio', { name: 'Gestão' }).props.accessibilityState.checked).toBe(true);
  const platform = screen.getByRole('link', { name: 'Plataforma' });
  expect(platform.props.accessibilityHint).toBe('Disponível apenas para operadores da plataforma');
  fireEvent.press(platform);
  expect(onPlatform).not.toHaveBeenCalled();
  fireEvent.press(screen.getByRole('menuitem', { name: 'Sair' }));
  expect(onLeave).toHaveBeenCalled();
  expect(modals()).toHaveLength(0);
});

test('a filter bar inside a dialog renders its fields inline instead of a second sheet', async () => {
  renderThemed(
    <Overlay visible onDismiss={() => undefined} title="Filtros">
      <FilterBar
        title="Filtros de transações"
        appliedSummary={['padrão — hoje']}
        activeCount={0}
        onApply={jest.fn()}
      >
        <TextField
          value=""
          onChangeText={() => undefined}
          accessibilityLabel="Cliente"
          testID="inline-field"
        />
      </FilterBar>
    </Overlay>,
  );
  expect(await screen.findByTestId('inline-field')).toBeTruthy();
  expect(screen.queryByText(/Filtros \(/)).toBeNull();
  expect(modals()).toHaveLength(1);
});
