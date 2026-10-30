import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Linking } from 'react-native';
import { usePathname, useRouter, useGlobalSearchParams, type Href } from 'expo-router';
import { useOptionalSession, useSession } from '@/session/provider';
import { nativeDeviceStore, emailPartitionKey } from '@/storage/native';
import {
  destinations,
  defaultMode,
  destinationMode,
  homeKey,
  matchDestination,
  fallbackPath,
  type WorkMode,
} from './policy';
import {
  ShellNavigationStore,
  validateShellNavigation,
  navigationAddress,
  permittedPath,
  type ShellNavigation,
} from './navigation';

export function nativeHref(path: string, branchId?: string): Href {
  if (path === '/') return '/';
  const [pathname, query = ''] = path.split('?');
  const destination = matchDestination(pathname!);
  const identityPaths = [
    '/entrar',
    '/filiais',
    '/conta',
    '/conta/senha',
    '/plataforma',
    '/entrar/esqueci-senha',
    '/reset-password',
    '/invitation',
  ];
  if (!destination && !identityPaths.includes(pathname!))
    throw new Error('Invalid native destination');
  const params: Record<string, string> = Object.fromEntries(new URLSearchParams(query));
  if (destination && branchId) params.branchId = branchId;
  const parts = pathname!.split('/');
  const template = destination?.path ?? pathname!;
  template.split('/').forEach((part, index) => {
    if (part.startsWith(':')) params[part.slice(1)] = parts[index]!;
  });
  // One audited Router boundary: registry matching validates resource/calendar segments first.
  return { pathname: template.replace(/:(id|year|month)/g, '[$1]'), params } as Href;
}
function useShellState() {
  const { engine, snapshot, navigation } = useSession();
  const router = useRouter();
  const pathname = usePathname();
  const params = useGlobalSearchParams();
  const address = navigationAddress(pathname, params);
  const [mode, setMode] = useState<WorkMode>('management');
  const [origins, setOrigins] = useState<readonly string[]>([]);
  useEffect(() => {
    const listener = Linking.addEventListener('url', () => {
      engine.incomingNavigation();
      setOrigins([]);
    });
    return () => listener.remove();
  }, [engine]);
  const [ready, setReady] = useState(false);
  const [restored, setRestored] = useState<ShellNavigation | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [logoutNotice, setLogoutNotice] = useState<string | null>(null);
  const retained = useMemo(
    () => new ShellNavigationStore(nativeDeviceStore, emailPartitionKey),
    [],
  );
  const restoredOwner = useRef<string | null>(null);
  const owner =
    snapshot.user && snapshot.branch
      ? `${snapshot.user.userId}:${snapshot.user.email}:${snapshot.branch.id}:${snapshot.branch.role}`
      : null;
  const currentDestination = matchDestination(pathname);
  const requestedMode =
    snapshot.branch && currentDestination
      ? destinationMode(currentDestination, snapshot.branch.role, mode)
      : snapshot.branch?.role === 'Member'
        ? 'shift'
        : mode;
  const effectiveMode =
    requestedMode === 'shift' &&
    snapshot.branch?.role !== 'Member' &&
    snapshot.selfStatus === 'ready' &&
    !snapshot.selfContext?.operatorId
      ? 'management'
      : requestedMode;
  const live = useRef({ pathname: address, mode: effectiveMode, origins });
  useLayoutEffect(() => {
    live.current = { pathname: address, mode: effectiveMode, origins };
  }, [address, effectiveMode, origins]);
  useEffect(() => {
    const restoreKey = `${owner}:${snapshot.scope}`;
    if (restoredOwner.current === restoreKey) return;
    let alive = true;
    setReady(false);
    setOrigins([]);
    if (
      !owner ||
      !snapshot.branch ||
      !snapshot.user ||
      !['ready', 'failed'].includes(snapshot.selfStatus)
    )
      return;
    const user = snapshot.user,
      branch = snapshot.branch;
    const scope = snapshot.scope;
    void (async () => {
      const raw = await retained.read(user, branch.id);
      const candidate = await validateShellNavigation(
        raw,
        branch.role,
        Boolean(snapshot.selfContext?.operatorId),
      );
      if (!alive || engine.getSnapshot().scope !== scope) return;
      const accepted =
        candidate && (pathname === '/' || address === candidate.path) ? candidate : null;
      setRestored(accepted);
      const current = matchDestination(live.current.pathname.split('?')[0]!);
      const nextMode = current
        ? destinationMode(current, branch.role, accepted?.mode)
        : (accepted?.mode ?? defaultMode(branch.role));
      setMode(
        nextMode === 'shift' && branch.role !== 'Member' && !snapshot.selfContext?.operatorId
          ? 'management'
          : nextMode,
      );
      if (accepted) setOrigins(accepted.origins);
      restoredOwner.current = restoreKey;
      setReady(true);
    })().catch(() => {
      if (alive) {
        setMode(defaultMode(branch.role));
        setReady(true);
        setNotice('Não foi possível restaurar a navegação neste dispositivo.');
      }
    });
    return () => {
      alive = false;
    };
    // Reconcile once per authority/scope, never for every route or clock tick.
  }, [
    owner,
    snapshot.scope,
    snapshot.selfStatus,
    engine,
    retained,
    snapshot.user,
    snapshot.branch,
    snapshot.selfContext,
    pathname,
    address,
    router,
  ]);
  const navigate = useCallback(
    async (path: string, keepOrigin = false, replace = false) => {
      const current = engine.getSnapshot();
      if (
        !current.user ||
        !current.branch ||
        current.status !== 'branch' ||
        !current.foreground ||
        !current.online
      )
        return;
      const destination = matchDestination(path.split('?')[0]!);
      if (!destination || !permittedPath(path, current.branch.role)) {
        setNotice('Você não tem acesso a este destino.');
        return;
      }
      const selectedMode = destinationMode(destination, current.branch.role, live.current.mode);
      if (selectedMode === 'shift') {
        await engine.recheckSelfContext();
        if (engine.getSnapshot().selfStatus !== 'ready') return;
      }
      const fresh = engine.getSnapshot();
      if (fresh.scope !== current.scope || fresh.branch?.id !== current.branch.id) return;
      const nextMode =
        selectedMode === 'shift' &&
        current.branch.role !== 'Member' &&
        !fresh.selfContext?.operatorId
          ? 'management'
          : selectedMode;
      const nextOrigins = keepOrigin
        ? [...live.current.origins, live.current.pathname].slice(-8)
        : [];
      const record: ShellNavigation = { v: 1, path, mode: nextMode, origins: nextOrigins };
      try {
        await retained.write(current.user, current.branch.id, record);
      } catch {
        setNotice('Não foi possível guardar a navegação neste dispositivo.');
      }
      if (engine.getSnapshot().scope !== current.scope) return;
      setMode(nextMode);
      setOrigins(nextOrigins);
      if (replace || !keepOrigin) router.replace(nativeHref(path, current.branch.id));
      else router.push(nativeHref(path, current.branch.id));
    },
    [engine, retained, router],
  );
  const back = useCallback(async () => {
    const before = engine.getSnapshot();
    await engine.verifyEntry();
    const current = engine.getSnapshot();
    if (
      !current.branch ||
      !current.user ||
      current.status !== 'branch' ||
      current.scope !== before.scope ||
      current.branch.id !== before.branch?.id ||
      current.user.userId !== before.user?.userId
    )
      return;
    const destination = matchDestination(live.current.pathname.split('?')[0]!);
    const origin = live.current.origins.at(-1);
    const candidate = origin
      ? await validateShellNavigation(
          { v: 1, path: origin, mode: live.current.mode, origins: [] },
          current.branch.role,
          Boolean(current.selfContext?.operatorId),
        )
      : null;
    // A resource origin waits for its owning feature's fresh validator; no stored id proves access.
    let target =
      candidate?.path ??
      (destination
        ? fallbackPath(destination, live.current.mode)
        : destinations.find((d) => d.key === homeKey(defaultMode(current.branch!.role)))!.path);
    if (!permittedPath(target, current.branch.role))
      target = destinations.find((d) => d.key === homeKey(defaultMode(current.branch!.role)))!.path;
    const nextOrigins = candidate ? live.current.origins.slice(0, -1) : [];
    const nextMode = destinationMode(
      matchDestination(target.split('?')[0]!)!,
      current.branch.role,
      live.current.mode,
    );
    try {
      await retained.write(current.user, current.branch.id, {
        v: 1,
        path: target,
        mode: nextMode,
        origins: nextOrigins,
      });
    } catch {
      setNotice('Não foi possível guardar a navegação neste dispositivo.');
    }
    if (engine.getSnapshot().scope !== current.scope) return;
    setMode(nextMode);
    setOrigins(nextOrigins);
    if (candidate && router.canGoBack()) router.back();
    else router.replace(nativeHref(target, current.branch.id));
  }, [engine, router, retained]);

  return {
    mode: effectiveMode,
    origins,
    ready,
    restored,
    notice,
    setNotice,
    logoutNotice,
    setLogoutNotice,
    navigate,
    back,
    async switchMode(next: WorkMode) {
      await engine.refresh({ selfContext: true });
      const current = engine.getSnapshot();
      if (current.selfStatus !== 'ready' || !current.branch) return;
      if (next === 'management' && current.branch.role === 'Member') return;
      if (next === 'shift' && current.branch.role !== 'Member' && !current.selfContext?.operatorId)
        return;
      const path = destinations.find((d) => d.key === homeKey(next))!.path;
      await navigate(path, false, true);
    },
    async resetBranchNavigation(branchId: string) {
      const current = engine.getSnapshot();
      if (current.user) await retained.clear(current.user, branchId);
    },
    async switchBranch(preserveIntent = false) {
      const current = engine.getSnapshot();
      if (current.user && current.branch) await retained.clear(current.user, current.branch.id);
      if (!preserveIntent) await navigation.acknowledge();
      await engine.selectBranch(null);
      setOrigins([]);
      setReady(false);
      setMode('management');
      if (router.canDismiss()) router.dismissAll();
      router.replace(nativeHref('/filiais'));
    },
  };
}
type ShellState = ReturnType<typeof useShellState>;
const Context = createContext<ShellState | null>(null);
export function ShellProvider({ children }: { children: ReactNode }) {
  const session = useOptionalSession();
  return session ? <ConfiguredShellProvider>{children}</ConfiguredShellProvider> : children;
}
function ConfiguredShellProvider({ children }: { children: ReactNode }) {
  const value = useShellState();
  return <Context.Provider value={value}>{children}</Context.Provider>;
}
export function useShell() {
  const value = useContext(Context);
  if (!value) throw new Error('ShellProvider is not configured.');
  return value;
}
