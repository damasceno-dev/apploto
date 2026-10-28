import {
  asCents,
  centsFromMoneyDigits,
  evaluateMoneyExpression,
  formatBrl,
  formatCount,
  identifierDigits,
  isValidCnpj,
  isValidCpf,
  parseCalendarDate,
  type CalendarDate,
} from '@loto/core';
import { useState, type ComponentType } from 'react';
import { Text, View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, shadowStyle, spacing } from '@/theme/style';
import { themePreferenceLabels, themePreferences, type ThemePreference } from '@/theme/theme';
import { colorNames, nativeTokens } from '@/theme/token-adapter';
import {
  AppText,
  Button,
  Checkbox,
  DateField,
  DateRangeField,
  DayStepper,
  FormField,
  FormGrid,
  IdentifierInput,
  MoneyInput,
  MonthField,
  Notice,
  Radio,
  RadioGroup,
  SegmentedControl,
  Select,
  spokenMoney,
  Switch,
  TextField,
  TimeField,
  type SelectOption,
} from '@/ui';
import { dataSections } from './sections-data';
import { Example, EventLog } from './example';

export interface GallerySection {
  readonly key: string;
  readonly title: string;
  /** The component contract file(s) the section exercises. */
  readonly contract: string;
  readonly render: ComponentType;
}

function ThemeSection() {
  const theme = useTheme();
  const { colors } = theme;
  const [group, setGroup] = useState<'sem' | 'dom'>('sem');
  const names = colorNames.filter((name) => name.startsWith(`${group}-`));
  const lookup = (name: string) => {
    const [layer, ...parts] = name.split('-');
    let node: unknown = colors[layer as 'sem' | 'dom'];
    // Paths contain hyphenated keys; walk greedily by matching the longest existing key.
    let rest = parts;
    while (rest.length > 0 && node && typeof node === 'object') {
      let matched = false;
      for (let size = rest.length; size > 0; size -= 1) {
        const key = rest.slice(0, size).join('-');
        if (key in (node as Record<string, unknown>)) {
          node = (node as Record<string, unknown>)[key];
          rest = rest.slice(size);
          matched = true;
          break;
        }
      }
      if (!matched) return undefined;
    }
    return typeof node === 'string' ? node : undefined;
  };
  return (
    <>
      <Example
        title="Tema"
        behavior="Sistema segue o aparelho ao vivo e ao voltar do segundo plano; Claro/Escuro fixam a escolha e sobrevivem ao reinício. Nenhuma credencial é tocada."
      >
        <SegmentedControl<ThemePreference>
          testID="theme-preference"
          accessibilityLabel="Tema"
          options={themePreferences.map((value) => ({
            value,
            label: themePreferenceLabels[value],
          }))}
          value={theme.preference}
          onValueChange={(value) => void theme.setPreference(value)}
        />
        <AppText testID="theme-state" variant="code" tone="secondary">
          {`preferência=${theme.preference} · aplicado=${theme.scheme} · movimento reduzido=${theme.reducedMotion ? 'sim' : 'não'} · escala de texto=${theme.fontScale.toFixed(2)} · largura=${Math.round(theme.width)} dp`}
        </AppText>
        {theme.persistFailed ? (
          <Notice
            kind="warning"
            title="Preferência não guardada"
            body="A escolha vale até o aplicativo reiniciar."
          />
        ) : null}
      </Example>
      <Example
        title="Classes NativeWind com tokens"
        behavior="As mesmas variáveis do tema chegam às classes semânticas (fundo, texto, borda)."
      >
        <View
          testID="nativewind-sample"
          className="rounded-control border border-sem-border-default bg-sem-surface-subtle p-inset-panel"
        >
          <Text className="text-body text-sem-text-primary">Texto primário por classe</Text>
          <Text className="text-helper text-dom-cash-variance-shortage-fg">
            −R$ 12,00 · falta (classe de domínio)
          </Text>
        </View>
      </Example>
      <Example
        title="Cores"
        behavior="Somente tokens semânticos e de domínio; nenhum nome primitivo existe no app."
      >
        <SegmentedControl
          accessibilityLabel="Camada de cor"
          options={[
            { value: 'sem', label: 'Semânticas' },
            { value: 'dom', label: 'Domínio' },
          ]}
          value={group}
          onValueChange={setGroup}
        />
        <View style={{ gap: spacing.stack.xs }}>
          {names.map((name) => (
            <View
              key={name}
              style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.inline.md }}
            >
              <View
                style={{
                  width: 32,
                  height: 24,
                  borderRadius: radius.xs,
                  borderWidth: spacing.size['border-width'],
                  borderColor: colors.sem.border.default,
                  backgroundColor: lookup(name),
                }}
              />
              <AppText variant="code" style={{ flex: 1 }}>
                {name}
              </AppText>
            </View>
          ))}
        </View>
      </Example>
      <Example title="Tipografia (escala do sistema aplicada)">
        {Object.keys(nativeTokens.typography).map((role) => (
          <AppText key={role} variant={role as keyof typeof nativeTokens.typography}>
            {`${role} · R$ 1.234,56`}
          </AppText>
        ))}
      </Example>
      <Example title="Espaçamento, raios, sombras e movimento">
        <View
          style={{
            flexDirection: 'row',
            flexWrap: 'wrap',
            gap: spacing.inline.sm,
            alignItems: 'flex-end',
          }}
        >
          {Object.entries(nativeTokens.spacing.scale).map(([key, value]) => (
            <View key={key} style={{ alignItems: 'center', gap: spacing.stack.xs }}>
              <View
                style={{
                  width: Math.max(1, value),
                  height: Math.max(1, value),
                  backgroundColor: colors.sem.action.primary.bg,
                }}
              />
              <AppText variant="helper" tone="muted">{`${key}=${value}`}</AppText>
            </View>
          ))}
        </View>
        <View style={{ flexDirection: 'row', gap: spacing.stack.md }}>
          {(['raised', 'floating'] as const).map((kind) => (
            <View
              key={kind}
              style={{
                flex: 1,
                padding: spacing.inset.panel,
                borderRadius: radius.panel,
                backgroundColor: colors.sem.surface.raised,
                ...shadowStyle(theme.scheme, kind),
              }}
            >
              <AppText variant="label">{`shadow.${kind}`}</AppText>
            </View>
          ))}
        </View>
        <AppText variant="code" tone="secondary">
          {Object.keys(nativeTokens.motion.transition)
            .map(
              (name) =>
                `${name}=${theme.motion(name as keyof typeof nativeTokens.motion.transition).duration}ms`,
            )
            .join(' · ')}
        </AppText>
      </Example>
    </>
  );
}

