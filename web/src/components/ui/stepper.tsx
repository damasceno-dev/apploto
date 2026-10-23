'use client';

import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { Disclosure } from './disclosure';
import { useFormat } from './format-port';
import { AlertIcon, CheckIcon, ErrorIcon, LockIcon, Spinner } from './icons';
import { useCompact } from './use-media-query';

// Stepper (design/specs/components/stepper.md): a guided journey's steps and each step's
// **confirmed** status. Done means the server confirmed it — never that a form was filled. A step's
// detail line carries confirmed values, the first verbatim message of a failure, or the verification
// the step allows when its result is not confirmed.

export type StepStatus =
  | 'not-started'
  | 'current'
  | 'saving'
  | 'done'
  | 'failed'
  | 'uncertain'
  | 'skipped'
  | 'unavailable';

export interface Step {
  readonly title: string;
  readonly status: StepStatus;
  /** **Operador criado: Ana Lima**, the first verbatim message, or the unavailable reason. */
  readonly detail?: ReactNode;
  /** The step's own recovery (**Tentar novamente**, the verification action). */
  readonly action?: ReactNode;
}

const statusText: Partial<Record<StepStatus, string>> = {
  saving: 'Salvando…',
  uncertain: 'Resultado não confirmado',
  skipped: 'Opcional — não feito',
};

function StatusMark({ status, index }: { readonly status: StepStatus; readonly index: string }) {
  switch (status) {
    case 'done':
      return <CheckIcon className="text-feedback-success-fg" />;
    case 'failed':
      return <ErrorIcon className="text-feedback-danger-fg" />;
    case 'uncertain':
      return <AlertIcon className="text-feedback-warning-fg" />;
    case 'saving':
      return <Spinner className="text-text-secondary" />;
    case 'unavailable':
      return <LockIcon className="text-text-muted" />;
    default:
      return (
        <span
          className={cn(
            'flex size-6 items-center justify-center rounded-full type-badge',
            status === 'current'
              ? 'bg-action-primary-bg text-action-primary-fg'
              : 'border border-border-strong text-text-secondary',
          )}
        >
          {index}
        </span>
      );
  }
}

function StepList({ steps }: { readonly steps: readonly Step[] }) {
  const format = useFormat();
  return (
    <ol className="flex flex-col gap-stack-sm">
      {steps.map((step, index) => (
        <li
          key={step.title}
          aria-current={step.status === 'current' ? 'step' : undefined}
          className="flex items-start gap-inline-md"
        >
          <span className="flex size-6 shrink-0 items-center justify-center">
            <StatusMark status={step.status} index={format.count(index + 1)} />
          </span>
          <span className="flex min-w-0 flex-col gap-stack-xs">
            <span
              className={cn(
                'type-label',
                step.status === 'current'
                  ? 'text-text-primary'
                  : step.status === 'skipped' || step.status === 'unavailable'
                    ? 'text-text-muted'
                    : 'text-text-secondary',
              )}
            >
              {step.title}
            </span>
            {statusText[step.status] || step.detail ? (
              <span
                role={step.status === 'failed' ? 'alert' : undefined}
                className={cn(
                  'type-helper',
                  step.status === 'failed'
                    ? 'text-feedback-danger-fg'
                    : step.status === 'uncertain'
                      ? 'text-feedback-warning-fg'
                      : 'text-text-muted',
                )}
              >
                {[statusText[step.status], step.detail].filter(Boolean).map((part, partIndex) => (
                  <span key={partIndex} className="block">
                    {part}
                  </span>
                ))}
              </span>
            ) : null}
            {step.action}
          </span>
        </li>
      ))}
    </ol>
  );
}

export function Stepper({
  label,
  steps,
  className,
}: {
  /** The journey's name (**Adicionar funcionário**). */
  readonly label: string;
  readonly steps: readonly Step[];
  readonly className?: string;
}) {
  const format = useFormat();
  const compact = useCompact();
  const currentIndex = Math.max(
    0,
    steps.findIndex((step) => step.status === 'current' || step.status === 'saving'),
  );
  const current = steps[currentIndex];
  const announcement = current ? `${current.title}: ${statusText[current.status] ?? ''}` : '';
  return (
    <nav aria-label={label} className={cn('min-w-0', className)}>
      <span role="status" aria-live="polite" className="sr-only">
        {announcement}
      </span>
      {compact ? (
        <Disclosure
          label={`Passo ${format.count(currentIndex + 1)} de ${format.count(steps.length)} · ${current?.title ?? ''}`}
        >
          <StepList steps={steps} />
        </Disclosure>
      ) : (
        <StepList steps={steps} />
      )}
    </nav>
  );
}
