# TorrentClou Backend

The .NET 9.0 backend for [TorrentClou](https://github.com/TorrenClou) — a self-hosted cloud torrent management platform.

> **Just want to run the whole project?** See [TorrenClou/deploy](https://github.com/TorrenClou/deploy) for one-command setup.

## Architecture

Built with **Clean Architecture** to keep business logic independent of frameworks, databases, and external services.

```
TorreClou.sln
├── TorreClou.Core/                 # Domain entities, interfaces, enums
├── TorreClou.Application/          # Use cases, DTOs, validators, service interfaces
├── TorreClou.Infrastructure/       # EF Core, Redis, external API clients
├── TorreClou.API/                  # ASP.NET Core controllers, middleware, DI
├── TorreClou.Worker/               # Torrent download background jobs
├── TorreClou.GoogleDrive.Worker/   # Google Drive sync background jobs
└── TorreClou.S3.Worker/            # S3 upload background jobs
```

**Dependency flow:** `API / Workers` → `Application` → `Core` ← `Infrastructure`

## Tech Stack

| Technology | Purpose |
|-----------|---------|
| .NET 9.0 / ASP.NET Core | Web API framework |
| Entity Framework Core | ORM, migrations, database access |
| PostgreSQL 15 | Primary database |
| Redis 7 | Caching, job queues, distributed locks |
| Hangfire | Background job processing |
| Serilog | Structured logging |
| Prometheus / OpenTelemetry | Metrics and tracing (optional) |

## API Endpoints

| Route | Purpose |
|-------|---------|
| `POST /api/auth/login` | Credential-based authentication |
| `GET /api/health/ready` | Health check |
| `GET/POST /api/torrents` | Torrent management |
| `GET/POST /api/jobs` | Background job management |
| `GET/POST /api/storage/gdrive` | Google Drive integration |
| `GET/POST /api/storage/s3` | S3 integration |
| `GET /hangfire` | Hangfire dashboard |
| `GET /metrics` | Prometheus metrics |

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://docs.docker.com/get-docker/) (for PostgreSQL + Redis)

## Development Setup

### 1. Clone and configure

```bash
git clone https://github.com/TorrenClou/backend.git
cd backend
cp .env.example .env
# Edit .env with your values
```

### 2. Start dependencies

```bash
docker-compose up -d postgres redis
```

### 3. Run the API

```bash
dotnet run --project TorreClou.API
```

The API starts at `http://localhost:5000`. Migrations are applied automatically on startup when `APPLY_MIGRATIONS=true`.

### 4. Run workers (optional)

In separate terminals:

```bash
dotnet run --project TorreClou.Worker
dotnet run --project TorreClou.GoogleDrive.Worker
dotnet run --project TorreClou.S3.Worker
```

## Docker (Individual Services)

Build and run services independently:

```bash
# Build API image
docker build -f TorreClou.API/Dockerfile -t torrenclou-api .

# Build worker image
docker build -f TorreClou.Worker/Dockerfile -t torrenclou-worker .
```

Or use docker-compose for the full stack (API + workers + Postgres + Redis + observability):

```bash
docker-compose up -d
```

## Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `POSTGRES_DB` | No | `torrenclo` | Database name |
| `POSTGRES_USER` | No | `torrenclo_user` | Database user |
| `POSTGRES_PASSWORD` | **Yes** | - | Database password |
| `JWT_SECRET` | **Yes** | - | JWT signing key (min 32 chars) |
| `JWT_ISSUER` | No | `TorrenClou_API` | JWT issuer |
| `JWT_AUDIENCE` | No | `TorrenClou_Client` | JWT audience |
| `ADMIN_EMAIL` | No | `admin@example.com` | Admin login email |
| `ADMIN_PASSWORD` | **Yes** | - | Admin login password |
| `FRONTEND_URL` | No | `http://localhost:3000` | Frontend URL (CORS) |
| `APPLY_MIGRATIONS` | No | `true` | Auto-apply EF Core migrations |
| `HANGFIRE_WORKER_COUNT` | No | `10` | Worker thread count |

See [`.env.example`](.env) for the complete list including observability settings.

## Observability Stack (Optional)

The docker-compose includes a full observability stack:

- **Prometheus** (`:9090`) — Metrics collection
- **Loki** (`:3100`) — Log aggregation
- **Grafana** (`:3001`) — Dashboards and visualization

## Project Structure Details

### Workers

Each worker is an independent .NET process that:
- Connects to the same PostgreSQL and Redis instances as the API
- Picks up jobs from Hangfire queues
- Can be scaled independently
- Has its own Dockerfile for isolated deployment

| Worker | Purpose |
|--------|---------|
| `TorreClou.Worker` | Downloads torrents to local storage |
| `TorreClou.GoogleDrive.Worker` | Syncs completed downloads to Google Drive |
| `TorreClou.S3.Worker` | Uploads completed downloads to S3-compatible storage |

### CI/CD

Merging to `main` triggers two pipelines:

1. **Individual images** (this repo's `docker-build.yml`) — Builds and pushes separate API/worker images to ghcr.io
2. **Combined image** (via `dispatch-combined-build.yml`) — Triggers a build of the all-in-one image in the [deploy repo](https://github.com/TorrenClou/deploy)

## Related Repositories

| Repository | Description |
|-----------|-------------|
| [TorrenClou/frontend](https://github.com/TorrenClou/frontend) | Next.js 15 web application |
| [TorrenClou/deploy](https://github.com/TorrenClou/deploy) | All-in-one Docker image, CI/CD, run scripts |

## License

See [LICENSE](LICENSE).