function ButtonSection() {
  const [events, setEvents] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const log = (event: string) => setEvents((current) => [...current.slice(-5), event]);
  return (
    <>
      <Example
        title="Variantes"
        behavior="Primária, CTA (uma por tela), secundária, link, perigo contornado e sólido."
      >
        <Button label="Salvar" onPress={() => log('salvar')} testID="button-primary" />
        <Button label="Fechar caixa" variant="cta" onPress={() => log('cta')} />
        <Button label="Dia anterior" variant="secondary" onPress={() => log('secundário')} />
        <Button
          label="Abrir transação"
          variant="link"
          role="link"
          accessibilityLabel="Abrir transação 24/09/2026 R$ 943,10 Terminal 2"
          onPress={() => log('link')}
        />
        <Button label="Cancelar transação" variant="danger" onPress={() => log('perigo')} />
        <Button
          label="Cancelar transação"
          variant="danger-solid"
          onPress={() => log('perigo sólido')}
        />
      </Example>
      <Example
        title="Indisponível com motivo e carregando"
        behavior="Toques não disparam nada; o motivo fica visível e é lido; a largura não salta."
      >
        <Button
          label="Salvar"
          disabled
          disabledReason="Nenhuma alteração desde o último salvamento"
          onPress={() => log('NÃO DEVERIA')}
          testID="button-disabled"
        />
        <Button
          label="Enviar para aprovação"
          loading={loading}
          loadingLabel="Enviando…"
          onPress={() => {
            log('enviar');
            setLoading(true);
            setTimeout(() => setLoading(false), 2000);
          }}
          testID="button-loading"
        />
        <Button
          label="Ver pendências de outros operadores e conferir os fechamentos atrasados desta filial"
          variant="secondary"
          onPress={() => log('rótulo longo')}
        />
        <EventLog events={events} testID="button-events" />
      </Example>
    </>
  );
}

