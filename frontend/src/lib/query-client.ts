import { QueryClient } from '@tanstack/react-query'

// No product queries in M1. M4 must define freshness and pass AbortSignal per query.
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 0, retry: false, refetchOnWindowFocus: false, refetchInterval: false },
  },
})
