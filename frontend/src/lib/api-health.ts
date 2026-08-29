import { z } from 'zod'

import { environment } from '@/lib/environment'

export const apiHealthSchema = z.object({
  status: z.enum(['Healthy', 'Degraded', 'Unhealthy']),
})

export type ApiHealth = z.infer<typeof apiHealthSchema>

export async function fetchApiHealth(signal?: AbortSignal): Promise<ApiHealth> {
  const response = await fetch(
    `${environment.VITE_API_BASE_PATH}/health/ready`,
    {
      headers: {
        Accept: 'application/json',
      },
      signal: signal ?? null,
    },
  )

  if (!response.ok) {
    throw new Error(`API health check failed with status ${response.status}`)
  }

  return apiHealthSchema.parse(await response.json())
}
