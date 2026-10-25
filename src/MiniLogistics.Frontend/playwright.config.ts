import { defineConfig, devices } from '@playwright/test';
import { fileURLToPath } from 'node:url';
import { randomUUID } from 'node:crypto';

const root = fileURLToPath(new URL('../../', import.meta.url));
const externalUrl = process.env.MINILOGISTICS_E2E_BASE_URL;

export default defineConfig({
  testDir: './e2e',
  outputDir: process.env.MINILOGISTICS_E2E_SENSITIVE ? './test-results/identity' : './test-results/shell',
  fullyParallel: true,
  workers: 2,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: 'list',
  use: {
    baseURL: externalUrl ?? 'https://localhost:15173/app/',
    ignoreHTTPSErrors: true, // Isolated local browser-test certificates only; proxy TLS remains strict.
    trace: process.env.MINILOGISTICS_E2E_SENSITIVE ? 'off' : 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: externalUrl ? undefined : [
    {
      command: 'dotnet run --project ../MiniLogistics.Web --no-launch-profile --no-build -c Release --urls https://localhost:17195',
      url: 'https://localhost:17195/health/live',
      ignoreHTTPSErrors: true,
      reuseExistingServer: false,
      timeout: 60_000,
      env: {
        ASPNETCORE_ENVIRONMENT: 'BrowserTests',
        BrowserSpa__Enabled: 'true',
        BrowserSpa__DistPath: fileURLToPath(new URL('./dist/', import.meta.url)),
        ConnectionStrings__DefaultConnection: `Server=(localdb)\\MSSQLLocalDB;Database=MiniLogisticsBrowser_${randomUUID().replaceAll('-', '')};Trusted_Connection=True;TrustServerCertificate=True`,
        Kestrel__Certificates__Default__Path: `${root}.agent/https/localhost.pem`,
        Kestrel__Certificates__Default__KeyPath: `${root}.agent/https/localhost.key`,
      },
    },
    {
      command: 'npm run dev -- --port 15173',
      url: 'https://localhost:15173/app/',
      ignoreHTTPSErrors: true,
      reuseExistingServer: false,
      env: { MINILOGISTICS_BACKEND_URL: 'https://localhost:17195' },
    },
  ],
});
