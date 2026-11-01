import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { parseETag, putSetting, type ResponseSettingJson } from '@loto/api';
import { useGetSetting } from '@loto/api/queries';
import { formatDate, formatDuration, parseCalendarDate } from '@loto/core';
import { GuardedResource } from '@/data/guarded';
import type { ActionFailure } from '@/data/failures';
import { useSession, useScopedApiClient } from '@/session/provider';
import { useShell } from '@/shell/provider';
import {
  AppText,
  Button,
  Card,
  DetailList,
  Fieldset,
  FormField,
  Notice,
  Overlay,
  ReadOnlyValue,
  TextField,
  UncertainNotice,
  useToast,
} from '@/ui';
import {
  datePart,
  durationText,
  durationValue,
  isSettingsInput,
  settingFields,
  type DurationText,
  type DurationValue,
  type SettingField,
  type SettingsInput,
} from './contracts';
import { ReadState, RetainedNotice, errorMessages, useAdminDraft, useAdminReads } from './common';

const fieldLabels: Record<SettingField, string> = {
  dailyTargetHours: 'Meta diária',
  lunchDeductionOver6H: 'Desconto de almoço acima de 6h',
  lunchDeductionOver4H: 'Desconto de almoço acima de 4h',
};
export const effectHelper = (date: string) =>
  `Alterações entram em vigor a partir de hoje (${date}). Os registros de dias anteriores mantêm as metas vigentes na época e não são recalculados.`;
export function targetConfirmation(next: string, date: string, previous: string) {
  return `A nova meta de ${next} será aplicada a partir de hoje, ${date}. Os dias anteriores deste mês permanecerão calculados sob a meta anterior (${previous}).`;
}
export const lunchConfirmation = (date: string) =>
  `As novas regras de jornada valem a partir de hoje, ${date}. Os dias anteriores mantêm as regras vigentes na época.`;
const NEVER_LOCKED = '0001-01-01';
const inputOf = (setting: ResponseSettingJson): SettingsInput => ({
  dailyTargetHours: durationText(setting.dailyTargetHours),
  lunchDeductionOver6H: durationText(setting.lunchDeductionOver6H),
  lunchDeductionOver4H: durationText(setting.lunchDeductionOver4H),
});
const valuesOf = (setting: ResponseSettingJson) => ({
  dailyTargetHours: setting.dailyTargetHours,
  lunchDeductionOver6H: setting.lunchDeductionOver6H,
  lunchDeductionOver4H: setting.lunchDeductionOver4H,
});
interface Change {
  readonly field: SettingField;
  readonly wire: string;
  readonly from: string;
  readonly to: string;
}
interface Confirmation {
  readonly kind: 'target' | 'lunch';
  readonly date: string;
  readonly changes: readonly Change[];
  readonly moved?: boolean;
}
type Outcome =
  | { readonly kind: 'saved'; readonly date: string }
  | { readonly kind: 'stale' | 'uncertain' | 'rejected'; readonly failure: ActionFailure }
  | { readonly kind: 'paused' }
  | null;

