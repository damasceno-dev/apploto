import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { Text } from 'react-native';
import { ThemeProvider, useTheme } from '../../../src/theme/provider';
import { parseThemePreference, resolveScheme, themePreferenceKey } from '../../../src/theme/theme';
import { SegmentedControl } from '../../../src/ui/navigation';
import {
  dark,
  deferredRead,
  FakeAppearance,
  FakePlatform,
  light,
  MemoryPreferenceStore,
  styleOf,
} from './support';

function Probe() {
  const theme = useTheme();
  return (
    <>
      <Text testID="state">{`${theme.preference}/${theme.scheme}/${theme.reducedMotion ? 'reduced' : 'full'}/${theme.persistFailed ? 'unsaved' : 'saved'}/${theme.motion('dialog-enter').duration}`}</Text>
      <SegmentedControl
        accessibilityLabel="Tema"
        options={[
          { value: 'light', label: 'Claro' },
          { value: 'dark', label: 'Escuro' },
          { value: 'system', label: 'Sistema' },
        ]}
        value={theme.preference}
        onValueChange={(value) => void theme.setPreference(value)}
      />
    </>
  );
}

function mount(
  store = new MemoryPreferenceStore(),
  appearance = new FakeAppearance(),
  platform = new FakePlatform(),
) {
  const result = render(
    <ThemeProvider store={store} appearance={appearance} platform={platform}>
      <Probe />
    </ThemeProvider>,
  );
  return { ...result, store, appearance, platform };
}

test('pure rules: unknown stored values fall back to Sistema; Sistema follows the OS', () => {
  expect(parseThemePreference('dark')).toBe('dark');
  expect(parseThemePreference('DARK')).toBe('system');
  expect(parseThemePreference(null)).toBe('system');
  expect(resolveScheme('system', 'dark')).toBe('dark');
  expect(resolveScheme('system', null)).toBe('light');
  expect(resolveScheme('light', 'dark')).toBe('light');
});

test('nothing paints before the stored preference is read, so the first frame has the right theme', async () => {
  const store = new MemoryPreferenceStore();
  store.values.set(themePreferenceKey, 'dark');
  const gate = deferredRead(store);
  const { appearance } = mount(store);
  expect(screen.queryByTestId('state')).toBeNull();
  expect(screen.queryByTestId('theme-root')).toBeNull();
  await act(async () => gate.release());
  expect(screen.getByTestId('state').props.children).toMatch(/^dark\/dark\//);
  expect(styleOf(screen.getByTestId('theme-root')).backgroundColor).toBe(dark.sem.background);
  // The explicit choice is applied to the OS appearance so native pickers/keyboards match.
  expect(appearance.sets).toEqual(['dark']);
});

test('Sistema follows OS changes live and after resume; an override ignores them', async () => {
  const appearance = new FakeAppearance();
  const { platform } = mount(new MemoryPreferenceStore(), appearance);
  await screen.findByTestId('state');
  expect(screen.getByTestId('state').props.children).toMatch(/^system\/light/);
  act(() => appearance.changeOs('dark'));
  expect(screen.getByTestId('state').props.children).toMatch(/^system\/dark/);
  // A change missed while backgrounded is picked up on resume.
  act(() => appearance.changeOs('light', false));
  act(() => platform.foreground());
  expect(screen.getByTestId('state').props.children).toMatch(/^system\/light/);
  fireEvent.press(screen.getByRole('radio', { name: 'Escuro' }));
  await waitFor(() => expect(screen.getByTestId('state').props.children).toMatch(/^dark\/dark/));
  act(() => appearance.changeOs('light'));
  expect(screen.getByTestId('state').props.children).toMatch(/^dark\/dark/);
  // Back to Sistema releases the override and reads the OS again.
  fireEvent.press(screen.getByRole('radio', { name: 'Sistema' }));
  await waitFor(() => expect(screen.getByTestId('state').props.children).toMatch(/^system\/light/));
  expect(appearance.sets.at(-1)).toBe('unspecified');
});

test('the choice persists under its own key only and survives a restart', async () => {
  const store = new MemoryPreferenceStore();
  store.values.set('loto.credentials', 'opaque');
  const first = mount(store);
  await screen.findByTestId('state');
  fireEvent.press(screen.getByRole('radio', { name: 'Escuro' }));
  await waitFor(() => expect(store.values.get(themePreferenceKey)).toBe('dark'));
  expect(store.values.get('loto.credentials')).toBe('opaque');
  first.unmount();
  mount(store);
  expect((await screen.findByTestId('state')).props.children).toMatch(/^dark\/dark/);
});

test('a failed write keeps the in-memory choice and reports it; a later success clears the flag', async () => {
  const store = new MemoryPreferenceStore();
  mount(store);
  await screen.findByTestId('state');
  store.failWrite = true;
  fireEvent.press(screen.getByRole('radio', { name: 'Escuro' }));
  await waitFor(() =>
    expect(screen.getByTestId('state').props.children).toBe('dark/dark/full/unsaved/180'),
  );
  store.failWrite = false;
  fireEvent.press(screen.getByRole('radio', { name: 'Claro' }));
  await waitFor(() =>
    expect(screen.getByTestId('state').props.children).toBe('light/light/full/saved/180'),
  );
});

test('an unreadable store starts in Sistema instead of blocking the app', async () => {
  const failing = new MemoryPreferenceStore();
  failing.failRead = true;
  const one = mount(failing);
  expect((await screen.findByTestId('state')).props.children).toMatch(/^system\//);
  one.unmount();
});

test('reduced motion swaps every transition to its reduced duration and listeners are released', async () => {
  const appearance = new FakeAppearance();
  const platform = new FakePlatform();
  platform.reduced = true;
  const view = mount(new MemoryPreferenceStore(), appearance, platform);
  await waitFor(() =>
    expect(screen.getByTestId('state').props.children).toBe('system/light/reduced/saved/0'),
  );
  act(() => platform.setReduced(false));
  expect(screen.getByTestId('state').props.children).toBe('system/light/full/saved/180');
  expect(appearance.listenerCount).toBe(1);
  view.unmount();
  expect(appearance.listenerCount).toBe(0);
  expect(platform.motionListeners.size).toBe(0);
  expect(platform.foregroundListeners.size).toBe(0);
});

test('token colors differ per theme for the same semantic name', () => {
  expect(light.sem.text.primary).not.toBe(dark.sem.text.primary);
  expect(Object.keys(light.dom)).toEqual(Object.keys(dark.dom));
});
