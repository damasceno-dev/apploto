import {
  asCents,
  formatBrl,
  formatCount,
  formatSignedBrl,
  formatDuration,
  presentFiadoBalance,
  spellSignedBrl,
} from '@loto/core';
import { useRef, useState, type ComponentType } from 'react';
import { View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { spacing } from '@/theme/style';
import {
  AppText,
  Badge,
  Button,
  Card,
  ChecklistItem,
  ConfirmDialog,
  DataTable,
  DetailList,
  Disclosure,
  EmptyState,
  ErrorSummary,
  FilterBar,
  FormField,
  GroupedList,
  Menu,
  MoneyInput,
  Notice,
  Overlay,
  PageHeader,
  Pagination,
  SaveStatus,
  Select,
  Skeleton,
  Stepper,
  SummaryStrip,
  Tabs,
  TextField,
  UncertainNotice,
  statusDomains,
  useToast,
  type InputHandle,
  type PageEnvelope,
  type SaveState,
  type StatusDomain,
  type TableState,
} from '@/ui';
import { Example, EventLog } from './example';

interface GallerySectionLike {
  readonly key: string;
  readonly title: string;
  readonly contract: string;
  readonly render: ComponentType;
}

function BadgeSection() {
  const { colors } = useTheme();
  const [unknown, setUnknown] = useState<string[]>([]);
  return (
    <>
      {(Object.keys(statusDomains) as StatusDomain[]).map((domain) => (
        <Example key={domain} title={domain}>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
            {Object.keys((colors.dom as Record<string, object>)[domain] ?? {}).map((value) => (
              <Badge key={value} kind="status" domain={domain} value={value} />
            ))}
          </View>
        </Example>
      ))}
      <Example
        title="Valor desconhecido, marcador, retorno e modo"
        behavior="Um valor futuro aparece cru e neutro e é reportado; Automática nunca substitui a situação."
      >
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
          <Badge
            kind="status"
            domain="daily-close-status"
            value="Archived"
            onUnknownValue={(_, value) =>
              setUnknown((current) => (current.includes(value) ? current : [...current, value]))
            }
            context="Situação"
          />
          <Badge kind="automatic" />
          <Badge kind="feedback" tone="info" label="você" />
          <Badge kind="feedback" tone="neutral" label="Inativa" />
          <Badge kind="mode" mode="gestao" />
          <Badge kind="mode" mode="meu-turno" />
        </View>
        <EventLog
          events={unknown.map((value) => `desconhecido: ${value}`)}
          testID="badge-unknown"
        />
      </Example>
    </>
  );
}

function StructureSection() {
  const [variance, setVariance] = useState('1200');
  const cents = asCents(-Number(variance || '0'));
  const fiado = presentFiadoBalance(asCents(-4550));
  return (
    <>
      <Example
        title="Card"
        behavior="Sem toque no cartão inteiro; ações explícitas; empilha em telas estreitas."
      >
        <Card
          variant="next-action"
          eyebrow="Próximo passo"
          title="Fechar o caixa de hoje"
          actions={
            <>
              <Button label="Fechar caixa" variant="cta" onPress={() => undefined} />
              <Button label="Ver fechamento de hoje" variant="link" onPress={() => undefined} />
            </>
          }
        >
          <AppText tone="secondary">Terminal 2 · quinta-feira, 24 de setembro de 2026</AppText>
        </Card>
        <Card variant="checklist" title="Configuração da lotérica">
          <ChecklistItem
            title="Contas"
            status="2 terminais configurados"
            done
            action={
              <Button label="Ir para Contas" variant="link" role="link" onPress={() => undefined} />
            }
          />
          <ChecklistItem
            title="Operadores"
            status="Nenhum operador vinculado"
            done={false}
            action={
              <Button
                label="Ir para Operadores"
                variant="link"
                role="link"
                onPress={() => undefined}
              />
            }
          />
        </Card>
      </Example>
      <Example
        title="SummaryStrip"
        behavior="Valor indisponível mostra — e nunca R$ 0,00; sinais e palavras sempre visíveis."
      >
        <FormField label="Diferença de exemplo (centavos de falta)">
          <MoneyInput value={variance} onValueChange={setVariance} />
        </FormField>
        <SummaryStrip
          variant="signed"
          items={[
            {
              label: 'Total do fechamento',
              value: formatBrl(asCents(1_234_56)),
              caption: 'ainda não enviada',
            },
            {
              label: 'Diferença',
              value: `${formatSignedBrl(cents)}${cents < 0 ? ' · falta' : cents > 0 ? ' · sobra' : ''}`,
              tone: {
                group: 'cash-variance',
                value: cents < 0 ? 'shortage' : cents > 0 ? 'surplus' : 'zero',
              },
              spoken: `${spellSignedBrl(cents)}${cents < 0 ? ', falta' : ''}`,
            },
            {
              label: 'Saldo do cliente',
              value: fiado.text,
              tone: { group: 'fiado-balance', value: fiado.kind },
            },
            {
              label: 'Banco de horas',
              value: formatDuration(2.25),
              tone: { group: 'hour-balance', value: 'positive' },
            },
            {
              label: 'Entradas',
              value: null,
              caption: 'Não foi possível carregar',
              onRetry: () => undefined,
            },
            { label: 'Saídas', value: null, loading: true },
          ]}
        />
      </Example>
      <Example title="DetailList">
        <DetailList
          rows={[
            {
              label: 'Aberto por',
              value: 'Lucas Ferreira · operador Lucas Ferreira · 24/09/2026 08:02',
            },
            { label: 'Contado por', value: null },
            { label: 'Abertura', value: formatBrl(asCents(100000)), provenance: 'calculated' },
            { label: 'Situação', value: 'Período bloqueado até 31/08/2026', restriction: true },
            { label: 'Aprovado por', value: null, loading: true },
          ]}
        />
      </Example>
    </>
  );
}

interface LedgerRow {
  readonly id: string;
  readonly date: string;
  readonly account: string;
  readonly type: string;
  readonly cents: number;
  readonly status: 'Draft' | 'Active' | 'Cancelled';
  readonly automatic: boolean;
}

function makeRows(total: number, page: number, size: number): LedgerRow[] {
  const start = (page - 1) * size;
  return Array.from({ length: Math.max(0, Math.min(size, total - start)) }, (_, offset) => {
    const index = start + offset + 1;
    return {
      id: `r${index}`,
      date: `${String((index % 28) + 1).padStart(2, '0')}/09/2026`,
      account: `Terminal ${(index % 3) + 1}`,
      type:
        index % 4 === 0
          ? 'Venda fiado com mercadoria contada e entrega ao cliente'
          : 'Pagamento de conta',
      cents: (index * 937_13) % 99_999_999,
      status: (['Active', 'Draft', 'Cancelled'] as const)[index % 3]!,
      automatic: index % 5 === 0,
    };
  });
}

function TableSection() {
  const [total, setTotal] = useState('137');
  const [envelope, setEnvelope] = useState({ page: 1, pageSize: 20 });
  const [events, setEvents] = useState<string[]>([]);
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const [kind, setKind] = useState<TableState['kind']>('ready');
  const [staged, setStaged] = useState<string | null>('all');
  const [applied, setApplied] = useState<string | null>('all');
  const heading = useRef(null);
  const totalCount = Math.max(0, Number(total) || 0);
  const totalPages = Math.ceil(totalCount / envelope.pageSize);
  const page: PageEnvelope = {
    page: envelope.page,
    pageSize: envelope.pageSize,
    totalCount,
    totalPages,
    hasNext: envelope.page < totalPages,
    hasPrevious: envelope.page > 1,
  };
  const rows = makeRows(totalCount, envelope.page, envelope.pageSize).filter(
    (row) => applied === 'all' || row.account === applied,
  );
  const log = (event: string) => setEvents((current) => [...current.slice(-5), event]);
  const state: TableState =
    kind === 'loading'
      ? { kind: 'loading', rows: 4 }
      : kind === 'empty' || rows.length === 0
        ? {
            kind: 'empty',
            empty: applied === 'all' ? 'no-records' : 'no-matches',
            statement:
              applied === 'all' ? 'Nenhuma transação hoje.' : 'Nenhuma transação neste filtro.',
            action:
              applied === 'all' ? undefined : (
                <Button
                  label="Limpar filtros"
                  variant="link"
                  onPress={() => {
                    setStaged('all');
                    setApplied('all');
                  }}
                />
              ),
          }
        : kind === 'error'
          ? {
              kind: 'error',
              title: 'Não foi possível carregar as transações',
              messages: ['Mensagem verbatim do servidor (exemplo técnico).'],
              onRetry: () => setKind('ready'),
            }
          : { kind };
  return (
    <>
      <Example
        title="FilterBar"
        behavior="Aplica ao enviar; resumo do escopo aplicado; Filtros (n) em telas estreitas."
      >
        <FilterBar
          title="Filtros de transações"
          appliedSummary={[applied === 'all' ? 'padrão — hoje, todas as contas' : (applied ?? '')]}
          activeCount={applied === 'all' ? 0 : 1}
          dirty={staged !== applied}
          onApply={() => {
            setApplied(staged);
            setEnvelope((current) => ({ ...current, page: 1 }));
          }}
          onClear={() => {
            setStaged('all');
            setApplied('all');
          }}
          resultAnnouncement={`${formatCount(rows.length)} transações`}
          testID="filter-bar"
        >
          <FormField label="Conta">
            <Select
              options={[
                { value: 'all', label: 'Todas as contas' },
                ...['Terminal 1', 'Terminal 2', 'Terminal 3'].map((value) => ({
                  value,
                  label: value,
                })),
              ]}
              value={staged}
              onValueChange={setStaged}
              testID="filter-account"
            />
          </FormField>
        </FilterBar>
      </Example>
      <Example
        title="Tabela (registro inteiro + link explícito + ação aninhada)"
        behavior="Tocar a linha abre; Ver parcelas e a seleção nunca abrem a linha; colunas não somem — a tabela rola na própria região."
      >
        <FormField label="Total de registros (exemplo arbitrário)">
          <TextField value={total} onChangeText={setTotal} testID="table-total" />
        </FormField>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.sm }}>
          {(['ready', 'loading', 'updating', 'empty', 'error'] as const).map((value) => (
            <Button
              key={value}
              label={value}
              variant={kind === value ? 'primary' : 'secondary'}
              onPress={() => setKind(value)}
            />
          ))}
        </View>
        <AppText
          ref={heading}
          variant="helper"
          tone="secondary"
        >{`${formatCount(selected.size)} selecionadas`}</AppText>
        <DataTable<LedgerRow>
          testID="ledger"
          caption="Transações de exemplo"
          rows={rows}
          rowKey={(row) => row.id}
          state={state}
          columns={[
            { key: 'date', header: 'Data', minWidth: 124, render: (row) => row.date },
            { key: 'account', header: 'Conta', minWidth: 112, render: (row) => row.account },
            {
              key: 'type',
              header: 'Tipo',
              minWidth: 200,
              render: (row) => (
                <View style={{ gap: spacing.stack.xs }}>
                  <AppText variant="table">{row.type}</AppText>
                  {row.automatic ? <Badge kind="automatic" /> : null}
                </View>
              ),
            },
            {
              key: 'value',
              header: 'Valor',
              numeric: true,
              minWidth: 152,
              render: (row) => formatBrl(asCents(row.cents)),
            },
            {
              key: 'status',
              header: 'Situação',
              minWidth: 120,
              render: (row) => (
                <Badge kind="status" domain="transaction-status" value={row.status} />
              ),
            },
          ]}
          link={{
            label: 'Abrir',
            accessibleName: (row) =>
              `transação ${row.date} ${formatBrl(asCents(row.cents))} ${row.account}`,
            onOpen: (row) => log(`abrir ${row.id}`),
          }}
          actions={(row) =>
            row.automatic ? (
              <Button
                label="Ver parcelas"
                variant="link"
                onPress={() => log(`parcelas ${row.id}`)}
                testID={`ledger-nested-${row.id}`}
              />
            ) : null
          }
          selection={{
            selected,
            label: (row) => `Selecionar ${row.date} ${row.account}`,
            onToggle: (key, next) =>
              setSelected((current) => {
                const copy = new Set(current);
                if (next) copy.add(key);
                else copy.delete(key);
                return copy;
              }),
          }}
          footer={
            <AppText
              variant="helper"
              tone="secondary"
            >{`Subtotal desta página: ${formatBrl(asCents(rows.reduce((sum, row) => sum + row.cents, 0)))}`}</AppText>
          }
        />
        <Pagination
          envelope={page}
          onPageChange={(next) => setEnvelope((current) => ({ ...current, page: next }))}
          listName="transações"
          pageSizes={[10, 20, 50]}
          onPageSizeChange={(pageSize) => setEnvelope({ page: 1, pageSize })}
          focusTarget={heading}
          testID="pagination"
        />
        <EventLog events={events} testID="ledger-events" />
      </Example>
      <Example title="Lista agrupada (telas estreitas) e grupos expansíveis">
        <GroupedList
          caption="Fila de trabalho"
          groups={[
            {
              heading: 'Aguardando aprovação',
              count: 2,
              items: [
                {
                  key: 'a',
                  primary: 'Terminal 1 · Ana Lima',
                  secondary: <Badge kind="status" domain="daily-close-status" value="Submitted" />,
                  trailing: formatSignedBrl(asCents(-1200)),
                  onOpen: () => log('conferir a'),
                  openLabel: 'Conferir',
                },
                {
                  key: 'b',
                  primary: 'Terminal 2 · João Araújo',
                  secondary: 'Rascunho · sem envio',
                  trailing: formatSignedBrl(asCents(0)),
                  onOpen: () => log('conferir b'),
                  openLabel: 'Conferir',
                  actions: (
                    <Button
                      label="Lembrar operador"
                      variant="link"
                      onPress={() => log('lembrar b')}
                    />
                  ),
                },
              ],
            },
            {
              heading: 'Plano de cheques #7',
              count: 3,
              expandable: true,
              items: [1, 2, 3].map((position) => ({
                key: `p${position}`,
                primary: `Parcela ${position}/3`,
                trailing: formatBrl(asCents(30000)),
              })),
            },
          ]}
        />
      </Example>
    </>
  );
}

