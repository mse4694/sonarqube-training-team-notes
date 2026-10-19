# Voucher patch report (C# fixture), example run of 19 October 2026

Everything here comes from one real run of Workshop 10.2 on the C# voucher fixture:

- An AI Coding Agent wrote the learner tests from the Lab 9.1 prompt and patched
  `Voucher/VoucherService.cs` from the Lab 10.2 plan and patch prompts (read-only plan first, then human approval).
  The final verification run re-applied exactly those two files (same SHA256) to a fresh workspace.
- `./scripts/voucher-verify` produced `patch.diff`, `readonly-check.txt`, `tests.log`, `checker.json`, `checker.log` and the
  before/after copies of `VoucherService.cs`.
- The human wrote `implementation-request.md`, `implementation-result.md` and `AgentExecutionRecord.md`.
- A separate AI session wrote `report.html` and `result.md` from those files only.

No native hook adapter was installed for this run, so the control status is `manual_review` /
`native_not_tested`. The only edit after the run: the absolute checkout path in `tests.log` was replaced
with `<checkout>`. This is an example of the expected evidence, not a learner's own result.
