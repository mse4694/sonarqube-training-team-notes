"""Read-only fixture model and ordinary lifecycle operations; no persistence."""
from dataclasses import asdict, dataclass, replace
from threading import Lock

STATES = frozenset({"issued", "active", "cancelled", "expired", "used"})


@dataclass
class Voucher:
    code: str
    expires_at: int
    state: str = "issued"
    redeemed_by: str | None = None


@dataclass(frozen=True)
class Redemption:
    ok: bool
    reason: str
    receipt_id: str | None = None
    actor: str | None = None


class VoucherStore:
    """Shared plumbing, not the redemption policy being repaired."""

    def __init__(self, records=None):
        self._lock = Lock()
        self._records = {}
        self._events = []
        for record in records or []:
            if not record.code or record.state not in STATES:
                raise ValueError("invalid fixture voucher")
            if type(record.expires_at) is not int or record.code in self._records:
                raise ValueError("invalid expiry or duplicate fixture code")
            self._records[record.code] = replace(record)

    def issue(self, code, expires_at):
        with self._lock:
            if not code or code in self._records or type(expires_at) is not int:
                raise ValueError("invalid or duplicate fixture voucher")
            self._records[code] = Voucher(code, expires_at)

    def activate(self, code):
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None or voucher.state != "issued":
                return False
            voucher.state = "active"
            return True

    def cancel(self, code):
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None or voucher.state not in {"issued", "active"}:
                return False
            voucher.state = "cancelled"
            return True

    def snapshot(self, code):
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return None
            result = asdict(voucher)
            result["redemption_count"] = sum(
                event["code"] == code for event in self._events
            )
            return result

    def events(self):
        with self._lock:
            return [dict(event) for event in self._events]

    def _commit_redemption(self, voucher, actor):
        """Caller must hold _lock. Each call creates a NEW business effect."""
        receipt_id = f"receipt-{len(self._events) + 1:04d}"
        voucher.state = "used"
        voucher.redeemed_by = actor
        self._events.append({"code": voucher.code, "actor": actor, "receipt_id": receipt_id})
        return Redemption(True, "redeemed", receipt_id, actor)
