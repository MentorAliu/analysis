import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it, vi } from 'vitest'
import { rankingsQueryOptions } from '@/features/rankings/queries'
import { readRankings, RankingsContractError, RankingsHttpError } from '@/features/rankings/transport'
import { zRankingsResponse } from '@/lib/api/generated/zod.gen'
import fixture from './fixtures/rankings.json'

const problem = { type: 'urn:analysis:problem:batch-not-found', title: 'No batch.', status: 404, correlationId: 'test', traceId: 'test', code: 'batch-not-found' }
function respond(body: unknown, status = 200, contentType = 'application/json') {
  const fetch = vi.fn<(request: Request) => Promise<Response>>().mockImplementation(async () => new Response(JSON.stringify(body), { status, headers: { 'content-type': contentType } }))
  vi.stubGlobal('fetch', fetch)
  return fetch
}

describe('generated rankings contract and transport', () => {
  it('preserves exact backend decimals, UTC, integers, nulls and complete response', async () => {
    const fetch = respond(fixture)
    const result = await readRankings()
    expect(result).toEqual(fixture)
    expect(typeof result.asOfAgeSeconds).toBe('number')
    expect(result.items[0].compositeScore).toBe('0.000000')
    expect(result.items[0].categories[2].score).toBeNull()
    const request = fetch.mock.calls[0]?.[0] as unknown as Request
    expect(request.url).toBe(`${window.location.origin}/api/v1/rankings?modelId=slice1-v1`)
    expect(request.redirect).toBe('error')
    expect(request.cache).toBe('no-store')
  })
  it.each([
    ['numeric score', (data: typeof fixture) => { Object.assign(data.items[0]!, { compositeScore: 0 }) }],
    ['exponent', (data: typeof fixture) => { data.items[0]!.compositeScore = '1e-6' }],
    ['excess precision', (data: typeof fixture) => { data.items[0]!.compositeScore = '0.0000001' }],
    ['trailing newline', (data: typeof fixture) => { data.items[0]!.compositeScore = '0.000000\n' }],
    ['range', (data: typeof fixture) => { data.items[0]!.compositeScore = '100.000001' }],
    ['enum', (data: typeof fixture) => { data.items[0]!.state = 'ready' }],
    ['timestamp', (data: typeof fixture) => { data.retrievedAtUtc = '2021-01-09T00:00:00+00:00' }],
    ['missing field', (data: typeof fixture) => { Reflect.deleteProperty(data.batch, 'knowledgeCutoffUtc') }],
    ['integer string', (data: typeof fixture) => { Object.assign(data.items[0]!, { rank: '1' }) }],
    ['age coercion', (data: typeof fixture) => { Object.assign(data, { asOfAgeSeconds: '86400' }) }],
  ])('rejects %s using generated validation', async (_name, corrupt) => {
    const data = structuredClone(fixture); corrupt(data)
    expect(zRankingsResponse.safeParse(data).success).toBe(false)
    respond(data)
    await expect(readRankings()).rejects.toBeInstanceOf(RankingsContractError)
  })
  it('validates problem details before throwing a typed HTTP error', async () => {
    respond(problem, 404, 'application/problem+json')
    await expect(readRankings()).rejects.toMatchObject({ name: 'RankingsHttpError', problem })
  })
  it.each([
    [{ ...problem, status: 500 }, 404, 'application/problem+json'],
    [{ title: 'incomplete' }, 404, 'application/problem+json'],
    [problem, 404, 'text/html'],
    [problem, 418, 'application/problem+json'],
    [fixture, 201, 'application/json'],
    [fixture, 200, 'text/html'],
  ])('rejects malformed/unexpected HTTP response %#', async (body, status, mediaType) => {
    respond(body, status, mediaType)
    await expect(readRankings()).rejects.toBeInstanceOf(RankingsContractError)
  })
  it('does not bypass validation for an empty success response', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 200, headers: { 'content-type': 'application/json', 'content-length': '0' } })))
    await expect(readRankings()).rejects.toBeInstanceOf(RankingsContractError)
  })
  it('normalizes defaults and isolates exact/model cache identities without policy overrides', () => {
    expect(rankingsQueryOptions().queryKey).toEqual(rankingsQueryOptions({ modelId: 'slice1-v1' }).queryKey)
    expect(rankingsQueryOptions().queryKey).not.toEqual(rankingsQueryOptions({ asOfUtc: '2021-01-08T00:00:00Z' }).queryKey)
    expect(rankingsQueryOptions().queryKey).not.toEqual(rankingsQueryOptions({ modelId: 'stored-v2' }).queryKey)
    expect(Object.keys(rankingsQueryOptions()).sort()).toEqual(['queryFn', 'queryKey'])
    expect(new QueryClient().getDefaultOptions()).toEqual({})
  })
  it('caches only the validated full response and leaves errors out of data', async () => {
    const client = new QueryClient()
    const good = rankingsQueryOptions()
    respond(fixture)
    await client.fetchQuery(good)
    expect(client.getQueryData(good.queryKey)).toEqual(fixture)
    const missing = rankingsQueryOptions({ modelId: 'missing' })
    respond(problem, 404, 'application/problem+json')
    await expect(client.fetchQuery(missing)).rejects.toBeInstanceOf(RankingsHttpError)
    expect(client.getQueryData(missing.queryKey)).toBeUndefined()
    client.clear()
  })
  it('propagates Query cancellation into the actual Fetch Request signal', async () => {
    const client = new QueryClient(), options = rankingsQueryOptions()
    const deferred = () => {
      let resolve = () => {}
      const promise = new Promise<void>(complete => { resolve = complete })
      return { promise, resolve }
    }
    const entered = deferred(), cancelled = deferred()
    vi.stubGlobal('fetch', vi.fn((request: Request) => new Promise<Response>((_resolve, reject) => {
      entered.resolve()
      request.signal.addEventListener('abort', () => { cancelled.resolve(); reject(request.signal.reason) }, { once: true })
    })))
    const pending = client.fetchQuery(options)
    const rejected = expect(pending).rejects.toBeDefined()
    await entered.promise
    await client.cancelQueries(options)
    await cancelled.promise
    await rejected
    expect(client.getQueryData(options.queryKey)).toBeUndefined()
    client.clear()
  })
})
