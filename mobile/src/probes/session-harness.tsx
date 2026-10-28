import { useEffect, useRef, useState } from 'react';
import { Button, Text, TextInput, View } from 'react-native';
import {
  getBranchCurrent,
  getOperatorSelfContext,
  isApiError,
  type RequestCreateTransactionJson,
} from '@loto/api';
import { roleLabels, enumLabel } from '@loto/core';
import { useOptionalSession, useSession } from '../session/provider';
import { FinancialOperation, type OperationOutcome } from '../data/operations';
import { secureUuid } from '../storage/native';
import type { RetainedDataReview } from '../storage/scoped';
import { loseNextFixtureResponse } from './session-transport';
import { readNativeFixture } from './native-fixture';

export function SessionHarness() {
  const available = useOptionalSession();
  if (!__DEV__ || process.env.EXPO_PUBLIC_NATIVE_SESSION_PROOF !== '1') return null;
  return available ? <ReadyHarness /> : <Text>Teste de sessão: configure a API local.</Text>;
}
function ReadyHarness() {
  const { engine, store, drafts, navigation, snapshot, discardRetainedData } = useSession();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [branch, setBranch] = useState('');
  const [notes, setNotes] = useState('');
  const [payload, setPayload] = useState('');
  const [destination, setDestination] = useState('/transacoes');
  const [feedback, setFeedback] = useState('');
  const [errors, setErrors] = useState<readonly string[]>([]);
  const [busy, setBusy] = useState(false);
  const [outcome, setOutcome] = useState<OperationOutcome | null>(null);
  const [resumed, setResumed] = useState('');
  const [grants, setGrants] = useState('');
  const [fixtureLoaded, setFixtureLoaded] = useState(false);
  const [cleanupReview, setCleanupReview] = useState<RetainedDataReview | null>(null);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const localApi = () => {
    try {
      return ['127.0.0.1', 'localhost', '10.0.2.2'].includes(new URL(engine.baseUrl).hostname);
    } catch {
      return false;
    }
  };
  async function action(run: () => Promise<unknown>) {
    if (busy) return;
    setBusy(true);
    setErrors([]);
    setFeedback('');
    try {
      await run();
    } catch (error) {
      if (isApiError(error)) {
        setErrors(error.errorMessages);
        setFeedback(
          error.errorMessages.length
            ? ''
            : 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
        );
      } else
        setFeedback(
          error instanceof Error
            ? error.message
            : 'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.',
        );
    } finally {
      if (mounted.current) setBusy(false);
    }
  }
  async function operation(retry: boolean) {
    if (!localApi()) throw new Error('Este teste financeiro exige a API local descartável.');
    const scope = await store.scope();
    const controller = new FinancialOperation(
      engine,
      store,
      scope,
      'native-financial-fixture',
      secureUuid,
    );
    const result = retry
      ? await controller.verifyAndRetry()
      : await controller.submit({
          operation: 'transaction',
          payload: JSON.parse(payload) as RequestCreateTransactionJson,
        });
    if (engine.getSnapshot().scope === scope.generation) setOutcome(result);
  }
  return (
    <View className="gap-3">
      <Text accessibilityRole="header">Teste técnico de sessão — DEV</Text>
      <Text testID="session-status">{snapshot.status}</Text>
      <Text testID="session-busy">{busy ? 'busy' : 'ready'}</Text>
      <Text testID="session-scope">{snapshot.scope}</Text>
      <Text testID="session-identity">{snapshot.user?.email ?? 'sem identidade'}</Text>
      <Text testID="session-branch">{snapshot.branch?.id ?? 'sem filial'}</Text>
      <Text testID="session-role">
        {snapshot.branch ? enumLabel(roleLabels, snapshot.branch.role).label : 'sem função'}
      </Text>
      <Text testID="session-clock">
        {snapshot.clock?.branchLocalDateTime ?? 'horário indisponível'}
      </Text>
      <Text testID="session-write-gate">
        {snapshot.writesPaused ? 'ações pausadas' : 'contexto verificado'}
      </Text>
      {snapshot.dayChanged ? (
        <Button title="Conferi a nova data" onPress={() => engine.acknowledgeDayChange()} />
      ) : null}
      {[...snapshot.messages, ...errors].map((message, index) => (
        <Text accessibilityRole="alert" key={`${index}:${message}`}>
          {message}
        </Text>
      ))}
      {snapshot.recovery ? <Text>{snapshot.recovery}</Text> : null}
      {feedback ? <Text testID="session-feedback">{feedback}</Text> : null}
      {process.env.EXPO_PUBLIC_NATIVE_FIXTURE_PREFILL === '1' ? (
        <>
          <Button
            testID="session-load-fixture"
            title="Preencher dados fictícios locais"
            disabled={busy || !localApi()}
            onPress={() =>
              void action(async () => {
                const fixture = await readNativeFixture(engine.baseUrl);
                if (!mounted.current) return;
                if (!snapshot.user) {
                  setEmail(fixture.login.email);
                  setPassword(fixture.login.password);
                } else if (!snapshot.branch) setBranch(fixture.branchId);
                else {
                  setNotes(fixture.notes);
                  setPayload(JSON.stringify(fixture.payload));
                }
                setFixtureLoaded(true);
              })
            }
          />
          <Text testID="session-fixture-state">
            {fixtureLoaded ? 'fixture-ready' : 'fixture-empty'}
          </Text>
        </>
      ) : null}
      {!snapshot.user ? (
        <>
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            autoComplete="off"
            textContentType="none"
            importantForAutofill="no"
            testID="session-email"
            accessibilityLabel="E-mail de teste"
            autoCapitalize="none"
            keyboardType="email-address"
            value={email}
            onChangeText={setEmail}
          />
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            autoComplete="off"
            textContentType="none"
            importantForAutofill="no"
            testID="session-password"
            accessibilityLabel="Senha de teste"
            secureTextEntry
            value={password}
            onChangeText={setPassword}
          />
          <Button
            testID="session-login"
            title="Autenticar no stack local"
            disabled={busy || !localApi()}
            onPress={() => {
              const candidate = password;
              setPassword('');
              void action(() => engine.login({ email, password: candidate }));
            }}
          />
        </>
      ) : (
        <>
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            testID="session-branch-input"
            accessibilityLabel="Id da filial de teste"
            value={branch}
            onChangeText={setBranch}
          />
          <Button
            testID="session-select-branch"
            title="Abrir sessão da filial"
            disabled={busy}
            onPress={() => void action(() => engine.selectBranch(branch))}
          />
          <Button
            testID="session-refresh"
            title="Verificar sessão e horário"
            disabled={busy}
            onPress={() => void action(() => engine.refresh())}
          />
          <Button
            testID="session-switch"
            title="Voltar à escolha de filial"
            disabled={busy}
            onPress={() => void action(() => engine.selectBranch(null))}
          />
          <Button
            testID="session-logout"
            title="Sair do teste"
            onPress={() =>
              void action(async () => {
                const result = await engine.logout();
                setFeedback(
                  result.serverConfirmed
                    ? 'Encerramento confirmado'
                    : 'Encerramento no servidor não confirmado',
                );
              })
            }
          />
        </>
      )}
      <Button
        title="Tentar recuperar sessão"
        disabled={busy}
        onPress={() => void action(() => engine.recover())}
      />
      {snapshot.status === 'branch' ? (
        <>
          <Button
            testID="session-grants"
            title="Ler permissões atuais"
            disabled={busy}
            onPress={() =>
              void action(async () => {
                const response = await getOperatorSelfContext({ client: engine.client() });
                setGrants(JSON.stringify(response.data));
              })
            }
          />
          <Text testID="session-grants-result">{grants}</Text>
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            testID="session-note"
            accessibilityLabel="Nota não secreta de teste"
            value={notes}
            onChangeText={setNotes}
          />
          <Button
            testID="session-save-note"
            title="Guardar nota no dispositivo"
            disabled={busy}
            onPress={() =>
              void action(async () => {
                await drafts.save(await store.scope(), 'native-note', { notes }, {});
                setFeedback('Nota guardada neste dispositivo');
              })
            }
          />
          <Button
            testID="session-restore-note"
            title="Restaurar nota do dispositivo"
            disabled={busy}
            onPress={() =>
              void action(async () => {
                const scope = await store.scope();
                const found = await drafts.restore(
                  scope,
                  'native-note',
                  (value): value is { notes: string } =>
                    typeof value === 'object' &&
                    value !== null &&
                    typeof (value as { notes?: unknown }).notes === 'string',
                  async () => {
                    const current = await getBranchCurrent({ client: engine.client() });
                    return current.data.branch.id === scope.branchId;
                  },
                );
                setNotes(found.status === 'found' ? found.value.input.notes : '');
                setFeedback(found.status);
              })
            }
          />
          <Text testID="session-note-state">{feedback}</Text>
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            testID="session-destination"
            accessibilityLabel="Destino técnico"
            value={destination}
            onChangeText={setDestination}
          />
          <Button
            testID="session-hold-intent"
            title="Guardar destino técnico"
            disabled={busy}
            onPress={() =>
              void action(() => navigation.hold(destination, snapshot.branch!.id, ['/turno/hoje']))
            }
          />
          <Button
            testID="session-resume-intent"
            title="Verificar destino técnico"
            disabled={busy}
            onPress={() =>
              void action(async () => {
                const target = await navigation.resolve(async (path) => {
                  const current = await getBranchCurrent({ client: engine.client() });
                  return (
                    current.data.branch.id === snapshot.branch?.id &&
                    (!path.startsWith('/gestao') || current.data.branch.role !== 'Member')
                  );
                });
                setResumed(target?.destination ?? 'destino indisponível');
              })
            }
          />
          <Text testID="session-resumed-intent">{resumed}</Text>
          <TextInput
            disableFullscreenUI
            showSoftInputOnFocus={process.env.EXPO_PUBLIC_NATIVE_AUTOMATION_INPUT !== '1'}
            testID="session-financial-payload"
            accessibilityLabel="Payload financeiro fictício"
            multiline
            returnKeyType="done"
            submitBehavior="blurAndSubmit"
            value={payload}
            onChangeText={setPayload}
          />
          <Button
            testID="session-lose-response"
            title="Perder próxima resposta fictícia"
            disabled={!localApi()}
            onPress={() => {
              loseNextFixtureResponse();
              setFeedback('Falha de resposta preparada para o próximo envio fictício');
            }}
          />
          <Button
            testID="session-create-fixture"
            title="Enviar operação fictícia"
            disabled={busy || snapshot.writesPaused || !localApi()}
            onPress={() => void action(() => operation(false))}
          />
          <Button
            testID="session-replay-fixture"
            title="Verificar mesma operação fictícia"
            disabled={busy || snapshot.writesPaused || !localApi()}
            onPress={() => void action(() => operation(true))}
          />
          <Text testID="session-operation-kind">
            {outcome?.kind === 'failed' ? outcome.failure.kind : (outcome?.kind ?? 'none')}
          </Text>
          <Text testID="session-operation-result">
            {outcome ? JSON.stringify(outcome) : 'sem operação'}
          </Text>
        </>
      ) : null}
      <Button
        testID="session-review-cleanup"
        title="Conferir dados guardados no dispositivo"
        disabled={busy}
        onPress={() => void action(async () => setCleanupReview(await store.inspectRetainedData()))}
      />
      {cleanupReview ? (
        <>
          <Text testID="session-cleanup-counts">{JSON.stringify(cleanupReview)}</Text>
          <Text>
            Apagar abandona entradas guardadas de todas as pessoas e filiais, inclusive operações
            sem resultado confirmado. Isso não desfaz nem confirma ações no servidor. Confira os
            resultados antes de apagar.
          </Text>
          <Button
            testID="session-confirm-cleanup"
            title="Conferi os resultados e confirmo apagar os dados guardados"
            disabled={busy}
            onPress={() =>
              void action(async () => {
                if (await discardRetainedData(cleanupReview, true)) {
                  setCleanupReview(null);
                  setNotes('');
                  setPayload('');
                  setOutcome(null);
                  setResumed('');
                  setFeedback('Dados guardados apagados deste dispositivo.');
                }
              })
            }
          />
        </>
      ) : null}
    </View>
  );
}
