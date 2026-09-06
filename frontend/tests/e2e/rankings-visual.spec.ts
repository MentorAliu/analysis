import { test, expect, ready } from '../support/rankings'

for (const viewport of [{ width: 1440, height: 1000 }, { width: 390, height: 844 }, { width: 320, height: 800 }]) {
  test(`reviewed comparison and provenance ${viewport.width}`, async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Baselines use one fixed pinned Chromium Linux rendering environment.')
    await page.setViewportSize(viewport)
    await page.clock.setFixedTime(new Date('2026-09-06T12:00:00.000Z'))
    await page.goto('/'); await ready(page)
    await expect(page).toHaveScreenshot(`rankings-${viewport.width}.png`, { fullPage: true, animations: 'disabled' })
    await page.getByRole('region', { name: 'Ranking comparison table', exact: true }).evaluate(node => { node.scrollLeft = node.scrollWidth })
    await expect(page).toHaveScreenshot(`rankings-${viewport.width}-table-end.png`, { fullPage: true, animations: 'disabled' })
    await page.getByRole('button', { name: 'View details for BTC' }).click()
    await page.getByRole('button', { name: 'Snapshot identifiers and hashes' }).click()
    await page.getByRole('button', { name: 'Batch and model details' }).click()
    await expect(page).toHaveScreenshot(`rankings-${viewport.width}-details.png`, { fullPage: true, animations: 'disabled' })
  })
}
