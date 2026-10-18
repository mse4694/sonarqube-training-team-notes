"""Intentionally vulnerable training engine. Only this file is an AI edit target."""
from voucher_model import Redemption, Voucher, VoucherStore


class VoucherService(VoucherStore):
    def redeem(self, code, actor, now, checkpoint=None):
        # now is a trusted integer fixture clock, not user-supplied production time.
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            # Intentional gaps: same-actor replay, inactive states and expiry.
            if voucher.state == "used" and voucher.redeemed_by != actor:
                return Redemption(False, "already_used")

        # TEST-ONLY scheduling seam: both racing calls have checked the old state.
        # Do not delete the seam to make the concurrency test stop exercising overlap.
        if checkpoint is not None:
            checkpoint()

        with self._lock:
            # A write lock alone is not an atomic check-and-transition.
            return self._commit_redemption(voucher, actor)
