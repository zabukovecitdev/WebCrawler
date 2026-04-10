import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { apiFetch } from '@/lib/api'
import { apiBase, apiKey } from '@/lib/config'
import type { CrawlJob, CrawlJobEvent, DiscoveredUrlRow } from '@/types/crawl'

export function JobDetailPage() {
  const { id } = useParams<{ id: string }>()
  const jobId = Number(id)
  const [live, setLive] = useState<unknown[]>([])

  const { data: job } = useQuery({
    queryKey: ['crawl-job', jobId],
    queryFn: () => apiFetch<CrawlJob>(`/api/v1/crawl-jobs/${jobId}`),
    enabled: Number.isFinite(jobId),
  })

  const { data: pages } = useQuery({
    queryKey: ['crawl-job-pages', jobId],
    queryFn: () =>
      apiFetch<{ total: number; items: DiscoveredUrlRow[] }>(`/api/v1/crawl-jobs/${jobId}/pages?limit=50`),
    enabled: Number.isFinite(jobId),
  })

  const { data: events } = useQuery({
    queryKey: ['crawl-job-events', jobId],
    queryFn: () => apiFetch<CrawlJobEvent[]>(`/api/v1/crawl-jobs/${jobId}/events?limit=200`),
    enabled: Number.isFinite(jobId),
  })

  useEffect(() => {
    if (!Number.isFinite(jobId) || !apiKey) {
      return
    }
    const conn = new HubConnectionBuilder()
      .withUrl(`${apiBase}/hubs/crawl-job`, {
        headers: { 'X-Api-Key': apiKey },
      })
      .configureLogging(LogLevel.Warning)
      .withAutomaticReconnect()
      .build()

    let cancelled = false
    void (async () => {
      try {
        await conn.start()
        await conn.invoke('JoinJob', jobId)
        conn.on('CrawlEvent', (payload: unknown) => {
          if (!cancelled) {
            setLive((prev) => [payload, ...prev].slice(0, 200))
          }
        })
      } catch {
        /* hub optional */
      }
    })()

    return () => {
      cancelled = true
      void conn.stop()
    }
  }, [jobId])

  if (!Number.isFinite(jobId)) {
    return <p className="text-sm text-red-600">Invalid job id.</p>
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Job #{jobId}</h1>
        {job && (
          <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-[var(--color-muted-foreground)]">
            <Badge variant={job.status === 'Running' ? 'success' : 'default'}>{job.status}</Badge>
            <span>JS render: {job.useJsRendering ? 'on' : 'off'}</span>
            <span>Robots: {job.respectRobots ? 'respect' : 'ignore'}</span>
          </div>
        )}
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Live events</CardTitle>
            <CardDescription>SignalR stream (requires API key + Redis). Fallback: persisted events below.</CardDescription>
          </CardHeader>
          <CardContent>
            <pre className="max-h-80 overflow-auto rounded-md bg-[var(--color-muted)] p-3 text-xs">
              {JSON.stringify(live.length ? live : events ?? [], null, 2)}
            </pre>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Pages ({pages?.total ?? 0})</CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="space-y-2 text-sm">
              {pages?.items.map((p) => (
                <li key={p.id} className="truncate border-b border-[var(--color-border)]/50 pb-2">
                  <span className="text-[var(--color-muted-foreground)]">d{p.depth}</span> {p.normalizedUrl ?? p.url}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
