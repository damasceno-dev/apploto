import {
  createContext,
  useContext,
  useEffect,
  useId,
  useLayoutEffect,
  useMemo,
  useRef,
  useSyncExternalStore,
  type ReactElement,
  type ReactNode,
  type RefObject,
} from 'react';
import {
  KeyboardAvoidingView,
  Modal,
  PanResponder,
  Pressable,
  ScrollView,
  View,
  useWindowDimensions,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTheme } from '@/theme/provider';
import { layer, radius, shadowStyle, spacing } from '@/theme/style';
import { Button } from './button';
import { AppText, Glyph, moveFocusTo } from './text';

export type OverlayVariant = 'dialog' | 'confirmation' | 'drawer' | 'bottom-sheet';

interface Layer {
  readonly element: ReactElement;
  readonly dismiss: () => void;
  readonly busy: boolean;
}

/**
 * Layers opened inside one native modal (a picker inside a dialog) render in that modal's outlet
 * instead of a second native modal, so there is one modal trap and Android back closes the topmost
 * layer first. The store lives outside React state: a nested layer updating itself re-renders only
 * the outlet, never the dialog that contains its trigger.
 */
class LayerStore {
  private layers: { id: string; layer: Layer }[] = [];
  private listeners = new Set<() => void>();
  private snapshot: readonly { id: string; layer: Layer }[] = [];
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  };
  get = () => this.snapshot;
  set(id: string, value: Layer | null) {
    const index = this.layers.findIndex((entry) => entry.id === id);
    if (value === null) {
      if (index < 0) return;
      this.layers.splice(index, 1);
    } else if (index < 0) this.layers.push({ id, layer: value });
    else this.layers[index] = { id, layer: value };
    this.snapshot = [...this.layers];
    this.listeners.forEach((listener) => listener());
  }
  /** Android back / escape: the topmost layer first. Returns false when no layer is open. */
  dismissTop(): boolean {
    const top = this.layers.at(-1);
    if (!top) return false;
    if (!top.layer.busy) top.layer.dismiss();
    return true;
  }
}

const LayerHostContext = createContext<LayerStore | null>(null);

/** True inside a dialog or sheet: pickers and menus then open as in-modal layers. */
export function useInsideOverlay() {
  return useContext(LayerHostContext) !== null;
}

function LayerOutlet({ store }: { readonly store: LayerStore }) {
  const layers = useSyncExternalStore(store.subscribe, store.get, store.get);
  return (
    <>
      {layers.map(({ id, layer: entry }, index) => (
        <View
          key={id}
          style={{
            position: 'absolute',
            top: 0,
            bottom: 0,
            left: 0,
            right: 0,
            zIndex: layer.popover + index,
          }}
          // Only the topmost layer is reachable by assistive tech.
          importantForAccessibility={index === layers.length - 1 ? 'auto' : 'no-hide-descendants'}
          accessibilityElementsHidden={index !== layers.length - 1}
        >
          {entry.element}
        </View>
      ))}
    </>
  );
}

function HiddenWhileLayered({
  store,
  children,
}: {
  readonly store: LayerStore;
  readonly children: ReactNode;
}) {
  const layers = useSyncExternalStore(store.subscribe, store.get, store.get);
  const hidden = layers.length > 0;
  return (
    <View
      style={{ flex: 1 }}
      importantForAccessibility={hidden ? 'no-hide-descendants' : 'auto'}
      accessibilityElementsHidden={hidden}
    >
      {children}
    </View>
  );
}

export interface OverlayProps {
  readonly visible: boolean;
  /** Close button, backdrop tap, Android back and the sheet swipe all call this one path. */
  readonly onDismiss: () => void;
  readonly title: string;
  readonly variant?: OverlayVariant;
  /** A write is in flight: nothing dismisses the surface. */
  readonly busy?: boolean;
  /** Consequence text announced with the title (confirmations). */
  readonly description?: string;
  readonly footer?: ReactNode;
  /** Pinned above the scrolling body (a picker's search field). */
  readonly pinned?: ReactNode;
  readonly children?: ReactNode;
  /** First meaningful element (a form's first field); the title otherwise. */
  readonly initialFocus?: RefObject<{ focus?: () => void } | null>;
  /** The trigger; focus returns to it on close (or to `fallbackFocus` if it is gone). */
  readonly returnFocus?: RefObject<{ focus?: () => void } | null>;
  readonly testID?: string;
}

