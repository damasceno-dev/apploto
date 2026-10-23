'use client';

import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { CheckIcon } from './icons';

// Tabs (design/specs/components/tabs.md): sibling views of the same scope. Tabs pattern: arrows
// move (and select) between tabs, Home/End to the ends, Tab moves into the panel. Labels never
// truncate: the list scrolls horizontally. The segmented control is a radio group for small
// immediate preferences (theme: Claro / Escuro / Sistema).

export interface TabItem {
  readonly id: string;
  readonly label: ReactNode;
  /** Shown after the label: **Dívidas em aberto (12)** — formatted by the screen. */
  readonly count?: string;
  readonly disabled?: boolean;
}

function rovingKey(
  event: KeyboardEvent<HTMLElement>,
  length: number,
  index: number,
): number | null {
  if (event.key === 'ArrowRight' || event.key === 'ArrowDown') return (index + 1) % length;
  if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') return (index - 1 + length) % length;
  if (event.key === 'Home') return 0;
  if (event.key === 'End') return length - 1;
  return null;
}

export function Tabs({
  label,
  items,
  selected,
  onSelect,
  panel,
  className,
}: {
  readonly label: string;
  readonly items: readonly TabItem[];
  readonly selected: string;
  readonly onSelect: (id: string) => void;
  /** The selected tab's panel (each panel keeps its own loading/empty/error states). */
  readonly panel: ReactNode;
  readonly className?: string;
}) {
  const base = useId();
  const list = useRef<HTMLDivElement>(null);
  const enabled = items.filter((item) => !item.disabled);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const index = enabled.findIndex((item) => item.id === selected);
    const next = rovingKey(event, enabled.length, index);
    if (next === null) return;
    event.preventDefault();
    const target = enabled[next];
    if (target === undefined) return;
    onSelect(target.id);
    list.current
      ?.querySelector<HTMLButtonElement>(`[data-tab="${CSS.escape(target.id)}"]`)
      ?.focus();
  };
  return (
    <div className={cn('flex min-w-0 flex-col gap-stack-md', className)}>
      <div
        ref={list}
        role="tablist"
        aria-label={label}
        onKeyDown={onKeyDown}
        className="flex min-w-0 overflow-x-auto border-b border-border-default"
      >
        {items.map((item) => {
          const active = item.id === selected;
          return (
            <button
              key={item.id}
              type="button"
              role="tab"
              id={`${base}-tab-${item.id}`}
              data-tab={item.id}
              aria-selected={active}
              aria-controls={`${base}-panel`}
              tabIndex={active ? 0 : -1}
              disabled={item.disabled}
              onClick={() => onSelect(item.id)}
              className={cn(
                'relative -mb-px flex min-h-size-touch-target-min shrink-0 items-center gap-inline-xs border-b-(length:--spacing-size-marker-width) px-inset-panel type-label whitespace-nowrap motion-control transition-colors',
                active
                  ? 'border-selection-indicator text-text-primary'
                  : 'border-transparent text-text-secondary hover:bg-surface-hover',
                'disabled:cursor-not-allowed disabled:text-text-disabled',
              )}
            >
              {item.label}
              {item.count ? (
                <>
                  {' '}
                  <span className="tabular-nums">({item.count})</span>
                </>
              ) : null}
            </button>
          );
        })}
      </div>
      <div
        role="tabpanel"
        id={`${base}-panel`}
        aria-labelledby={`${base}-tab-${selected}`}
        tabIndex={0}
        className="min-w-0 outline-none focus-visible:outline-2"
      >
        {panel}
      </div>
    </div>
  );
}

export interface SegmentOption<T extends string> {
  readonly value: T;
  readonly label: string;
  readonly icon?: ReactNode;
}

/**
 * A bordered group of equal choices; the selected one is filled with `sem.surface.selected` and
 * marked by weight and a check, not color alone. A radio group: arrows move and select.
 */
export function SegmentedControl<T extends string>({
  label,
  options,
  value,
  onValueChange,
  iconsOnly = false,
  className,
}: {
  readonly label: string;
  readonly options: readonly SegmentOption<T>[];
  readonly value: T;
  readonly onValueChange: (value: T) => void;
  /** Narrow top bar: icons with names (the names stay the accessible ones). */
  readonly iconsOnly?: boolean;
  readonly className?: string;
}) {
  const group = useRef<HTMLDivElement>(null);
  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const index = options.findIndex((option) => option.value === value);
    const next = rovingKey(event, options.length, index);
    if (next === null) return;
    event.preventDefault();
    const target = options[next];
    if (target === undefined) return;
    onValueChange(target.value);
    group.current?.querySelectorAll<HTMLButtonElement>('[role="radio"]')[next]?.focus();
  };
  return (
    <div
      ref={group}
      role="radiogroup"
      aria-label={label}
      onKeyDown={onKeyDown}
      className={cn(
        'inline-flex rounded-control border border-border-control bg-surface-default',
        className,
      )}
    >
      {options.map((option) => {
        const checked = option.value === value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={checked}
            aria-label={iconsOnly ? option.label : undefined}
            tabIndex={checked ? 0 : -1}
            onClick={() => onValueChange(option.value)}
            className={cn(
              'flex min-h-size-control-height items-center gap-inline-xs px-inset-control-x type-button-small pointer-coarse:min-h-size-touch-target-min',
              'border-s border-border-subtle first:border-s-0 first:rounded-s-control last:rounded-e-control',
              checked
                ? 'bg-surface-selected font-semibold text-text-primary'
                : 'font-regular text-text-secondary hover:bg-surface-hover',
            )}
          >
            {checked && !option.icon ? <CheckIcon /> : option.icon}
            {iconsOnly ? null : <span>{option.label}</span>}
            {checked && option.icon && !iconsOnly ? <CheckIcon /> : null}
          </button>
        );
      })}
    </div>
  );
}
