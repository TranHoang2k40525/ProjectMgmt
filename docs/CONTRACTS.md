# Cross-module Contracts v1

Contracts contain public data required by consumers, never EF entities. All asynchronous methods accept `CancellationToken`. DTO IDs are raw GUIDs matching XMOD references.

## IdentityAccess provides

### `IUserLookupService`

Consumers: ProjectManagement, SprintBacklog, IssueTracking, AiAssist, AiAssignment, Notification.

- `ExistsAsync(Guid userId, CancellationToken)` validates an XMOD user ID.
- `GetDisplayInfoAsync(Guid userId, CancellationToken)` returns `UserDisplayInfo?`.
- batch `GetDisplayInfoAsync(IReadOnlyCollection<Guid>, CancellationToken)` prevents N+1 calls.

`UserDisplayInfo` contains only `UserId`, display name, and optional avatar URL.

### `IUserSkillService`

Consumer: AiAssignment.

- `GetUserSkillsAsync(userIds, ct)` returns normalized skill code/name, level, skill experience, and declaration/verification flags.
- `GetProfileFeaturesAsync(userIds, ct)` returns job title, seniority, and overall years of experience for cold-start ranking.

## Notification provides

### `INotificationSender`

Consumers: every business/AI module.

- `SendAsync(NotificationDto, ct)` accepts recipient, optional project, type/content, optional target entity, optional string metadata, and correlation ID.

Sending has no dependency on a SignalR hub or Notification EF type. Persistence/broadcast policy remains inside Notification.

## ProjectManagement provides

### `IProjectLookupService`

Consumers: SprintBacklog, IssueTracking, AiAssist, AiAssignment.

- `ExistsAsync`, `GetProjectKeyAsync`, `GetIssueTypesAsync`, and `GetStatusesAsync` expose only public project configuration.

### `IWorkflowValidationService`

Consumer: IssueTracking.

- `CanTransitionAsync(projectId, fromStatusId, toStatusId, userPermissions, ct)`.
- `GetInitialStatusAsync(projectId, ct)`.
- `CheckWipLimitAsync(boardId, statusId, ct)`.

`WorkflowValidationResult` includes `IsAllowed`, a user-facing reason, and optional current/limit counts so the API/UI can explain a WIP rejection.

### `IIssueNumberGenerator`

Consumer: IssueTracking.

- `NextAsync(projectId, ct)` reserves the next project-local integer. The later real implementation must be transactional/concurrency-safe.

## IssueTracking provides

### `IIssueService`

Consumers: AiAssist and normal Issue API orchestration.

- `CreateIssueAsync(CreateIssueDto, ct)` returns `Result<CreateIssueResultDto>`.
- The DTO includes future optional Sprint/parent/epic/assignee/priority/points/due-date/AI trace fields without making later-sprint fields required.

### `IIssueReadService`

Consumers: AiAssist, SprintBacklog, AiAssignment.

- `GetByIdAsync`.
- paged `GetBySprintAsync` and `GetOpenByAssigneeAsync`.
- `GetStatusHistoryAsync` and `GetAssignmentHistoryAsync`.

### `IIssueSprintService`

Consumer: SprintBacklog.

- `MoveToSprintAsync(issueIds, sprintId, ct)`.
- `ReturnToBacklogAsync(issueIds, ct)`.

### `IIssueSkillService`

Consumers: AiAssist and AiAssignment.

- `GetRequiredSkillsAsync(issueIds, ct)` returns a dictionary keyed by issue ID with skill ID, minimum level, weight, and source.

## AiAssist provides

### `IAiBreakdownFeedbackExportService`

Consumer: AiDataOps.

- `ExportBreakdownSamplesAsync(fromUtc, toUtc, ct)` exports immutable original AI text/AC, final user-edited text/AC, action, edit ratio, and review time.

The distinct name replaces the ambiguous older proposal `IAiFeedbackExportService`.

## AiAssignment provides

### `IAiAssignmentFeedbackExportService`

Consumer: AiDataOps.

- `ExportAssignmentSamplesAsync(fromUtc, toUtc, ct)` returns suggestion/final user, outcome/reason, feature snapshot, and decision time.

## AiDataOps provides

### `IEvaluationSetCatalog`

Consumers: future evaluation/training orchestration.

- `GetCurrentAsync(ct)` returns version, count, SHA-256 checksum, and frozen time. It does not expose dataset EF entities.

## Versioning rule

- Within a sprint, changes are additive and backward compatible.
- Do not remove, rename, narrow, or change DTO meaning without advance team agreement.
- Record every public signature/semantic change in root `CONTRACTS-CHANGELOG.md`.
- A new optional member is preferred to breaking existing consumers; a semantic break requires a new version/type.