function OverlaySection() {
  const [dialog, setDialog] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [drawer, setDrawer] = useState(false);
  const [sheet, setSheet] = useState(false);
  const [reason, setReason] = useState('');
  const [account, setAccount] = useState<string | null>(null);
  const [tab, setTab] = useState<'debt' | 'credit'>('debt');
  const [mode, setMode] = useState<'gestao' | 'turno'>('gestao');
  const [events, setEvents] = useState<string[]>([]);
  const first = useRef<InputHandle>(null);
  const dialogTrigger = useRef(null);
  const confirmTrigger = useRef(null);
  const log = (event: string) => setEvents((current) => [...current.slice(-5), event]);
  return (
    <>
      <Example
        title="Diálogo com formulário e seleção aninhada"
        behavior="Foco vai ao primeiro campo e volta ao gatilho; a seleção abre como camada dentro do mesmo modal; voltar do Android fecha a camada de cima primeiro."
      >
        <Button
          ref={dialogTrigger}
          label="Novo cliente"
          variant="secondary"
          onPress={() => {
            log('diálogo aberto');
            setDialog(true);
          }}
          testID="open-dialog"
        />
        <Overlay
          visible={dialog}
          onDismiss={() => setDialog(false)}
          title="Novo cliente"
          initialFocus={first}
          returnFocus={dialogTrigger}
          testID="dialog"
          footer={
            <>
              <Button
                label="Salvar cliente"
                onPress={() => {
                  log('salvar cliente');
                  setDialog(false);
                }}
                fullWidth
              />
              <Button
                label="Voltar"
                variant="secondary"
                onPress={() => setDialog(false)}
                fullWidth
              />
            </>
          }
        >
          <FormField label="Motivo">
            <TextField ref={first} value={reason} onChangeText={setReason} testID="dialog-reason" />
          </FormField>
          <FormField label="Conta">
            <Select
              options={['Terminal 1', 'Terminal 2', 'Fiado Terminal 1'].map((value) => ({
                value,
                label: value,
              }))}
              value={account}
              onValueChange={setAccount}
              testID="dialog-select"
            />
          </FormField>
        </Overlay>
      </Example>
      <Example
        title="Confirmação destrutiva com escrita em andamento"
        behavior="Ocupado não fecha por voltar, fundo ou Fechar."
      >
        <Button
          ref={confirmTrigger}
          label="Cancelar transação"
          variant="danger"
          onPress={() => setConfirm(true)}
          testID="open-confirm"
        />
        <ConfirmDialog
          visible={confirm}
          title="Cancelar esta transação?"
          consequence="A transação sai dos totais do dia. Esta ação não pode ser desfeita."
          confirmLabel="Cancelar transação"
          busyLabel="Cancelando…"
          destructive
          busy={busy}
          returnFocus={confirmTrigger}
          testID="confirm"
          onConfirm={() => {
            setBusy(true);
            setTimeout(() => {
              setBusy(false);
              setConfirm(false);
              log('cancelada (exemplo)');
            }, 20000);
          }}
          onDismiss={() => setConfirm(false)}
        />
      </Example>
      <Example title="Gaveta e folha inferior">
        <Button
          label="Abrir menu de navegação"
          variant="secondary"
          onPress={() => setDrawer(true)}
          testID="open-drawer"
        />
        <Overlay
          visible={drawer}
          onDismiss={() => setDrawer(false)}
          title="Menu de navegação"
          variant="drawer"
          testID="drawer"
        >
          {['Fila de trabalho', 'Transações', 'Relatórios'].map((entry) => (
            <Button
              key={entry}
              label={entry}
              variant="link"
              role="link"
              onPress={() => {
                setDrawer(false);
                log(entry);
              }}
            />
          ))}
        </Overlay>
        <Button
          label="Abrir folha"
          variant="secondary"
          onPress={() => {
            log('folha aberta');
            setSheet(true);
          }}
          testID="open-sheet"
        />
        <Overlay
          visible={sheet}
          onDismiss={() => setSheet(false)}
          title="Folha inferior"
          variant="bottom-sheet"
          testID="sheet"
        >
          <AppText>Arraste a alça para baixo, toque no fundo ou use Fechar.</AppText>
          <FormField label="Observação">
            <TextField value={reason} onChangeText={setReason} testID="sheet-input" />
          </FormField>
        </Overlay>
      </Example>
      <Example
        title="Menu da sessão"
        behavior="Grupo de modo como rádio; item indisponível continua focável com motivo; item ocupado."
      >
        <Menu
          testID="session-menu"
          triggerLabel="Ana Lima"
          triggerAccessibilityLabel="Abrir menu da conta"
          title="Conta"
          header={
            <AppText tone="secondary">ana@example.invalid · Filial Centro · Administrador</AppText>
          }
          groups={[
            {
              heading: 'Modo',
              items: [
                {
                  key: 'gestao',
                  label: 'Gestão',
                  kind: 'radio',
                  checked: mode === 'gestao',
                  onSelect: () => setMode('gestao'),
                },
                {
                  key: 'turno',
                  label: 'Meu turno',
                  kind: 'radio',
                  checked: mode === 'turno',
                  onSelect: () => setMode('turno'),
                },
              ],
            },
            {
              items: [
                {
                  key: 'conta',
                  label: 'Minha conta',
                  kind: 'link',
                  onSelect: () => log('minha conta'),
                },
                {
                  key: 'plataforma',
                  label: 'Plataforma',
                  kind: 'link',
                  unavailableReason: 'Disponível apenas para operadores da plataforma',
                  fix: { label: 'Saiba mais', onPress: () => log('saiba mais') },
                },
                { key: 'sair', label: 'Sair', busyLabel: undefined, onSelect: () => log('sair') },
              ],
            },
          ]}
        />
      </Example>
      <Example title="Abas">
        <Tabs
          tabs={[
            { key: 'debt', label: 'Dívidas em aberto', count: 12 },
            { key: 'credit', label: 'Saldo crédito', count: 3 },
          ]}
          value={tab}
          onValueChange={setTab}
          accessibilityLabel="Saldo fiado"
          testID="tabs"
        />
        <AppText>
          {tab === 'debt'
            ? 'Painel: dívidas (total próprio da aba).'
            : 'Painel: créditos (total próprio da aba).'}
        </AppText>
      </Example>
      <EventLog events={events} testID="overlay-events" />
    </>
  );
}

