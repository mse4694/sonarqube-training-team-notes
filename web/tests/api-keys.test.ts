import { describe, expect, it } from 'vitest';
import { maskKey } from '../lib/api-keys';

describe('maskKey', () => {
  it('keeps the public prefix and hides the rest', () => {
    expect(maskKey('tnk_AbCdEfGh')).toBe('tnk_AbCdEfGh••••••••');
  });
});
