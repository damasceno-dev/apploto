// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  DomainText,
  domainEnums,
  FeedbackBadge,
  GeneratedMarker,
  ModeBadge,
  StatusBadge,
} from '@/components/ui/badge';
import { Card } from '@/components/ui/card';
import { DetailList } from '@/components/ui/detail-list';
import { FilterBar } from '@/components/ui/filter-bar';
import { PageHeader } from '@/components/ui/page-header';
import { Pagination, type PageEnvelope } from '@/components/ui/pagination';
import { SummaryStrip } from '@/components/ui/summary-strip';
import {
  RowLink,
  Table,
  TableBody,
  TableHead,
  TableRegion,
  Td,
  Th,
  Tr,
} from '@/components/ui/table';
import { domainColors } from '@/theme/tokens.generated';
import { installMatchMedia, WithFormat } from '../support/format-double';

vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

beforeEach(() => installMatchMedia(false));
afterEach(cleanup);

describe('Badge', () => {
  it('every backend enum value has exactly its domain token, and the reverse', () => {
    for (const [domain, enumeration] of Object.entries(domainEnums)) {
      const tokens = Object.keys(domainColors[domain as keyof typeof domainColors].values).sort();
      expect(Object.values(enumeration).sort(), domain).toEqual(tokens);
    }
  });

  it('renders each status from its own domain variables, with optional visually hidden context', () => {
    const { container } = render(
      <>
        {Object.keys(domainColors['daily-close-status'].values).map((value) => (
          <StatusBadge key={value} domain="daily-close-status" value={value} context="Situação">
            {value}
          </StatusBadge>
        ))}
      </>,
    );
    const submitted = container.querySelector('[data-value="Submitted"]') as HTMLElement;
    expect(submitted.style.getPropertyValue('--badge-bg')).toBe(
      'var(--loto-dom-daily-close-status-Submitted-bg)',
    );
    expect(submitted.style.getPropertyValue('--badge-fg')).toBe(
      'var(--loto-dom-daily-close-status-Submitted-fg)',
    );
    expect(submitted.textContent).toBe('Situação: Submitted');
  });

  it('an unknown future value is neutral raw text and is reported, never a guessed color', () => {
    const onUnknown = vi.fn();
    render(
      <StatusBadge domain="transaction-status" value="Archived" onUnknown={onUnknown}>
        Arquivada
      </StatusBadge>,
    );
    expect(screen.getByText('Archived').className).toContain('bg-feedback-neutral-bg');
    expect(onUnknown).toHaveBeenCalledWith('transaction-status', 'Archived');
  });

  it('provenance, feedback, mode and signed-value presentations', () => {
    render(
      <>
        <GeneratedMarker />
        <FeedbackBadge kind="info">você</FeedbackBadge>
        <ModeBadge mode="turno" />
        <DomainText domain="cash-variance" value="shortage">
          −R$ 12,00 · falta
        </DomainText>
      </>,
    );
    expect(screen.getByText('Automática').className).toContain('border-(--badge-border)');
    expect(screen.getByText('você').className).toContain('text-feedback-info-fg');
    expect(screen.getByText('Meu turno').className).toContain('text-accent-secondary-fg');
    expect(
      (screen.getByText('−R$ 12,00 · falta') as HTMLElement).style.getPropertyValue('--badge-fg'),
    ).toBe('var(--loto-dom-cash-variance-shortage-fg)');
  });
});

