'use client';

import { useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog, Dialog } from '@/components/ui/dialog';
import { DateField } from '@/components/ui/date-picker';
import { Disclosure } from '@/components/ui/disclosure';
import { EmptyState } from '@/components/ui/empty-state';
import { FormField, FormGrid } from '@/components/ui/form-field';
import type { CalendarDate } from '@/components/ui/format-port';
import { Input } from '@/components/ui/input';
import {
  Menu,
  MenuGroup,
  MenuItem,
  MenuLink,
  MenuRadioItem,
  MenuSeparator,
  MenuUnavailable,
} from '@/components/ui/menu';
import { ErrorSummary, NoBodyNotice, Notice, UncertainNotice } from '@/components/ui/notice';
import { SaveBar, SaveStatus, type SaveState } from '@/components/ui/save-status';
import { Select, type SelectOption } from '@/components/ui/select';
import { LoadingRegion, Skeleton } from '@/components/ui/skeleton';
import { Stepper } from '@/components/ui/stepper';
import { SegmentedControl, Tabs } from '@/components/ui/tabs';
import { useToast } from '@/components/ui/toast';
import { Example, Section, ThemePair } from './gallery-kit';
import { fixtureToday } from './input-sections';

const types: SelectOption[] = [
  { value: 'v', label: 'Venda de bolão' },
  { value: 'r', label: 'Recebimento de fiado' },
  { value: 's', label: 'Saque de prêmio' },
];

const saveStates: readonly SaveState[] = [
  { kind: 'saved', time: '14:32' },
  { kind: 'saved', time: '18:42', date: '23/09' },
  { kind: 'never-recorded' },
  { kind: 'dirty' },
  { kind: 'saving' },
  { kind: 'unsaved-on-server' },
  { kind: 'failed' },
  { kind: 'uncertain' },
  { kind: 'stale' },
  { kind: 'device-only' },
  { kind: 'storage-failed' },
];

