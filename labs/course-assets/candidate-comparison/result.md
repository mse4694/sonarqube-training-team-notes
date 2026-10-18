# Outputs

- `report.html`: self-contained offline security review, with Evidence Ledger, counts, escaped examples, candidate comparison, exact security/formatting predicates, limitations and one integration gap.
- `result.md`: this summary.

AI recommendation: reject A for demonstrated renderer failures; prefer B for further validation within the recorded scope. Final human approval: **PENDING**.

Recorded evidence: A passes 3/5 original checks and 0/2 extra probes; B passes 5/5 and 2/2. No candidate code, tests, scanners, packages, browser execution or network requests were run for this report.

Preparation checks: parsed supplied JSON, aggregated evidence counts, checked supplied source-byte SHA-256 values against source-validation.json and inspected generated HTML structure for prohibited active elements/attributes and external assets. Examples are escaped text.

Human checks still required: establish deployed source/build identity, review actual shared-note HTTP responses and authorization outcomes, inspect DOM/execution behavior through the browser insertion path, and confirm the exact security and formatting predicates. The report specifies the gap’s target, predicate and next evidence. Renderer evidence does not establish HTTP, browser, auth or deployment validation. Human approval remains pending.