function InputSection() {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [notes, setNotes] = useState('');
  const [search, setSearch] = useState('');
  const [cpf, setCpf] = useState('');
  const [cnpj, setCnpj] = useState('');
  const [phone, setPhone] = useState('');
  const [cep, setCep] = useState('');
  const clients = ['Ana Lima', 'Bárbara Souza', 'Caio Ferreira', 'Débora Antunes', 'Érico Nunes'];
  const matches = clients.filter((client) =>
    client.toLocaleLowerCase('pt-BR').includes(search.toLocaleLowerCase('pt-BR')),
  );
  return (
    <>
      <Example
        title="FormField e Input"
        behavior="Rótulo persistente; opcional marcado; o valor nunca é aparado pelo campo."
      >
        <FormGrid>
          <FormField label="Nome" helper="Como aparece nos relatórios">
            <TextField
              value={name}
              onChangeText={setName}
              autoComplete="name"
              testID="input-name"
            />
          </FormField>
          <FormField label="E-mail" optional>
            <TextField
              kind="email"
              value={email}
              onChangeText={setEmail}
              placeholder="nome@exemplo.com.br"
              testID="input-email"
            />
          </FormField>
        </FormGrid>
        <FormField label="Senha atual">
          <TextField
            kind="password"
            value={password}
            onChangeText={setPassword}
            testID="input-password"
          />
        </FormField>
        <FormField label="Observações" helper="Até 1.000 caracteres" unsaved={notes !== ''}>
          <TextField
            kind="textarea"
            value={notes}
            onChangeText={setNotes}
            maxLength={1000}
            testID="input-notes"
          />
        </FormField>
        <FormField label="Buscar cliente">
          <TextField
            kind="search"
            value={search}
            onChangeText={setSearch}
            resultCountLabel={`${formatCount(matches.length)} clientes encontrados`}
            testID="input-search"
          />
        </FormField>
        <AppText variant="helper" tone="secondary">
          {matches.join(' · ') || 'Nenhum cliente nesta busca.'}
        </AppText>
      </Example>
      <Example
        title="Estados: inválido, indisponível, somente leitura"
        behavior="O estado próprio do controle soma-se ao do campo, dentro e fora do FormField."
      >
        <FormField label="Motivo" error="O motivo é obrigatório.">
          <TextField value="" onChangeText={() => undefined} testID="input-invalid" />
        </FormField>
        <FormField label="Conta" disabled disabledReason="Escolha a filial primeiro">
          <TextField
            value="Terminal 2"
            onChangeText={() => undefined}
            testID="input-field-disabled"
          />
        </FormField>
        <TextField
          value="Controle desabilitado fora de um campo"
          onChangeText={() => undefined}
          disabled
          accessibilityLabel="Sem campo"
          testID="input-own-disabled"
        />
        <FormField label="Data da transação" readOnly readOnlyReason="Período bloqueado">
          <TextField value="24/09/2026" onChangeText={() => undefined} testID="input-readonly" />
        </FormField>
      </Example>
      <Example
        title="Identificadores (pontuação progressiva)"
        behavior="Apagar um separador apaga o dígito vizinho; letras, excesso e CNPJ alfanumérico ficam como digitados."
      >
        <FormField
          label="CPF"
          helper={
            cpf
              ? `dígitos ${identifierDigits(cpf)} · ${isValidCpf(cpf) ? 'válido' : 'não validado'}`
              : undefined
          }
        >
          <IdentifierInput kind="cpf" value={cpf} onChangeText={setCpf} testID="input-cpf" />
        </FormField>
        <FormField
          label="CNPJ"
          helper={cnpj ? (isValidCnpj(cnpj) ? 'válido' : 'não validado') : undefined}
        >
          <IdentifierInput kind="cnpj" value={cnpj} onChangeText={setCnpj} testID="input-cnpj" />
        </FormField>
        <FormField label="Telefone">
          <IdentifierInput
            kind="phone"
            value={phone}
            onChangeText={setPhone}
            testID="input-phone"
          />
        </FormField>
        <FormField label="CEP">
          <IdentifierInput kind="cep" value={cep} onChangeText={setCep} testID="input-cep" />
        </FormField>
      </Example>
    </>
  );
}

