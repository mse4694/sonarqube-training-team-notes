"""Voucher redemption policy for the local, single-process fixture."""
from voucher_model import Redemption, Voucher, VoucherStore


class VoucherService(VoucherStore):
    def redeem(self, code, actor, now, checkpoint=None):
        # now is a trusted integer fixture clock, not user-supplied production time.
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            if voucher.state == "used":
                return Redemption(False, "already_used")
            if voucher.state != "active":
                return Redemption(False, "inactive_voucher")
            if now >= voucher.expires_at:
                return Redemption(False, "expired_voucher")

        # TEST-ONLY scheduling seam: both racing calls have checked the old state.
        # Do not delete the seam to make the concurrency test stop exercising overlap.
        if checkpoint is not None:
            checkpoint()

        with self._lock:
            # Recheck after the scheduling seam, then consume and emit the event
            # while holding the same lock so only one caller can succeed.
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            if voucher.state == "used":
                return Redemption(False, "already_used")
            if voucher.state != "active":
                return Redemption(False, "inactive_voucher")
            if now >= voucher.expires_at:
                return Redemption(False, "expired_voucher")
            return self._commit_redemption(voucher, actor)
