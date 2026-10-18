# AgentExecutionRecord — learner example
Workspace: same Voucher workspace and frozen Lab9 tests.
Agent: Codex CLI 0.163.0; plan reviewed before patch.
Allowed edit: voucher.py only; other input hashes unchanged.
Control: manual_review; native_not_tested.
Human diff review: state/time/replay checks + atomic commit; checkpoint retained.
Human test: 8 passed, 0 skipped, 0 errors.
Calibration: frozen baseline 2/8; reference8/8; all5mutants detected.
Scope: single-process memory; no HTTP/auth/DB/Sonar conclusion.