function MoneySection() {
  const [count, setCount] = useState('');
  const [value, setValue] = useState('0');
  const [locked, setLocked] = useState('1234');
  const [expression, setExpression] = useState('');
  const outcome =
    expression === '' ? null : evaluateMoneyExpression(expression, { allowNegative: false });
  return (
    <>
      <Example
        title="Contagem: em branco ≠ zero"
        behavior="Dígitos entram da direita (centavos); apagar volta a em branco; 0 grava zero; o 15º dígito é recusado; colar R$ 31.415,92 funciona."
      >
        <FormField label="Contagem de Raspadinha">
          <MoneyInput value={count} onValueChange={setCount} testID="money-count" />
        </FormField>
        <AppText testID="money-count-state" variant="code" tone="secondary">
          {`estado="${count}" · falado: ${spokenMoney(count)}`}
        </AppText>
        <FormField label="Valor">
          <MoneyInput value={value} onValueChange={setValue} testID="money-value" />
        </FormField>
        <AppText testID="money-value-state" variant="code" tone="secondary">
          {`estado="${value}" · ${value === '' ? 'não informado' : formatBrl(centsFromMoneyDigits(value))}`}
        </AppText>
      </Example>
      <Example
        title="Somente leitura e indisponível"
        behavior="Digitar, apagar ou colar não muda o valor."
      >
        <FormField label="Abertura" readOnly>
          <MoneyInput
            value="100000"
            onValueChange={() => undefined}
            provenance="calculated"
            testID="money-readonly"
          />
        </FormField>
        <FormField label="Valor bloqueado" disabled disabledReason="Período bloqueado">
          <MoneyInput value={locked} onValueChange={setLocked} testID="money-disabled" />
        </FormField>
        <AppText
          testID="money-disabled-state"
          variant="code"
          tone="secondary"
        >{`estado="${locked}"`}</AppText>
        <FormField label="Valor inválido" error="O valor da transação deve ser maior que zero">
          <MoneyInput value="0" onValueChange={() => undefined} testID="money-invalid" />
        </FormField>
      </Example>
      <Example
        title="Demonstração técnica: aritmética restrita"
        behavior="Somente nesta galeria (Design 4.9.7 decide o padrão); nenhum MoneyInput real ganha modo de expressão."
      >
        <FormField label="Expressão (demonstração)">
          <TextField
            value={expression}
            onChangeText={setExpression}
            placeholder="12,50 + 3 × 2"
            testID="money-expression"
          />
        </FormField>
        <AppText testID="money-expression-result" variant="code" tone="secondary">
          {outcome === null
            ? 'Resultado: —'
            : outcome.status === 'ok'
              ? `Resultado: ${formatBrl(asCents(outcome.cents))}${outcome.rounded ? ' (arredondado)' : ''}`
              : `Resultado: ${outcome.status}${outcome.status === 'invalid' ? ` (${outcome.error})` : ''}`}
        </AppText>
      </Example>
    </>
  );
}

const accountOptions: readonly SelectOption[] = [
  { value: 't1', label: 'Terminal 1', description: 'Caixa · Térreo', group: 'Terminais' },
  { value: 't2', label: 'Terminal 2', description: 'Caixa · Térreo', group: 'Terminais' },
  { value: 'f1', label: 'Fiado Terminal 1', group: 'Fiado' },
  {
    value: 'b1',
    label: 'Conta corrente',
    description: 'Banco 001 · ag. 1234',
    group: 'Bancos',
    inactive: true,
  },
];

