import { Outlet, createRootRouteWithContext } from '@tanstack/react-router'

import type { RouterContext } from '@/router-context'

function RootLayout() {
  return (
    <div className="min-h-svh bg-muted/30">
      <header className="border-b bg-background">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
          <span className="font-heading text-base font-medium">
            Crypto Analysis
          </span>
          <span className="text-sm text-muted-foreground">
            Research platform
          </span>
        </div>
      </header>
      <main className="mx-auto max-w-5xl px-6 py-12">
        <Outlet />
      </main>
    </div>
  )
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => (
    <p className="text-sm text-muted-foreground">Page not found.</p>
  ),
})
