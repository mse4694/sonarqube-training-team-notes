// Pure helpers for the audit log page (unit-tested in tests/audit.test.ts).

/** ISO timestamp -> "2026-10-10 14:05:09" in UTC (stable on server and browser). */
export function formatUtc(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toISOString().replace('T', ' ').slice(0, 19);
}