function SelectSection() {
  const [account, setAccount] = useState<string | null>(null);
  const [method, setMethod] = useState<string | null>(null);
  const [client, setClient] = useState<string | null>(null);
  const [carried, setCarried] = useState<string | null>('retired-42');
  const [events, setEvents] = useState<string[]>([]);
  const clients = Array.from({ length: 60 }, (_, index) => ({
    value: `c${index}`,
    label: `${['Ana', 'Ângela', 'João', 'Márcia', 'Otávio'][index % 5]} ${['Lima', 'Souza', 'Araújo', 'Conceição'][index % 4]} ${index + 1}`,
  }));
  return (
    <>
      <Example
        title="Lista com grupos e marcador de inatividade"
        behavior="Inativo só aparece com campo explícito do servidor."
      >
        <FormField label="Conta">
          <Select
            options={accountOptions}
            value={account}
            onValueChange={setAccount}
            testID="select-account"
          />
        </FormField>
      </Example>
      <Example title="Sem padrão" behavior="Mostra Escolha… até a pessoa escolher.">
        <FormField label="Forma de recebimento">
          <Select
            options={[
              { value: 'cash', label: 'Dinheiro' },
              { value: 'pix', label: 'PIX' },
            ]}
            value={method}
            onValueChange={setMethod}
            testID="select-method"
          />
        </FormField>
      </Example>
      <Example
        title="Pesquisável (coleção completa, local)"
        behavior="Busca sem acento; contagem falada após a pausa."
      >
        <FormField label="Cliente">
          <Select
            options={clients}
            value={client}
            onValueChange={setClient}
            searchable
            resultNoun="clientes"
            testID="select-client"
          />
        </FormField>
      </Example>
      <Example title="Fora das opções, carregando, falhou, vazio">
        <FormField label="Tipo">
          <Select
            options={accountOptions}
            value={carried}
            missingLabel="Tarifa antiga"
            onValueChange={setCarried}
          />
        </FormField>
        <FormField label="Operador">
          <Select options={[]} value={null} onValueChange={() => undefined} state="loading" />
        </FormField>
        <FormField label="Categoria">
          <Select
            options={[]}
            value={null}
            onValueChange={() => undefined}
            state="failed"
            onRetry={() => setEvents((current) => [...current, 'tentar novamente'])}
          />
        </FormField>
        <FormField label="Conta do operador">
          <Select
            options={[]}
            value={null}
            onValueChange={() => undefined}
            emptyText="Nenhuma conta disponível para você"
            emptyAction={{
              label: 'Solicitar vínculo',
              onPress: () => setEvents((current) => [...current, 'solicitar vínculo']),
            }}
          />
        </FormField>
      </Example>
      <Example title="Indisponível e somente leitura" behavior="Não abre e não emite valor.">
        <FormField label="Conta (indisponível)" disabled disabledReason="Escolha a filial primeiro">
          <Select
            options={accountOptions}
            value="t1"
            onValueChange={() => setEvents((current) => [...current, 'NÃO DEVERIA'])}
            testID="select-disabled"
          />
        </FormField>
        <Select
          options={accountOptions}
          value="t2"
          onValueChange={() => setEvents((current) => [...current, 'NÃO DEVERIA'])}
          disabled
          accessibilityLabel="Conta (controle desabilitado)"
          testID="select-own-disabled"
        />
        <FormField label="Conta (somente leitura)" readOnly>
          <Select options={accountOptions} value="t2" onValueChange={() => undefined} />
        </FormField>
        <EventLog events={events} testID="select-events" />
      </Example>
    </>
  );
}

function DateSection() {
  const today = parseCalendarDate('2026-09-24') as CalendarDate;
  const [date, setDate] = useState<CalendarDate | null>(today);
  const [edge, setEdge] = useState<CalendarDate | null>(parseCalendarDate('0001-01-01'));
  const [range, setRange] = useState<{ from: CalendarDate | null; to: CalendarDate | null }>({
    from: null,
    to: null,
  });
  const [day, setDay] = useState<CalendarDate>(today);
  const [month, setMonth] = useState<{ year: number; month: number } | null>(null);
  const [time, setTime] = useState<string | null>('09:05');
  return (
    <>
      <Example
        title="Data com hoje da filial e limite"
        behavior="Digitação sempre possível; texto inválido é mantido com a mensagem; datas depois de hoje ficam indisponíveis no calendário (hoje = 24/09/2026, exemplo técnico)."
      >
        <FormField label="Data de abertura">
          <DateField
            value={date}
            onValueChange={setDate}
            today={today}
            max={today}
            testID="date-open"
          />
        </FormField>
        <AppText
          testID="date-open-state"
          variant="code"
          tone="secondary"
        >{`valor=${date ?? 'nulo'}`}</AppText>
      </Example>
      <Example
        title="Limites do calendário (0001–9999)"
        behavior="Mês anterior para em 01/0001 sem erro; Ir para o ano aceita 0001–9999."
      >
        <FormField label="Data histórica">
          <DateField value={edge} onValueChange={setEdge} testID="date-edge" />
        </FormField>
        <DayStepper value={day} onValueChange={setDay} max={today} testID="day-stepper" />
      </Example>
      <Example title="Intervalo, mês e hora">
        <DateRangeField
          from={range.from}
          to={range.to}
          onChange={setRange}
          today={today}
          testID="date-range"
        />
        <MonthField value={month} onValueChange={setMonth} testID="month-waiting" />
        <MonthField
          value={month}
          onValueChange={setMonth}
          reference={{ year: 2026, month: 9 }}
          testID="month-reference"
        />
        <FormField label="Horário">
          <TimeField value={time} onValueChange={setTime} testID="time-field" />
        </FormField>
      </Example>
      <Example
        title="Indisponível e somente leitura"
        behavior="O calendário não abre e nenhuma data é emitida."
      >
        <FormField label="Vencimento" disabled disabledReason="Plano finalizado">
          <DateField value={today} onValueChange={() => undefined} testID="date-disabled" />
        </FormField>
        <FormField label="Data registrada" readOnly>
          <DateField value={today} onValueChange={() => undefined} />
        </FormField>
      </Example>
    </>
  );
}

