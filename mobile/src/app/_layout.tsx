import { Slot } from 'expo-router';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import '../global.css';
import { AppSessionProvider } from '@/session/provider';

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <AppSessionProvider>
        <Slot />
      </AppSessionProvider>
    </SafeAreaProvider>
  );
}
