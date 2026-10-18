Implemented the approved plan in [voucher.py](/tmp/sonar-learner89-20261018/voucher.py): require active, unexpired, unused; preserve the public API and checkpoint outside the lock; recheck, consume, and emit the event atomically under one lock.

Frozen test SHA256 matches. No tests run; native control untested. Status: `manual_review/native_not_tested`.