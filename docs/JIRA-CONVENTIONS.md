# Jira and delivery conventions

Jira project/workflow creation is external to the repository and was already prepared in the supplied exports/workbook. Repository conventions:

- Project key: `SCRUMAI`.
- Workflow: Backlog → Selected for Development → In Progress → Code Review → Testing → Ready for Release → Done, with Blocked/rework transitions as configured in Jira.
- Components: Shared/DevOps, M1 through M10, and QA/Security.
- Labels: `owner-a|owner-b|owner-c`, `backend|frontend|testing`, `ai|ai-assignment|ai-data`, `cicd|ci|cd`, and `sprint-<n>`.
- Branches: `feature/<module>/<short-description>`.
- A work item is started only when Definition of Ready passes; it is closed only when Definition of Done passes.
- The nine foundation tasks are called Sprint 0 in the workbook but Sprint 1 in the Jira export/master prompt. Repository Sprint 1 completion refers to that nine-task foundation scope.
- Public Contract changes require a linked changelog update and provider/consumer review.
