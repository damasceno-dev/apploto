'use client';

import { useEffect, useLayoutEffect, useRef, type ReactNode, type RefObject } from 'react';
import { cn } from '@/lib/cn';

// A floating surface anchored to a trigger (listbox, calendar, menu). It uses the browser top layer
// (`popover="manual"`), so it opens above a modal dialog it was opened from (layers README: popovers
// sit above dialogs). Focus and keyboard stay with the owning component; this only places, shows
// and dismisses on an outside press.

interface PopoverProps {
  readonly open: boolean;
  readonly anchor: RefObject<HTMLElement | null>;
  /** Pointer down outside both the anchor and the popover. */
  readonly onDismiss: () => void;
  readonly id?: string;
  readonly role?: string;
  readonly 'aria-label'?: string;
  readonly 'aria-labelledby'?: string;
  readonly 'aria-modal'?: boolean;
  /** Match the anchor's width (lists) or size to the content (calendars, menus). */
  readonly matchWidth?: boolean;
  readonly className?: string;
  readonly children: ReactNode;
  readonly onKeyDown?: (event: React.KeyboardEvent<HTMLDivElement>) => void;
  readonly ref?: React.Ref<HTMLDivElement>;
}

const gap = 4;
const margin = 16;

/** The top layer is used where the browser has it; elsewhere the surface is a fixed layer. */
function supportsPopover(): boolean {
  return typeof HTMLElement !== 'undefined' && 'showPopover' in HTMLElement.prototype;
}

export function Popover({
  open,
  anchor,
  onDismiss,
  matchWidth = false,
  className,
  children,
  ref,
  ...props
}: PopoverProps) {
  const surface = useRef<HTMLDivElement | null>(null);

  useLayoutEffect(() => {
    const element = surface.current;
    const trigger = anchor.current;
    if (!open || element === null || trigger === null) return;
    if (typeof element.showPopover === 'function' && !element.matches(':popover-open')) {
      element.showPopover();
    }
    const place = () => {
      const rect = trigger.getBoundingClientRect();
      const viewportWidth = document.documentElement.clientWidth;
      const viewportHeight = window.innerHeight;
      element.style.minWidth = matchWidth ? `${rect.width}px` : '';
      element.style.maxWidth = `${viewportWidth - margin * 2}px`;
      const height = element.offsetHeight;
      const below = viewportHeight - rect.bottom - gap - margin;
      const above = rect.top - gap - margin;
      const flip = height > below && above > below;
      const top = flip ? Math.max(margin, rect.top - gap - height) : rect.bottom + gap;
      element.style.maxHeight = `${Math.max(160, flip ? above : below)}px`;
      const left = Math.min(
        Math.max(margin, rect.left),
        Math.max(margin, viewportWidth - margin - element.offsetWidth),
      );
      element.style.top = `${top}px`;
      element.style.left = `${left}px`;
    };
    place();
    window.addEventListener('resize', place);
    window.addEventListener('scroll', place, true);
    return () => {
      window.removeEventListener('resize', place);
      window.removeEventListener('scroll', place, true);
      if (typeof element.hidePopover === 'function' && element.matches(':popover-open')) {
        element.hidePopover();
      }
    };
  }, [open, anchor, matchWidth]);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      const target = event.target as Node;
      if (surface.current?.contains(target) || anchor.current?.contains(target)) return;
      onDismiss();
    };
    document.addEventListener('pointerdown', onPointerDown, true);
    return () => document.removeEventListener('pointerdown', onPointerDown, true);
  }, [open, anchor, onDismiss]);

  if (!open) return null;
  return (
    <div
      {...props}
      ref={(element) => {
        surface.current = element;
        if (typeof ref === 'function') ref(element);
        else if (ref) (ref as { current: HTMLDivElement | null }).current = element;
      }}
      popover={supportsPopover() ? 'manual' : undefined}
      className={cn(
        // Fixed in the top layer; placed by `place()` above. `m-0` cancels the UA centering.
        'fixed m-0 overflow-auto rounded-panel border border-border-default bg-surface-raised p-0 text-text-primary shadow-floating layer-popover',
        className,
      )}
    >
      {children}
    </div>
  );
}

/** Folds case and accents for type-ahead and local search (`São` matches `sao`). */
export function foldText(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLocaleLowerCase('pt-BR');
}
