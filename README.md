# TorrenClou Backend

The .NET 9 API and background workers behind
[TorrenClou](https://tc.gitnasr.com) — self-hosted torrent-to-cloud.

> **Just want to run it?** You do not need this repo.
>
> <!-- snippet:install-linux -->
> ```bash
> curl -fsSL https://raw.githubusercontent.com/TorrenClou/deploy/main/install.sh | bash
> ```
> <!-- /snippet -->
>
> Full documentation: **[tc.gitnasr.com/docs](https://tc.gitnasr.com/docs)**

## What lives in this repo

Clean Architecture, so the domain does not depend on frameworks or databases.

```
TorrenClou.sln
├── TorrenClou.Core/                 # Domain entities, interfaces, enums, options
├── TorrenClou.Application/          # Use cases, DTOs, validators
├── TorrenClou.Infrastructure/       # EF Core, Redis, Hangfire, external clients
├── TorrenClou.API/                  # ASP.NET Core controllers, middleware, DI
├── TorrenClou.Worker/               # Torrent downloads
├── TorrenClou.GoogleDrive.Worker/   # Google Drive uploads
└── TorrenClou.S3.Worker/            # S3 uploads
```

**Dependency flow:** `API / Workers` → `Application` → `Core` ← `Infrastructure`

Each worker is an independent process sharing the same PostgreSQL and Redis as
the API, pulling from its own Hangfire queue, and scalable on its own.

> The projects are still named `TorrenClou.*` while the product is `TorrenClou`.
> That rename is tracked separately — it touches every namespace and assembly,
> so it lands in one atomic change rather than piecemeal.

## Developing

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and
[Docker](https://docs.docker.com/get-docker/).

```bash
git clone https://github.com/TorrenClou/backend.git
cd backend
docker compose up -d postgres redis
dotnet run --project TorrenClou.API
```

The API listens on `http://localhost:47200`. No `.env` is needed — compose and
the app both fall back to working development defaults. Copy `.env.example` to
`.env` only if you want to override something.

Workers run the same way, in their own terminals:

```bash
dotnet run --project TorrenClou.Worker
dotnet run --project TorrenClou.GoogleDrive.Worker
dotnet run --project TorrenClou.S3.Worker
```

The API reference and every configuration key are documented at
[tc.gitnasr.com/docs](https://tc.gitnasr.com/docs) — they are generated from this
code, so they are not restated here.

## Repositories

| Repository | Contents |
|------------|----------|
| [frontend](https://github.com/TorrenClou/frontend) | Next.js web app |
| [website](https://github.com/TorrenClou/website) | Documentation site — the canonical docs live here |
| [deploy](https://github.com/TorrenClou/deploy) | All-in-one image, installer, CI |

## License

MIT — see [LICENSE](https://github.com/TorrenClou/backend/blob/main/LICENSE).
