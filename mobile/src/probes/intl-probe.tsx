import { useState } from 'react';
import { Button, Platform, Text, View } from 'react-native';
import Constants from 'expo-constants';
import expoMetadata from 'expo/package.json';
import { verifyCore } from './core-verification';

type HermesGlobal = typeof globalThis & {
  HermesInternal?: { getRuntimeProperties?: () => Record<string, unknown> };
};

function codePoints(value: string) {
  return Array.from(
    value,
    (character) => `U+${character.codePointAt(0)!.toString(16).toUpperCase().padStart(4, '0')}`,
  );
}

// Measurement only. No formatter strategy or normalization belongs to Mobile M1.
export function measureIntl() {
  const hermes = (globalThis as HermesGlobal).HermesInternal;
  if (!hermes || !['ios', 'android'].includes(Platform.OS)) {
    throw new Error('Executar em iOS ou Android sob Hermes, sem depurador remoto.');
  }
  const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
  const signed = new Intl.NumberFormat('pt-BR', {
    style: 'currency',
    currency: 'BRL',
    signDisplay: 'exceptZero',
  });
  const instant = new Date('2026-07-08T17:30:00.000Z');
  const timezone = 'America/Sao_Paulo';
  const options: Intl.DateTimeFormatOptions = {
    timeZone: timezone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  };
  const readings: { name: string; output: string; expected: string }[] = [
    { name: 'BRL positivo', output: currency.format(1234.56), expected: 'R$\u00a01.234,56' },
    { name: 'BRL negativo', output: currency.format(-1234.56), expected: '−R$\u00a01.234,56' },
    { name: 'BRL zero', output: currency.format(0), expected: 'R$\u00a00,00' },
    {
      name: 'BRL agrupamento',
      output: currency.format(1234567.89),
      expected: 'R$\u00a01.234.567,89',
    },
    { name: 'BRL sinal positivo', output: signed.format(12), expected: '+R$\u00a012,00' },
    { name: 'BRL sinal negativo', output: signed.format(-12), expected: '−R$\u00a012,00' },
    { name: 'BRL sinal zero', output: signed.format(0), expected: 'R$\u00a00,00' },
    {
      name: 'Número decimal',
      output: new Intl.NumberFormat('pt-BR').format(1234.5),
      expected: '1.234,5',
    },
    {
      name: 'Data',
      output: new Intl.DateTimeFormat('pt-BR', options).format(instant),
      expected: '08/07/2026',
    },
    {
      name: 'Hora',
      output: new Intl.DateTimeFormat('pt-BR', {
        timeZone: timezone,
        hour: '2-digit',
        minute: '2-digit',
        hourCycle: 'h23',
      }).format(instant),
      expected: '14:30',
    },
    {
      name: 'Data e hora',
      output: new Intl.DateTimeFormat('pt-BR', {
        ...options,
        hour: '2-digit',
        minute: '2-digit',
        hourCycle: 'h23',
      }).format(instant),
      expected: '08/07/2026 14:30',
    },
  ];
  return {
    engine: 'Hermes',
    runtime: hermes.getRuntimeProperties?.() ?? 'API de propriedades indisponível',
    platform: Platform.OS,
    osVersion: Platform.Version,
    expoSdk: Constants.expoConfig?.sdkVersion ?? null,
    // Bundled dependency metadata, not a manually maintained version or engine measurement.
    expoPackage: expoMetadata.version ?? null,
    deviceIntlOptions: new Intl.DateTimeFormat().resolvedOptions(),
    locale: 'pt-BR',
    timezone,
    input: instant.toISOString(),
    numberOptions: currency.resolvedOptions(),
    dateOptions: new Intl.DateTimeFormat('pt-BR', options).resolvedOptions(),
    historical: ['gregory', 'iso8601'].flatMap((calendar) =>
      ['UTC', 'Etc/GMT+1', 'America/New_York'].map((timeZone) => {
        try {
          const formatter = new Intl.DateTimeFormat('en-US', {
            calendar,
            timeZone,
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            era: 'short',
            hour: '2-digit',
            minute: '2-digit',
            hourCycle: 'h23',
            timeZoneName: 'longOffset',
          });
          return {
            calendar,
            timeZone,
            options: formatter.resolvedOptions(),
            parts: formatter.formatToParts(new Date('0001-01-01T00:00:00Z')),
          };
        } catch (error) {
          return { calendar, timeZone, error: String(error) };
        }
      }),
    ),
    readings: readings.map((reading) => ({
      ...reading,
      matchesSpec: reading.output === reading.expected,
      outputCodePoints: codePoints(reading.output),
      expectedCodePoints: codePoints(reading.expected),
    })),
  };
}

export function IntlProbe() {
  const [result, setResult] = useState('');
  const [summary, setSummary] = useState('');
  function run() {
    try {
      const measurement = { intl: measureIntl(), core: verifyCore() };
      setSummary(
        `${measurement.intl.platform}: ${measurement.core.passed}/${measurement.core.total}`,
      );
      // DEV-only, non-secret measurement for the native runner. Never stores session data.
      if (__DEV__)
        (globalThis as typeof globalThis & { __lotoCoreProof?: unknown }).__lotoCoreProof =
          measurement;
      setResult(JSON.stringify(measurement));
      // Optional DEV capture only, explicitly configured by the local native test runner.
      // This sends non-secret measurements to an owned loopback sink, never to the backend.
      const sink = process.env.EXPO_PUBLIC_NATIVE_PROBE_SINK;
      if (__DEV__ && sink) {
        const url = new URL(sink);
        if (url.protocol !== 'http:' || !['127.0.0.1', 'localhost'].includes(url.hostname))
          throw new Error('Probe sink must be loopback');
        void fetch(url.toString(), {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify(measurement),
        }).catch(() => setSummary('Não foi possível capturar a medição.'));
      }
    } catch (error) {
      setResult(error instanceof Error ? error.message : String(error));
    }
  }
  return (
    <View className="gap-4">
      <Button testID="native-core-measure" title="Medir Intl no Hermes" onPress={run} />
      {summary ? <Text testID="native-core-summary">{summary}</Text> : null}
      {result ? (
        <Text testID="native-core-result" accessible accessibilityLabel={result} selectable>
          {result}
        </Text>
      ) : null}
    </View>
  );
}
