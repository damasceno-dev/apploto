import type { ExpoConfig } from 'expo/config';

const config: ExpoConfig = {
  name: 'Lotero',
  slug: 'lotero',
  platforms: ['ios', 'android'],
  scheme: 'lotero',
  version: '0.0.0',
  userInterfaceStyle: 'automatic',
  ios: { bundleIdentifier: 'com.example.lotero.development' },
  android: { package: 'com.example.lotero.development' },
  plugins: ['expo-router', ['expo-secure-store', { configureAndroidBackup: true }]],
  experiments: { typedRoutes: true },
};

export default config;
