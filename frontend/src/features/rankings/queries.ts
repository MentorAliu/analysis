import { queryOptions } from '@tanstack/react-query'
import type { GetRankingsData } from '@/lib/api/generated/types.gen'
import { zGetRankingsQuery } from '@/lib/api/generated/zod.gen'
import { readRankings } from './transport'

export function rankingsQueryOptions(query: GetRankingsData['query'] = {}) {
  const normalized = zGetRankingsQuery.parse(query)
  return queryOptions({
    queryKey: ['rankings', 'v1', normalized] as const,
    queryFn: ({ signal }) => readRankings(normalized, signal),
    // M5: keep a successfully displayed research batch steady until requested.
    // Surface failures immediately for explicit recovery, including permanent 4xx.
    // All global and remaining Query defaults are intentionally unchanged.
    refetchOnMount: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
  })
}
