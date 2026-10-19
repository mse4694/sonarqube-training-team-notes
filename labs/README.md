# Course lab sources

These files are included in the existing Team Notes repository. No separate ZIP download or application clone is required.

| Directory | Used for |
| --- | --- |
| `password-storage/` | Workshop 3: isolated password verification/rehash probe |
| `report-evidence/` | Workshops 5.1 and 5.2: instructor report, source snapshots and scope metadata |
| `sonarcloud-issue/` | Workshop 10.1: offline SonarCloud issue evidence and PDF |
| `workflow-security/` | Workshops 8 to 10: one voucher workspace, observations, AI-assisted tests and guarded repair |
| `agent-control/` | Workshop 10: manually reviewed Agent controls |
| `candidate-review/` | Workshop 11: evidence audit and candidate comparison |
| `prompts/` | Reusable English prompts without exercise numbers |
| `course-assets/` | Actual learner screenshots and the prompts used for them (finished example documents are on branch `solution-day2`) |
| `security-tools/` | Workshop 12: HTTP/TLS measurements and supplementary tool examples |

From the repository root, set `LAB_PACK_DIR="$PWD/labs"`. For Workshop 8, `./scripts/voucher-workspace` copies `workflow-security` (a C# / .NET 10 project) once to a disposable `WORKFLOW_DIR` that Workshops 9 and 10 reuse. Do not copy the baseline over learner tests or a reviewed patch later. Start the guarded Agent inside `WORKFLOW_DIR`, where its only repair target is `Voucher/VoucherService.cs`.

`workflow-security/reference/` and the recorded validation JSON are labeled instructor comparison material. Answers never live on `main`: the secure engine, the weakened-control mutants, the completed contract tests, the example AI repair and the finished example documents of Workshops 7 and 11 are on branch `solution-day2` (see its `SOLUTION.md`), and `voucher-workspace` copies only the checker's comparison files from there into the private workspace, hash-checked. Keep them out of the coding Agent context as described by the control settings. The working `Voucher/VoucherService.cs` is intentionally vulnerable, and the learner test starter intentionally contains skipped cases. A supplied reference result is not evidence of a learner's own run.

Read each directory's README before running its commands. The source manifest records the reviewed files and their SHA256 hashes. Test outputs, local credentials and generated keys do not belong in this directory.

The Workshop 11 audit prompt is `course-assets/candidate-comparison/audit-prompt.txt`. The finished example audit and report are answers, so they live on branch `solution-day2`; the Lab shows only their screenshots. Learners submit their own audit and browser screenshot.
