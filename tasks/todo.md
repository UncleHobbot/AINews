# AINews — Task Tracker

## Phase 1: Foundation ✅
- [x] Solution scaffold: AINews.sln + AINews.Api + AINews.Frontend
- [x] AppDbContext + all entities + EF Core initial migration
- [x] SettingsService with DataProtection encryption
- [x] Google OAuth + email whitelist auth + Program.cs wiring
- [x] React frontend shell: login page, app shell, routing

## Phase 2: Source Integrations ✅
- [x] RedditService (app-only OAuth, subreddit fetch, rate-limit awareness)
- [x] XService (bearer token, batched keyword search, rate-limit gate)
- [x] Topics + Sources CRUD controllers
- [x] Settings controller (API key management, Reddit OAuth flow)

## Phase 3: AI Pipeline ✅
- [x] ContentFetcherService (GitHub README, YouTube oEmbed, article HTML scraping)
- [x] LinkExtractorService (URL detection + classification)
- [x] AiService (Z.ai primary + OpenAI fallback, JSON structured output)
- [x] ScanOrchestrator (full pipeline: fetch → extract → AI → persist)
- [x] ScanBackgroundService (Channel-based, single-scan-at-a-time)
- [x] ScanController + NewsController

## Phase 4: Real-time UI ✅
- [x] ScanProgressHub (SignalR)
- [x] useSignalR hook
- [x] FeedPage with scan progress + news timeline
- [x] SourcesPage CRUD
- [x] SettingsPage (API keys + Reddit OAuth + Topics)

## Phase 5: Docker ✅
- [x] Multi-stage Dockerfile (node:20-alpine + dotnet/sdk:10.0 + dotnet/aspnet:10.0)
- [x] docker-compose.yml for local dev
- [x] .dockerignore

## Still TODO (before first use)
- [ ] Set Google OAuth ClientId/ClientSecret in docker-compose.yml or Settings UI
- [ ] Set AllowedEmails in docker-compose.yml to your email
- [ ] Register Reddit API app at https://www.reddit.com/prefs/apps
- [ ] Apply for X Developer API access at developer.twitter.com
- [ ] Set up Z.ai or OpenAI API key in Settings UI after first login
- [ ] Build multi-arch image: `docker buildx build --platform linux/amd64,linux/arm64 -t your-registry/ainews:latest --push .`
