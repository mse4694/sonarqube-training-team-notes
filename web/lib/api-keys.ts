// Pure helpers for personal API keys (unit-tested in tests/api-keys.test.ts).

/** "tnk_AbCdEfGh" -> "tnk_AbCdEfGh••••••••" : show only the non-secret prefix. */
export function maskKey(prefix: string): string {
  return `${prefix}${'•'.repeat(8)}`;
}
