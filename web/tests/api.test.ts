import { describe, expect, it } from 'vitest';
import { truncate } from '../lib/api';

describe('truncate', () => {
  it('leaves short text untouched', () => {
    expect(truncate('hello', 10)).toBe('hello');
  });

  it('cuts long text and adds an ellipsis', () => {
    expect(truncate('abcdefghij', 5)).toBe('abcde…');
  });
});
