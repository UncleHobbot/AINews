# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

### Backend (ASP.NET Core 10)
```bash
cd src/AINews.Api
dotnet build
dotnet run                          # starts on http://localhost:5000
dotnet ef migrations add <Name>     # add EF Core migration
dotnet ef migrations remove         # undo last migration
```

### Frontend (React + Vite)
```bash
cd src/AINews.Frontend
npm install
npm run dev                         # starts on http://localhost:5173 (proxied to :5000)
npm run build                       # production build → dist/
```

### Docker
```bash
# Local dev
docker compose up --build

# Multi-arch for Synology NAS
docker buildx build --platform linux/amd64,linux/arm64 -t your-registry/ainews:latest --push .
```

## Architecture

Single Docker container: ASP.NET Core 10 API serves both the REST/SignalR backend **and** the React static files (`wwwroot/` from Vite's `dist/`). SQLite database auto-migrates on startup.

### Request flow
1. React (port 5173 in dev, same origin in production) → `axios` with `withCredentials` → `/api/*`
2. Auth: Google OAuth cookie session. `AuthController` issues a cookie after validating the Google callback and checking `AppUsers` table + `AllowedEmails` config whitelist.
3. All API controllers require `[Authorize]`.

### Scan pipeline (`ScanOrchestrator`)
`POST /api/scan/trigger` → `ScanBackgroundService` (singleton, `Channel<int>`, `SemaphoreSlim(1,1)`) → `ScanOrchestrator` (scoped):
1. Fetch Reddit posts via `RedditService` (app-only OAuth, `client_credentials`)
2. Fetch X tweets via `XService` (bearer token; **all keyword queries batched into one OR compound request** to respect the free-tier 1-req/15-min limit; enforced by storing `X:LastSearchAt` in `AppSettings`)
3. For each `RawPost` (upserted with unique index on `(SourceId, ExternalId)`):
   - Extract URLs → classify as GitHub/YouTube/Docs/Article
   - Fetch link content in parallel (max 3 concurrent, 5s timeout)
   - `AiService.AnalyzePost` → `{ summary, insights[], relevance, shouldInclude }` — Z.ai first, OpenAI fallback
   - If `shouldInclude`: create `NewsItem` + `ExtractedLink` rows with AI-generated summaries
4. Emit `ScanProgress` events via `ScanProgressHub` (SignalR) to the `"scan-watchers"` group

### Secrets & settings
All API keys/tokens are stored **encrypted** in the `AppSettings` SQLite table using ASP.NET Core `IDataProtectionProvider`. The DataProtection key ring lives in `/app/keys` (Docker volume — **must be backed up**; losing it makes all stored settings unreadable). `SettingsService` is the only entry point for reading/writing secrets.

### Source config shapes (stored as JSON in `Sources.Config`)
- Reddit: `{ "subreddit": "ClaudeAI", "limit": 100 }`
- X: `{ "query": "Claude Code -is:retweet lang:en", "maxResults": 100 }`

### Frontend data flow
- `useAuth` hook (runs once) → `GET /api/auth/me` → populates `useAuthStore` (Zustand)
- `App.tsx` wraps protected routes in `<AuthGate>` which redirects to `/login` on 401
- TanStack Query handles all API state; keys follow `['topics']`, `['sources']`, `['news', topicId]` pattern
- `useSignalR` hook connects to `/hubs/scan` and exposes `ScanProgress` events consumed by `FeedPage`

## Key Files

| File | Purpose |
|---|---|
| `src/AINews.Api/Services/ScanOrchestrator.cs` | Core scan pipeline — most complex service |
| `src/AINews.Api/Services/AiService.cs` | Z.ai/OpenAI integration; prompt contracts define the data shape for the whole pipeline |
| `src/AINews.Api/Services/SettingsService.cs` | All credential access; must be used exclusively for secrets |
| `src/AINews.Api/Data/AppDbContext.cs` | EF Core schema + indexes (unique constraint on `RawPosts(SourceId, ExternalId)`) |
| `src/AINews.Api/Program.cs` | DI wiring, auth config, middleware pipeline |
| `src/AINews.Frontend/src/pages/FeedPage.tsx` | Main UI — news timeline + scan trigger + SignalR progress |

## Important Constraints

- **X API rate limit**: Free tier allows ~1 search per 15 minutes. `XService` gates calls using `X:LastSearchAt` stored in `AppSettings`. Never add a second call path that bypasses this check.
- **EF migrations**: Run `dotnet ef migrations add <Name>` from `src/AINews.Api/`. The `AppDbContextFactory` provides the design-time context (uses a throwaway `ainews_design.db`).
- **ScanBackgroundService is a singleton** but `ScanOrchestrator` and all EF-dependent services are scoped — always resolve them via `IServiceScopeFactory` inside the background service.
- **DataProtection key path** must be configured via `DataProtection:KeyPath` config or it defaults to `<contentRoot>/keys`. In Docker this maps to `/app/keys` volume.
