import AxeBuilder from '@axe-core/playwright'
import { mkdir, writeFile } from 'node:fs/promises'
import { test, expect, ready, rankingsFixture, problemResponse } from '../support/rankings'

for (const state of ['loading', 'success', 'partial', 'not-ready', 'validation', 'error', 'details'] as const) {
  test(`axe A/AA: ${state}`, async ({ page, api }, testInfo) => {
    let release: () => void = () => {}
    const wait = new Promise<void>(resolve => { release = resolve })
    api.handler = async url => {
      if (state === 'loading') await wait
      return state === 'error' ? problemResponse(503, 'schema-not-ready') : { body: rankingsFixture(url, state === 'not-ready' ? 'not-ready' : state === 'partial' || state === 'details' ? 'mixed' : 'complete') }
    }
    await page.goto(state === 'validation' ? '/?modelId=INVALID' : '/')
    if (state === 'loading') await expect(page.getByRole('status')).toContainText('Loading rankings')
    else if (state === 'error' || state === 'validation') await expect(page.getByRole('alert')).toBeVisible()
    else await ready(page)
    if (state === 'details') {
      await page.getByRole('button', { name: 'View details for BTC' }).click()
      await page.getByRole('button', { name: 'Snapshot identifiers and hashes' }).click()
      await page.getByRole('button', { name: 'Batch and model details' }).click()
    }
    try {
      const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze()
      await mkdir(testInfo.outputDir, { recursive: true })
      const evidence = testInfo.outputPath('axe-results.json')
      await writeFile(evidence, JSON.stringify({ violations: results.violations, incomplete: results.incomplete }, null, 2))
      await testInfo.attach('axe-results', { path: evidence, contentType: 'application/json' })
      expect(results.violations).toEqual([])
    } finally { release() }
  })
}

test('keyboard workflow has visible focus, named row controls and touch-sized actions', async ({ page }) => {
  await page.goto('/'); await ready(page)
  const trigger = page.getByRole('button', { name: 'View details for SOL' })
  await trigger.focus(); await page.keyboard.press('Enter')
  await expect(page.getByRole('heading', { name: 'SOL details' })).toBeFocused()
  await page.keyboard.press('Tab')
  await expect(page.getByRole('button', { name: 'Close and return to row' })).toBeFocused()
  await page.keyboard.press('Enter'); await expect(trigger).toBeFocused()
  await expect(trigger).toBeInViewport()
  const controls = await page.locator('.rankings-page button[data-size="touch"], .rankings-page input[data-slot="input"]').evaluateAll(nodes => nodes.map(node => ({ height: node.getBoundingClientRect().height, width: node.getBoundingClientRect().width })))
  expect(controls.length).toBeGreaterThan(0)
  expect(controls.every(rect => rect.height >= 44 && rect.width >= 44)).toBe(true)
  expect(await trigger.evaluate(node => getComputedStyle(node).outlineStyle)).toBe('solid')
})

test('200% text and WCAG spacing reflow outside the contained table at 320 CSS px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 800 })
  await page.goto(`/?modelId=${'m'.repeat(64)}`); await ready(page)
  await page.getByRole('button', { name: 'View details for BTC' }).click()
  await page.getByRole('button', { name: 'Snapshot identifiers and hashes' }).click()
  await page.getByRole('button', { name: 'Batch and model details' }).click()
  await page.addStyleTag({ content: 'html { font-size: 200% !important; } * { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }' })
  const overflow = await page.evaluate(() => ({ width: window.innerWidth, scroll: document.documentElement.scrollWidth, nodes: [...document.querySelectorAll('body *')].filter(node => !node.closest('[role="region"][aria-label="Ranking comparison table"]') && node.getBoundingClientRect().right > innerWidth).map(node => ({ tag: node.tagName, class: node.className, right: node.getBoundingClientRect().right })) }))
  expect(overflow.scroll, JSON.stringify(overflow)).toBeLessThanOrEqual(overflow.width)
  await page.getByRole('button', { name: 'Close and return to row' }).click()
  await expect(page.getByRole('button', { name: 'View details for BTC' })).toBeFocused()
})

