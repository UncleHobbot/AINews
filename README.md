# AINews

A personal AI-powered news aggregator that monitors Reddit subreddits and X.com keyword searches, extracts insights and links using AI, and presents findings in a clean timeline UI. Designed for local network use on a home NAS.

## Features

- **Reddit & X.com monitoring** — scan subreddits and keyword searches for new posts since the last run
- **AI extraction** — summarizes posts, identifies links (GitHub repos, YouTube videos, articles, docs), and summarizes linked content
- **Feedback learning** — thumb up/down on articles; disliked items are hidden and feedback history is used to teach the AI your preferences
- **Z.ai + OpenAI** — uses Z.ai as the primary AI provider with OpenAI as fallback
- **Real-time progress** — "Scan Now" button with live SignalR progress updates
- **Topic & source management** — organize sources into topics, enable/disable individually
- **No login required** — no authentication; intended for private local network only

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS |
| Database | SQLite via Entity Framework Core 10 |
| Real-time | SignalR |
| Deployment | Docker (single container, multi-arch) |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- X.com account (browser session cookies — no developer account needed)
- Z.ai API key and/or OpenAI API key

No Reddit API key needed — the app reads public subreddits directly via their JSON feed.

### Running locally

**1. Start the API (Terminal 1):**
```bash
cd src/AINews.Api
dotnet run
# API starts at http://localhost:5230
```

**2. Start the frontend (Terminal 2):**
```bash
cd src/AINews.Frontend
npm install
npm run dev
# App at http://localhost:5173
```

Or use the helper scripts at the project root: `run-api.bat` / `run-fe.bat` (Windows).

**3. Go to Settings** to enter:
- X Auth Token (`auth_token` cookie) and X CSRF Token (`ct0` cookie) — see below
- Z.ai API Key & Base URL (`https://api.z.ai/v1`) and/or OpenAI API Key

**4. Add topics and sources**, then click **Scan Now**.

### X.com Setup (no developer account needed)

AINews uses X.com's internal web API via your browser session:

1. Log into [x.com](https://x.com) in your browser
2. Open DevTools → **Application** → **Cookies** → `https://x.com`
3. Copy the **`auth_token`** value → paste into Settings → "X Auth Token"
4. Copy the **`ct0`** value → paste into Settings → "X CSRF Token"

These tokens are long-lived. When they expire, the scan will skip X sources and log a warning — just re-extract them.

### Reddit Setup

No setup needed. Reddit sources use the public JSON endpoint (`reddit.com/r/{sub}/new.json`) — works for all public subreddits without any API key or account.

### Docker (Synology NAS or any Docker host)

```bash
# Build (multi-arch for ARM64 Synology NAS)
docker buildx build --platform linux/amd64,linux/arm64 \
  -t your-registry/ainews:latest --push .

# Or local single-arch
docker compose up --build
```

#### Volume mounts (Synology)

| Host path | Container path | Purpose |
|---|---|---|
| `/volume1/docker/ainews/data` | `/app/data` | SQLite database |
| `/volume1/docker/ainews/keys` | `/app/keys` | DataProtection key ring |
| `/volume1/docker/ainews/logs` | `/app/logs` | Application logs |

> **Important:** Back up `/app/keys`. Losing this volume makes all stored API keys unreadable.

## Configuration Reference

All API keys are stored encrypted in the database via the **Settings** page. No startup configuration is required.

| Setting | Where | Description |
|---|---|---|
| `DataProtection:KeyPath` | Config / env | Override key ring directory (default: `<root>/keys`) |
| `ConnectionStrings:DefaultConnection` | Config / env | SQLite path (default: `<root>/data/ainews.db`) |

All other keys (X session, Z.ai, OpenAI) are configured via the Settings UI.
