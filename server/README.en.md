# Server: deploy your own cloud

[简体中文](README.md) · **English**

This directory holds the **server-side source** of TokenTracker. If you want to run your own server instead of using the official cloud, everything you need is here: for yourself, or for a team or a community to share.

Three things to know up front:

- **The official cloud is a paid hosted service; self-hosting is free**, but you own the server, domain, backups and upgrades. Self-hosting runs the MIT-licensed code in this repository; the scripts the official cloud runs are not necessarily identical to the repository.
- **Self-hosting is a technical preview, not a one-click install.** Upstream verified the installer and the functions in an isolated Linux environment. The full chain of "a public server plus a packaged desktop app" was not verified upstream, and has not been verified in this fork either.
- **On the client side**, this fork already lets you enter a self-hosted instance's address under Settings → Cloud. No environment variables needed.

## What is in this directory

| Path | Contents |
| --- | --- |
| `self-host/` | The private backend installer: it generates the schema SQL (`install.cjs`), builds the backend functions (`build-functions.cjs`), and keeps the install manifest (`manifest.cjs`) and the schema scripts (`baseline.sql`, `aggregation.sql`, `private-rpc-access.sql`). `acceptance.cjs` is an acceptance harness for developers |
| `migrations/` | Database migrations (PostgreSQL), 59 files. They are the incremental history of the official database, so **never run all of them on a new database**. The installer picks out the parts it needs |
| `cloudflare/` | The Cloudflare Worker backend: the official hosted cloud's newer backend, in charge of accounts, subscriptions, payments and the public leaderboard. **Not supported for self-hosting**; see the end of this page |
| `docs/` | Upstream's self-hosting documents, with more background, evidence and acceptance checklists than this page |

One part of the server code lives outside this directory: the **backend function sources** are in [`desktop/dashboard/edge-patches/`](../desktop/dashboard/edge-patches/), 46 `.ts` files (device authorization, usage ingestion, account reads, billing, payment webhooks, leaderboards and so on). They share type definitions with the dashboard, which is why upstream keeps them under `dashboard/`. This fork leaves them there so that the weekly upstream sync does not run into a large number of conflicts. `self-host/build-functions.cjs` reads the 14 functions that self-hosting needs from there.

## A self-hosted instance has three parts

1. **The InsForge platform.** An open-source backend-as-a-service (PostgreSQL, authentication, API, function runtime), deployed with Docker Compose.
2. **TokenTracker's application part.** Tables, RPCs, triggers, permissions, plus 14 backend functions. The installer in this directory takes care of it.
3. **A dashboard that points at the instance**, or the desktop app from this fork.

## Deployment steps

### 0. Prepare

**A server.** Linux with Docker Engine and Compose v2, a domain name, and an HTTPS reverse proxy. Do not expose the database, PostgREST or the function runtime ports directly; expose only the API and the admin entrance through HTTPS.

**A build machine.** The installer and the function build borrow the dependencies of the desktop npm package, so install them in `desktop/` first:

```bash
git clone https://github.com/LceAn/TokenTracker.git
cd TokenTracker/desktop
npm ci
```

(`npm --prefix dashboard ci` is only needed to run the developer acceptance harness `acceptance.cjs`, or to build the web dashboard yourself.)

### 1. Install the InsForge platform

Follow InsForge's official [Docker Compose self-hosting instructions](https://github.com/InsForge/InsForge#self-hosted-docker-compose). A few points:

- Pick a release you have reviewed, and pin the image version or digest.
- The official setup script generates keys but does not start the services. Set `API_BASE_URL` and `VITE_API_BASE_URL` to your public API domain before starting them.
- The admin password, database password, JWT signing secret, encryption key and admin API key stay on the server. The frontend only ever uses the public anon key.

The combination upstream verified is InsForge v2.3.3 (PostgreSQL 15.18, Deno 2.0.6). The installer requires the platform to provide `auth.users(id)`, `auth.uid()` and the roles `anon`, `authenticated` and `project_admin`; it fails if they are missing.

### 2. Generate and apply the application schema

From the repository root on the build machine:

```bash
node server/self-host/install.cjs --sql /absolute/private/path/tokentracker-install.sql
```

This only writes a file; it does not connect to any database. Review the SQL, then run the whole file in one transaction on the server:

```bash
psql --set ON_ERROR_STOP=1 --file /absolute/private/path/tokentracker-install.sql
```

