'use client';

import { useEffect, useRef, type ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { sessionCopy } from '@/lib/session-copy';
import { AlertIcon, ErrorIcon, InfoIcon, LockIcon, SuccessIcon } from './icons';
import { LineIcon } from './form-field';

// Notice (design/specs/components/notice.md): an inline, persistent message where the problem is.
// `ErrorMessages` render verbatim, in order, one item per string — no prefix, merge, dedupe or
// truncation. Client copy (title, explanation) stays apart from backend text. A blocking failure is
// announced assertively once; the rest politely.

export type NoticeKind = 'danger' | 'warning' | 'info' | 'success' | 'neutral';

const kinds: Record<NoticeKind, { rule: string; icon: string; Icon: typeof InfoIcon }> = {
  danger: {
    rule: 'border-s-feedback-danger-border',
    icon: 'text-feedback-danger-fg',
    Icon: ErrorIcon,
  },
  warning: {
    rule: 'border-s-feedback-warning-border',
    icon: 'text-feedback-warning-fg',
    Icon: AlertIcon,
  },
  info: { rule: 'border-s-feedback-info-border', icon: 'text-feedback-info-fg', Icon: InfoIcon },
  success: {
    rule: 'border-s-feedback-success-border',
    icon: 'text-feedback-success-fg',
    Icon: SuccessIcon,
  },
  neutral: {
    rule: 'border-s-feedback-neutral-border',
    icon: 'text-feedback-neutral-fg',
    Icon: LockIcon,
  },
};

/** Backend `ErrorMessages`, verbatim and in order (duplicates included). */
export function ErrorMessages({ messages }: { readonly messages: readonly string[] }) {
  if (messages.length === 0) return null;
  return (
    <ul className="flex list-disc flex-col gap-stack-xs ps-5 type-body text-text-primary">
      {messages.map((message, index) => (
        // Index keys: the same message may legitimately repeat and must render twice.
        <li key={index}>{message}</li>
      ))}
    </ul>
  );
}

export interface NoticeProps {
  readonly kind: NoticeKind;
  readonly title: ReactNode;
  readonly children?: ReactNode;
  /** Verbatim backend messages, rendered after the client copy. */
  readonly messages?: readonly string[];
  readonly actions?: ReactNode;
  /** Defaults: danger interrupts (`alert`); the rest are polite (`status`). */
  readonly live?: 'assertive' | 'polite' | 'off';
  readonly id?: string;
  readonly className?: string;
  readonly ref?: React.Ref<HTMLDivElement>;
  readonly tabIndex?: number;
}

export function Notice({
  kind,
  title,
  children,
  messages = [],
  actions,
  live = kind === 'danger' ? 'assertive' : 'polite',
  className,
  ...props
}: NoticeProps) {
  const { rule, icon, Icon } = kinds[kind];
  return (
    <div
      {...props}
      role={live === 'assertive' ? 'alert' : live === 'polite' ? 'status' : undefined}
      data-kind={kind}
      className={cn(
        'flex gap-inline-md rounded-panel border border-border-default border-s-(length:--spacing-size-marker-width) bg-surface-default p-inset-notice outline-none',
        rule,
        className,
      )}
    >
      <span className={cn('flex', icon)}>
        <LineIcon>
          <Icon large />
        </LineIcon>
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-stack-sm">
        <p className="type-body-strong text-text-primary">{title}</p>
        {children ? <div className="type-body text-text-secondary">{children}</div> : null}
        <ErrorMessages messages={messages} />
        {actions ? <div className="flex flex-wrap gap-inline-md">{actions}</div> : null}
      </div>
    </div>
  );
}

/**
 * **Resultado não confirmado**: the request was dispatched and no definitive answer arrived. Any
 * backend messages appear verbatim as well; `actions` is only the recovery the catalog allows for
 * that operation (**Verificar e tentar novamente**, **Recarregar e conferir**, **Entrar novamente**).
 */
export function UncertainNotice({
  messages = [],
  actions,
  className,
}: {
  readonly messages?: readonly string[];
  readonly actions?: ReactNode;
  readonly className?: string;
}) {
  return (
    <Notice
      kind="warning"
      live="assertive"
      title={sessionCopy.resultNotConfirmed}
      messages={messages}
      actions={actions}
      className={className}
    >
      {sessionCopy.confirmationWarning}
    </Notice>
  );
}

/** A failure with no usable error body: the standard fallback sentence (copy §6). */
export function NoBodyNotice({
  actions,
  className,
}: {
  readonly actions?: ReactNode;
  readonly className?: string;
}) {
  return (
    <Notice
      kind="danger"
      title={sessionCopy.noUsableErrorBody}
      actions={actions}
      className={className}
    />
  );
}

export interface SummaryItem {
  /** Verbatim. */
  readonly message: string;
  /** The field it names, when the association is unambiguous (the API has no field dictionary). */
  readonly fieldId?: string;
}

/**
 * The form's error summary: every message, verbatim and in order; one that names a field links to
 * it. After a failed submit it takes focus once and is announced assertively.
 */
export function ErrorSummary({
  title,
  items,
  focusOnMount = true,
  className,
}: {
  readonly title: ReactNode;
  readonly items: readonly SummaryItem[];
  readonly focusOnMount?: boolean;
  readonly className?: string;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (focusOnMount) ref.current?.focus();
  }, [focusOnMount]);
  return (
    <Notice ref={ref} tabIndex={-1} kind="danger" title={title} className={className}>
      <ul className="flex list-disc flex-col gap-stack-xs ps-5 type-body text-text-primary">
        {items.map((item, index) => (
          <li key={index}>
            {item.fieldId ? (
              <a
                href={`#${item.fieldId}`}
                onClick={(event) => {
                  event.preventDefault();
                  document.getElementById(item.fieldId ?? '')?.focus();
                }}
                className="text-text-link underline hover:text-text-link-hover"
              >
                {item.message}
              </a>
            ) : (
              item.message
            )}
          </li>
        ))}
      </ul>
    </Notice>
  );
}
