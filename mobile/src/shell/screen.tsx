import {
  useCallback,
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
  type ReactNode,
} from 'react';
import { BackHandler, Keyboard, View } from 'react-native';
import {
  useFocusEffect,
  useIsFocused,
  useLocalSearchParams,
  usePathname,
  useRouter,
} from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { formatWallDateTime, roleLabels } from '@loto/core';
import { branchForm } from './branches';
import { navigationAddress } from './navigation';
import { loginForm } from './login';
import { sessionGate, platformGate } from '@/session/gate';
import { useOptionalSession, useSession, useScopedApiClient } from '@/session/provider';
import { useTheme } from '@/theme/provider';
import { isCompact, spacing, layer } from '@/theme/style';
import { AppText, Button, EmptyState, Notice, Overlay, PageHeader, Skeleton } from '@/ui';
import { ScreenScroll } from '@/ui/screen';
import { FocusRing, PressableBox, useFocusState } from '@/ui/focus';
import { BranchPicker, IdentitySheet, PasswordLogin } from './entry';
import { SessionMenu } from './menu';
import { validateTimeResource } from '@/time/resources';
import { TimeSurface, isTimeSurface } from '@/time/surface';
import {
  destinations,
  canEnter,
  defaultMode,
  homeKey,
  matchDestination,
  menuDestinations,
  fallbackPath,
} from './policy';
import { useShell, nativeHref } from './provider';

/* eslint-disable @typescript-eslint/no-require-imports */
const DevelopmentTools = __DEV__ ? require('../probes/entry-tools').DevelopmentTools : () => null;
/* eslint-enable @typescript-eslint/no-require-imports */

