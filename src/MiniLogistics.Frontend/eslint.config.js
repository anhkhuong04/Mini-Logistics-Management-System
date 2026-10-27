import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import { architectureRule } from './eslint/architecture.mjs';

export default tseslint.config(
  { ignores: ['dist', 'node_modules', 'playwright-report', 'test-results'] },
  js.configs.recommended,
  { files: ['**/*.{js,mjs}'], languageOptions: { globals: globals.node } },
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
      architecture: { rules: { boundaries: architectureRule } },
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      'architecture/boundaries': 'error',
    },
  },
  { files: ['src/test/**'], rules: { 'react-refresh/only-export-components': 'off' } },
);
