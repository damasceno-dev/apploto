import { Buffer } from 'node:buffer';
import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

export function assertReleaseBoundary(map, bundle, platform) {
  assert.ok(
    !map.sources.some((source) => /(?:^|\/)(?:mobile\/)?src\/probes\//.test(source)),
    `${platform}: technical probe module in production map`,
  );
  for (const marker of [
    'Medir Intl no Hermes',
    'DEV fixture response intentionally lost',
    '/__native-fixture',
  ])
    for (const encoding of ['utf8', 'utf16le'])
      assert.ok(
        !bundle.includes(Buffer.from(marker, encoding)),
        `${platform}: technical marker in production bytecode: ${marker}`,
      );
}

async function check(root) {
  for (const platform of ['ios', 'android']) {
    const directory = path.join(root, '_expo/static/js', platform);
    const files = (await readdir(directory)).filter((file) => file.endsWith('.map'));
    assert.equal(files.length, 1, `${platform}: expected one bundle map`);
    const map = JSON.parse(await readFile(path.join(directory, files[0]), 'utf8'));
    const react = map.sources.filter((source) =>
      /\/react\/cjs\/react\.(?:production|development)\.js$/.test(source),
    );
    const query = map.sources.filter((source) =>
      /\/react-query\/build\/modern\/QueryClientProvider\.js$/.test(source),
    );
    assert.equal(react.length, 1, `${platform}: duplicate React runtimes: ${react}`);
    assert.ok(react[0].includes('react@19.2.3/'), `${platform}: React must match Expo SDK`);
    assert.equal(query.length, 1, `${platform}: duplicate query contexts: ${query}`);
    assert.ok(query[0].includes('_react@19.2.3/'), `${platform}: app-owned query context required`);
    const production = /react\.production\.js$/.test(react[0]);
    if (process.argv.includes('--production'))
      assert.ok(production, `${platform}: expected production React`);
    if (production) {
      const bundle = await readFile(path.join(directory, files[0].slice(0, -4)));
      assertReleaseBoundary(map, bundle, platform);
      console.log(
        `${platform}: production graph/bytecode contain no technical probes or fault-injection markers.`,
      );
    }
    for (const entry of [
      '/shared/api/src/index.ts',
      '/shared/api/src/queries/index.ts',
      '/shared/core/src/index.ts',
      '/shared/core/src/format/money-expression.ts',
      '/shared/core/src/dates/instant.ts',
      '/shared/core/src/labels/enums.ts',
    ])
      assert.ok(
        map.sources.some((source) => source.endsWith(entry)),
        `${platform}: missing source entry ${entry}`,
      );
    console.log(
      `${platform}: one React 19.2.3 runtime, one app-owned query context; API root, queries and actual core formatter/parser/labels sources compiled.`,
    );
  }
  console.log('Runtime platform evidence is separate from source-map inclusion.');
}
if (process.argv[1] && import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href)
  await check(process.argv[2] ?? 'dist/proof');
