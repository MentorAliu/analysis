import { createClient, createConfig } from '@/lib/api/generated/client'
import { getRankings } from '@/lib/api/generated/sdk.gen'
import type { GetRankingsData, RankingsProblem } from '@/lib/api/generated/types.gen'
import { zGetRankingsQuery, zRankingsProblem, zRankingsResponse } from '@/lib/api/generated/zod.gen'

export class RankingsHttpError extends Error {
  readonly problem: RankingsProblem
  constructor(problem: RankingsProblem) {
    super(problem.title)
    this.name = 'RankingsHttpError'
    this.problem = problem
  }
}
export class RankingsContractError extends Error {
  constructor() {
    super('The rankings response does not match the API contract.')
    this.name = 'RankingsContractError'
  }
}

export async function readRankings(query: GetRankingsData['query'] = {}, signal?: AbortSignal) {
  const normalized = zGetRankingsQuery.parse(query)
  const client = createClient(createConfig({
    baseUrl: window.location.origin,
    redirect: 'error',
    cache: 'no-store',
    credentials: 'same-origin',
  }))
  const result = await getRankings({ client, query: normalized, signal, parseAs: 'json', responseStyle: 'fields', throwOnError: false })
  // The generator returns errors as fields, including aborts and validator failures.
  signal?.throwIfAborted()
  if (!result.response) throw result.error instanceof Error ? result.error : new RankingsContractError()
  const mediaType = result.response.headers.get('content-type')?.split(';')[0]?.trim().toLowerCase()
  if (result.response.status !== 200) {
    if (![400, 403, 404, 405, 500, 503].includes(result.response.status) || mediaType !== 'application/problem+json') throw new RankingsContractError()
    const problem = zRankingsProblem.safeParse(result.error)
    if (!problem.success || problem.data.status !== result.response.status) throw new RankingsContractError()
    throw new RankingsHttpError(problem.data)
  }
  if (mediaType !== 'application/json' || result.error !== undefined) throw new RankingsContractError()
  // Explicit parsing also covers the generated Fetch client's empty-body fast path.
  // This schema is generated; there is no parallel handwritten transport model.
  const parsed = zRankingsResponse.safeParse(result.data)
  if (!parsed.success) throw new RankingsContractError()
  return parsed.data
}
