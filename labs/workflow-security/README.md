# One-time voucher workflow fixture

C# on .NET 10 (xUnit for the tests), run through the Team Notes tools container
(`./scripts/lab-tools`), so nothing is installed on the host. No HTTP server,
database, persistent balance, browser, real credential or remote target is used
by the supplied code. The fake `alice`/`bob` actors and voucher codes are local
simulation data. This is a workflow/security-test exercise, not another BOLA,
SQL injection, password-hashing or Sonar setup exercise.

`./scripts/voucher-workspace` copies this folder ONCE to your own `WORKFLOW_DIR`
(`.lab-workspaces/voucher.XXXXXX`). Use that same folder for Business Logic & Workflow
Security, Security Test Engineering and the Agent exercise. In Lab 9.1, the Agent may help
write `tests/RedemptionContractTests.cs` from the contract while the human reviews
assertions and runs the suite. In Workshop 10, only `Voucher/VoucherService.cs` is editable;
the completed tests and helpers stay read-only.
Implement the working policy rather than importing the supplied reference
repair, tests or probes as an answer. Human diff review checks this as well as
the path allowlist and test results.

## Layout

| Path | Role | Who may change it |
| --- | --- | --- |
| `Voucher/VoucherService.cs` | The redemption engine, intentionally vulnerable | The Agent in Workshop 10 only |
| `Voucher/VoucherModel.cs` | `Voucher`, `Redemption`, `VoucherStore` (issue/activate/cancel/snapshot/events) | Read-only |
| `Voucher/ScenarioSupport.cs` | `Scenario.Now`/`ExpiresAt`, `ActiveService`, `ConcurrentObservation`, `RedemptionObservation` | Read-only |
| `Probes/` | `dotnet run --project Probes -- <scenario>`: manual observations as JSON or a table | Read-only |
| `tests/RedemptionContractTests.cs` | Learner contract tests (starter: 2 complete + 6 skipped) | The learner (with AI help) in Lab 9.1, then frozen |
| `reference/` | Instructor engines, mutants, completed contract and checker | Instructor material; the Agent may not read it |

## Business contract

- `Issue` creates `issued`; `Activate` accepts only `issued` and produces `active`.
- `Cancel` accepts only `issued`/`active` and produces `cancelled`.
- Redemption accepts only `active` with `now < expiresAt`.
- Unknown, issued, cancelled, explicitly expired, deadline-expired and used
  vouchers must not create a new receipt or redemption effect.
- The SAME actor retry is a replay, not permission for a second effect. This
  exercise rejects the replay; an idempotent API returning an old receipt would
  require a different explicit contract.
- Two concurrent callers may produce at most ONE successful redemption and ONE
  event. Both being `used` at the end is insufficient: check the effect count.

Clock injection is a trusted test seam. `Scenario.Now = 10`, `Scenario.ExpiresAt = 100`;
`now = 100` is already expired. No `Thread.Sleep`, wall-clock expiry or real payment is involved.
In a deployed system this would use an authoritative server clock and identity;
this fixture does not implement authentication.

## API

```csharp
using VoucherLab;

var service = new VoucherService();          // optionally seed: new VoucherService([new Voucher("V-1", 100, VoucherStates.Expired)])
service.Issue("V-DEMO", expiresAt: 100);
service.Activate("V-DEMO");
var result = service.Redeem("V-DEMO", actor: "alice", now: 10);
// Redemption: Ok, Reason, ReceiptId, Actor
service.Snapshot("V-DEMO");                  // Code, ExpiresAt, State, RedeemedBy, RedemptionCount
service.Events();                            // copy of committed fake business effects
```

`VoucherStore.Gate` is a `System.Threading.Lock`: guard it with `lock (Gate) { ... }`. `Monitor.Enter(Gate)` would take a
different lock than the base class (the compiler only warns, CS9216).

`Redeem(..., checkpoint: null)` exposes a TEST-ONLY scheduling callback. The
read-only race helper `Scenario.ConcurrentObservation` uses a `Barrier(2)` so both callers
reach that seam before continuing, and the whole observation has a five-second deadline. A
patch that waits at the Barrier while holding the lock makes the first caller time out after two
seconds and the barrier is then broken for the other one: both report `BrokenBarrierException` in
`Errors`, so the race test fails on an empty `Errors` assertion. A caller that never returns, or an
error while setting up or reading the observation, makes the helper throw
`ObservationHarnessException` (the probe exits 1 and reports `inconclusive`; xUnit records an
error). Neither is evidence of a secure result or a detected business-policy mutation.
Timeouts do not manufacture a race using sleeps. The helper reports how many times the
callback ran (`CheckpointCalls`), so deleting the seam is not a valid way to make the
concurrency test pass.

The secure reference calls this seam **before** the atomic check/transition
lock. Waiting for the second caller while holding that lock is a broken design.
Which actor wins is intentionally unspecified; success/effect count is stable.

## Workshop 8: manual observations

From the Team Notes root (the first `dotnet` command restores and builds, later ones reuse it):

```bash
source .lab-workspaces/voucher.env
cd "$WORKFLOW_DIR"
"$LAB_TOOLS" dotnet --version
"$LAB_TOOLS" dotnet run --project Probes -- positive
"$LAB_TOOLS" dotnet run --project Probes -- replay
"$LAB_TOOLS" dotnet run --project Probes -- state
"$LAB_TOOLS" dotnet run --project Probes -- cancel
"$LAB_TOOLS" dotnet run --project Probes -- expiry
"$LAB_TOOLS" dotnet run --project Probes -- race
# One table of every scenario (one line per redeem call), easy to read and screenshot:
"$LAB_TOOLS" dotnet run --project Probes -- all --table
```

