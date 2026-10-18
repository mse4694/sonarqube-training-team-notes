Lab 10.2, same WORKFLOW_DIR and FINAL frozen learner suite from Lab 9.
Read VoucherControlContract.md, voucher.py, voucher_model.py,
scenario_support.py and tests/test_redemption_contract.py (SHA256 below).
Human approved the earlier plan: require active/unexpired/unused; keep
checkpoint outside lock; recheck+consume+event atomically under one lock.
Implement that plan in voucher.py ONLY. Preserve public API and checkpoint.
Do not read reference/, change tests/model/helpers/contract, or run tests.
Human will check hashes, actual diff and execute the unchanged suite.
Native control was not tested; record manual_review/native_not_tested.
Return concise plan confirmation, changed file and untested scope.
Frozen tests SHA256: 62ba5ba228726dc75f2e916c71fe538e13e4214efe4d8b3540d40ab86b8574fe
