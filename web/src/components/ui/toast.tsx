'use client';

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { cn } from '@/lib/cn';
import { Button } from './button';
import { CloseIcon, SuccessIcon } from './icons';

// Toast (design/specs/components/toast.md): a brief echo of an endpoint-confirmed outcome that
// moved the user elsewhere (**Transação salva**). Success only — failures, uncertain results and
// decisions belong in a Notice. Visible at least 5 s (longer for longer text), paused while hovered
// or focused, dismissible, one at a time; announced politely and never takes focus.

interface ToastApi {
  /** Call only after the server confirmed the outcome. */
  readonly showSuccess: (message: string) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const api = useContext(ToastContext);
  if (api === null) throw new Error('useToast must be used inside ToastProvider.');
  return api;
}

/** Reading time: 5 s, plus 60 ms per character beyond 40. */
export function toastDurationMs(message: string): number {
  return 5000 + Math.max(0, message.length - 40) * 60;
}

export function ToastProvider({ children }: { readonly children: ReactNode }) {
  const [toast, setToast] = useState<{ readonly id: number; readonly message: string } | null>(
    null,
  );
  // Pause belongs to one message: hovering or focusing it. A message that leaves (replaced,
  // closed) takes its pause with it, so it can never hold back the next one.
  const [pause, setPause] = useState<{
    readonly id: number;
    readonly hover: boolean;
    readonly focus: boolean;
  } | null>(null);
  // Unread time per message id; only the current message has an entry.
  const budgets = useRef(new Map<number, number>());
  const counter = useRef(0);

  const showSuccess = useCallback((message: string) => {
    counter.current += 1;
    const id = counter.current;
    budgets.current = new Map([[id, toastDurationMs(message)]]);
    setPause(null);
    setToast({ id, message });
  }, []);

  const dismiss = useCallback(() => {
    budgets.current = new Map();
    setPause(null);
    setToast(null);
  }, []);

  const paused = toast !== null && pause?.id === toast.id && (pause.hover || pause.focus);
  const toastId = toast?.id ?? null;
  useEffect(() => {
    if (toastId === null || paused) return;
    const id = toastId;
    const budget = budgets.current.get(id);
    if (budget === undefined) return;
    const started = Date.now();
    const timer = window.setTimeout(
      () => {
        budgets.current.delete(id);
        setToast((current) => (current?.id === id ? null : current));
      },
      Math.max(0, budget),
    );
    return () => {
      window.clearTimeout(timer);
      // Pausing keeps this message's unread time; a message already gone charges nothing.
      if (budgets.current.has(id)) budgets.current.set(id, budget - (Date.now() - started));
    };
  }, [toastId, paused]);

  const setFlag = (flag: 'hover' | 'focus', value: boolean) => {
    if (toast === null) return;
    const id = toast.id;
    setPause((current) => {
      const base = current?.id === id ? current : { id, hover: false, focus: false };
      return { ...base, [flag]: value };
    });
  };

  const api = useMemo(() => ({ showSuccess }), [showSuccess]);
  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        role="status"
        aria-live="polite"
        className="pointer-events-none fixed inset-x-0 bottom-0 flex justify-center p-page-inline-compact layer-toast wide:justify-end wide:p-page-inline-wide"
      >
        {toast ? (
          <div
            key={toast.id}
            onPointerEnter={() => setFlag('hover', true)}
            onPointerLeave={() => setFlag('hover', false)}
            onFocus={() => setFlag('focus', true)}
            onBlur={() => setFlag('focus', false)}
            className={cn(
              'pointer-events-auto flex max-w-[min(100%,var(--container-dialog-max))] items-start gap-inline-md rounded-panel border border-border-default border-s-(length:--spacing-size-marker-width) border-s-feedback-success-border bg-surface-raised p-inset-notice shadow-floating',
              'motion-toast transition-[opacity,translate] starting:translate-y-2 starting:opacity-0',
            )}
          >
            <SuccessIcon large className="text-feedback-success-fg" />
            <p className="flex-1 type-body text-text-primary">{toast.message}</p>
            <Button
              variant="text"
              size="small"
              iconOnly
              aria-label="Fechar"
              icon={<CloseIcon />}
              onClick={dismiss}
            />
          </div>
        ) : null}
      </div>
    </ToastContext.Provider>
  );
}
