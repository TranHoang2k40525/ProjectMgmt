# Definition of Done

A work item is Done only when:

- Code builds in Release configuration.
- Appropriate unit tests pass; integration tests pass for persistence/integration behavior.
- Architecture rules pass and no implementation/entity leaked across a module boundary.
- Frontend lint, unit tests, and production build pass when frontend was touched.
- Acceptance criteria and relevant negative/security cases pass.
- No credential/secret or sensitive production data is committed.
- Migration/rollback/index impact was reviewed; no automatic production migration was introduced.
- Logs/errors are actionable and do not disclose secrets.
- Peer review is complete, including the directory owner when required.
- README/Swagger/Contracts/changelog/Jira are updated as applicable.
- The change can be demonstrated in its target environment.
- No unresolved Critical/High defect blocks the flow.
