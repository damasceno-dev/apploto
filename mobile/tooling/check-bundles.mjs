import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';

const root = process.argv[2] ?? 'dist/proof';
for (const platform of ['ios', 'android']) {
  const directory = path.join(root, '_expo/static/js', platform);
  const files = (await readdir(directory)).filter((file) => file.endsWith('.map'));
  assert.equal(files.length, 1, `${platform}: expected one bundle map`);
  const map = JSON.parse(await readFile(path.join(directory, files[0]), 'utf8'));
  const react = map.sources.filter((source) => /\/react\/cjs\/react\.production\.js$/.test(source));
  const query = map.sources.filter((source) =>
    /\/react-query\/build\/modern\/QueryClientProvider\.js$/.test(source),
  );
  assert.equal(react.length, 1, `${platform}: duplicate React runtimes: ${react}`);
  assert.ok(react[0].includes('react@19.2.3/'), `${platform}: React must match Expo SDK`);
  assert.equal(query.length, 1, `${platform}: duplicate query contexts: ${query}`);
  assert.ok(query[0].includes('_react@19.2.3/'), `${platform}: app-owned query context required`);
  for (const entry of ['/shared/api/src/index.ts', '/shared/api/src/queries/index.ts'])
    assert.ok(
      map.sources.some((source) => source.endsWith(entry)),
      `${platform}: missing source entry ${entry}`,
    );
  console.log(
    `${platform}: one React 19.2.3 runtime, one app-owned query context; API root and queries source entries compiled.`,
  );
}
console.log(
  'Core is an empty runtime namespace; optimization may omit its empty source mapping. Runtime platform evidence is separate.',
);