/** Dialog, confirmation, drawer and bottom sheet (modal-sheet.md). */
export function Overlay(props: OverlayProps) {
  const parent = useContext(LayerHostContext);
  const id = useId();
  const { visible, onDismiss, busy = false } = props;
  const wasVisible = useRef(false);
  const { returnFocus } = props;

  useEffect(() => {
    const closed = wasVisible.current && !visible;
    wasVisible.current = visible;
    if (!closed) return undefined;
    // Restore focus to the trigger after the native modal has gone.
    const timer = setTimeout(() => moveFocusTo(returnFocus?.current), 50);
    return () => clearTimeout(timer);
  }, [visible, returnFocus]);

  if (parent !== null) return <NestedLayer {...props} store={parent} id={id} />;
  return <TopLevelModal {...props} busy={busy} onDismiss={onDismiss} />;
}

function NestedLayer({
  store,
  id,
  ...props
}: OverlayProps & { readonly store: LayerStore; readonly id: string }) {
  const element = <Surface {...props} nested />;
  useLayoutEffect(() => {
    store.set(
      id,
      props.visible ? { element, dismiss: props.onDismiss, busy: props.busy ?? false } : null,
    );
  });
  useLayoutEffect(() => () => store.set(id, null), [store, id]);
  return null;
}

function TopLevelModal(props: OverlayProps & { readonly busy: boolean }) {
  const store = useMemo(() => new LayerStore(), []);
  const { reducedMotion } = useTheme();
  const animation = reducedMotion ? 'none' : props.variant === 'bottom-sheet' ? 'slide' : 'fade';
  return (
    <Modal
      visible={props.visible}
      transparent
      animationType={animation}
      statusBarTranslucent
      navigationBarTranslucent
      onRequestClose={() => {
        if (store.dismissTop()) return;
        if (!props.busy) props.onDismiss();
      }}
      supportedOrientations={['portrait', 'landscape']}
    >
      <LayerHostContext.Provider value={store}>
        <HiddenWhileLayered store={store}>
          <Surface {...props} />
        </HiddenWhileLayered>
        <LayerOutlet store={store} />
      </LayerHostContext.Provider>
    </Modal>
  );
}

