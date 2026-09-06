import { describe, expect, it, vi } from 'vitest'
import { rankingsSearchSchema, searchErrors, selectionFromSearch } from '@/features/rankings/search'
import { parseStringSearch } from '@/lib/url-search'

describe('ranking selection boundary', () => {
  it('supplies only documented defaults and separates UI sorting', () => {
    expect(rankingsSearchSchema.parse({})).toEqual({ modelId: 'slice1-v1', sort: 'model' })
    expect(selectionFromSearch(rankingsSearchSchema.parse({ modelId: 'true', sort: 'quality-asc' }))).toEqual({ modelId: 'true' })
  })
  it.each(['0001-01-01T00:00:00Z', '2000-02-29T23:00:00Z', '2024-02-29T00:00:00Z'])('accepts real past hour %s', asOfUtc => {
    expect(rankingsSearchSchema.safeParse({ asOfUtc }).success).toBe(true)
  })
  it.each([
    '?ModelId=slice1-v1', '?modelId=BTC', '?modelId=%20slice1-v1', '?modelId=', '?modelId=a&modelId=b', '?other=x', '?sort=wrong', '?sort=model&sort=model',
    '?asOfUtc=', '?asOfUtc=0000-01-01T00:00:00Z', '?asOfUtc=1900-02-29T00:00:00Z', '?asOfUtc=2023-02-29T00:00:00Z',
    '?asOfUtc=2024-04-31T00:00:00Z', '?asOfUtc=2024-01-01T00:30:00Z', '?asOfUtc=2024-01-01T00:00:00.000Z',
    '?asOfUtc=2024-01-01T00:00:00%2B00:00', '?asOfUtc=2024-01-01T24:00:00Z', '?asOfUtc=2024-01-01T00:00:00Z&asOfUtc=2024-01-01T00:00:00Z',
  ])('rejects %s without transforming it', raw => {
    const result = rankingsSearchSchema.safeParse(parseStringSearch(raw))
    expect(result.success).toBe(false)
    if (!result.success) expect(Object.keys(searchErrors(result.error)).length).toBeGreaterThan(0)
  })
  it('checks future hours using the client clock', () => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-09-06T12:30:00Z'))
    expect(rankingsSearchSchema.safeParse({ asOfUtc: '2026-09-06T12:00:00Z' }).success).toBe(true)
    expect(rankingsSearchSchema.safeParse({ asOfUtc: '2026-09-06T13:00:00Z' }).success).toBe(false)
  })
})
