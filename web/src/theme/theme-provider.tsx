'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import { themeAttribute, themeCookie, parseThemePreference, type ThemePreference } from './theme';

interface ThemeApi {
  readonly preference: ThemePreference;
  readonly setPreference: (preference: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeApi | null>(null);
const channelName = 'loto-theme';

export function useTheme(): ThemeApi {
  const api = useContext(ThemeContext);
  if (api === null) throw new Error('useTheme must be used inside ThemeProvider.');
  return api;
}

/**
 * Holds the device's theme preference. The server already rendered `<html>` with it, so this only
 * reacts to a change: it updates the attribute (CSS re-themes at once, no reload, no cross-fade),
 * persists the cookie and tells the other tabs, which apply the same choice.
 */
export function ThemeProvider({
  initial,
  children,
}: {
  readonly initial: ThemePreference;
  readonly children: ReactNode;
}) {
  const [preference, setState] = useState(initial);

  useEffect(() => {
    document.documentElement.setAttribute(themeAttribute, preference);
  }, [preference]);

  useEffect(() => {
    if (typeof BroadcastChannel === 'undefined') return;
    const channel = new BroadcastChannel(channelName);
    channel.onmessage = (event: MessageEvent<unknown>) =>
      setState(parseThemePreference(event.data));
    return () => channel.close();
  }, []);

  const setPreference = useCallback((next: ThemePreference) => {
    setState(next);
    document.cookie = themeCookie(next);
    if (typeof BroadcastChannel !== 'undefined') {
      const channel = new BroadcastChannel(channelName);
      channel.postMessage(next);
      channel.close();
    }
  }, []);

  const api = useMemo(() => ({ preference, setPreference }), [preference, setPreference]);
  return <ThemeContext.Provider value={api}>{children}</ThemeContext.Provider>;
}
