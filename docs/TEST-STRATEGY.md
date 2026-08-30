# Test strategy

## Test layers

| Layer | Use when | Foundation command |
|---|---|---|
| Unit | Pure domain/application rules, validators, Result mappings, deterministic fakes | `dotnet test ProjectMgmt.Tests.Unit` |
| Architecture | Project references, Contracts isolation, BuildingBlocks independence, no EF types in Contracts | `dotnet test ProjectMgmt.Tests.Architecture` |
| Integration | API host/pipeline, persistence, MySQL/provider, external integration adapters | `dotnet test ProjectMgmt.Tests.Integration` |
| Frontend unit | Components, guards, interceptors, signal stores, pure UI logic | `npm test -- --watch=false` |
| E2E/smoke | Critical browser/API paths after real features and an environment exist | later sprint |
| UAT | Product acceptance against agreed scenarios before release candidate | release sprint |

Integration tests must not connect to a developer's real database implicitly. A database test must use an explicitly provisioned disposable/isolated target and must never run the supplied destructive DDL without an approved manual step.

## Required coverage by change

- Pure behavior: unit tests for success, boundary, and predicted failure.
- Persistence/query/migration: integration test against compatible MySQL and an index/query review.
- Contract/module reference: architecture suite must stay green.
- API: status code, Problem Details shape, cancellation, authorization when introduced.
- Frontend: lint/build plus unit tests for touched logic; E2E for critical end-to-end flows once available.
- AI: frozen evaluation set, exact schema/JSON validation, qualitative rubric, model/prompt/dataset version, and no train/test leakage.

## Test data and fakes

`ProjectMgmt.Tests.Fakes` provides deterministic, configurable, no-network/no-database doubles. Production registration must never select them implicitly. Shared test records use synthetic GUIDs and data; no real email, credential, customer, or internal secret belongs in fixtures.

## CI gates

Backend restore/build/test, frontend install/lint/test/build, Compose syntax, and AI JSONL validation fail the workflow. A later quality-gate sprint will add real MySQL integration containers, secret/dependency scanning, browser E2E, and release artifacts.

## Defect regression

Every fixed High/Critical defect needs an automated regression test at the lowest reliable layer. Medium defects need a regression test unless the cost is disproportionate and documented. Low visual-only defects may use review evidence.
