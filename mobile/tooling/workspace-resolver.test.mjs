import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { createWorkspaceResolver } from './workspace-resolver.cjs';

const project = fileURLToPath(new URL('..', import.meta.url));
const appRequire = createRequire(new URL('../package.json', import.meta.url));

test('private and nonexistent Shared subpaths fail before Metro fallback', () => {
  const resolve = createWorkspaceResolver(project, (_, name) => name);
  for (const name of ['@loto/api/src/client', '@loto/api/nonexistent', '@loto/core/src/index'])
    assert.throws(() => resolve({}, name, 'ios'), /Private workspace subpath/);
  for (const name of ['@loto/api', '@loto/api/queries', '@loto/core'])
    assert.equal(resolve({}, name, 'android'), name);
});

test('Shared imports resolve to the app-owned React and query instance', () => {
  const resolve = createWorkspaceResolver(project, (context, name) =>
    createRequire(context.originModulePath).resolve(name),
  );
  const origin = fileURLToPath(new URL('../../shared/api/package.json', import.meta.url));
  for (const name of ['react', 'react/jsx-runtime', '@tanstack/react-query'])
    assert.equal(resolve({ originModulePath: origin }, name, 'ios'), appRequire.resolve(name));
  assert.equal(appRequire('react').version, '19.2.3');
  assert.equal(path.basename(project), 'mobile');
});
