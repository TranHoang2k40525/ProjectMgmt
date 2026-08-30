# Sprint 1 Foundation Audit

Audit date: 2026-08-30 (Asia/Ho_Chi_Minh)

## Source-of-truth review

The complete `C:\Users\hoang\Downloads\Tài liệu đồ án` directory was inventoried before source changes. The review covered the MySQL DDL, database-design and ownership PDFs, ERD image, Jira CSV exports and workbook, problem-scope DOCX, Jira PowerShell helpers, and the Sprint 1 master prompt.

Precedence used for decisions:

1. `projectmgmt_schema_mysql.sql`
2. database design v3 PDF and 55-table ERD
3. three-person module-ownership PDF
4. latest Jira export / foundation work items
5. older technical and problem-scope documents

The older v2 document mentions SQL Server and the newer problem statement proposes ReBAC, CRDT, event sourcing, and additional research models. Those items conflict with or extend beyond the v3 DDL and are not Sprint 1 scope. This foundation therefore uses MySQL and the v3 modular-monolith boundaries.

Important DDL finding: the supplied DDL contains `DROP DATABASE IF EXISTS projectmgmt`. It will not be executed by this work. The physical database and ERD are treated as pre-existing.

## Git state

- `git rev-parse --show-toplevel`: not a Git repository.
- Initial branch and commit: not available.
- No reset, clean, checkout, or other destructive Git operation was performed.
- Git initialization is intentionally not part of the implementation because there is no initial history to preserve and the user did not request repository creation.

Final-state note: a later read-only check found that an external Git tool had created an empty root repository on `main` (no commit, no remote, and `.git/gk` metadata) after the initial audit. This implementation did not edit `.git`, create a commit, reset, clean, or discard any file.

## Current repository inventory

The initial repository contains 22 non-generated files and seven projects:

| Project | SDK / target | Initial state |
|---|---|---|
| `ProjectMgmt.Solution` | Web SDK / `net10.0` | Minimal controller host with WeatherForecast template |
| `ProjectMgmt.Core` | SDK / `net10.0` | Empty placeholder |
| `ProjectMgmt.IdentityAccess` | SDK / `net10.0` | Empty placeholder; references Core |
| `ProjectMgmt.ProjectManagement` | SDK / `net10.0` | Empty placeholder |
| `ProjectMgmt.IssueTracking` | SDK / `net10.0` | Empty placeholder |
| `ProjectMgmt.AiAssist` | SDK / `net10.0` | Empty placeholder; references Core |
| `ProjectMgmt.Tests` | SDK / `net10.0` | Empty class library; no test SDK/framework |

There are no nested source directories beyond the host controller/properties folders. All handwritten `.cs`, project, solution, JSON, HTTP, and user-project files were read. Generated `bin`, `obj`, and `.vs` content was excluded.

## Packages and configuration

- Initial direct package: `Microsoft.AspNetCore.OpenApi` 10.0.10 in the host.
- Initial build resolved `Microsoft.OpenApi` 2.0.0 and reported high-severity advisory `GHSA-v5pm-xwqc-g5wc` (`NU1903`).
- No EF Core provider, FluentValidation, Mapster, Serilog, Hangfire, SignalR client, health-check provider, or test packages are configured.
- `appsettings.json` only has default logging and `AllowedHosts`.
- There is no `ConnectionStrings:ProjectMgmt`, secret guidance, CORS configuration, or persistence setting.
- Launch URLs are HTTP 5083 and HTTPS 7282.

## Backend architecture gaps

- The host has no project references to modules and no composition-root registrations.
- `ProjectMgmt.Core` has no primitives, Result pattern, shared exceptions, clock, pagination, correlation ID, exception handling, security conventions, or MySQL registration helper.
- No Contracts projects exist.
- No `DbContext` exists. All nine required module contexts and their migrations-history configuration are missing.
- Missing implementation modules: SprintBacklog, AiCore, AiAssignment, AiDataOps, and Notification.
- Existing implementation modules contain only `Class1` placeholders.
- No controllers or business endpoints exist beyond WeatherForecast. Business features for later sprints will remain out of scope.
- There are no module-to-module implementation references yet, so no existing boundary violation needs preserving.

