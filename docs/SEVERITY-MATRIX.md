# Defect severity matrix

| Severity | Meaning | Examples | Expected action |
|---|---|---|---|
| Critical / Sev-1 | Data loss, exploitable security failure, or core system cannot start/usefully run | destructive migration, authentication bypass, corrupt writes | Stop release; immediate triage and owner; regression evidence required |
| High / Sev-2 | Primary flow is unusable with no acceptable workaround | cannot create/transition issue, login consistently fails | Fix in current sprint/release gate; regression test required |
| Medium / Sev-3 | Partial impact with a reasonable workaround | one filter/report wrong, intermittent non-core notification | Prioritize in sprint/backlog with documented workaround |
| Low / Sev-4 | Minor UI/UX or cosmetic issue that does not block business | spacing, copy, non-critical visual inconsistency | Schedule by value; may accept as known issue |

Severity measures impact, not implementation difficulty. Priority may differ but cannot lower a security/data-loss severity.
