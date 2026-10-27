'use client';

import { useRef, useState } from 'react';
import {
  formatBrlWire,
  formatDate,
  parseCalendarDate,
  transactionStatusLabels,
  type CalendarDate,
} from '@loto/core';
import { FeedbackBadge, GeneratedMarker, ModeBadge, StatusBadge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, ChecklistItem } from '@/components/ui/card';
import { Checkbox } from '@/components/ui/choice';
import { DetailList } from '@/components/ui/detail-list';
import { EmptyState } from '@/components/ui/empty-state';
import { FilterBar } from '@/components/ui/filter-bar';
import { FormField, FormGrid } from '@/components/ui/form-field';
import { NativeSelect } from '@/components/ui/select';
import { Notice } from '@/components/ui/notice';
import { PageHeader } from '@/components/ui/page-header';
import { Pagination, type PageEnvelope } from '@/components/ui/pagination';
import { Skeleton } from '@/components/ui/skeleton';
import { SummaryStrip } from '@/components/ui/summary-strip';
import {
  GroupedList,
  GroupedListItem,
  RowLink,
  Table,
  TableBody,
  TableHead,
  TableMessageRow,
  TableRegion,
  Td,
  Th,
  Tr,
} from '@/components/ui/table';
import { Example, Section, ThemePair } from './gallery-kit';

// Illustrative rows (labelled **Dados de exemplo**), formatted by `@loto/core` like API data.
const day = (iso: string) => formatDate(parseCalendarDate(iso) as CalendarDate);

const rows = [
  {
    id: '1',
    date: day('2026-09-24'),
    type: 'Venda de bolão',
    account: 'Terminal 2',
    value: formatBrlWire(943.1),
    status: 'Active',
    generated: false,
  },
  {
    id: '2',
    date: day('2026-09-24'),
    type: 'Recebimento de fiado',
    account: 'Terminal 2',
    value: formatBrlWire(80),
    status: 'Draft',
    generated: true,
  },
  {
    id: '3',
    date: day('2026-09-23'),
    type: 'Saque de prêmio com descrição longa para testar a quebra de linha',
    account: 'Banco do Brasil',
    value: formatBrlWire(2437),
    status: 'Cancelled',
    generated: false,
  },
] as const;

