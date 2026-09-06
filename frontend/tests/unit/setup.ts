import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Vitest APIs are explicitly imported, so register React cleanup explicitly too.
afterEach(cleanup)
