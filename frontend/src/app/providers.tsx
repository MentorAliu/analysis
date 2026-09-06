import { lazy, Suspense } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from '@tanstack/react-router'
import type { Application } from '@/app/application'

const Devtools = import.meta.env.DEV && import.meta.env.MODE !== 'test'
  ? lazy(() => import('@/app/development-tools'))
  : null

export function ApplicationRoot({ application }: { application: Application }) {
  return (
    <QueryClientProvider client={application.queryClient}>
      <RouterProvider router={application.router} />
      {Devtools && (
        <Suspense fallback={null}>
          <Devtools application={application} />
        </Suspense>
      )}
    </QueryClientProvider>
  )
}
