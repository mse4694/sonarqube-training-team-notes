"""One deliberately weakened control per mutant; never applied to the work file."""
from voucher_model import Redemption
from reference.secure_voucher import VoucherService as Secure


class MissingStateGuard(Secure):
    def _failure_reason(self, voucher, actor, now):
        if voucher.state == "used":
            return "already_used"
        if now >= voucher.expires_at:
            return "expired"
        return None


class MissingExpiryGuard(Secure):
    def _failure_reason(self, voucher, actor, now):
        if voucher.state == "used":
            return "already_used"
        if voucher.state != "active":
            return "invalid_state"
        return None


class ReplayAllowed(Secure):
    def _failure_reason(self, voucher, actor, now):
        if voucher.state == "used" and voucher.redeemed_by == actor:
            return None
        return super()._failure_reason(voucher, actor, now)


class NonAtomicTransition(Secure):
    def redeem(self, code, actor, now, checkpoint=None):
        with self._lock:
            voucher = self._records.get(code)
            if voucher is None:
                return Redemption(False, "unknown_voucher")
            reason = self._failure_reason(voucher, actor, now)
            if reason is not None:
                return Redemption(False, reason)
        if checkpoint is not None:
            checkpoint()
        with self._lock:
            return self._commit_redemption(voucher, actor)


class SkipsCheckpoint(Secure):
    def redeem(self, code, actor, now, checkpoint=None):
        return super().redeem(code, actor, now, checkpoint=None)


MUTANTS = {
    "missing_state_guard": MissingStateGuard,
    "missing_expiry_guard": MissingExpiryGuard,
    "replay_allowed": ReplayAllowed,
    "non_atomic_transition": NonAtomicTransition,
    "skips_checkpoint": SkipsCheckpoint,
}