const saveStates: readonly SaveState[] = [
  'saved',
  'never',
  'dirty',
  'saving',
  'unsaved-server',
  'failed',
  'uncertain',
  'stale',
  'device-only',
  'storage-failed',
];

function FeedbackSection() {
  const toast = useToast();
  const [save, setSave] = useState<SaveState>('never');
  const [summary, setSummary] = useState(false);
  const [value, setValue] = useState('');
  const [note, setNote] = useState('');
  const field = useRef<InputHandle>(null);
  return (
    <>
      <Example
        title="Notice"
        behavior="Mensagens do servidor verbatim, em ordem, sem fundir duplicadas."
      >
        <Notice
          kind="danger"
          title="Não foi possível salvar"
          messages={[
            'O valor da transação deve ser maior que zero',
            'O valor da transação deve ser maior que zero',
            'A conta está inativa',
          ]}
          testID="notice-danger"
        />
        <Notice kind="danger" title="Erro" testID="notice-fallback" />
        <UncertainNotice
          recovery={
            <Button
              label="Verificar e tentar novamente"
              variant="secondary"
              onPress={() => undefined}
            />
          }
        />
        <Notice
          kind="info"
          title="Entrada recuperada"
          body="Restauramos a contagem guardada neste aparelho."
        />
        <Notice kind="success" title="Convite reenviado" />
        <Notice kind="neutral" title="Período bloqueado" body="Bloqueado até 31/08/2026." />
      </Example>
      <Example title="Resumo de erros com link para o campo">
        <Button
          label="Simular envio com erro"
          variant="secondary"
          onPress={() => setSummary(true)}
          testID="error-summary-trigger"
        />
        {summary ? (
          <ErrorSummary
            messages={[
              {
                text: 'O valor da transação deve ser maior que zero',
                field: { label: 'Valor', focus: () => field.current?.focus() },
              },
              { text: 'Mensagem sem campo associado.' },
            ]}
            testID="error-summary"
          />
        ) : null}
        <FormField
          label="Valor"
          error={summary ? 'O valor da transação deve ser maior que zero' : undefined}
        >
          <MoneyInput ref={field} value={value} onValueChange={setValue} />
        </FormField>
      </Example>
      <Example
        title="Toast (somente sucesso confirmado)"
        behavior="≥ 5 s, pausa ao tocar, um por vez (o novo substitui), anunciado sem tomar o foco; acima de um teclado já aberto."
      >
        <FormField label="Campo para abrir o teclado">
          <TextField value={note} onChangeText={setNote} testID="toast-keyboard-field" />
        </FormField>
        <Button
          label="Mostrar toast curto"
          variant="secondary"
          onPress={() => toast.show('Transação salva')}
          testID="toast-short"
        />
        <Button
          label="Mostrar toast longo"
          variant="secondary"
          onPress={() =>
            toast.show(
              'Fechamento enviado para aprovação do gerente da filial com as contagens conferidas',
            )
          }
          testID="toast-long"
        />
      </Example>
      <Example title="SaveStatus">
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.inline.xs }}>
          {saveStates.map((state) => (
            <Button
              key={state}
              label={state}
              variant={state === save ? 'primary' : 'secondary'}
              onPress={() => setSave(state)}
            />
          ))}
        </View>
        <SaveStatus
          state={save}
          savedAt={{ time: '14:32' }}
          onSave={() => setSave('saving')}
          onRetry={() => setSave('saving')}
          onReload={() => setSave('saved')}
          testID="save-status"
        />
      </Example>
      <Example
        title="Skeleton e EmptyState"
        behavior="Sem brilho com movimento reduzido; tudo certo é neutro, não sucesso."
      >
        <Skeleton lines={3} testID="skeleton" />
        <EmptyState
          kind="no-records"
          statement="Nenhuma transação hoje."
          primary={<Button label="Nova transação" onPress={() => undefined} />}
        />
        <EmptyState
          kind="no-matches"
          statement="Nenhuma transação neste filtro."
          explanation="24/09/2026 · Terminal 2 · Rascunho"
          primary={<Button label="Limpar filtros" variant="link" onPress={() => undefined} />}
        />
        <EmptyState kind="all-clear" statement="Tudo certo — nada pendente de aprovação." />
        <EmptyState
          kind="setup-needed"
          statement="Você ainda não tem um terminal atribuído."
          primary={
            <Button label="Solicitar vínculo" variant="secondary" onPress={() => undefined} />
          }
        />
      </Example>
      <Example title="Stepper (status confirmados)">
        <Stepper
          steps={[
            { title: 'Dados da pessoa', status: 'done', detail: 'Operador criado: Ana Lima' },
            {
              title: 'Acesso',
              status: 'failed',
              detail: 'O e-mail já está em uso nesta filial',
              onRetry: () => undefined,
            },
            { title: 'Terminal', status: 'uncertain', onVerify: () => undefined },
            { title: 'Contas', status: 'current' },
            { title: 'Convite', status: 'skipped' },
            {
              title: 'Remover acesso próprio',
              status: 'unavailable',
              detail: 'Você não pode remover o próprio acesso',
            },
          ]}
        />
      </Example>
      <Example title="Disclosure">
        <Disclosure label="Ajuda sobre diferença de caixa" summary="fórmula geral">
          <AppText>
            Diferença = contagem de hoje − abertura − entradas + saídas (texto de ajuda de exemplo).
          </AppText>
        </Disclosure>
        <Disclosure
          label="Ver o que mudou"
          unavailableText="Detalhes da alteração indisponíveis para este fechamento"
        />
      </Example>
      <Example title="PageHeader">
        <PageHeader
          title="Fechamento do dia"
          context="Terminal 2 · quinta-feira, 24 de setembro de 2026 · Rascunho · Meu turno"
          origin={{ label: 'Fila de trabalho', onPress: () => undefined, trail: ['Conferência'] }}
          badge={<Badge kind="status" domain="daily-close-status" value="Draft" />}
          actions={<Button label="Salvar" onPress={() => undefined} />}
          focusOnMount={false}
        />
      </Example>
    </>
  );
}

export const dataSections: readonly GallerySectionLike[] = [
  { key: 'badge', title: 'Badge', contract: 'badge.md', render: BadgeSection },
  {
    key: 'structure',
    title: 'Card, SummaryStrip e DetailList',
    contract: 'card.md · summary-strip.md · detail-list.md',
    render: StructureSection,
  },
  {
    key: 'table',
    title: 'Table/List, FilterBar e Pagination',
    contract: 'table.md · filter-bar.md · pagination.md',
    render: TableSection,
  },
  {
    key: 'overlay',
    title: 'Modal/Sheet, Menu e Tabs',
    contract: 'modal-sheet.md · menu.md · tabs.md',
    render: OverlaySection,
  },
  {
    key: 'feedback',
    title: 'Notice, Toast, SaveStatus, Skeleton, EmptyState, Stepper, Disclosure, PageHeader',
    contract:
      'notice.md · toast.md · save-status.md · skeleton.md · empty-state.md · stepper.md · disclosure.md · page-header.md',
    render: FeedbackSection,
  },
];
