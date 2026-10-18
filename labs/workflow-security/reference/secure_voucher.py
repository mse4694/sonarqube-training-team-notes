"""Instructor reference: validate and consume in the same critical section."""
from voucher_model import Redemption, VoucherStore


class VoucherService(VoucherStore):
    def _failure_reason(self, voucher, actor, now):
        if voucher.state == "used":
            return "already_used"
        if voucher.state != "active":
            return "invalid_state"
        if now >= voucher.expires_at:
            return "expired"
        return None

    def redeem(self, code, actor, now, checkpoint=None):
        # Test synchronization must not wait for the second caller while holding
        # the lock. The definitive state check follows this seam under the lock.
        if checkpoint is not None:
            checkpoint()
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            reason = self._failure_reason(voucher, actor, now)
            if reason is not None:
                if reason == "expired":
                    voucher.state = "expired"
                return Redemption(False, reason)
            return self._commit_redemption(voucher, actor)
