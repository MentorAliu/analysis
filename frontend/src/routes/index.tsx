import { createFileRoute, stripSearchParams, useLocation } from '@tanstack/react-router'
import { RankingsPage, InvalidRankingsSelection } from '@/features/rankings/components/rankings-page'
import {
  rankingsSearchSchema,
  searchErrors,
  selectionFromSearch,
  selectionIdentity,
  type RankingsSelection,
} from '@/features/rankings/search'
import { parseStringSearch } from '@/lib/url-search'

export const Route = createFileRoute('/')({
  validateSearch: rankingsSearchSchema,
  search: { middlewares: [stripSearchParams({ sort: 'model' })] },
  component: function RankingsRoute() {
    const search = Route.useSearch()
    const navigate = Route.useNavigate()
    const selection = selectionFromSearch(search)
    return (
      <RankingsPage
        key={selectionIdentity(selection)}
        selection={selection}
        sort={search.sort}
        onSelection={(next) => {
          void navigate({ search: { ...next }, resetScroll: false })
        }}
        onSort={(sort) => {
          void navigate({
            search: { ...selection, ...(sort === 'model' ? {} : { sort }) },
            replace: true,
            resetScroll: false,
          })
        }}
      />
    )
  },
  errorComponent: function InvalidSelectionRoute() {
    const raw = useLocation({ select: (location) => location.searchStr })
    const navigate = Route.useNavigate()
    const parameters = parseStringSearch(raw)
    const checked = rankingsSearchSchema.safeParse(parameters)
    if (checked.success)
      return (
        <div className="rankings-page">
          <h1 className="text-3xl font-semibold">Rankings</h1>
          <p role="alert">The workspace could not be displayed. Reload the page to try again.</p>
        </div>
      )
    const draft = (value: unknown) =>
      Array.isArray(value) ? value.join(', ') : typeof value === 'string' ? value : ''
    const selection: RankingsSelection = {
      modelId: parameters.modelId === undefined ? 'slice1-v1' : draft(parameters.modelId),
      ...(parameters.asOfUtc !== undefined ? { asOfUtc: draft(parameters.asOfUtc) } : {}),
    }
    return (
      <InvalidRankingsSelection
        key={raw}
        selection={selection}
        errors={searchErrors(checked.error)}
        onSelection={(next) => {
          void navigate({ search: { ...next }, resetScroll: false })
        }}
      />
    )
  },
})
