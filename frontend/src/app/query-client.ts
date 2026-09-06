import { QueryClient } from '@tanstack/react-query'

// Preserve Query's defaults. Feature-owned queryOptions may document per-query policy.
export function createAppQueryClient() {
  return new QueryClient()
}
