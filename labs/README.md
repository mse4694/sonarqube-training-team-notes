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
| `course-assets/` | Actual learner screenshots, offline documents, and the candidate-comparison HTML report |
| `security-tools/` | Workshop 12: HTTP/TLS measurements and supplementary tool examples |

From the repository root, set `LAB_PACK_DIR="$PWD/labs"`. For Workshop 8, copy `workflow-security` once to a disposable `WORKFLOW_DIR` and reuse that directory in Workshops 9 and 10. Do not copy the baseline over learner tests or a reviewed patch later. Start the guarded Agent inside `WORKFLOW_DIR`, where its only repair target is `voucher.py`.

`workflow-security/reference/` and the recorded validation JSON are labeled instructor comparison material. Keep them out of the coding Agent context as described by the control settings. The working `voucher.py` is intentionally vulnerable, and the learner test starter intentionally contains skipped cases. A supplied reference result is not evidence of a learner's own run.

Read each directory's README before running its commands. The source manifest records the reviewed files and their SHA256 hashes. Test outputs, local credentials and generated keys do not belong in this directory.

The candidate comparison example is `course-assets/candidate-comparison/report.html`; download and open it offline. Its AI recommendation is pending human review and does not certify application security. The matching audit and prompts are provided alongside it. Learners submit their own report, evidence and browser screenshots.
