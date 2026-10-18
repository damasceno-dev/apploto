// `pnpm generate` (Shared M2 Phase 2): regenerate `src/generated/` from the committed document.
// Works from any working directory; needs no .NET.
import { generateInto, packageDir } from './codegen.ts';

try {
  await generateInto(packageDir);
  console.log(`Generated ${packageDir}/src/generated from openapi/server-api.openapi.json`);
} catch (error) {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
}
