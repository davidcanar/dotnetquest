# Deploying DevQuest

The `publish/` folder (created by `dotnet publish`, see below) is a complete,
self-sufficient deployment. Copy it anywhere and run it — that's the whole story.

## What's in the folder

```
DotNetQuest.App.dll        ← the server (APIs + static Blazor client)
runner/                    ← the sandboxed code-execution child process (required!)
wwwroot/                   ← Blazor WASM client, content packs, i18n, PWA files
appsettings.json           ← configuration (Google OAuth + DeepSeek keys)
appsettings.Production.json
learnai.db                 ← created automatically on first start
```

## Server requirements

- **.NET 10 Runtime** installed (framework-dependent deploy). Get it at
  https://dotnet.microsoft.com/download/dotnet/10.0 — "ASP.NET Core Runtime" for
  your server OS. If you can't install a runtime, see "Self-contained" below.
- Write permission for the app folder (it creates/maintains `learnai.db` there).
- Outbound HTTPS (Google sign-in + DeepSeek calls go out; nothing inbound except
  the port you serve on).

## Run it

```bash
# Windows
DotNetQuest.App.exe --urls http://0.0.0.0:8080

# Linux / macOS
dotnet DotNetQuest.App.dll --urls http://0.0.0.0:8080
```

Open `http://<your-server>:8080`. The database is created and migrated
automatically on first start.

## Configure before going live

Edit `appsettings.json` **on the server** (or use environment variables):

```json
"Authentication": {
  "Google": {
    "ClientId": "...",       ← your Google OAuth Client ID
    "ClientSecret": "..."
  }
},
"AiTutor": {
  "ApiKey": "...",           ← your DeepSeek (or other) key
  "Model": "deepseek-flash"
}
```

Environment-variable alternative (no files touched):

```
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
AiTutor__ApiKey=...
```

Valid DeepSeek model ids: `deepseek-flash` (recommended), `deepseek-v4-pro`,
and legacy names like `deepseek-chat` / `deepseek-reasoner`. If Bit says its
brain is offline **and shows a 🔎 reason line**, that line is the provider's
exact error (e.g. `401` = bad key, `402` = account out of credit).

⚠️ Without Google credentials the sign-in button won't appear (development
login is disabled outside Development). Without the DeepSeek key Bit falls
back to the built-in per-exercise hints — the game still works.

## Google OAuth for your server

In Google Cloud Console → Credentials → your OAuth client → **Authorized
redirect URIs**, add your server's URL:

```
https://your-domain.com/signin-google
```

(Use the exact scheme/host the browser will visit. The path is always
`/signin-google`.)

## HTTPS & reverse proxy

The app doesn't terminate TLS itself — put it behind a reverse proxy:

- **Caddy** (easiest, auto-HTTPS): `your-domain.com { reverse_proxy localhost:8080 }` —
  Caddy sends the required forwarded headers automatically.
- **nginx**: must forward the scheme/host or Google sign-in will bounce back to
  the landing page:

  ```nginx
  location / {
      proxy_pass http://localhost:8080;
      proxy_set_header Host $host;
      proxy_set_header X-Forwarded-Proto $scheme;
      proxy_set_header X-Forwarded-Host $host;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
  }
  ```

- **IIS**: install the ASP.NET Core Hosting Bundle and use the generated `web.config`
  (`dotnet publish --self-contained false -p:PublishIISAssets=true`).

The PWA (install + offline) requires HTTPS — another reason for the proxy.

### Troubleshooting: "Google login bounces back to the landing page"

1. **Missing forwarded headers.** The app must see `https` as its scheme, or the
   OAuth `redirect_uri` it computes (`http://localhost:8080/signin-google`)
   won't match the one registered with Google and authentication fails silently.
   The app honors `X-Forwarded-Proto` / `X-Forwarded-Host` out of the box (from
   any proxy address — Caddy and Cloudflare send them automatically; for nginx
   add the `proxy_set_header` lines above).
2. **Redirect URI not registered.** In Google Cloud Console → Credentials →
   your OAuth client, `https://your-domain.com/signin-google` must be listed
   under Authorized redirect URIs (exact scheme/host, path `/signin-google`).
3. **App in Testing mode.** On the OAuth consent screen, only accounts listed
   under Test users can sign in until you press "Publish app".

A quick server-side check of what Google will be told:

