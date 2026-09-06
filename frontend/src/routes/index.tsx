import { createFileRoute } from '@tanstack/react-router'
import { Workspace } from '@/features/workspace/components/workspace'

export const Route = createFileRoute('/')({ component: Workspace })
