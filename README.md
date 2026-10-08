# DevQuest — Learn C# & .NET by playing 🤖

A gamified, Duolingo-style course for **C# and .NET**: 12 technology worlds, real
code execution in the browser, an energetic AI tutor robot named **Bit** (with a
voice), XP, streaks, levels and a leaderboard — fully bilingual (English/Spanish).

Built with **Blazor WebAssembly + ASP.NET Core + SQLite (EF Core)**.

## Features

- 🗺️ **12 worlds** grouped by track: C# Fundamentals, Methods & Collections, OOP,
  .NET CLI, LINQ, Async/Records, EF Core, Web APIs, Blazor, WPF, MAUI, Testing &
  Pro skills — **198 bilingual lessons**, all playable today.
- ⌨️ **Learners write and RUN real C#.** A sandboxed Roslyn runner child process
  compiles and executes the code with timeouts and an assembly allow-list.
- 🤖 **Bit, the AI tutor** — powered by any OpenAI-compatible API (configured for
  DeepSeek by default). Bit gives Socratic hints about *your* code, speaks its
  answers out loud (browser speech synthesis), and falls back to per-exercise
  static hints when the AI is unreachable.
- ⚡ **XP, levels, stars, daily streaks** — all persisted server-side in SQLite.
- 🏆 **Top-10 leaderboard** — all-time and weekly tabs.
- ❤️ **Full gamification loop**: hearts with timed refill (or gem refill), answer
  combos with XP bonus, daily goal, 3 rotating daily quests, 7-day login bonus
  cycle, Bit's Daily Challenge, double-XP weekends, a gem shop (heart refills,
  streak freezes, extra Bit hints) and **35 achievements**.
- ⛈️ **Lightning mode** (60-second XP sprint) and **Practice mode** (spaced
  repetition of missed exercises).
- 🧩 **Nine exercise types**: multiple choice, true/false, fill-the-blank, output
  prediction, type-in, free code (runner-graded), Parsons line-ordering, bug hunt
  (fix the code) and match-the-pairs.
- 📜 **Cheat sheets** per world (playful quick-reference cards).
- 🏄 **Real databases in the sandbox**: EF Core + SQLite exercises run against a
  fresh isolated database per submission.
- 🌐 **Live API exercises**: the sandbox hosts your minimal API on a real Kestrel
  server and tests it with real HTTP requests (status codes included).
- ⏳ **Real async**: awaited top-level programs work in the sandbox — the runner
  awaits Task-returning entry points and captures output across delays.
- 🖥️ **WPF/MVVM ViewModels and Blazor component logic run as real C#** — the UI
  worlds teach architecture with runnable, testable code.
- 🧪 **Real xUnit-style tests in the sandbox**: learners write [Fact]/[Theory]
  tests with Assert.* — the runner discovers and executes them, reporting
  per-test pass/fail.
- 📱 **MAUI world**: cross-platform architecture with runnable DI/platform-service
  patterns.
- 💾 **Progress export/import** as JSON (cloud-save style backups).
- 📲 **PWA**: installable, with offline support via a service worker.
- 🔐 **Sign in with Google**, pick a nickname and a robot avatar.
- 🧪 **Playground** — free C# sandbox with instant output.
- 🇪🇸🇬🇧 **Full EN/ES localization** — UI and lesson content.

## Run it

```bash
dotnet run --project src/DotNetQuest.App
```

