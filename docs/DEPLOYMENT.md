# Deployment Guide

This document describes how to deploy the Sales Portal API to MonsterASP using the GitHub Actions workflow in `.github/workflows/deploy.yml`.

## 1. GitHub Setup

### Create a `production` environment

1. In your GitHub repository, go to **Settings** → **Environments** → **New environment**.
2. Name it `production`.
3. (Optional) Add required reviewers to gate manual approval before a deployment runs.

### Add repository/environment **secrets**

| Secret | Description | Example |
|--------|-------------|---------|
| `MONSTER_WEBDEPLOY_SERVER` | The Web Deploy server host. Found in MonsterASP control panel under Websites > your site > Deploy. | `site12345.siteasp.net` |
| `MONSTER_SITE_NAME` | The IIS site name / site ID. Shown on the Visual Studio access card. | `site12345` |
| `MONSTER_WEBDEPLOY_USER` | Web Deploy username (usually the same as the site ID). | `site12345` |
| `MONSTER_WEBDEPLOY_PASSWORD` | Web Deploy password. Regenerated in the control panel if needed; update this secret when you do. | *(generated)* |

### Add the **variable** `APP_URL`

| Variable | Description | Example |
|----------|-------------|---------|
| `APP_URL` | The public API URL used for the `/health` smoke test after deployment. | `https://api.yourdomain.com` |

> **Note:** Web Deploy must be enabled in the MonsterASP control panel under **Websites > your site > Deploy**. If you regenerate the Web Deploy password there, update `MONSTER_WEBDEPLOY_PASSWORD` in GitHub secrets immediately.

---

## 2. MonsterASP Environment Variables

Configure these in the MonsterASP control panel under **Scripting > Environment variables**, then restart the app. Use `__` (double underscore) for nested keys.

| Variable | Description |
|----------|-------------|
| `DB_CONNECTION_STRING` (or `ConnectionStrings__Default`) | PostgreSQL connection string. **Confirm your hosting plan includes PostgreSQL** (the app uses Npgsql). |
| `PORTAL_NOTIFICATIONS_CONNECTION_STRING` (or `ConnectionStrings__PortalNotifications`) | Optional separate connection for live updates; falls back to `DB_CONNECTION_STRING`. Set it only on hosts that offer a direct/session-mode connection. MonsterASP routes every connection through PgBouncer transaction mode, where `LISTEN` is unsupported, so the API also polls table counters as a fallback. |
| `JWT_SECRET` | Signing key, **at least 32 characters**. Startup fails if missing or too short. |
| `JWT_ISSUER` | JWT issuer claim (e.g. `SalesPortalAPI`). |
| `JWT_AUDIENCE` | JWT audience claim (e.g. `SalesPortalClient`). |
| `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … | Frontend origins (e.g. `https://app.yourdomain.com`). The `Cors:AllowedOrigins` array is empty by default; without these every browser request from the frontend is blocked. |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production`. |
| `Database__AutoMigrate` | Set to `true` for the **first deployment** and after any release that adds migrations. The app applies pending migrations at startup. **This is the only migration mechanism in the pipeline.** Taking a database backup before such a release is strongly recommended. |
| `BOOTSTRAP_ADMIN_USERNAME` | (Optional, first-login only) Username for the initial admin account. |
| `BOOTSTRAP_ADMIN_PASSWORD` | (Optional, first-login only) Password for the initial admin account. **Must be at least 12 characters.** |
| `BOOTSTRAP_ADMIN_FULL_NAME` | (Optional, first-login only) Full name of the admin. |
| `BOOTSTRAP_ADMIN_INITIALS` | (Optional, first-login only) Initials (used in offer references). |
| `BOOTSTRAP_ADMIN_EMAIL` | (Optional, first-login only) Email of the admin. |
| `Audit__RetentionDays` | Days of audit history kept by the daily cleanup job; default `365`. Set to `0` to keep all rows. |
| `BUILD_COMMIT` | Short git SHA of the deployed build. `GET /api/system/status` returns it as `commit`, or `null` when absent/unknown. The API reads `App:Commit` first, then `BUILD_COMMIT`, then `SOURCE_COMMIT` as a fallback. |

> `apiVersion` in `/api/system/status` is derived from the built assembly (and mirrored in `version`); it is independent of the frontend package version.

> **To verify:** The MonsterASP help pages (help.monsterasp.net) confirm Web Deploy on port 8172 and the environment variable mechanism. The availability of PostgreSQL depends on the specific hosting plan — **confirm with MonsterASP support that your plan includes PostgreSQL**.

### Live updates and SSE delivery

