# Contracts changelog

## 2026-08-31 — modular consolidation

- Consolidated seven Contracts projects into `ProjectMgmt.Contracts` while retaining public namespaces for compatibility.
- Added `ISprintLookupService` for sprint validation across module boundaries.
- Added `IIssueWorkInProgressCounter` so Planning can enforce WIP limits without referencing IssueTracking implementation.

## 2026-08-30 — v1 foundation freeze

- Added IdentityAccess user lookup/skill/profile contracts.
- Added Notification sender contract with navigation, metadata, and correlation fields.
- Added Project lookup, workflow validation/WIP result, and issue-number contracts.
- Added Issue create/read/sprint/skill contracts with Result and pagination primitives.
- Added distinct `IAiBreakdownFeedbackExportService` and `IAiAssignmentFeedbackExportService` names to avoid `IAiFeedbackExportService` ambiguity.
- Added AiDataOps evaluation-set descriptor contract.
- Contracts contain no EF entities and require cancellation-token propagation.
