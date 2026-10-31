import { useRef, useState } from 'react';
import { View } from 'react-native';
import { useRouter } from 'expo-router';
import { useGetBranchMyBranches } from '@loto/api/queries';
import { roleLabels } from '@loto/core';
import { useSession, useScopedApiClient } from '@/session/provider';
import { AppText, Button, ConfirmDialog, Notice, Overlay, SegmentedControl } from '@/ui';
import { ThemeControl } from './entry';
import { useShell, nativeHref } from './provider';

/* eslint-disable @typescript-eslint/no-require-imports */
const DevelopmentTools = __DEV__ ? require('../probes/entry-tools').DevelopmentTools : () => null;
/* eslint-enable @typescript-eslint/no-require-imports */

export function SessionMenu({ suspended = false }: { suspended?: boolean } = {}) {
  const { engine, snapshot, store } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const trigger = useRef<View>(null);
  const command = useRef(false);
  const branches = useGetBranchMyBranches({
    request: { client },
    query: { enabled: open && Boolean(snapshot.user), retry: false },
  });
  const linked = Boolean(snapshot.selfContext?.operatorId);
  const elevated = snapshot.branch && snapshot.branch.role !== 'Member';
  const modeLabel = shell.mode === 'management' ? 'Gestão' : 'Meu turno';
  async function logout() {
    if (command.current) return;
    command.current = true;
    setBusy(true);
    try {
      const result = await engine.logout();
      shell.setLogoutNotice(
        !result.localCleared
          ? 'Não foi possível concluir a limpeza neste dispositivo. Tente sair novamente.'
          : !result.serverConfirmed
            ? 'Você saiu deste dispositivo. Não foi possível confirmar o encerramento no servidor.'
            : null,
      );
      if (result.localCleared) {
        setConfirm(false);
        setOpen(false);
        if (router.canDismiss()) router.dismissAll();
        router.replace('/');
      }
    } catch {
      setFailure('Não foi possível concluir a saída. Tente novamente.');
    } finally {
      command.current = false;
      setBusy(false);
    }
  }
  async function requestLogout() {
    if (command.current) return;
    try {
      const review = await store.inspectOwnWork();
      if (review.drafts || review.operations) setConfirm(true);
      else await logout();
    } catch {
      setFailure(
        'Não foi possível conferir o trabalho guardado. Mantenha esta tela aberta e tente novamente.',
      );
    }
  }
  function account(path: string) {
    setOpen(false);
    router.push(nativeHref(path));
  }
  return (
    <>
      <Button
        ref={trigger}
        label="Conta"
        testID="session-menu"
        disabled={suspended}
        accessibilityLabel={`Conta de ${snapshot.user?.name ?? ''}${snapshot.branch ? `, ${modeLabel}` : ''}`}
        variant="secondary"
        onPress={() => {
          setOpen(true);
          if (snapshot.branch) void engine.refresh({ selfContext: true, background: true });
        }}
      />
      <Overlay
        visible={open && !suspended}
        title="Sua conta"
        variant="bottom-sheet"
        returnFocus={trigger}
        busy={busy}
        onDismiss={() => setOpen(false)}
      >
        <AppText variant="body-strong">{snapshot.user?.name}</AppText>
        <AppText variant="helper">{snapshot.user?.email}</AppText>
        {snapshot.branch ? (
          <AppText>
            {snapshot.branch.name} · {roleLabels[snapshot.branch.role]} · {modeLabel}
          </AppText>
        ) : null}
        {elevated ? (
          <>
            <AppText variant="label">Modo de trabalho</AppText>
            <SegmentedControl
              accessibilityLabel="Modo de trabalho"
              value={shell.mode}
              options={[
                { value: 'management', label: 'Gestão' },
                {
                  value: 'shift',
                  label: 'Meu turno',
                  disabled: !linked || snapshot.selfStatus !== 'ready',
                  disabledReason:
                    snapshot.selfStatus === 'failed'
                      ? 'Não foi possível verificar seu vínculo.'
                      : 'Seu login precisa estar vinculado a um operador ativo desta filial.',
                },
              ]}
              disabled={snapshot.selfStatus === 'checking'}
              onValueChange={(mode) => {
                if (mode === 'shift' && (!linked || snapshot.selfStatus !== 'ready')) return;
                setOpen(false);
                void shell.switchMode(mode);
              }}
            />
            {snapshot.selfStatus === 'checking' ? (
              <AppText>Verificando…</AppText>
            ) : snapshot.selfStatus === 'failed' ? (
              <Notice
                kind="warning"
                title="Não foi possível verificar seu vínculo."
                messages={snapshot.selfMessages}
                actions={
                  <Button
                    label="Verificar novamente"
                    onPress={() => void engine.recheckSelfContext()}
                  />
                }
              />
            ) : !linked ? (
              <>
                <AppText>
                  Indisponível: seu login não está vinculado a um operador desta filial.
                </AppText>
                <Button
                  label="Resolver em Operadores"
                  variant="link"
                  onPress={() => {
                    setOpen(false);
                    void shell.navigate('/gestao/operadores');
                  }}
                />
              </>
            ) : null}
          </>
        ) : null}
        {(branches.data?.data.branches.length ?? 0) > 1 && snapshot.branch ? (
          <Button
            testID="switch-branch"
            label="Trocar filial"
            variant="secondary"
            onPress={() => {
              setOpen(false);
              void shell
                .switchBranch()
                .catch(() =>
                  shell.setNotice(
                    'Não foi possível trocar a filial. Confira sua conexão e tente novamente.',
                  ),
                );
            }}
          />
        ) : null}
        <Button label="Minha conta" variant="link" role="link" onPress={() => account('/conta')} />
        {snapshot.user?.hasPassword ? (
          <Button
            label="Alterar senha"
            variant="link"
            role="link"
            onPress={() => account('/conta/senha')}
          />
        ) : null}
        {snapshot.user?.isPlatformOperator ? (
          <Button
            label="Plataforma"
            variant="link"
            role="link"
            onPress={() => account('/plataforma')}
          />
        ) : null}
        <ThemeControl />
        {failure ? <Notice kind="danger" title={failure} /> : null}
        <Button
          testID="logout"
          label="Sair"
          loading={busy}
          loadingLabel="Saindo…"
          variant="danger"
          onPress={() => void requestLogout()}
        />
        <DevelopmentTools />
        <ConfirmDialog
          visible={confirm}
          title="Sair da conta?"
          consequence="Há alterações ou operações não confirmadas guardadas neste dispositivo. Elas continuam isoladas por conta e filial. Sair não confirma nem cancela operações no servidor."
          confirmLabel="Sair e manter os dados"
          busyLabel="Saindo…"
          busy={busy}
          onConfirm={() => void logout()}
          onDismiss={() => setConfirm(false)}
        />
      </Overlay>
    </>
  );
}