Both the change listener and the notification listener use a dedicated `LISTEN` connection. Npgsql
TCP keepalive is enabled with a 15-second interval, and a `SELECT 1` probe runs every 60 seconds;
a half-open socket therefore fails into the reconnect loop (0.5s to 30s exponential backoff with
jitter) instead of hanging. `GET /health` reports `liveListener` as `connected`, `disconnected`
or `disabled`; `disabled` means no notification connection string is configured, so neither
listener nor the counter poll runs. This is a diagnostic signal only: in-process publishes keep
live updates working without a dedicated connection, and the table-counter poll covers a failed
`LISTEN` when a connection string is configured. Local notification delivery never depends on
it. Every publish also sends `pg_notify`, so healthy instances still receive notifications
published by another instance.

`/api/events` is a long-lived `text/event-stream` response. The controller sends `Cache-Control:
no-store, no-transform` and `X-Accel-Buffering: no`, and disables response buffering. The shipped
`web.config` disables IIS dynamic/static compression and explicitly excludes `text/event-stream`
from dynamic compression. Keep those settings on any proxy in front of the API: buffering or
compressing the stream delays toasts and live screen refreshes. Frontend clients use `fetch` with
the bearer token rather than `EventSource`, because `EventSource` cannot set headers.

---

## 3. First-Login Bootstrap

On the very first deployment the `Users` table is empty and nobody can log in. The app supports an **environment-driven bootstrap**:

- If the `Users` table is empty **and** all of the following are set:
  - `BOOTSTRAP_ADMIN_USERNAME`
  - `BOOTSTRAP_ADMIN_PASSWORD`
  - `BOOTSTRAP_ADMIN_FULL_NAME`
  - `BOOTSTRAP_ADMIN_INITIALS`
- then the app creates one active user with `UserRole.Admin` and a BCrypt-hashed password. `BOOTSTRAP_ADMIN_EMAIL` is optional.
- A password shorter than 12 characters causes a clear startup error.
- If users already exist, the bootstrap does nothing.
- The password is never logged.
- The Development demo seeding (`seedDemoUser: true`) is unchanged.

**After the first successful login, remove the `BOOTSTRAP_ADMIN_*` variables from the MonsterASP control panel and restart the app.**

### .env.example additions

```dotenv
# First-admin bootstrap (Production only). Remove after first login.
BOOTSTRAP_ADMIN_USERNAME="admin"
BOOTSTRAP_ADMIN_PASSWORD="ChangeMe123456"
BOOTSTRAP_ADMIN_FULL_NAME="Admin User"
BOOTSTRAP_ADMIN_INITIALS="AU"
BOOTSTRAP_ADMIN_EMAIL="admin@example.com"
```

---

## 4. Rollback

- **Re-run an earlier workflow:** In GitHub Actions, open a previous successful run and click **Run workflow**.
- **Redeploy a previous artifact:** Download the `sales-portal-api` artifact (retained for 7 days) from an earlier build and deploy it manually with `msdeploy.exe`, or re-run the `deploy` job for that commit.

---

## 5. MonsterASP Web Deploy Behavior

MonsterASP stops and restarts the site automatically during a Web Deploy publish. The smoke test in the workflow waits up to ~2.5 minutes (10 attempts × 15 s) for `/health` to return `Healthy`.

---

## Secrets/Variables Checklist for the Owner

After merging this PR, the repository owner must set the following **by hand** (they are not in the repo):

### GitHub Secrets (repository or `production` environment)
- `MONSTER_WEBDEPLOY_SERVER`
- `MONSTER_SITE_NAME`
- `MONSTER_WEBDEPLOY_USER`
- `MONSTER_WEBDEPLOY_PASSWORD`

### GitHub Variable (repository or `production` environment)
- `APP_URL`

### MonsterASP Control Panel (Scripting > Environment variables)
- `DB_CONNECTION_STRING` (or `ConnectionStrings__Default`)
- `PORTAL_NOTIFICATIONS_CONNECTION_STRING` (only for a direct/session-mode connection)
- `JWT_SECRET` (≥ 32 chars)
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, …
- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__AutoMigrate=true` (first deploy / after migrations; **take a database backup immediately before the release**)
- `BUILD_COMMIT` (short git SHA of the deployed build)
- `BOOTSTRAP_ADMIN_USERNAME`
- `BOOTSTRAP_ADMIN_PASSWORD` (≥ 12 chars)
- `BOOTSTRAP_ADMIN_FULL_NAME`
- `BOOTSTRAP_ADMIN_INITIALS`
- `BOOTSTRAP_ADMIN_EMAIL` (optional)