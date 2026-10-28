import { Platform, type TextStyle, type ViewStyle } from 'react-native';
import type { ColorScheme, TypographyRole } from './theme';
import { nativeTokens } from './token-adapter';

export const spacing = nativeTokens.spacing;
export const radius = nativeTokens.radius;
export const layer = nativeTokens.layer;
export const breakpoint = nativeTokens.breakpoint;

/** Typography role as a native text style. Sizes stay in dp/pt; the OS font scale applies. */
export function textStyle(role: TypographyRole): TextStyle {
  const spec: {
    fontSize: number;
    lineHeight: number;
    fontWeight: string;
    letterSpacing: number;
    textTransform?: string;
    fontVariant?: readonly string[];
    mono?: boolean;
  } = nativeTokens.typography[role];
  return {
    fontSize: spec.fontSize,
    lineHeight: spec.lineHeight,
    fontWeight: spec.fontWeight as TextStyle['fontWeight'],
    letterSpacing: spec.letterSpacing,
    ...(spec.textTransform
      ? { textTransform: spec.textTransform as TextStyle['textTransform'] }
      : {}),
    ...(spec.fontVariant ? { fontVariant: [...spec.fontVariant] as TextStyle['fontVariant'] } : {}),
    ...(spec.mono ? { fontFamily: Platform.select({ ios: 'Menlo', default: 'monospace' }) } : {}),
  };
}

/** Elevation recipe (iOS shadow + Android elevation) of the active theme. */
export function shadowStyle(scheme: ColorScheme, kind: 'raised' | 'floating'): ViewStyle {
  const recipe = nativeTokens.themes[scheme].shadow[kind];
  return { ...recipe, shadowOffset: { ...recipe.shadowOffset } };
}

/** Extra hit area that brings a drawn box up to the minimum touch target. */
export function hitSlopFor(height: number, width = height) {
  const min = spacing.size['touch-target-min'];
  const vertical = Math.max(0, Math.ceil((min - height) / 2));
  const horizontal = Math.max(0, Math.ceil((min - width) / 2));
  return { top: vertical, bottom: vertical, left: horizontal, right: horizontal };
}

/** Compact below `breakpoint.medium`, and whenever enlarged text no longer fits two columns. */
export function isCompact(width: number, fontScale: number) {
  return width / Math.max(1, fontScale) < breakpoint.medium;
}
