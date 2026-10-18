# One-time voucher workflow fixture

Python 3.10+ standard library on Linux, macOS or WSL (the race helper requires
the `fork` process start method). No SDK package installation, HTTP server,
database, persistent balance, browser, real credential or remote target is used
by the supplied code. The fake `alice`/`bob` actors and voucher codes are local
simulation data. This is a workflow/security-test exercise, not another BOLA,
SQL injection, password-hashing or Sonar setup exercise.

Copy this folder ONCE to your own temporary `WORKFLOW_DIR`. Use that same folder
for Business Logic & Workflow Security, Security Test Engineering and the Agent
exercise. In Lab 9.2, the Agent may help write `tests/test_redemption_contract.py`
from the contract while the human reviews assertions and runs the suite. In
Workshop 10, only `voucher.py` is editable; the completed tests and helpers stay
read-only.
Implement the working policy rather than importing the supplied reference
repair, tests or probes as an answer. Human diff review checks this as well as
the path allowlist and test results.

## Business contract

- `issue` creates `issued`; `activate` accepts only `issued` and produces `active`.
- `cancel` accepts only `issued`/`active` and produces `cancelled`.
- Redemption accepts only `active` with `now < expires_at`.
- Unknown, issued, cancelled, explicitly expired, deadline-expired and used
  vouchers must not create a new receipt or redemption effect.
- The SAME actor retry is a replay, not permission for a second effect. This
  exercise rejects the replay; an idempotent API returning an old receipt would
  require a different explicit contract.
- Two concurrent callers may produce at most ONE successful redemption and ONE
  event. Both being `used` at the end is insufficient: check the effect count.

Clock injection is a trusted test seam. `NOW=10`, `EXPIRES_AT=100`; `now=100`
is already expired. No sleeps, wall-clock expiry or real payment is involved.
In a deployed system this would use an authoritative server clock and identity;
this fixture does not implement authentication.

## API

`VoucherService` in `voucher.py` exposes:

```python
from voucher import VoucherService
from voucher_model import Voucher

service = VoucherService()  # optionally seed controlled Voucher records
service.issue("V-DEMO", expires_at=100)
service.activate("V-DEMO")
result = service.redeem("V-DEMO", actor="alice", now=10)
# Redemption: ok, reason, receipt_id, actor
service.snapshot("V-DEMO")  # state, expiry, redeemed_by, redemption_count
service.events()           # copy of committed fake business effects
```

`redeem(..., checkpoint=None)` exposes a TEST-ONLY scheduling callback. The
read-only race helper uses `threading.Barrier(2)` so both callers reach that seam
before continuing. The whole race observation runs in its own disposable child
process with a five-second deadline, including final state/effect reads. A
patch that waits on the Barrier while holding the lock breaks the Barrier after
its two-second timeout: both workers report `BrokenBarrierError` in `errors`, so
the race test fails on `errors == []`. A hang the Barrier cannot break is
terminated with its owned child; the probe exits1 and reports
`inconclusive`, while unittest records a harness error. Neither is evidence of
a secure result or a detected business-policy mutation. Timeouts do not
manufacture a race using sleeps. The helper asserts that both callbacks were invoked, so
deleting the seam is not a valid way to make the concurrency test pass.

The secure reference calls this seam **before** the atomic check/transition
lock. Waiting for the second caller while holding that lock is a broken design.
Which actor wins is intentionally unspecified; success/effect count is stable.

## Workshop 8: manual observations

From `WORKFLOW_DIR`:

```bash
python3 --version
python3 probes.py positive
python3 probes.py replay
python3 probes.py state
python3 probes.py cancel
python3 probes.py expiry
python3 probes.py race
# Or produce all independent observations in one JSON artifact:
python3 probes.py all > manual-observations.json
```

Default is the current **working** `voucher.py`, identified by its SHA256 in
the JSON. It is initially intentionally vulnerable:

