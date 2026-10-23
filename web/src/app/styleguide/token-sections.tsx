'use client';

import type { CSSProperties } from 'react';
import { DomainText, StatusBadge, GeneratedMarker } from '@/components/ui/badge';
import {
  breakpointTokens,
  domainColors,
  layerTokens,
  radiusTokens,
  semanticColors,
  shadowTokens,
  spacingTokens,
  transitionTokens,
  typographyRoles,
} from '@/theme/tokens.generated';
import { ComputedValue, Example, Section, ThemePair } from './gallery-kit';

// Every exposed token, by its semantic/domain name, in both themes. Class maps are literal so
// Tailwind generates each role utility; a unit test checks they cover every generated name.

export const typeClasses: Record<(typeof typographyRoles)[number], string> = {
  'page-title': 'type-page-title',
  'page-title-compact': 'type-page-title-compact',
  'next-action': 'type-next-action',
  'summary-value': 'type-summary-value',
  'section-title': 'type-section-title',
  body: 'type-body',
  'body-strong': 'type-body-strong',
  control: 'type-control',
  button: 'type-button',
  'button-small': 'type-button-small',
  label: 'type-label',
  money: 'type-money',
  table: 'type-table',
  'table-header': 'type-table-header',
  helper: 'type-helper',
  badge: 'type-badge',
  overline: 'type-overline',
  code: 'type-code',
};

export const layerClasses: Record<(typeof layerTokens)[number], string> = {
  base: 'layer-base',
  raised: 'layer-raised',
  sticky: 'layer-sticky',
  'drawer-backdrop': 'layer-drawer-backdrop',
  drawer: 'layer-drawer',
  'dialog-backdrop': 'layer-dialog-backdrop',
  dialog: 'layer-dialog',
  popover: 'layer-popover',
  toast: 'layer-toast',
  'skip-link': 'layer-skip-link',
};

export const motionClasses: Record<(typeof transitionTokens)[number], string> = {
  control: 'motion-control',
  disclosure: 'motion-disclosure',
  'drawer-enter': 'motion-drawer-enter',
  'drawer-exit': 'motion-drawer-exit',
  'dialog-enter': 'motion-dialog-enter',
  'dialog-exit': 'motion-dialog-exit',
  toast: 'motion-toast',
  skeleton: 'motion-skeleton',
};

/** copy §5 labels as gallery reference strings; the official maps arrive with `@loto/core`. */
export const fixtureLabels: Record<string, Record<string, string>> = {
  'transaction-status': { Draft: 'Rascunho', Active: 'Ativa', Cancelled: 'Cancelada' },
  'daily-close-status': {
    Draft: 'Rascunho',
    Submitted: 'Enviado',
    Approved: 'Aprovado',
    Rejected: 'Rejeitado',
  },
  'aging-bucket': {
    Current: 'Em dia',
    Days0To30: '0–30 dias',
    Days31To60: '31–60 dias',
    Days61To90: '61–90 dias',
    Days91Plus: 'Mais de 90 dias',
  },
  'time-entry-status': {
    Present: 'Presente',
    DayOff: 'Folga',
    Sunday: 'Domingo',
    Holiday: 'Feriado',
    Vacation: 'Férias',
    JustifiedAbsence: 'Falta justificada',
    UnjustifiedAbsence: 'Falta injustificada',
  },
  'installment-plan-status': {
    Draft: 'Rascunho',
    Active: 'Ativo',
    Mixed: 'Misto',
    Cancelled: 'Cancelado',
  },
  'invitation-status': {
    Pending: 'Pendente',
    Accepted: 'Aceito',
    Declined: 'Recusado',
    Expired: 'Expirado',
  },
  'mail-delivery-status': {
    Pending: 'Envio pendente',
    Sent: 'Enviado ao servidor de e-mail',
    Failed: 'Falha no envio',
  },
  'operator-link-request-status': { Pending: 'Pendente', Resolved: 'Resolvida' },
  'review-event-type': {
    Rejected: 'Rejeitado na conferência',
    OpeningChanged: 'Abertura alterada',
  },
  'settlement-instrument': {
    None: 'Sem classificação',
    Cheque: 'Cheque',
    CreditCard: 'Cartão de crédito',
    DebitCard: 'Cartão de débito',
  },
  'monthly-financial-role': {
    None: 'Nenhuma',
    BolaoServiceFee: 'Tarifa de bolão',
    CommercialLeftover: 'Sobra (perda comercial)',
  },
  'overview-section-status': {
    Ready: 'Atualizado',
    Empty: 'Nada no período',
    NotConfigured: 'Não configurado',
  },
  direction: { In: 'Entrada', Out: 'Saída' },
  'cash-variance': { shortage: '−R$ 12,00 · falta', surplus: '+R$ 3,50 · sobra', zero: 'R$ 0,00' },
  'fiado-balance': {
    debt: 'R$ 80,00 · em aberto',
    settled: 'quitado',
    credit: '+R$ 10,00 · crédito do cliente',
  },
  'hour-balance': { positive: '+2h 15min', negative: '−2h 15min', zero: '0h 00min' },
  'generated-movement': { automatic: 'Automática' },
};

const textDomains = new Set(['direction', 'cash-variance', 'fiado-balance', 'hour-balance']);

