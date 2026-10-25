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
      '/api', '/auth', '/culture', '/shop/files', '/health', '/openapi', '/frontend-config.js', '/frontend-assets',
      '^/$', '/faq', '/contact', '/tracking', '/Error', '/not-found', '/login', '/register-shop', '/dashboard', '/shipments',
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
    optimizeDeps: { include: [
      'react', 'react-dom', 'react-dom/client', 'react/jsx-runtime', 'react/jsx-dev-runtime',
      'react-router', '@tanstack/react-query', 'antd', '@ant-design/icons',
      'antd/locale/vi_VN', 'antd/locale/en_US', 'dayjs', 'dayjs/locale/vi', 'dayjs/locale/en',
    ] },
    base: command === 'serve' ? '/' : '/frontend-assets/',
    build: {
      assetsDir: '', sourcemap: false,
      rolldownOptions: { output: { codeSplitting: { groups: [
        { name: 'react-vendor', test: /node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/, priority: 20 },
        { name: 'antd-vendor', test: /node_modules[\\/](antd|@ant-design|@rc-component)[\\/]/, priority: 10 },
        { name: 'vendor', test: /node_modules/, priority: 5 },
      ] } } },
    },
    server: { host: 'localhost', port: 5173, strictPort: true, https, proxy },
  };
});