function Surface({
  onDismiss,
  title,
  variant = 'dialog',
  busy = false,
  description,
  footer,
  pinned,
  children,
  initialFocus,
  testID,
  nested = false,
}: OverlayProps & { readonly nested?: boolean }) {
  const { colors, scheme } = useTheme();
  const { width, height } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const titleRef = useRef<{ focus?: () => void } | null>(null);
  useEffect(() => {
    const timer = setTimeout(() => moveFocusTo(initialFocus?.current ?? titleRef.current), 80);
    return () => clearTimeout(timer);
  }, [initialFocus]);
  const dismiss = () => {
    if (!busy) onDismiss();
  };
  const swipe = useMemo(
    () =>
      PanResponder.create({
        onMoveShouldSetPanResponder: (_, gesture) => gesture.dy > 8,
        onPanResponderRelease: (_, gesture) => {
          if (gesture.dy > 64) dismiss();
        },
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [busy, onDismiss],
  );
  const backdropOpacity = colors.backdropOpacity;
  const edge = spacing.page['inline-compact'];
  const surface: ViewStyle = {
    backgroundColor: colors.sem.surface.raised,
    ...shadowStyle(scheme, 'floating'),
    overflow: 'hidden',
  };
  const placement: ViewStyle =
    variant === 'bottom-sheet'
      ? {
          position: 'absolute',
          left: 0,
          right: 0,
          bottom: 0,
          maxHeight: height * 0.9,
          paddingBottom: insets.bottom,
        }
      : variant === 'drawer'
        ? {
            position: 'absolute',
            top: 0,
            bottom: 0,
            left: 0,
            width: Math.min(spacing.size['drawer-max'], width * 0.86),
            paddingTop: insets.top,
            paddingBottom: insets.bottom,
          }
        : {
            alignSelf: 'center',
            width: Math.min(spacing.size['dialog-max'], width - edge * 2),
            maxHeight: height - insets.top - insets.bottom - edge * 2,
            borderRadius: radius.panel,
          };
  return (
    <KeyboardAvoidingView
      // Android 15+ is edge to edge inside the modal too: the window does not resize, so pad.
      behavior="padding"
      style={{
        flex: 1,
        justifyContent: variant === 'dialog' || variant === 'confirmation' ? 'center' : 'flex-end',
        zIndex: nested ? layer.popover : layer.dialog,
      }}
    >
      <Pressable
        testID={testID ? `${testID}-backdrop` : undefined}
        accessible={false}
        importantForAccessibility="no"
        onPress={dismiss}
        style={{
          position: 'absolute',
          top: 0,
          bottom: 0,
          left: 0,
          right: 0,
          backgroundColor: colors.sem.overlay['backdrop-base'],
          opacity: backdropOpacity,
        }}
      />
      <View testID={testID} accessibilityViewIsModal aria-modal style={[surface, placement]}>
        {variant === 'bottom-sheet' ? (
          <View
            {...swipe.panHandlers}
            style={{ alignItems: 'center', paddingTop: spacing.stack.sm }}
          >
            <View
              style={{
                width: 40,
                height: 4,
                borderRadius: radius.full,
                backgroundColor: colors.sem.border.strong,
              }}
            />
          </View>
        ) : null}
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: spacing.inline.md,
            paddingLeft: spacing.inset.dialog,
            paddingRight: spacing.inline.sm,
            paddingVertical: spacing.stack.sm,
          }}
        >
          <AppText
            ref={titleRef as never}
            variant="section-title"
            accessibilityRole="header"
            style={{ flex: 1 }}
          >
            {title}
          </AppText>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Fechar"
            accessibilityState={{ disabled: busy }}
            onPress={dismiss}
            style={{
              minWidth: spacing.size['touch-target-comfortable'],
              minHeight: spacing.size['touch-target-comfortable'],
              alignItems: 'center',
              justifyContent: 'center',
            }}
          >
            <Glyph
              name="close"
              color={busy ? colors.sem.text.disabled : colors.sem.text.secondary}
              size={18}
            />
          </Pressable>
        </View>
        {pinned ? (
          <View
            style={{ paddingHorizontal: spacing.inset.dialog, paddingBottom: spacing.stack.sm }}
          >
            {pinned}
          </View>
        ) : null}
        <ScrollView
          style={{ flexGrow: 0, flexShrink: 1 }}
          contentContainerStyle={{
            paddingHorizontal: spacing.inset.dialog,
            paddingBottom: spacing.inset.dialog,
            gap: spacing.stack.md,
          }}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode="interactive"
        >
          {description ? <AppText variant="body">{description}</AppText> : null}
          {children}
        </ScrollView>
        {footer ? (
          <View
            style={{
              borderTopWidth: spacing.size['border-width'],
              borderTopColor: colors.sem.border.default,
              padding: spacing.inset.panel,
              gap: spacing.stack.sm,
            }}
          >
            {footer}
          </View>
        ) : null}
      </View>
    </KeyboardAvoidingView>
  );
}

/**
 * Confirmation of a consequential action: the title states the decision, the body the consequence;
 * confirm names the object and dismiss is **Voltar**. Busy keeps it open; errors stay inside.
 */
export function ConfirmDialog({
  visible,
  title,
  consequence,
  confirmLabel,
  busyLabel,
  destructive = false,
  busy = false,
  onConfirm,
  onDismiss,
  returnFocus,
  children,
  testID,
}: {
  readonly visible: boolean;
  readonly title: string;
  readonly consequence: string;
  readonly confirmLabel: string;
  readonly busyLabel?: string;
  readonly destructive?: boolean;
  readonly busy?: boolean;
  readonly onConfirm: () => void;
  readonly onDismiss: () => void;
  readonly returnFocus?: RefObject<{ focus?: () => void } | null>;
  readonly children?: ReactNode;
  readonly testID?: string;
}) {
  return (
    <Overlay
      visible={visible}
      title={title}
      variant="confirmation"
      description={consequence}
      busy={busy}
      onDismiss={onDismiss}
      returnFocus={returnFocus}
      testID={testID}
      footer={
        <>
          <Button
            label={confirmLabel}
            variant={destructive ? 'danger-solid' : 'primary'}
            loading={busy}
            loadingLabel={busyLabel}
            onPress={onConfirm}
            fullWidth
          />
          <Button
            label="Voltar"
            variant="secondary"
            disabled={busy}
            onPress={onDismiss}
            fullWidth
          />
        </>
      }
    >
      {children}
    </Overlay>
  );
}
