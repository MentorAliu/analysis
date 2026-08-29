import { createRouter } from '@tanstack/react-router'

import { routeTree } from '@/routeTree.gen'
import type { RouterContext } from '@/router-context'

export function createAppRouter(context: RouterContext) {
  return createRouter({
    routeTree,
    context,
    defaultPreload: 'intent',
    scrollRestoration: true,
  })
}

export type AppRouter = ReturnType<typeof createAppRouter>

declare module '@tanstack/react-router' {
  interface Register {
    router: AppRouter
  }
}
