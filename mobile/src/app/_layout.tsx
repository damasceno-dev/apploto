import { Stack } from 'expo-router';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import '../global.css';
import { AppSessionProvider } from '@/session/provider';
import { useTheme, ThemeProvider } from '@/theme/provider';
import { ShellProvider } from '@/shell/provider';
import { ToastProvider } from '@/ui/feedback';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      {/* Presentation preference only; independent of identity/branch session state. */}
      <ThemeProvider>
        <ToastProvider>
          <AppSessionProvider>
            <ShellProvider>
              <NavigationStack />
            </ShellProvider>
          </AppSessionProvider>
        </ToastProvider>
      </ThemeProvider>
    </SafeAreaProvider>
  );
}

function NavigationStack() {
  const { reducedMotion } = useTheme();
  return (
    <Stack
      screenOptions={{
        headerShown: false,
        gestureEnabled: true,
        fullScreenGestureEnabled: true,
        animation: reducedMotion ? 'none' : 'default',
      }}
    />
  );
}
