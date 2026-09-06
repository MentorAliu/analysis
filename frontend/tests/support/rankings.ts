import { test as base, expect, type Page } from '@playwright/test'
import { zRankingsResponse } from '../../src/lib/api/generated/zod.gen'
import fixture from '../unit/features/rankings/fixtures/rankings.json' with { type: 'json' }

/** Synthetic test data only. The API order is deliberately independent of display sorting. */
export function rankingsFixture(url = new URL('http://127.0.0.1/api/v1/rankings'), variant: 'mixed' | 'complete' | 'not-ready' = 'mixed') {
  const data = zRankingsResponse.parse(structuredClone(fixture))
  data.batch.model.id = url.searchParams.get('modelId') ?? 'slice1-v1'
  data.requestedAsOfUtc = url.searchParams.get('asOfUtc')
  data.selection = data.requestedAsOfUtc ? 'exact' : 'latest'
  if (data.requestedAsOfUtc) data.batch.asOfUtc = data.requestedAsOfUtc.replace('Z', '.000Z')
  if (variant === 'mixed') {
    data.items[0].compositeScore = '12.345678'
    data.items[0].categories[0].score = '-0.000001'
    data.items[1].compositeScore = '12.345679'
    data.items[1].state = 'partial'
    data.items[1].quality.dataQualityPercent = '40.000000'
    Object.assign(data.items[1].categories[2], { state: 'missing', score: null, dataQualityPercent: '0.000000', availableWeightNumerator: 0 })
    Object.assign(data.items[2], { state: 'not-ready', rank: null, compositeScore: null, bullishConfidenceScore: null, bearishConfidenceScore: null })
    data.items[2].quality.corePriceReady = false
    data.items[2].quality.dataQualityPercent = '90.000000'
  }
  if (variant === 'not-ready') for (const item of data.items) {
    Object.assign(item, { state: 'not-ready', rank: null, compositeScore: null, bullishConfidenceScore: null, bearishConfidenceScore: null })
    item.quality.corePriceReady = false
  }
  return data
}
export function problemResponse(status: number, code: string, extra = {}) {
  return { status, contentType: 'application/problem+json', body: { type: `urn:analysis:problem:${code}`, title: 'Synthetic request failure', status, code, correlationId: 'm5-test-reference', traceId: 'm5-test-trace', instance: '/api/v1/rankings', ...extra } }
}
export type MockResponse = { body: unknown; status?: number; contentType?: string }
export type RankingsMock = { requests: URL[]; handler: (url: URL) => MockResponse | Promise<MockResponse> }

export const test = base.extend<{ api: RankingsMock }>({
  api: [async ({ page, context, baseURL }, use) => {
    const problems: string[] = []
    page.on('pageerror', error => problems.push(error.message))
    const api: RankingsMock = { requests: [], handler: url => ({ body: rankingsFixture(url) }) }
    await context.route('**/*', async route => {
      const request = route.request(), url = new URL(request.url())
      if (url.origin === baseURL && url.pathname === '/api/v1/rankings' && request.method() === 'GET' && [...url.searchParams.keys()].every(key => ['modelId', 'asOfUtc'].includes(key))) {
        api.requests.push(url)
        const response = await api.handler(url)
        await route.fulfill({ status: response.status ?? 200, contentType: response.contentType ?? 'application/json', body: JSON.stringify(response.body) })
      } else if (url.origin !== baseURL || /^\/api(?:\/|$)/.test(url.pathname)) {
        problems.push(`Unexpected request: ${request.method()} ${url.origin}${url.pathname}`)
        await route.abort('blockedbyclient')
      } else await route.continue()
    })
    await use(api)
    expect(problems, 'No runtime errors, external requests or unintended API reads').toEqual([])
  }, { auto: true }],
})
export { expect }
export const ready = (page: Page) => expect(page.getByRole('heading', { name: 'Asset comparison', exact: true })).toBeVisible()
export const rowSymbols = (page: Page) => page.locator('tbody th[scope="row"]').evaluateAll(rows => rows.map(row => row.querySelector('span')?.textContent))
