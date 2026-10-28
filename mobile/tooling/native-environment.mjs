import { existsSync } from 'node:fs';
import { homedir } from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

function read(command, args) {
  const result = spawnSync(command, args, { encoding: 'utf8' });
  return result.status === 0 ? (result.stdout || result.stderr).trim() : null;
}

// Child-only environment; never changes the user's shell or global tool selections.
export function nativeEnvironment(
  parent = process.env,
  { home = homedir(), exists = existsSync, output = read } = {},
) {
  const env = { ...parent };
  function javaHome(candidate) {
    if (!candidate || !exists(path.join(candidate, 'bin/java'))) return null;
    const version = output(path.join(candidate, 'bin/java'), ['-version']);
    const major = /version "(\d+)/.exec(version ?? '')?.[1];
    return major && Number(major) >= 17 ? candidate : null;
  }
  let java = javaHome(parent.JAVA_HOME) ?? javaHome(output('/usr/libexec/java_home', ['-v', '17']));
  if (!java) {
    const brew = output('brew', ['--prefix', 'openjdk@17']);
    java = javaHome(brew && path.join(brew, 'libexec/openjdk.jdk/Contents/Home')) ?? javaHome(brew);
  }
  if (java) env.JAVA_HOME = java;
  else delete env.JAVA_HOME;
  const sdk = [
    parent.ANDROID_HOME,
    parent.ANDROID_SDK_ROOT,
    path.join(home, 'Library/Android/sdk'),
    path.join(home, 'Android/Sdk'),
  ].find((candidate) => candidate && exists(path.join(candidate, 'platform-tools/adb')));
  if (sdk) {
    env.ANDROID_HOME = sdk;
    env.ANDROID_SDK_ROOT = sdk;
  } else {
    delete env.ANDROID_HOME;
    delete env.ANDROID_SDK_ROOT;
  }
  env.PATH = [
    java && path.join(java, 'bin'),
    sdk && path.join(sdk, 'platform-tools'),
    sdk && path.join(sdk, 'emulator'),
    parent.PATH,
  ]
    .filter(Boolean)
    .join(path.delimiter);
  return env;
}
