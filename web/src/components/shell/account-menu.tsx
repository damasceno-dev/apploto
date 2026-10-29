'use client';
import { useState, useRef } from 'react';
import { useGetUserProfile } from '@loto/api/queries';
import { roleLabels } from '@loto/core';
import { useSession, beforeGateEvent } from '@/client/session/session-provider';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { sha256Hex, deviceStorage } from '@/client/storage/partition';
import { navigationKey } from '@/lib/navigation-state';
import { sessionCopy } from '@/lib/session-copy';
import { Menu, MenuLink, MenuItem } from '@/components/ui/menu';
import { Dialog } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Notice } from '@/components/ui/notice';
import { ThemeControl } from '@/theme/theme-control';
import type { ReactNode } from 'react';

export function AccountMenu({
  modeLabel,
  children,
  multiple = false,
  onSwitchBranch,
  liveRole,
}: {
  readonly modeLabel?: string;
  readonly children?: ReactNode;
  readonly multiple?: boolean;
  readonly onSwitchBranch?: () => void;
  readonly liveRole?: import('@loto/api').Role;
}) {
  const session = useSession();
  const client = useScopedApiClient();
  const profile = useGetUserProfile({
    query: { enabled: session.snapshot.user !== null, refetchOnWindowFocus: 'always' },
    request: { client },
  });
  const [unconfirmedOperation, setUnconfirmedOperation] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [pending, setPending] = useState(false);
  const [messages, setMessages] = useState<readonly string[]>([]);
  const busy = useRef(false);
  async function signOut(confirmed = false) {
    if (busy.current) return;
    busy.current = true;
    setPending(true);
    window.dispatchEvent(new Event(beforeGateEvent));
    try {
      if (!confirmed && session.snapshot.user) {
        const hash = await sha256Hex(session.snapshot.user.email);
        const storage = deviceStorage();
        const keys = storage
          ? Array.from({ length: storage.length }, (_, index) => storage.key(index))
          : [];
        const operationKeys = keys.filter(
          (key): key is string => key?.startsWith(`loto:op:v1:${hash}:`) === true,
        );
        const retained =
          operationKeys.length > 0 || keys.some((key) => key?.startsWith(`loto:draft:v1:${hash}:`));
        const unresolved = operationKeys.some((key) => {
          try {
            const value = JSON.parse(storage!.getItem(key) ?? 'null') as {
              status?: string;
              dispatches?: number;
              everUncertain?: boolean;
            } | null;
            return (
              value?.status === 'uncertain' ||
              value?.everUncertain === true ||
              (value?.status === 'pending' && (value.dispatches ?? 0) > 0)
            );
          } catch {
            return false;
          }
        });
        setUnconfirmedOperation(unresolved);
        if (retained) {
          setConfirm(true);
          return;
        }
      }
      const result = await session.logout();
      if ('ok' in result) {
        setMessages(result.errorMessages);
        return;
      }
      try {
        sessionStorage.removeItem(navigationKey);
        sessionStorage.setItem(
          'loto-logout-notice',
          result.serverConfirmed ? 'confirmed' : 'unconfirmed',
        );
      } catch {
        /* No navigation state is required for logout. */
      }
      window.location.assign(new URL('/entrar', window.location.origin).href);
    } catch {
      setMessages([sessionCopy.noUsableErrorBody]);
    } finally {
      busy.current = false;
      setPending(false);
    }
  }
  const user = profile.data?.data ?? session.snapshot.user;
  const retainOrigin = () => {
    try {
      sessionStorage.setItem(
        'loto-account-origin',
        JSON.stringify({
          identity: session.snapshot.user?.userId,
          href: window.location.pathname + window.location.search,
        }),
      );
    } catch {
      /* Direct-entry fallback remains available. */
    }
  };
  return (
    <>
      <Menu
        title="Sua conta"
        trigger={
          <>
            <span
              className="flex size-size-avatar shrink-0 items-center justify-center rounded-full bg-surface-selected type-label"
              aria-hidden="true"
            >
              {user?.name?.slice(0, 1) ?? 'C'}
            </span>
            <span className="hidden medium:flex flex-col text-start">
              <span>{user?.name ?? 'Conta'}</span>
              {session.snapshot.branch ? (
                <span className="type-helper text-text-muted">
                  {roleLabels[liveRole ?? session.snapshot.branch.role]}
                </span>
              ) : null}
            </span>
          </>
        }
        triggerLabel={`Conta de ${user?.name ?? ''}${modeLabel ? `, ${modeLabel}` : ''}`}
        header={
          <div className="flex flex-col gap-stack-xs">
            <strong>{user?.name}</strong>
            <span>{user?.email}</span>
            {session.snapshot.branch ? (
              <span>
                {session.snapshot.branch.name} ·{' '}
                {roleLabels[liveRole ?? session.snapshot.branch.role]}
              </span>
            ) : null}
            {modeLabel ? <span>{modeLabel}</span> : null}
          </div>
        }
      >
        {children}
        {multiple ? (
          onSwitchBranch ? (
            <MenuItem label="Trocar filial" onSelect={onSwitchBranch} />
          ) : (
            <MenuLink href="/filiais" label="Trocar filial" />
          )
        ) : null}
        <MenuLink href="/conta" label="Minha conta" onNavigate={retainOrigin} />
        {profile.data?.data.hasPassword ? (
          <MenuLink href="/conta/senha" label="Alterar senha" onNavigate={retainOrigin} />
        ) : null}
        {profile.data?.data.isPlatformOperator ? (
          <MenuLink href="/plataforma" label="Plataforma" />
        ) : null}
        <MenuItem label="Sair" busy={pending} busyLabel="Saindo…" onSelect={() => void signOut()} />
        <div className="border-t border-border-default p-4 medium:hidden">
          <ThemeControl iconsOnly />
        </div>
      </Menu>
      {messages.length && !confirm ? (
        <Notice kind="danger" title="Não foi possível sair" messages={messages} />
      ) : null}
      <Dialog
        open={confirm}
        busy={pending}
        onDismiss={() => setConfirm(false)}
        title="Sair com dados guardados?"
        description="Há alterações guardadas neste dispositivo. Elas continuam guardadas e voltam quando você entrar de novo com este e-mail."
        footer={
          <>
            <Button variant="secondary" onClick={() => setConfirm(false)}>
              Continuar aqui
            </Button>
            <Button loading={pending} loadingLabel="Saindo…" onClick={() => void signOut(true)}>
              Sair
            </Button>
          </>
        }
      >
        {unconfirmedOperation ? (
          <p>
            O resultado da operação ainda precisa ser conferido depois que você entrar novamente.
          </p>
        ) : null}
        {messages.length ? (
          <Notice kind="danger" title="Não foi possível sair" messages={messages} />
        ) : null}
      </Dialog>
    </>
  );
}
