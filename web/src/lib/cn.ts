import { clsx, type ClassValue } from 'clsx';
import { extendTailwindMerge } from 'tailwind-merge';
import {
  breakpointTokens,
  layerTokens,
  radiusTokens,
  shadowTokens,
  spacingTokens,
  transitionTokens,
  typographyRoles,
} from '@/theme/tokens.generated';

// tailwind-merge learns the token vocabulary (Web M3): named spacing, radii, shadows and
// breakpoints, plus the role utilities, so a later class overrides an earlier one of its group
// (`cn('type-body', 'type-label')` keeps only the label role).
const twMerge = extendTailwindMerge<'type-role' | 'motion-role' | 'layer-role'>({
  extend: {
    theme: {
      spacing: [...spacingTokens],
      radius: [...radiusTokens],
      shadow: [...shadowTokens],
      breakpoint: [...breakpointTokens],
    },
    classGroups: {
      'type-role': [{ type: [...typographyRoles] }],
      'motion-role': [{ motion: [...transitionTokens] }],
      'layer-role': [{ layer: [...layerTokens] }],
    },
  },
});

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
