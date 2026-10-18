"""Frozen original engine for test-quality comparisons after voucher.py is patched."""
from voucher_model import Redemption, VoucherStore


class VoucherService(VoucherStore):
    def redeem(self, code, actor, now, checkpoint=None):
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            if voucher.state == "used" and voucher.redeemed_by != actor:
                return Redemption(False, "already_used")
        if checkpoint is not None:
            checkpoint()
        with self._lock:
            return self._commit_redemption(voucher, actor)
