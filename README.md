# ScrumAI Project Management

Sprint 1 foundation for a contract-first modular monolith: ASP.NET Core on .NET 10, MySQL, an Angular standalone SPA, SignalR-ready frontend infrastructure, local Ollama, and separate module persistence boundaries.

## Repository map

- `ProjectMgmt.Solution` — API host/composition root.
- `ProjectMgmt.Core` — retained legacy project name; this is the BuildingBlocks equivalent and exposes `ProjectMgmt.BuildingBlocks.*` namespaces.
- `ProjectMgmt.<Module>` — nine module implementations and their `DbContext`/registration.
- `ProjectMgmt.<Module>.Contracts` — cross-module DTOs/interfaces only.
- `ProjectMgmt.Tests.Unit`, `.Integration`, `.Architecture`, `.Fakes` — test architecture.
- `frontend/projectmgmt-web` — Angular standalone shell.
- `data/ai-evaluation/v1` — frozen manual evaluation-set candidate and rubric.
- `docs`, `scripts`, `.github`, `docker-compose.yml` — team/process/operations foundation.

## Prerequisites

- .NET SDK 10
- Node.js 24 and npm 11
- MySQL 8.0.16+ (existing database) or Docker for an isolated local service
- Ollama when exercising later AI work

Pomelo has not published an EF Core 10-compatible stable package as of 2026-08-30. The solution therefore targets .NET 10 but pins Pomelo 9.0.0 and EF Core 9.0.19 behind one persistence extension. See [Architecture](docs/ARCHITECTURE.md) for the upgrade decision.

## Configure the existing database safely

The API reads only `ConnectionStrings:ProjectMgmt`. No startup migration, `EnsureCreated`, or destructive DDL execution exists.

Development User Secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:ProjectMgmt" "Server=localhost;Port=3306;Database=projectmgmt;User=YOUR_USER;Password=YOUR_PASSWORD;CharSet=utf8mb4;" --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj
```

The Host already contains a non-secret `UserSecretsId`; never commit the resulting local secret store.

Or set environment variable `ConnectionStrings__ProjectMgmt`. Do not put credentials in `appsettings*.json`.

## Run

```powershell
dotnet restore .\ProjectMgmt.slnx
dotnet run --project .\ProjectMgmt.Solution\ProjectMgmt.Solution.csproj --launch-profile http
```

Open:

- live process health: `http://localhost:5083/health/live`
- readiness including all module DB checks: `http://localhost:5083/health`
- OpenAPI in Development: `http://localhost:5083/openapi/v1.json`

Frontend:

```powershell
Set-Location .\frontend\projectmgmt-web
cmd /c npm ci
cmd /c npm start
```

Open `http://localhost:4200`.

## Verify

```powershell
dotnet build .\ProjectMgmt.slnx -c Release
dotnet test .\ProjectMgmt.slnx -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-ai-evaluation.ps1

Set-Location .\frontend\projectmgmt-web
cmd /c npm run lint
cmd /c npm test -- --watch=false
cmd /c npm run build
```

The XMOD audit is intentionally read-only and currently reports DDL findings with a non-zero exit code:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-xmod-indexes.ps1
```

## Local Docker services

See [Local Docker](docs/LOCAL-DOCKER.md). Compose never mounts or runs the supplied destructive DDL. Do not start it on port 3306 if that would conflict with the existing MySQL instance.

## Team rules

- Branches: `feature/<module>/<short-description>`.
- Cross-module calls use Contracts; never reference another implementation or its EF entities.
- XMOD GUIDs do not have physical foreign keys and must have a usable index.
- Each module owns its migration history table; AiCore migrations have one designated runner.
- Contract removals/renames require team agreement and an entry in [CONTRACTS-CHANGELOG](CONTRACTS-CHANGELOG.md).

Start with [Sprint 1 Audit](docs/Sprint1-Audit.md), [Architecture](docs/ARCHITECTURE.md), and [Contracts](docs/CONTRACTS.md).
