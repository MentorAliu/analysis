import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, beforeEach, vi } from 'vitest'

// Vitest APIs are explicitly imported, so register React cleanup explicitly too.
afterEach(cleanup)
// jsdom has no layout/scroll implementation; browser projects verify navigation.
beforeEach(() => {
  vi.stubGlobal('scrollTo', vi.fn())
  // Radix radio controls observe geometry; real layout is verified by Playwright.
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} })
})