| Observation | Original working engine | Secure contract/reference |
| --- | --- | --- |
| Issued → activate → redeem | success, 1 effect | success, 1 effect |
| Unknown voucher | reject, 0 effects | reject, 0 effects |
| Same-actor replay | second success, 2 effects | second reject, 1 effect |
| Issued/cancelled/explicit expired state | success, 1 effect each | reject, 0 effects |
| `now == expires_at` | success, 1 effect | reject, 0 effects |
| Two callers with synchronized overlap | 2 successes, 2 effects | 1 success, 1 effect |

These are observed facts in a local model. Compare them with the contract to
write the invariant/control gap. The probe itself does not declare the system
secure or change the source file. Each CLI selection runs only that scenario.

## Workshop 9, Lab 9.2: AI-assisted learner tests

`tests/test_redemption_contract.py` contains TWO complete positive/control tests
and SIX unfinished tests marked `@unittest.skip`. Use the reviewed contract to ask an AI Agent to draft the missing
assertions, review them yourself, then remove those six skips. Removing skips but leaving
`self.fail` is not a completed test; removing assertions to get green is invalid.

```bash
python3 -m unittest discover -s tests -p 'test_redemption_contract.py' -v
```

Starter: 2 passed / 6 skipped. **`OK (skipped=6)` is not a secure result or a
completed contract.** The completed eight-case contract executes 8 / skips 0:
on the original engine 2 pass and 6 fail; on the secure reference all 8 pass.
Extra meaningful cases may be added; record actual counts if they differ.

Instructor examples live separately in `reference/`. Do not replace your
learner-authored tests with them and call that your authored work. Humans can
inspect the provided example for comparison; the Agent exercise denies reads
of `reference/**` so the supplied repair is not an answer copied by the Agent.

```bash
# PROVIDED instructor contract, frozen baseline/reference/mutants:
python3 reference/check_contract.py
# Test your completed suite against those comparison engines:
python3 reference/check_contract.py --learner-tests tests/test_redemption_contract.py
```

The harness changes the imported service binding in memory only; it never
overwrites `voucher.py`, the tests, or another file. It reports the suite and
working-engine SHA256. The original vulnerable engine is frozen separately so
the quality comparison still works after the working file is repaired.

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

At least eight executed tests, zero skips/errors/expected failures/unexpected
successes, baseline failures, a passing
reference, and detection of every weakened control are required by the quality
check. `@unittest.expectedFailure` cannot mask a failing contract or convert an
unexpected pass into acceptance. A compiler/setup/runtime error does not count
as detecting a mutation.

## Workshop 10: same case, guarded Agent repair

Keep `WORKFLOW_DIR` and your completed tests. Human-provided policy allows edits
to `voucher.py` only; models, helpers, probes, tests and reference stay read-only.
Use the separate Agent-control fixture/adapter with the new voucher allowlist.
The human records the read-only plan and native probe result, reviews the diff,
and runs tests. The supplied fixture does not install a hook automatically.

After the reviewed patch:

```bash
python3 -m unittest discover -s tests -p 'test_redemption_contract.py' -v
python3 probes.py all > after-observations.json
python3 reference/check_contract.py --learner-tests tests/test_redemption_contract.py --expect-working secure
```

The last command additionally requires the **working** implementation to pass
the completed suite. Do not count a passing reference run as the Agent repair.
Do not modify acceptance assertions or the race seam to hide a failure.

The supplied `native-observation.json` and `reference/observed_agent_patch.py`
record a separate INSTRUCTOR validation run: an out-of-scope Write was blocked,
the marker was absent, the Agent edited only the temporary copy's `voucher.py`,
and human verification ran the unchanged provided eight-case contract. These
are observed instructor artifacts, not proof that a learner authored tests or
that the learner's own native runtime has been tested. The original working
`voucher.py` in this teaching folder remains vulnerable. Read the actual record
and hashes when using that observation; do not copy the observed patch into your
exercise and call it your own repair.

## Limits

Each observation checks one-process in-memory state; the race harness isolates
that state in a disposable child process. The reference lock does not prove a
multi-process database transaction, distributed idempotency, authentication,
authorization, durable storage or deployment isolation. The test seam/fixtures
are not production inputs. No network/service or real data was used in the
recorded checks. A file allowlist is not a Python execution sandbox: review an
Agent patch before executing it in the disposable workspace, and do not give
the exercise real credentials or production connections.
