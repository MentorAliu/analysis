import { type PropsWithChildren } from 'react'
import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClientProvider, queryOptions, useQuery } from '@tanstack/react-query'
import { expect, test, vi } from 'vitest'
import { createAppQueryClient } from '@/app/query-client'

interface Note { id: string; title: string; revision: number }

const noteOptions = (id: string) => queryOptions({
  queryKey: ['selector-fixture', id],
  queryFn: async (): Promise<Note> => ({ id, title: 'Reference note', revision: 1 }),
  // Immutable local fixture: isolate projection behavior from freshness/network work.
  staleTime: Infinity,
})

test('a stable select projects the view while retaining complete cache data', async () => {
  const queryClient = createAppQueryClient()
  const options = noteOptions('reference')
  // Stable across hook renders; production selectors without captures live at module scope.
  const selectTitle = vi.fn((note: Note) => ({ title: note.title }))
  function Wrapper({ children }: PropsWithChildren) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
  const view = renderHook(() => useQuery({ ...options, select: selectTitle }).data, { wrapper: Wrapper })
  try {
    await waitFor(() => expect(view.result.current).toEqual({ title: 'Reference note' }))
    const selected = view.result.current
    const selections = selectTitle.mock.calls.length
    expect(queryClient.getQueryData(options.queryKey)).toEqual({ id: 'reference', title: 'Reference note', revision: 1 })

    view.rerender()
    expect(selectTitle).toHaveBeenCalledTimes(selections)
    expect(view.result.current).toBe(selected)

    act(() => { queryClient.setQueryData(options.queryKey, note => note && { ...note, revision: 2 }) })
    await waitFor(() => expect(selectTitle).toHaveBeenCalledTimes(selections + 1))
    expect(view.result.current).toBe(selected)
    expect(queryClient.getQueryData(options.queryKey)).toEqual({ id: 'reference', title: 'Reference note', revision: 2 })

    act(() => { queryClient.setQueryData(options.queryKey, note => note && { ...note, title: 'Updated note' }) })
    await waitFor(() => expect(view.result.current).toEqual({ title: 'Updated note' }))
    expect(queryClient.getQueryData(options.queryKey)).toEqual({ id: 'reference', title: 'Updated note', revision: 2 })
  } finally {
    view.unmount()
    queryClient.clear()
  }
})
