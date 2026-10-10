import { describe, expect, it } from 'vitest';
import { formatUtc } from '../lib/audit';

describe('formatUtc', () => {
  it('formats an ISO timestamp as UTC date + time', () => {
    expect(formatUtc('2026-10-10T14:05:09.123Z')).toBe('2026-10-10 14:05:09');
  });

  it('returns the input when it is not a date', () => {
    expect(formatUtc('not-a-date')).toBe('not-a-date');
  });
});
