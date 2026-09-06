import { describe, expect, it } from 'vitest'
import { parseStringSearch, stringifyStringSearch } from '@/lib/url-search'

describe('string-only URL search', () => {
  it.each(['123', 'true', 'false', 'null', 'slice1-v1', 'a.b_c-d'])('round trips exact ID %s', modelId => {
    const input = { modelId, asOfUtc: '2021-01-08T00:00:00Z', sort: 'quality-desc' }
    expect(parseStringSearch(stringifyStringSearch(input))).toEqual(input)
  })
  it('keeps duplicates and empty values for boundary rejection', () => {
    expect(parseStringSearch('?modelId=one&modelId=two&asOfUtc=')).toEqual({ modelId: ['one', 'two'], asOfUtc: '' })
    expect(stringifyStringSearch({ modelId: 'true', asOfUtc: undefined })).toBe('?modelId=true')
  })
  it('treats prototype names as data', () => {
    const parsed = parseStringSearch('?__proto__=value&constructor=x')
    expect(Object.getPrototypeOf(parsed)).toBeNull()
    expect(parsed.__proto__).toBe('value')
  })
})
