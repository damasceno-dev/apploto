import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const directory = mkdtempSync(path.join(tmpdir(), 'loto-listener-test-'));
const app = fileURLToPath(new URL('../', import.meta.url));
const include = path.join(directory, 'include');
const stub = `#pragma once
#include <functional>
#include <memory>
#include <optional>
#include <cstring>
#include <vector>
namespace facebook::react {
using SurfaceId = int;
struct ShadowViewMutation { enum class Type {Remove, Insert}; Type type; struct {const char *componentName; int tag;} oldChildShadowView; };
using ShadowViewMutationList = std::vector<ShadowViewMutation>;
struct TransactionTelemetry {};
struct MountingTransaction { using Number=int; SurfaceId surfaceId; Number number; ShadowViewMutationList mutations; TransactionTelemetry telemetry; };
struct MountingOverrideDelegate {
 virtual ~MountingOverrideDelegate()=default;
 virtual bool shouldOverridePullTransaction() const=0;
 virtual std::optional<MountingTransaction> pullTransaction(SurfaceId,MountingTransaction::Number,const TransactionTelemetry&,ShadowViewMutationList) const=0;
};
}
`;
try {
  for (const name of [
    'react/renderer/componentregistry/ComponentDescriptorFactory.h',
    'react/renderer/mounting/MountingOverrideDelegate.h',
    'react/renderer/mounting/ShadowView.h',
    'react/renderer/mounting/ShadowViewMutation.h',
  ]) {
    const target = path.join(include, name);
    mkdirSync(path.dirname(target), { recursive: true });
    writeFileSync(target, stub);
  }
  // One stub definition even though the actual source includes multiple renderer headers.
  writeFileSync(path.join(include, 'renderer-stub.h'), stub);
  for (const name of [
    'react/renderer/componentregistry/ComponentDescriptorFactory.h',
    'react/renderer/mounting/MountingOverrideDelegate.h',
    'react/renderer/mounting/ShadowView.h',
    'react/renderer/mounting/ShadowViewMutation.h',
  ])
    writeFileSync(path.join(include, name), '#pragma once\n#include "renderer-stub.h"\n');
  const source =
    process.argv.slice(2).find((arg) => !arg.startsWith('--')) ??
    path.join(app, 'node_modules/react-native-screens/cpp');
  const compiler = process.env.CXX ?? 'clang++';
  const executable = path.join(directory, 'listener-test');
  execFileSync(
    compiler,
    [
      '-std=c++20',
      '-pthread',
      '-O1',
      ...(process.argv.includes('--tsan') ? ['-fsanitize=thread', '-g'] : []),
      '-I',
      include,
      '-I',
      source,
      path.join(app, 'test/native/screens-listener.cpp'),
      path.join(source, 'RNSScreenRemovalListener.cpp'),
      '-o',
      executable,
    ],
    { stdio: 'inherit', timeout: 60000 },
  );
  execFileSync(executable, [], { stdio: 'inherit', timeout: 60000 });
  console.log(
    'Native listener: ownership, stale teardown, disarmed passthrough, reentrancy and 80,000 concurrent cycles passed.',
  );
} finally {
  rmSync(directory, { recursive: true, force: true });
}
