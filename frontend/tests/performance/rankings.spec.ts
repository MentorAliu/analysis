import { test, expect, type Locator } from '@playwright/test'
import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { cpus, platform, release } from 'node:os'
import { gzipSync } from 'node:zlib'

const profiles = [
  {
    name: 'desktop',
    viewport: { width: 1440, height: 1000 },
    cpu: 1,
    latency: 20,
    down: 10_000_000 / 8,
    up: 5_000_000 / 8,
  },
  {
    name: 'narrow',
    viewport: { width: 390, height: 844 },
    cpu: 4,
    latency: 150,
    down: 1_600_000 / 8,
    up: 750_000 / 8,
  },
]
type Metrics = {
  lcp: number
  cls: number
  ready: number
  responsePaint: number
  longTasks: number[]
  action?: { event: string; start: number; paint: number; kind: string; before: string | null }
}
declare global {
  interface Window {
    m5Metrics: Metrics
  }
}
const percentile = (values: number[], p: number) =>
  [...values].sort((a, b) => a - b)[Math.ceil(values.length * p) - 1]!

for (const profile of profiles)
  test(`production lab ${profile.name}`, async ({ browser, baseURL }) => {
    const baseline = process.env.M5_BASELINE === '1',
      output = process.env.M5_EVIDENCE ?? 'test-results/performance'
    await mkdir(output, { recursive: true })
    const samples: Array<Metrics & { entry: string; js: number; css: number; requests: string[] }> = []
    const interactions: Record<string, number[]> = {}
    const context = await browser.newContext({
      viewport: profile.viewport,
      deviceScaleFactor: 1,
      locale: 'en-GB',
      timezoneId: 'UTC',
      colorScheme: 'light',
      reducedMotion: 'reduce',
      serviceWorkers: 'block',
    })
    const page = await context.newPage(),
      cdp = await context.newCDPSession(page)
    const unexpected: string[] = []
    page.on('request', (request) => {
      if (new URL(request.url()).origin !== baseURL) unexpected.push(request.url())
    })
    await cdp.send('Network.enable')
    await cdp.send('Network.setCacheDisabled', { cacheDisabled: true })
    await cdp.send('Network.emulateNetworkConditions', {
      offline: false,
      latency: profile.latency,
      downloadThroughput: profile.down,
      uploadThroughput: profile.up,
    })
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: profile.cpu })
    await page.addInitScript(() => {
      const metrics: Metrics = { lcp: 0, cls: 0, ready: 0, responsePaint: 0, longTasks: [] }
      window.m5Metrics = metrics
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) metrics.lcp = entry.startTime
      }).observe({ type: 'largest-contentful-paint', buffered: true })
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          const shift = entry as PerformanceEntry & { hadRecentInput: boolean; value: number }
          if (!shift.hadRecentInput) metrics.cls += shift.value
        }
      }).observe({ type: 'layout-shift', buffered: true })
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) metrics.longTasks.push(entry.duration)
      }).observe({ type: 'longtask', buffered: true })
      let wasBusy = false
      const painted = (callback: () => void) => requestAnimationFrame(() => requestAnimationFrame(callback))
      const check = () => {
        if (
          !metrics.ready &&
          (document.querySelector('[data-rankings-ready]') ||
            document.querySelector('h1')?.textContent === 'Research starts with evidence.')
        )
          painted(() => {
            metrics.ready ||= performance.now()
          })
        const busy = document.querySelector('#rankings-refresh')?.getAttribute('aria-disabled') === 'true'
        if (wasBusy && !busy)
          painted(() => {
            metrics.responsePaint = performance.now()
          })
        wasBusy = busy
        const action = metrics.action
        if (!action?.start || action.paint) return
        const done =
          action.kind === 'details'
            ? !!document.querySelector('#ranking-detail-heading')
            : action.kind === 'close'
              ? !document.querySelector('#ranking-detail-heading')
              : action.kind === 'composite' || action.kind === 'quality'
                ? document
                    .querySelector(`th:has(button[data-measure="${action.kind}"])`)
                    ?.getAttribute('aria-sort') !== action.before
                : action.kind === 'refresh' || action.kind === 'submit'
                  ? busy
                  : true
        if (done)
          painted(() => {
            if (metrics.action === action) action.paint ||= performance.now()
          })
      }
      new MutationObserver(check).observe(document, {
        subtree: true,
        childList: true,
        attributes: true,
        characterData: true,
      })
      for (const event of ['click', 'input'])
        document.addEventListener(
          event,
          (received) => {
            const action = metrics.action
            if (action && action.event === event && !action.start) {
              action.start = received.timeStamp
              check()
            }
          },
          true,
        )
    })
    // Trace the cold navigation and every measured interaction; no third-party telemetry.
    const trace: unknown[] = []
    cdp.on('Tracing.dataCollected', (event) => trace.push(...event.value))
    await cdp.send('Tracing.start', {
      categories: 'devtools.timeline,blink.user_timing,loading',
      options: 'record-as-much-as-possible',
    })
    for (let index = 0; index < 20; index++) {
      const entry = !baseline && index % 2 ? 'exact' : 'latest'
      await page.goto(
        `${baseURL}/${entry === 'exact' ? '?modelId=slice1-v1&asOfUtc=2021-01-08T00%3A00%3A00Z' : ''}`,
      )
      await expect(
        page.getByRole('heading', {
          name: baseline ? 'No research data yet' : 'Asset comparison',
          exact: true,
        }),
      ).toBeVisible()
      await page.waitForFunction(() => window.m5Metrics.ready > 0)
      samples.push({
        entry,
        ...(await page.evaluate(() => {
          const resources = performance.getEntriesByType('resource') as PerformanceResourceTiming[]
          return {
            ...window.m5Metrics,
            js: resources
              .filter((r) => new URL(r.name).pathname.endsWith('.js'))
              .reduce((n, r) => n + r.encodedBodySize, 0),
            css: resources
              .filter((r) => new URL(r.name).pathname.endsWith('.css'))
              .reduce((n, r) => n + r.encodedBodySize, 0),
            requests: resources
              .filter((r) => new URL(r.name).pathname.startsWith('/api/'))
              .map((r) => r.name),
          }
        })),
      })
    }
    if (!baseline) {
      // Start timing at the actual browser input event, after Playwright actionability/scrolling.
      // End two frames after the required DOM state exists, to include a paint opportunity.
      async function measure(kind: string, control: Locator, input?: string) {
        await control.scrollIntoViewIfNeeded()
        await control.evaluate((node, name) => node.setAttribute('data-measure', name), kind)
        const before = await control.evaluate((node) => node.closest('th')?.getAttribute('aria-sort') ?? null)
        await page.evaluate(
          ({ kind, before, input }) => {
            window.m5Metrics.action = {
              kind,
              before,
              event: input === undefined ? 'click' : 'input',
              start: 0,
              paint: 0,
            }
          },
          { kind, before, input },
        )
        if (input === undefined) await control.click()
        else await control.fill(input)
        await page.waitForFunction(() => (window.m5Metrics.action?.paint ?? 0) > 0)
        const duration = await page.evaluate(
          () => window.m5Metrics.action!.paint - window.m5Metrics.action!.start,
        )
        ;(interactions[kind] ??= []).push(duration)
      }
      for (let index = 0; index < 20; index++) {
        await measure('composite', page.getByRole('button', { name: /Composite.*Sort|Composite.*Clear/ }))
        await measure('quality', page.getByRole('button', { name: /Data quality.*Sort|Data quality.*Clear/ }))
        await measure('details', page.getByRole('button', { name: 'View details for BTC' }))
        await measure('close', page.getByRole('button', { name: 'Close and return to row' }))
        await measure('input', page.getByRole('textbox', { name: 'Model ID' }), `draft-${index}`)
        await page.getByRole('textbox', { name: 'Model ID' }).fill('slice1-v1')
        for (const kind of ['submit', 'refresh']) {
          await page.evaluate(() => {
            window.m5Metrics.responsePaint = 0
          })
          await measure(
            kind,
            page.getByRole('button', {
              name: kind === 'submit' ? 'Load rankings' : 'Refresh rankings',
              exact: true,
            }),
          )
          await page.waitForFunction(() => window.m5Metrics.responsePaint > 0)
          const latency = await page.evaluate(() => {
            const last = (performance.getEntriesByType('resource') as PerformanceResourceTiming[])
              .filter((r) => new URL(r.name).pathname === '/api/v1/rankings')
              .at(-1)!
            return window.m5Metrics.responsePaint - last.responseEnd
          })
          ;(interactions['response-to-render'] ??= []).push(latency)
        }
      }
    }
    const traceDone = new Promise<void>((resolve) => cdp.once('Tracing.tracingComplete', () => resolve()))
    await cdp.send('Tracing.end')
    await traceDone
    await writeFile(
      `${output}/${baseline ? 'baseline' : 'rankings'}-${profile.name}-trace.json.gz`,
      gzipSync(JSON.stringify({ traceEvents: trace })),
    )
    const report = {
      baseline,
      profile,
      environment: {
        os: `${platform()} ${release()}`,
        cpu: cpus()[0]?.model,
        node: process.version,
        browser: browser.version(),
        limits: { cpus: 4, memoryGiB: 4 },
      },
      samples,
      interactions,
      unexpected,
      summary: {
        lcpP75: percentile(
          samples.map((s) => s.lcp),
          0.75,
        ),
        readyP75: percentile(
          samples.map((s) => s.ready),
          0.75,
        ),
        clsMax: Math.max(...samples.map((s) => s.cls)),
        jsMax: Math.max(...samples.map((s) => s.js)),
        cssMax: Math.max(...samples.map((s) => s.css)),
        interactionP95: Object.fromEntries(
          Object.entries(interactions).map(([name, values]) => [name, percentile(values, 0.95)]),
        ),
      },
    }
    await writeFile(
      `${output}/${baseline ? 'baseline' : 'rankings'}-${profile.name}.json`,
      JSON.stringify(report, null, 2),
    )
    console.log(JSON.stringify(report.summary))
    await context.close()
    expect(unexpected).toEqual([])
    if (!baseline) {
      expect(samples.every((sample) => sample.requests.length === 1)).toBe(true)
      expect(report.summary.lcpP75).toBeLessThanOrEqual(2500)
      expect(report.summary.readyP75).toBeLessThanOrEqual(3000)
      expect(report.summary.clsMax).toBeLessThanOrEqual(0.1)
      for (const [name, duration] of Object.entries(report.summary.interactionP95))
        expect(duration, name).toBeLessThanOrEqual(
          ['response-to-render', 'refresh', 'submit'].includes(name) ? 100 : 200,
        )
      const previous = JSON.parse(await readFile(`${output}/baseline-${profile.name}.json`, 'utf8'))
      expect(report.summary.jsMax - previous.summary.jsMax).toBeLessThanOrEqual(100 * 1024)
      expect(report.summary.cssMax - previous.summary.cssMax).toBeLessThanOrEqual(8 * 1024)
    }
  })
