import {
  createContext,
  forwardRef,
  useCallback,
  useContext,
  useEffect,
  useImperativeHandle,
  useRef,
  useState,
} from 'react';
import {
  findNodeHandle,
  Keyboard,
  Platform,
  ScrollView,
  TextInput,
  type ScrollViewProps,
} from 'react-native';
import { spacing } from '@/theme/style';

/** Height of a keyboard that is already on screen (0 when hidden). */
export function currentKeyboardHeight(): number {
  return Keyboard.isVisible() ? (Keyboard.metrics()?.height ?? 0) : 0;
}

/**
 * Current on-screen keyboard height (0 when hidden), starting from the keyboard already open when
 * the caller mounts and following later show/hide events.
 */
export function useKeyboardHeight() {
  const [height, setHeight] = useState(currentKeyboardHeight);
  useEffect(() => {
    const show = Keyboard.addListener(
      Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow',
      (event) => setHeight(event.endCoordinates.height),
    );
    const hide = Keyboard.addListener(
      Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide',
      () => setHeight(0),
    );
    return () => {
      show.remove();
      hide.remove();
    };
  }, []);
  return height;
}

const RevealContext = createContext<() => void>(() => undefined);
/**
 * Brings the currently focused field above the Android keyboard on demand — for a field that
 * gained focus while the keyboard was already open (no keyboard event fires then). No-op on iOS,
 * where the native inset adjustment already follows focus.
 */
export function useRevealFocused() {
  return useContext(RevealContext);
}

/**
 * A screen's scroll container that keeps the focused field reachable above the keyboard. iOS uses
 * the native inset adjustment; Android 15+ draws edge to edge and no longer resizes the window for
 * the keyboard (`adjustResize` has no effect), so the inset and the focused-field reveal are
 * applied here.
 */
export const ScreenScroll = forwardRef<ScrollView, ScrollViewProps>(function ScreenScroll(
  { contentContainerStyle, ...props },
  ref,
) {
  const scroll = useRef<ScrollView>(null);
  useImperativeHandle(ref, () => scroll.current as ScrollView, []);
  const keyboard = useKeyboardHeight();
  const android = Platform.OS === 'android';
  const reveal = useCallback(() => {
    if (!android || currentKeyboardHeight() === 0) return;
    const focused = TextInput.State.currentlyFocusedInput();
    const handle = focused ? findNodeHandle(focused as never) : null;
    if (handle !== null)
      scroll.current?.scrollResponderScrollNativeHandleToKeyboard(
        handle,
        spacing.size['touch-target-comfortable'],
        true,
      );
  }, [android]);
  useEffect(() => {
    if (keyboard !== 0) reveal();
  }, [keyboard, reveal]);
  return (
    <RevealContext.Provider value={reveal}>
      <ScrollView
        ref={scroll}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode={android ? 'on-drag' : 'interactive'}
        automaticallyAdjustKeyboardInsets
        {...props}
        contentContainerStyle={[
          contentContainerStyle,
          android && keyboard > 0 ? { paddingBottom: keyboard + spacing.stack.lg } : null,
        ]}
      />
    </RevealContext.Provider>
  );
});
