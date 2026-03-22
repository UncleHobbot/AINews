# AINews

A personal AI-powered news aggregator that monitors Reddit subreddits and X.com keyword searches, extracts insights and links using AI, and presents findings in a clean timeline UI.

## Features

- **Reddit & X.com monitoring** — scan subreddits and keyword searches for new posts since the last run
- **AI extraction** — summarizes posts, identifies links (GitHub repos, YouTube videos, articles, docs), and summarizes linked content
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
- Reddit API app ([reddit.com/prefs/apps](https://www.reddit.com/prefs/apps)) — script type, app-only auth
- X Developer account with Bearer Token ([developer.twitter.com](https://developer.twitter.com)) — Free tier works (1 search/15 min)
- Z.ai API key and/or OpenAI API key

### Running locally

**1. Start the API:**
```bash
cd src/AINews.Api
dotnet run
# API starts at http://localhost:5000
```

**2. Start the frontend (separate terminal):**
```bash
cd src/AINews.Frontend
npm install
npm run dev
# App available at http://localhost:5173
```

**3. Configure your Google OAuth credentials** in `src/AINews.Api/appsettings.json`:
```json
{
  "AllowedEmails": ["your@gmail.com"],
  "Google": {
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  }
}
```
Set the OAuth callback URL in Google Console to `http://localhost:5000/api/auth/callback/google`.

**4. After first login**, go to **Settings** to enter:
- Reddit Client ID & Secret → then click **Connect Reddit Account**
- X Bearer Token
- Z.ai API Key & Base URL (and/or OpenAI API Key)

**5. Add topics and sources**, then click **Scan Now**.

### Docker (Synology NAS or any Docker host)

```bash
# Build (multi-arch for ARM64 Synology NAS)
docker buildx build --platform linux/amd64,linux/arm64 \
  -t your-registry/ainews:latest --push .

# Or local single-arch
docker compose up --build
```

Edit `docker-compose.yml` to set your email and Google credentials before running:
```yaml
environment:
  - AllowedEmails__0=your@gmail.com
  - Google__ClientId=your-google-client-id
  - Google__ClientSecret=your-google-client-secret
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
| `DataProtection:KeyPath` | Config / env | Override key ring directory (default: `<root>/keys`) |
| `ConnectionStrings:DefaultConnection` | Config / env | SQLite path (default: `<root>/data/ainews.db`) |

All other keys (Reddit, X, Z.ai, OpenAI) are configured via the Settings UI after login.

## X API Rate Limits

The X (Twitter) Free tier allows approximately **1 search request per 15 minutes**. The app automatically:
- Batches all keyword queries into a single OR compound request
- Tracks the last search time and enforces the cooldown
- Displays the remaining cooldown in the scan status

Consider upgrading to the Basic tier ($100/month) for 10M tweets/month if you need more frequent scans.
