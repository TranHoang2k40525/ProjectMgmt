# Module ownership

| Owner | Backend | Frontend |
|---|---|---|
| A — Trần Văn Hoàng | IdentityAccess, Notification, AiAssist; AiPromptTemplate logic | auth, notifications, shared ai-breakdown |
| B — Nguyễn Thế Hoài | ProjectManagement, SprintBacklog, AiAssignment | projects, backlog, board, reports |
| C — Hoàng Trần Huy Hoàng | IssueTracking, AiDataOps; AiModel logic | issue-detail, ai-dataops |

AiCore is shared at runtime but not ownerless: A owns prompt-template logic, C owns model-registry logic, and one designated person runs AiCore migrations. Every change to AiCore needs review from both logical owners.

Rules:

1. One table/entity/configuration/migration has one owner.
2. Cross-module reads/writes go through Contracts. No business SQL join or implementation reference crosses a boundary.
3. XMOD columns keep raw GUIDs, no physical FK, and a usable index.
4. Branch names use `feature/<module>/<short-description>`.
5. A PR touching another owner's area needs that owner's review. Do not make unreviewed “quick fixes” in another module.
6. Contract changes are additive within a sprint. Rename/removal requires prior coordination and a changelog entry.
7. Fakes unblock consumers but are registered only in tests or explicitly configured development scenarios.

The repository has no Git remote at Sprint 1 audit time. `.github/CODEOWNERS` uses expected handle-shaped placeholders and must be reconciled with actual GitHub usernames when the remote is created.
