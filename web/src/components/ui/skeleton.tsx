import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';

// Skeleton (design/specs/components/skeleton.md): content-shaped placeholders for a region's first
// load — never zero, never an all-clear. The shimmer uses `motion.transition.skeleton` and becomes a
// static placeholder under reduced motion (the token duration is then 0).

export function Skeleton({ className }: { readonly className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'block h-lh rounded-xs bg-skeleton-base',
        'bg-[linear-gradient(90deg,var(--loto-sem-skeleton-base)_0%,var(--loto-sem-skeleton-highlight)_50%,var(--loto-sem-skeleton-base)_100%)] bg-[length:200%_100%]',
        'animate-[loto-shimmer_var(--loto-motion-skeleton-duration)_linear_infinite]',
        className,
      )}
    />
  );
}

/**
 * The busy region around skeletons: `aria-busy` while loading and **Carregando…** announced once;
 * the shapes are hidden from assistive technology. Content replaces them without stealing focus.
 */
export function LoadingRegion({
  loading,
  label = 'Carregando…',
  skeleton,
  children,
  className,
}: {
  readonly loading: boolean;
  readonly label?: string;
  readonly skeleton: ReactNode;
  readonly children?: ReactNode;
  readonly className?: string;
}) {
  return (
    <div aria-busy={loading || undefined} className={className}>
      <span role="status" className="sr-only">
        {loading ? label : ''}
      </span>
      {loading ? skeleton : children}
    </div>
  );
}
