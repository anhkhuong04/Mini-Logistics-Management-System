import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/frontend-assets/',
  build: { assetsDir: '', sourcemap: false },
  server: { host: 'localhost', port: 5173, strictPort: true },
});
