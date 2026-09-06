import { z } from 'zod'

export const config = z.object({
  appName: z.string().trim().min(1).max(80).default('Research workspace'),
}).parse({ appName: import.meta.env.VITE_APP_NAME })
