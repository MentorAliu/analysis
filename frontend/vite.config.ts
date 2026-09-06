import { fileURLToPath, URL } from 'node:url'
import { loadEnv } from 'vite'
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import tailwindcss from '@tailwindcss/vite'
import { z } from 'zod'

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, process.cwd(), '')
  const target = z.url().refine(value => ['http:', 'https:'].includes(new URL(value).protocol))
    .parse(process.env.API_PROXY_TARGET ?? environment.API_PROXY_TARGET ?? 'http://127.0.0.1:5080')
  return {
    plugins: [tanstackRouter({ target: 'react', autoCodeSplitting: true }), react(), tailwindcss()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    test: {
      include: ['tests/unit/**/*.test.{ts,tsx}'],
      environment: 'jsdom',
      setupFiles: ['./tests/unit/setup.ts'],
      restoreMocks: true,
      unstubEnvs: true,
      unstubGlobals: true,
    },
    server: { port: 5173, strictPort: true, proxy: { '/api': { target, changeOrigin: true } } },
  }
})
