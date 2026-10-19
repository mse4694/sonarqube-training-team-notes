# AgentExecutionRecord

| Field | Value |
| --- | --- |
| Workspace | .lab-workspaces/voucher.E1vRDa (WORKFLOW_DIR from Workshop 8) |
| Frozen learner tests | tests/RedemptionContractTests.cs sha256 b224d1b81e1f00532df75423727ed1bd6b966cfe0576c53bc3b2ff165e89d33f |
| Write scope given to the Agent | Voucher/VoucherService.cs only |
| Control status | manual_review / native_not_tested (no native hook adapter installed for this run) |
| Native probe | not run (no adapter); no native block is claimed |
| Plan | read-only plan with file:line citations, approved by the human before patching |
| Human checks (voucher-verify) | readonly-check.txt 24 OK, 0 changed or missing, 0 added (checked again after the tests); tests.log Test Run Successful, Total tests 8, Passed 8; checker.json quality_passed true, working_expectation_met true, overall_passed true |
| Guard helper self-test (voucher-freeze) | 34 direct helper tests passed; native integration NOT tested |
| Not covered | HTTP, authentication, persistence, distributed transactions, deployment |