describe('Card, SummaryStrip and DetailList', () => {
  it('the next-action card is a region named by its heading', () => {
    render(<Card variant="next-action" title="Abrir o dia do Terminal 2" />);
    expect(screen.getByRole('region', { name: 'Abrir o dia do Terminal 2' })).toBeTruthy();
  });

  it('an unavailable figure is a dash with its own retry and caption — never zero', () => {
    const retry = vi.fn();
    render(
      <SummaryStrip
        items={[
          { label: 'Total do fechamento', figure: { kind: 'value', value: 'R$ 2.437,00' } },
          { label: 'Entradas', figure: { kind: 'unavailable', retry } },
          { label: 'Saídas', figure: { kind: 'loading' } },
        ]}
      />,
    );
    const strip = screen.getAllByRole('term')[0]?.closest('dl') as HTMLElement;
    expect(strip.getAttribute('aria-busy')).toBe('true');
    expect(screen.getByText('—')).toBeTruthy();
    expect(screen.getByText('Não foi possível carregar')).toBeTruthy();
    expect(strip.textContent).not.toContain('R$ 0,00');
    fireEvent.click(screen.getByRole('button', { name: 'Tentar novamente' }));
    expect(retry).toHaveBeenCalled();
  });

  it('a detail list uses label/value pairs and the em dash for an absent value', () => {
    render(
      <DetailList
        rows={[
          { label: 'Aberto por', value: 'Lucas Ferreira' },
          { label: 'Enviado por', value: null },
          { label: 'Restrição', value: 'Período bloqueado até 31/08/2026', restriction: true },
        ]}
      />,
    );
    expect(screen.getAllByRole('term').map((term) => term.textContent)).toEqual([
      'Aberto por',
      'Enviado por',
      'Restrição',
    ]);
    expect(screen.getAllByRole('definition')[1]?.textContent).toBe('—');
  });
});

describe('Table rows open their record without hijacking nested controls', () => {
  function Ledger({ onNested }: { onNested: () => void }) {
    return (
      <TableRegion
        caption="Transações de 24/09/2026"
        scrollLabel="Tabela de transações, role horizontalmente"
      >
        <Table caption="Transações de 24/09/2026">
          <TableHead>
            <tr>
              <Th>Data</Th>
              <Th align="end">Valor</Th>
              <Th>Ações</Th>
            </tr>
          </TableHead>
          <TableBody>
            <Tr href="/f/x/transacoes/1">
              <Td>24/09/2026</Td>
              <Td align="end">R$ 943,10</Td>
              <Td>
                <RowLink
                  href="/f/x/transacoes/1"
                  label="Abrir transação"
                  accessibleName="Abrir transação 24/09/2026 R$ 943,10 Terminal 2"
                />
                <button type="button" onClick={onNested}>
                  Ver parcelas
                </button>
              </Td>
            </Tr>
          </TableBody>
        </Table>
      </TableRegion>
    );
  }

  it('semantic table, named scroll region, scoped headers and an explicit named link', () => {
    render(<Ledger onNested={() => undefined} />);
    expect(screen.getByRole('table', { name: 'Transações de 24/09/2026' })).toBeTruthy();
    const region = screen.getByRole('region', {
      name: 'Tabela de transações, role horizontalmente',
    });
    expect(region.getAttribute('tabindex')).toBe('0');
    expect(
      screen.getAllByRole('columnheader').every((th) => th.getAttribute('scope') === 'col'),
    ).toBe(true);
    expect(
      screen.getByRole('link', { name: 'Abrir transação 24/09/2026 R$ 943,10 Terminal 2' }),
    ).toBeTruthy();
  });

  it('a row click activates the row link; nested controls and text selection do not', () => {
    const nested = vi.fn();
    render(<Ledger onNested={nested} />);
    const link = screen.getByRole('link');
    const clicks: { ctrl: boolean }[] = [];
    link.addEventListener('click', (event) => {
      event.preventDefault();
      clicks.push({ ctrl: event.ctrlKey });
    });
    fireEvent.click(screen.getByText('R$ 943,10'), { ctrlKey: true });
    expect(clicks).toEqual([{ ctrl: true }]);
    fireEvent.click(screen.getByRole('button', { name: 'Ver parcelas' }));
    expect(nested).toHaveBeenCalled();
    expect(clicks).toHaveLength(1);
    const selection = vi
      .spyOn(window, 'getSelection')
      .mockReturnValue({ toString: () => '943,10' } as Selection);
    fireEvent.click(screen.getByText('R$ 943,10'));
    expect(clicks).toHaveLength(1);
    selection.mockRestore();
  });
});