export function TokenSections() {
  return (
    <>
      <Section
        id="tokens-cores"
        title="Cores semânticas"
        contract="design/tokens/colors.json · README"
      >
        <ThemePair>
          {(theme) => (
            <ul
              className="grid grid-cols-1 gap-stack-xs medium:grid-cols-2"
              aria-label={`Cores semânticas, ${theme === 'light' ? 'Claro' : 'Escuro'}`}
            >
              {semanticColors.map((name) => {
                const variable = `--loto-sem-${name.replaceAll('.', '-')}`;
                return (
                  <li key={name} className="flex items-center gap-inline-sm">
                    <span
                      aria-hidden="true"
                      className="size-6 shrink-0 rounded-xs border border-border-strong"
                      style={{ backgroundColor: `var(${variable})` } as CSSProperties}
                    />
                    <span className="flex min-w-0 flex-col">
                      <span className="type-helper text-text-primary [overflow-wrap:anywhere]">
                        {name}
                      </span>
                      <ComputedValue variable={variable} />
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </ThemePair>
      </Section>

      <Section
        id="tokens-dominio"
        title="Cores de domínio"
        contract="design/tokens/colors.json (domain) · badge.md"
      >
        <p className="type-helper text-text-muted">
          Rótulos de exemplo da cópia §5; os mapas oficiais chegam com o{' '}
          <code className="type-code">@loto/core</code> (Shared M3).
        </p>
        <ThemePair>
          {() => (
            <div className="flex flex-col gap-stack-md">
              {Object.entries(domainColors).map(([group, domain]) => (
                <div key={group} className="flex flex-col gap-stack-xs">
                  <span className="type-helper text-text-secondary">
                    {group}
                    {domain.enumName ? ` · ${domain.enumName}` : ''}
                  </span>
                  <div className="flex flex-wrap gap-inline-sm">
                    {Object.keys(domain.values).map((value) => {
                      const label = fixtureLabels[group]?.[value] ?? value;
                      if (group === 'generated-movement')
                        return <GeneratedMarker key={value}>{label}</GeneratedMarker>;
                      if (textDomains.has(group)) {
                        return (
                          <DomainText
                            key={value}
                            domain={group as 'direction'}
                            value={value as 'In'}
                            className="type-money"
                          >
                            {label}
                          </DomainText>
                        );
                      }
                      return (
                        <StatusBadge key={value} domain={group as 'aging-bucket'} value={value}>
                          {label}
                        </StatusBadge>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </ThemePair>
      </Section>

      <Section id="tokens-tipografia" title="Tipografia" contract="design/tokens/typography.json">
        <ul className="flex flex-col gap-stack-sm">
          {typographyRoles.map((role) => (
            <li
              key={role}
              className="flex flex-col gap-stack-xs border-b border-border-subtle pb-stack-sm"
            >
              <span className="type-helper text-text-muted">{role}</span>
              <span className={typeClasses[role]}>Fechamento do dia · 1.234,56 · 24/09/2026</span>
            </li>
          ))}
        </ul>
      </Section>

      <Section
        id="tokens-espacos"
        title="Espaçamento, tamanhos, cantos e sombras"
        contract="spacing.json · radii.json · shadows.json"
      >
        <Example title="Espaçamento e tamanhos (rem: crescem com o texto)">
          <ul className="flex flex-col gap-stack-xs">
            {spacingTokens.map((name) => (
              <li
                key={name}
                className="grid grid-cols-[minmax(0,14em)_1fr] items-center gap-inline-md"
              >
                <span className="type-helper text-text-secondary [overflow-wrap:anywhere]">
                  {name} · <ComputedValue variable={`--spacing-${name}`} />
                </span>
                <span
                  aria-hidden="true"
                  className="block h-2 max-w-full rounded-xs bg-action-primary-bg"
                  style={{ width: `var(--spacing-${name})` }}
                />
              </li>
            ))}
          </ul>
        </Example>
        <Example title="Cantos">
          <div className="flex flex-wrap gap-inline-lg">
            {radiusTokens.map((name) => (
              <span key={name} className="flex flex-col items-center gap-stack-xs">
                <span
                  aria-hidden="true"
                  className="size-12 border border-border-strong bg-surface-default"
                  style={{ borderRadius: `var(--radius-${name})` }}
                />
                <span className="type-helper text-text-secondary">{name}</span>
              </span>
            ))}
          </div>
        </Example>
        <ThemePair>
          {() => (
            <div className="flex flex-wrap gap-inline-lg">
              {shadowTokens.map((name) => (
                <span key={name} className="flex flex-col items-center gap-stack-xs">
                  <span
                    aria-hidden="true"
                    className="size-16 rounded-panel bg-surface-raised"
                    style={{ boxShadow: `var(--loto-shadow-${name})` }}
                  />
                  <span className="type-helper text-text-secondary">sombra {name}</span>
                </span>
              ))}
            </div>
          )}
        </ThemePair>
      </Section>

      <Section
        id="tokens-layout"
        title="Pontos de quebra, movimento e camadas"
        contract="breakpoints.json · motion.json · layers.json"
      >
        <Example title="Pontos de quebra (em: texto ampliado muda o arranjo antes)">
          <ul className="flex flex-col gap-stack-xs">
            {breakpointTokens.map((name) => (
              <li key={name} className="type-body">
                {name}: <ComputedValue variable={`--breakpoint-${name}`} />
              </li>
            ))}
          </ul>
        </Example>
        <Example
          title="Movimento"
          note="Com “reduzir movimento” ativo no sistema, toda duração vira 0 ms."
        >
          <ul className="flex flex-col gap-stack-xs">
            {transitionTokens.map((name) => (
              <li key={name} className="type-body">
                <span className={motionClasses[name]}>{name}</span>:{' '}
                <ComputedValue variable={`--loto-motion-${name}-duration`} />
              </li>
            ))}
          </ul>
        </Example>
        <Example title="Camadas (de baixo para cima)">
          <ol className="flex flex-col gap-stack-xs">
            {layerTokens.map((name) => (
              <li key={name} className={`type-body ${layerClasses[name]} relative`}>
                {name}
              </li>
            ))}
          </ol>
        </Example>
      </Section>
    </>
  );
}
