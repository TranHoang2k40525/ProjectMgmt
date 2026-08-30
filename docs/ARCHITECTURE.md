# Architecture

## Shape

The backend is a modular monolith hosted by `ProjectMgmt.Solution`. The pre-existing root layout is retained to avoid namespace churn. `ProjectMgmt.Core` is the BuildingBlocks-equivalent project; business modules consume types under `ProjectMgmt.BuildingBlocks.*`.

The host is the only composition root and may reference implementations. An implementation may reference BuildingBlocks, its own Contracts, and another module's Contracts. Contracts may reference only the BCL and BuildingBlocks. Architecture tests enforce these project-reference rules and reject EF-facing types in Contracts.

## Module ownership and data

| Code module | DDL ownership |
|---|---|
| IdentityAccess | User, profile/login/token/RBAC/skills (11) |
| ProjectManagement | Organization, Project, component/version, workflow/configuration/board (10) |
| SprintBacklog | Sprint, SprintSnapshot, SprintMemberCapacity (3) |
| IssueTracking | Issue and 13 child/link/history tables (14) |
| AiCore | AiModel, AiPromptTemplate (2) |
| AiAssist | AiGenerationLog, AiSuggestedTask (2) |
| AiAssignment | workload/performance/run/candidate/decision (5) |
| AiDataOps | dataset/version/sample/cleaning/quality/training/evaluation (7) |
| Notification | Notification (1) |

All contexts use the same logical `ConnectionStrings:ProjectMgmt` and database. Sprint 1 contexts are intentionally entity-free: later owners add only their own entities/configurations. No code auto-creates, auto-migrates, or deletes the database.

Migration histories:

- `__EFMigrationsHistory_IdentityAccess`
- `__EFMigrationsHistory_ProjectManagement`
- `__EFMigrationsHistory_SprintBacklog`
- `__EFMigrationsHistory_IssueTracking`
- `__EFMigrationsHistory_AiCore`
- `__EFMigrationsHistory_AiAssist`
- `__EFMigrationsHistory_AiAssignment`
- `__EFMigrationsHistory_AiDataOps`
- `__EFMigrationsHistory_Notification`

## XMOD rule

An XMOD column is a raw GUID reference across module boundaries. It has no physical foreign key. Validation goes through a provider Contract, not a cross-module SQL join. Every XMOD column needs a usable left-prefix index.

The four DDL views are the documented read-only exception for reporting/feature extraction. They are not allowed in transactional business logic. `scripts/validate-xmod-indexes.ps1` audits the supplied DDL without changing it or connecting to MySQL.

## Web foundation

- Global `IExceptionHandler` maps known application exceptions to RFC Problem Details.
- Predicted business failures use `Result`/`Result<T>`.
- Correlation IDs use `X-Correlation-ID` and flow into Serilog's log context.
- `IClock` standardizes UTC time access.
- CORS origins come from configuration; only Angular localhost is enabled in Development.
- `/health/live` excludes readiness checks. `/health` runs a check for every module context.
- Sensitive EF logging requires both Development and the explicit `Persistence:EnableSensitiveDataLogging` setting.

## Provider compatibility decision ADR-001

Decision date: 2026-08-30.

The intended stack is .NET 10 + EF Core 10 + Pomelo. The [Pomelo NuGet package](https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql) currently has 9.0.0 as its stable release, while the upstream [EF Core 10 support issue](https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql/issues/2007) remains open. Using EF Core 10 with Pomelo 9 creates an unsupported dependency conflict, while changing to Oracle's provider violates the selected stack.

The working foundation therefore uses:

- `net10.0`
- EF Core 9.0.19
- Pomelo 9.0.0
- C# 13 language mode, the published workaround for Pomelo/EF9 under the .NET 10 SDK

All provider setup is isolated in `AddProjectMgmtMySqlDbContext<TContext>`. When stable Pomelo 10 exists, update the two central package versions, remove the C# 13 workaround if no longer necessary, and run all integration/architecture tests. No module registration signature needs to change.

## Deliberate Sprint 1 exclusions

No generic repository, MediatR/CQRS, service locator, full auth, business CRUD, SignalR hub, Hangfire job, Ollama inference, migration, or 55-entity generation is included. These belong to owning sprints and must build on the frozen Contracts.
