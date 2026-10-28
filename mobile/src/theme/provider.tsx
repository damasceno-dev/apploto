import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SystemUI from 'expo-system-ui';
import { vars } from 'nativewind';
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import {
  AccessibilityInfo,
  Appearance,
  AppState,
  StatusBar,
  View,
  useWindowDimensions,
} from 'react-native';
import {
  motionFor,
  parseThemePreference,
  resolveScheme,
  themePreferenceKey,
  type ColorScheme,
  type MotionName,
  type SchemeReading,
  type ThemeColors,
  type ThemePreference,
} from './theme';
import { colorVariables, nativeTokens } from './token-adapter';

/** Device storage for the preference only — never the session/credential store. */
export interface ThemePreferenceStore {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
}

/** OS appearance; `set('unspecified')` hands the decision back to the system. */
export interface AppearancePort {
  get(): SchemeReading;
  set(scheme: 'light' | 'dark' | 'unspecified'): void;
  subscribe(listener: (scheme: SchemeReading) => void): () => void;
}

export interface PlatformPreferencesPort {
  reduceMotion(): Promise<boolean>;
  subscribeReduceMotion(listener: (enabled: boolean) => void): () => void;
  subscribeForeground(listener: () => void): () => void;
}

export const nativeAppearance: AppearancePort = {
  get: () => Appearance.getColorScheme(),
  set: (scheme) => Appearance.setColorScheme(scheme),
  subscribe: (listener) => {
    const subscription = Appearance.addChangeListener(({ colorScheme }) => listener(colorScheme));
    return () => subscription.remove();
  },
};

export const nativePlatformPreferences: PlatformPreferencesPort = {
  reduceMotion: () => AccessibilityInfo.isReduceMotionEnabled(),
  subscribeReduceMotion: (listener) => {
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', listener);
    return () => subscription.remove();
  },
  subscribeForeground: (listener) => {
    const subscription = AppState.addEventListener('change', (state) => {
      if (state === 'active') listener();
    });
    return () => subscription.remove();
  },
};

export interface Theme {
  readonly scheme: ColorScheme;
  readonly preference: ThemePreference;
  /** Persists the choice; the in-memory choice applies even when the device write fails. */
  readonly setPreference: (preference: ThemePreference) => Promise<void>;
  /** The last preference write failed; the choice applies only until the app restarts. */
  readonly persistFailed: boolean;
  readonly colors: ThemeColors;
  readonly reducedMotion: boolean;
  /** OS text scale (1 = 100%); layouts stack instead of shrinking text when it grows. */
  readonly fontScale: number;
  readonly width: number;
  readonly motion: (name: MotionName) => { readonly duration: number };
}

const ThemeContext = createContext<Theme | null>(null);

export function useTheme(): Theme {
  const theme = useContext(ThemeContext);
  if (theme === null) throw new Error('useTheme requires ThemeProvider.');
  return theme;
}

/**
 * Resolves Claro / Escuro / Sistema before anything paints: children render only after the stored
 * preference is read, so the first frame of app UI already uses the right theme. An explicit
 * choice is also applied to the OS appearance so native pickers, keyboards and alerts match.
 */
export function ThemeProvider({
  children,
  store = AsyncStorage,
  appearance = nativeAppearance,
  platform = nativePlatformPreferences,
}: {
  readonly children: ReactNode;
  readonly store?: ThemePreferenceStore;
  readonly appearance?: AppearancePort;
  readonly platform?: PlatformPreferencesPort;
}) {
  const [preference, setPreferenceState] = useState<ThemePreference | null>(null);
  const [system, setSystem] = useState<SchemeReading>(() => appearance.get());
  const [reducedMotion, setReducedMotion] = useState(false);
  const [persistFailed, setPersistFailed] = useState(false);
  const preferenceRef = useRef<ThemePreference | null>(null);
  const writes = useRef(0);
  const { fontScale, width } = useWindowDimensions();

  const apply = useCallback(
    (next: ThemePreference) => {
      preferenceRef.current = next;
      appearance.set(next === 'system' ? 'unspecified' : next);
      // With the override released the OS reading is the system's own again.
      if (next === 'system') setSystem(appearance.get());
      setPreferenceState(next);
    },
    [appearance],
  );

  useEffect(() => {
    let live = true;
    store
      .getItem(themePreferenceKey)
      .catch(() => null)
      .then((raw) => {
        // A choice made while the read was pending wins over the stored one.
        if (live && preferenceRef.current === null) apply(parseThemePreference(raw));
      });
    return () => {
      live = false;
    };
  }, [store, apply]);

  useEffect(() => {
    const unsubscribeAppearance = appearance.subscribe((scheme) => {
      // While an explicit choice is applied the OS reports the override, not the system.
      if (preferenceRef.current === 'system') setSystem(scheme);
    });
    const unsubscribeForeground = platform.subscribeForeground(() => {
      if (preferenceRef.current === 'system') setSystem(appearance.get());
    });
    let live = true;
    platform
      .reduceMotion()
      .then((enabled) => live && setReducedMotion(enabled))
      .catch(() => undefined);
    const unsubscribeMotion = platform.subscribeReduceMotion(setReducedMotion);
    return () => {
      live = false;
      unsubscribeAppearance();
      unsubscribeForeground();
      unsubscribeMotion();
    };
  }, [appearance, platform]);

  const setPreference = useCallback(
    async (next: ThemePreference) => {
      apply(next);
      const write = ++writes.current;
      try {
        await store.setItem(themePreferenceKey, next);
        if (write === writes.current) setPersistFailed(false);
      } catch {
        if (write === writes.current) setPersistFailed(true);
      }
    },
    [apply, store],
  );

  const scheme = resolveScheme(preference ?? 'system', system);
  const theme = useMemo<Theme>(
    () => ({
      scheme,
      preference: preference ?? 'system',
      setPreference,
      persistFailed,
      colors: nativeTokens.themes[scheme],
      reducedMotion,
      fontScale,
      width,
      motion: (name) => motionFor(name, reducedMotion),
    }),
    [scheme, preference, setPreference, persistFailed, reducedMotion, fontScale, width],
  );
  const loaded = preference !== null;
  const background = nativeTokens.themes[scheme].sem.background;

  useEffect(() => {
    if (loaded) SystemUI.setBackgroundColorAsync(background).catch(() => undefined);
  }, [loaded, background]);

  // Nothing of the app paints until the stored preference is known: no wrong-theme first frame.
  if (!loaded) return null;
  return (
    <ThemeContext.Provider value={theme}>
      <StatusBar
        barStyle={scheme === 'dark' ? 'light-content' : 'dark-content'}
        backgroundColor={theme.colors.sem.background}
      />
      <View
        testID="theme-root"
        style={[
          { flex: 1, backgroundColor: theme.colors.sem.background },
          vars(colorVariables[scheme]),
        ]}
      >
        {children}
      </View>
    </ThemeContext.Provider>
  );
}
