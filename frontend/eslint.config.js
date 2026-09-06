import js from '@eslint/js'
import globals from 'globals'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import query from '@tanstack/eslint-plugin-query'
import router from '@tanstack/eslint-plugin-router'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist', 'src/routeTree.gen.ts', '.vitest', 'coverage', 'playwright-report', 'test-results', '.playwright-browsers']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, tseslint.configs.recommended, query.configs['flat/recommended-strict'], router.configs['flat/recommended']],
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    extends: [reactHooks.configs.flat.recommended, reactRefresh.configs.vite],
    languageOptions: { globals: globals.browser },
    rules: { 'react-refresh/only-export-components': ['error', { allowExportNames: ['Route'] }] },
  },
  { files: ['tests/**/*.{ts,tsx}'], languageOptions: { globals: { ...globals.browser, ...globals.node } } },
  { files: ['vite.config.ts', 'playwright*.config.ts', 'eslint.config.js'], languageOptions: { globals: globals.node } },
])
