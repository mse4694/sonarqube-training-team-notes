"""PROVIDED instructor contract example, not learner-authored work."""
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

    def test_same_actor_replay_is_rejected(self):
        service = active_service(self.SERVICE_TYPE)
        first = service.redeem("V-ACTIVE", "alice", NOW)
        second = service.redeem("V-ACTIVE", "alice", NOW + 1)
        self.assertTrue(first.ok)
        self.assertFalse(second.ok)
        self.assertIsNone(second.receipt_id)
        self.assertEqual(len(service.events()), 1)

    def test_issued_voucher_is_rejected(self):
        service = self.SERVICE_TYPE([Voucher("V-ISSUED", EXPIRES_AT)])
        result = service.redeem("V-ISSUED", "alice", NOW)
        self.assertFalse(result.ok)
        self.assertIsNone(result.receipt_id)
        self.assertEqual(service.snapshot("V-ISSUED")["state"], "issued")
        self.assertEqual(service.events(), [])

    def test_cancelled_voucher_is_rejected(self):
        service = active_service(self.SERVICE_TYPE, "V-CANCELLED")
        self.assertTrue(service.cancel("V-CANCELLED"))
        result = service.redeem("V-CANCELLED", "alice", NOW)
        self.assertFalse(result.ok)
        self.assertEqual(service.snapshot("V-CANCELLED")["state"], "cancelled")
        self.assertEqual(service.events(), [])

    def test_expired_state_is_rejected(self):
        # Explicit expired state matters even when its timestamp is in the future.
        service = self.SERVICE_TYPE([Voucher("V-EXPIRED", EXPIRES_AT, state="expired")])
        result = service.redeem("V-EXPIRED", "alice", NOW)
        self.assertFalse(result.ok)
        self.assertEqual(service.snapshot("V-EXPIRED")["state"], "expired")
        self.assertEqual(service.events(), [])

    def test_expiry_deadline_is_rejected(self):
        service = active_service(self.SERVICE_TYPE)
        result = service.redeem("V-ACTIVE", "alice", EXPIRES_AT)
        self.assertFalse(result.ok)  # boundary is now >= expires_at
        self.assertIsNone(result.receipt_id)
        self.assertEqual(service.events(), [])

    def test_concurrent_redemption_has_one_success(self):
        observed = concurrent_observation(self.SERVICE_TYPE)
        self.assertEqual(observed["checkpoint_calls"], 2, "Do not bypass the scheduling seam")
        self.assertEqual(observed["errors"], [], "A deadlock/barrier error is not a secure result")
        self.assertEqual(observed["success_count"], 1)
        self.assertEqual(observed["event_count"], 1)
        self.assertEqual(observed["snapshot"]["state"], "used")
        winner = next(row for row in observed["requests"] if row["result"]["ok"])
        self.assertEqual(observed["snapshot"]["redeemed_by"], winner["actor"])
        self.assertEqual(observed["events"][0]["actor"], winner["actor"])


if __name__ == "__main__":
    unittest.main()
