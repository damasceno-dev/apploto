import { formatCount } from '@loto/core';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Pressable, View } from 'react-native';
import { useTheme } from '@/theme/provider';
import { radius, spacing } from '@/theme/style';
import { Button } from './button';
import { FocusRing, PressableBox } from './focus';
import { useFieldControl } from './form-field';
import {
  ReadOnlyValue,
  TextField,
  useControlChrome,
  type FieldControlProps,
  type InputHandle,
} from './input';
import { Overlay } from './overlay';
import { AppText, Glyph } from './text';

export interface SelectOption {
  readonly value: string;
  readonly label: string;
  readonly description?: string;
  readonly group?: string;
  /** Only when an explicit server activity field confirms it (never inferred from absence). */
  readonly inactive?: boolean;
}

export const NO_DEFAULT_PLACEHOLDER = 'Escolha…';
export const OUT_OF_OPTIONS = '(fora das opções atuais)';

export interface SelectProps extends FieldControlProps {
  readonly options: readonly SelectOption[];
  /** null: nothing chosen yet (no-default selects read **Escolha…**). */
  readonly value: string | null;
  readonly onValueChange: (value: string) => void;
  /** The record's own text for a carried-over value missing from the current options. */
  readonly missingLabel?: string;
  /** Local filtering over the complete collection only — never one page of a paged endpoint. */
  readonly searchable?: boolean;
  /** Noun for the polite result count (**clientes** → "3 clientes encontrados"). */
  readonly resultNoun?: string;
  readonly state?: 'ready' | 'loading' | 'failed';
  readonly onRetry?: () => void;
  /** Why the list is empty (**Nenhuma conta disponível para você**). */
  readonly emptyText?: string;
  readonly emptyAction?: { readonly label: string; readonly onPress: () => void };
  /** The sheet title; defaults to the field label. */
  readonly title?: string;
}

/** Diacritic- and case-insensitive local match. */
export function matchesQuery(option: SelectOption, query: string) {
  const fold = (text: string) =>
    text
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLocaleLowerCase('pt-BR');
  const needle = fold(query.trim());
  return (
    needle === '' ||
    fold(option.label).includes(needle) ||
    fold(option.description ?? '').includes(needle)
  );
}

/**
 * Select and searchable select (select.md) on native: a trigger plus an accessible sheet list. A
 * disabled or read-only select never opens and never emits. Inside a dialog the list opens as an
 * in-modal layer, not a second native modal.
 */
