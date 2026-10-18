"""Learner starter: TWO assertions-based tests and SIX explicitly unfinished tests."""
import sys
sys.dont_write_bytecode = True

import unittest
from voucher import VoucherService
from voucher_model import Voucher
from scenario_support import NOW, EXPIRES_AT, active_service, concurrent_observation


class RedemptionContract(unittest.TestCase):
    SERVICE_TYPE = VoucherService

    def test_valid_active_voucher_succeeds_once(self):
        service = self.SERVICE_TYPE()
        service.issue("V-VALID", EXPIRES_AT)
        self.assertEqual(service.snapshot("V-VALID")["state"], "issued")
        self.assertTrue(service.activate("V-VALID"))
        result = service.redeem("V-VALID", "alice", NOW)
        self.assertTrue(result.ok)
        self.assertIsNotNone(result.receipt_id)
        self.assertEqual(service.snapshot("V-VALID")["state"], "used")
        self.assertEqual(service.snapshot("V-VALID")["redeemed_by"], "alice")
        self.assertEqual(len(service.events()), 1)

    def test_unknown_voucher_is_rejected(self):
        service = self.SERVICE_TYPE()
        result = service.redeem("V-UNKNOWN", "alice", NOW)
        self.assertFalse(result.ok)
        self.assertEqual(result.reason, "unknown_voucher")
        self.assertIsNone(result.receipt_id)
        self.assertEqual(service.events(), [])

    @unittest.skip("TODO: learner must implement replay assertions and remove this skip")
    def test_same_actor_replay_is_rejected(self):
        # Redeem once, repeat with the SAME actor, inspect receipt and event_count.
        self.fail("not implemented")

    @unittest.skip("TODO: learner must implement issued-state assertions and remove this skip")
    def test_issued_voucher_is_rejected(self):
        # Issued is not active; no new receipt/effect should occur.
        self.fail("not implemented")

    @unittest.skip("TODO: learner must implement cancellation assertions and remove this skip")
    def test_cancelled_voucher_is_rejected(self):
        # Activate then cancel before attempting redemption.
        self.fail("not implemented")

    @unittest.skip("TODO: learner must implement expired-state assertions and remove this skip")
    def test_expired_state_is_rejected(self):
        # A seeded expired state with future timestamp must still be rejected.
        self.fail("not implemented")

    @unittest.skip("TODO: learner must implement clock-boundary assertions and remove this skip")
    def test_expiry_deadline_is_rejected(self):
        # Use now == EXPIRES_AT, not sleep or the real clock.
        self.fail("not implemented")

    @unittest.skip("TODO: learner must implement atomicity assertions and remove this skip")
    def test_concurrent_redemption_has_one_success(self):
        # concurrent_observation exposes both callers, checkpoint_calls, errors,
        # events and final state. Assert one effect, not just state == 'used'.
        self.fail("not implemented")


if __name__ == "__main__":
    unittest.main()