describe('FilterBar, Pagination and PageHeader', () => {
  it('applies staged changes once on submit; Limpar filtros only when offered', () => {
    const onApply = vi.fn();
    const { rerender } = render(
      <FilterBar
        label="Filtros de transações"
        scopeSummary="24/09/2026 · padrão — todos os tipos"
        fields={<input aria-label="Conta" />}
        onApply={onApply}
        activeCount={1}
      />,
    );
    expect(screen.getByRole('form', { name: 'Filtros de transações' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Limpar filtros' })).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Filtrar' }));
    expect(onApply).toHaveBeenCalledTimes(1);
    rerender(
      <FilterBar
        label="Filtros de transações"
        scopeSummary="Terminal 2"
        fields={<input aria-label="Conta" />}
        onApply={onApply}
        onClear={() => undefined}
        dirty
        activeCount={2}
        resultsAnnouncement="37 transações"
      />,
    );
    expect(screen.getByRole('button', { name: 'Filtrar' }).className).toContain(
      'bg-action-primary-bg',
    );
    expect(screen.getByRole('button', { name: 'Limpar filtros' })).toBeTruthy();
    expect(screen.getByRole('status').textContent).toBe('37 transações');
  });

  it('compact: the scope summary and Filtros (n) open the fields in a sheet', () => {
    installMatchMedia(true);
    render(
      <FilterBar
        label="Filtros de transações"
        scopeSummary="Terminal 2"
        fields={<input aria-label="Conta" />}
        onApply={() => undefined}
        activeCount={2}
        activeCountLabel="2"
      />,
    );
    expect(screen.queryByLabelText('Conta')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Filtros (2)' }));
    expect(screen.getByLabelText('Conta')).toBeTruthy();
  });

  it('pagination is driven by the envelope, with counts from the format port', () => {
    const onPageChange = vi.fn();
    const envelope: PageEnvelope = {
      page: 2,
      pageSize: 20,
      totalCount: 137,
      totalPages: 7,
      hasNext: true,
      hasPrevious: true,
    };
    const { rerender } = render(
      <WithFormat>
        <Pagination envelope={envelope} onPageChange={onPageChange} listName="transações" />
      </WithFormat>,
    );
    expect(screen.getByRole('navigation', { name: 'Paginação de transações' })).toBeTruthy();
    expect(screen.getByText('#21–#40 de #137')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Página #2' }).getAttribute('aria-current')).toBe(
      'page',
    );
    fireEvent.click(screen.getByRole('button', { name: 'Próxima página' }));
    expect(onPageChange).toHaveBeenCalledWith(3);
    rerender(
      <WithFormat>
        <Pagination
          envelope={{ ...envelope, page: 1, hasPrevious: false }}
          onPageChange={onPageChange}
          listName="transações"
        />
      </WithFormat>,
    );
    expect(
      screen.getByRole('button', { name: 'Página anterior' }).getAttribute('aria-disabled'),
    ).toBe('true');
    rerender(
      <WithFormat>
        <Pagination
          envelope={{ ...envelope, page: 1, totalPages: 1, hasNext: false, hasPrevious: false }}
          onPageChange={onPageChange}
          listName="transações"
        />
      </WithFormat>,
    );
    expect(screen.queryByRole('navigation')).toBeNull();
  });

  it('page header: the h1 takes focus on entry, the origin is a breadcrumb and a Voltar link', () => {
    render(
      <PageHeader
        title="Conferência"
        origin={[{ label: 'Fila de trabalho', href: '/f/x/gestao/fila' }]}
        context="Terminal 2 · quinta-feira, 24 de setembro de 2026"
        actions={<button type="button">Aprovar</button>}
      />,
    );
    const heading = screen.getByRole('heading', { level: 1, name: 'Conferência' });
    expect(document.activeElement).toBe(heading);
    expect(screen.getByRole('navigation', { name: 'Você está em' })).toBeTruthy();
    expect(
      screen.getByRole('link', { name: 'Voltar para Fila de trabalho' }).getAttribute('href'),
    ).toBe('/f/x/gestao/fila');
  });
});
