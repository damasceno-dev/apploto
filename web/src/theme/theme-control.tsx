'use client';

import { MonitorIcon, MoonIcon, SunIcon } from '@/components/ui/icons';
import { SegmentedControl } from '@/components/ui/tabs';
import { themeLabels, type ThemePreference } from './theme';
import { useTheme } from './theme-provider';

/** The theme choice (shell blueprint: Claro / Escuro / Sistema, icons with names). */
export function ThemeControl({ iconsOnly = false }: { readonly iconsOnly?: boolean }) {
  const { preference, setPreference } = useTheme();
  return (
    <SegmentedControl<ThemePreference>
      label="Tema"
      iconsOnly={iconsOnly}
      value={preference}
      onValueChange={setPreference}
      options={[
        { value: 'light', label: themeLabels.light, icon: <SunIcon /> },
        { value: 'dark', label: themeLabels.dark, icon: <MoonIcon /> },
        { value: 'system', label: themeLabels.system, icon: <MonitorIcon /> },
      ]}
    />
  );
}
