import { mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const directory = fileURLToPath(new URL('../../../.agent/https/', import.meta.url));
mkdirSync(directory, { recursive: true });
const result = spawnSync(
  'dotnet',
  [
    'dev-certs',
    'https',
    '--export-path',
    join(directory, 'localhost.pem'),
    '--format',
    'PEM',
    '--no-password',
  ],
  { stdio: 'inherit' },
);
process.exit(result.status ?? 1);
