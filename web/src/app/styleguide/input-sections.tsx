'use client';

import { useState } from 'react';
import { asCents, formatBrl, formatBrlWire, MAX_CENTS } from '@loto/core';
import { Button, ButtonLink } from '@/components/ui/button';
import { Checkbox, Radio, RadioGroup, Switch } from '@/components/ui/choice';
import {
  DateField,
  DateRangeField,
  DayStepper,
  MonthField,
  TimeField,
  invalidDateMessage,
  type CalendarMonth,
} from '@/components/ui/date-picker';
import { Fieldset, FormField, FormGrid, ReadOnlyField } from '@/components/ui/form-field';
import { asCalendarDate, type CalendarDate } from '@/components/ui/format-port';
import {
  IdentifierInput,
  Input,
  PasswordInput,
  SearchInput,
  Textarea,
} from '@/components/ui/input';
import {
  blankMoney,
  MoneyInput,
  ReadOnlyAmount,
  type MoneyValue,
} from '@/components/ui/money-input';
import { ErrorSummary } from '@/components/ui/notice';
import { NativeSelect, SearchableSelect, Select, type SelectOption } from '@/components/ui/select';
import { CheckIcon } from '@/components/ui/icons';
import { Example, Section, ThemePair } from './gallery-kit';

/** The branch date a screen would read from `GET /branch/current` — a labelled fixture here. */
export const fixtureToday = asCalendarDate('2026-09-24') as CalendarDate;

const accounts: SelectOption[] = [
  { value: 't1', label: 'Terminal 1', group: 'Terminais' },
  { value: 't2', label: 'Terminal 2', group: 'Terminais', description: 'Lotérica Centro' },
  { value: 'b1', label: 'Banco do Brasil', group: 'Bancos' },
  { value: 'b2', label: 'Caixa Econômica', group: 'Bancos', inactive: true },
];
const clients: SelectOption[] = [
  'Ana Lima',
  'Bruno Costa',
  'Carla Souza',
  'Daniel Rocha',
  'Elisa Martins',
  'Fábio Nunes',
  'Gabriela Alves',
  'Heitor Prado',
  'Íris Campos',
  'João Pedro',
  'Lúcia Ferraz',
  'Márcio Teles',
].map((name, index) => ({ value: `c${index}`, label: name }));