- Run it as the database owner, in a new application database. Use the server's protected connection configuration; do not put passwords on the command line. The file carries its own transaction and install lock.
- Do not run every migration in `migrations/`: the history contains operator-specific fixes for the official user base.
- The installation is recorded in `tokentracker_self_host_installations` (each step with its SHA-256). Running the unchanged installer again preserves users, tokens and usage; a step whose content changed fails explicitly instead of replacing an installation silently.
- The last step sets the instance to `hosting_mode='self_hosted'`: private sync is free, with no device quota or history window, and no paid orders are created.

If your InsForge instance is already linked through its CLI, you can go through the platform's migration API instead (`--migration`); see [docs/self-hosting-backend.md](docs/self-hosting-backend.md).

### 3. Build and deploy the backend functions

```bash
node server/self-host/build-functions.cjs /absolute/private/path/tokentracker-functions
```

This writes 14 bundled functions (one `.js` file each) and a `functions.json` manifest. Deploy each function in the manifest through your instance's management API or admin interface.

The 14 functions are: the three device-authorization endpoints, device token issuance, device rename, usage ingestion, seven account usage reads, and the account and device billing API. **Not included:** the public leaderboard and payment webhooks.

### 4. Configure the functions' environment

```dotenv
INSFORGE_BASE_URL=https://api.your-domain.example
INSFORGE_SERVICE_ROLE_KEY=your-server-only-service-credential
ANON_KEY=your-public-anonymous-key
JWT_SECRET=your-platform-signing-secret
TOKENTRACKER_DASHBOARD_URL=https://dashboard.your-domain.example
TOKENTRACKER_BILLING_ENVIRONMENT=live
```

- If the platform signs sessions with RS256, use `JWT_PUBLIC_KEY` instead of `JWT_SECRET`.
- `TOKENTRACKER_DASHBOARD_URL` must be HTTPS (loopback HTTP is allowed). The device-authorization link points at it.
- `TOKENTRACKER_BILLING_ENVIRONMENT` accepts only `live` or `sandbox`, and may be left unset. See "Notes from a real deployment" below.
- Do not configure Waffo, Paddle, WeChat Pay or Alipay keys for a personal instance.

### 5. Dashboard (optional)

To use it in a browser, build the web dashboard in `desktop/` with your own address and anon key. The output lands in `dashboard/dist/`, and your server must fall back to `index.html` for unknown paths:

```bash
cd desktop
VITE_INSFORGE_BASE_URL=https://api.your-domain.example \
VITE_INSFORGE_ANON_KEY=your-public-anonymous-key \
npm --prefix dashboard run build
```

The build rejects admin keys, service-role keys and user JWTs; it accepts only the public anon key. InsForge v2.3.3 issues anon keys of the form `anon_` followed by 40 or 64 hex characters; older instances may use a JWT with `role=anon`.

If you enable OAuth sign-in on InsForge, allow the `tokentracker://auth/callback` redirect (used by the desktop apps) and your own dashboard origin and `/dashboard` callback (used by web sign-in).

### 6. Connect the clients

**The desktop app from this fork.** Open Settings → Cloud. In the Server card, choose to use a self-hosted server, enter the server address and the anon key, click the connection test, and save once it passes. Switching signs you out of the current account; sign in again on the new server. To go back, restore the official cloud.

**The command line, or other builds.** Use the environment variables `TOKENTRACKER_INSFORGE_BASE_URL` and `TOKENTRACKER_INSFORGE_ANON_KEY`.

The anon key is public and may live in a client. Admin keys, service-role keys and signing secrets must never ship in a frontend or a client.

### 7. What counts as deployed

Verify each of these against the server's real HTTPS address. Unit tests are no substitute:

- A new account can sign in, sign out and refresh its session; one account cannot read another account's data.
- The device-authorization link points at your own dashboard; two clients can write usage to the same account without overwriting each other's device identity.
- Re-uploading the same batch does not increase totals; corrections, device filters, time zones and daylight-saving time, and model costs agree with the local view.
- The interface recovers from session expiry, a paused device and an offline backend, without losing the local queue.
- Browser and desktop requests both go to the instance you chose; no data reaches the official backend or the public leaderboard.
- A backup restores into another instance with identical totals.

## Notes from a real deployment

