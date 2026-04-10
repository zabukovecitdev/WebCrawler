export type CrawlJob = {
  id: number
  ownerUserId?: string | null
  status: string
  seedUrls: string
  maxDepth?: number | null
  maxUrls?: number | null
  useJsRendering: boolean
  respectRobots: boolean
  createdAt: string
  updatedAt: string
  startedAt?: string | null
  completedAt?: string | null
}

export type CrawlJobEvent = {
  id: number
  crawlJobId: number
  eventType: string
  payload: string
  createdAt: string
}

export type DiscoveredUrlRow = {
  id: number
  host: string
  url: string
  normalizedUrl?: string | null
  status: string
  crawlJobId?: number | null
  depth: number
  useJsRendering: boolean
  respectRobots: boolean
}
