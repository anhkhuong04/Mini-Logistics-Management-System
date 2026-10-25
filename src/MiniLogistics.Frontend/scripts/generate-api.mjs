import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const schema = fileURLToPath(new URL('../../../docs/Service/browser-api.openapi.json', import.meta.url));
if (!existsSync(schema)) {
  console.error('Browser OpenAPI is not available yet. Generate the browser contract in T02.01 first.');
  process.exit(1);
}
const cli = fileURLToPath(new URL('../node_modules/openapi-typescript/bin/cli.js', import.meta.url));
const result = spawnSync(process.execPath, [cli, schema, '-o', 'src/shared/api/generated/browser-api.d.ts'], {
  stdio: 'inherit',
});
process.exit(result.status ?? 1);
