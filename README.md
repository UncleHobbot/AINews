# AINews

A personal AI-powered news aggregator that monitors Reddit subreddits and X.com keyword searches, extracts insights and links using AI, and presents findings in a clean timeline UI.

## Features

- **Reddit & X.com monitoring** — scan subreddits and keyword searches for new posts since the last run
- **AI extraction** — summarizes posts, identifies links (GitHub repos, YouTube videos, articles, docs), and summarizes linked content
- **Feedback learning** — thumb up/down on articles; disliked items are hidden and feedback history is used to teach the AI your preferences
- **Z.ai + OpenAI** — uses Z.ai as the primary AI provider with OpenAI as fallback
- **Real-time progress** — "Scan Now" button with live SignalR progress updates
- **Topic & source management** — organize sources into topics, enable/disable individually
- **Google OAuth** — access restricted to whitelisted Google accounts

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS |
| Database | SQLite via Entity Framework Core 10 |
| Real-time | SignalR |
| Auth | Google OAuth + cookie session |
| Deployment | Docker (single container, multi-arch) |

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- Google OAuth credentials ([console.cloud.google.com](https://console.cloud.google.com))
- Reddit API app ([reddit.com/prefs/apps](https://www.reddit.com/prefs/apps)) — script type, requires API access approval
- X.com account (browser session cookies — no developer account needed)
- Z.ai API key and/or OpenAI API key

### Running locally

**1. Configure Google OAuth** in `src/AINews.Api/appsettings.Development.json` (create if absent, gitignored):
```json
{
  "AllowedEmails": ["your@gmail.com"],
  "Google": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  },
  "FrontendBaseUrl": "http://localhost:5173"
}
```
Add `http://localhost:5230/api/auth/callback/google` as an authorized redirect URI in Google Console.

**2. Start the API (Terminal 1):**
```bash
cd src/AINews.Api
dotnet run
# API starts at http://localhost:5230
```

**3. Start the frontend (Terminal 2):**
```bash
cd src/AINews.Frontend
npm install
npm run dev
# App at http://localhost:5173
```

Or use the helper scripts at the project root: `run-api.bat` / `run-fe.bat` (Windows).

**4. After first login**, go to **Settings** to enter:
- Reddit Client ID & Secret → then click **Connect Reddit Account**
- X Auth Token (`auth_token` cookie) and X CSRF Token (`ct0` cookie) — see below
- Z.ai API Key & Base URL (`https://api.z.ai/v1`) and/or OpenAI API Key

**5. Add topics and sources**, then click **Scan Now**.

### X.com Setup (no developer account needed)

Instead of the official API, AINews uses X.com's internal web API via your browser session:

1. Log into [x.com](https://x.com) in your browser
2. Open DevTools → **Application** → **Cookies** → `https://x.com`
3. Copy the **`auth_token`** value → paste into Settings → "X Auth Token"
4. Copy the **`ct0`** value → paste into Settings → "X CSRF Token"

These tokens are long-lived. When they expire, the scan will skip X sources and log a warning — just re-extract them.

### Docker (Synology NAS or any Docker host)

```bash
# Build (multi-arch for ARM64 Synology NAS)
docker buildx build --platform linux/amd64,linux/arm64 \
  -t your-registry/ainews:latest --push .

# Or local single-arch
docker compose up --build
```

Copy `.env.example` to `.env` and fill in your values before running:
```
GOOGLE_CLIENT_ID=your-google-client-id
GOOGLE_CLIENT_SECRET=your-google-client-secret
ALLOWED_EMAIL=your@gmail.com
```

Set the OAuth callback URL in Google Console to `http://<nas-ip>:8080/api/auth/callback/google`.

#### Volume mounts (Synology)

| Host path | Container path | Purpose |
|---|---|---|
| `/volume1/docker/ainews/data` | `/app/data` | SQLite database |
| `/volume1/docker/ainews/keys` | `/app/keys` | DataProtection key ring |
| `/volume1/docker/ainews/logs` | `/app/logs` | Application logs |

> **Important:** Back up `/app/keys`. Losing this volume makes all stored API keys unreadable.

## Configuration Reference

All API keys are stored encrypted in the database after first login via the **Settings** page. The only values needed at startup are Google OAuth credentials and the allowed email list.

| Setting | Where | Description |
|---|---|---|
| `Google:ClientId` / `Google:ClientSecret` | Config / env | Required at startup for OAuth |
| `AllowedEmails` | Config / env | List of emails allowed to log in |
| `FrontendBaseUrl` | Config | Dev only — redirect target after login (e.g. `http://localhost:5173`) |
| `DataProtection:KeyPath` | Config / env | Override key ring directory (default: `<root>/keys`) |
| `ConnectionStrings:DefaultConnection` | Config / env | SQLite path (default: `<root>/data/ainews.db`) |

All other keys (Reddit, X, Z.ai, OpenAI) are configured via the Settings UI after login.