export function Select({
  options,
  value,
  onValueChange,
  missingLabel,
  searchable = false,
  resultNoun = 'resultados',
  state = 'ready',
  onRetry,
  emptyText = 'Nenhuma opção disponível',
  emptyAction,
  title,
  testID,
  ...own
}: SelectProps) {
  const control = useFieldControl(own);
  const { colors } = useTheme();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [focused, setFocused] = useState(false);
  const trigger = useRef<View>(null);
  const search = useRef<InputHandle>(null);
  const chrome = useControlChrome({
    focused,
    invalid: control.invalid,
    disabled: control.disabled,
  });
  const { registerFocus } = control;
  const unavailable = control.disabled || state !== 'ready';
  useEffect(() => {
    registerFocus(() => trigger.current?.focus?.());
    return () => registerFocus(null);
  }, [registerFocus]);

  const selected = options.find((option) => option.value === value);
  const shown =
    value === null
      ? null
      : selected
        ? `${selected.label}${selected.inactive ? ' (inativo)' : ''}`
        : `${missingLabel ?? value} ${OUT_OF_OPTIONS}`;
  const filtered = useMemo(
    () => (searchable ? options.filter((option) => matchesQuery(option, query)) : options),
    [options, query, searchable],
  );

  if (control.readOnly)
    return (
      <ReadOnlyValue value={shown ?? '—'} label={control.accessibilityLabel} testID={testID} />
    );

  const triggerText =
    state === 'loading'
      ? 'Carregando…'
      : state === 'failed'
        ? 'Não foi possível carregar as opções'
        : (shown ?? NO_DEFAULT_PLACEHOLDER);
  const choose = (next: string) => {
    if (control.disabled || control.readOnly) return;
    setOpen(false);
    setQuery('');
    onValueChange(next);
  };
  return (
    <View style={{ gap: spacing.stack.xs }}>
      <Pressable
        ref={trigger}
        testID={testID}
        accessibilityRole="combobox"
        accessibilityLabel={`${control.accessibilityLabel ?? ''}${control.accessibilityLabel ? ', ' : ''}${triggerText}`}
        accessibilityHint={control.accessibilityHint}
        accessibilityState={{ disabled: unavailable, expanded: open, busy: state === 'loading' }}
        aria-required={control.required}
        onPress={() => {
          if (!unavailable) setOpen(true);
        }}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        style={[chrome.container, { paddingRight: spacing.inset['control-x'] }]}
      >
        <AppText
          variant="control"
          color={
            control.disabled
              ? colors.sem.action.disabled.fg
              : value === null || state !== 'ready'
                ? colors.sem.text.placeholder
                : colors.sem.text.primary
          }
          style={chrome.text}
        >
          {triggerText}
        </AppText>
        <Glyph
          name="chevronDown"
          color={control.disabled ? colors.sem.action.disabled.fg : colors.sem.text.secondary}
        />
        <FocusRing visible={focused} />
      </Pressable>
      {state === 'failed' && onRetry && !control.disabled ? (
        <Button label="Tentar novamente" variant="link" onPress={onRetry} />
      ) : null}
      <Overlay
        // A select that became unavailable while open closes without choosing.
        visible={open && !control.disabled && state === 'ready'}
        onDismiss={() => {
          setOpen(false);
          setQuery('');
        }}
        title={title ?? control.accessibilityLabel ?? 'Escolha uma opção'}
        variant="bottom-sheet"
        returnFocus={trigger}
        initialFocus={searchable ? search : undefined}
        testID={testID ? `${testID}-sheet` : undefined}
        pinned={
          searchable ? (
            <View style={{ gap: spacing.stack.xs }}>
              <TextField
                ref={search}
                kind="search"
                value={query}
                onChangeText={setQuery}
                accessibilityLabel="Buscar"
                placeholder="Buscar"
                resultCountLabel={`${formatCount(filtered.length)} ${resultNoun} encontrados`}
                testID={testID ? `${testID}-search` : undefined}
              />
              <AppText
                variant="helper"
                tone="muted"
              >{`${formatCount(filtered.length)} ${resultNoun}`}</AppText>
            </View>
          ) : undefined
        }
      >
        {options.length === 0 ? (
          <View style={{ gap: spacing.stack.sm }}>
            <AppText variant="body" tone="secondary">
              {emptyText}
            </AppText>
            {emptyAction ? (
              <Button
                label={emptyAction.label}
                variant="link"
                role="link"
                onPress={emptyAction.onPress}
              />
            ) : null}
          </View>
        ) : filtered.length === 0 ? (
          <AppText variant="body" tone="secondary">
            Nenhuma opção corresponde à busca.
          </AppText>
        ) : (
          <View accessibilityRole="list">
            {filtered.map((option, index) => {
              const heading =
                option.group !== filtered[index - 1]?.group ? option.group : undefined;
              const isSelected = option.value === value;
              return (
                <View key={option.value}>
                  {heading ? (
                    <AppText
                      variant="overline"
                      tone="muted"
                      accessibilityRole="header"
                      style={{ marginTop: spacing.stack.sm }}
                    >
                      {heading}
                    </AppText>
                  ) : null}
                  <PressableBox
                    testID={testID ? `${testID}-option-${option.value}` : undefined}
                    accessibilityRole="button"
                    accessibilityLabel={`${option.label}${option.inactive ? ', inativo' : ''}${option.description ? `, ${option.description}` : ''}`}
                    accessibilityState={{ selected: isSelected }}
                    onPress={() => choose(option.value)}
                    style={(pressed) => ({
                      minHeight: spacing.size['touch-target-comfortable'],
                      paddingVertical: spacing.stack.sm,
                      paddingHorizontal: spacing.inline.sm,
                      flexDirection: 'row',
                      alignItems: 'center',
                      gap: spacing.inline.md,
                      borderRadius: radius.control,
                      backgroundColor: isSelected
                        ? colors.sem.surface.selected
                        : pressed
                          ? colors.sem.surface.hover
                          : undefined,
                    })}
                  >
                    <View style={{ flex: 1, gap: spacing.stack.xs }}>
                      <AppText variant="control">{`${option.label}${option.inactive ? ' (inativo)' : ''}`}</AppText>
                      {option.description ? (
                        <AppText variant="helper" tone="muted">
                          {option.description}
                        </AppText>
                      ) : null}
                    </View>
                    {isSelected ? (
                      <Glyph name="check" color={colors.sem.selection.indicator} />
                    ) : null}
                  </PressableBox>
                </View>
              );
            })}
          </View>
        )}
      </Overlay>
    </View>
  );
}
