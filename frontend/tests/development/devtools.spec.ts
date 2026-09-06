import { expect, test } from '@playwright/test'

test('development inspectors expose the application cache and current route', async ({ page, context, baseURL }) => {
  const errors: string[] = []
  page.on('pageerror', error => errors.push(error.message))
  page.on('console', message => { if (message.type() === 'error') errors.push(message.text()) })
  await context.route('**/*', async route => {
    const url = new URL(route.request().url())
    if (url.origin !== baseURL || /^\/api(?:\/|$)/.test(url.pathname)) {
      errors.push(`Unexpected request: ${url.origin}${url.pathname}`)
      await route.abort()
    } else {
      await route.continue()
    }
  })
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'No research data yet' })).toBeVisible()
  await page.getByRole('button', { name: 'Open Tanstack query devtools', exact: true }).click()
  await expect(page.getByLabel('Filter queries by query key')).toBeVisible()
  await expect(page.getByLabel('Fresh: 0', { exact: true })).toBeVisible()
  await expect(page.getByLabel('Fetching: 0', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Close Tanstack query devtools', exact: true }).click()
  const openRouter = page.getByRole('button', { name: 'Open TanStack Router Devtools', exact: true })
  await openRouter.click()
  const inspector = page.getByRole('region', { name: 'Router inspector' })
  await expect(inspector).toBeVisible()
  await expect(inspector.locator('code').filter({ hasText: /^\/$/ }).first()).toBeVisible()
  // The overlay remains mounted while the real router navigates.
  await page.getByRole('navigation').getByRole('link', { name: 'About', exact: true }).click()
  await expect(page).toHaveURL('/about')
  await expect(inspector.locator('code').filter({ hasText: /^\/about$/ }).first()).toBeVisible()
  await page.goBack()
  await expect(inspector.locator('code').filter({ hasText: /^\/$/ }).first()).toBeVisible()
  expect(errors).toEqual([])
})