test('forced colors, reduced motion and narrow landscape preserve controls', async ({ page, browserName }) => {
  test.skip(browserName === 'webkit', 'Forced colors emulation is not supported by WebKit.')
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' })
  await page.setViewportSize({ width: 844, height: 390 })
  await page.goto('/'); await ready(page)
  await page.getByRole('button', { name: 'View details for ETH' }).focus()
  await page.keyboard.press('Enter')
  await expect(page.getByRole('heading', { name: 'ETH details' })).toBeFocused()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true)
})

test('touch emulation opens and closes details', async ({ page, isMobile }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-narrow' && !isMobile, 'Touch-enabled project only.')
  await page.goto('/'); await ready(page)
  await page.getByRole('button', { name: 'View details for BTC' }).tap()
  await expect(page.getByRole('heading', { name: 'BTC details' })).toBeVisible()
  await page.getByRole('button', { name: 'Close and return to row' }).tap()
  await expect(page.getByRole('button', { name: 'View details for BTC' })).toBeFocused()
})

test('rendered semantic text and essential control colors meet contrast targets', async ({ page, api }, testInfo) => {
  await page.goto('/'); await ready(page)
  await page.getByRole('button', { name: 'View details for BTC' }).click()
  api.handler = () => problemResponse(503, 'database-unavailable')
  await page.getByRole('button', { name: 'Refresh rankings', exact: true }).click()
  await expect(page.getByRole('alert')).toBeVisible()
  // Resolve axe's alpha-OKLab hover-background limitation using rendered pixels.
  await page.locator('tbody tr').first().hover()
  const pairs = await page.evaluate(() => {
    const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1
    const context = canvas.getContext('2d')!
    function luminance(color: string) {
      context.fillStyle = '#fff'; context.fillRect(0, 0, 1, 1); context.fillStyle = color; context.fillRect(0, 0, 1, 1)
      const rgb = [...context.getImageData(0, 0, 1, 1).data].slice(0, 3).map(value => { const channel = value / 255; return channel <= .04045 ? channel / 12.92 : ((channel + .055) / 1.055) ** 2.4 })
      return rgb[0]! * .2126 + rgb[1]! * .7152 + rgb[2]! * .0722
    }
    const cases = [
      ['body', 'color', 'body', 4.5], ['#model-help', 'color', '#model-help', 4.5],
      ['#rankings-load', 'color', '#rankings-load', 4.5], ['[data-score-direction="positive"]', 'color', '[data-score-direction="positive"]', 4.5],
      ['[data-score-direction="negative"]', 'color', '[data-score-direction="negative"]', 4.5],
      ['[data-slot="badge"][class*="bg-attention"]', 'color', '[data-slot="badge"][class*="bg-attention"]', 4.5],
      ['[role="alert"]', 'color', '[role="alert"]', 4.5], ['#rankings-model-id', 'borderTopColor', '#rankings-model-id', 3],
      ['#selection-exact', 'borderTopColor', '#selection-exact', 3], ['#rankings-load', 'backgroundColor', '#rankings-model-id', 3],
      ['tbody tr:first-child td:first-child', 'color', 'tbody tr:first-child', 4.5],
      ['tbody tr:first-child th .text-muted-foreground', 'color', 'tbody tr:first-child', 4.5],
      ['#ranking-details-bitcoin', 'color', 'tbody tr:first-child', 4.5], ['caption', 'color', 'caption', 4.5],
    ] as const
    return cases.map(([selector, property, backgroundSelector, minimum]) => {
      const node = document.querySelector(selector)!, foreground = getComputedStyle(node)[property]
      let parent: Element | null = document.querySelector(backgroundSelector), background = 'rgb(255, 255, 255)'
      while (parent) { const candidate = getComputedStyle(parent).backgroundColor; if (candidate !== 'rgba(0, 0, 0, 0)' && candidate !== 'transparent') { background = candidate; break }; parent = parent.parentElement }
      const a = luminance(foreground), b = luminance(background)
      return { selector, property, foreground, background, minimum, ratio: (Math.max(a, b) + .05) / (Math.min(a, b) + .05) }
    })
  })
  await mkdir(testInfo.outputDir, { recursive: true })
  await writeFile(testInfo.outputPath('contrast.json'), JSON.stringify(pairs, null, 2))
  for (const pair of pairs) expect(pair.ratio, JSON.stringify(pair)).toBeGreaterThanOrEqual(pair.minimum)
})
