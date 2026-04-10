import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { apiFetch } from '@/lib/api'
import type { CrawlJob } from '@/types/crawl'
import { useState } from 'react'

export function JobsPage() {
  const qc = useQueryClient()
  const { data: jobs, isLoading, error } = useQuery({
    queryKey: ['crawl-jobs'],
    queryFn: () => apiFetch<CrawlJob[]>('/api/v1/crawl-jobs'),
  })

  const [seeds, setSeeds] = useState('https://example.com')
  const [maxDepth, setMaxDepth] = useState<string>('')
  const [maxUrls, setMaxUrls] = useState<string>('')
  const [useJs, setUseJs] = useState(false)
  const [respectRobots, setRespectRobots] = useState(true)

  const create = useMutation({
    mutationFn: () =>
      apiFetch<CrawlJob>('/api/v1/crawl-jobs', {
        method: 'POST',
        body: JSON.stringify({
          seedUrls: seeds.split(/\s+/).filter(Boolean),
          maxDepth: maxDepth ? Number(maxDepth) : null,
          maxUrls: maxUrls ? Number(maxUrls) : null,
          useJsRendering: useJs,
          respectRobots,
        }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['crawl-jobs'] }),
  })

  const action = useMutation({
    mutationFn: ({ id, act }: { id: number; act: string }) =>
      apiFetch(`/api/v1/crawl-jobs/${id}/actions`, {
        method: 'POST',
        body: JSON.stringify({ action: act }),
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['crawl-jobs'] }),
  })

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Crawl jobs</h1>
        <p className="text-[var(--color-muted-foreground)]">Create jobs, start crawling, and open a job for live telemetry.</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>New job</CardTitle>
          <CardDescription>Seed URLs (whitespace-separated). Optional max depth and max URLs.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="seeds">Seed URLs</Label>
            <Input id="seeds" value={seeds} onChange={(e) => setSeeds(e.target.value)} placeholder="https://example.com" />
          </div>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="maxDepth">Max depth</Label>
              <Input id="maxDepth" value={maxDepth} onChange={(e) => setMaxDepth(e.target.value)} placeholder="e.g. 3" />
            </div>
            <div className="space-y-2">
              <Label htmlFor="maxUrls">Max URLs</Label>
              <Input id="maxUrls" value={maxUrls} onChange={(e) => setMaxUrls(e.target.value)} placeholder="e.g. 1000" />
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-4">
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={useJs} onChange={(e) => setUseJs(e.target.checked)} />
              JavaScript rendering (requires Chrome CDP)
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={respectRobots} onChange={(e) => setRespectRobots(e.target.checked)} />
              Respect robots.txt
            </label>
          </div>
          <Button disabled={create.isPending} onClick={() => create.mutate()}>
            {create.isPending ? 'Creating…' : 'Create job'}
          </Button>
          {create.error && <p className="text-sm text-red-600">{(create.error as Error).message}</p>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recent jobs</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading && <p className="text-sm text-[var(--color-muted-foreground)]">Loading…</p>}
          {error && <p className="text-sm text-red-600">{(error as Error).message}</p>}
          {jobs && jobs.length === 0 && <p className="text-sm text-[var(--color-muted-foreground)]">No jobs yet.</p>}
          {jobs && jobs.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-[var(--color-border)]">
                    <th className="pb-2 pr-4 font-medium">ID</th>
                    <th className="pb-2 pr-4 font-medium">Status</th>
                    <th className="pb-2 pr-4 font-medium">Created</th>
                    <th className="pb-2 font-medium">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {jobs.map((j) => (
                    <tr key={j.id} className="border-b border-[var(--color-border)]/60">
                      <td className="py-2 pr-4">
                        <Link className="text-blue-600 hover:underline" to={`/jobs/${j.id}`}>
                          {j.id}
                        </Link>
                      </td>
                      <td className="py-2 pr-4">
                        <Badge variant={j.status === 'Running' ? 'success' : 'default'}>{j.status}</Badge>
                      </td>
                      <td className="py-2 pr-4 text-[var(--color-muted-foreground)]">
                        {new Date(j.createdAt).toLocaleString()}
                      </td>
                      <td className="py-2">
                        <div className="flex flex-wrap gap-2">
                          <Button size="sm" variant="outline" onClick={() => action.mutate({ id: j.id, act: 'start' })}>
                            Start
                          </Button>
                          <Button size="sm" variant="outline" onClick={() => action.mutate({ id: j.id, act: 'pause' })}>
                            Pause
                          </Button>
                          <Button size="sm" variant="destructive" onClick={() => action.mutate({ id: j.id, act: 'cancel' })}>
                            Cancel
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
