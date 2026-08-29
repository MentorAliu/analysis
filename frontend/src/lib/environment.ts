import { z } from 'zod'

const environmentSchema = z.object({
  VITE_API_BASE_PATH: z.string().startsWith('/').default('/api'),
})

export const environment = environmentSchema.parse(import.meta.env)
