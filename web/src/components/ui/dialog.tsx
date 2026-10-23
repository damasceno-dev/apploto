'use client';

import {
  createContext,
  useContext,
  useEffect,
  useId,
  useLayoutEffect,
  useRef,
  type ReactNode,
  type RefObject,
} from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { CloseIcon } from './icons';
import { useCompact } from './use-media-query';

// Modal / Sheet (design/specs/components/modal-sheet.md) on the native modal `<dialog>`: the browser
// top layer makes the background inert; Escape, the close button and a backdrop press dismiss
// through one path; focus moves in, stays in and returns to the trigger; nothing is dismissible
// while a write is in flight. One level only; pickers opened inside use the popover layer.

export type DialogVariant = 'dialog' | 'drawer' | 'sheet';
export type DismissReason = 'escape' | 'close' | 'backdrop';

export interface DialogProps {
  readonly open: boolean;
  /** Escape, **Fechar** and the backdrop all arrive here (never while `busy`). */
  readonly onDismiss: (reason: DismissReason) => void;
  readonly title: ReactNode;
  /** The consequence or purpose, linked as the dialog's description. */
  readonly description?: ReactNode;
  readonly variant?: DialogVariant;
  /** A write is in flight: no dismissal by any path. */
  readonly busy?: boolean;
  /**
   * Where focus lands: the title (confirmations), the first field (forms, the default), or a ref.
   */
  readonly initialFocus?: 'title' | 'first-field' | RefObject<HTMLElement | null>;
  /** Focus target when the trigger no longer exists on close (the logical heading). */
  readonly returnFocusFallback?: RefObject<HTMLElement | null>;
  readonly footer?: ReactNode;
  readonly closeLabel?: string;
  /** For the navigation drawer: **Menu de navegação**. */
  readonly 'aria-label'?: string;
  readonly className?: string;
  readonly children?: ReactNode;
}

const focusable =
  'input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])';

let openDialogs = 0;

/**
 * True below a dialog. One modal layer only: a picker opened from inside a dialog uses the
 * popover layer (above the dialog) instead of opening its compact bottom sheet as a second modal.
 */
const InDialogContext = createContext(false);

export function useInDialog(): boolean {
  return useContext(InDialogContext);
}

/** A compact layout where a picker may open its own bottom sheet (not below a dialog). */
export function useCompactOutsideDialog(): boolean {
  const compact = useCompact();
  const inDialog = useInDialog();
  return compact && !inDialog;
}

