"""Compare a contract against frozen baseline, secure reference and weakened controls."""
import sys
sys.dont_write_bytecode = True

import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import types
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from voucher_model import Voucher, Redemption
from voucher import VoucherService as Working
from reference.vulnerable_voucher import VoucherService as Baseline
from reference.secure_voucher import VoucherService as Secure
from reference.mutations import MUTANTS


def evaluate(path, service_type):
    # Only the import binding changes; no file is copied over or rewritten.
    original = sys.modules.get("voucher")
    shim = types.ModuleType("voucher")
    shim.VoucherService, shim.Voucher, shim.Redemption = service_type, Voucher, Redemption
    sys.modules["voucher"] = shim
    try:
        spec = importlib.util.spec_from_file_location("selected_redemption_contract", path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        suite = unittest.defaultTestLoader.loadTestsFromModule(module)
        stream = io.StringIO()
        result = unittest.TextTestRunner(stream=stream, verbosity=2).run(suite)
        return {
            "discovered": result.testsRun,
            "executed": result.testsRun - len(result.skipped),
            "passed": (result.testsRun - len(result.skipped) - len(result.failures) - len(result.errors)
                       - len(result.expectedFailures) - len(result.unexpectedSuccesses)),
            "failed": len(result.failures), "errors": len(result.errors), "skipped": len(result.skipped),
            "expected_failures": len(result.expectedFailures),
            "unexpected_successes": len(result.unexpectedSuccesses),
            "failure_names": [case.id().split(".")[-1] for case, _ in result.failures],
            "error_names": [case.id().split(".")[-1] for case, _ in result.errors],
            "skip_names": [case.id().split(".")[-1] for case, _ in result.skipped],
            "expected_failure_names": [case.id().split(".")[-1] for case, _ in result.expectedFailures],
            "unexpected_success_names": [case.id().split(".")[-1] for case in result.unexpectedSuccesses],
        }
    finally:
        if original is None:
            sys.modules.pop("voucher", None)
        else:
            sys.modules["voucher"] = original


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--learner-tests", type=Path)
    parser.add_argument("--expect-working", choices=("vulnerable", "secure", "any"), default="any")
    args = parser.parse_args()
    path = (args.learner_tests or ROOT / "reference/contract_tests.py").resolve()
    if not path.is_file() or not path.is_relative_to(ROOT):
        parser.error("select an existing contract file inside this fixture")
    engines = {"baseline": Baseline, "reference": Secure, **MUTANTS, "working": Working}
    rows = {name: evaluate(path, engine) for name, engine in engines.items()}
    complete = all(row["executed"] >= 8 and row["skipped"] == 0 and row["errors"] == 0
                   and row["expected_failures"] == 0 and row["unexpected_successes"] == 0
                   for row in rows.values())
    quality = (complete and rows["baseline"]["failed"] >= 6
               and rows["reference"]["failed"] == 0
               and all(rows[name]["failed"] >= 1 for name in MUTANTS))
    expected = args.expect_working
    working_ok = (rows["working"]["expected_failures"] == 0
                  and rows["working"]["unexpected_successes"] == 0 and
                  (expected == "any" or
                  (expected == "secure" and rows["working"]["failed"] == 0) or
                  (expected == "vulnerable" and rows["working"]["failed"] >= 6)))
    print(json.dumps({
        "suite_kind": "learner" if args.learner_tests else "provided_instructor_reference",
        "contract_sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "working_engine_sha256": hashlib.sha256((ROOT / "voucher.py").read_bytes()).hexdigest(),
        "quality_passed": quality, "working_expectation": expected,
        "working_expectation_met": working_ok, "overall_passed": quality and working_ok,
        "results": rows,
        "limits": "in-memory contract only; no HTTP, authentication, persistence or deployment test",
    }, indent=2))
    return 0 if quality and working_ok else 1


if __name__ == "__main__":
    sys.exit(main())
