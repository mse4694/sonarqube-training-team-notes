# SQLi fix: actual Docker test evidence

This native terminal capture is a fresh execution of the course Docker test command after applying only the SearchAsync Dapper parameter fix to a detached main-derived working copy. The three SQLi and six HappyPath assertions are unchanged. Result: 9 passed, 0 failed, 0 skipped; exit status 0.

The screenshot records real xterm pixels. The displayed command adds a console-summary filter under `set -o pipefail` to omit application log lines containing training session tokens; the inner Docker/.NET test command is unchanged. Filtering is not a claim that individual test output was removed or edited after capture. Full first-run logs remain private.

`fix.patch` and `NotesEndpoints.after.cs` record the sole source change. `provenance.json` records the base commit, file hashes, command, filter, result and asset hash. `run-public.log` is the actual filtered terminal transcript. It is not used to recreate the screenshot.

Open the [actual terminal screenshot](../task-6-1-terminal-sqli-fixed.png). Learners submit their own post-fix terminal result, not this reference image.
