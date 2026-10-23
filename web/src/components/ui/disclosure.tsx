'use client';

import { useId, useState, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { ChevronRightIcon } from './icons';

// Disclosure (design/specs/components/disclosure.md): secondary content opened on demand (**Ajuda
// sobre diferença de caixa**, **Ver o que mudou**). Toggling never navigates or changes data; the
// label stays the same and the state is the expanded attribute plus the rotating chevron.

export function Disclosure({
  label,
  summary,
  defaultOpen = false,
  open: controlled,
  onOpenChange,
  className,
  children,
}: {
  readonly label: ReactNode;
  /** On the trigger line (**3 produtos alterados**). */
  readonly summary?: ReactNode;
  readonly defaultOpen?: boolean;
  /** Controlled state, when the screen restores it across a return. */
  readonly open?: boolean;
  readonly onOpenChange?: (open: boolean) => void;
  readonly className?: string;
  readonly children: ReactNode;
}) {
  const [uncontrolled, setUncontrolled] = useState(defaultOpen);
  const open = controlled ?? uncontrolled;
  const contentId = useId();
  const toggle = () => {
    setUncontrolled(!open);
    onOpenChange?.(!open);
  };
  return (
    <div className={cn('flex min-w-0 flex-col', className)}>
      <button
        type="button"
        aria-expanded={open}
        aria-controls={contentId}
        onClick={toggle}
        className="flex min-h-size-touch-target-min w-full items-center gap-inline-sm rounded-control text-start type-label text-action-ghost-fg hover:text-action-ghost-fg-hover"
      >
        <ChevronRightIcon
          className={cn('motion-disclosure transition-transform', open && 'rotate-90')}
        />
        <span>{label}</span>
        {summary ? (
          <span className="type-helper font-regular text-text-muted">{summary}</span>
        ) : null}
      </button>
      <div
        id={contentId}
        hidden={!open}
        className="ms-2 border-s border-border-default ps-inset-panel pb-stack-sm"
      >
        {children}
      </div>
    </div>
  );
}
