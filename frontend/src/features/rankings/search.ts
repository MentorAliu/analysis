import { z } from 'zod'
import { zGetRankingsQuery } from '@/lib/api/generated/zod.gen'

export const rankingSort = z.enum(['model', 'composite-desc', 'composite-asc', 'quality-desc', 'quality-asc'])
export const rankingsSearchSchema = zGetRankingsQuery.extend({ sort: rankingSort.optional().default('model') }).strict().superRefine((value, context) => {
  if (value.asOfUtc && (value.asOfUtc.startsWith('0000-') || Date.parse(value.asOfUtc) > Date.now())) {
    context.addIssue({ code: 'custom', path: ['asOfUtc'], message: 'Use an existing UTC hour in year 0001 or later, no later than now.' })
  }
})
export type RankingsSearch = z.output<typeof rankingsSearchSchema>
export type RankingsSelection = Pick<RankingsSearch, 'modelId' | 'asOfUtc'>
export type RankingSort = RankingsSearch['sort']
export type SelectionErrors = Partial<Record<'modelId' | 'asOfUtc' | 'form', string>>

export function selectionFromSearch(search: RankingsSearch): RankingsSelection {
  return { modelId: search.modelId, ...(search.asOfUtc ? { asOfUtc: search.asOfUtc } : {}) }
}
export function selectionIdentity(selection: RankingsSelection) {
  return JSON.stringify([selection.modelId, selection.asOfUtc ?? null])
}
export function searchErrors(error: z.ZodError): SelectionErrors {
  const errors: SelectionErrors = {}
  for (const issue of error.issues) {
    const field = issue.path[0]
    if (field === 'modelId') errors.modelId = 'Use one exact model ID: 1–64 lowercase letters, digits, dots, underscores or hyphens; begin with a letter or digit.'
    else if (field === 'asOfUtc') errors.asOfUtc = 'Use one real UTC hour: YYYY-MM-DDTHH:00:00Z, year 0001 or later, no later than now.'
    else errors.form = 'The URL contains an unknown, duplicate or invalid selection parameter. Correct the selection or use the default.'
  }
  return errors
}

export const sortLabels: Record<RankingSort, string> = {
  model: 'Model ranking', 'composite-desc': 'Composite, descending', 'composite-asc': 'Composite, ascending',
  'quality-desc': 'Data quality, descending', 'quality-asc': 'Data quality, ascending',
}
