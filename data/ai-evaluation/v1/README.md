# Manual AI breakdown evaluation set v1

This candidate frozen set contains 60 synthetic, bilingual (30 Vietnamese / 30 English) Scrum stories across eight categories. It is independent of production data and contains no real person, customer, email, credential, token, or internal system name.

Files:

- `evaluation.jsonl` — one UTF-8 JSON object per line.
- `rubric.md` — manual and automated scoring rules.

Validate:

```powershell
pwsh .\scripts\validate-ai-evaluation.ps1
```

Before using the set for a formal baseline:

1. Two reviewers independently inspect every expected breakdown.
2. Resolve disagreements and change `reviewerStatus` from `draft` to `approved`.
3. Compute SHA-256, record it with model/prompt versions, and freeze the file.
4. Never use this evaluation file as training data.

Record schema:

- `id`, `language`, `category`
- `input.title`, `input.description`
- `expected.subTasks[]`: `summary`, `description`, `acceptanceCriteria[]`
- `tags[]`, `reviewerStatus`
