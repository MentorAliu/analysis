import { Link, Outlet } from '@tanstack/react-router'
import { config } from '@/lib/config'

export function NotFound() {
  return (
    <section className="space-y-4 py-16">
      <h1 className="text-3xl font-semibold">Page not found</h1>
      <Link to="/" className="text-primary underline underline-offset-4">Return to workspace</Link>
    </section>
  )
}

export function RootLayout() {
  return (
    <div className="mx-auto flex min-h-svh max-w-5xl flex-col px-6 sm:px-10">
      <a className="sr-only focus:not-sr-only focus:py-4" href="#main">Skip to content</a>
      <header className="flex flex-wrap items-center justify-between gap-6 border-b py-7">
        <Link to="/" className="font-semibold tracking-tight">{config.appName}</Link>
        <nav aria-label="Main navigation" className="flex gap-6 text-sm">
          <Link to="/" activeProps={{ className: 'text-primary underline underline-offset-8' }} activeOptions={{ exact: true }}>Workspace</Link>
          <Link to="/about" activeProps={{ className: 'text-primary underline underline-offset-8' }}>About</Link>
        </nav>
      </header>
      <main id="main" className="flex-1" tabIndex={-1}><Outlet /></main>
      <footer className="border-t py-6 text-xs text-muted-foreground">A workspace for analytics and research.</footer>
    </div>
  )
}
