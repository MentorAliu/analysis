import { useQuery } from '@tanstack/react-query'
import { createFileRoute } from '@tanstack/react-router'
import { ActivityIcon, DatabaseIcon, ServerCogIcon } from 'lucide-react'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { fetchApiHealth } from '@/lib/api-health'

function IndexPage() {
  const apiHealth = useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => fetchApiHealth(signal),
    retry: false,
    refetchInterval: 30_000,
  })

  const status = apiHealth.isPending
    ? 'Checking'
    : apiHealth.isError
      ? 'Unavailable'
      : apiHealth.data.status

  return (
    <div className="space-y-8">
      <div className="max-w-2xl space-y-3">
        <Badge variant="outline">Milestone M1</Badge>
        <h1 className="font-heading text-4xl font-medium tracking-tight">
          Analysis platform foundation
        </h1>
        <p className="text-base leading-7 text-muted-foreground">
          The frontend, API, worker, PostgreSQL, and Redis runtime skeleton is
          ready for the first provider adapter.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>API</CardTitle>
            <CardDescription>OpenAPI and problem details</CardDescription>
            <CardAction>
              <ServerCogIcon className="size-4 text-muted-foreground" />
            </CardAction>
          </CardHeader>
          <CardContent className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">Readiness</span>
            <Badge
              aria-live="polite"
              variant={apiHealth.isError ? 'destructive' : 'secondary'}
            >
              {status}
            </Badge>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Worker</CardTitle>
            <CardDescription>Separate cancellable host</CardDescription>
            <CardAction>
              <ActivityIcon className="size-4 text-muted-foreground" />
            </CardAction>
          </CardHeader>
          <CardContent>
            <Badge variant="secondary">Configured</Badge>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Data plane</CardTitle>
            <CardDescription>PostgreSQL and Redis</CardDescription>
            <CardAction>
              <DatabaseIcon className="size-4 text-muted-foreground" />
            </CardAction>
          </CardHeader>
          <CardContent>
            <Badge variant="secondary">Compose managed</Badge>
          </CardContent>
        </Card>
      </div>

      {apiHealth.isError ? (
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          <span>The API is not reachable through the development proxy.</span>
          <Button
            size="sm"
            variant="outline"
            onClick={() => void apiHealth.refetch()}
          >
            Retry
          </Button>
        </div>
      ) : null}
    </div>
  )
}

export const Route = createFileRoute('/')({
  component: IndexPage,
})
