import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export function DashboardPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Dashboard</h1>
        <p className="text-[var(--color-muted-foreground)]">Overview of crawl activity and system health.</p>
      </div>
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Jobs</CardTitle>
            <CardDescription>Create and monitor crawl jobs from the Jobs page.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm text-[var(--color-muted-foreground)]">
            Real-time events stream over SignalR when Redis is configured end-to-end.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Compliance</CardTitle>
            <CardDescription>Inspect robots.txt decisions and crawl-delay.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm text-[var(--color-muted-foreground)]">
            Use Explain URL to see whether SamoBot is allowed to fetch a URL and cached robots metadata.
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Configuration</CardTitle>
            <CardDescription>Depth, budgets, and JS rendering are per job.</CardDescription>
          </CardHeader>
          <CardContent className="text-sm text-[var(--color-muted-foreground)]">
            Set <code className="rounded bg-[var(--color-muted)] px-1">VITE_API_KEY</code> to match{' '}
            <code className="rounded bg-[var(--color-muted)] px-1">DashboardApi:Key</code> in the API.
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