export function DataSections() {
  const [selected, setSelected] = useState<Set<string>>(new Set(['2']));
  const [tableState, setTableState] = useState<'rows' | 'loading' | 'empty' | 'error' | 'updating'>(
    'rows',
  );
  const [envelope, setEnvelope] = useState<PageEnvelope>({
    page: 2,
    pageSize: 20,
    totalCount: 137,
    totalPages: 7,
    hasNext: true,
    hasPrevious: true,
  });
  const heading = useRef<HTMLHeadingElement>(null);
  const [applied, setApplied] = useState('24/09/2026 · padrão — todas as contas, todos os tipos');
  const [dirty, setDirty] = useState(false);

  return (
    <>
      <Section id="badge" title="Badge" contract="design/specs/components/badge.md">
        <ThemePair>
          {() => (
            <div className="flex flex-wrap items-center gap-inline-sm">
              <StatusBadge domain="daily-close-status" value="Submitted" context="Situação">
                Enviado
              </StatusBadge>
              <StatusBadge domain="aging-bucket" value="Days91Plus" dot>
                Mais de 90 dias
              </StatusBadge>
              <GeneratedMarker />
              <FeedbackBadge kind="info">você</FeedbackBadge>
              <FeedbackBadge kind="neutral">Inativa</FeedbackBadge>
              <ModeBadge mode="gestao" />
              <ModeBadge mode="turno" />
              <StatusBadge domain="transaction-status" value="Archived">
                Archived
              </StatusBadge>
            </div>
          )}
        </ThemePair>
        <p className="type-helper text-text-muted">
          O último é um valor desconhecido: texto cru, neutro, relatado como divergência de
          contrato. Todos os valores de domínio estão em “Cores de domínio”.
        </p>
      </Section>

      <Section id="card" title="Card" contract="design/specs/components/card.md">
        <FormGrid>
          <Card
            variant="next-action"
            eyebrow="Próximo passo"
            title="Abrir o dia do Terminal 2"
            actions={
              <>
                <Button variant="cta">Abrir dia</Button>
                <Button variant="text">Ver fechamento de hoje</Button>
              </>
            }
          >
            <p className="type-body text-text-secondary">
              A abertura repete a contagem do fechamento anterior.
            </p>
          </Card>
          <Card title="Configuração inicial" variant="checklist">
            <ul>
              <ChecklistItem label="Operadores" status="Concluído" />
              <ChecklistItem
                label="Contas dos operadores"
                status="Pendente"
                action={
                  <Button variant="text" size="small">
                    Resolver
                  </Button>
                }
              />
              <ChecklistItem label="Feriados" status="Não foi possível verificar" />
            </ul>
          </Card>
          <Card title="Carregando">
            <Skeleton className="w-3/4" />
            <Skeleton className="w-1/2" />
          </Card>
          <Card title="Com erro" accent="danger">
            <Notice
              kind="danger"
              title="Não foi possível carregar"
              messages={['Erro desconhecido']}
            />
          </Card>
        </FormGrid>
      </Section>

      <Section
        id="summary-strip"
        title="SummaryStrip"
        contract="design/specs/components/summary-strip.md"
      >
        <Example title="Totais, explicativos e indisponível" fixture>
          <SummaryStrip
            label="Prévia da diferença · ainda não enviada"
            variant="explanatory"
            items={[
              {
                label: 'Total do fechamento',
                figure: { kind: 'value', value: formatBrlWire(2437) },
                caption: 'ainda não enviada',
              },
              { label: 'Total da abertura', figure: { kind: 'value', value: formatBrlWire(2199) } },
              { label: 'Entradas', figure: { kind: 'unavailable', retry: () => undefined } },
              { label: 'Saídas', figure: { kind: 'loading' } },
            ]}
          />
        </Example>
      </Section>

      <Section
        id="detail-list"
        title="DetailList"
        contract="design/specs/components/detail-list.md"
      >
        <DetailList
          groups={[
            [
              {
                label: 'Aberto por',
                value: 'Lucas Ferreira · operador Lucas Ferreira · 24/09/2026 08:02',
              },
              { label: 'Contado por', value: 'Sem operador associado ao registro' },
              { label: 'Enviado por', value: null },
            ],
            [
              { label: 'Abertura', value: `${formatBrlWire(2199)} · calculado pelo sistema` },
              { label: 'Situação', value: 'Período bloqueado até 31/08/2026', restriction: true },
            ],
          ]}
        />
      </Section>

      <Section id="table" title="Table / List" contract="design/specs/components/table.md">
        <FormField label="Estado da tabela (cenário de teste)">
          <NativeSelect
            value={tableState}
            onChange={(event) => setTableState(event.target.value as typeof tableState)}
            options={[
              { value: 'rows', label: 'Com linhas' },
              { value: 'loading', label: 'Carregando' },
              { value: 'empty', label: 'Vazia (sem resultados no filtro)' },
              { value: 'error', label: 'Erro' },
              { value: 'updating', label: 'Atualizando' },
            ]}
          />
        </FormField>
        <TableRegion
          headingRef={heading}
          caption="Transações de 24/09/2026 · Terminal 2"
          scrollLabel="Tabela de transações, role horizontalmente"
          updating={tableState === 'updating'}
          toolbar={
            <p className="type-helper text-text-secondary">{`${selected.size} selecionada(s) para cancelamento`}</p>
          }
          footer={
            <Pagination
              envelope={envelope}
              listName="transações"
              onPageChange={(page) =>
                setEnvelope({
                  ...envelope,
                  page,
                  hasPrevious: page > 1,
                  hasNext: page < envelope.totalPages,
                })
              }
              onNavigated={() => heading.current?.focus()}
            />
          }
        >
          <Table caption="Transações de 24/09/2026 · Terminal 2" className="min-w-[48rem]">
            <TableHead>
              <tr>
                <Th>
                  <span className="sr-only">Selecionar</span>
                </Th>
                <Th>Data</Th>
                <Th>Tipo</Th>
                <Th>Conta</Th>
                <Th align="end">Valor</Th>
                <Th>Situação</Th>
                <Th sticky="end">Ações</Th>
              </tr>
            </TableHead>
            <TableBody>
              {tableState === 'loading' ? (
                [0, 1, 2].map((index) => (
                  <TableMessageRow key={index} columns={7}>
                    <Skeleton className="my-inset-table-cell-y w-full" />
                  </TableMessageRow>
                ))
              ) : tableState === 'empty' ? (
                <TableMessageRow columns={7}>
                  <EmptyState
                    kind="no-matches"
                    statement="Nenhuma transação neste filtro."
                    explanation="24/09/2026 · Terminal 2 · Rascunho"
                    primaryAction={<Button variant="secondary">Limpar filtros</Button>}
                  />
                </TableMessageRow>
              ) : tableState === 'error' ? (
                <TableMessageRow columns={7}>
                  <div className="py-stack-md">
                    <Notice
                      kind="danger"
                      title="Não foi possível carregar as transações"
                      messages={['Erro desconhecido']}
                      actions={<Button variant="secondary">Tentar novamente</Button>}
                    />
                  </div>
                </TableMessageRow>
              ) : (
                rows.map((row) => {
                  const isSelected = selected.has(row.id);
                  return (
                    <Tr
                      key={row.id}
                      href={`/styleguide?transacao=${row.id}#table`}
                      selected={isSelected}
                      muted={row.status === 'Cancelled'}
                    >
                      <Td>
                        <Checkbox
                          label={
                            <span className="sr-only">{`Selecionar ${row.type} ${row.value}`}</span>
                          }
                          checked={isSelected}
                          onChange={() => {
                            const next = new Set(selected);
                            if (next.has(row.id)) next.delete(row.id);
                            else next.add(row.id);
                            setSelected(next);
                          }}
                        />
                      </Td>
                      <Td>{row.date}</Td>
                      <Td>
                        <span className="flex flex-wrap items-center gap-inline-sm">
                          {row.type}
                          {row.generated ? <GeneratedMarker /> : null}
                        </span>
                      </Td>
                      <Td>{row.account}</Td>
                      <Td align="end">{row.value}</Td>
                      <Td>
                        <StatusBadge domain="transaction-status" value={row.status} />
                      </Td>
                      <Td sticky="end">
                        <span className="flex flex-wrap gap-inline-sm">
                          <RowLink
                            href={`/styleguide?transacao=${row.id}#table`}
                            label="Abrir transação"
                            accessibleName={`Abrir transação ${row.date} ${row.value} ${row.account}`}
                          />
                          <Button variant="text" size="small">
                            Ver parcelas
                          </Button>
                        </span>
                      </Td>
                    </Tr>
                  );
                })
              )}
            </TableBody>
          </Table>
        </TableRegion>
        <Example title="Lista agrupada (compacta)" fixture>
          <GroupedList label="Fila de trabalho">
            {rows.map((row) => (
              <GroupedListItem
                key={row.id}
                primary={
                  <RowLink
                    href={`/styleguide?transacao=${row.id}#table`}
                    label={row.type}
                    accessibleName={`Abrir ${row.type} ${row.value}`}
                  />
                }
                secondary={`${row.account} · ${row.value} · ${transactionStatusLabels[row.status]}`}
                trailing={
                  <Button variant="text" size="small">
                    Conferir
                  </Button>
                }
              />
            ))}
          </GroupedList>
        </Example>
      </Section>

      <Section id="filter-bar" title="FilterBar" contract="design/specs/components/filter-bar.md">
        <FilterBar
          label="Filtros de transações"
          scopeSummary={applied}
          activeCount={dirty ? 2 : 1}
          dirty={dirty}
          onApply={() => {
            setApplied('24/09/2026 · Terminal 2 · Rascunho · Somente minhas');
            setDirty(false);
          }}
          onClear={() => setApplied('24/09/2026 · padrão — todas as contas, todos os tipos')}
          resultsAnnouncement="3 transações"
          fields={
            <FormGrid>
              <FormField label="Conta">
                <NativeSelect
                  onChange={() => setDirty(true)}
                  options={[
                    { value: '', label: 'Todas as contas' },
                    { value: 't2', label: 'Terminal 2' },
                  ]}
                />
              </FormField>
              <FormField label="Situação">
                <NativeSelect
                  onChange={() => setDirty(true)}
                  options={[
                    { value: '', label: 'Todas' },
                    { value: 'Draft', label: 'Rascunho' },
                  ]}
                />
              </FormField>
            </FormGrid>
          }
        />
      </Section>

      <Section id="pagination" title="Pagination" contract="design/specs/components/pagination.md">
        <p className="type-helper text-text-muted">
          Exemplo ao pé da tabela acima: guiado só pelo envelope; ao mudar de página o foco vai ao
          título da tabela.
        </p>
      </Section>

      <Section
        id="page-header"
        title="PageHeader"
        contract="design/specs/components/page-header.md"
      >
        <div className="rounded-panel border border-border-default bg-surface-default p-inset-panel">
          <PageHeader
            focusOnMount={false}
            title="Conferência"
            origin={[{ label: 'Fila de trabalho', href: '/styleguide#page-header' }]}
            context="Terminal 2 · quinta-feira, 24 de setembro de 2026 · Enviado · Gestão"
            status={
              <StatusBadge domain="daily-close-status" value="Submitted">
                Enviado
              </StatusBadge>
            }
            actions={
              <>
                <Button variant="danger">Rejeitar</Button>
                <Button>Aprovar</Button>
              </>
            }
          />
        </div>
      </Section>
    </>
  );
}
