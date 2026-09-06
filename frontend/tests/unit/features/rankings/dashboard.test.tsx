import { QueryClient, QueryClientProvider, focusManager, onlineManager } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { RankingsPage } from '@/features/rankings/components/rankings-page'
import { RankingsTable } from '@/features/rankings/components/rankings-table'
import { rankingsQueryOptions } from '@/features/rankings/queries'
import { zRankingsResponse } from '@/lib/api/generated/zod.gen'
import fixture from './fixtures/rankings.json'

const selection = { modelId: 'slice1-v1' }
const clients: QueryClient[] = []
afterEach(() => { clients.forEach(client => client.clear()); onlineManager.setOnline(true); focusManager.setFocused(undefined) })
function setup() {
  const client = new QueryClient(); clients.push(client)
  const navigate = vi.fn()
  const view = render(<QueryClientProvider client={client}><RankingsPage selection={selection} sort="model" onSelection={navigate} onSort={vi.fn()} /></QueryClientProvider>)
  return { client, navigate, ...view }
}
function mockResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'content-type': status === 200 ? 'application/json' : 'application/problem+json' } })
}
it('retains complete verified data, canonical details and original cache precision during failed refresh; denial hides it', async () => {
  const fetch = vi.fn(async () => mockResponse(fixture)); vi.stubGlobal('fetch', fetch)
  const { client } = setup()
  await screen.findByRole('table')
  fireEvent.click(screen.getByRole('button', { name: 'View details for BTC' }))
  expect(screen.getByRole('heading', { name: 'BTC details' })).toBeInTheDocument()
  expect(screen.getAllByText(/Stored quality zero is a placeholder/)).toHaveLength(2)
  fetch.mockImplementation(async () => mockResponse({ status: 503, code: 'database-unavailable', title: 'Synthetic', type: 'urn:test', correlationId: 'test', traceId: 'test' }, 503))
  fireEvent.click(screen.getByRole('button', { name: 'Refresh rankings' }))
  await screen.findByText(/Previously retrieved; refresh failed/)
  expect(screen.getByRole('heading', { name: 'BTC details' })).toBeInTheDocument()
  expect(client.getQueryData(rankingsQueryOptions().queryKey)).toEqual(fixture)
  fetch.mockImplementation(async () => mockResponse({ status: 403, code: 'private-use-disabled', title: 'Synthetic', type: 'urn:test', correlationId: 'test', traceId: 'test' }, 403))
  fireEvent.click(screen.getByRole('button', { name: 'Retry request' }))
  await screen.findByText('Private access is disabled')
  expect(screen.queryByRole('table')).not.toBeInTheDocument()
  expect(screen.queryByRole('heading', { name: 'BTC details' })).not.toBeInTheDocument()
  expect(client.getDefaultOptions()).toEqual({})
})
it('draft edits and invalid submission do not replace displayed context or request data', async () => {
  const fetch = vi.fn(async () => mockResponse(fixture)); vi.stubGlobal('fetch', fetch)
  const { navigate } = setup()
  await screen.findByRole('table')
  fireEvent.change(screen.getByRole('textbox', { name: 'Model ID' }), { target: { value: 'INVALID' } })
  fireEvent.click(screen.getByRole('button', { name: 'Load rankings' }))
  expect(screen.getByRole('alert')).toHaveTextContent('Check the selection')
  await waitFor(() => expect(screen.getByRole('textbox', { name: 'Model ID' })).toHaveFocus())
  expect(fetch).toHaveBeenCalledTimes(1)
  expect(navigate).not.toHaveBeenCalled()
  expect(screen.getByRole('table')).toBeInTheDocument()
})
it('a new latest batch replaces the whole envelope while preserving selected canonical identity', async () => {
  const fetch = vi.fn(async () => mockResponse(fixture)); vi.stubGlobal('fetch', fetch)
  const { client } = setup()
  await screen.findByRole('table')
  fireEvent.click(screen.getByRole('button', { name: 'View details for BTC' }))
  await waitFor(() => expect(screen.getByRole('heading', { name: 'BTC details' })).toHaveFocus())
  const next = structuredClone(fixture)
  next.batch.id = 'f'.repeat(64)
  next.batch.asOfUtc = '2021-01-08T01:00:00.000Z'
  next.retrievedAtUtc = '2021-01-09T00:00:00.001Z'
  next.asOfAgeSeconds = 82800
  next.items[0]!.compositeScore = '1.000001'
  fetch.mockImplementation(async () => mockResponse(next))
  const refresh = screen.getByRole('button', { name: 'Refresh rankings' })
  refresh.focus(); fireEvent.click(refresh)
  await screen.findByText('+1.000001', { exact: true })
  expect(screen.getByRole('heading', { name: 'BTC details' })).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Refresh rankings' })).toHaveFocus()
  expect(screen.getAllByText('2021-01-08 01:00:00.000 UTC', { exact: true })).toHaveLength(2)
  expect(screen.queryByText('2021-01-08 00:00:00.000 UTC', { exact: true })).not.toBeInTheDocument()
  expect(client.getQueryData(rankingsQueryOptions().queryKey)).toEqual(next)
  expect(fetch).toHaveBeenCalledTimes(2)
})
it('does not start unsolicited reads on focus, reconnect or a successful-cache remount', async () => {
  const fetch = vi.fn(async () => mockResponse(fixture)); vi.stubGlobal('fetch', fetch)
  const { client, unmount } = setup()
  await screen.findByRole('table')
  await act(async () => { focusManager.setFocused(false); focusManager.setFocused(true); onlineManager.setOnline(false); onlineManager.setOnline(true) })
  unmount()
  render(<QueryClientProvider client={client}><RankingsPage selection={selection} sort="model" onSelection={vi.fn()} onSort={vi.fn()} /></QueryClientProvider>)
  expect(screen.getByRole('table')).toBeInTheDocument()
  expect(fetch).toHaveBeenCalledTimes(1)
})
it('cancels paused work and does not resume it on reconnect', async () => {
  onlineManager.setOnline(false)
  const fetch = vi.fn(async () => mockResponse(fixture)); vi.stubGlobal('fetch', fetch)
  setup()
  expect(screen.getByRole('status')).toHaveTextContent('Request paused while offline')
  fireEvent.click(screen.getByRole('button', { name: 'Cancel request' }))
  await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Request cancelled'))
  await act(async () => onlineManager.setOnline(true))
  expect(fetch).not.toHaveBeenCalled()
  expect(screen.getByRole('button', { name: 'Load rankings' })).toHaveFocus()
})
it('sorts exact values and nulls while preserving API ranks, original tie order and quality of not-ready rows', async () => {
  const data = zRankingsResponse.parse(structuredClone(fixture))
  data.items[0].compositeScore = '1.000001'; data.items[1].compositeScore = '1.000002'
  data.items[2].compositeScore = null; data.items[2].rank = null
  data.items[0].quality.dataQualityPercent = '0.000000'
  data.items[1].quality.dataQualityPercent = '20.000000'
  const onSort = vi.fn(), onDetails = vi.fn()
  const view = render(<RankingsTable items={data.items} sort="composite-desc" onSort={onSort} selectedAssetId={null} onDetails={onDetails} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('ETH'))
  expect(within(screen.getAllByRole('row')[1]!).getAllByRole('cell')[0]).toHaveTextContent('2')
  view.rerender(<RankingsTable items={data.items} sort="composite-asc" onSort={onSort} selectedAssetId={null} onDetails={onDetails} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('BTC'))
  expect(screen.getAllByRole('row')[3]).toHaveTextContent('SOL')
  view.rerender(<RankingsTable items={data.items} sort="quality-desc" onSort={onSort} selectedAssetId={null} onDetails={onDetails} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('SOL'))
  expect(screen.getAllByRole('row')[1]).toHaveTextContent('Unranked')
  expect(data.items[0].compositeScore).toBe('1.000001')
  const ties = zRankingsResponse.parse(structuredClone(fixture))
  view.rerender(<RankingsTable items={ties.items} sort="composite-desc" onSort={onSort} selectedAssetId={null} onDetails={onDetails} />)
  await waitFor(() => expect(screen.getAllByRole('row')[1]).toHaveTextContent('BTC'))
  expect(screen.getAllByRole('row')[2]).toHaveTextContent('ETH')
})
