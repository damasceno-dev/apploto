import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import * as Crypto from 'expo-crypto';
import { parseCredentials } from '../session/credentials';
import type { CredentialEnvelope, CredentialStore } from '../session/types';
import type { DeviceStore } from './ports';

const secureKey = 'loto.session.v1';
const installKey = 'loto.install.v1';
const signedOutKey = 'loto.session.signed-out.v1';
const secureOptions = {
  keychainService: 'loto.session.v1',
  keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY,
};
export const nativeDeviceStore: DeviceStore = AsyncStorage;

async function ensureInstallation() {
  if ((await AsyncStorage.getItem(installKey)) === null) {
    // iOS keychain can outlive app uninstallation; a new app install cannot inherit it.
    await SecureStore.deleteItemAsync(secureKey, secureOptions);
    await AsyncStorage.setItem(installKey, '1');
  }
}
export const nativeCredentials: CredentialStore = {
  async read() {
    await ensureInstallation();
    if ((await AsyncStorage.getItem(signedOutKey)) !== null) {
      await SecureStore.deleteItemAsync(secureKey, secureOptions);
      return null;
    }
    const raw = await SecureStore.getItemAsync(secureKey, secureOptions);
    if (raw === null) return null;
    const parsed = parseCredentials(raw);
    if (parsed === null) await SecureStore.deleteItemAsync(secureKey, secureOptions);
    return parsed;
  },
  async write(value: CredentialEnvelope) {
    await ensureInstallation();
    await SecureStore.setItemAsync(secureKey, JSON.stringify(value), secureOptions);
    await AsyncStorage.removeItem(signedOutKey);
  },
  async clear() {
    // Non-secret tombstone prevents a failed keychain deletion from restoring an old bearer.
    let markerError: unknown;
    try {
      await AsyncStorage.setItem(signedOutKey, '1');
    } catch (error) {
      markerError = error;
    }
    await SecureStore.deleteItemAsync(secureKey, secureOptions);
    if (markerError) throw markerError;
  },
};
export const secureUuid = () => Crypto.randomUUID();
export const emailPartitionKey = (email: string) =>
  Crypto.digestStringAsync(Crypto.CryptoDigestAlgorithm.SHA256, email);
export async function clearNativeSelections(userId: string, branchId: string) {
  const prefix = `loto.selection.v1:${userId}:${branchId}:`;
  const keys = (await AsyncStorage.getAllKeys()).filter((key) => key.startsWith(prefix));
  for (const key of keys) await AsyncStorage.removeItem(key);
}
