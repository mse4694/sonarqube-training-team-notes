"""Read-only observation helpers. They do not decide whether a result is secure."""
from concurrent.futures import ThreadPoolExecutor
from dataclasses import asdict
import multiprocessing
from threading import Barrier, Lock
from voucher_model import Voucher

NOW = 10
EXPIRES_AT = 100


def active_service(service_type, code="V-ACTIVE"):
    service = service_type()
    service.issue(code, EXPIRES_AT)
    service.activate(code)
    return service


class ObservationHarnessError(RuntimeError):
    """An incomplete observation is inconclusive, never a secure result."""


def _threaded_observation(service_type):
    service = active_service(service_type, "V-RACE")
    barrier = Barrier(2, timeout=2)
    counter_lock = Lock()
    checkpoint_calls = 0

    def checkpoint():
        nonlocal checkpoint_calls
        with counter_lock:
            checkpoint_calls += 1
        barrier.wait()

    def request(actor):
        try:
            return {"actor": actor, "result": asdict(service.redeem(
                "V-RACE", actor, NOW, checkpoint=checkpoint)), "error": None}
        except Exception as error:
            return {"actor": actor, "result": None, "error": type(error).__name__}

    with ThreadPoolExecutor(max_workers=2) as pool:
        requests = list(pool.map(request, ("alice", "bob")))
    return {
        "requests": requests,
        "success_count": sum(bool(row["result"] and row["result"]["ok"]) for row in requests),
        "errors": [row["error"] for row in requests if row["error"]],
        "checkpoint_calls": checkpoint_calls,
        "event_count": len(service.events()),
        "events": service.events(),
        "snapshot": service.snapshot("V-RACE"),
    }


def _observation_child(connection, service_type):
    try:
        connection.send({"observation": _threaded_observation(service_type)})
    except Exception as error:
        connection.send({"harness_error": type(error).__name__})
    finally:
        connection.close()


def concurrent_observation(service_type, *, deadline_seconds=5):
    # A Future timeout cannot stop stuck threads: executor shutdown would wait
    # forever. Own the entire threaded observation in a disposable process.
    if "fork" not in multiprocessing.get_all_start_methods():
        raise ObservationHarnessError("fork_required: use Linux, macOS or WSL")
    context = multiprocessing.get_context("fork")
    receiver, sender = context.Pipe(duplex=False)
    child = context.Process(target=_observation_child, args=(sender, service_type))
    try:
        child.start()
        sender.close()
        if not receiver.poll(deadline_seconds):
            raise ObservationHarnessError("deadline_exceeded: race observation inconclusive")
        try:
            envelope = receiver.recv()
        except EOFError as error:
            raise ObservationHarnessError("child_exited_without_observation") from error
        if "harness_error" in envelope:
            raise ObservationHarnessError("child_error: " + envelope["harness_error"])
        return envelope["observation"]
    finally:
        sender.close()
        receiver.close()
        if child.pid is not None:
            child.join(timeout=0.2)
            if child.is_alive():
                child.terminate()
                child.join(timeout=1)
            if child.is_alive():
                child.kill()
                child.join(timeout=1)
            child.close()


def redemption_observation(service_type, state, now=NOW):
    service = service_type([Voucher("V-STATE", EXPIRES_AT, state=state)])
    result = service.redeem("V-STATE", "alice", now)
    return {"result": asdict(result), "event_count": len(service.events()),
            "snapshot": service.snapshot("V-STATE")}
