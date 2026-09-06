import type { ReactNode } from 'react'
import { LuChevronDown } from 'react-icons/lu'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible'
import { Skeleton } from '@/components/ui/skeleton'
import { RankingsContractError, RankingsHttpError } from '../transport'

export function Disclosure({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Collapsible className="min-w-0">
      <CollapsibleTrigger asChild>
        <Button type="button" variant="ghost" size="touch">
          <LuChevronDown aria-hidden="true" focusable="false" data-icon="inline-start" />
          {title}
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent className="min-w-0 pt-4">{children}</CollapsibleContent>
    </Collapsible>
  )
}

export function RankingsLoading() {
  return (
    <div className="ranking-panel flex min-h-72 flex-col gap-6" aria-hidden="true">
      <Skeleton className="h-7 w-48" />
      <Skeleton className="h-5 w-2/3" />
      <Skeleton className="h-16 w-full" />
      <Skeleton className="h-16 w-full" />
      <Skeleton className="h-16 w-full" />
    </div>
  )
}

export function RankingsFailure({
  error,
  retained,
  exact,
  onRetry,
  onDefault,
  onLatest,
  busy,
}: {
  error: Error
  retained: boolean
  exact: boolean
  onRetry: () => void
  onDefault: () => void
  onLatest: () => void
  busy: boolean
}) {
  const problem = error instanceof RankingsHttpError ? error.problem : undefined
  const code = problem?.code
  let title = 'Unable to retrieve rankings'
  let explanation = 'The request did not complete. Check the connection and try again.'
  if (error instanceof RankingsContractError) {
    title = 'Response could not be verified'
    explanation =
      'The response does not match the expected rankings contract or selection. No new results were accepted.'
  } else if (problem?.status === 403) {
    title = 'Private access is disabled'
    explanation =
      'Rankings are unavailable in this configuration. Retry after private access has been corrected.'
  } else if (code === 'model-not-found') {
    title = 'Model not found'
    explanation = 'No stored model matches this exact ID. Edit the model or explicitly choose the default.'
  } else if (code === 'batch-not-found') {
    title = 'Stored batch not found'
    explanation = exact
      ? 'No stored batch matches this exact UTC hour. Edit the hour or choose latest stored for this model.'
      : 'No stored ranking batch is available for this model.'
  } else if (problem?.status === 400) {
    title = 'Check the selection'
    explanation = 'The service rejected these inputs. Correct the indicated fields and load again.'
  } else if (problem?.status === 503) {
    title = 'Rankings service unavailable'
    explanation =
      code === 'schema-not-ready'
        ? 'The service schema is not ready for rankings reads. Retry after the service is ready.'
        : 'The rankings database is unavailable. Try again when the service has recovered.'
  } else if (problem?.status === 500 || problem?.status === 405) {
    title = 'Rankings request failed'
    explanation =
      'The service could not provide a verified ranking batch. Use the request reference when investigating the failure.'
  }
  return (
    <Alert variant="destructive">
      <AlertTitle>{title}</AlertTitle>
      <AlertDescription>
        <p>{explanation}</p>
        {problem?.errors &&
          Object.entries(problem.errors)
            .filter(([field]) => !['modelId', 'asOfUtc'].includes(field))
            .flatMap(([field, messages]) =>
              messages.map((message, index) => <p key={`${field}-${index}`}>{message}</p>),
            )}
        {retained && (
          <p className="font-medium">
            Previously retrieved; refresh failed. The displayed batch retains its original retrieval time.
          </p>
        )}
        {problem && (
          <p>
            Request reference: <span className="ranking-hash">{problem.correlationId}</span>
          </p>
        )}
        <div className="flex flex-wrap gap-2 pt-3">
          <Button variant="outline" size="touch" type="button" aria-disabled={busy} onClick={onRetry}>
            Retry request
          </Button>
          {code === 'model-not-found' && (
            <Button variant="outline" size="touch" type="button" onClick={onDefault}>
              Use default model
            </Button>
          )}
          {code === 'batch-not-found' && exact && (
            <Button variant="outline" size="touch" type="button" onClick={onLatest}>
              Use latest stored
            </Button>
          )}
        </div>
        {problem && (
          <Disclosure title="Request details">
            <dl className="ranking-metadata">
              {Object.entries(problem)
                .filter(([key, value]) => key !== 'errors' && value != null)
                .map(([key, value]) => (
                  <div key={key}>
                    <dt>{key}</dt>
                    <dd className="ranking-hash">{String(value)}</dd>
                  </div>
                ))}
              {problem.errors && (
                <div>
                  <dt>Validation errors</dt>
                  <dd>
                    <ul className="flex list-inside list-disc flex-col gap-2">
                      {Object.entries(problem.errors).flatMap(([field, messages]) =>
                        messages.map((message, index) => (
                          <li key={`${field}-${index}`}>
                            {field}: {message}
                          </li>
                        )),
                      )}
                    </ul>
                  </dd>
                </div>
              )}
            </dl>
          </Disclosure>
        )}
      </AlertDescription>
    </Alert>
  )
}
