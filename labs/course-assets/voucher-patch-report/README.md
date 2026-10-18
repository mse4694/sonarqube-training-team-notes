# Historical voucher patch report

Report generation and native captures: 2026-10-18.

This reporting phase runs in `/tmp/sonar102-html-work`, separate from the original guarded voucher implementation workspace. It does not extend the earlier `voucher.py`-only patch permission. No tests, candidate code, network scans or new patch were run during this phase.

The source execution artifacts were captured on 2026-10-18. Use the corrected `lab10/final-voucher` run with frozen suite SHA256 `62ba5ba228726dc75f2e916c71fe538e13e4214efe4d8b3540d40ab86b8574fe`; the older parent `voucher-verification.json` belongs to the superseded `96fd...` suite and is deliberately excluded.

`input-sources.json` and `input-hashes.json` identify copied originals. `agent-run.log` is the fresh Codex report-writing invocation. `prompt-native.png` and `response-native.png` are xterm captures. The marked prompt adds a vector outline around the original screenshot without changing its text. Browser captures show the real generated standalone HTML. `input-validation.json` confirms that this report invocation did not modify its supplied inputs.

Reported suite/calibration results are supplied historical observations. Static comparison of the before/after files and hashes does not independently reproduce their original execution. Native guard enforcement remains `native_not_tested`; the documented workflow used `manual_review`.
