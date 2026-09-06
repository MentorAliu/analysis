import { Link } from '@tanstack/react-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export function Workspace() {
  return (
    <section className="py-16 sm:py-24">
      <p className="mb-5 text-xs font-medium uppercase tracking-[0.18em] text-primary">Crypto intelligence</p>
      <h1 className="max-w-2xl text-4xl font-semibold leading-tight tracking-tight sm:text-5xl">Research starts with evidence.</h1>
      <p className="mt-6 max-w-xl text-base leading-7 text-muted-foreground">A place to inspect market observations, understand analytical signals, and follow how the evidence changes.</p>
      <Card className="mt-12 max-w-2xl border-dashed shadow-none">
        <CardHeader><CardTitle><h2>No research data yet</h2></CardTitle></CardHeader>
        <CardContent className="space-y-5 text-sm leading-6 text-muted-foreground">
          <p>Data sources have not been connected. This workspace will show observations and rankings when validated data becomes available.</p>
          <Link to="/about" className="inline-block font-medium text-primary underline underline-offset-4">About this workspace <span aria-hidden="true">↗</span></Link>
        </CardContent>
      </Card>
    </section>
  )
}