export function OverlaySections() {
  const toast = useToast();
  const [dialog, setDialog] = useState(false);
  const [confirm, setConfirm] = useState(false);
  const [confirmBusy, setConfirmBusy] = useState(false);
  const [drawer, setDrawer] = useState(false);
  const [sheet, setSheet] = useState(false);
  const [type, setType] = useState<string | null>(null);
  const [due, setDue] = useState<CalendarDate | null>(null);
  const [mode, setMode] = useState<'gestao' | 'turno'>('gestao');
  const [tab, setTab] = useState('debt');
  const [density, setDensity] = useState<'comfortable' | 'compact'>('comfortable');
  const [saveIndex, setSaveIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const heading = useRef<HTMLHeadingElement>(null);

  return (
    <>
      <Section
        id="modal-sheet"
        title="Modal / Sheet"
        contract="design/specs/components/modal-sheet.md"
      >
        <p className="type-helper text-text-muted">
          Esc, “Fechar” e o fundo fecham pelo mesmo caminho; o foco fica preso e volta ao botão que
          abriu. A lista e o calendário dentro do diálogo abrem acima dele (camada de popover).
        </p>
        <div className="flex flex-wrap gap-inline-md">
          <Button variant="secondary" onClick={() => setDialog(true)}>
            Novo cliente (diálogo com seletores)
          </Button>
          <Button variant="danger" onClick={() => setConfirm(true)}>
            Cancelar transação
          </Button>
          <Button variant="secondary" onClick={() => setDrawer(true)}>
            Menu de navegação (gaveta)
          </Button>
          <Button variant="secondary" onClick={() => setSheet(true)}>
            Folha inferior
          </Button>
        </div>
        <Dialog
          open={dialog}
          onDismiss={() => setDialog(false)}
          title="Novo cliente"
          description="O cliente fica disponível nas transações desta filial."
          footer={
            <>
              <Button variant="secondary" onClick={() => setDialog(false)}>
                Voltar
              </Button>
              <Button onClick={() => setDialog(false)}>Salvar cliente</Button>
            </>
          }
        >
          <FormGrid>
            <FormField label="Nome">
              <Input />
            </FormField>
            <FormField label="Tipo de transação">
              <Select options={types} value={type} onValueChange={setType} />
            </FormField>
            <FormField label="Vencimento">
              <DateField value={due} onValueChange={setDue} today={fixtureToday} />
            </FormField>
          </FormGrid>
        </Dialog>
        <ConfirmDialog
          open={confirm}
          busy={confirmBusy}
          busyLabel="Cancelando…"
          onDismiss={() => setConfirm(false)}
          onConfirm={() => setConfirmBusy(true)}
          title="Cancelar esta transação?"
          consequence="A transação deixa de contar nos totais do dia. Esta é uma simulação: nada é enviado."
          confirmLabel="Cancelar transação"
          destructive
          error={
            confirmBusy ? (
              <p className="type-helper text-text-muted">
                Cenário de teste: ocupado — não fecha por nenhum caminho.{' '}
                <button
                  type="button"
                  className="underline"
                  onClick={() => {
                    setConfirmBusy(false);
                    setConfirm(false);
                  }}
                >
                  Encerrar cenário
                </button>
              </p>
            ) : null
          }
        />
        <Dialog
          open={drawer}
          onDismiss={() => setDrawer(false)}
          title="Lotero"
          aria-label="Menu de navegação"
          variant="drawer"
        >
          <nav aria-label="Principal" data-on-nav="" className="flex flex-col gap-stack-xs">
            <span className="type-overline text-nav-text-muted">Gestão</span>
            {['Fila de trabalho', 'Transações', 'Nova transação'].map((item, index) => (
              <a
                key={item}
                href="#modal-sheet"
                aria-current={index === 0 ? 'page' : undefined}
                className="flex min-h-size-touch-target-min items-center rounded-control border-s-(length:--spacing-size-marker-width) border-transparent px-inset-control-x text-nav-text hover:bg-nav-hover aria-[current=page]:border-nav-marker aria-[current=page]:bg-nav-active aria-[current=page]:text-nav-text-strong"
              >
                {item}
              </a>
            ))}
          </nav>
        </Dialog>
        <Dialog open={sheet} onDismiss={() => setSheet(false)} title="Filtros" variant="sheet">
          <p className="type-body">
            Folha inferior: até 90% da altura, alça visual e botão “Fechar”.
          </p>
        </Dialog>
      </Section>

      <Section id="menu" title="Menu" contract="design/specs/components/menu.md">
        <Example
          title="Menu da sessão"
          fixture
          note="Seta para baixo abre no primeiro item; setas circulam; digitar uma letra salta; Esc devolve o foco."
        >
          <Menu
            title="Conta"
            trigger={<span className="type-control">Ricardo Moura</span>}
            triggerLabel={`Conta de Ricardo Moura, ${mode === 'gestao' ? 'Gestão' : 'Meu turno'}`}
            header={
              <div className="flex flex-col">
                <span className="type-body-strong">Ricardo Moura</span>
                <span className="type-helper text-text-muted">
                  ricardo@exemplo.com.br · Lotérica Centro · Gerente
                </span>
              </div>
            }
          >
            <MenuGroup label="Modo de trabalho">
              <MenuRadioItem
                checked={mode === 'gestao'}
                onSelect={() => setMode('gestao')}
                label="Gestão"
                description="Fila, relatórios e cadastros"
              />
              <MenuUnavailable
                label="Meu turno"
                reason="Indisponível: seu login não está vinculado a um operador desta filial."
                fix={{ label: 'Resolver em Operadores', href: '/styleguide#menu' }}
              />
            </MenuGroup>
            <MenuGroup>
              <MenuItem onSelect={() => undefined} label="Trocar filial" />
              <MenuLink href="/styleguide#menu" label="Minha conta" />
              <MenuSeparator />
              <MenuItem onSelect={() => undefined} label="Sair" busyLabel="Saindo…" />
            </MenuGroup>
          </Menu>
        </Example>
      </Section>

      <Section
        id="tabs"
        title="Tabs e controle segmentado"
        contract="design/specs/components/tabs.md"
      >
        <Tabs
          label="Saldo fiado"
          items={[
            { id: 'debt', label: 'Dívidas em aberto', count: '12' },
            { id: 'credit', label: 'Saldo crédito', count: '3' },
            { id: 'off', label: 'Indisponível', disabled: true },
          ]}
          selected={tab}
          onSelect={setTab}
          panel={
            <EmptyState
              kind="no-records"
              statement={
                tab === 'debt' ? 'Painel de dívidas (exemplo).' : 'Painel de crédito (exemplo).'
              }
            />
          }
        />
        <ThemePair>
          {() => (
            <SegmentedControl
              label="Densidade de exemplo"
              value={density}
              onValueChange={setDensity}
              options={[
                { value: 'comfortable', label: 'Confortável' },
                { value: 'compact', label: 'Compacta' },
              ]}
            />
          )}
        </ThemePair>
      </Section>

      <Section id="disclosure" title="Disclosure" contract="design/specs/components/disclosure.md">
        <Disclosure label="Ajuda sobre diferença de caixa">
          <p className="type-body-strong">Como a diferença é calculada</p>
          <p className="type-body">
            Diferença Caixa = total do fechamento − total da abertura − (entradas − saídas)
          </p>
        </Disclosure>
        <Disclosure label="Ver o que mudou" summary="3 produtos alterados">
          <p className="type-body text-text-muted">
            Detalhes da alteração indisponíveis para este fechamento
          </p>
        </Disclosure>
      </Section>

      <Section id="stepper" title="Stepper" contract="design/specs/components/stepper.md">
        <Stepper
          label="Adicionar funcionário"
          steps={[
            { title: 'Dados do operador', status: 'done', detail: 'Operador criado: Ana Lima' },
            { title: 'Terminal', status: 'current' },
            { title: 'Convite', status: 'saving' },
            {
              title: 'Conta do operador',
              status: 'failed',
              detail: 'O e-mail já foi convidado para esta lotérica.',
              action: (
                <Button variant="text" size="small">
                  Tentar novamente
                </Button>
              ),
            },
            {
              title: 'Acesso',
              status: 'uncertain',
              action: (
                <Button variant="text" size="small">
                  Verificar e tentar novamente
                </Button>
              ),
            },
            { title: 'Revisão', status: 'skipped' },
            {
              title: 'Desligar a si mesmo',
              status: 'unavailable',
              detail: 'Você não pode remover o próprio acesso',
            },
          ]}
        />
      </Section>

      <Section
        id="notice"
        title="Notice e resumo de erros"
        contract="design/specs/components/notice.md"
      >
        <ThemePair>
          {() => (
            <div className="flex flex-col gap-stack-sm">
              <Notice
                kind="danger"
                title="Não foi possível salvar a transação"
                live="off"
                messages={['A conta informada está inativa.', 'A conta informada está inativa.']}
              />
              <UncertainNotice
                messages={['Erro desconhecido']}
                actions={<Button variant="secondary">Verificar e tentar novamente</Button>}
              />
              <Notice kind="info" title="A data da filial mudou." live="off">
                Confira os dados antes de continuar.
              </Notice>
              <Notice kind="success" title="Convite reenviado" live="off" />
              <Notice kind="neutral" title="Período bloqueado" live="off">
                Bloqueado até 31/08/2026.
              </Notice>
              <NoBodyNotice />
            </div>
          )}
        </ThemePair>
        <ErrorSummary
          focusOnMount={false}
          title="Não foi possível enviar o fechamento"
          items={[{ message: 'Existem rascunhos neste terminal.' }]}
        />
      </Section>

      <Section id="toast" title="Toast" contract="design/specs/components/toast.md">
        <Example
          title="Eco de sucesso"
          note="Cenário de teste: em uma tela, só aparece depois da confirmação do servidor. Fica ao menos 5 s e pausa sob o ponteiro ou foco."
        >
          <Button variant="secondary" onClick={() => toast.showSuccess('Transação salva')}>
            Simular confirmação do servidor
          </Button>
        </Example>
      </Section>

      <Section
        id="save-status"
        title="SaveStatus"
        contract="design/specs/components/save-status.md"
      >
        <Example
          title="Todos os estados"
          fixture
          note="Horários de exemplo; numa tela vêm do UpdatedAt confirmado pela resposta."
        >
          <ul className="flex flex-col gap-stack-xs">
            {saveStates.map((state, index) => (
              <li key={index}>
                <SaveStatus state={state} />
              </li>
            ))}
          </ul>
        </Example>
        <Example
          title="Salvar com o estado ao lado"
          note="Cenário controlado: percorra os estados."
        >
          <SaveBar
            state={saveStates[saveIndex] ?? { kind: 'dirty' }}
            clean={saveIndex <= 1}
            onSave={() => setSaveIndex(4)}
          />
          <Button
            variant="text"
            size="small"
            onClick={() => setSaveIndex((saveIndex + 1) % saveStates.length)}
          >
            Próximo estado do cenário
          </Button>
        </Example>
      </Section>

      <Section id="skeleton" title="Skeleton" contract="design/specs/components/skeleton.md">
        <Button variant="text" size="small" onClick={() => setLoading(!loading)}>
          {loading ? 'Concluir carregamento (cenário)' : 'Recarregar (cenário)'}
        </Button>
        <LoadingRegion
          loading={loading}
          skeleton={
            <div className="flex flex-col gap-stack-sm">
              <Skeleton className="w-1/3" />
              <Skeleton className="w-full" />
              <Skeleton className="w-2/3" />
            </div>
          }
        >
          <h3 ref={heading} className="type-section-title">
            Conteúdo carregado
          </h3>
        </LoadingRegion>
      </Section>

      <Section
        id="empty-state"
        title="EmptyState"
        contract="design/specs/components/empty-state.md"
      >
        <FormGrid>
          <EmptyState
            kind="no-records"
            statement="Nenhuma transação hoje."
            primaryAction={<Button>Nova transação</Button>}
          />
          <EmptyState
            kind="no-matches"
            statement="Nenhuma transação neste filtro."
            explanation="24/09/2026 · Terminal 2 · Rascunho"
            primaryAction={<Button variant="secondary">Limpar filtros</Button>}
          />
          <EmptyState kind="all-clear" statement="Tudo certo — nada pendente de aprovação." />
          <EmptyState
            kind="setup"
            statement="Você ainda não tem um terminal atribuído."
            explanation="Peça à gerência para atribuir um terminal."
            primaryAction={<Button variant="secondary">Solicitar vínculo</Button>}
          />
        </FormGrid>
      </Section>
    </>
  );
}
