'use client';

import Link from 'next/link';
import {
  useId,
  type ComponentProps,
  type ComponentPropsWithoutRef,
  type MouseEvent,
  type ReactNode,
} from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/cn';
import { Spinner } from './icons';

// Button (design/specs/components/button.md). Every variant paints all four states from its own
// `sem.action.<variant>.*` tokens; translucent fills sit over the surface below (text/link) or over
// the variant's own default fill (outlined danger), as the contrast gate composited them.
export const buttonVariants = cva(
  [
    'relative inline-flex items-center justify-center gap-inline-sm rounded-control text-center',
    'px-inset-button-x motion-control transition-[background-color,color,border-color]',
    'cursor-pointer select-none no-underline',
  ],
  {
    variants: {
      variant: {
        primary:
          'bg-action-primary-bg text-action-primary-fg hover:bg-action-primary-bg-hover active:bg-action-primary-bg-active',
        cta: 'bg-action-cta-bg text-action-cta-fg hover:bg-action-cta-bg-hover active:bg-action-cta-bg-active',
        secondary:
          'border border-action-secondary-border bg-action-secondary-bg text-action-secondary-fg hover:bg-action-secondary-bg-hover active:bg-action-secondary-bg-active',
        text: 'bg-action-ghost-bg text-action-ghost-fg hover:bg-action-ghost-bg-hover hover:text-action-ghost-fg-hover active:bg-action-ghost-bg-active',
        danger: [
          'border border-action-danger-border bg-action-danger-bg text-action-danger-fg',
          'hover:[background-image:linear-gradient(var(--loto-sem-action-danger-bg-hover),var(--loto-sem-action-danger-bg-hover))]',
          'active:[background-image:linear-gradient(var(--loto-sem-action-danger-bg-active),var(--loto-sem-action-danger-bg-active))]',
        ],
        'danger-solid':
          'bg-action-danger-solid-bg text-action-danger-solid-fg hover:bg-action-danger-solid-bg-hover active:bg-action-danger-solid-bg-active',
        /** Any variant while its action cannot run (applied by `Button`, not chosen by callers). */
        disabled:
          'cursor-not-allowed border border-action-disabled-border bg-action-disabled-bg text-action-disabled-fg',
      },
      size: {
        // Fine pointer: 40; touch screens: 48 (spacing.size.control-height[-touch]).
        default:
          'type-button min-h-size-control-height pointer-coarse:min-h-size-control-height-touch',
        // Web with a fine pointer only; on touch the hit area still reaches the touch minimum.
        small: [
          'type-button-small min-h-size-control-height-small',
          "pointer-coarse:after:absolute pointer-coarse:after:inset-x-0 pointer-coarse:after:top-1/2 pointer-coarse:after:min-h-size-touch-target-min pointer-coarse:after:-translate-y-1/2 pointer-coarse:after:content-['']",
        ],
      },
      iconOnly: { true: 'px-0 aspect-square', false: '' },
    },
    defaultVariants: { variant: 'primary', size: 'default', iconOnly: false },
  },
);

type Variant = Exclude<NonNullable<VariantProps<typeof buttonVariants>['variant']>, 'disabled'>;

interface ButtonOwnProps {
  readonly variant?: Variant;
  readonly size?: 'default' | 'small';
  /** The action exists but cannot run now. The button stays focusable (`aria-disabled`). */
  readonly disabled?: boolean;
  /** Why it is disabled: shown as adjacent text and linked as the description. */
  readonly disabledReason?: ReactNode;
  /** A request is in flight: activation is blocked and `loadingLabel` replaces the label. */
  readonly loading?: boolean;
  /** The in-progress verb (**Salvando…**, **Enviando…**). */
  readonly loadingLabel?: ReactNode;
  readonly icon?: ReactNode;
  /** Icon-only buttons need a name (`aria-label`), e.g. **Tema escuro**. */
  readonly iconOnly?: boolean;
}

export type ButtonProps = ButtonOwnProps &
  Omit<ComponentPropsWithoutRef<'button'>, 'disabled'> & {
    // The JSX namespace's own Ref type (the workspace holds two @types/react copies).
    readonly ref?: React.Ref<HTMLButtonElement>;
  };

/**
 * A native `<button>`. Disabled and loading states suppress pointer and keyboard activation —
 * including a form's implicit submission — while the button stays focusable so its reason can be
 * read. A button never shows success itself.
 */
export function Button({
  variant = 'primary',
  size = 'default',
  disabled = false,
  disabledReason,
  loading = false,
  loadingLabel,
  icon,
  iconOnly = false,
  type = 'button',
  className,
  children,
  onClick,
  ...props
}: ButtonProps) {
  const reasonId = useId();
  const blocked = disabled || loading;
  const describedBy = [props['aria-describedby'], disabled && disabledReason ? reasonId : null]
    .filter(Boolean)
    .join(' ');

  const handleClick = (event: MouseEvent<HTMLButtonElement>) => {
    if (blocked) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }
    onClick?.(event);
  };

  const button = (
    <button
      {...props}
      type={type}
      aria-disabled={disabled || undefined}
      aria-busy={loading || undefined}
      aria-describedby={describedBy || undefined}
      className={cn(
        buttonVariants({ variant: disabled ? 'disabled' : variant, size, iconOnly }),
        loading && 'cursor-progress',
        className,
      )}
      onClick={handleClick}
    >
      {loading ? <Spinner /> : icon}
      {iconOnly ? null : (
        // Both labels share one grid cell, so the width does not jump while loading.
        <span className="inline-grid">
          <span
            aria-hidden={loading && loadingLabel ? true : undefined}
            className={cn('col-start-1 row-start-1', loading && loadingLabel && 'invisible')}
          >
            {children}
          </span>
          {loadingLabel ? (
            <span
              aria-hidden={loading ? undefined : true}
              className={cn('col-start-1 row-start-1', !loading && 'invisible')}
            >
              {loadingLabel}
            </span>
          ) : null}
        </span>
      )}
    </button>
  );

  if (!(disabled && disabledReason)) return button;
  return (
    <span className="inline-flex flex-col items-start gap-stack-xs">
      {button}
      <span id={reasonId} className="type-helper text-text-muted">
        {disabledReason}
      </span>
    </span>
  );
}

type LinkProps = ComponentProps<typeof Link>;

/**
 * A navigation styled as a button or text link: a real link (`next/link`), so opening in a new tab
 * and history behave. Use the `text` variant for record navigation (**Abrir transação**).
 */
export function ButtonLink({
  variant = 'text',
  size = 'default',
  className,
  ...props
}: LinkProps & { readonly variant?: Variant; readonly size?: 'default' | 'small' }) {
  return <Link {...props} className={cn(buttonVariants({ variant, size }), className)} />;
}
