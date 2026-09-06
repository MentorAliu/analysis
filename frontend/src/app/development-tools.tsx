import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import type { Application } from '@/app/application'

export default function DevelopmentTools({ application }: { application: Application }) {
  return (
    <>
      <ReactQueryDevtools client={application.queryClient} initialIsOpen={false} buttonPosition="bottom-right" />
      <TanStackRouterDevtools router={application.router} initialIsOpen={false} position="bottom-left"
        containerElement="aside"
        panelProps={{ role: 'region', 'aria-label': 'Router inspector' }} />
    </>
  )
}