Default is the current **working** `Voucher/VoucherService.cs`, identified by its SHA256 in
the JSON (`engine_sha256`). It is initially intentionally vulnerable:

| Observation | Original working engine | Secure contract/reference |
| --- | --- | --- |
| Issued → activate → redeem | success, 1 effect | success, 1 effect |
| Unknown voucher | reject, 0 effects | reject, 0 effects |
| Same-actor replay | second success, 2 effects | second reject, 1 effect |
| Issued/cancelled/explicit expired state | success, 1 effect each | reject, 0 effects |
| `now == expiresAt` | success, 1 effect | reject, 0 effects |
| Two callers with synchronized overlap | 2 successes, 2 effects | 1 success, 1 effect |

These are observed facts in a local model. Compare them with the contract to
write the invariant/control gap. The probe itself does not declare the system
secure or change the source file. Each CLI selection runs only that scenario.
Instructors can observe a comparison engine with
`dotnet run --project Probes -p:VoucherEngine=reference -- all --table` (or `baseline`).

## Workshop 9, Lab 9.1: AI-assisted learner tests

`tests/RedemptionContractTests.cs` contains TWO complete positive/control tests
and SIX unfinished tests marked `[Fact(Skip = "TODO: ...")]`. Use the reviewed contract to ask an
AI Agent to draft the missing assertions, review them yourself, then remove those six `Skip`
values. Removing `Skip` but leaving `Assert.Fail("not implemented")` is not a completed test;
removing assertions to get green is invalid.

```bash
"$LAB_TOOLS" dotnet test tests --logger "console;verbosity=normal"
```

Starter: `Total tests: 8`, `Passed: 2`, `Skipped: 6`. **A green run with six skipped tests is
not a secure result or a completed contract.** The completed eight-case contract executes 8 /
skips 0: on the original engine 2 pass and 6 fail; on the secure reference all 8 pass.
Extra meaningful cases may be added; record actual counts if they differ.

Instructor examples live separately in `reference/`. Do not replace your
learner-authored tests with them and call that your authored work. Humans can
inspect the provided example for comparison; the Agent exercise denies reads
of `reference/**` so the supplied repair is not an answer copied by the Agent.

```bash
# PROVIDED instructor contract, frozen baseline/reference/mutants:
"$LAB_TOOLS" dotnet run --project reference/ContractCheck
# Test your completed suite against those comparison engines:
"$LAB_TOOLS" dotnet run --project reference/ContractCheck -- \
  --learner-tests tests/RedemptionContractTests.cs --expect-working vulnerable
```

The checker copies the selected suite and one engine into a throwaway test project per engine
under `obj/contract-check/` (never over a fixture file), runs them all (a hang stops after 60
seconds), and prints JSON with the suite and working-engine SHA256 (`--summary` prints a short
table instead). Every engine's assembly has the same name, so a suite cannot ask which engine it
is testing; a suite that inspects the engine's code by reflection could still game it, which is
why the human reviews the tests. The original vulnerable engine is frozen separately
(`reference/Engines/VulnerableVoucherService.cs`) so the quality comparison still works after
the working file is repaired.

Expected provided comparison:

| Engine | Executed | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| frozen baseline | 8 | 2 | 6 | 0 |
| secure reference | 8 | 8 | 0 | 0 |
| missing state guard | 8 | 5 | 3 | 0 |
| missing expiry guard | 8 | 7 | 1 | 0 |
| replay allowed | 8 | 7 | 1 | 0 |
| non-atomic transition | 8 | 7 | 1 | 0 |
| skipped scheduling seam | 8 | 7 | 1 | 0 |

At least eight executed tests, zero skips and errors, baseline failures, a passing
reference, and detection of every weakened control are required by the quality
check. A failure counts only when it is an xUnit assertion failure: any other exception (for
example `InvalidOperationException`), a harness error or a compile error is an `error` and never
counts as detecting a mutation (a row whose project does not compile is marked `build_failed`).

## Workshop 10: same case, guarded Agent repair

Keep `WORKFLOW_DIR` and your completed tests. Human-provided policy allows edits
to `Voucher/VoucherService.cs` only; the model, helpers, probes, tests and reference stay
read-only. Use the separate Agent-control fixture (`labs/agent-control`) with that allowlist.
The human records the read-only plan and native probe result, reviews the diff,
and runs tests. The supplied fixture does not install a hook automatically.

`./scripts/voucher-freeze` (before the Agent) records the SHA256 of every file except
`Voucher/VoucherService.cs`; `./scripts/voucher-verify` (after) reports changed, missing and newly
added files (checked again after the patched code has run), the learner tests, and the checker,
and collects them in a report folder:

```bash
"$LAB_TOOLS" dotnet test tests --logger "console;verbosity=normal"
"$LAB_TOOLS" dotnet run --project Probes -- all --table
"$LAB_TOOLS" dotnet run --project reference/ContractCheck -- \
  --learner-tests tests/RedemptionContractTests.cs --expect-working secure
```

The last command additionally requires the **working** implementation to pass
the completed suite. Do not count a passing reference run as the Agent repair.
Do not modify acceptance assertions or the race seam to hide a failure.

`validation.json` records the INSTRUCTOR validation of this fixture: the provided contract and
the manual observations on the baseline and reference engines, with the source hashes they
were run against. It is not proof that a learner authored tests or that the learner's own
native runtime has been tested. The original working `VoucherService.cs` in this teaching
folder remains vulnerable.

## Limits

Each observation checks one-process in-memory state. The reference lock does not prove a
multi-process database transaction, distributed idempotency, authentication,
authorization, durable storage or deployment isolation. The test seam/fixtures
are not production inputs. No network/service or real data was used in the
recorded checks. A file allowlist is not a .NET execution sandbox: review an
Agent patch before executing it in the disposable workspace, and do not give
the exercise real credentials or production connections.
