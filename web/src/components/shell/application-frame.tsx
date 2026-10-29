'use client';
import Link from 'next/link';
import { useEffect, useRef, useState } from 'react';
import { usePathname } from 'next/navigation';
import { isApiError, type ResponseSelfContextJson, type Role } from '@loto/api';
import { useGetOperatorSelfContext, useGetBranchMyBranches } from '@loto/api/queries';
import { formatWallDateTime, roleLabels } from '@loto/core';
import { hasScopedRetainedInput } from '@/client/storage/retained-input';
import { branchAccessSignalEvent } from '@/client/bff-fetch';
import { useBranchClock } from '@/client/clock/use-branch-clock';
import { beforeGateEvent, scopeExitEvent, useSession } from '@/client/session/session-provider';
import { useScopedApiClient } from '@/client/query/scoped-query-provider';
import {
  destinations,
  canEnter,
  defaultMode,
  destinationMode,
  destinationHref,
  homeKey,
  matchDestination,
  type WorkMode,
} from '@/lib/destinations';
import { parseBranchPath } from '@/lib/branch-route';
import { rememberNavigation } from '@/lib/scoped-navigation';
import { navigationKey, validateNavigation, type NavigationEntry } from '@/lib/navigation-state';
import { sessionCopy } from '@/lib/session-copy';
import { Button, ButtonLink } from '@/components/ui/button';
import { LoadingRegion, Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/ui/page-header';
import { Notice } from '@/components/ui/notice';
import { Dialog } from '@/components/ui/dialog';
import { ModeBadge } from '@/components/ui/badge';
import { MenuGroup, MenuRadioItem, MenuUnavailable } from '@/components/ui/menu';
import { ThemeControl } from '@/theme/theme-control';
import { AccountMenu } from './account-menu';

export function ApplicationFrame({
  initialSelf,
  initialDate,
  multiple,
  initialRole,
  initialSelfFailure = null,
  initialBranchFailure = null,
}: {
  readonly initialSelf: ResponseSelfContextJson | null;
  readonly initialSelfFailure?: readonly string[] | null;
  readonly initialBranchFailure?: readonly string[] | null;
  readonly initialDate: string;
  readonly multiple: boolean | null;
  readonly initialRole: Role;
}) {
  const session = useSession();
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  const client = useScopedApiClient();
  const pathname = usePathname();
  const branch = session.snapshot.branch!;
  const entry = matchDestination(parseBranchPath(pathname)?.rest ?? '');
  const clock = useBranchClock();
  const self = useGetOperatorSelfContext({
    query: {
      ...(initialSelf === null
        ? {}
        : { initialData: { data: initialSelf, status: 200 as const, headers: new Headers() } }),
      staleTime: 0,
      refetchOnMount: 'always',
      refetchOnWindowFocus: 'always',
      refetchOnReconnect: 'always',
    },
    request: { client },
  });
  const branches = useGetBranchMyBranches({
    query: { enabled: multiple === null, refetchOnWindowFocus: 'always' },
    request: { client },
  });
  const showSwitchBranch = multiple ?? (branches.data?.data.branches.length ?? 0) > 1;
  const branchFailure = branches.isError
    ? isApiError(branches.error)
      ? branches.error.errorMessages
      : []
    : branches.data
      ? null
      : initialBranchFailure;
  const selfFailure = self.isError
    ? isApiError(self.error)
      ? self.error.errorMessages
      : []
    : self.data
      ? null
      : initialSelfFailure;
  const linkKnown = self.data !== undefined;
  const linked = Boolean(self.data?.data.operatorId);
  const linkMissing = linkKnown && !linked;

  const role = clock.state.status === 'ready' ? clock.state.clock.liveRole : initialRole;
  const [selectedMode, setMode] = useState<WorkMode>(() =>
    entry ? destinationMode(entry, role) : defaultMode(role),
  );
  const [lastPath, setLastPath] = useState(pathname);
  if (lastPath !== pathname && entry) {
    const previous = matchDestination(parseBranchPath(lastPath)?.rest ?? '');
    setLastPath(pathname);
    setMode(
      destinationMode(
        entry,
        role,
        previous ? destinationMode(previous, role, selectedMode) : selectedMode,
      ),
    );
  }
  if (entry?.owner === 'shared' && role !== 'Member' && linkMissing && selectedMode === 'shift')
    setMode('management');
  const resolvedMode = entry ? destinationMode(entry, role, selectedMode) : defaultMode(role);
  const mode =
    role !== 'Member' && entry?.owner === 'shared' && linkMissing ? 'management' : resolvedMode;
  const [restored, setRestored] = useState(false);
  const [origin, setOrigin] = useState<NavigationEntry['origins']>([]);
  const [drawer, setDrawer] = useState(false);
  const [online, setOnline] = useState(true);
  const [roleNotice, setRoleNotice] = useState(false);
  const roleNoticeKey = `loto-role-change:${session.snapshot.user?.userId}:${branch.id}`;
  const [retained, setRetained] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [rechecking, setRechecking] = useState(false);
  const date = clock.state.status === 'ready' ? clock.state.clock.branchLocalDate : initialDate;
  const href = (key: string) => destinationHref(key, branch.id, date);
  const storageId = `${session.snapshot.user?.userId}|${branch.id}`;
  const modeRef = useRef(mode);

  const originRef = useRef(origin);

  const identityRef = useRef(storageId);
  useEffect(() => {
    modeRef.current = mode;
    originRef.current = origin;
    identityRef.current = storageId;
  }, [mode, origin, storageId]);

  useEffect(() => {
    let candidate: NavigationEntry | null = null;
    try {
      sessionStorage.removeItem('loto-navigation-v1');
      const saved = JSON.parse(sessionStorage.getItem(navigationKey) ?? '{}') as Record<
        string,
        unknown
      >;
      const held = window.history.state?.lotoNavigation as
        { identity?: unknown; branchId?: unknown; entry?: unknown } | undefined;
      const source =
        held?.identity === session.snapshot.user?.userId && held?.branchId === branch.id
          ? held.entry
          : saved[`${storageId}|${window.location.pathname + window.location.search}`];
      candidate = validateNavigation(source, branch.id, role, linked);
    } catch {
      /* Corrupt/unavailable state uses the catalog fallback. */
    }
    const current = window.location.pathname + window.location.search;
    const frame = requestAnimationFrame(() => {
      if (candidate && candidate.href === current && entry) {
        setMode(destinationMode(entry, role, candidate.mode));
        setOrigin(candidate.origins);
        const restore = candidate;
        requestAnimationFrame(() => {
          window.scrollTo(0, restore.scroll);
          if (restore.focus) document.getElementById(restore.focus)?.focus();
        });
      }
      setRestored(true);
    });
    return () => cancelAnimationFrame(frame);
    // Validate once per server-validated page. Subsequent role/link changes are handled below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname, storageId]);
  useEffect(() => {
    const address = pathname + window.location.search;
    const save = () => {
      if (!restored || !entry || entry.path.includes(':id')) return;
      const value: NavigationEntry = {
        href: address,
        mode: modeRef.current,
        origins: originRef.current,
        scroll: window.scrollY,
        focus: document.activeElement?.id || null,
      };
      try {
        const saved = JSON.parse(sessionStorage.getItem(navigationKey) ?? '{}') as Record<
          string,
          unknown
        >;
        saved[`${identityRef.current}|${value.href}`] = value;
        sessionStorage.setItem(navigationKey, JSON.stringify(saved));
        if (window.location.pathname + window.location.search === address)
          window.history.replaceState(
            {
              ...window.history.state,
              lotoNavigation: {
                identity: session.snapshot.user?.userId,
                branchId: branch.id,
                entry: value,
              },
            },
            '',
          );
      } catch {
        /* Storage is optional; never block a safe navigation. */
      }
    };
    const connection = () => setOnline(navigator.onLine);
    const foreground = () => {
      if (document.visibilityState === 'visible') {
        void session.refresh();
        void self.refetch();
        void clock.refresh();
      }
    };
    const before = () => save();
    const beforeNavigation = (event: MouseEvent) => {
      if ((event.target as Element | null)?.closest('a[href]')) save();
    };
    const back = () => setDrawer(false);
    document.addEventListener('click', beforeNavigation, true);
    window.addEventListener('popstate', back);
    window.addEventListener('pagehide', save);
    window.addEventListener(beforeGateEvent, before);
    window.addEventListener('online', connection);
    window.addEventListener('offline', connection);
    document.addEventListener('visibilitychange', foreground);
    return () => {
      save();
      document.removeEventListener('click', beforeNavigation, true);
      window.removeEventListener('popstate', back);
      window.removeEventListener('pagehide', save);
      window.removeEventListener(beforeGateEvent, before);
      window.removeEventListener('online', connection);
      window.removeEventListener('offline', connection);
      document.removeEventListener('visibilitychange', foreground);
    };
  }, [restored, entry, session, self, clock, pathname, branch.id]);
  useEffect(() => {
    if (!entry || !canEnter(entry, role)) {
      try {
        sessionStorage.setItem(roleNoticeKey, 'changed');
      } catch {
        /* Navigation still follows the live role. */
      }
      window.location.replace(href(homeKey(defaultMode(role))));
      return;
    }

    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entry, role]);
  useEffect(() => {
    let active = true;
    const check = () => {
      void hasScopedRetainedInput(session.snapshot).then((value) => {
        if (active) setRetained(value);
      });
    };
    check();
    window.addEventListener('storage', check);
    window.addEventListener(beforeGateEvent, check);
    return () => {
      active = false;
      window.removeEventListener('storage', check);
      window.removeEventListener(beforeGateEvent, check);
    };
  }, [session.snapshot]);
  useEffect(() => {
    if (entry?.owner === 'shift') void self.refetch();
    const denied = (event: Event) => {
      if ((event as CustomEvent<{ branchId: string }>).detail?.branchId === branch.id) {
        void self.refetch();
        void clock.refresh();
      }
    };
    window.addEventListener(branchAccessSignalEvent, denied);
    return () => window.removeEventListener(branchAccessSignalEvent, denied);
    // The route-entry and access-refusal boundaries explicitly request new authoritative context.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname, branch.id]);
  async function switchMode(next: WorkMode) {
    if (rechecking) return;
    setRechecking(true);
    try {
      if (next === 'shift') {
        const result = await self.refetch();
        if (!alive.current || result.isError || !result.data?.data.operatorId) return;
      } else {
        // Re-read at the mode boundary without making Gestão depend on optional operator metadata.
        void self.refetch();
      }
      await session.refresh();
      if (!alive.current) return;
      setMode(next);
      try {
        const saved = JSON.parse(sessionStorage.getItem(navigationKey) ?? '{}') as Record<
          string,
          unknown
        >;
        saved[`${storageId}|${href(homeKey(next))}`] = {
          href: href(homeKey(next)),
          mode: next,
          origins: [],
          scroll: 0,
          focus: null,
        };
        sessionStorage.setItem(navigationKey, JSON.stringify(saved));
      } catch {
        /* Default home still resolves safely. */
      }
      window.dispatchEvent(new Event(beforeGateEvent));
      window.dispatchEvent(new CustomEvent(scopeExitEvent, { detail: { branchId: branch.id } }));
      window.location.assign(href(homeKey(next)));
    } finally {
      setRechecking(false);
    }
  }
  function switchBranch() {
    window.dispatchEvent(new Event(beforeGateEvent));
    try {
      const saved = JSON.parse(sessionStorage.getItem(navigationKey) ?? '{}') as Record<
        string,
        unknown
      >;
      for (const key of Object.keys(saved)) if (key.startsWith(`${storageId}|`)) delete saved[key];
      sessionStorage.setItem(navigationKey, JSON.stringify(saved));
    } catch {
      /* No retained financial input is touched. */
    }
    window.dispatchEvent(new CustomEvent(scopeExitEvent, { detail: { branchId: branch.id } }));
    window.location.assign(new URL('/filiais', window.location.origin).href);
  }
  const shiftUnavailable = entry?.owner === 'shift' && role !== 'Member' && linkMissing;
  useEffect(() => {
    try {
      if (sessionStorage.getItem(roleNoticeKey) === 'changed') {
        const frame = requestAnimationFrame(() => {
          sessionStorage.removeItem(roleNoticeKey);
          setRoleNotice(true);
        });
        return () => cancelAnimationFrame(frame);
      }
    } catch {
      /* Optional non-secret explanation. */
    }
  }, [roleNoticeKey]);
  const notices: { key: string; node: React.ReactNode }[] = [];
  if (session.recovering || session.snapshot.state === 'renewal-unconfirmed')
    notices.push({
      key: 'recovery',
      node: (
        <Notice
          kind="warning"
          title={sessionCopy.sessionRenewing}
          actions={
            <Button variant="secondary" onClick={() => void session.refresh()}>
              Verificar sessão
            </Button>
          }
        />
      ),
    });
  if (roleNotice)
    notices.push({
      key: 'role',
      node: (
        <Notice
          kind="info"
          title="Seu perfil nesta filial mudou. Abrimos o início disponível para seu acesso."
        />
      ),
    });
  if (clock.state.status === 'ready' && clock.state.dayChanged)
    notices.push({
      key: 'day',
      node: (
        <Notice
          kind="info"
          title={sessionCopy.branchDayChanged}
          actions={
            <Button variant="text" onClick={clock.acknowledgeDayChange}>
              Entendi
            </Button>
          }
        />
      ),
    });
  if (!online || clock.state.status === 'error' || clock.state.status === 'invalid')
    notices.push({
      key: 'connection',
      node: (
        <Notice kind="warning" title="Sem conexão atualizada com a filial">
          Confira sua conexão. Os dados anteriores estão desatualizados.
        </Notice>
      ),
    });
  if (selfFailure !== null)
    notices.push({
      key: 'self',
      node: (
        <Notice
          kind="danger"
          title="Não foi possível verificar seu vínculo"
          messages={selfFailure}
          actions={
            <Button variant="secondary" onClick={() => void self.refetch()}>
              Tentar novamente
            </Button>
          }
        />
      ),
    });
  if (retained)
    notices.push({
      key: 'retained',
      node: (
        <Notice kind="info" title="Há dados guardados neste dispositivo.">
          Os dados continuam vinculados à conta e à filial de origem. Confira o contexto antes de
          continuar.
        </Notice>
      ),
    });
  if (branchFailure !== null)
    notices.push({
      key: 'branches',
      node: (
        <Notice
          kind="warning"
          title="Não foi possível verificar as filiais disponíveis"
          messages={branchFailure}
          actions={
            <Button variant="secondary" onClick={() => void branches.refetch()}>
              Tentar novamente
            </Button>
          }
        />
      ),
    });
  const context = (onNav = false) => (
    <div className="flex flex-wrap items-center gap-inline-md">
      <strong className="[overflow-wrap:anywhere]">{branch.name}</strong>
      <ModeBadge
        mode={mode === 'management' ? 'gestao' : 'turno'}
        className={onNav ? 'bg-nav-active text-nav-text-strong' : undefined}
      />
      <LoadingRegion
        loading={clock.state.status !== 'ready'}
        label="Carregando horário da filial…"
        skeleton={<Skeleton className="w-form-column-min max-w-full" />}
        className="type-helper min-w-0 max-w-full"
      >
        {clock.state.status === 'ready' ? (
          <time data-testid="branch-clock" dateTime={clock.state.clock.branchLocalDateTime}>
            {formatWallDateTime(clock.state.clock.branchLocalDateTime)} · horário da filial
          </time>
        ) : null}
      </LoadingRegion>
      {showSwitchBranch ? (
        <Button
          variant="text"
          className={
            onNav
              ? 'text-nav-text-strong hover:text-nav-text-strong hover:bg-nav-active'
              : undefined
          }
          onClick={switchBranch}
        >
          Trocar filial
        </Button>
      ) : null}
    </div>
  );
  const navEntries = destinations.filter(
    (destination) =>
      destination.group &&
      canEnter(destination, role) &&
      (mode === 'management'
        ? destination.owner !== 'shift'
        : [
            'today',
            'create',
            'transactions',
            'clients',
            'clock',
            'my-transactions',
            'my-hours',
          ].includes(destination.key)),
  );
  const navOrder =
    mode === 'shift'
      ? ['today', 'create', 'transactions', 'clients', 'clock', 'my-transactions', 'my-hours']
      : [
          'queue',
          'transactions',
          'create',
          'dashboard',
          'ledger',
          'fiado',
          'aging',
          'cheques',
          'receivables',
          'variance',
          'month-close',
          'month-financial',
          'time',
          'operators',
          'assignments',
          'accounts',
          'clients',
          'categories',
          'products',
          'holidays',
          'settings',
          'members',
        ];
  navEntries.sort((a, b) => navOrder.indexOf(a.key) - navOrder.indexOf(b.key));
  const navigation = (
    <nav aria-label="Navegação principal" className="flex flex-col gap-stack-md p-4">
      {Array.from(
        new Set(
          navEntries.map((destination) =>
            mode === 'shift' && ['create', 'transactions', 'clients'].includes(destination.key)
              ? 'Meu turno'
              : destination.group,
          ),
        ),
      )
        .sort(
          (a, b) =>
            ['Gestão', 'Meu turno', 'Relatórios', 'Ponto', 'Cadastros', 'Meus números'].indexOf(
              a ?? '',
            ) -
            ['Gestão', 'Meu turno', 'Relatórios', 'Ponto', 'Cadastros', 'Meus números'].indexOf(
              b ?? '',
            ),
        )
        .map((group) => (
          <section key={group}>
            <h2 className="type-overline text-nav-text-muted mb-2">{group}</h2>
            <ul>
              {navEntries
                .filter(
                  (destination) =>
                    (mode === 'shift' &&
                    ['create', 'transactions', 'clients'].includes(destination.key)
                      ? 'Meu turno'
                      : destination.group) === group,
                )
                .map((destination) => (
                  <li key={destination.key}>
                    <Link
                      href={href(destination.key) as never}
                      onClick={(event) => {
                        if (
                          event.metaKey ||
                          event.ctrlKey ||
                          event.shiftKey ||
                          event.altKey ||
                          event.button !== 0
                        )
                          return;
                        try {
                          rememberNavigation(
                            sessionStorage,
                            {
                              identity: session.snapshot.user?.userId ?? '',
                              branchId: branch.id,
                              role,
                              linked,
                            },
                            {
                              href: href(destination.key),
                              mode: destinationMode(destination, role, mode),
                              origins: [],
                              scroll: 0,
                              focus: null,
                            },
                          );
                        } catch {
                          /* Safe role defaults apply when navigation storage is unavailable. */
                        }
                        setDrawer(false);
                      }}
                      aria-current={destination.key === entry?.key ? 'page' : undefined}
                      className={`block min-h-size-touch-target-min border-s-(length:--spacing-size-marker-width) px-3 py-2 type-body text-nav-text-strong [overflow-wrap:anywhere] hover:bg-nav-active ${destination.key === entry?.key ? 'border-nav-marker bg-nav-active' : 'border-transparent'}`}
                    >
                      {destination.title}
                    </Link>
                  </li>
                ))}
            </ul>
          </section>
        ))}
    </nav>
  );
  if (!entry || !canEnter(entry, role)) return null;
  const fallback = entry.fallback ? href(entry.fallback) : href(homeKey(mode));
  const crumbs = origin.length
    ? origin
    : entry.key === homeKey(mode)
      ? []
      : [
          {
            href: fallback,
            label:
              destinations.find(
                (destination) => destination.key === (entry.fallback ?? homeKey(mode)),
              )?.title ?? 'Início',
          },
        ];
  return (
    <>
      <a
        href="#conteudo"
        className="fixed top-0 -translate-y-[200%] start-4 focus:translate-y-0 layer-skip-link bg-surface-default p-4 text-text-link"
      >
        Pular para o conteúdo
      </a>
      <div className="min-h-screen wide:grid wide:grid-cols-[var(--spacing-size-sidebar)_minmax(0,1fr)]">
        <aside
          data-on-nav
          className="hidden wide:block sticky top-0 h-dvh overflow-y-auto bg-nav-bg text-nav-text-strong"
        >
          <div className="p-4 type-section-title">Lotero</div>
          {navigation}
        </aside>
        <div className="min-w-0">
          <header className="min-h-size-topbar-min sticky top-0 layer-sticky flex flex-wrap items-center justify-between gap-stack-sm border-b border-topbar-border bg-topbar-bg px-page-inline-compact py-2 shadow-raised">
            <Button variant="text" className="wide:hidden" onClick={() => setDrawer(true)}>
              Menu
            </Button>
            <span className="medium:hidden type-body-strong">{entry.title}</span>
            <div className="hidden medium:block">{context()}</div>
            <div className="hidden medium:block">
              <ThemeControl iconsOnly />
            </div>
            <AccountMenu
              modeLabel={mode === 'management' ? 'Gestão' : 'Meu turno'}
              multiple={showSwitchBranch}
              liveRole={role}
              onSwitchBranch={switchBranch}
            >
              {role !== 'Member' ? (
                <MenuGroup label="Modo de trabalho">
                  <MenuRadioItem
                    checked={mode === 'management'}
                    label="Gestão"
                    onSelect={() => void switchMode('management')}
                  />
                  {self.isFetching || rechecking ? (
                    <MenuUnavailable label="Meu turno" reason="Verificando…" />
                  ) : linked ? (
                    <MenuRadioItem
                      checked={mode === 'shift'}
                      label="Meu turno"
                      onSelect={() => void switchMode('shift')}
                    />
                  ) : (
                    <MenuUnavailable
                      label="Meu turno"
                      reason={
                        self.isError
                          ? 'Não foi possível verificar seu vínculo.'
                          : 'Indisponível: seu login não está vinculado a um operador desta filial.'
                      }
                      fix={{ label: 'Resolver em Operadores', href: href('operators') }}
                    />
                  )}
                </MenuGroup>
              ) : null}
            </AccountMenu>
          </header>
          <div
            className="flex flex-col gap-stack-sm px-page-inline-compact pt-2"
            role="region"
            aria-label="Avisos da sessão"
          >
            {(expanded ? notices : notices.slice(0, 2)).map((notice) => (
              <div key={notice.key}>{notice.node}</div>
            ))}
            {notices.length > 2 ? (
              <Button variant="text" onClick={() => setExpanded(!expanded)}>
                {expanded ? 'Recolher avisos' : `+${notices.length - 2} avisos`}
              </Button>
            ) : null}
          </div>
          <main
            id="conteudo"
            tabIndex={-1}
            className="mx-auto max-w-content-max px-page-inline-compact py-page-block-start medium:px-page-inline-wide"
          >
            <PageHeader
              key={entry.key}
              title={entry.title}
              origin={crumbs}
              context={`${branch.name} · ${roleLabels[role]} · ${mode === 'management' ? 'Gestão' : 'Meu turno'}`}
            />
            {entry.owner === 'shift' && !linkKnown && selfFailure === null ? (
              <LoadingRegion loading skeleton={<Skeleton className="h-size-control-height" />} />
            ) : shiftUnavailable ? (
              <div className="flex flex-col gap-stack-md">
                <Notice kind="info" title="Meu turno não está disponível para você nesta filial.">
                  <p>
                    {entry.title} faz parte do trabalho de quem registra o próprio caixa. Seu login
                    precisa estar vinculado a um operador ativo desta filial.
                  </p>
                  <p>
                    Seu perfil permanece {roleLabels[role]} e todas as telas de Gestão continuam
                    disponíveis. Cadastre-se em Operadores ou vincule seu login a um operador
                    existente. Depois, atribua uma conta em Contas dos operadores.
                  </p>
                </Notice>
                <ButtonLink href={href('operators') as never} variant="primary">
                  Ir para Operadores
                </ButtonLink>
                <ButtonLink href={href('assignments') as never}>Contas dos operadores</ButtonLink>
                <ButtonLink href={href('queue') as never}>
                  Voltar para a fila de trabalho
                </ButtonLink>
              </div>
            ) : selfFailure !== null && entry.owner === 'shift' ? null : (
              <>
                <Notice kind="info" title="Esta função estará disponível em uma próxima etapa.">
                  Os dados e as ações de {entry.title.toLowerCase()} ainda não estão disponíveis
                  neste aplicativo.
                </Notice>
                {mode === 'shift' &&
                !self.isFetching &&
                !self.isError &&
                (!linked ||
                  !self.data?.data.availableAccounts.some(
                    (account) => account.accountType === 'Terminal',
                  )) ? (
                  <Notice
                    kind="info"
                    title={
                      !linked
                        ? 'Seu login ainda não está vinculado a um operador.'
                        : 'Seu operador ainda não tem uma conta disponível.'
                    }
                  >
                    {role === 'Member' ? (
                      'Peça à administração da filial para conferir seu vínculo e as contas atribuídas.'
                    ) : (
                      <ButtonLink href={href('assignments') as never}>
                        Contas dos operadores
                      </ButtonLink>
                    )}
                  </Notice>
                ) : null}
              </>
            )}
          </main>
        </div>
      </div>
      <Dialog
        open={drawer}
        onDismiss={() => setDrawer(false)}
        title="Menu de navegação"
        variant="drawer"
      >
        <div data-on-nav className="bg-nav-bg text-nav-text-strong">
          {context(true)}
          {navigation}
        </div>
      </Dialog>
    </>
  );
}
