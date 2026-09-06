import { StrictMode } from 'react'
import { act, render, screen } from '@testing-library/react'
import { queryOptions, useQueryClient } from '@tanstack/react-query'
import { createMemoryHistory, useRouter } from '@tanstack/react-router'
import { expect, test, vi } from 'vitest'
import { ApplicationRoot } from '@/app/providers'
import { createApplication } from '@/app/application'
import { createAppQueryClient } from '@/app/query-client'

const probeOptions = () => queryOptions({
  queryKey: ['foundation', 'probe'],
  queryFn: async () => 'retained',
})

const noteOptions = () => queryOptions({
  queryKey: ['note'],
  queryFn: async () => 'initial',
})

test('the application client preserves Query global defaults', () => {
  const queryClient = createAppQueryClient()
  expect(queryClient.getDefaultOptions()).toEqual({})
  expect(queryClient.getQueryDefaults(probeOptions().queryKey)).toEqual({})
  expect(queryClient.getMutationDefaults(['foundation'])).toEqual({})
  queryClient.clear()
})

// Probe the real application provider and typed root context without adding a page.
vi.mock('@/app/app-layout', async (importOriginal) => ({
  ...await importOriginal<typeof import('@/app/app-layout')>(),
  RootLayout: function ContextProbe() {
    const client = useQueryClient()
    const router = useRouter()
    return <output>{client === router.options.context.queryClient ? 'Shared client' : 'Different clients'}</output>
  },
}))

test('the provider and typed Router context share one stable client through rerenders', async () => {
  const application = createApplication(createMemoryHistory({ initialEntries: ['/'] }))
  await act(() => application.router.load())
  const view = render(<StrictMode><ApplicationRoot application={application} /></StrictMode>)
  expect(await screen.findByText('Shared client')).toBeInTheDocument()
  application.queryClient.setQueryData(probeOptions().queryKey, 'retained')
  view.rerender(<StrictMode><ApplicationRoot application={application} /></StrictMode>)
  expect(screen.getByText('Shared client')).toBeInTheDocument()
  expect(application.router.options.context.queryClient.getQueryData(probeOptions().queryKey)).toBe('retained')
  expect(application.router.options.defaultPreloadStaleTime).toBe(0)
  application.queryClient.clear()
})

test('separate application factories isolate identical cache keys', async () => {
  const first = createApplication(createMemoryHistory())
  const second = createApplication(createMemoryHistory())
  try {
    const options = noteOptions()
    await first.queryClient.fetchQuery(options)
    expect(second.queryClient.getQueryData(options.queryKey)).toBeUndefined()
    await second.queryClient.fetchQuery(options)
    second.queryClient.setQueryData(options.queryKey, 'changed only in second')
    expect(first.queryClient.getQueryData(options.queryKey)).toBe('initial')
    expect(second.queryClient.getQueryData(options.queryKey)).toBe('changed only in second')
  } finally {
    first.queryClient.clear()
    second.queryClient.clear()
  }
})