export function Dialog({
  open,
  onDismiss,
  title,
  description,
  variant = 'dialog',
  busy = false,
  initialFocus = 'first-field',
  returnFocusFallback,
  footer,
  closeLabel = 'Fechar',
  className,
  children,
  ...props
}: DialogProps) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const opener = useRef<HTMLElement | null>(null);
  const titleId = useId();
  const descriptionId = useId();
  const latest = useRef({ busy, onDismiss, returnFocusFallback });
  useEffect(() => {
    latest.current = { busy, onDismiss, returnFocusFallback };
  });

  useLayoutEffect(() => {
    const element = dialog.current;
    if (element === null || !open) return;
    opener.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    if (!element.open) {
      if (typeof element.showModal === 'function') element.showModal();
      else element.setAttribute('open', '');
    }
    // Background scroll is locked while any dialog is open.
    openDialogs += 1;
    document.documentElement.style.overflow = 'hidden';
    const target =
      initialFocus === 'title'
        ? titleRef.current
        : initialFocus === 'first-field'
          ? (body.current?.querySelector<HTMLElement>(focusable) ?? titleRef.current)
          : initialFocus.current;
    target?.focus();
    return () => {
      openDialogs -= 1;
      if (openDialogs === 0) document.documentElement.style.overflow = '';
      if (element.open) {
        if (typeof element.close === 'function') element.close();
        else element.removeAttribute('open');
      }
      const back = opener.current?.isConnected
        ? opener.current
        : latest.current.returnFocusFallback?.current;
      back?.focus();
    };
    // `initialFocus` is read once, when the dialog opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    const element = dialog.current;
    if (element === null || !open) return;
    // Escape arrives as `cancel`: the same path as the other dismissals, refused while busy.
    const onCancel = (event: Event) => {
      event.preventDefault();
      if (!latest.current.busy) latest.current.onDismiss('escape');
    };
    element.addEventListener('cancel', onCancel);
    return () => element.removeEventListener('cancel', onCancel);
  }, [open]);

  if (!open) return null;
  return (
    <dialog
      ref={dialog}
      aria-labelledby={props['aria-label'] ? undefined : titleId}
      aria-label={props['aria-label']}
      aria-describedby={description ? descriptionId : undefined}
      aria-busy={busy || undefined}
      // A press on the backdrop lands on the <dialog> itself, outside its content box.
      onPointerDown={(event) => {
        if (event.target === dialog.current && !busy) onDismiss('backdrop');
      }}
      className={cn(
        'm-0 flex max-h-none max-w-none flex-col overflow-hidden border-0 bg-surface-raised p-0 text-text-primary shadow-floating',
        'backdrop:bg-overlay-backdrop-base backdrop:opacity-(--loto-backdrop-opacity)',
        'transition-[opacity,translate] starting:opacity-0',
        variant === 'dialog' &&
          'motion-dialog-enter inset-0 m-auto max-h-[calc(100dvh-2rem)] w-[min(var(--container-dialog-max),calc(100vw-2rem))] rounded-panel starting:translate-y-2',
        variant === 'drawer' &&
          'motion-drawer-enter inset-y-0 start-0 h-dvh max-h-dvh w-[min(var(--container-drawer-max),86vw)] starting:-translate-x-4 bg-nav-bg text-nav-text',
        variant === 'sheet' &&
          'motion-dialog-enter inset-x-0 bottom-0 top-auto mt-auto max-h-[90dvh] w-full rounded-t-panel starting:translate-y-4',
        className,
      )}
    >
      {variant === 'sheet' ? (
        <span aria-hidden="true" className="mx-auto mt-2 h-1 w-10 rounded-full bg-border-strong" />
      ) : null}
      <header
        className={cn(
          'flex items-start justify-between gap-inline-md px-inset-dialog pt-inset-dialog pb-stack-sm',
          variant === 'drawer' && 'px-inset-panel',
        )}
      >
        <h2 ref={titleRef} id={titleId} tabIndex={-1} className="type-section-title outline-none">
          {title}
        </h2>
        <Button
          variant="text"
          iconOnly
          aria-label={closeLabel}
          disabled={busy}
          onClick={() => onDismiss('close')}
          className={cn(
            '-me-2 -mt-1',
            variant === 'drawer' && 'text-nav-text hover:text-nav-text-strong',
          )}
          icon={<CloseIcon />}
        />
      </header>
      <div
        ref={body}
        className={cn(
          'min-h-0 flex-1 overflow-y-auto px-inset-dialog pb-inset-dialog',
          variant === 'drawer' && 'px-inset-panel',
        )}
      >
        {description ? (
          <p id={descriptionId} className="mb-stack-md type-body text-text-secondary">
            {description}
          </p>
        ) : null}
        <InDialogContext.Provider value={true}>{children}</InDialogContext.Provider>
      </div>
      {footer ? (
        <footer className="flex flex-col-reverse gap-stack-sm border-t border-border-default px-inset-dialog py-stack-md medium:flex-row medium:justify-end medium:gap-inline-md [&>*]:max-medium:w-full">
          <InDialogContext.Provider value={true}>{footer}</InDialogContext.Provider>
        </footer>
      ) : null}
    </dialog>
  );
}

/**
 * A confirmation: the title states the decision, the description the consequence; the confirm
 * label names the object (**Cancelar transação**, never "OK"); the dismiss action is **Voltar**.
 * A destructive confirm uses the solid danger fill. Errors render verbatim above the actions.
 */
export function ConfirmDialog({
  open,
  onDismiss,
  onConfirm,
  title,
  consequence,
  confirmLabel,
  busyLabel,
  destructive = false,
  busy = false,
  error,
}: {
  readonly open: boolean;
  readonly onDismiss: (reason: DismissReason | 'back') => void;
  readonly onConfirm: () => void;
  readonly title: ReactNode;
  readonly consequence: ReactNode;
  readonly confirmLabel: ReactNode;
  readonly busyLabel?: ReactNode;
  readonly destructive?: boolean;
  readonly busy?: boolean;
  readonly error?: ReactNode;
}) {
  return (
    <Dialog
      open={open}
      onDismiss={onDismiss}
      title={title}
      description={consequence}
      initialFocus="title"
      busy={busy}
      footer={
        <>
          <Button variant="secondary" disabled={busy} onClick={() => onDismiss('back')}>
            Voltar
          </Button>
          <Button
            variant={destructive ? 'danger-solid' : 'primary'}
            loading={busy}
            loadingLabel={busyLabel}
            onClick={onConfirm}
          >
            {confirmLabel}
          </Button>
        </>
      }
    >
      {error ?? null}
    </Dialog>
  );
}
