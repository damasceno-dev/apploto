'use client';

import { useSyncExternalStore } from 'react';
import { breakpointTokens } from '@/theme/tokens.generated';

/** Live result of a media query; `false` during server rendering. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (notify) => {
      const list = window.matchMedia(query);
      list.addEventListener('change', notify);
      return () => list.removeEventListener('change', notify);
    },
    () => window.matchMedia(query).matches,
    () => false,
  );
}

void breakpointTokens;
/**
 * Below `breakpoint.medium` (40em, so enlarged text switches earlier): the compact arrangement,
 * where pickers and filter panels open as bottom sheets.
 */
export const compactQuery = '(width < 40em)';

export function useCompact(): boolean {
  return useMediaQuery(compactQuery);
}
