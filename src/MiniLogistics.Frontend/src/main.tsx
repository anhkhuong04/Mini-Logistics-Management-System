import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import 'antd/dist/reset.css';
import { App } from './app/App';
import { AppProviders } from './app/providers/AppProviders';
import './app/styles/global.css';
import { SessionProvider } from './shared/auth/SessionProvider';

const runtimeConfigUrl = '/frontend-config.js';
await import(/* @vite-ignore */ runtimeConfigUrl);

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders locale={document.documentElement.lang === 'en' ? 'en-US' : 'vi-VN'}>
      <BrowserRouter basename={window.__MINI_LOGISTICS_CONFIG__!.basePath}>
        <SessionProvider>
          <App />
        </SessionProvider>
      </BrowserRouter>
    </AppProviders>
  </StrictMode>,
);
