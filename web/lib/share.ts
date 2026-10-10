// Pure helpers for share links (unit-tested in tests/share.test.ts).

export type Share = {
  id: number;
  token: string;
  url: string;
  created_at: string;
  expires_at: string | null;
  revoked_at: string | null;
};

export type ShareStatus = 'active' | 'expired' | 'revoked';

export function shareStatus(share: Pick<Share, 'expires_at' | 'revoked_at'>, now: Date = new Date()): ShareStatus {
  if (share.revoked_at) return 'revoked';
  if (share.expires_at && new Date(share.expires_at).getTime() <= now.getTime()) return 'expired';
  return 'active';
}

/** "Expires 12 Oct 2026, 14:05" / "Never expires" (UTC, so server and browser agree). */
export function expiryLabel(expiresAt: string | null): string {
  if (!expiresAt) return 'Never expires';
  const d = new Date(expiresAt);
  if (Number.isNaN(d.getTime())) return 'Never expires';
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  const hh = String(d.getUTCHours()).padStart(2, '0');
  const mm = String(d.getUTCMinutes()).padStart(2, '0');
  return `Expires ${d.getUTCDate()} ${months[d.getUTCMonth()]} ${d.getUTCFullYear()}, ${hh}:${mm} UTC`;
}
