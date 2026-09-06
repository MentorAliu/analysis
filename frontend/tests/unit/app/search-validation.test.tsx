import { act, render, screen } from '@testing-library/react'
import { QueryClientProvider, queryOptions, useSuspenseQuery } from '@tanstack/react-query'
import { createMemoryHistory, createRootRouteWithContext, createRoute, createRouter, RouterProvider } from '@tanstack/react-router'
import type { QueryClient } from '@tanstack/react-query'
import { expect, test, vi } from 'vitest'
import { z } from 'zod'
import { createAppQueryClient } from '@/app/query-client'

interface Note { label: string; revision: number }
const selectNoteLabel = (note: Note) => `Note: ${note.label}`

// Direct Zod 4 Standard Schema integration lives only in this memory route.
// This is URL state, not a handwritten backend transport contract.
function createSearchFixture(path: string) {
  const queryClient = createAppQueryClient()
  const loadNote = vi.fn(async (label: string): Promise<Note> => ({ label, revision: 1 }))
  const noteOptions = (label: string) => queryOptions({
    queryKey: ['foundation-note', label],
    queryFn: () => loadNote(label),
    // Test-only immutable fixture: prove loader/hook reuse without background refetch.
    staleTime: Infinity,
  })
  const root = createRootRouteWithContext<{ queryClient: QueryClient }>()()
  const noteRoute = createRoute({
    getParentRoute: () => root,
    path: '/note',
    validateSearch: z.object({ label: z.string().min(1).default('Untitled'), order: z.enum(['asc', 'desc']).default('asc') }),
    loaderDeps: ({ search }) => ({ label: search.label }),
    loader: ({ context, deps }) => context.queryClient.ensureQueryData(noteOptions(deps.label)),
    component: function Note() {
      const { label, order } = noteRoute.useSearch()
      const { data } = useSuspenseQuery({ ...noteOptions(label), select: selectNoteLabel })
      return <p>{data} ({order})</p>
    },
    errorComponent: () => <p role="alert">Invalid note URL</p>,
  })
  const router = createRouter({
    routeTree: root.addChildren([noteRoute]),
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: [path] }),
    defaultPreloadStaleTime: 0,
  })
  return { queryClient, router, loadNote, noteOptions }
}

test.each([
  ['/note?label=Reference&order=desc', 'Note: Reference (desc)', 'Reference'],
  ['/note', 'Note: Untitled (asc)', 'Untitled'],
])('validates URL state and shares fresh loader data with Query: %s', async (path, expected, label) => {
  const fixture = createSearchFixture(path)
  try {
    await act(async () => {
      await fixture.router.load()
      render(<QueryClientProvider client={fixture.queryClient}><RouterProvider router={fixture.router} /></QueryClientProvider>)
    })
    expect(await screen.findByText(expected)).toBeInTheDocument()
    expect(fixture.loadNote).toHaveBeenCalledTimes(1)
    expect(fixture.loadNote).toHaveBeenCalledWith(label)
    expect(fixture.queryClient.getQueryData(fixture.noteOptions(label).queryKey)).toEqual({ label, revision: 1 })
  } finally {
    fixture.queryClient.clear()
  }
})

test('invalid URL search fails before the loader or cache is populated', async () => {
  const fixture = createSearchFixture('/note?label=Reference&order=sideways')
  try {
    await act(() => fixture.router.load())
    expect(fixture.router.state.matches.at(-1)?.status).toBe('error')
    expect(fixture.router.state.matches.at(-1)?.error).toMatchObject({
      cause: { issues: [{ code: 'invalid_value', path: ['order'] }] },
    })
    expect(fixture.loadNote).not.toHaveBeenCalled()
    expect(fixture.queryClient.getQueryCache().getAll()).toHaveLength(0)
  } finally {
    fixture.queryClient.clear()
  }
})
