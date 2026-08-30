# Breakdown evaluation rubric

Score each model response from 0 to 100. Automated gates run before qualitative scoring.

## Hard gates

- JSON parses: fail response if invalid.
- Output matches the required schema and has no unexpected prose: fail schema dimension if not.
- Every sub-task has a non-empty summary/description and at least two testable acceptance criteria.
- No credential, personal data, invented customer/internal identifier, or unsafe destructive instruction.

## Dimensions

| Dimension | Points | Guidance |
|---|---:|---|
| JSON validity | 10 | 10 valid; 0 invalid |
| Schema match | 10 | Required fields/types and item limits |
| Requirement coverage | 20 | Covers all meaningful behavior and failure paths from input |
| Granularity | 10 | Each task is independently executable, neither vague nor excessively tiny |
| No redundant/unnecessary work | 10 | No duplicate task or generic “research/meeting/check again” filler |
| Technical feasibility | 15 | Fits stated stack/constraints; no invented dependency |
| Acceptance criteria quality | 15 | Specific, observable, testable, includes important negative case |
| No hallucination | 5 | Does not invent fields, roles, endpoints, or policy absent from context |
| Language/clarity | 5 | Uses input language and concise engineering wording |

## Comparison labels

- 90–100: excellent; usable with minimal edits.
- 75–89: good; small edits.
- 60–74: partial; important edits required.
- below 60: reject.

Also record: kept/edited/rejected, normalized edit distance, missing coverage tags, redundant task count, hallucination notes, evaluator ID, model code, prompt version, dataset checksum, and inference latency.
