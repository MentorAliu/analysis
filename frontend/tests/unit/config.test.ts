// @vitest-environment node
import { beforeEach, expect, test, vi } from 'vitest'

beforeEach(() => vi.resetModules())

test('uses a meaningful name when public configuration is absent', async () => {
  vi.stubEnv('VITE_APP_NAME', undefined)
  const { config } = await import('@/lib/config')
  expect(config.appName).toBe('Research workspace')
})

test('trims the configured public name', async () => {
  vi.stubEnv('VITE_APP_NAME', '  Evidence lab  ')
  const { config } = await import('@/lib/config')
  expect(config.appName).toBe('Evidence lab')
})

test('accepts the maximum supported name length', async () => {
  vi.stubEnv('VITE_APP_NAME', 'A'.repeat(80))
  const { config } = await import('@/lib/config')
  expect(config.appName).toHaveLength(80)
})

test.each(['', '   ', 'A'.repeat(81)])('rejects invalid public name %j', async (name) => {
  vi.stubEnv('VITE_APP_NAME', name)
  await expect(import('@/lib/config')).rejects.toThrow()
})
