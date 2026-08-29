import { afterEach, describe, expect, it, vi } from 'vitest'

import { fetchApiHealth } from '@/lib/api-health'

describe('fetchApiHealth', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('validates a healthy API response', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: 'Healthy' }), {
        headers: { 'Content-Type': 'application/json' },
        status: 200,
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await expect(fetchApiHealth()).resolves.toEqual({ status: 'Healthy' })
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/health/ready',
      expect.objectContaining({
        signal: null,
      }),
    )
  })

  it('rejects malformed API responses', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ status: 'unknown' }), {
          headers: { 'Content-Type': 'application/json' },
          status: 200,
        }),
      ),
    )

    await expect(fetchApiHealth()).rejects.toThrow()
  })
})
