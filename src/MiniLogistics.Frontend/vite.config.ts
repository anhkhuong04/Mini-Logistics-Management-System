import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { Agent } from 'node:https';

export default defineConfig(({ command }) => {
  const https = command === 'serve' ? {
    cert: readFileSync(process.env.MINILOGISTICS_HTTPS_CERT ?? fileURLToPath(new URL('../../.agent/https/localhost.pem', import.meta.url))),
    key: readFileSync(process.env.MINILOGISTICS_HTTPS_KEY ?? fileURLToPath(new URL('../../.agent/https/localhost.key', import.meta.url))),
  } : undefined;
  const proxy = command === 'serve' ? Object.fromEntries(
    [
      '/api', '/auth', '/culture', '/shop/files', '/frontend-config.js', '/frontend-assets',
      '^/$', '/faq', '/contact', '/login', '/register-shop', '/dashboard', '/shipments',
      '/profile', '/shop/', '/operations', '/shipper', '/admin', '/partner/integrations',
      '/images', '/data', '/lib', '/app.', '/MiniLogistics.Web', '/_framework', '/_content', '/_blazor',
    ].map(path => [path, {
      target: process.env.MINILOGISTICS_BACKEND_URL ?? 'https://localhost:7195',
      secure: true,
      agent: new Agent({ ca: https!.cert }),
      ws: path === '/_blazor',
    }]),
  ) : undefined;

  return {
  plugins: [react()],
  base: command === 'serve' ? '/' : '/frontend-assets/',
  build: { assetsDir: '', sourcemap: false },
  server: { host: 'localhost', port: 5173, strictPort: true, https, proxy },
  };
});