Then open the URL shown (e.g. http://localhost:5117). The database is created
and migrated automatically.

## Deploy to a server

```bash
dotnet publish src/DotNetQuest.App -c Release -o publish
```

Copy the `publish/` folder to your server and run `dotnet DotNetQuest.App.dll`
— full instructions (reverse proxy, HTTPS, Google OAuth for production,
self-contained builds, running as a service) in **[DEPLOY.md](DEPLOY.md)**.

### Development login (no Google setup yet)

Until you configure Google OAuth, the landing page shows a **Development login**
button (only available when `ASPNETCORE_ENVIRONMENT=Development`). Use it to
create a player and play immediately.

## One-time configuration

Credentials live **outside** git so they never get committed. Create
`src/DotNetQuest.App/appsettings.Development.json` (already `.gitignore`d) with
your keys — it layers on top of `appsettings.json` when running locally:

```json
{
  "Authentication": {
    "Google": {
      "ClientId": "<YOUR_CLIENT_ID>.apps.googleusercontent.com",
      "ClientSecret": "<YOUR_CLIENT_SECRET>"
    }
  },
  "AiTutor": {
    "BaseUrl": "https://api.deepseek.com",
    "ApiKey": "<YOUR_API_KEY>",
    "Model": "deepseek-flash"
  }
}
```

(For production, fill the same sections in the server's own
`appsettings.json` — the publish step deliberately never overwrites it.
See **[DEPLOY.md](DEPLOY.md)**.)

**Getting the credentials:**

1. *Google sign-in* — https://console.cloud.google.com/apis/credentials →
   **Create credentials → OAuth client ID** → Web application → add
   `http://localhost:5117/signin-google` (and your production
   `https://yourdomain/signin-google`) under Authorized redirect URIs.
2. *Bit's AI brain* — get an API key from https://platform.deepseek.com (or any
   OpenAI-compatible provider: OpenAI, OpenRouter, Azure OpenAI, Ollama, LM
   Studio). Switching providers is just changing `BaseUrl` + `Model` + `ApiKey`.
   Without a key the game stays fully playable — Bit falls back to the built-in
   per-exercise hints.

> Never commit real credentials — anything you put in
> `appsettings.Development.json` stays local by design.

## Project layout

```
DotNetQuest.slnx
src/
  DotNetQuest.App/          # Blazor Web App host: auth, APIs, EF Core + SQLite
    Endpoints/              #   /api/me, /api/progress, /api/code/run, /api/tutor/chat(/stream),
                            #   /api/leaderboard, /api/daily, /api/quests, /api/shop, /api/achievements...
    Services/               #   AiTutorService (streaming), CodeRunnerService
    Data/                   #   Entities, GameDb, migrations (learnai.db)
  DotNetQuest.App.Client/   # WASM client: game UI, engine, i18n, content packs
    Pages/                  #   Landing, Onboarding, Worlds, World, LessonPlayer, Leaderboard, Playground, Profile
    Components/             #   BitMascot (SVG robot), BitChat (AI tutor), CodeRunPanel
    wwwroot/content/worlds/ #   Bilingual lesson content (JSON)
    wwwroot/i18n/           #   en.json / es.json UI strings
  DotNetQuest.Runner/       # Sandboxed console worker: Roslyn compile + run (child process)
  DotNetQuest.Shared/       # Content models, grader, XP/streak calculators, DTOs
tests/DotNetQuest.Tests/    # 67 tests: grader (9 types), XP/streaks/combos, quests,
                            # achievements, content QA (all 12 worlds), code executor
                            # (console, async, EF Core, live API, xUnit), certificates
```

## How the code runner sandbox works

`POST /api/code/run` spawns `DotNetQuest.Runner` as a child process and feeds it
JSON over stdin. The runner compiles the learner code with Roslyn using an
allow-list of framework assemblies, runs it per test case with redirected
console I/O, and reports output + per-test pass/fail. Runaway code (infinite
loops) is killed by the timeout — the game server is never blocked. Unsafe code
is rejected at compile time.

## Testing

```bash
dotnet test
```

## Roadmap

- ~~**Phase 2** — hearts, quests, achievements, gems shop, boss battles,
  lightning mode, practice, streaming tutor replies, Worlds 2 & 4~~ ✔ done.
- ~~**Phase 3** — OOP + EF Core + Web API worlds, QuestPDF certificates~~ ✔ done.
- ~~**Phase 4** — LINQ, Async/Records, WPF and Blazor worlds; async sandbox~~ ✔ done.
- ~~**Phase 5** — Testing/Pro + MAUI worlds, PWA, export/import, final QA~~ ✔ done.
- **All 5 phases complete — all 12 worlds playable, 198 lessons.**
