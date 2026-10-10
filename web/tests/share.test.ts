import { describe, expect, it } from 'vitest';
import { expiryLabel, shareStatus } from '../lib/share';

const now = new Date('2026-10-10T12:00:00Z');

describe('shareStatus', () => {
  it('is active without expiry or revocation', () => {
    expect(shareStatus({ expires_at: null, revoked_at: null }, now)).toBe('active');
  });

  it('is expired once expires_at has passed', () => {
    expect(shareStatus({ expires_at: '2026-10-10T11:59:00Z', revoked_at: null }, now)).toBe('expired');
  });

  it('revocation wins over expiry', () => {
    expect(shareStatus({ expires_at: '2026-10-11T00:00:00Z', revoked_at: '2026-10-10T10:00:00Z' }, now)).toBe('revoked');
  });
});

describe('expiryLabel', () => {
  it('says "Never expires" for links without an expiry', () => {
    expect(expiryLabel(null)).toBe('Never expires');
  });

  it('formats the expiry in UTC', () => {
    expect(expiryLabel('2026-10-11T09:05:00Z')).toBe('Expires 11 Oct 2026, 09:05 UTC');
  });
});
