import { render, type RenderOptions } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import {
  ThemeProvider,
  type AppearancePort,
  type PlatformPreferencesPort,
  type ThemePreferenceStore,
} from '../../../src/theme/provider';
import type { SchemeReading } from '../../../src/theme/theme';
import { nativeTokens } from '../../../src/theme/token-adapter';

export const light = nativeTokens.themes.light;
export const dark = nativeTokens.themes.dark;

/** Programmable OS appearance: `set` mimics RN (an override reports itself; unspecified → OS). */
export class FakeAppearance implements AppearancePort {
  os: SchemeReading = 'light';
  override: 'light' | 'dark' | null = null;
  sets: string[] = [];
  private listeners = new Set<(scheme: SchemeReading) => void>();
  get = () => this.override ?? this.os;
  set = (scheme: 'light' | 'dark' | 'unspecified') => {
    this.sets.push(scheme);
    this.override = scheme === 'unspecified' ? null : scheme;
  };
  subscribe = (listener: (scheme: SchemeReading) => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  get listenerCount() {
    return this.listeners.size;
  }
  /** The OS switches theme; listeners hear the effective scheme like RN's change event. */
  changeOs(scheme: 'light' | 'dark', notify = true) {
    this.os = scheme;
    if (notify) this.listeners.forEach((listener) => listener(this.get()));
  }
}

export class FakePlatform implements PlatformPreferencesPort {
  reduced = false;
  motionListeners = new Set<(enabled: boolean) => void>();
  foregroundListeners = new Set<() => void>();
  reduceMotion = async () => this.reduced;
  subscribeReduceMotion = (listener: (enabled: boolean) => void) => {
    this.motionListeners.add(listener);
    return () => {
      this.motionListeners.delete(listener);
    };
  };
  subscribeForeground = (listener: () => void) => {
    this.foregroundListeners.add(listener);
    return () => {
      this.foregroundListeners.delete(listener);
    };
  };
  setReduced(enabled: boolean) {
    this.reduced = enabled;
    this.motionListeners.forEach((listener) => listener(enabled));
  }
  foreground() {
    this.foregroundListeners.forEach((listener) => listener());
  }
}

export class MemoryPreferenceStore implements ThemePreferenceStore {
  values = new Map<string, string>();
  failRead = false;
  failWrite = false;
  pendingRead: Promise<void> | null = null;
  async getItem(key: string) {
    if (this.pendingRead) await this.pendingRead;
    if (this.failRead) throw new Error('read failed');
    return this.values.get(key) ?? null;
  }
  async setItem(key: string, value: string) {
    if (this.failWrite) throw new Error('write failed');
    this.values.set(key, value);
  }
}

const metrics = {
  frame: { x: 0, y: 0, width: 375, height: 812 },
  insets: { top: 47, left: 0, right: 0, bottom: 34 },
};

export function renderThemed(
  ui: ReactElement,
  {
    store = new MemoryPreferenceStore(),
    appearance = new FakeAppearance(),
    platform = new FakePlatform(),
    ...options
  }: RenderOptions & {
    store?: MemoryPreferenceStore;
    appearance?: FakeAppearance;
    platform?: FakePlatform;
  } = {},
) {
  const wrap = (node: ReactElement) => (
    <SafeAreaProvider initialMetrics={metrics}>
      <ThemeProvider store={store} appearance={appearance} platform={platform}>
        {node}
      </ThemeProvider>
    </SafeAreaProvider>
  );
  const result = render(wrap(ui), options);
  return {
    ...result,
    store,
    appearance,
    platform,
    rerenderThemed: (next: ReactElement) => result.rerender(wrap(next)),
  };
}

/** Flattened style of a host element. */
export function styleOf(element: { props: { style?: unknown } }): Record<string, unknown> {
  const flat = (style: unknown): Record<string, unknown> =>
    Array.isArray(style)
      ? Object.assign({}, ...style.map(flat))
      : style && typeof style === 'object'
        ? (style as Record<string, unknown>)
        : {};
  return flat(element.props.style);
}

/** Holds the store's next read until `release()` (startup-before-preference scenarios). */
export function deferredRead(store: MemoryPreferenceStore) {
  let release!: () => void;
  store.pendingRead = new Promise<void>((resolve) => {
    release = () => {
      store.pendingRead = null;
      resolve();
    };
  });
  return { release };
}
