'use client';

import { useState, type ReactNode } from 'react';
import {
  asCents,
  enumLabels,
  evaluateMoneyExpression,
  formatBrl,
  formatBrlWire,
  formatCount,
  formatDate,
  formatDateWithWeekday,
  formatDuration,
  formatIdentifier,
  formatInstallment,
  formatLongDate,
  formatOptional,
  formatPercent,
  formatSignedBrlWire,
  formatSignedDuration,
  formatWallDateTime,
  formatWallTime,
  MAX_CENTS,
  moneyDigitsFromCents,
  presentFiadoBalanceWire,
  spellBrl,
  type CalendarDate,
  type MoneyExpressionError,
} from '@loto/core';
import { DomainText } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { FormField, FormGrid } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { blankMoney, MoneyInput, type MoneyValue } from '@/components/ui/money-input';
import { AuditInstant } from '@/client/format/audit-instant';
import { Example, Section } from './gallery-kit';

// The `@loto/core` helpers on illustrative values (labelled **Dados de exemplo**): every figure is
// computed by the real package, including values no fixture table ever held. Nothing is fetched.

const fiadoKind = { debt: 'debt', settled: 'settled', credit: 'credit' } as const;

/**
 * Gallery-only copy for the arithmetic demonstration. The product pattern (where an expression is
 * allowed, its wording and confirmation) is Design 4.9.7; these strings are provisional.
 */
const expressionMessages: Record<MoneyExpressionError, string> = {
  'too-long': 'Conta longa demais.',
  'too-complex': 'Conta longa demais.',
  'unsupported-character': 'Use apenas números, vírgula, ponto, + − × ÷ e parênteses.',
  'malformed-number': 'Número inválido: vírgula para centavos, ponto para milhares.',
  'unexpected-token': 'Conta incompleta ou fora de ordem.',
  'unbalanced-parentheses': 'Há parênteses sobrando.',
  'division-by-zero': 'Não é possível dividir por zero.',
  'out-of-range': `O valor passa de ${formatBrl(asCents(MAX_CENTS))}.`,
  'negative-result': 'O resultado não pode ser negativo.',
};

function ExpressionDemo() {
  const [text, setText] = useState('');
  const [money, setMoney] = useState<MoneyValue>(blankMoney);
  const result = evaluateMoneyExpression(text);
  const preview =
    result.status === 'ok'
      ? `= ${formatBrl(result.cents)}${result.rounded ? ' (arredondado para centavos)' : ''}`
      : result.status === 'incomplete'
        ? 'Continue digitando…'
        : result.status === 'invalid'
          ? expressionMessages[result.error]
          : '';
  return (
    <Example
      title="Valor por conta (M9.6 Fase 8 — demonstração)"
      fixture
      note="Digite 100+50+20, (10,50 × 3) ÷ 2 ou 10/3. Nada é executado: o @loto/core analisa a conta, mostra o resultado antes de aceitar e só o número resultante seguiria para o servidor."
    >
      <FormGrid>
        <FormField
          label="Valor ou conta"
          error={result.status === 'invalid' ? preview : undefined}
          helper={result.status === 'invalid' ? undefined : preview || 'Ex.: 100 + 50 + 20'}
        >
          <Input
            value={text}
            inputMode="decimal"
            autoComplete="off"
            onChange={(event) => setText(event.target.value)}
            data-testid="expression-input"
          />
        </FormField>
        <FormField label="Valor aceito">
          <MoneyInput value={money} onValueChange={setMoney} />
        </FormField>
      </FormGrid>
      <div className="flex flex-wrap items-center gap-inline-md">
        <Button
          variant="secondary"
          disabled={result.status !== 'ok'}
          disabledReason={result.status === 'ok' ? undefined : 'Digite uma conta válida'}
          onClick={() => {
            if (result.status === 'ok')
              setMoney({ kind: 'amount', digits: moneyDigitsFromCents(result.cents) });
          }}
        >
          {result.status === 'ok' ? `Usar ${formatBrl(result.cents)}` : 'Usar resultado'}
        </Button>
        <span className="type-helper text-text-muted" data-testid="expression-state">
          {result.status === 'ok' ? `ok:${result.cents}` : result.status}
          {result.status === 'invalid' ? `:${result.error}` : ''}
        </span>
      </div>
    </Example>
  );
}

function Row({ label, value, testId }: { label: string; value: ReactNode; testId?: string }) {
  return (
    <div className="flex flex-wrap justify-between gap-inline-md border-b border-border-subtle py-stack-xs">
      <dt className="type-helper text-text-secondary">{label}</dt>
      <dd className="type-money text-text-primary" data-testid={testId}>
        {value}
      </dd>
    </div>
  );
}

