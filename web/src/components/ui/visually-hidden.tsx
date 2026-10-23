import type { ReactNode } from 'react';

/** Text for assistive technology only (the component contract's "visually hidden context"). */
export function VisuallyHidden({
  children,
  id,
}: {
  readonly children: ReactNode;
  readonly id?: string;
}) {
  return (
    <span id={id} className="sr-only">
      {children}
    </span>
  );
}
