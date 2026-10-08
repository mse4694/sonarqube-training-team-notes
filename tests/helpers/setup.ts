// Test-only environment (values are not secrets).
process.env.SESSION_SECRET ??= 'test-session-secret-0123456789abcdef';
process.env.REMEMBER_ME_SECRET ??= 'test-remember-secret-0123456789abcdef';
