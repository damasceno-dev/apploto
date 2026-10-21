// Expo/NativeWind use synchronous CommonJS config APIs even in this ESM app.
const { getDefaultConfig } = require('expo/metro-config');
const { withNativeWind } = require('nativewind/metro');
const { createWorkspaceResolver } = require('./tooling/workspace-resolver.cjs');

const config = getDefaultConfig(__dirname);
const fallback =
  config.resolver.resolveRequest ??
  ((context, name, platform) => context.resolveRequest(context, name, platform));
config.resolver.resolveRequest = createWorkspaceResolver(__dirname, fallback);
module.exports = withNativeWind(config, { input: './src/global.css' });
