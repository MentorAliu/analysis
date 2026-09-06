import { createRouter, type RouterHistory } from '@tanstack/react-router'
import { routeTree } from '@/routeTree.gen'
import { createAppQueryClient } from '@/app/query-client'

export function createApplication(history?: RouterHistory) {
  const queryClient = createAppQueryClient()
  const router = createRouter({
    routeTree,
    context: { queryClient },
    ...(history ? { history } : {}),
    defaultPreload: 'intent',
    // Query owns data freshness; Router coordinates when loaders run.
    defaultPreloadStaleTime: 0,
  })
  return { queryClient, router }
}

export type Application = ReturnType<typeof createApplication>

declare module '@tanstack/react-router' {
  interface Register { router: Application['router'] }
}
