import AsyncStorage from '@react-native-async-storage/async-storage';
import * as SecureStore from 'expo-secure-store';
import { nativeCredentials } from '../../src/storage/native';
import type { CredentialEnvelope } from '../../src/session/types';
import { profile } from './support';
jest.mock('@react-native-async-storage/async-storage', () =>
  jest.requireActual('@react-native-async-storage/async-storage/jest/async-storage-mock'),
);
jest.mock('expo-secure-store', () => ({
  AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY: 4,
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));
const envelope: CredentialEnvelope = {
  v: 1,
  user: { userId: profile.userId, email: profile.email, name: profile.name },
  tokens: { token: 'secret-identity', refreshToken: 'secret-refresh' },
  branch: null,
};
let secure: string | null;
beforeEach(async () => {
  await AsyncStorage.clear();
  secure = null;
  jest.mocked(SecureStore.getItemAsync).mockImplementation(async () => secure);
  jest.mocked(SecureStore.setItemAsync).mockImplementation(async (_key, value) => {
    secure = value;
  });
  jest.mocked(SecureStore.deleteItemAsync).mockImplementation(async () => {
    secure = null;
  });
});
it('puts credentials only in the platform secure store and keeps normal device storage non-secret', async () => {
  await nativeCredentials.write(envelope);
  expect(JSON.parse(secure!)).toMatchObject({ tokens: envelope.tokens });
  const entries = await AsyncStorage.multiGet(await AsyncStorage.getAllKeys());
  expect(JSON.stringify(entries)).not.toContain('secret-');
  expect(await nativeCredentials.read()).toMatchObject(envelope);
});
it('fresh installation cannot inherit an iOS keychain session left by an earlier install', async () => {
  secure = JSON.stringify(envelope);
  expect(await nativeCredentials.read()).toBeNull();
  expect(secure).toBeNull();
});
it('a durable sign-out tombstone prevents credential restoration when keychain deletion fails', async () => {
  await nativeCredentials.write(envelope);
  jest.mocked(SecureStore.deleteItemAsync).mockRejectedValue(new Error('keychain unavailable'));
  await expect(nativeCredentials.clear()).rejects.toThrow();
  expect(await AsyncStorage.getItem('loto.session.signed-out.v1')).toBe('1');
  await expect(nativeCredentials.read()).rejects.toThrow();
  jest.mocked(SecureStore.deleteItemAsync).mockImplementation(async () => {
    secure = null;
  });
  expect(await nativeCredentials.read()).toBeNull();
});
it('does not fall back to plaintext when native secure persistence rejects a value', async () => {
  jest.mocked(SecureStore.setItemAsync).mockRejectedValue(new Error('native write failed'));
  await expect(nativeCredentials.write(envelope)).rejects.toThrow();
  expect(
    JSON.stringify(await AsyncStorage.multiGet(await AsyncStorage.getAllKeys())),
  ).not.toContain('secret-');
});
