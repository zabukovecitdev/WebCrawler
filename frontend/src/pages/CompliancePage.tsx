import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { apiFetch } from '@/lib/api'

export function CompliancePage() {
  const [host, setHost] = useState('example.com')
  const [explainUrl, setExplainUrl] = useState('https://example.com/')

  const robots = useMutation({
    mutationFn: () => apiFetch<unknown>(`/api/v1/hosts/${encodeURIComponent(host)}/robots`),
  })

  const explain = useMutation({
    mutationFn: () =>
      apiFetch<unknown>('/api/v1/hosts/explain', {
        method: 'POST',
        body: JSON.stringify({ url: explainUrl }),
      }),
  })

  const sitemaps = useMutation({
    mutationFn: () => apiFetch<unknown>(`/api/v1/hosts/${encodeURIComponent(host)}/sitemaps`),
  })

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Compliance</h1>
        <p className="text-[var(--color-muted-foreground)]">Robots.txt cache, crawl-delay, and URL allow decisions.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Robots.txt for host</CardTitle>
          <CardDescription>Uses the same cache and parser as the crawler.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-2">
            <div className="space-y-2">
              <Label htmlFor="host">Host</Label>
              <Input id="host" value={host} onChange={(e) => setHost(e.target.value)} />
            </div>
            <Button disabled={robots.isPending} onClick={() => robots.mutate()}>
              Load robots
            </Button>
            <Button variant="outline" disabled={sitemaps.isPending} onClick={() => sitemaps.mutate()}>
              Sitemaps (stub)
            </Button>
          </div>
          {robots.data != null && (
            <pre className="max-h-96 overflow-auto rounded-md bg-[var(--color-muted)] p-3 text-xs">
              {JSON.stringify(robots.data as Record<string, unknown>, null, 2)}
            </pre>
          )}
          {robots.error && <p className="text-sm text-red-600">{(robots.error as Error).message}</p>}
          {sitemaps.data != null && (
            <pre className="max-h-48 overflow-auto rounded-md bg-[var(--color-muted)] p-3 text-xs">
              {JSON.stringify(sitemaps.data as Record<string, unknown>, null, 2)}
            </pre>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Explain URL</CardTitle>
          <CardDescription>Whether SamoBot is allowed, crawl-delay, and cached robots payload.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="url">URL</Label>
            <Input id="url" value={explainUrl} onChange={(e) => setExplainUrl(e.target.value)} />
          </div>
          <Button disabled={explain.isPending} onClick={() => explain.mutate()}>
            Explain
          </Button>
          {explain.data != null && (
            <pre className="max-h-96 overflow-auto rounded-md bg-[var(--color-muted)] p-3 text-xs">
              {JSON.stringify(explain.data as Record<string, unknown>, null, 2)}
            </pre>
          )}
          {explain.error && <p className="text-sm text-red-600">{(explain.error as Error).message}</p>}
        </CardContent>
      </Card>
    </div>
  )
}
