import { Slot } from 'expo-router';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import '../global.css';
import { AppSessionProvider } from '@/session/provider';
import { ThemeProvider } from '@/theme/provider';
import { ToastProvider } from '@/ui/feedback';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      {/* Presentation preference only; independent of identity/branch session state. */}
      <ThemeProvider>
        <ToastProvider>
          <AppSessionProvider>
            <Slot />
          </AppSessionProvider>
        </ToastProvider>
      </ThemeProvider>
    </SafeAreaProvider>
  );
}