```bash
curl -sI "http://localhost:8080/login-google"   -H "X-Forwarded-Proto: https" -H "X-Forwarded-Host: your-domain.com"   | grep -i location
# location must contain redirect_uri=https%3A%2F%2Fyour-domain.com%2Fsignin-google
```

## Persistence & backups

Everything lives in `learnai.db` next to the executable. Back it up with any
file backup; to move servers, copy the file. Users can also export/import
their own progress as JSON from the profile page.

## IIS

1. Install the **ASP.NET Core Hosting Bundle** on the server (contains ANCM).
   Restart IIS after installing: `net stop was /y && net start w3svc`.
2. Create an IIS site pointing at the copied `publish` folder (the included
   `web.config` wires up the AspNetCoreModuleV2 handler, in-process).
3. The site must have an **https binding** (Google + the PWA require it).
4. Give the app pool **write access** to the folder — it needs to create/update
   `learnai.db` and the `keys/` folder (data-protection keys that keep login
   cookies working across app-pool recycles). Either:
   - App Pool → Advanced Settings → **Load User Profile = True**, or
   - `icacls "C:\inetpub\devquest" /grant "IIS AppPool\<poolName>:(OI)(CI)M"`

### Troubleshooting: "Google login bounces back to the landing page"

1. **Redirect URI mismatch.** Google Cloud Console → Credentials → your OAuth
   client must list exactly `https://devquest.dcsystech.com/signin-google`
   (scheme + host + path).
2. **Cookie/keys problem.** If the auth or correlation cookie can't be
   decrypted (keys not persisted), every request looks logged out. Fixed in the
   current build: keys persist to the `keys/` folder — verify it exists and is
   writable by the app pool identity.
3. **Check what the server tells Google:**

   ```powershell
   curl.exe -sI "http://localhost:8080/login-google" | findstr /i location
   # location must contain redirect_uri=https%3A%2F%2Fyour-domain.com%2Fsignin-google
   ```

4. **Read the actual error:** since this build, a failed Google callback
   redirects to `/?authError=<message>` and the landing page displays it.
   That message (typically "correlation failed" or "oauth state was missing or
   invalid") pinpoints the cause.

## Troubleshooting: "My AI brain is offline"

Bit's chat shows the provider's exact error in a 🔎 line when the DeepSeek
call fails. Common causes:

- `provider returned 401` — the ApiKey is wrong
- `provider returned 402` — the DeepSeek account is out of credit
- `provider returned 404 ... Model Not Exist` — the Model name is wrong
  (current ids: `deepseek-flash`, `deepseek-v4-pro`; see
  https://api-docs.deepseek.com for current names)
- `could not reach https://api.deepseek.com` — the server has no outbound
  internet access

You can also check `GET /api/tutor/status` (logged in) for the last failure.

## Logs (where to see AI tutor / auth errors)

- **`logs	utor-errors.log`** (next to the DLL) — every failed DeepSeek call
  with the exact provider error (401 bad key, 402 out of credit, 404 model not
  exist, network unreachable). Also shown in the Bit chat with a 🔎 prefix.
- **`logs\stdout_*.log`** — full ASP.NET Core console output (startup, auth
  warnings, DB commands). Enabled in web.config (stdoutLogEnabled).
- **Windows Event Viewer → Application** — unhandled exceptions.

Bit's chat shows the provider error in a 🔎 line too, so you usually don't
need the logs — but they're there.

## Optional: run as a service

- **Linux (systemd)**: `systemd` unit running `dotnet DotNetQuest.App.dll`,
  `WorkingDirectory=/opt/devquest`, `Restart=always`.
- **Windows**: `sc create DevQuest binPath=...\DotNetQuest.App.exe` or NSSM,
  or just run it under IIS.

## Self-contained (no runtime install)

Prefer a folder that includes the .NET runtime? Republish with a RID:

```bash
dotnet publish src/DotNetQuest.App -c Release -r linux-x64 --self-contained true -o publish-linux
dotnet publish src/DotNetQuest.App -c Release -r win-x64 --self-contained true -o publish-win
```

Then start `./DotNetQuest.App` (Linux) or `DotNetQuest.App.exe` (Windows) — no
`dotnet` command needed. Note: the code-execution sandbox still spawns
`dotnet runner/DotNetQuest.Runner.dll`, so the runtime (or a self-contained
runner) must be available on the machine.

## Rebuilding the deploy folder

```bash
dotnet publish src/DotNetQuest.App -c Release -o publish
```

The csproj handles copying the `runner/` sandbox into the output automatically.
