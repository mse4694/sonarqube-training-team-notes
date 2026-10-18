"""Manual observations, not a test pass or a security verdict."""
import sys
sys.dont_write_bytecode = True

import argparse
from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import platform
from scenario_support import NOW, EXPIRES_AT, ObservationHarnessError, active_service, concurrent_observation, redemption_observation


def observations(service_type, scenario="all"):
    def positive():
        service = service_type()
        service.issue("V-POSITIVE", EXPIRES_AT)
        issued = service.snapshot("V-POSITIVE")
        activated = service.activate("V-POSITIVE")
        result = service.redeem("V-POSITIVE", "alice", NOW)
        return {"issued": issued, "activated": activated, "result": asdict(result),
                "event_count": len(service.events()), "snapshot": service.snapshot("V-POSITIVE")}

    def unknown():
        service = service_type()
        result = service.redeem("V-MISSING", "alice", NOW)
        return {"result": asdict(result), "event_count": len(service.events())}

    def replay():
        service = active_service(service_type, "V-REPLAY")
        first = service.redeem("V-REPLAY", "alice", NOW)
        second = service.redeem("V-REPLAY", "alice", NOW + 1)
        return {"first": asdict(first), "second": asdict(second),
                "event_count": len(service.events()), "snapshot": service.snapshot("V-REPLAY")}

    def cancel():
        service = active_service(service_type, "V-CANCELLED")
        cancelled = service.cancel("V-CANCELLED")
        result = service.redeem("V-CANCELLED", "alice", NOW)
        return {"cancelled": cancelled, "result": asdict(result),
                "event_count": len(service.events()), "snapshot": service.snapshot("V-CANCELLED")}

    builders = {
        "positive": positive, "unknown": unknown, "replay": replay,
        "state": lambda: {"issued": redemption_observation(service_type, "issued"),
                          "expired": redemption_observation(service_type, "expired")},
        "cancel": cancel,
        "expiry": lambda: redemption_observation(service_type, "active", now=EXPIRES_AT),
        "race": lambda: concurrent_observation(service_type),
    }
    return {name: build() for name, build in builders.items() if scenario in ("all", name)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scenario", nargs="?", default="all",
                        choices=("all", "positive", "unknown", "replay", "state", "cancel", "expiry", "race"))
    parser.add_argument("--implementation", default="working", choices=("working", "reference", "baseline"))
    args = parser.parse_args()
    if args.implementation == "reference":
        from reference.secure_voucher import VoucherService
        engine_path = Path(__file__).parent / "reference/secure_voucher.py"
    elif args.implementation == "baseline":
        from reference.vulnerable_voucher import VoucherService
        engine_path = Path(__file__).parent / "reference/vulnerable_voucher.py"
    else:
        from voucher import VoucherService
        engine_path = Path(__file__).parent / "voucher.py"
    try:
        rows = observations(VoucherService, args.scenario)
    except ObservationHarnessError as error:
        print(json.dumps({"fixture": "voucher-workflow", "implementation": args.implementation,
                          "status": "inconclusive", "harness_error": str(error),
                          "observations": None}, indent=2))
        return 1
    print(json.dumps({"fixture": "voucher-workflow", "implementation": args.implementation,
                      "python_version": platform.python_version(), "clock": "trusted fixture integer",
                      "engine_sha256": hashlib.sha256(engine_path.read_bytes()).hexdigest(),
                      "observations": rows}, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
