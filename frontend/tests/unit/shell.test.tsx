import { act, render, screen, within } from '@testing-library/react'
import { createMemoryHistory, createRouter, RouterProvider } from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { routeTree } from '@/routeTree.gen'

async function renderRoute(path: string) {
  const router = createRouter({
    routeTree,
    history: createMemoryHistory({ initialEntries: [path] }),
    defaultPendingMinMs: 0,
  })
  await act(async () => {
    await router.load()
    render(<RouterProvider router={router} />)
  })
}

test('workspace explains the absence of data and offers research context', async () => {
  await renderRoute('/')
  const main = within(screen.getByRole('main'))
  expect(await main.findByRole('heading', { name: 'No research data yet' })).toBeInTheDocument()
  expect(main.getByText(/Data sources have not been connected/)).toBeInTheDocument()
  expect(main.getByRole('link', { name: 'About this workspace' })).toHaveAttribute('href', '/about')
  expect(main.queryByRole('table')).not.toBeInTheDocument()
})

test('about explains the analytics-only product boundary', async () => {
  await renderRoute('/about')
  const main = within(screen.getByRole('main'))
  expect(await main.findByRole('heading', { name: 'An inspectable research process.' })).toBeInTheDocument()
  expect(main.getByText(/Exchange trading and asset custody are outside its scope/)).toBeInTheDocument()
  expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument()
})

test('unknown routes provide a way back to the workspace', async () => {
  await renderRoute('/missing-page')
  expect(await screen.findByRole('heading', { name: 'Page not found' })).toBeInTheDocument()
  expect(screen.getByRole('link', { name: 'Return to workspace' })).toHaveAttribute('href', '/')
})
