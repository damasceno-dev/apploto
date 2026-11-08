import { parseCalendarDate, accountTypeLabels, type CalendarDate } from '@loto/core';
import type { ResponseClientJson, ResponseOperatorJson } from '@loto/api';
import { useSession } from '@/session/provider';
import { useShell } from '@/shell/provider';
import { Button, DateField, FormField, Select, TimeField, TextField } from '@/ui';
import { CountMoneyField } from '@/day/arithmetic';
import type { Digits } from '@/day/contracts';
import type { AccountOption, TypeWithDirection } from './contracts';
import type { Lookup } from './common';

const lookupState = (status: string) =>
  status === 'pending'
    ? ('loading' as const)
    : status === 'error'
      ? ('failed' as const)
      : ('ready' as const);

export function TypeField({
  types,
  value,
  onChange,
  status,
  retry,
  testID,
  label = 'Tipo de transação',
  disabled,
}: {
  types: readonly TypeWithDirection[];
  value: string | null;
  onChange: (id: string) => void;
  status: string;
  retry: () => void;
  testID: string;
  label?: string;
  disabled?: boolean;
}) {
  return (
    <FormField label={label} required disabled={disabled}>
      <Select
        testID={testID}
        searchable
        resultNoun="tipos"
        state={lookupState(status)}
        onRetry={retry}
        value={value}
        onValueChange={onChange}
        emptyText="Nenhum tipo disponível para lançamento manual."
        options={types.map((type) => ({
          value: type.id,
          label: type.name,
          description: `${type.categoryName} · ${type.direction === 'In' ? 'Entrada' : 'Saída'}${type.requiresTabAccountAndClient ? ' · fiado' : ''}`,
        }))}
      />
    </FormField>
  );
}

export function AccountField({
  accounts,
  lookup,
  value,
  onChange,
  testID,
  label = 'Conta',
  helper,
  disabled,
}: {
  accounts: readonly AccountOption[];
  lookup: Lookup<unknown>;
  value: string | null;
  onChange: (id: string) => void;
  testID: string;
  label?: string;
  helper?: string;
  disabled?: boolean;
}) {
  return (
    <FormField label={label} required helper={helper} disabled={disabled}>
      <Select
        testID={testID}
        state={lookupState(lookup.status)}
        onRetry={() => void lookup.refetch()}
        value={value}
        onValueChange={onChange}
        emptyText="Nenhuma conta disponível para você."
        options={accounts.map((account) => ({
          value: account.id,
          label: account.name,
          description: accountTypeLabels[account.type],
        }))}
      />
    </FormField>
  );
}

/**
 * Client choice over the complete branch client list (local search only), with **Cadastrar
 * cliente** going to Clientes; a client created there returns here selected, values intact.
 */
export function ClientField({
  clients,
  status,
  retry,
  value,
  onChange,
  required,
  testID,
  locked,
  lockedReason,
}: {
  clients: readonly ResponseClientJson[] | undefined;
  status: string;
  retry: () => void;
  value: string | null;
  onChange: (id: string | null) => void;
  required: boolean;
  testID: string;
  locked?: boolean;
  lockedReason?: string;
}) {
  const shell = useShell();
  return (
    <FormField
      label="Cliente"
      required={required}
      optional={!required}
      readOnly={locked}
      readOnlyReason={lockedReason}
    >
      <Select
        testID={testID}
        searchable
        resultNoun="clientes"
        state={lookupState(status)}
        onRetry={retry}
        value={value}
        onValueChange={onChange}
        emptyText="Nenhum cliente cadastrado."
        emptyAction={{
          label: 'Cadastrar cliente',
          onPress: () => void shell.navigate('/clientes?section=new', true),
        }}
        options={(clients ?? []).map((client) => ({ value: client.id, label: client.name }))}
      />
      {locked ? null : (
        <Button
          label="Cadastrar cliente"
          variant="link"
          testID={`${testID}-new`}
          onPress={() => void shell.navigate('/clientes?section=new', true)}
        />
      )}
    </FormField>
  );
}

export function OperatorField({
  operators,
  status,
  value,
  onChange,
  required,
  testID,
}: {
  operators: readonly ResponseOperatorJson[] | undefined;
  status: string;
  value: string | null;
  onChange: (id: string | null) => void;
  required: boolean;
  testID: string;
}) {
  const { snapshot } = useSession();
  const own = snapshot.selfContext?.operatorId ?? null;
  return (
    <FormField
      label="Operador responsável"
      required={required}
      optional={!required}
      helper={
        required
          ? 'Seu login não tem operador vinculado: escolha em nome de quem a operação é registrada.'
          : 'Deixe em branco para registrar em seu nome.'
      }
    >
      <Select
        testID={testID}
        searchable
        resultNoun="operadores"
        state={lookupState(status)}
        value={value}
        onValueChange={onChange}
        options={(operators ?? []).map((o) => ({
          value: o.id,
          label: o.id === own ? `${o.name} (você)` : o.name,
        }))}
      />
    </FormField>
  );
}

export function AmountField({
  label = 'Valor',
  value,
  onChange,
  buffer,
  onBuffer,
  testID,
  disabled,
}: {
  label?: string;
  value: Digits;
  onChange: (digits: Digits) => void;
  buffer: string | undefined;
  onBuffer: (raw: string | undefined) => void;
  testID: string;
  disabled?: boolean;
}) {
  return (
    <CountMoneyField
      label={label}
      testID={testID}
      value={value}
      onValueChange={onChange}
      buffer={buffer}
      onBufferChange={onBuffer}
      disabled={disabled}
    />
  );
}

export function DateTimeFields({
  date,
  time,
  onDate,
  onTime,
  testID,
  dateLabel = 'Data',
  disabled,
}: {
  date: string | null;
  time: string | null;
  onDate: (date: string | null) => void;
  onTime?: (time: string | null) => void;
  testID: string;
  dateLabel?: string;
  disabled?: boolean;
}) {
  const { snapshot } = useSession();
  const today = snapshot.clock?.branchLocalDate ?? null;
  return (
    <>
      <FormField label={dateLabel} required disabled={disabled}>
        <DateField
          testID={`${testID}-date`}
          today={today ? (parseCalendarDate(today) ?? undefined) : undefined}
          value={date ? (parseCalendarDate(date) as CalendarDate | null) : null}
          onValueChange={(value) => onDate(value)}
        />
      </FormField>
      {onTime ? (
        <FormField label="Hora" optional disabled={disabled}>
          <TimeField testID={`${testID}-time`} value={time} onValueChange={onTime} />
        </FormField>
      ) : null}
    </>
  );
}

export function DescriptionField({
  value,
  onChange,
  testID,
  label = 'Descrição',
  disabled,
}: {
  value: string;
  onChange: (text: string) => void;
  testID: string;
  label?: string;
  disabled?: boolean;
}) {
  return (
    <FormField label={label} optional disabled={disabled}>
      <TextField
        kind="textarea"
        testID={testID}
        value={value}
        maxLength={500}
        onChangeText={onChange}
      />
    </FormField>
  );
}
