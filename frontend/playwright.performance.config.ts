import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './tests/performance',
  workers: 1,
  retries: 0,
  timeout: 240_000,
  reporter: 'list',
  use: { baseURL: 'http://127.0.0.1:4175', locale: 'en-GB', timezoneId: 'UTC', colorScheme: 'light', serviceWorkers: 'block' },
  webServer: {
    command: 'node tests/performance/server.mjs',
    url: 'http://127.0.0.1:4175',
    reuseExistingServer: false,
    gracefulShutdown: { signal: 'SIGTERM', timeout: 5_000 },
  },
})