export function SettingsScreen() {
  const { engine, snapshot } = useSession();
  const client = useScopedApiClient();
  const shell = useShell();
  const toast = useToast();
  const reads = useAdminReads();
  const setting = useGetSetting({
    request: { client },
    query: { staleTime: 0, refetchOnMount: 'always', enabled: reads },
  });
  const [guard] = useState(() => new GuardedResource(engine));
  const [base, setBase] = useState<ResponseSettingJson | null>(null);
  const [input, setInput] = useState<SettingsInput | null>(null);
  const inputRef = useRef(input);
  const [fresh, setFresh] = useState<ResponseSettingJson | null>(null);
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [outcome, setOutcome] = useState<Outcome>(null);
  const [pending, setPending] = useState(false);
  const flight = useRef(false);
  const [localError, setLocalError] = useState<unknown>(null);
  const draft = useAdminDraft(
    'admin/settings',
    isSettingsInput,
    async () => engine.getSnapshot().branch?.role !== 'Member',
  );
  // The first fresh read after mount is the base; later reads never replace typed work silently.
  const [seed, setSeed] = useState<NonNullable<typeof setting.data> | null>(null);
  const seedable =
    !seed && setting.isSuccess && setting.isFetchedAfterMount ? setting.data : undefined;
  const seedVersionOk =
    !!seedable && parseETag(seedable.headers.get('etag')) === seedable.data.version;
  if (seedable && seedVersionOk) {
    setSeed(seedable);
    setBase(seedable.data);
    setInput(inputOf(seedable.data));
  }
  useLayoutEffect(() => {
    inputRef.current = input;
  }, [input]);
  useEffect(() => {
    // Pre-validated above; the in-memory precondition never reaches device storage.
    if (seed) guard.acceptFresh(seed.headers, seed.data.version);
  }, [seed, guard]);
  const today = snapshot.clock
    ? formatDate(parseCalendarDate(snapshot.clock.branchLocalDate)!)
    : '';
  const values: Record<SettingField, DurationValue> | null =
    base && input
      ? (Object.fromEntries(
          settingFields.map((field) => [field, durationValue(base[field], input[field])]),
        ) as Record<SettingField, DurationValue>)
      : null;
  const changes: readonly Change[] = values
    ? settingFields.flatMap((field) => {
        const value = values[field];
        return value.kind === 'changed'
          ? [{ field, wire: value.wire, from: formatDuration(base![field]), to: value.display }]
          : [];
      })
    : [];
  const invalid = values
    ? settingFields.some((f) => ['invalid', 'imprecise'].includes(values[f].kind))
    : true;
  const blocked =
    pending || !base || !reads || outcome?.kind === 'stale' || outcome?.kind === 'uncertain';
  const update = (field: SettingField, part: keyof DurationText, text: string) => {
    if (!inputRef.current) return;
    const next = { ...inputRef.current, [field]: { ...inputRef.current[field], [part]: text } };
    inputRef.current = next;
    setInput(next);
    if (outcome?.kind === 'saved' || outcome?.kind === 'rejected') setOutcome(null);
    void draft.keep(next, base ? valuesOf(base) : null);
  };
  /** Authoritative branch day immediately before the dialog and again before dispatch. */
  const branchDay = async () => {
    await engine.refresh();
    const clock = engine.getSnapshot().clock;
    if (!clock) throw new Error('Não foi possível verificar a data da filial.');
    return formatDate(parseCalendarDate(clock.branchLocalDate)!);
  };
  const prepare = () => {
    if (blocked || invalid || changes.length === 0 || flight.current) return;
    const pendingChanges = changes;
    flight.current = true;
    setPending(true);
    setLocalError(null);
    void branchDay()
      .then((date) =>
        setConfirmation({
          kind: pendingChanges.some((c) => c.field === 'dailyTargetHours') ? 'target' : 'lunch',
          date,
          changes: pendingChanges,
        }),
      )
      .catch(setLocalError)
      .finally(() => {
        flight.current = false;
        setPending(false);
      });
  };
  const confirm = () => {
    const current = confirmation;
    if (!current || flight.current) return;
    flight.current = true;
    setPending(true);
    void (async () => {
      const kept = await draft.keep(inputRef.current!, base ? valuesOf(base) : null);
      const date = await branchDay();
      if (date !== current.date) {
        // Never confirm yesterday's scope as today's: show the new effective day first.
        setConfirmation({ ...current, date, moved: true });
        return;
      }
      const payload = Object.fromEntries(current.changes.map((c) => [c.field, c.wire]));
      const result = await guard.execute((etag, options) =>
        putSetting(payload, { 'If-Match': etag }, options),
      );
      if (result.kind === 'paused') {
        setOutcome({ kind: 'paused' });
        return;
      }
      setConfirmation(null);
      if (result.kind === 'failed') {
        const kind = result.failure.kind;
        setOutcome({
          kind: kind === 'conflict' ? 'stale' : kind === 'uncertain' ? 'uncertain' : 'rejected',
          failure: result.failure,
        });
        return;
      }
      // Confirmed: the returned strong ETag becomes the in-memory base for the next explicit edit.
      setOutcome({ kind: 'saved', date });
      toast.show('Configurações salvas no servidor.');
      guard.acceptFresh(result.data.headers, result.data.data.version);
      setBase(result.data.data);
      const next = inputOf(result.data.data);
      inputRef.current = next;
      setInput(next);
      if (kept) await draft.acknowledge(kept);
      void setting.refetch();
    })()
      .catch(setLocalError)
      .finally(() => {
        flight.current = false;
        setPending(false);
      });
  };
  const reload = () => {
    setLocalError(null);
    void setting
      .refetch()
      .then((result) => {
        if (result.data && !result.error) setFresh(result.data.data);
        else setLocalError(result.error);
      })
      .catch(setLocalError);
  };
  const adopt = () => {
    const latest = setting.data;
    if (!latest || !fresh || latest.data.version !== fresh.version) return;
    try {
      // Explicit reconciliation: the new version becomes the base, the typed candidate stays.
      guard.acceptFresh(latest.headers, latest.data.version);
      setBase(latest.data);
      setFresh(null);
      setOutcome(null);
    } catch (error) {
      setLocalError(error);
    }
  };
  const lockDate = base ? base.lockDate.slice(0, 10) : null;
  const lock = lockDate && datePart(lockDate);
  const month = snapshot.clock?.branchLocalDate.slice(0, 7).split('-');
  return (
    <>
      <ReadState
        pending={!base && !setting.error}
        error={!base ? setting.error : null}
        retry={() => void setting.refetch()}
      />
      {seedable && !seedVersionOk ? (
        <Notice kind="danger" title="A versão das configurações não pôde ser verificada." />
      ) : null}
      {localError ? (
        <Notice
          kind="danger"
          title="Confira os dados antes de continuar."
          messages={errorMessages(localError)}
        />
      ) : null}
      {base && input && values ? (
        <>
          <Card title="Data de bloqueio">
            <ReadOnlyValue
              label="Data de bloqueio"
              testID="setting-lock-date"
              value={
                !lockDate || lockDate === NEVER_LOCKED || !lock
                  ? 'Nenhum mês bloqueado'
                  : formatDate(lock)
              }
            />
            <AppText variant="helper">
              O bloqueio é feito no fechamento mensal, nunca nesta tela.
            </AppText>
            {month ? (
              <Button
                label="Ir para fechamento mensal"
                variant="secondary"
                onPress={() =>
                  void shell.navigate(
                    `/relatorios/fechamento-mensal/${month[0]}/${Number(month[1])}`,
                    true,
                  )
                }
              />
            ) : null}
          </Card>
          <Card title="Jornada">
            <RetainedNotice
              visible={!!draft.retained}
              onRestore={() => {
                const value = draft.retained!.input;
                inputRef.current = value;
                setInput(value);
                draft.dismissRetained();
              }}
            />
            {draft.problem ? <Notice kind="warning" title={draft.problem} /> : null}
            <AppText testID="setting-effect">{effectHelper(today)}</AppText>
            {settingFields.map((field) => {
              const value = values[field];
              return (
                <Fieldset
                  key={field}
                  legend={fieldLabels[field]}
                  helper={`Atual: ${formatDuration(base[field])}`}
                  error={
                    value.kind === 'invalid'
                      ? 'Informe horas e minutos (0 a 59).'
                      : value.kind === 'imprecise'
                        ? 'A filial guarda as horas com duas casas decimais. Use minutos divisíveis por 3 (por exemplo 18, 21 ou 24).'
                        : undefined
                  }
                >
                  <DurationInputs
                    field={field}
                    value={input[field]}
                    disabled={blocked}
                    onChange={(part, text) => update(field, part, text)}
                  />
                </Fieldset>
              );
            })}
            {outcome?.kind === 'saved' ? (
              <Notice
                kind="success"
                title="Configurações salvas no servidor."
                body={`Valem a partir de ${outcome.date}. Os dias anteriores mantêm as regras vigentes na época.`}
              />
            ) : null}
            {outcome?.kind === 'paused' ? (
              <Notice
                kind="info"
                title="Aguarde a verificação da sessão para salvar."
                body="Nada foi enviado."
              />
            ) : null}
            {outcome?.kind === 'rejected' ? (
              <Notice
                kind="danger"
                title="Não foi possível salvar."
                body={outcome.failure.recovery || 'Seus valores continuam no formulário.'}
                messages={outcome.failure.messages}
              />
            ) : null}
            {outcome?.kind === 'stale' ? (
              <Notice
                kind="warning"
                title="As configurações foram alteradas em outro lugar."
                body="Seus valores continuam no formulário. Nada foi sobrescrito nem será reenviado."
                messages={outcome.failure.messages}
                actions={
                  <Button label="Recarregar e conferir" testID="setting-reload" onPress={reload} />
                }
              />
            ) : null}
            {outcome?.kind === 'uncertain' ? (
              <UncertainNotice
                messages={outcome.failure.messages}
                recovery={
                  <Button label="Recarregar e conferir" testID="setting-reload" onPress={reload} />
                }
              />
            ) : null}
            {fresh ? (
              <Card title="Valores atuais no servidor" testID="setting-fresh">
                <DetailList
                  rows={settingFields.map((field) => ({
                    label: fieldLabels[field],
                    value: `${formatDuration(fresh[field])} no servidor · ${input[field].hours}h ${input[field].minutes.padStart(2, '0')}min no formulário`,
                  }))}
                />
                <Button
                  label="Usar a versão atual como base e manter meus valores"
                  testID="setting-adopt"
                  onPress={adopt}
                />
                <Button
                  label="Descartar meus valores e usar os do servidor"
                  variant="secondary"
                  onPress={() => {
                    adopt();
                    const next = inputOf(fresh);
                    inputRef.current = next;
                    setInput(next);
                  }}
                />
              </Card>
            ) : null}
            <Button
              label="Salvar"
              testID="setting-save"
              loading={pending && !confirmation}
              loadingLabel="Conferindo a data da filial…"
              disabled={blocked || invalid || changes.length === 0}
              onPress={prepare}
            />
            {changes.length === 0 && !invalid ? (
              <AppText variant="helper">Nenhuma alteração para salvar.</AppText>
            ) : null}
          </Card>
          <Button
            label="Conferir banco de horas da equipe"
            variant="link"
            onPress={() => void shell.navigate('/gestao/ponto/banco-de-horas', true)}
          />
        </>
      ) : null}
      <Overlay
        visible={!!confirmation && reads}
        variant="confirmation"
        title={
          confirmation?.kind === 'target'
            ? 'Confirmar alteração de meta?'
            : 'Confirmar alteração das regras de jornada?'
        }
        description={
          confirmation
            ? confirmation.kind === 'target'
              ? targetConfirmation(
                  confirmation.changes.find((c) => c.field === 'dailyTargetHours')!.to,
                  confirmation.date,
                  confirmation.changes.find((c) => c.field === 'dailyTargetHours')!.from,
                )
              : lunchConfirmation(confirmation.date)
            : undefined
        }
        busy={pending}
        onDismiss={() => setConfirmation(null)}
        testID="setting-confirmation"
        footer={
          <>
            <Button
              label="Confirmar e salvar"
              testID="setting-confirm"
              loading={pending}
              loadingLabel="Salvando…"
              onPress={confirm}
              fullWidth
            />
            <Button
              label="Cancelar"
              variant="secondary"
              disabled={pending}
              onPress={() => setConfirmation(null)}
              fullWidth
            />
          </>
        }
      >
        {confirmation?.moved ? (
          <Notice
            kind="warning"
            title={`A data da filial mudou para ${confirmation.date}.`}
            body="Confira a nova data de vigência antes de confirmar."
          />
        ) : null}
        <DetailList
          rows={(confirmation?.changes ?? []).map((c) => ({
            label: fieldLabels[c.field],
            value: `${c.from} → ${c.to}`,
          }))}
        />
      </Overlay>
    </>
  );
}

function DurationInputs({
  field,
  value,
  disabled,
  onChange,
}: {
  field: SettingField;
  value: DurationText;
  disabled: boolean;
  onChange: (part: keyof DurationText, text: string) => void;
}) {
  return (
    <>
      <FormField label={`${fieldLabels[field]}: horas`} disabled={disabled}>
        <TextField
          kind="integer"
          testID={`setting-${field}-hours`}
          value={value.hours}
          maxLength={3}
          onChangeText={(text) => onChange('hours', text)}
        />
      </FormField>
      <FormField label={`${fieldLabels[field]}: minutos`} disabled={disabled}>
        <TextField
          kind="integer"
          testID={`setting-${field}-minutes`}
          value={value.minutes}
          maxLength={2}
          onChangeText={(text) => onChange('minutes', text)}
        />
      </FormField>
    </>
  );
}
