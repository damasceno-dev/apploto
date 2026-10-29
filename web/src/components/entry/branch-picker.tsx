'use client';
import { useEffect, useRef, useState } from 'react';
import { useGetUserProfile } from '@loto/api/queries';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import { formatCnpj, roleLabels } from '@loto/core';
import type { SessionSnapshot } from '@/lib/bff-protocol';
import { landingFor } from '@/lib/route-policy';
import { sessionCopy } from '@/lib/session-copy';
import { useSession } from '@/client/session/session-provider';
import { Button, ButtonLink } from '@/components/ui/button';
import { LoadingRegion, Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/ui/empty-state';
import { Notice } from '@/components/ui/notice';
import { AccountMenu } from '@/components/shell/account-menu';
import { IdentitySheet } from './identity-sheet';

export function BranchPicker() {
  const { readBranchChoice, selectBranch, snapshot } = useSession();
  const client = useScopedApiClient();
  const profile = useGetUserProfile({
    request: { client },
    query: { refetchOnWindowFocus: 'always' },
  });
  const [loading, setLoading] = useState(true);
  const reading = useRef(false);
  const [choice, setChoice] = useState<SessionSnapshot | null>(null);
  const [messages, setMessages] = useState<readonly string[]>([]);
  const [pending, setPending] = useState<string | null>(null);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const started = useRef(false);
  const busy = useRef(false);
  async function load() {
    if (reading.current) return;
    reading.current = true;
    setLoading(true);
    setMessages([]);
    try {
      const result = await readBranchChoice();
      if (!alive.current) return;
      if (!result.ok) {
        setMessages(
          result.errorMessages.length ? result.errorMessages : [sessionCopy.noUsableErrorBody],
        );
        return;
      }
      setChoice(result.snapshot);
      if (result.snapshot.messages.length) setMessages(result.snapshot.messages);
      if (result.snapshot.state === 'branch' || result.snapshot.resume !== null)
        window.location.replace(landingFor(result.snapshot));
    } catch {
      if (alive.current) setMessages([sessionCopy.noUsableErrorBody]);
    } finally {
      reading.current = false;
      if (alive.current) setLoading(false);
    }
  }
  useEffect(() => {
    if (!started.current) {
      started.current = true;
      void load();
    }
  });
  async function choose(id: string) {
    if (busy.current) return;
    busy.current = true;
    setPending(id);
    setMessages([]);
    try {
      const result = await selectBranch(id);
      if (!alive.current) return;
      if (result.ok && result.snapshot.state === 'branch') {
        window.location.assign(landingFor(result.snapshot));
        return;
      }
      if (!result.ok)
        setMessages(
          result.errorMessages.length ? result.errorMessages : [sessionCopy.noUsableErrorBody],
        );
      else {
        setChoice(result.snapshot);
        setMessages(result.snapshot.messages);
      }
    } catch {
      setMessages([sessionCopy.resultNotConfirmed, sessionCopy.confirmationWarning]);
    } finally {
      busy.current = false;
      setPending(null);
    }
  }
  return (
    <IdentitySheet title="Escolha a filial">
      {messages.length ? (
        <Notice
          kind="danger"
          title="Não foi possível abrir a filial"
          messages={messages}
          actions={
            <Button
              variant="secondary"
              loading={loading}
              loadingLabel="Atualizando…"
              onClick={() => void load()}
            >
              Atualizar filiais
            </Button>
          }
        />
      ) : null}
      {loading ? (
        <LoadingRegion
          loading
          skeleton={
            <div className="flex flex-col gap-stack-sm">
              <Skeleton className="h-size-control-height" />
              <Skeleton className="h-size-control-height" />
              <Skeleton className="h-size-control-height" />
            </div>
          }
        />
      ) : null}
      {choice?.state === 'no-memberships' ? (
        <EmptyState
          kind="setup"
          statement="Você ainda não tem acesso a uma lotérica."
          explanation="Abra o link do convite recebido por e-mail. Se ainda não recebeu um convite, peça à administração da lotérica acesso para sua conta."
        />
      ) : null}
      {choice?.state === 'branch' ? <p role="status">Abrindo {choice.branch?.name}…</p> : null}
      <div className="flex flex-col gap-stack-sm">
        {(choice?.state === 'branch' ? [] : choice?.branches)?.map((branch) => (
          <Button
            key={branch.id}
            variant="secondary"
            disabled={pending !== null && pending !== branch.id}
            loading={pending === branch.id}
            loadingLabel="Abrindo…"
            onClick={() => void choose(branch.id)}
            className="w-full justify-start text-start"
          >
            <span className="flex flex-col gap-stack-xs">
              <span className="type-body-strong">
                {branch.name} — {roleLabels[branch.role]}
              </span>
              {branch.address ? <span className="type-helper">{branch.address}</span> : null}
              {branch.cnpj ? <span className="type-helper">{formatCnpj(branch.cnpj)}</span> : null}
            </span>
          </Button>
        ))}
      </div>
      {profile.data?.data.isPlatformOperator ? (
        <ButtonLink href="/plataforma">Plataforma</ButtonLink>
      ) : null}
      <footer className="flex flex-col gap-stack-sm border-t border-border-default pt-4">
        <p>
          {snapshot.user?.name} · {snapshot.user?.email}
        </p>
        <ButtonLink href={'/conta' as never}>Minha conta</ButtonLink>
        <AccountMenu />
      </footer>
    </IdentitySheet>
  );
}
