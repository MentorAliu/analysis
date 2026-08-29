import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import { render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'

import { createAppRouter } from '@/router'

afterEach(() => {
  vi.unstubAllGlobals()
})

it('renders the M1 platform status route', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: 'Healthy' }), {
        headers: { 'Content-Type': 'application/json' },
        status: 200,
      }),
    ),
  )

  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  })
  const router = createAppRouter({ queryClient })

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )

  expect(
    await screen.findByRole('heading', {
      name: 'Analysis platform foundation',
    }),
  ).toBeInTheDocument()
  expect(await screen.findByText('Healthy')).toBeInTheDocument()
})
