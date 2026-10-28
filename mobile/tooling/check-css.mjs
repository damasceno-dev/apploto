import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import postcss from 'postcss';
import { hasArbitraryColor, hasPrimitiveReference, hasRawColor } from './colors.mjs';

export function checkCss(source, filename) {
  const errors = [];
  const root = postcss.parse(source, { from: filename });
  root.walkDecls((decl) => {
    if (hasPrimitiveReference(decl.prop) || hasPrimitiveReference(decl.value))
      errors.push(`${filename}:${decl.source.start.line}: primitive token in ${decl.prop}`);
    if (hasRawColor(decl.value))
      errors.push(`${filename}:${decl.source.start.line}: raw color in ${decl.prop}`);
  });
  root.walkAtRules((rule) => {
    if (hasPrimitiveReference(rule.params))
      errors.push(`${filename}:${rule.source.start.line}: primitive token in @${rule.name}`);
    if (hasArbitraryColor(rule.params) || hasRawColor(rule.params))
      errors.push(`${filename}:${rule.source.start.line}: raw color in @${rule.name}`);
  });
  return errors;
}

const appRoot = fileURLToPath(new URL('..', import.meta.url));
const generatedDirectories = new Set([
  'node_modules',
  '.next',
  '.expo',
  'dist',
  'out',
  'ios',
  'android',
]);

async function walk(directory) {
  const errors = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    // Only generated/dependency directories at the app root are skipped, not source folders.
    if (directory === appRoot && generatedDirectories.has(entry.name)) continue;
    const filename = path.join(directory, entry.name);
    if (entry.isDirectory()) errors.push(...(await walk(filename)));
    else if (entry.name.endsWith('.css'))
      errors.push(...checkCss(await readFile(filename, 'utf8'), filename));
  }
  return errors;
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const errors = await walk(appRoot);
  if (errors.length) {
    console.error(errors.join('\n'));
    process.exitCode = 1;
  }
}
