import { useEffect, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ApiError } from '@loto/api';
import {
  formatBrlWire,
  isWireMoney,
  formatSignedBrlWire,
  formatDate,
  formatInstant,
  parseCalendarDate,
} from '@loto/core';
import { useSession } from '@/session/provider';
import { Button, Notice, Skeleton, UncertainNotice } from '@/ui';
import { calendarPart, managerRole } from './contracts';
import { deviceZone } from '@/day/contracts';

import { errorMessages } from '@/time/common';

export { errorMessages };
export { CommandState } from '@/day/common';
export { useDayCommand as useDecisionCommand } from '@/day/common';

/** Manager/Admin read gate: refuses a Member (and any reduced role) before a single read is made. */
export function useManagerReads(): boolean {
  const { snapshot } = useSession();
  return (
    snapshot.status === 'branch' &&
    snapshot.online &&
    snapshot.foreground &&
    managerRole(snapshot.branch?.role)
  );
}
/**
 * Authority only (verified role + branch): decides whether manager content may render at all. A
 * transient foreground/connectivity change must never unmount a review, because that would drop a
 * typed reason, an open confirmation or a confirmed-decision receipt; it only pauses the reads.
 */
export function useManagerAccess(): boolean {
  const { snapshot } = useSession();
  // Role only: the session status changes during a credential renewal, and unmounting the screen
  // then would discard a typed reason, a selection or an open confirmation.
  return managerRole(snapshot.branch?.role);
}
/**
 * Cancels every scoped manager read the moment the verified role, branch or scope stops authorizing
 * it, so a stale result can never render after a role reduction, logout or branch switch.
 */
export function useManagerScopeTeardown() {
  const { snapshot } = useSession();
  const queries = useQueryClient();
  const allowed = managerRole(snapshot.branch?.role);
  useEffect(() => {
    if (!allowed) void queries.cancelQueries({ queryKey: ['manager'] });
    return () => void queries.cancelQueries({ queryKey: ['manager', snapshot.scope] });
  }, [allowed, queries, snapshot.scope]);
  return allowed;
}
export function ManagerGate({ children }: { children: ReactNode }) {
  const { snapshot } = useSession();
  if (!snapshot.branch || !snapshot.clock) return <Skeleton />;
  if (!managerRole(snapshot.branch.role))
    return <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />;
  return <>{children}</>;
}
/**
 * Server-returned amount: unsigned when non-negative, signed (U+2212) when negative. A figure the
 * server computed with more than two decimals (a mean) is shown exactly as returned — never rounded
 * and never allowed to break the screen.
 */
export const money = (value: number | null | undefined) => {
  if (value === null || value === undefined) return '—';
  if (isWireMoney(value)) return value < 0 ? formatSignedBrlWire(value) : formatBrlWire(value);
  const exact = String(Math.abs(value)).replace('.', ',');
  return `${value < 0 ? '\u2212' : ''}R$\u00a0${exact}`;
};
export const day = (value: string) => {
  const part = calendarPart(value);
  return parseCalendarDate(part) ? formatDate(part as never) : value;
};
export const instant = (value: string | null | undefined) =>
  value ? formatInstant(value, deviceZone()) : null;
export const httpStatus = (error: unknown) => (error instanceof ApiError ? error.status : null);
export const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;
/** Query key root for every Manager M7 read; tearing down cancels exactly this tree. */
export const managerKey = (scope: unknown, ...rest: readonly unknown[]) =>
  ['manager', scope, ...rest] as const;

export { useTimeCommand as useOnceCommand } from '@/time/common';

/**
 * Refusal / unknown-outcome / post-write state for a NON-idempotent command (reject, reopen, recall,
 * attribution). It is never replayed: the only way forward from an unknown outcome is a fresh read.
 */
export function OnceState({
  command,
  receipt,
  reread,
  testID,
}: {
  command: {
    error: unknown;
    uncertain: boolean;
    postWriteError: unknown;
  };
  receipt: string;
  reread: () => void;
  testID: string;
}) {
  if (command.postWriteError)
    return (
      <Notice
        kind="warning"
        testID={`${testID}-post-write`}
        title={`${receipt} A releitura não foi concluída.`}
        body="Nada será enviado de novo. Releia os dados para continuar."
        messages={errorMessages(command.postWriteError)}
        actions={<Button label="Reler e conferir" onPress={reread} />}
      />
    );
  if (command.uncertain)
    return (
      <UncertainNotice
        testID={`${testID}-uncertain`}
        messages={errorMessages(command.error)}
        recovery={<Button label="Reler e conferir" testID={`${testID}-reread`} onPress={reread} />}
      />
    );
  if (command.error)
    return (
      <Notice
        kind="danger"
        testID={`${testID}-error`}
        title="O servidor não aceitou a ação."
        body="O que você digitou continua na tela."
        messages={errorMessages(command.error)}
        actions={<Button label="Reler e conferir" variant="secondary" onPress={reread} />}
      />
    );
  return null;
}
