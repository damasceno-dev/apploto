import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { Animated, Easing, Platform, Pressable, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { formatCount } from '@loto/core';
import { useTheme } from '@/theme/provider';
import { isCompact, layer, radius, shadowStyle, spacing } from '@/theme/style';
import type { SemanticColors } from '@/theme/theme';
import { Button } from './button';
import { useKeyboardHeight } from './screen';
import { announce, AppText, Glyph, moveFocusTo } from './text';

type FeedbackKind = keyof SemanticColors['feedback'];

export const UNCERTAIN_TITLE = 'Resultado não confirmado';
export const UNCERTAIN_BODY = 'Não foi possível confirmar se a ação foi concluída.';
export const NO_BODY_FALLBACK =
  'Não foi possível concluir a ação. Verifique sua conexão e tente novamente.';

const glyphFor: Record<FeedbackKind, 'error' | 'warning' | 'info' | 'check' | 'lock'> = {
  danger: 'error',
  warning: 'warning',
  info: 'info',
  success: 'check',
  neutral: 'lock',
};

// ------------------------------------------------------------------------------------------------
// Notice
// ------------------------------------------------------------------------------------------------

export interface NoticeProps {
  readonly kind: FeedbackKind;
  readonly title: string;
  /** Client copy, separate from backend text. */
  readonly body?: string;
  /** `ResponseErrorJson.ErrorMessages` verbatim and in order — one item per string. */
  readonly messages?: readonly string[];
  readonly actions?: ReactNode;
  /** A failure that blocks the action is announced assertively once; others politely. */
  readonly blocking?: boolean;
  readonly onDismiss?: () => void;
  readonly testID?: string;
}

/** Persistent in-flow message (notice.md): a start rule and icon carry the kind, no large tint. */
export function Notice({
  kind,
  title,
  body,
  messages,
  actions,
  blocking,
  onDismiss,
  testID,
}: NoticeProps) {
  const { colors } = useTheme();
  const assertive = blocking ?? (kind === 'danger' || kind === 'warning');
  const list = messages ?? [];
  const text = body ?? (kind === 'danger' && list.length === 0 ? NO_BODY_FALLBACK : undefined);
  const spoken = [title, text, ...list].filter(Boolean).join('. ');
  useEffect(() => {
    // Android reads the live region itself; iOS needs an explicit announcement on insertion and
    // when the next result of the same request replaces the text.
    if (Platform.OS === 'ios') announce(spoken);
  }, [spoken]);
  const tone = colors.sem.feedback[kind];
  return (
    <View
      testID={testID}
      accessibilityLiveRegion={assertive ? 'assertive' : 'polite'}
      role={assertive ? 'alert' : 'status'}
      style={{
        flexDirection: 'row',
        gap: spacing.inline.md,
        backgroundColor: colors.sem.surface.default,
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.default,
        borderLeftWidth: spacing.size['marker-width'],
        borderLeftColor: tone.border,
        borderRadius: radius.panel,
        padding: spacing.inset.notice,
      }}
    >
      <Glyph name={glyphFor[kind]} color={tone.fg} size={18} />
      <View style={{ flex: 1, gap: spacing.stack.sm }}>
        <AppText variant="body-strong">{title}</AppText>
        {text ? <AppText variant="body">{text}</AppText> : null}
        {list.length > 0 ? (
          <View accessibilityRole="list" style={{ gap: spacing.stack.xs }}>
            {list.map((message, index) => (
              <View
                key={`${index}-${message}`}
                style={{ flexDirection: 'row', gap: spacing.inline.sm }}
              >
                <AppText variant="body" tone="secondary" importantForAccessibility="no">
                  {'•'}
                </AppText>
                <AppText variant="body" style={{ flex: 1 }}>
                  {message}
                </AppText>
              </View>
            ))}
          </View>
        ) : null}
        {actions || onDismiss ? (
          <View
            style={{
              flexDirection: 'row',
              flexWrap: 'wrap',
              gap: spacing.size['touch-target-gap'],
            }}
          >
            {actions}
            {onDismiss ? <Button label="Fechar aviso" variant="link" onPress={onDismiss} /> : null}
          </View>
        ) : null}
      </View>
    </View>
  );
}

/** **Resultado não confirmado** with only the recovery the catalog allows for that operation. */
export function UncertainNotice({
  messages,
  recovery,
  testID,
}: {
  readonly messages?: readonly string[];
  readonly recovery: ReactNode;
  readonly testID?: string;
}) {
  return (
    <Notice
      kind="warning"
      title={UNCERTAIN_TITLE}
      body={UNCERTAIN_BODY}
      messages={messages}
      actions={recovery}
      testID={testID}
    />
  );
}

export interface SummaryMessage {
  readonly text: string;
  /** Only when the association to one field is unambiguous: focus moves there. */
  readonly field?: { readonly label: string; readonly focus: () => void };
}

/**
 * Error summary at the top of a form: lists every message; those that name a field link to it.
 * Receives screen-reader focus once when shown after a failed submit.
 */
export function ErrorSummary({
  title = 'Não foi possível salvar',
  messages,
  testID,
}: {
  readonly title?: string;
  readonly messages: readonly SummaryMessage[];
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  const container = useRef<View>(null);
  useEffect(() => {
    const timer = setTimeout(() => moveFocusTo(container.current), 50);
    return () => clearTimeout(timer);
  }, []);
  return (
    <View
      ref={container}
      testID={testID}
      accessible={false}
      role="alert"
      accessibilityLiveRegion="assertive"
      style={{
        gap: spacing.stack.sm,
        borderWidth: spacing.size['border-width'],
        borderColor: colors.sem.border.default,
        borderLeftWidth: spacing.size['marker-width'],
        borderLeftColor: colors.sem.feedback.danger.border,
        borderRadius: radius.panel,
        padding: spacing.inset.notice,
        backgroundColor: colors.sem.surface.default,
      }}
    >
      <AppText variant="body-strong" accessibilityRole="header">
        {title}
      </AppText>
      {messages.map((message, index) =>
        message.field ? (
          <Button
            key={`${index}-${message.text}`}
            label={message.text}
            variant="link"
            accessibilityLabel={`${message.text}. Ir para ${message.field.label}`}
            onPress={message.field.focus}
          />
        ) : (
          <AppText key={`${index}-${message.text}`} variant="body">
            {message.text}
          </AppText>
        ),
      )}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// SaveStatus
// ------------------------------------------------------------------------------------------------

export type SaveState =
  | 'saved'
  | 'never'
  | 'dirty'
  | 'saving'
  | 'unsaved-server'
  | 'failed'
  | 'uncertain'
  | 'stale'
  | 'device-only'
  | 'storage-failed';

/** The acknowledged save time, pre-formatted from the response's `UpdatedAt` (never a device time). */
export interface SavedAt {
  readonly time: string;
  /** dd/MM for a historical day (**Salvo em 23/09 às 18:42**). */
  readonly date?: string;
}

export function saveStatusText(state: SaveState, savedAt?: SavedAt): string {
  switch (state) {
    case 'saved':
      return savedAt
        ? savedAt.date
          ? `Salvo em ${savedAt.date} às ${savedAt.time}`
          : `Salvo às ${savedAt.time}`
        : 'Salvo';
    case 'never':
      return 'Contagem ainda não salva';
    case 'dirty':
      return 'Alterações não salvas';
    case 'saving':
      return 'Salvando…';
    case 'unsaved-server':
      return 'Alterações não salvas no servidor';
    case 'failed':
      return 'Não salvo — tentar novamente';
    case 'uncertain':
      return UNCERTAIN_TITLE;
    case 'stale':
      return 'Este fechamento mudou em outro lugar';
    case 'device-only':
      return 'Rascunho salvo neste dispositivo — ainda não salvo no servidor';
    case 'storage-failed':
      return 'Não foi possível guardar as alterações neste dispositivo. Mantenha esta tela aberta.';
  }
}

/**
 * SaveStatus (save-status.md): never claims a save the server did not acknowledge. **Salvar** is
 * visible in every state and disabled (with the clean reason) only when clean or saving.
 */
export function SaveStatus({
  state,
  savedAt,
  onSave,
  onRetry,
  onReload,
  cleanReason = 'Nenhuma alteração desde o último salvamento',
  testID,
}: {
  readonly state: SaveState;
  readonly savedAt?: SavedAt;
  readonly onSave: () => void;
  readonly onRetry?: () => void;
  readonly onReload?: () => void;
  readonly cleanReason?: string;
  readonly testID?: string;
}) {
  const { colors } = useTheme();
  const text = saveStatusText(state, savedAt);
  const previous = useRef(state);
  useEffect(() => {
    if (previous.current === state) return;
    previous.current = state;
    // Final states only — never each keystroke or debounce tick.
    if (
      ['saved', 'failed', 'uncertain', 'stale', 'storage-failed', 'unsaved-server'].includes(state)
    )
      announce(text);
  }, [state, text]);
  const color =
    state === 'saved'
      ? colors.sem.text.muted
      : state === 'failed' || state === 'storage-failed'
        ? colors.sem.feedback.danger.fg
        : state === 'uncertain' || state === 'stale' || state === 'unsaved-server'
          ? colors.sem.feedback.warning.fg
          : state === 'device-only'
            ? colors.sem.feedback.info.fg
            : colors.sem.text.secondary;
  return (
    <View
      testID={testID}
      style={{
        flexDirection: 'row',
        flexWrap: 'wrap',
        alignItems: 'center',
        gap: spacing.inline.md,
      }}
    >
      <Button
        label="Salvar"
        loading={state === 'saving'}
        loadingLabel="Salvando…"
        disabled={state === 'saved'}
        disabledReason={state === 'saved' ? cleanReason : undefined}
        onPress={onSave}
      />
      <View
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.inline.xs,
          flexShrink: 1,
        }}
        accessible
        accessibilityLabel={text}
      >
        {state === 'saved' ? <Glyph name="check" color={colors.sem.feedback.success.fg} /> : null}
        <AppText variant="helper" color={color} style={{ flexShrink: 1 }}>
          {text}
        </AppText>
      </View>
      {state === 'failed' && onRetry ? (
        <Button label="Tentar novamente" variant="link" onPress={onRetry} />
      ) : null}
      {(state === 'uncertain' || state === 'stale') && onReload ? (
        <Button label="Recarregar e conferir" variant="link" onPress={onReload} />
      ) : null}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Disclosure
// ------------------------------------------------------------------------------------------------

/** Collapsed secondary content (disclosure.md); toggling never navigates or changes data. */
export function Disclosure({
  label,
  summary,
  expanded: controlled,
  onExpandedChange,
  defaultExpanded = false,
  loading = false,
  unavailableText,
  children,
  testID,
}: {
  readonly label: string;
  readonly summary?: string;
  readonly expanded?: boolean;
  readonly onExpandedChange?: (expanded: boolean) => void;
  readonly defaultExpanded?: boolean;
  readonly loading?: boolean;
  readonly unavailableText?: string;
  readonly children?: ReactNode;
  readonly testID?: string;
}) {
  const { colors, motion } = useTheme();
  const [own, setOwn] = useState(defaultExpanded);
  const expanded = controlled ?? own;
  const rotation = useState(() => new Animated.Value(expanded ? 1 : 0))[0];
  const duration = motion('disclosure').duration;
  useEffect(() => {
    Animated.timing(rotation, {
      toValue: expanded ? 1 : 0,
      duration,
      easing: Easing.bezier(0.2, 0, 0, 1),
      useNativeDriver: true,
    }).start();
  }, [expanded, duration, rotation]);
  const toggle = () => {
    const next = !expanded;
    if (controlled === undefined) setOwn(next);
    onExpandedChange?.(next);
  };
  return (
    <View testID={testID}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={summary ? `${label}, ${summary}` : label}
        accessibilityState={{ expanded }}
        onPress={toggle}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.inline.sm,
          minHeight: spacing.size['touch-target-min'],
        }}
      >
        <Animated.View
          style={{
            transform: [
              {
                rotate: rotation.interpolate({
                  inputRange: [0, 1],
                  outputRange: ['0deg', '90deg'],
                }),
              },
            ],
          }}
        >
          <Glyph name="chevronRight" color={colors.sem.action.ghost.fg} size={18} />
        </Animated.View>
        <AppText variant="label" color={colors.sem.action.ghost.fg} style={{ flexShrink: 1 }}>
          {label}
        </AppText>
        {summary ? (
          <AppText variant="helper" tone="muted">
            {summary}
          </AppText>
        ) : null}
      </Pressable>
      {expanded ? (
        <View
          style={{
            borderLeftWidth: spacing.size['border-width'],
            borderLeftColor: colors.sem.border.default,
            paddingLeft: spacing.inset.panel,
            gap: spacing.stack.sm,
          }}
        >
          {loading ? (
            <AppText variant="helper" tone="muted" aria-busy>
              Carregando…
            </AppText>
          ) : unavailableText ? (
            <AppText variant="body" tone="secondary">
              {unavailableText}
            </AppText>
          ) : (
            children
          )}
        </View>
      ) : null}
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Stepper
// ------------------------------------------------------------------------------------------------

export type StepStatus =
  | 'not-started'
  | 'current'
  | 'saving'
  | 'done'
  | 'failed'
  | 'uncertain'
  | 'skipped'
  | 'unavailable'
  | 'filled'
  | 'incomplete';

export interface Step {
  readonly title: string;
  readonly status: StepStatus;
  /** Confirmed values for done (**Operador criado: Ana Lima**); the first verbatim message for failed; the reason for unavailable. */
  readonly detail?: string;
  readonly onRetry?: () => void;
  readonly onVerify?: () => void;
}

const stepText: Partial<Record<StepStatus, string>> = {
  saving: 'Salvando…',
  uncertain: UNCERTAIN_TITLE,
  skipped: 'Opcional — não feito',
  filled: 'preenchido',
  incomplete: 'incompleto',
};

/**
 * Confirmed per-step status of a guided journey (stepper.md). Done means the server confirmed it.
 * Compact layouts show **Passo n de N · Título** with the full list in a disclosure.
 */
export function Stepper({
  steps,
  testID,
}: {
  readonly steps: readonly Step[];
  readonly testID?: string;
}) {
  const { colors, width, fontScale } = useTheme();
  const compact = isCompact(width, fontScale);
  const current = Math.max(
    0,
    steps.findIndex((step) => step.status === 'current' || step.status === 'saving'),
  );
  const previous = useRef(steps.map((step) => step.status).join());
  useEffect(() => {
    const key = steps.map((step) => step.status).join();
    if (key === previous.current) return;
    previous.current = key;
    const failed = steps.find((step) => step.status === 'failed');
    announce(
      failed ? `${failed.title}: ${failed.detail ?? 'falhou'}` : (steps[current]?.title ?? ''),
    );
  }, [steps, current]);
  const list = (
    <View accessibilityRole="list" style={{ gap: spacing.stack.sm }}>
      {steps.map((step, index) => {
        const color =
          step.status === 'done'
            ? colors.sem.feedback.success.fg
            : step.status === 'failed'
              ? colors.sem.feedback.danger.fg
              : step.status === 'uncertain'
                ? colors.sem.feedback.warning.fg
                : step.status === 'current' || step.status === 'saving'
                  ? colors.sem.text.primary
                  : step.status === 'skipped' || step.status === 'unavailable'
                    ? colors.sem.text.muted
                    : colors.sem.text.secondary;
        const statusLine = step.detail ?? stepText[step.status];
        const isCurrent =
          index === current && (step.status === 'current' || step.status === 'saving');
        return (
          <View
            key={step.title}
            accessible
            aria-current={isCurrent ? 'step' : undefined}
            accessibilityLabel={`Passo ${formatCount(index + 1)} de ${formatCount(steps.length)}: ${step.title}${statusLine ? `, ${statusLine}` : ''}${isCurrent ? ', passo atual' : ''}`}
            style={{ flexDirection: 'row', gap: spacing.inline.md, alignItems: 'flex-start' }}
          >
            <View
              style={{
                width: 28,
                height: 28,
                borderRadius: radius.full,
                alignItems: 'center',
                justifyContent: 'center',
                backgroundColor: isCurrent
                  ? colors.sem.action.primary.bg
                  : colors.sem.surface.muted,
              }}
            >
              {step.status === 'done' ? (
                <Glyph name="check" color={colors.sem.feedback.success.fg} />
              ) : step.status === 'failed' ? (
                <Glyph name="error" color={color} />
              ) : step.status === 'uncertain' ? (
                <Glyph name="warning" color={color} />
              ) : step.status === 'unavailable' ? (
                <Glyph name="lock" color={color} />
              ) : (
                <AppText
                  variant="label"
                  color={isCurrent ? colors.sem.action.primary.fg : colors.sem.text.secondary}
                >
                  {formatCount(index + 1)}
                </AppText>
              )}
            </View>
            <View style={{ flex: 1, gap: spacing.stack.xs }}>
              <AppText
                variant="label"
                color={color}
                style={isCurrent ? { fontWeight: '700' } : undefined}
              >
                {step.title}
              </AppText>
              {statusLine ? (
                <AppText variant="helper" color={color}>
                  {statusLine}
                </AppText>
              ) : null}
              {step.status === 'failed' && step.onRetry ? (
                <Button label="Tentar novamente" variant="link" onPress={step.onRetry} />
              ) : null}
              {step.status === 'uncertain' && step.onVerify ? (
                <Button
                  label="Verificar e tentar novamente"
                  variant="link"
                  onPress={step.onVerify}
                />
              ) : null}
            </View>
          </View>
        );
      })}
    </View>
  );
  if (!compact) return <View testID={testID}>{list}</View>;
  return (
    <View testID={testID} style={{ gap: spacing.stack.sm }}>
      <AppText variant="label">{`Passo ${formatCount(current + 1)} de ${formatCount(steps.length)} · ${steps[current]?.title ?? ''}`}</AppText>
      <Disclosure label="Ver todos os passos">{list}</Disclosure>
    </View>
  );
}

// ------------------------------------------------------------------------------------------------
// Toast
// ------------------------------------------------------------------------------------------------

/** Injectable timers so tests drive toast lifetime deterministically. */
export interface ToastClock {
  now(): number;
  setTimeout(callback: () => void, ms: number): unknown;
  clearTimeout(handle: unknown): void;
}

export const systemClock: ToastClock = {
  now: () => Date.now(),
  setTimeout: (callback, ms) => setTimeout(callback, ms),
  clearTimeout: (handle) => clearTimeout(handle as ReturnType<typeof setTimeout>),
};

export const TOAST_MIN_MS = 5000;
/** At least 5 s; longer text stays longer (about 60 ms per character beyond 40). */
export function toastDuration(message: string) {
  return TOAST_MIN_MS + Math.max(0, message.length - 40) * 60;
}

interface ToastState {
  readonly id: number;
  readonly message: string;
}

const ToastContext = createContext<{ readonly show: (message: string) => void } | null>(null);

export function useToast() {
  const toast = useContext(ToastContext);
  if (toast === null) throw new Error('useToast requires ToastProvider.');
  return toast;
}

/**
 * Success echo only (toast.md): shown after the server confirmed, at most one at a time (a new one
 * replaces the old, which charges nothing to the new budget), paused while touched or focused,
 * dismissible, announced politely, never focused, kept above the keyboard and safe area.
 */
export function ToastProvider({
  children,
  clock = systemClock,
}: {
  readonly children: ReactNode;
  readonly clock?: ToastClock;
}) {
  const [toast, setToast] = useState<ToastState | null>(null);
  const timer = useRef<{
    handle: unknown;
    startedAt: number;
    remaining: number;
    id: number;
  } | null>(null);
  const nextId = useRef(1);
  const clear = useCallback(() => {
    if (timer.current) clock.clearTimeout(timer.current.handle);
    timer.current = null;
  }, [clock]);
  const schedule = useCallback(
    (id: number, remaining: number) => {
      const handle = clock.setTimeout(() => {
        timer.current = null;
        setToast((current) => (current?.id === id ? null : current));
      }, remaining);
      timer.current = { handle, startedAt: clock.now(), remaining, id };
    },
    [clock],
  );
  const show = useCallback(
    (message: string) => {
      clear();
      const id = nextId.current++;
      setToast({ id, message });
      if (Platform.OS === 'ios') announce(message);
      schedule(id, toastDuration(message));
    },
    [clear, schedule],
  );
  const pause = useCallback(() => {
    const active = timer.current;
    if (!active || active.handle === null) return;
    clock.clearTimeout(active.handle);
    timer.current = {
      ...active,
      handle: null,
      remaining: Math.max(0, active.remaining - (clock.now() - active.startedAt)),
    };
  }, [clock]);
  const resume = useCallback(() => {
    const active = timer.current;
    if (!active || active.handle !== null) return;
    schedule(active.id, active.remaining);
  }, [schedule]);
  const dismiss = useCallback(() => {
    clear();
    setToast(null);
  }, [clear]);
  useEffect(() => clear, [clear]);
  const value = useMemo(() => ({ show }), [show]);
  const keyboard = useKeyboardHeight();
  return (
    <ToastContext.Provider value={value}>
      {children}
      {toast ? (
        <ToastView
          key={toast.id}
          message={toast.message}
          keyboard={keyboard}
          onPause={pause}
          onResume={resume}
          onDismiss={dismiss}
        />
      ) : null}
    </ToastContext.Provider>
  );
}

function ToastView({
  message,
  keyboard,
  onPause,
  onResume,
  onDismiss,
}: {
  readonly message: string;
  /** Keyboard height tracked by the provider, so a replacing toast starts above an open keyboard. */
  readonly keyboard: number;
  readonly onPause: () => void;
  readonly onResume: () => void;
  readonly onDismiss: () => void;
}) {
  const { colors, scheme, motion, width, fontScale } = useTheme();
  const insets = useSafeAreaInsets();
  const opacity = useState(() => new Animated.Value(0))[0];
  const duration = motion('toast').duration;
  useEffect(() => {
    Animated.timing(opacity, { toValue: 1, duration, useNativeDriver: true }).start();
  }, [opacity, duration]);
  const compact = isCompact(width, fontScale);
  return (
    <Animated.View
      testID="toast"
      pointerEvents="box-none"
      style={{
        position: 'absolute',
        left: spacing.page['inline-compact'],
        right: spacing.page['inline-compact'],
        bottom: Math.max(insets.bottom, keyboard) + spacing.stack.md,
        alignItems: compact ? 'center' : 'flex-end',
        zIndex: layer.toast,
        elevation: layer.toast,
        opacity,
      }}
    >
      <Pressable
        accessible
        accessibilityLiveRegion="polite"
        role="status"
        accessibilityLabel={message}
        onPressIn={onPause}
        onPressOut={onResume}
        onFocus={onPause}
        onBlur={onResume}
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: spacing.inline.md,
          backgroundColor: colors.sem.surface.raised,
          ...shadowStyle(scheme, 'floating'),
          borderRadius: radius.panel,
          borderLeftWidth: spacing.size['marker-width'],
          borderLeftColor: colors.sem.feedback.success.border,
          paddingLeft: spacing.inset.panel,
          maxWidth: '100%',
        }}
      >
        <Glyph name="check" color={colors.sem.feedback.success.fg} />
        <AppText variant="body" style={{ flexShrink: 1, paddingVertical: spacing.stack.sm }}>
          {message}
        </AppText>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Fechar"
          onPress={onDismiss}
          onFocus={onPause}
          onBlur={onResume}
          style={{
            minWidth: spacing.size['touch-target-min'],
            minHeight: spacing.size['touch-target-min'],
            alignItems: 'center',
            justifyContent: 'center',
          }}
        >
          <Glyph name="close" color={colors.sem.text.secondary} />
        </Pressable>
      </Pressable>
    </Animated.View>
  );
}