export function InputSections() {
  const [busy, setBusy] = useState(false);
  const [email, setEmail] = useState('');
  const [notes, setNotes] = useState('Contagem parcial: 3 maços');
  const [search, setSearch] = useState('');
  const [cpf, setCpf] = useState('');
  const [readOnlyChanges, setReadOnlyChanges] = useState(0);
  const [count, setCount] = useState<MoneyValue>(blankMoney);
  const [amount, setAmount] = useState<MoneyValue>({ kind: 'amount', digits: '94310' });
  const [account, setAccount] = useState<string | null>(null);
  const [carried] = useState<string | null>('t9');
  const [client, setClient] = useState<string | null>(null);
  const [date, setDate] = useState<CalendarDate | null>(fixtureToday);
  const [dateInvalid, setDateInvalid] = useState(false);
  const [range, setRange] = useState<{ from: CalendarDate | null; to: CalendarDate | null }>({
    from: null,
    to: null,
  });
  const [day, setDay] = useState(fixtureToday);
  const [month, setMonth] = useState<CalendarMonth | null>({ year: 2026, month: 9 });
  const [time, setTime] = useState('09:05');
  const [mine, setMine] = useState(true);
  const [handsOut, setHandsOut] = useState<string | null>(null);
  const [preference, setPreference] = useState(false);
  const filtered = clients.filter((item) =>
    item.label.toLowerCase().includes(search.toLowerCase()),
  );

  return (
    <>
      <Section id="button" title="Button (e link)" contract="design/specs/components/button.md">
        <ThemePair>
          {() => (
            <div className="flex flex-wrap items-start gap-inline-md">
              <Button>Salvar</Button>
              <Button variant="cta">Abrir dia</Button>
              <Button variant="secondary">Voltar</Button>
              <Button variant="text">Abrir transação</Button>
              <Button variant="danger">Cancelar transação</Button>
              <Button variant="danger-solid">Remover acesso</Button>
              <Button size="small" variant="secondary">
                Dia anterior
              </Button>
            </div>
          )}
        </ThemePair>
        <FormGrid>
          <Example
            title="Desabilitado com motivo"
            note="Continua focável (Tab); Enter/Espaço não acionam; o motivo é texto adjacente."
          >
            <Button disabled disabledReason="Nenhuma alteração desde o último salvamento">
              Salvar
            </Button>
          </Example>
          <Example
            title="Carregando"
            note="Cenário controlado: alterne para ver o rótulo em andamento sem a largura pular."
          >
            <div className="flex flex-wrap gap-inline-md">
              <Button loading={busy} loadingLabel="Salvando…" onClick={() => setBusy(true)}>
                Salvar
              </Button>
              <Button variant="text" onClick={() => setBusy(false)}>
                Encerrar cenário
              </Button>
            </div>
          </Example>
          <Example title="Ícone com nome e link real">
            <div className="flex flex-wrap gap-inline-md">
              <Button variant="text" iconOnly aria-label="Confirmar seleção" icon={<CheckIcon />} />
              <ButtonLink href={'/styleguide#button' as never}>Abrir transação (link)</ButtonLink>
            </div>
          </Example>
        </FormGrid>
      </Section>

      <Section id="form-field" title="FormField" contract="design/specs/components/form-field.md">
        <FormGrid>
          <FormField label="Nome" helper="Como aparece nas transações">
            <Input autoComplete="name" />
          </FormField>
          <FormField label="Motivo" optional helper="O operador verá este motivo">
            <Input />
          </FormField>
          <FormField
            label="Valor"
            error="O valor da transação deve ser maior que zero"
            helper="Erro de validação do cliente com a redação do backend"
          >
            <Input defaultValue="0" />
          </FormField>
          <FormField label="Conta" disabledReason="Período bloqueado">
            <Input defaultValue="Terminal 2" />
          </FormField>
          <FormField label="Descrição" changed>
            <Input defaultValue="Bolão #123" />
          </FormField>
          <ReadOnlyField
            label="Abertura"
            value={formatBrlWire(2199)}
            reason="calculado pelo sistema"
            align="end"
          />
        </FormGrid>
        <Example
          title="Resumo de erros após um envio com falha"
          fixture
          note="Recebe o foco uma vez; cada mensagem que nomeia um campo leva até ele."
        >
          <ErrorSummary
            focusOnMount={false}
            title="Não foi possível salvar a transação"
            items={[
              { message: 'O valor da transação deve ser maior que zero', fieldId: 'galeria-valor' },
              { message: 'A data informada está em um período bloqueado.' },
            ]}
          />
          <FormField label="Valor da transação" id="galeria-valor">
            <Input />
          </FormField>
        </Example>
      </Section>

      <Section id="input" title="Input" contract="design/specs/components/input.md">
        <FormGrid>
          <FormField label="E-mail">
            <Input
              type="email"
              placeholder="nome@exemplo.com.br"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              autoComplete="email"
            />
          </FormField>
          <FormField label="Senha" helper="Mostrar senha alterna o mascaramento">
            <PasswordInput autoComplete="current-password" />
          </FormField>
          <FormField label="Nova senha">
            <PasswordInput autoComplete="new-password" />
          </FormField>
          <FormField label="Buscar cliente" helper="Filtra apenas a lista completa carregada">
            <SearchInput
              value={search}
              onValueChange={setSearch}
              resultsAnnouncement={`${filtered.length} clientes encontrados`}
            />
          </FormField>
          <FormField
            label="CPF"
            helper="Máscara aplicada com 11 dígitos; outro tamanho fica como digitado"
          >
            <IdentifierInput kind="cpf" value={cpf} onValueChange={setCpf} />
          </FormField>
          <FormField label="Indisponível" disabledReason="Somente o gerente altera">
            <Input defaultValue="Bloqueado" />
          </FormField>
        </FormGrid>
        <FormField
          label="Observações"
          helper="Até 120 caracteres neste exemplo; o contador aparece perto do limite"
        >
          <Textarea
            value={notes}
            maxLength={120}
            onChange={(event) => setNotes(event.target.value)}
          />
        </FormField>
      </Section>

      <Section
        id="money-input"
        title="MoneyInput"
        contract="design/specs/components/money-input.md"
      >
        <p className="type-helper text-text-muted">
          Exibição, valor falado e colagem vêm do <code className="type-code">@loto/core</code> pela
          porta de formatação: qualquer valor até {formatBrl(asCents(MAX_CENTS))}; o 15º dígito é
          recusado.
        </p>
        <FormGrid>
          <FormField
            label="Contagem de Raspadinha"
            helper="Vazio = não informado; digite 0 para registrar zero"
          >
            <MoneyInput variant="count" value={count} onValueChange={setCount} />
          </FormField>
          <FormField
            label="Valor"
            error={
              amount.kind === 'blank' ? 'O valor da transação deve ser maior que zero' : undefined
            }
          >
            <MoneyInput value={amount} onValueChange={setAmount} />
          </FormField>
          <FormField label="Valor conferido" helper="Somente leitura: não muda ao digitar ou colar">
            <MoneyInput
              readOnly
              value={{ kind: 'amount', digits: '1234' }}
              onValueChange={() => setReadOnlyChanges((count) => count + 1)}
            />
          </FormField>
          <div className="flex flex-col gap-stack-xs">
            <span className="type-label">Abertura (somente leitura)</span>
            <ReadOnlyAmount amount={formatBrlWire(2199)} provenance="calculado pelo sistema" />
          </div>
        </FormGrid>
        <p className="type-helper text-text-secondary" data-testid="money-state">
          Estado relatado: {count.kind === 'blank' ? 'blank' : `amount:${count.digits}`}
        </p>
        <p className="type-helper text-text-secondary" data-testid="readonly-changes">
          Alterações no valor somente leitura: {readOnlyChanges}
        </p>
        <p className="type-helper text-text-secondary" data-testid="amount-state">
          Valor relatado: {amount.kind === 'blank' ? 'blank' : `amount:${amount.digits}`}
        </p>
      </Section>

      <Section id="select" title="Select" contract="design/specs/components/select.md">
        <FormGrid>
          <FormField label="Situação">
            <NativeSelect
              options={[
                { value: '', label: 'Todas' },
                { value: 'Draft', label: 'Rascunho' },
                { value: 'Active', label: 'Ativa' },
              ]}
            />
          </FormField>
          <FormField label="Conta" helper="Setas, Home/End e digitação; Esc fecha">
            <Select options={accounts} value={account} onValueChange={setAccount} />
          </FormField>
          <FormField label="Conta (fora das opções)">
            <Select
              options={accounts}
              value={carried}
              onValueChange={() => undefined}
              missingLabel="Terminal 9"
            />
          </FormField>
          <FormField label="Cliente" helper="Busca local sobre a lista completa">
            <SearchableSelect
              options={clients}
              value={client}
              onValueChange={setClient}
              resultsLabel={(n) => `${n} clientes`}
              sheetTitle="Cliente"
            />
          </FormField>
          <FormField label="Tipo (carregando)">
            <Select
              options={[]}
              value={null}
              onValueChange={() => undefined}
              status={{ kind: 'loading' }}
            />
          </FormField>
          <FormField label="Tipo (falhou)">
            <Select
              options={[]}
              value={null}
              onValueChange={() => undefined}
              status={{ kind: 'failed', retry: () => undefined }}
            />
          </FormField>
          <FormField label="Conta (vazia)">
            <Select
              options={[]}
              value={null}
              onValueChange={() => undefined}
              emptyMessage="Nenhuma conta disponível para você"
            />
          </FormField>
        </FormGrid>
      </Section>

      <Section
        id="date-picker"
        title="DatePicker"
        contract="design/specs/components/date-picker.md"
      >
        <p className="type-helper text-text-muted">
          “Hoje” = 24/09/2026, data de exemplo no lugar do relógio da filial (
          <code className="type-code">GET /branch/current</code>). Nenhum valor passa por{' '}
          <code className="type-code">Date</code>.
        </p>
        <FormGrid>
          <FormField
            label="Data da transação"
            error={dateInvalid ? invalidDateMessage : undefined}
            helper="Calendário: setas, Page Up/Down, Home/End, Enter, Esc"
          >
            <DateField
              value={date}
              onValueChange={setDate}
              onInvalidChange={setDateInvalid}
              today={fixtureToday}
              max={fixtureToday}
            />
          </FormField>
          <FormField label="Data registrada" helper="Somente leitura: o calendário não abre">
            <DateField
              readOnly
              value={fixtureToday}
              onValueChange={() => setReadOnlyChanges((count) => count + 1)}
              today={fixtureToday}
            />
          </FormField>
          <FormField label="Mês de referência">
            <MonthField value={month} onValueChange={setMonth} max={{ year: 2026, month: 9 }} />
          </FormField>
          <FormField label="Horário" helper="24 horas, como na parede da filial">
            <TimeField value={time} onValueChange={setTime} />
          </FormField>
        </FormGrid>
        <DateRangeField
          legend="Período"
          from={range.from}
          to={range.to}
          onRangeChange={setRange}
          today={fixtureToday}
          max={fixtureToday}
        />
        <Example title="Dia da fila">
          <DayStepper
            label="Dia da fila de trabalho"
            value={day}
            onValueChange={setDay}
            today={fixtureToday}
            max={fixtureToday}
            nextUnavailableReason="Ainda não há movimento depois de hoje"
          />
        </Example>
      </Section>

      <Section
        id="choice"
        title="Checkbox, Radio e Switch"
        contract="design/specs/components/choice-controls.md"
      >
        <ThemePair>
          {() => (
            <div className="flex flex-col gap-stack-sm">
              <Checkbox
                label="Somente minhas"
                checked={mine}
                onChange={(event) => setMine(event.target.checked)}
              />
              <Checkbox label="Todas as parcelas restantes" indeterminate readOnly />
              <Checkbox label="Indisponível" disabled description="Somente o gerente altera" />
              <RadioGroup
                legend="Entregou mercadoria contada ao cliente?"
                value={handsOut}
                onValueChange={setHandsOut}
                error={handsOut === null ? 'Responda antes de salvar.' : undefined}
              >
                <Radio value="sim" label="Sim" />
                <Radio value="nao" label="Não" />
              </RadioGroup>
              <Switch
                label="Mostrar valores grandes"
                description="Preferência deste dispositivo, aplicada na hora"
                checked={preference}
                onCheckedChange={setPreference}
              />
            </div>
          )}
        </ThemePair>
        <Fieldset legend="Forma de pagamento" helper="Sem padrão: a escolha é deliberada">
          <span className="type-helper text-text-muted">(grupo vazio de exemplo)</span>
        </Fieldset>
      </Section>
    </>
  );
}
