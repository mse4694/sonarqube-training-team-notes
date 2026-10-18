"""Intentionally vulnerable training engine. Only this file is an AI edit target."""
from voucher_model import Redemption, Voucher, VoucherStore


class VoucherService(VoucherStore):
    def redeem(self, code, actor, now, checkpoint=None):
        # now is a trusted integer fixture clock, not user-supplied production time.
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            rejection = self._rejection(voucher, now)
            if rejection is not None:
                return rejection

        # TEST-ONLY scheduling seam: both racing calls have checked the old state.
        # Do not delete the seam to make the concurrency test stop exercising overlap.
        if checkpoint is not None:
            checkpoint()

        with self._lock:
            # Re-check under the same lock that commits, so check and transition are atomic.
            rejection = self._rejection(voucher, now)
            if rejection is not None:
                return rejection
            return self._commit_redemption(voucher, actor)

    @staticmethod
    def _rejection(voucher, now):
        """Caller must hold _lock. Returns a failed Redemption, or None if redeemable."""
        if voucher.state == "used":
            return Redemption(False, "already_used")
        if voucher.state != "active":
            return Redemption(False, f"not_redeemable_{voucher.state}")
        if now >= voucher.expires_at:
            return Redemption(False, "expired")
        return None
