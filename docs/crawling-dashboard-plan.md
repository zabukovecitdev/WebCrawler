# SamoBot crawling dashboard — agent reference

This document is the canonical spec for the crawl dashboard, API, real-time telemetry, compliance APIs, crawler controls, and optional Chrome-based JS rendering. It aligns with the implementation in the repository.

## Executive summary

- **Backend**: `SamoBot.Api` (ASP.NET Core) exposes REST under `/api/v1`, Swagger at `/swagger`, and SignalR hub at `/hubs/crawl-job`. Authentication is a development-friendly **API key** (`X-Api-Key` header), configured as `DashboardApi:Key` in `appsettings` (see `src/SamoBot.Settings/appsettings.json`).
- **Workers**: Existing services (`SamoBot`, `SamoBot.Crawler`, `SamoBot.Parser`) emit crawl telemetry via `ICrawlTelemetryService` (PostgreSQL `CrawlJobEvents` + optional Redis Pub/Sub channel `CrawlTelemetry:RedisChannel`, default `samo:crawl:telemetry`). The API subscribes to Redis and forwards payloads to SignalR groups `crawlJob:{id}`.
- **Frontend**: `frontend/` — Vite + React + TypeScript, TanStack Query, React Router, Tailwind + Radix-style UI primitives (`src/components/ui/*`). Set `VITE_API_BASE` and `VITE_API_KEY` (see `frontend/.env.example`).
- **Database**: FluentMigrator migration `013_CreateCrawlJobsAndEvents` adds `CrawlJobs`, `CrawlJobEvents`, and extends `DiscoveredUrls` with `CrawlJobId`, `Depth`, `UseJsRendering`, `RespectRobots`.

## REST API (v1)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/v1/me` | Current principal (API key user) |
| GET | `/api/v1/crawl-jobs` | List recent jobs |
| POST | `/api/v1/crawl-jobs` | Create job (`seedUrls`, `maxDepth`, `maxUrls`, `useJsRendering`, `respectRobots`) |
| GET | `/api/v1/crawl-jobs/{id}` | Job detail |
| POST | `/api/v1/crawl-jobs/{id}/actions` | Body `{ "action": "start" \| "pause" \| "cancel" }` |
| GET | `/api/v1/crawl-jobs/{id}/pages` | Paginated discovered URLs for job |
| GET | `/api/v1/crawl-jobs/{id}/events` | Poll telemetry (`after` cursor by event id) |
| GET | `/api/v1/hosts/{host}/robots` | Cached robots.txt model |
| GET | `/api/v1/hosts/{host}/sitemaps` | Stub — empty list until sitemap storage exists |
| POST | `/api/v1/hosts/explain` | Body `{ "url" }` — allow/deny, crawl-delay, robots payload |

## SignalR

- **Hub**: `CrawlJobHub` — methods `JoinJob(jobId)`, `LeaveJob(jobId)`.
- **Client event**: `CrawlEvent` — JSON payload includes `crawlJobId`, `eventId`, `eventType`, and `payload` (from Redis envelope).
- **Headers**: Send `X-Api-Key` on the SignalR connection (see `frontend/src/pages/JobDetailPage.tsx`).

## Telemetry event types (emitted by workers)

- `JobCreated`, `JobStarted`, `JobPaused`, `JobCancelled` — from `CrawlJobService` / lifecycle.
- `FetchStarted`, `FetchCompleted` — from `ContentProcessingPipeline`.
- `BlockedByRobots` — from `RobotsTxtPolicy` when URL disallowed.
- `PolitenessDeferred` — from `PolitenessPolicy` when URL deferred due to rate limiting.

## Crawler behavior changes

- **Depth**: `DiscoveredUrls.Depth` — seeds at `0`; parser publishes `UrlDiscoveryMessage` with `Depth = parent.Depth + 1`. `MessageConsumerWorker` enforces `MaxDepth` / `MaxUrls` when `CrawlJobId` is set.
- **Respect robots**: `ScheduledUrl.RespectRobots` — `RobotsTxtPolicy` skips robots check when `false`.
- **JS rendering**: `ScheduledUrl.UseJsRendering` + `ChromeRendering` options — `ContentProcessingPipeline` uses `IJsRenderService` (Playwright `ConnectOverCDPAsync`) when enabled; otherwise HTTP fetch.

## Docker / Chrome CDP

- See `docker-compose.yml` service `chrome-cdp` (Alpine Chrome with remote debugging on **9222**).
- Configure `ChromeRendering:Enabled` and `ChromeRendering:CdpEndpoint` (e.g. `http://localhost:9222` or `http://chrome-cdp:9222` from another container).
- Resource limits and pooling should be enforced in production (not fully automated here).

## Project map

| Area | Location |
|------|----------|
| API host | `src/SamoBot.Api/` |
| Hub + Redis subscriber | `src/SamoBot.Api/Hubs/`, `src/SamoBot.Api/Hosting/` |
| Crawl jobs / telemetry | `src/SamoBot.Infrastructure/Services/`, `Data/CrawlJob*.cs` |
| JS render | `src/SamoBot.Infrastructure/Storage/Services/JsRenderService.cs` |
| Migrations | `src/SamoBot.Migrations/Migrations/013_CreateCrawlJobsAndEvents.cs` |
| Dashboard UI | `frontend/src/` |

## Production checklist (non-exhaustive)

- Replace API key auth with OIDC/JWT for real users.
- Enable Redis for live SignalR forwarding; without Redis, use REST polling on `/events`.
- Run `playwright install chromium` in CI/deploy images if using CDP against remote Chrome.
- Lock down CORS `Cors:Origins` to production dashboard origins.
