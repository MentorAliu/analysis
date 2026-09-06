import { test as base, expect } from '@playwright/test'

// Every scenario fails on runtime errors or attempted data/external requests.
// This shell has no API contract yet; no fabricated API responses are provided.
const test = base.extend({
  page: async ({ page, context, baseURL }, use) => {
    const problems: string[] = []
    page.on('pageerror', (error) => problems.push(error.message))
    page.on('console', (message) => {
      if (message.type() === 'error') problems.push(message.text())
    })
    await context.route('**/*', async (route) => {
      const url = new URL(route.request().url())
      if (url.origin !== baseURL || /^\/api(?:\/|$)/.test(url.pathname)) {
        problems.push(`Unexpected request: ${url.origin}${url.pathname}`)
        await route.abort('blockedbyclient')
      } else {
        await route.continue()
      }
    })
    await use(page)
    expect(problems, 'The shell must load without runtime errors or data requests').toEqual([])
  },
})

test('empty workspace, navigation and browser history remain usable', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'No research data yet' })).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
  await page.getByRole('link', { name: 'About this workspace' }).click()
  await expect(page).toHaveURL('/about')
  await expect(page.getByRole('heading', { name: 'An inspectable research process.' })).toBeVisible()
  await page.goBack()
  await expect(page.getByRole('heading', { name: 'No research data yet' })).toBeVisible()
  await page.goForward()
  await expect(page).toHaveURL('/about')
  await page.getByRole('navigation').getByRole('link', { name: 'Workspace', exact: true }).click()
  await expect(page).toHaveURL('/')
})

test('about loads directly and survives reload', async ({ page }) => {
  await page.goto('/about')
  await expect(page.getByText(/Exchange trading and asset custody are outside its scope/)).toBeVisible()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'An inspectable research process.' })).toBeVisible()
})

test('missing page returns to the empty workspace', async ({ page }) => {
  await page.goto('/missing-page')
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible()
  await page.getByRole('link', { name: 'Return to workspace' }).click()
  await expect(page).toHaveURL('/')
  await expect(page.getByRole('heading', { name: 'No research data yet' })).toBeVisible()
})

test('keyboard users can skip navigation and the layout fits the viewport', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'No research data yet' })).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeFocused()
  await expect(page.getByRole('link', { name: 'Skip to content' })).toBeInViewport()
  await page.keyboard.press('Enter')
  await expect(page.getByRole('main')).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('link', { name: 'About this workspace' })).toBeFocused()
  await page.keyboard.press('Enter')
  await expect(page).toHaveURL('/about')
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
  await expect(page.getByRole('navigation')).toBeInViewport()
})