These come from a real self-hosted deployment, recorded in a comment on upstream issue [#1119](https://github.com/xiufengsun/TokenTracker/issues/1119), at that time on upstream `0397a2c`. For the four rows marked "verified" we confirmed the behaviour on the current code ourselves. The rest are environment issues that we have not reproduced in the current environment.

| Symptom | Cause and what to do |
| --- | --- |
| Running the installer again on another machine reports `Installed step has changed`, although the manifest was not touched | **Verified.** The installer hashes the raw bytes it reads for each step (SHA-256), so a different line ending gives a different checksum. The repository's `.gitattributes` fixes line endings to LF, and a clean clone is LF. If a checkout turned them into CRLF (for example files in an old checkout on Windows), its checksums differ from everyone else's. Always generate the install files from a clean clone. |
| Every cloud function returns 503, and the caller only sees a failure | **Verified.** `TOKENTRACKER_BILLING_ENVIRONMENT` accepts only `live` and `sandbox`. The "free" part of self-hosting comes from the policy the installer's last step writes into the database, not from this variable. Leave it unset or set it to `live`. |
| Usage migrated from the official cloud comes out about 3 times too high | **Verified.** For each `(hour_start, source, model)` only the row with the largest `total_tokens` is kept, while the client's `queue.jsonl` is a series of growing snapshots. Take the maximum per key and upload that; never sum the queue line by line. |
| A multi-user instance wants a leaderboard | **Verified.** Self-hosting does not include the leaderboard: none of the 14 functions provides it, and the schema scripts create none of its tables. You have to build your own for now. |
| A newly created RPC returns 404 | PostgREST caches the schema. After creating or changing a function, run `NOTIFY pgrst, 'reload schema';` |
| A function's return type did not change | `CREATE OR REPLACE FUNCTION` cannot change a return type. `DROP FUNCTION` first. |
| A plpgsql function fails with `column reference is ambiguous` | Output column names of `RETURNS TABLE` must differ from the queried column names; use aliases. |
| Deploying the billing function fails at the command line | The bundled function is about 270 KB. Inlining that much source in a command line exceeds the shell's argument limit. Send the request body from a file instead, for example curl's `-d @file`. |
| `npm ci` fails on linux/arm64 | A native dependency (zstd-related) has no arm64 prebuilt binary. Build the functions on an x64 machine or on macOS, then copy the output to the server. |
| The desktop app's sign-in window fails | At the time (desktop 1.5.15) the sign-in window requested the remote instance directly, and on a mainland-China network behind a Cloudflare-proxied domain the request was often cut off. We patched the embedded dashboard so that sign-in requests went through the local server's proxy. Whether the current version still has this problem on such a network is something we have not verified. |

## Backup, restore and upgrades

- **Backup.** Keep the database dump, the server configuration and the file storage together in one backup, stored separately from the server, and rehearse a restore.
- **Tool versions.** The PostgreSQL server inside the image and its bundled tools may differ (the image upstream verified runs PostgreSQL 15.18 while its default tools are 18.4). Use matching versions of `pg_dump` and `pg_restore`.
- **Before an upgrade.** Record the application commit, the InsForge version and image digest, and the installed steps. Back up first.
- **Upgrading the application.** The installer checks installed steps: unchanged steps are left alone, changed steps fail. An upgrade therefore needs a separately reviewed manifest. Do not delete the installation ledger, and do not edit an already-installed step to force a retry.
- **Upgrading InsForge.** Check authentication, storage and the function runtime for changes at the same time.

## Not done yet

- A complete fresh install, two-device sync, account isolation, backup restore and upgrade test on a real public server.
- Sign-in and sync acceptance for the packaged desktop apps (Windows, macOS, Linux) against a real instance.
- If you want OAuth or email sign-in: configure the instance's own provider and verify it.
- Of the problems in the notes above, this fork has so far only documented them; none is fixed in code yet.

## About the Cloudflare Worker backend

`cloudflare/` is the official hosted cloud's newer backend, which upstream made primary from v1.5.31. It is tied to upstream's accounts, payments, public leaderboard and official domain, and is not supported for self-hosting. The server switch in this fork also targets InsForge instances. When this fork syncs to upstream v1.5.31 or later, that part needs rework, and this page will be updated then.

## Related documents

- [docs/self-hosting.md](docs/self-hosting.md) and [docs/self-hosting.zh-CN.md](docs/self-hosting.zh-CN.md): upstream's self-hosting overview
- [docs/self-hosting-backend.md](docs/self-hosting-backend.md): backend installation details and verification evidence
- [docs/self-hosting-status.md](docs/self-hosting-status.md): implementation status and remaining gates
- [InsForge self-hosting instructions](https://github.com/InsForge/InsForge#self-hosted-docker-compose)
