'use client';

import type { ReactNode } from 'react';
import { cn } from '@/lib/cn';
import { sessionCopy } from '@/lib/session-copy';
import { Button } from './button';
import { AlertIcon, CheckIcon, ErrorIcon, InfoIcon, Spinner } from './icons';
import { LineIcon } from './form-field';

// SaveStatus (design/specs/components/save-status.md): whether the count on screen is the count on
// the server, truthfully. It never claims a save the server did not acknowledge: times come from the
// acknowledged response (`DailyClose.UpdatedAt`, formatted by the screen), never the device clock.

export type SaveState =
  | { readonly kind: 'saved'; readonly time?: string; readonly date?: string }
  | { readonly kind: 'never-recorded' }
  | { readonly kind: 'dirty' }
  | { readonly kind: 'saving' }
  | { readonly kind: 'unsaved-on-server' }
  | { readonly kind: 'failed' }
  | { readonly kind: 'uncertain' }
  | { readonly kind: 'stale' }
  | { readonly kind: 'device-only' }
  | { readonly kind: 'storage-failed' };

export function saveStatusText(state: SaveState): string {
  switch (state.kind) {
    case 'saved':
      if (state.time === undefined) return 'Salvo';
      return state.date ? `Salvo em ${state.date} às ${state.time}` : `Salvo às ${state.time}`;
    case 'never-recorded':
      return 'Contagem ainda não salva';
    case 'dirty':
      return 'Alterações não salvas';
    case 'saving':
      return 'Salvando…';
    case 'unsaved-on-server':
      return 'Alterações não salvas no servidor';
    case 'failed':
      return 'Não salvo — tentar novamente';
    case 'uncertain':
      return sessionCopy.resultNotConfirmed;
    case 'stale':
      return 'Este fechamento mudou em outro lugar';
    case 'device-only':
      return sessionCopy.deviceDraft;
    case 'storage-failed':
      return sessionCopy.deviceStorageFailed;
  }
}

const tone: Record<SaveState['kind'], string> = {
  saved: 'text-text-muted',
  'never-recorded': 'text-text-secondary',
  dirty: 'text-text-secondary',
  saving: 'text-text-secondary',
  'unsaved-on-server': 'text-feedback-warning-fg',
  failed: 'text-feedback-danger-fg',
  uncertain: 'text-feedback-warning-fg',
  stale: 'text-feedback-warning-fg',
  'device-only': 'text-feedback-info-fg',
  'storage-failed': 'text-feedback-danger-fg',
};

function Mark({ state }: { readonly state: SaveState }) {
  switch (state.kind) {
    case 'saved':
      return <CheckIcon className="text-feedback-success-fg" />;
    case 'saving':
      return <Spinner />;
    case 'failed':
    case 'storage-failed':
      return <ErrorIcon />;
    case 'uncertain':
    case 'stale':
    case 'unsaved-on-server':
      return <AlertIcon />;
    case 'device-only':
      return <InfoIcon />;
    default:
      return null;
  }
}

/**
 * The status line. Settled states are announced once — failures and uncertainty assertively — never
 * each keystroke or debounce tick; the text is always visible too.
 */
export function SaveStatus({
  state,
  action,
  className,
}: {
  readonly state: SaveState;
  readonly action?: ReactNode;
  readonly className?: string;
}) {
  const urgent =
    state.kind === 'failed' || state.kind === 'uncertain' || state.kind === 'storage-failed';
  const quiet = state.kind === 'dirty' || state.kind === 'saving';
  return (
    <div className={cn('flex flex-wrap items-center gap-x-inline-md gap-y-stack-xs', className)}>
      <p
        role={urgent ? 'alert' : quiet ? undefined : 'status'}
        className={cn('flex items-start gap-inline-xs type-helper', tone[state.kind])}
      >
        <LineIcon>
          <Mark state={state} />
        </LineIcon>
        <span>{saveStatusText(state)}</span>
      </p>
      {action}
    </div>
  );
}

/**
 * **Salvar** beside its status, visible in every state: disabled with the clean reason only when
 * nothing changed since the last server save, and while the save runs.
 */
export function SaveBar({
  state,
  onSave,
  clean,
  action,
}: {
  readonly state: SaveState;
  readonly onSave: () => void;
  /** Nothing changed since the last acknowledged save (a never-recorded count is never clean). */
  readonly clean: boolean;
  readonly action?: ReactNode;
}) {
  const saving = state.kind === 'saving';
  return (
    <div className="flex flex-wrap items-start gap-inline-md">
      <Button
        onClick={onSave}
        loading={saving}
        loadingLabel="Salvando…"
        disabled={clean && !saving}
        disabledReason={
          clean && !saving ? 'Nenhuma alteração desde o último salvamento' : undefined
        }
      >
        Salvar
      </Button>
      <SaveStatus state={state} action={action} className="min-h-size-control-height" />
    </div>
  );
}