export function FormatSections() {
  const [zoneDemo] = useState('2026-07-08T17:30:00Z');
  return (
    <Section
      id="formatacao"
      title="Formatação (@loto/core)"
      contract="design/specs/formatting-ptbr.md · copy-guidelines.md §5"
    >
      <p className="type-helper text-text-muted">
        Valores de exemplo formatados pelo pacote real. Dinheiro, datas e rótulos usam as mesmas
        funções na renderização do servidor e no navegador; o horário de auditoria usa o fuso do
        dispositivo, lido só no navegador.
      </p>
      <FormGrid>
        <Example title="Dinheiro" fixture>
          <dl>
            <Row label="Valor (sem sinal)" value={formatBrlWire(1234567.89)} testId="fmt-money" />
            <Row label="Valor máximo" value={formatBrl(asCents(MAX_CENTS))} />
            <Row
              label="Diferença (falta)"
              value={
                <DomainText domain="cash-variance" value="shortage">
                  {`${formatSignedBrlWire(-20)} · falta`}
                </DomainText>
              }
              testId="fmt-signed"
            />
            <Row
              label="Diferença (sobra)"
              value={
                <DomainText domain="cash-variance" value="surplus">
                  {`${formatSignedBrlWire(3.5)} · sobra`}
                </DomainText>
              }
            />
            <Row label="Diferença zero" value={formatSignedBrlWire(0)} />
            {[290, 0, -10].map((wire) => {
              const balance = presentFiadoBalanceWire(wire);
              return (
                <Row
                  key={wire}
                  label={`Saldo fiado (${balance.kind})`}
                  value={
                    <DomainText domain="fiado-balance" value={fiadoKind[balance.kind]}>
                      {balance.text}
                    </DomainText>
                  }
                  testId={`fmt-fiado-${balance.kind}`}
                />
              );
            })}
            <Row label="Falado" value={spellBrl(asCents(123456789))} testId="fmt-spoken" />
          </dl>
        </Example>
        <Example title="Datas, horas e durações" fixture>
          <dl>
            <Row label="Data" value={formatDate('2028-02-29' as CalendarDate)} testId="fmt-date" />
            <Row label="Data longa" value={formatLongDate('2031-03-07' as CalendarDate)} />
            <Row label="Dia completo" value={formatDateWithWeekday('2031-03-07' as CalendarDate)} />
            <Row label="Horário da filial" value={formatWallTime('09:05:00')} />
            <Row label="Ponto (parede)" value={formatWallDateTime('2026-07-08T14:30:00')} />
            <Row
              label="Auditoria em America/Sao_Paulo"
              value={<AuditInstant value={zoneDemo} timeZone="America/Sao_Paulo" />}
              testId="fmt-instant-explicit"
            />
            <Row
              label="Auditoria no fuso deste dispositivo"
              value={<AuditInstant value={zoneDemo} />}
              testId="fmt-instant-device"
            />
            <Row label="Jornada (7,33 h)" value={formatDuration(7.33)} testId="fmt-duration" />
            <Row label="Banco de horas" value={formatSignedDuration(-1.5)} />
          </dl>
        </Example>
        <Example title="Números, identificadores e ausência" fixture>
          <dl>
            <Row label="Contagem" value={formatCount(1234567)} />
            <Row label="Percentual" value={formatPercent(12.5)} />
            <Row label="Parcela" value={formatInstallment(3, 12)} />
            <Row label="CPF" value={formatIdentifier('cpf', '52998224725')} testId="fmt-cpf" />
            <Row label="CNPJ" value={formatIdentifier('cnpj', '11222333000181')} />
            <Row label="Telefone" value={formatIdentifier('phone', '1132345678')} />
            <Row label="CEP" value={formatIdentifier('cep', '01310100')} />
            <Row label="Telefone fora do padrão" value={formatIdentifier('phone', 'ramal 204')} />
            <Row label="Ausente" value={formatOptional(null, formatCount)} />
            <Row label="Zero" value={formatOptional(0, formatCount)} />
          </dl>
        </Example>
      </FormGrid>
      <ExpressionDemo />
      <Example title="Rótulos dos 25 enums (cópia §5)">
        <dl className="grid grid-cols-1 gap-stack-sm medium:grid-cols-2" data-testid="enum-labels">
          {Object.entries(enumLabels).map(([name, labels]) => (
            <div key={name} className="flex flex-col gap-stack-xs">
              <dt className="type-helper text-text-muted">{name}</dt>
              <dd className="type-body text-text-primary">{Object.values(labels).join(' · ')}</dd>
            </div>
          ))}
        </dl>
      </Example>
    </Section>
  );
}
