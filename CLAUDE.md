# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

### Backend (ASP.NET Core 10)
```bash
cd src/AINews.Api
dotnet build
dotnet run                          # starts on http://localhost:5230
dotnet ef migrations add <Name>     # add EF Core migration
dotnet ef migrations remove         # undo last migration
```

### Frontend (React + Vite)
```bash
cd src/AINews.Frontend
npm install
npm run dev                         # starts on http://localhost:5173 (proxied to :5230)
npm run build                       # production build → dist/
```

### Quick start (Windows)
```
run-api.bat    # opens a terminal running the API
run-fe.bat     # opens a terminal running the frontend
```

### Docker
```bash
# Local dev
docker compose up --build

# Multi-arch for Synology NAS
docker buildx build --platform linux/amd64,linux/arm64 -t your-registry/ainews:latest --push .
```

## Architecture

Single Docker container: ASP.NET Core 10 API serves both the REST/SignalR backend **and** the React static files (`wwwroot/` from Vite's `dist/`). SQLite database auto-migrates on startup. No authentication — designed for local network use only.

### Request flow
1. React (port 5173 in dev, same origin in production) → `axios` → `/api/*`
2. No auth — all API endpoints are open. App is intended for private local network only.

### Scan pipeline (`ScanOrchestrator`)
`POST /api/scan/trigger` → `ScanBackgroundService` (singleton, `Channel<int>`, `SemaphoreSlim(1,1)`) → `ScanOrchestrator` (scoped):
1. Fetch Reddit posts via `RedditService` (public JSON endpoint — no API key needed)
2. Fetch X tweets via `XService` (internal X.com web API — see below)
3. For each `RawPost` (upserted with unique index on `(SourceId, ExternalId)`):
   - Extract URLs → classify as GitHub/YouTube/Docs/Article
   - Fetch link content in parallel (max 3 concurrent, 5s timeout)
   - `AiService.AnalyzePost` → `{ summary, insights[], relevance, shouldInclude }` — Z.ai first, OpenAI fallback
   - If `shouldInclude`: create `NewsItem` + `ExtractedLink` rows with AI-generated summaries
4. Emit `ScanProgress` events via `ScanProgressHub` (SignalR) to the `"scan-watchers"` group

### Reddit (public JSON)
`RedditService` fetches `https://www.reddit.com/r/{subreddit}/new.json` — no API key or OAuth needed. Works for all public subreddits. Only requires a `User-Agent` header.

### X.com scraping (internal API)
`XService` calls X.com's internal `search/adaptive.json` endpoint using the user's browser session cookies:
- **`X:AuthToken`** — the `auth_token` cookie from x.com (extract via DevTools → Application → Cookies)
- **`X:CsrfToken`** — the `ct0` cookie from x.com
- A fixed public bearer token (`AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I4xNbq4=`) used by X.com's own web app
- Retweets are filtered out during response parsing
- When the session expires, the scan logs a warning and skips X sources gracefully

### Secrets & settings
API keys/tokens (X session cookies, AI keys) are stored **encrypted** in the `AppSettings` SQLite table using ASP.NET Core `IDataProtectionProvider`. The DataProtection key ring lives in `/app/keys` (Docker volume — **must be backed up**; losing it makes all stored settings unreadable). `SettingsService` is the only entry point for reading/writing secrets.

### Source config shapes (stored as JSON in `Sources.Config`)
- Reddit: `{ "subreddit": "ClaudeAI", "limit": 100 }`
- X: `{ "query": "Claude Code -is:retweet lang:en", "maxResults": 100 }`

### Frontend data flow
- No auth layer — app opens directly to the feed
- TanStack Query handles all API state; keys follow `['topics']`, `['sources']`, `['news', topicId]` pattern
- `useSignalR` hook connects to `/hubs/scan` and exposes `ScanProgress` events consumed by `FeedPage`

## Key Files

| File | Purpose |
|---|---|
| `src/AINews.Api/Services/ScanOrchestrator.cs` | Core scan pipeline — most complex service |
| `src/AINews.Api/Services/AiService.cs` | Z.ai/OpenAI integration; prompt contracts define the data shape for the whole pipeline |
| `src/AINews.Api/Services/XService.cs` | X.com internal API scraping via session cookies |
| `src/AINews.Api/Services/RedditService.cs` | Reddit public JSON fetcher — no API key needed |
| `src/AINews.Api/Services/SettingsService.cs` | All credential access; must be used exclusively for secrets |
| `src/AINews.Api/Data/AppDbContext.cs` | EF Core schema + indexes (unique constraint on `RawPosts(SourceId, ExternalId)`) |
| `src/AINews.Api/Program.cs` | DI wiring, middleware pipeline |
| `src/AINews.Frontend/src/pages/FeedPage.tsx` | Main UI — news timeline + scan trigger + SignalR progress |

## Important Constraints

- **No authentication**: App has no auth and is designed for local/private network only. Do not expose to the public internet.
- **X.com session**: `auth_token` and `ct0` cookies must be re-extracted from the browser when the session expires.
- **EF migrations**: Run `dotnet ef migrations add <Name>` from `src/AINews.Api/`. The `AppDbContextFactory` provides the design-time context (uses a throwaway `ainews_design.db`).
- **ScanBackgroundService is a singleton** but `ScanOrchestrator` and all EF-dependent services are scoped — always resolve them via `IServiceScopeFactory` inside the background service.
- **DataProtection key path** must be configured via `DataProtection:KeyPath` config or it defaults to `<contentRoot>/keys`. In Docker this maps to `/app/keys` volume.
