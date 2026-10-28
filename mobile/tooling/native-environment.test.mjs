import assert from 'node:assert/strict';
import { test } from 'node:test';
import { nativeEnvironment } from './native-environment.mjs';

const output = (command) =>
  command.endsWith('/bin/java')
    ? 'openjdk version "17.0.21"'
    : command === 'brew'
      ? '/brew/jdk'
      : null;
const exists = (p) =>
  [
    '/valid/bin/java',
    '/brew/jdk/bin/java',
    '/sdk/platform-tools/adb',
    '/home/test/Android/Sdk/platform-tools/adb',
  ].includes(p);
const options = { home: '/home/test', output, exists };
test('valid explicit tools win and parent environment is unchanged', () => {
  const parent = { JAVA_HOME: '/valid', ANDROID_HOME: '/sdk', PATH: '/system' };
  assert.deepEqual(nativeEnvironment(parent, options), {
    ...parent,
    ANDROID_SDK_ROOT: '/sdk',
    PATH: '/valid/bin:/sdk/platform-tools:/sdk/emulator:/system',
  });
  assert.deepEqual(parent, { JAVA_HOME: '/valid', ANDROID_HOME: '/sdk', PATH: '/system' });
});
test('invalid paths fall through to discovered JDK and user SDK', () => {
  const env = nativeEnvironment({ JAVA_HOME: '/missing', ANDROID_HOME: '/missing' }, options);
  assert.equal(env.JAVA_HOME, '/brew/jdk');
  assert.equal(env.ANDROID_HOME, '/home/test/Android/Sdk');
});
test('SDK_ROOT is respected before conventional locations', () => {
  assert.equal(nativeEnvironment({ ANDROID_SDK_ROOT: '/sdk' }, options).ANDROID_HOME, '/sdk');
});
test('unsupported JDK is rejected and no missing-tool path is invented', () => {
  const env = nativeEnvironment(
    { JAVA_HOME: '/valid', ANDROID_HOME: '/sdk', PATH: '/system' },
    { home: '/empty', exists: () => true, output: () => 'openjdk version "11.0.1"' },
  );
  assert.equal(env.JAVA_HOME, undefined);
});
test('absent tools preserve the existing PATH', () => {
  assert.deepEqual(
    nativeEnvironment(
      { PATH: '/system' },
      { home: '/empty', exists: () => false, output: () => null },
    ),
    { PATH: '/system' },
  );
});
