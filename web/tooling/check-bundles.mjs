import assert from 'node:assert/strict';
import { readdir, readFile, realpath } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const build = process.argv[2]
  ? path.resolve(process.argv[2])
  : fileURLToPath(new URL('../.next', import.meta.url));
const manifest = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
const expected = (await realpath(require.resolve('@tanstack/react-query/package.json'))).match(
  /\.pnpm\/([^/]+)\/node_modules\//,
)?.[1];
assert.ok(
  expected?.endsWith(`_react@${manifest.dependencies.react}`),
  'App query dependency must use app React',
);

function sources(map) {
  return [
    ...(map.sources ?? []),
    ...(map.sections ?? []).flatMap((section) => sources(section.map)),
  ];
}
const directory = path.join(build, 'server/chunks/ssr');
const names = (await readdir(directory)).filter((name) => name.endsWith('.map'));
assert.ok(names.length, 'Expected SSR source maps from a production build');
const allSources = (
  await Promise.all(
    names.map(async (name) =>
      sources(JSON.parse(await readFile(path.join(directory, name), 'utf8'))),
    ),
  )
)
  .flat()
  .map(decodeURIComponent);
const queryCopies = new Set(
  allSources.flatMap(
    (source) => source.match(/\.pnpm\/(@tanstack\+react-query@[^/]+)\/node_modules\//)?.[1] ?? [],
  ),
);
assert.deepEqual(
  [...queryCopies],
  [expected],
  'SSR hooks/provider must compile exactly the app-owned TanStack copy',
);
assert.ok(
  allSources.some((source) => source.includes('/react-query/src/QueryClientProvider.')),
  'Expected real provider source in SSR output',
);

const trace = JSON.parse(await readFile(path.join(build, 'server/app/page.js.nft.json'), 'utf8'));
for (const source of [...allSources, ...trace.files]) {
  const react = source.match(/\.pnpm\/react@([^/]+)\/node_modules\/react\//)?.[1];
  // Tracing may retain another workspace's package metadata without executing that React.
  if (react && !source.endsWith('/package.json'))
    assert.equal(
      react,
      manifest.dependencies.react,
      'Shared tooling React must not reach the page runtime',
    );
}
// An ordinary production build carries neither the end-to-end fixtures nor the component gallery.
if (!process.argv[2]) {
  const routes = await readdir(path.join(build, 'server/app'));
  for (const technical of ['e2e', 'styleguide']) {
    assert.ok(!routes.includes(technical), `An ordinary build must not contain /${technical}`);
  }
}
console.log(
  `Web SSR: one app-owned ${expected}; no foreign executable React in source maps/page trace. Next keeps its framework React aliases.`,
);