`ProjectMgmt.Core` will be retained as the existing layout's BuildingBlocks-equivalent project to avoid a duplicate shared-kernel project and unnecessary folder/namespace churn. Public shared types will use the `ProjectMgmt.BuildingBlocks.*` namespaces and this mapping will be documented.

## Database audit

- Source: MySQL 8.0.16+, InnoDB, utf8mb4, database `projectmgmt`.
- DDL: 55 tables, four reporting/feature views, and 64 columns marked `XMOD`.
- Module ownership follows the nine code modules in the master prompt: IdentityAccess, ProjectManagement, SprintBacklog, IssueTracking, AiCore, AiAssist, AiAssignment, AiDataOps, Notification.
- Cross-module GUIDs have no physical foreign key by design. Reporting views are the documented read-only exception for cross-module joins.
- The DDL's XMOD-index promise needs a repeatable audit. A conservative left-prefix index scan reports multiple XMOD columns without their own usable leading index. Sprint 1 will add a read-only validation script and report findings; it will not alter the DDL or live database.
- Generated columns `UserRole.ScopeKey` and `Sprint.ActiveGuard` are documented for future entity mappings. Sprint 1 will not generate all 55 EF entities.

## Frontend, DevOps, and quality gaps

- Angular: absent.
- Docker / Compose: absent.
- CI provider/configuration: absent. There is no Git remote; GitHub Actions will be supplied as a repository-ready default because CODEOWNERS is also requested.
- Tests: absent; the existing `ProjectMgmt.Tests` is not a runnable test project.
- README / architecture / contracts / ownership / test-process documents: absent.
- AI evaluation dataset and validator: absent.

## Baseline verification

Environment detected:

- .NET SDK 10.0.302; ASP.NET Core runtime 10.0.10
- Node.js 24.13.0; npm 11.6.2; Angular CLI 21.2.12
- Git 2.52.0
- Docker CLI not installed or not on `PATH`

Baseline `dotnet build ProjectMgmt.slnx -c Release` result:

- Succeeded: 0 errors
- Warnings: 2 `NU1903` warnings for the vulnerable transitive `Microsoft.OpenApi` 2.0.0 package
- Tests were not meaningful because no test SDK/framework or test cases existed.

## Changes planned for Sprint 1

- Keep the existing root layout and host name; do not move code merely to match a cosmetic target tree.
- Turn Core into the BuildingBlocks equivalent and add explicit module registration.
- Add missing module and Contracts projects without implementing later-sprint business CRUD/auth/AI behavior.
- Register nine empty-but-connectable module `DbContext` classes against the same logical connection string, each with an independent migration-history table; never auto-migrate.
- Freeze cross-module DTO/interfaces, add deterministic fakes, and add unit/integration/architecture test projects.
- Replace the WeatherForecast template with foundation health/OpenAPI endpoints and a short composition root.
- Create a standalone Angular shell with lazy placeholder routes and frontend infrastructure skeletons.
- Add local MySQL/Ollama Compose configuration, CI, read-only validators, process/architecture/ownership documentation, and 60 bilingual manual AI evaluation records.

## Explicitly retained or deferred

Retained:

- `net10.0`, ASP.NET Core Web API, `.slnx`, the existing module folders, launch ports, and modular-monolith direction.
- The database name/table names and the no-FK XMOD model.

Deferred to later sprints:

- Full registration/login, OTP, OAuth, token rotation, and scoped authorization implementation.
- All 55 EF entities/configurations and database migrations.
- Project/Issue/Sprint CRUD, workflow execution, SignalR business hubs, Hangfire jobs, Ollama inference, AI assignment, dataset training, and production deployment.
- DDL or live-database changes, including XMOD index remediation.
- ReBAC, CRDT, event sourcing/CQRS, process mining, Story Point ML, CP-SAT planning, RAG, and QLoRA training from the broader problem-scope document.
