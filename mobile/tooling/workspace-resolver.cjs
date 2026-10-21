const path = require('node:path');
const api = require('../../shared/api/package.json');
const core = require('../../shared/core/package.json');

const packages = [api, core];

function createWorkspaceResolver(projectRoot, fallback) {
  return function resolveWorkspace(context, moduleName, platform) {
    for (const pkg of packages) {
      if (moduleName === pkg.name || moduleName.startsWith(`${pkg.name}/`)) {
        const key = moduleName === pkg.name ? '.' : `.${moduleName.slice(pkg.name.length)}`;
        if (!Object.hasOwn(pkg.exports, key))
          throw new Error(`Private workspace subpath: ${moduleName}`);
      }
    }
    // Shared's tool-only peers must not create a second native React/query context.
    const appOwned =
      moduleName === 'react' ||
      moduleName.startsWith('react/') ||
      moduleName === 'react-dom' ||
      moduleName.startsWith('react-dom/') ||
      moduleName === '@tanstack/react-query';
    const nextContext = appOwned
      ? { ...context, originModulePath: path.join(projectRoot, 'package.json') }
      : context;
    return fallback(nextContext, moduleName, platform);
  };
}

module.exports = { createWorkspaceResolver };
