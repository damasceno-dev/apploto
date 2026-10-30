import { useEffect, useRef, useSyncExternalStore, type ReactNode } from 'react';
import { Keyboard, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { formatCnpj, roleLabels } from '@loto/core';
import { useScopedApiClient, useSession } from '@/session/provider';
import { actionFailure, type ActionFailure } from '@/data/failures';
import { useTheme } from '@/theme/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Button,
  Card,
  EmptyState,
  FormField,
  Notice,
  SegmentedControl,
  Skeleton,
  TextField,
  type InputHandle,
} from '@/ui';
import { ScreenScroll } from '@/ui/screen';
import { PressableBox } from '@/ui/focus';
import { branchForm } from './branches';
import { loginForm } from './login';
import { nativeHref, useShell } from './provider';

/* eslint-disable @typescript-eslint/no-require-imports */
const DevelopmentTools = __DEV__ ? require('../probes/entry-tools').DevelopmentTools : () => null;
/* eslint-enable @typescript-eslint/no-require-imports */

export function ThemeControl() {
  const theme = useTheme();
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <SegmentedControl
        accessibilityLabel="Tema"
        options={[
          { value: 'light', label: 'Claro' },
          { value: 'dark', label: 'Escuro' },
          { value: 'system', label: 'Sistema' },
        ]}
        value={theme.preference}
        onValueChange={(value) => void theme.setPreference(value)}
      />
      {theme.persistFailed ? (
        <AppText variant="helper">
          Não foi possível guardar o tema. A escolha vale nesta sessão.
        </AppText>
      ) : null}
    </View>
  );
}
export function IdentitySheet({ title, children }: { title: string; children: ReactNode }) {
  const { colors } = useTheme();
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: colors.sem.background }}>
      <ScreenScroll
        contentContainerStyle={{ padding: spacing.page['inline-compact'], gap: spacing.stack.lg }}
      >
        <View
          style={{
            backgroundColor: colors.sem.nav.bg,
            padding: spacing.inset.panel,
            gap: spacing.stack.sm,
          }}
        >
          <AppText variant="section-title" color={colors.sem.nav['text-strong']}>
            Lotero
          </AppText>
        </View>
        <ThemeControl />
        <Card variant="sheet" title={title}>
          {children}
        </Card>
      </ScreenScroll>
    </SafeAreaView>
  );
}
export function FailureNotice({ failure, title }: { failure: ActionFailure; title: string }) {
  return (
    <Notice
      kind="danger"
      title={failure.kind === 'uncertain' ? 'Resultado não confirmado' : title}
      messages={failure.messages}
      body={
        [
          failure.recovery,
          failure.retryAfter
            ? `O servidor sugere aguardar ${failure.retryAfter} antes de tentar novamente.`
            : '',
        ]
          .filter(Boolean)
          .join(' ') || undefined
      }
    />
  );
}
export function PasswordLogin() {
  const { engine, snapshot } = useSession();
  const shell = useShell();
  const router = useRouter();
  const form = loginForm(engine);
  const { email, password, pending, failure } = useSyncExternalStore(
    form.subscribe,
    form.get,
    form.get,
  );
  useEffect(() => () => form.clearPassword(), [form]);
  const pass = useRef<InputHandle>(null);
  const emailField = useRef<InputHandle>(null);
  useEffect(() => {
    if (failure) pass.current?.focus();
  }, [failure]);
  function submit() {
    if (!email.trim() || !password) {
      (email.trim() ? pass : emailField).current?.focus();
      return;
    }
    Keyboard.dismiss();
    void form.submit();
  }
  return (
    <IdentitySheet title="Entrar">
      {snapshot.recovery && snapshot.status === 'anonymous' ? (
        <Notice kind="info" title={snapshot.recovery} />
      ) : null}
      {shell.logoutNotice ? (
        <Notice
          kind="info"
          title={shell.logoutNotice}
          onDismiss={() => shell.setLogoutNotice(null)}
        />
      ) : null}
      {failure ? <FailureNotice failure={failure} title="Não foi possível entrar" /> : null}
      {!failure && snapshot.messages.length ? (
        <Notice kind="danger" title="Não foi possível entrar" messages={snapshot.messages} />
      ) : null}
      <FormField label="E-mail" required disabled={pending}>
        <TextField
          ref={emailField}
          testID="login-email"
          kind="email"
          autoComplete="username"
          value={email}
          onChangeText={form.email}
          returnKeyType="next"
          onSubmitEditing={() => pass.current?.focus()}
        />
      </FormField>
      <FormField label="Senha" required disabled={pending}>
        <TextField
          ref={pass}
          testID="login-password"
          kind="password"
          value={password}
          onChangeText={form.password}
          returnKeyType="go"
          onSubmitEditing={() => void submit()}
        />
      </FormField>
      <Button
        testID="login-submit"
        label="Entrar"
        loading={pending}
        loadingLabel="Entrando…"
        fullWidth
        onPress={() => void submit()}
      />
      <Button
        label="Esqueci minha senha"
        variant="link"
        role="link"
        disabled={pending}
        onPress={() => router.push(nativeHref('/entrar/esqueci-senha'))}
      />
      <Notice
        kind="info"
        title="Recuperação de acesso e Google"
        body="Essas opções estarão disponíveis na etapa de conta e acesso. Primeiro acesso? Abra o link do convite recebido por e-mail."
      />
      <DevelopmentTools />
    </IdentitySheet>
  );
}
// A component remount or strict-effect replay cannot repeat a single-membership command.
export function BranchPicker({ menu }: { menu: ReactNode }) {
  const { engine, snapshot, navigation } = useSession();
  const client = useScopedApiClient();
  const { colors } = useTheme();
  const router = useRouter();
  const shell = useShell();
  const query = useGetBranchMyBranches({
    request: { client },
    query: {
      enabled: ['identity', 'branch'].includes(snapshot.status),
      retry: false,
      staleTime: 0,
    },
  });
  const form = branchForm(engine);
  const choice = useSyncExternalStore(form.subscribe, form.get, form.get);
  const { pending, failure } = choice;
  const loaded = query.data?.data.branches;
  const branches = loaded ?? (pending !== null ? choice.branches : null);
  useEffect(() => {
    if (loaded) form.list(loaded);
  }, [form, loaded]);
  async function choose(id: string, explicit = false) {
    await form.choose(id, async () => {
      if (explicit) {
        await shell.resetBranchNavigation(id);
        await navigation.discardForeignBranch(id);
      }
      try {
        await engine.selectBranch(id);
        if (engine.getSnapshot().status === 'branch') router.replace('/');
      } catch (error) {
        void query.refetch();
        throw error;
      }
    });
  }
  useEffect(() => {
    if (
      branches?.length !== 1 ||
      snapshot.status !== 'identity' ||
      !snapshot.user ||
      pending !== null
    )
      return;
    const id = branches[0]!.id;
    if (form.attempted.has(id)) return;
    form.attempted.add(id);
    void Promise.resolve().then(() => choose(id));
  });
  const problem = failure ?? (query.isError ? actionFailure(query.error) : null);
  return (
    <IdentitySheet title="Escolha a filial">
      {menu}
      {!problem && snapshot.messages.length ? (
        <Notice
          kind="danger"
          title="Não foi possível abrir a filial"
          messages={snapshot.messages}
          body={snapshot.recovery ?? undefined}
          actions={<Button label="Atualizar filiais" onPress={() => void query.refetch()} />}
        />
      ) : null}
      {problem ? (
        <>
          <FailureNotice failure={problem} title="Não foi possível abrir a filial" />
          <Button label="Atualizar filiais" onPress={() => void query.refetch()} />
          {branches?.length === 1 ? (
            <Button
              label="Tentar abrir novamente"
              loading={pending !== null}
              loadingLabel="Abrindo…"
              onPress={() => void choose(branches[0]!.id, true)}
            />
          ) : null}
        </>
      ) : null}
      {query.isPending && !branches ? <Skeleton label="Carregando filiais…" /> : null}
      {branches?.length === 0 ? (
        <EmptyState
          kind="setup-needed"
          statement="Você ainda não tem acesso a uma lotérica."
          explanation="Abra o link do convite recebido por e-mail. Se ainda não recebeu um convite, peça à administração da lotérica acesso para sua conta."
        />
      ) : null}
      {branches?.map((branch) => (
        <PressableBox
          key={branch.id}
          testID={`branch-${branch.id}`}
          accessibilityRole="button"
          accessibilityLabel={`${branch.name} — ${roleLabels[branch.role]}${branch.address ? `, ${branch.address}` : ''}${branch.cnpj ? `, ${formatCnpj(branch.cnpj)}` : ''}`}
          accessibilityState={{ disabled: pending !== null, busy: pending === branch.id }}
          onPress={() => {
            if (pending === null) void choose(branch.id, true);
          }}
          style={(pressed) => ({
            minHeight: spacing.size['touch-target-comfortable'],
            padding: spacing.inset.panel,
            gap: spacing.stack.xs,
            borderWidth: spacing.size['border-width'],
            borderColor: colors.sem.border.control,
            backgroundColor: pressed ? colors.sem.surface.hover : colors.sem.surface.default,
          })}
        >
          <AppText variant="body-strong">{branch.name}</AppText>
          {branch.address ? <AppText variant="helper">{branch.address}</AppText> : null}
          {branch.cnpj ? <AppText variant="helper">{formatCnpj(branch.cnpj)}</AppText> : null}
          <AppText variant="helper">
            {pending === branch.id ? 'Abrindo…' : roleLabels[branch.role]}
          </AppText>
        </PressableBox>
      ))}
      <AppText>{snapshot.user?.name}</AppText>
      <AppText variant="helper">{snapshot.user?.email}</AppText>
    </IdentitySheet>
  );
}