function ChoiceSection() {
  const [mine, setMine] = useState(false);
  const [rows, setRows] = useState<readonly boolean[]>([true, false, false]);
  const [handsOut, setHandsOut] = useState<string | null>(null);
  const [haptics, setHaptics] = useState(true);
  const all = rows.every(Boolean);
  const some = rows.some(Boolean) && !all;
  return (
    <>
      <Example title="Checkbox e seleção de grupo">
        <Checkbox
          label="Somente minhas"
          checked={mine}
          onCheckedChange={setMine}
          testID="check-mine"
        />
        <Checkbox
          label="Todas as parcelas restantes"
          checked={all}
          indeterminate={some}
          onCheckedChange={(next) => setRows(rows.map(() => next))}
          testID="check-all"
        />
        {rows.map((checked, index) => (
          <Checkbox
            key={index}
            label={`Parcela ${index + 2}/4`}
            checked={checked}
            onCheckedChange={(next) =>
              setRows(rows.map((value, at) => (at === index ? next : value)))
            }
          />
        ))}
        <Checkbox
          label="Opção indisponível"
          checked={false}
          disabled
          onCheckedChange={() => undefined}
        />
      </Example>
      <Example title="Rádio sem padrão" behavior="Nada selecionado até a resposta.">
        <FormField label="Entregou mercadoria contada ao cliente?">
          <RadioGroup value={handsOut} onValueChange={setHandsOut} testID="radio-hands-out">
            <Radio value="yes" label="Sim" />
            <Radio value="no" label="Não" />
          </RadioGroup>
        </FormField>
        <AppText variant="code" tone="secondary">{`valor=${handsOut ?? 'nenhum'}`}</AppText>
      </Example>
      <Example title="Switch (preferência imediata do aparelho)">
        <Switch
          label="Vibrar ao salvar"
          description="preferência deste aparelho"
          value={haptics}
          onValueChange={setHaptics}
          testID="switch-haptics"
        />
        <Switch
          label="Opção indisponível"
          value={false}
          onValueChange={() => undefined}
          disabled
          disabledReason="Não suportado neste aparelho"
        />
      </Example>
    </>
  );
}

export const gallerySections: readonly GallerySection[] = [
  { key: 'theme', title: 'Tema e tokens', contract: 'tokens/README.md', render: ThemeSection },
  { key: 'button', title: 'Button', contract: 'button.md', render: ButtonSection },
  {
    key: 'input',
    title: 'FormField e Input',
    contract: 'form-field.md · input.md',
    render: InputSection,
  },
  { key: 'money', title: 'MoneyInput', contract: 'money-input.md', render: MoneySection },
  { key: 'select', title: 'Select', contract: 'select.md', render: SelectSection },
  { key: 'date', title: 'DatePicker', contract: 'date-picker.md', render: DateSection },
  {
    key: 'choice',
    title: 'Checkbox, Radio e Switch',
    contract: 'choice-controls.md',
    render: ChoiceSection,
  },
  ...dataSections,
];
