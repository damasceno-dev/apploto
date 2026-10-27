'use client';

import { ToastProvider } from '@/components/ui/toast';
import { useCompact, useMediaQuery } from '@/components/ui/use-media-query';
import { ThemeControl } from '@/theme/theme-control';
import { DataSections } from './data-sections';
import { FormatSections } from './format-sections';
import { InputSections } from './input-sections';
import { OverlaySections } from './overlay-sections';
import { TokenSections } from './token-sections';

const contents = [
  ['tokens-cores', 'Cores'],
  ['tokens-dominio', 'Domínio'],
  ['tokens-tipografia', 'Tipografia'],
  ['tokens-espacos', 'Espaços'],
  ['tokens-layout', 'Quebras, movimento, camadas'],
  ['formatacao', 'Formatação'],
  ['button', 'Button'],
  ['form-field', 'FormField'],
  ['input', 'Input'],
  ['money-input', 'MoneyInput'],
  ['select', 'Select'],
  ['date-picker', 'DatePicker'],
  ['choice', 'Checkbox, Radio, Switch'],
  ['badge', 'Badge'],
  ['card', 'Card'],
  ['summary-strip', 'SummaryStrip'],
  ['detail-list', 'DetailList'],
  ['table', 'Table / List'],
  ['filter-bar', 'FilterBar'],
  ['pagination', 'Pagination'],
  ['page-header', 'PageHeader'],
  ['modal-sheet', 'Modal / Sheet'],
  ['menu', 'Menu'],
  ['tabs', 'Tabs'],
  ['disclosure', 'Disclosure'],
  ['stepper', 'Stepper'],
  ['notice', 'Notice'],
  ['toast', 'Toast'],
  ['save-status', 'SaveStatus'],
  ['skeleton', 'Skeleton'],
  ['empty-state', 'EmptyState'],
] as const;

/**
 * The running component gallery (Web M3 Phase 6). Development-only: it exists in `next dev` and in
 * an explicitly requested gallery build, never in an ordinary production build. Data are labelled
 * examples formatted by the app's real `@loto/core` port (provided by `AppProviders`); nothing here
 * sends a request or performs a financial mutation.
 */
export function Gallery() {
  const compact = useCompact();
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const coarse = useMediaQuery('(pointer: coarse)');
  return (
    <ToastProvider>
      <a
        href="#conteudo"
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:rounded-control focus:bg-surface-raised focus:px-inset-control-x focus:py-2 layer-skip-link"
      >
        Pular para o conteúdo
      </a>
      <div className="mx-auto flex w-full max-w-content-max flex-col gap-page-section-gap px-page-inline-compact pt-page-block-start pb-page-block-end medium:px-page-inline-wide">
        <header className="flex flex-col gap-stack-md">
          <div className="flex flex-wrap items-center justify-between gap-inline-md">
            <h1 className="type-page-title-compact medium:type-page-title">Guia de estilo</h1>
            <ThemeControl />
          </div>
          <p className="type-body text-text-secondary">
            Somente para desenvolvimento. Todos os valores são dados de exemplo; nenhuma requisição
            é feita e nada é salvo. A formatação é a real, do{' '}
            <code className="type-code">@loto/core</code>.
          </p>
          <p className="type-helper text-text-muted" data-testid="environment">
            {`Arranjo: ${compact ? 'compacto' : 'amplo'} · ponteiro: ${coarse ? 'toque' : 'fino'} · movimento: ${reducedMotion ? 'reduzido' : 'normal'}`}
          </p>
          <nav aria-label="Seções do guia">
            <ul className="flex flex-wrap gap-x-inline-md gap-y-stack-xs type-helper">
              {contents.map(([id, label]) => (
                <li key={id}>
                  <a
                    href={`#${id}`}
                    className="text-text-link hover:text-text-link-hover hover:underline"
                  >
                    {label}
                  </a>
                </li>
              ))}
            </ul>
          </nav>
        </header>
        <main
          id="conteudo"
          tabIndex={-1}
          className="flex flex-col gap-page-section-gap outline-none"
        >
          <TokenSections />
          <FormatSections />
          <InputSections />
          <DataSections />
          <OverlaySections />
        </main>
      </div>
    </ToastProvider>
  );
}
