import { createFileRoute } from '@tanstack/react-router'
import { About } from '@/features/workspace/components/about'

export const Route = createFileRoute('/about')({ component: About })