export function MobileScreen() {
  const optional = useOptionalSession();
  return optional ? <FocusedRoute /> : <DevelopmentTools />;
}
function FocusedRoute() {
  // usePathname is global, while local params belong to a particular native stack entry.
  // An outgoing/background entry must never interpret the new route with its old params.
  const focused = useIsFocused();
  return focused ? <GuardedScreen /> : null;
}
function GuardedScreen() {
  const { engine, snapshot, navigation } = useSession();
  const shell = useShell();
  const choiceForm = branchForm(engine);
  const choice = useSyncExternalStore(choiceForm.subscribe, choiceForm.get, choiceForm.get);
  const path = usePathname();
  const params = useLocalSearchParams<Record<string, string | string[]>>();
  const address = navigationAddress(path, params);
  const router = useRouter();
  const [verifiedEntry, setVerifiedEntry] = useState<string | null>(null);
  const [intentFailure, setIntentFailure] = useState(false);
  const held = useRef<string | null>(null);
  const entry = matchDestination(path);
  const capability = ['/reset-password', '/invitation'].includes(path);
  const anonymousAvailability = capability || path === '/entrar/esqueci-senha';
  const identityAvailability = ['/conta', '/conta/senha', '/plataforma'].includes(path);
  const picker = path === '/filiais';
  const identityRoute = identityAvailability || picker;
  const gate = sessionGate(snapshot, identityRoute ? 'identity' : 'branch');
  const needsSelfContext =
    entry?.owner === 'shift' || (entry?.owner === 'shared' && shell.mode === 'shift');
  // Presentation filters/focus do not change the authority or identity of a resource editor.
  const verificationKey = JSON.stringify([
    snapshot.scope,
    path,
    params.branchId ?? null,
    needsSelfContext,
  ]);
  const keepTimeView = Boolean(
    entry &&
    isTimeSurface(entry.key) &&
    canEnter(entry, snapshot.branch?.role ?? 'Member') &&
    verifiedEntry === verificationKey &&
    snapshot.user &&
    snapshot.branch &&
    snapshot.clock &&
    !['starting', 'anonymous', 'identity', 'closing'].includes(snapshot.status),
  );
  const pauseTimeView =
    keepTimeView &&
    (gate !== 'allow' || snapshot.selfStatus === 'checking' || snapshot.selfStatus === 'idle');
  useEffect(() => {
    if (
      !entry ||
      (gate === 'allow' && (!params.branchId || params.branchId === snapshot.branch?.id)) ||
      held.current === address
    )
      return;
    held.current = address;
    void navigation
      .hold(address, typeof params.branchId === 'string' ? params.branchId : null)
      .catch(() => setIntentFailure(true));
  }, [entry, gate, navigation, path, address, params.branchId, snapshot.branch?.id]);
  useEffect(() => {
    if (!snapshot.accessChanged || !snapshot.branch || snapshot.selfStatus !== 'ready' || !entry)
      return;
    const lostShift =
      (entry.owner === 'shift' || (entry.owner === 'shared' && shell.mode === 'shift')) &&
      snapshot.branch.role !== 'Member' &&
      !snapshot.selfContext?.operatorId;
    if (!canEnter(entry, snapshot.branch.role) || lostShift)
      router.replace(
        nativeHref(
          destinations.find((d) => d.key === homeKey(defaultMode(snapshot.branch!.role)))!.path,
          snapshot.branch!.id,
        ),
      );
  }, [
    snapshot.accessChanged,
    snapshot.branch,
    snapshot.selfStatus,
    snapshot.selfContext,
    entry,
    shell.mode,
    router,
  ]);
  useFocusEffect(
    useCallback(() => {
      let active = true;
      if (gate === 'allow' && (entry || identityRoute)) {
        void engine
          .verifyEntry({
            selfContext: needsSelfContext,
          })
          .then(() => {
            if (active) setVerifiedEntry(verificationKey);
          })
          .catch(() => {
            if (active) setIntentFailure(true);
          });
      }
      return () => {
        active = false;
      };
    }, [engine, verificationKey, entry, identityRoute, gate, needsSelfContext]),
  );
  useEffect(() => {
    if (gate !== 'allow' || !snapshot.branch || !shell.ready || !['/', '/entrar'].includes(path))
      return;
    let active = true;
    const scope = snapshot.scope;
    void navigation
      .resolve(async (candidate) => {
        const target = matchDestination(candidate.split('?')[0]!);
        // Resource restoration waits for its feature-owned live resource validator.
        return (
          !!target &&
          canEnter(target, snapshot.branch!.role) &&
          (!target.path.includes(':id') || (await validateTimeResource(engine, candidate)))
        );
      })
      .then(async (intent) => {
        if (!active || engine.getSnapshot().scope !== scope) return;
        if (intent) {
          await navigation.acknowledge();
          await shell.navigate(intent.destination, false, true);
        } else
          router.replace(
            nativeHref(
              shell.restored?.path ??
                destinations.find((d) => d.key === homeKey(defaultMode(snapshot.branch!.role)))!
                  .path,
              snapshot.branch!.id,
            ),
          );
      })
      .catch(() => {
        if (active) setIntentFailure(true);
      });
    return () => {
      active = false;
    };
  }, [gate, snapshot.branch, snapshot.scope, shell.ready, path, navigation, engine, router, shell]);
  if (anonymousAvailability)
    return (
      <IdentitySheet
        title={
          path === '/invitation'
            ? 'Convite'
            : path === '/reset-password'
              ? 'Redefinir senha'
              : 'Recuperar acesso'
        }
      >
        <Availability title="conta e acesso" />
        <Button label="Voltar para Entrar" onPress={() => router.replace('/')} />
      </IdentitySheet>
    );
  if (gate === 'login') return <PasswordLogin />;
  if (choice.pending !== null && snapshot.user) return <BranchPicker menu={null} />;
  if (gate === 'branch') return <BranchPicker menu={<SessionMenu />} />;
  if (snapshot.status === 'starting' && loginForm(engine).get().pending) return <PasswordLogin />;
  if ((gate === 'loading' || snapshot.status === 'starting') && !keepTimeView)
    return (
      <IdentitySheet title={snapshot.status === 'closing' ? 'Saindo…' : 'Verificando sessão…'}>
        <Skeleton />
      </IdentitySheet>
    );
  if (gate === 'recovery' && !keepTimeView)
    return (
      <IdentitySheet title="Verifique sua sessão">
        <Notice
          kind="warning"
          title={snapshot.recovery ?? 'Sua sessão precisa ser verificada.'}
          messages={snapshot.messages}
          body={!snapshot.online ? 'Sem conexão. Confira sua rede para continuar.' : undefined}
          actions={<Button label="Verificar sessão" onPress={() => void engine.recover()} />}
        />
        <SessionMenu />
      </IdentitySheet>
    );
  if ((entry || identityRoute) && gate === 'allow' && verifiedEntry !== verificationKey)
    return (
      <IdentitySheet title="Verificando destino…">
        <Skeleton />
      </IdentitySheet>
    );
  if (picker) return <BranchPicker menu={<SessionMenu />} />;
  if (identityAvailability) {
    if (path === '/plataforma' && platformGate(snapshot) !== 'allow')
      return (
        <IdentitySheet title="Acesso não permitido">
          <Notice kind="danger" title="Sua conta não tem acesso à Plataforma." />
          <Button label="Voltar" onPress={() => router.replace('/')} />
        </IdentitySheet>
      );
    return (
      <IdentitySheet
        title={
          path === '/conta'
            ? 'Minha conta'
            : path === '/conta/senha'
              ? 'Alterar senha'
              : 'Plataforma'
        }
      >
        <Availability title="conta e acesso" />
        <Button
          label="Voltar"
          onPress={() => (router.canGoBack() ? router.back() : router.replace('/'))}
        />
        <SessionMenu />
      </IdentitySheet>
    );
  }
  if (!snapshot.branch || !snapshot.clock)
    return (
      <IdentitySheet title="Escolha a filial">
        <Skeleton />
      </IdentitySheet>
    );
  if (params.branchId && params.branchId !== snapshot.branch.id)
    return (
      <IdentitySheet title="Filial diferente">
        <Notice
          kind="warning"
          title="Este destino pertence a outra filial."
          body="Escolha a filial do destino antes de continuar."
        />
        <Button label="Escolher filial" onPress={() => void shell.switchBranch(true)} />
      </IdentitySheet>
    );
  if (!entry && path !== '/')
    return (
      <BranchFrame title="Página não encontrada">
        <Notice kind="info" title="Este destino não existe." />
        <Button label="Voltar ao início" onPress={() => router.replace('/')} />
      </BranchFrame>
    );
  if (entry && !canEnter(entry, snapshot.branch.role))
    return (
      <BranchFrame title="Acesso não permitido">
        <Notice kind="danger" title="Seu perfil não tem acesso a este destino." />
        <Button label="Voltar ao início" onPress={() => router.replace('/')} />
      </BranchFrame>
    );
  if (!entry || !shell.ready)
    return (
      <IdentitySheet title="Abrindo…">
        <Skeleton />
      </IdentitySheet>
    );
  const ownShift = entry.owner === 'shift' || (entry.owner === 'shared' && shell.mode === 'shift');
  const header = (
    <PageHeader
      key="screen-header"
      title={entry.title}
      focusOnMount
      context={`${snapshot.branch.name} · ${roleLabels[snapshot.branch.role]}`}
      origin={
        entry.key !== homeKey(shell.mode)
          ? {
              label:
                matchDestination(
                  (shell.origins.at(-1) ?? fallbackPath(entry, shell.mode)).split('?')[0]!,
                )?.title ?? 'início',
              onPress: shell.back,
            }
          : undefined
      }
    />
  );
  if (
    ownShift &&
    !keepTimeView &&
    (snapshot.selfStatus === 'checking' || snapshot.selfStatus === 'idle')
  )
    return (
      <BranchFrame title={entry.title}>
        {header}
        <Skeleton label="Verificando seu vínculo…" />
      </BranchFrame>
    );
  const linked = Boolean(snapshot.selfContext?.operatorId);
  return (
    <BranchFrame title={entry.title} sessionPaused={pauseTimeView}>
      {intentFailure ? (
        <Notice
          kind="warning"
          title="Não foi possível guardar ou restaurar o destino solicitado."
        />
      ) : null}
      {header}
      {ownShift && snapshot.selfStatus === 'failed' ? (
        <Notice
          kind="warning"
          title="Não foi possível verificar seu vínculo nesta filial."
          messages={snapshot.selfMessages}
          actions={
            <Button label="Verificar novamente" onPress={() => void engine.recheckSelfContext()} />
          }
        />
      ) : ownShift &&
        !linked &&
        !(
          snapshot.branch.role === 'Member' && ['my-transactions', 'my-hours'].includes(entry.key)
        ) ? (
        <>
          <EmptyState
            kind="setup-needed"
            statement="Seu login ainda não está vinculado a um operador."
            explanation={
              snapshot.branch.role === 'Member'
                ? 'Peça à administração da filial para conferir seu vínculo e as contas atribuídas.'
                : 'Para usar Meu turno, seu login precisa estar vinculado a um operador ativo desta filial. Seu perfil e os acessos de Gestão permanecem disponíveis. Vincule seu login em Operadores e atribua uma conta em Contas dos operadores.'
            }
          />
          {snapshot.branch.role !== 'Member' ? (
            <>
              <Button
                label="Ir para Operadores"
                onPress={() => void shell.navigate('/gestao/operadores')}
              />
              <Button
                label="Contas dos operadores"
                variant="secondary"
                onPress={() => void shell.navigate('/gestao/contas-dos-operadores')}
              />
              <Button
                label="Voltar para a fila de trabalho"
                variant="link"
                onPress={() => void shell.navigate('/gestao/fila')}
              />
            </>
          ) : null}
        </>
      ) : (
        <>
          {ownShift &&
          !snapshot.selfContext?.availableAccounts.some((a) => a.accountType === 'Terminal') ? (
            <Notice
              kind="info"
              title="Seu operador ainda não tem uma conta disponível."
              body="Peça à administração da filial para conferir as contas atribuídas."
            />
          ) : null}
          <>
            {isTimeSurface(entry.key) ? (
              <TimeSurface
                key={entry.key === 'time-detail' ? path : address}
                destination={entry.key}
                preserveVerifiedView={keepTimeView}
              />
            ) : (
              <Availability title={entry.title.toLowerCase()} />
            )}
          </>
        </>
      )}
    </BranchFrame>
  );
}
export function Availability({ title }: { title: string }) {
  return (
    <Notice
      kind="info"
      title="Esta função estará disponível em uma próxima etapa."
      body={`Os dados e as ações de ${title} ainda não estão disponíveis neste aplicativo.`}
    />
  );
}
function BranchFrame({
  title,
  children,
  sessionPaused = false,
}: {
  title: string;
  children: ReactNode;
  sessionPaused?: boolean;
}) {
  const { snapshot, engine, store } = useSession();
  const shell = useShell();
  const { colors, width, fontScale } = useTheme();
  const stackedHeader = isCompact(width, fontScale);
  const client = useScopedApiClient();
  const branches = useGetBranchMyBranches({
    request: { client },
    query: {
      enabled: snapshot.status === 'branch' && snapshot.online && snapshot.foreground,
      retry: false,
    },
  });
  const [drawer, setDrawer] = useState(false);
  const [more, setMore] = useState(false);
  const [retained, setRetained] = useState<boolean | null>(false);
  const trigger = useRef<View>(null);
  const path = usePathname();
  useEffect(() => {
    let active = true;
    void store
      .inspectOwnWork()
      .then((review) => {
        if (active) setRetained(review.drafts + review.operations > 0);
      })
      .catch(() => {
        if (active) setRetained(null);
      });
    return () => {
      active = false;
    };
  }, [store, path, snapshot.scope]);
  useFocusEffect(
    useCallback(() => {
      const sub = BackHandler.addEventListener('hardwareBackPress', () => {
        if (sessionPaused) {
          Keyboard.dismiss();
          return true;
        }
        if (Keyboard.isVisible()) {
          Keyboard.dismiss();
          return true;
        }
        if (drawer) {
          setDrawer(false);
          return true;
        }
        const entry = matchDestination(path);
        if (entry && entry.key !== homeKey(shell.mode)) {
          shell.back();
          return true;
        }
        return false;
      });
      return () => sub.remove();
    }, [path, drawer, shell, sessionPaused]),
  );
  const alerts = [
    ...(snapshot.recovery || snapshot.selfStatus === 'failed'
      ? [
          {
            key: 'session',
            title: snapshot.recovery ?? 'Não foi possível verificar seu vínculo nesta filial.',
            action: <Button label="Verificar sessão" onPress={() => void engine.recover()} />,
          },
        ]
      : []),
    ...(snapshot.dayChanged
      ? [
          {
            key: 'day',
            title: 'A data da filial mudou. Confira os dados antes de continuar.',
            action: <Button label="Entendi" onPress={() => engine.acknowledgeDayChange()} />,
          },
        ]
      : []),
    ...(retained !== false
      ? [
          {
            key: 'retained',
            title:
              retained === null
                ? 'Não foi possível conferir as alterações guardadas neste dispositivo.'
                : 'Há alterações ou operações guardadas neste dispositivo — ainda não confirmadas no servidor.',
            action: undefined,
          },
        ]
      : []),
  ];
  const role = snapshot.branch!.role;
  const mode = shell.mode;
  const context = (
    <View style={{ gap: spacing.stack.sm }}>
      <AppText variant="body-strong">{snapshot.branch!.name}</AppText>
      <AppText>
        {roleLabels[role]} · {mode === 'management' ? 'Gestão' : 'Meu turno'}
      </AppText>
      <AppText variant="helper">{snapshot.user?.name}</AppText>
      <AppText variant="helper">
        {snapshot.clock ? formatWallDateTime(snapshot.clock.branchLocalDateTime) : '—'} · horário da
        filial
      </AppText>
    </View>
  );
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: colors.sem.background }}>
      <View
        style={{
          gap: spacing.inline.sm,
          paddingHorizontal: spacing.page['inline-compact'],
          backgroundColor: colors.sem.topbar.bg,
          borderBottomWidth: spacing.size['border-width'],
          borderBottomColor: colors.sem.topbar.border,
        }}
      >
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.inline.sm }}>
          <Button
            ref={trigger}
            label="Menu"
            testID="navigation-menu"
            variant="secondary"
            onPress={() => setDrawer(true)}
          />
          {stackedHeader ? (
            <View style={{ flex: 1 }} />
          ) : (
            <AppText variant="label" style={{ flex: 1 }}>
              {title}
            </AppText>
          )}
          <SessionMenu suspended={sessionPaused} />
        </View>
        {stackedHeader ? <AppText variant="label">{title}</AppText> : null}
      </View>
      <ScreenScroll
        testID="branch-content"
        style={{ opacity: sessionPaused ? 0 : 1 }}
        pointerEvents={sessionPaused ? 'none' : 'auto'}
        accessibilityElementsHidden={sessionPaused}
        importantForAccessibility={sessionPaused ? 'no-hide-descendants' : 'auto'}
        contentContainerStyle={{ padding: spacing.page['inline-compact'], gap: spacing.stack.md }}
      >
        {(more ? alerts : alerts.slice(0, 2)).map((alert) => (
          <Notice
            key={alert.key}
            kind="warning"
            blocking={false}
            title={alert.title}
            actions={alert.action}
          />
        ))}
        {alerts.length > 2 ? (
          <Button
            label={more ? 'Menos avisos' : `+${alerts.length - 2} avisos`}
            variant="link"
            onPress={() => setMore(!more)}
          />
        ) : null}
        {snapshot.accessChanged ? (
          <Notice
            kind="info"
            title="Seu acesso nesta filial mudou. Confira os dados antes de continuar."
            actions={
              <Button
                label="Entendi a mudança de acesso"
                onPress={() => engine.acknowledgeAccessChange()}
              />
            }
          />
        ) : null}
        {shell.notice ? (
          <Notice kind="info" title={shell.notice} onDismiss={() => shell.setNotice(null)} />
        ) : null}
        {children}
      </ScreenScroll>
      {sessionPaused ? (
        <View
          testID="time-session-pause"
          accessibilityViewIsModal
          style={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            left: 0,
            right: 0,
            zIndex: layer.dialog,
            backgroundColor: colors.sem.background,
            padding: spacing.inset.panel,
            gap: spacing.stack.md,
            justifyContent: 'center',
          }}
        >
          <AppText variant="section-title">
            {snapshot.status === 'recovering' ? 'Verificando sessão…' : 'Verifique sua sessão'}
          </AppText>
          {snapshot.status === 'recovering' || snapshot.selfStatus === 'checking' ? (
            <Skeleton label="Verificando seu acesso…" />
          ) : (
            <Notice
              kind="warning"
              title={snapshot.recovery ?? 'Sua sessão precisa ser verificada.'}
              messages={snapshot.messages}
              actions={<Button label="Verificar sessão" onPress={() => void engine.recover()} />}
            />
          )}
          <SessionMenu />
        </View>
      ) : null}
      <Overlay
        visible={drawer && !sessionPaused}
        title="Menu"
        variant="drawer"
        returnFocus={trigger}
        onDismiss={() => setDrawer(false)}
      >
        {context}
        {(branches.data?.data.branches.length ?? 0) > 1 ? (
          <Button
            label="Trocar filial"
            onPress={() => {
              setDrawer(false);
              void shell.switchBranch();
            }}
          />
        ) : null}
        <View
          style={{
            backgroundColor: colors.sem.nav.bg,
            padding: spacing.inset.panel,
            gap: spacing.stack.sm,
          }}
        >
          {menuDestinations(role, mode).map((destination, index, items) => {
            const group =
              mode === 'shift' && destination.owner === 'shared' ? 'Meu turno' : destination.group;
            const previous = items[index - 1];
            const previousGroup =
              previous && mode === 'shift' && previous.owner === 'shared'
                ? 'Meu turno'
                : previous?.group;
            const resolved = destination.path
              .replace(':year', snapshot.clock!.branchLocalDate.slice(0, 4))
              .replace(':month', String(Number(snapshot.clock!.branchLocalDate.slice(5, 7))));
            return (
              <View key={destination.key}>
                {group !== previousGroup ? (
                  <AppText
                    variant="overline"
                    color={colors.sem.nav['text-muted']}
                    accessibilityRole="header"
                  >
                    {group}
                  </AppText>
                ) : null}
                <NavigationRow
                  title={destination.title}
                  selected={resolved === path}
                  onPress={() => {
                    setDrawer(false);
                    void shell.navigate(resolved);
                  }}
                />
              </View>
            );
          })}
        </View>
        <SessionMenu suspended={sessionPaused} />
      </Overlay>
    </SafeAreaView>
  );
}

function NavigationRow({
  title,
  selected,
  onPress,
}: {
  title: string;
  selected: boolean;
  onPress: () => void;
}) {
  const { colors } = useTheme();
  const [focused, handlers] = useFocusState();
  return (
    <PressableBox
      accessibilityRole="link"
      accessibilityLabel={title}
      accessibilityState={{ selected }}
      onPress={onPress}
      {...handlers}
      style={(pressed) => ({
        minHeight: spacing.size['touch-target-comfortable'],
        padding: spacing.inline.md,
        justifyContent: 'center',
        borderLeftWidth: spacing.size['marker-width'],
        borderLeftColor: selected ? colors.sem.nav.marker : colors.sem.nav.bg,
        backgroundColor: selected
          ? colors.sem.nav.active
          : pressed
            ? colors.sem.nav.hover
            : colors.sem.nav.bg,
      })}
    >
      <AppText color={colors.sem.nav['text-strong']}>{title}</AppText>
      <FocusRing visible={focused} onNavigation />
    </PressableBox>
  );
}
