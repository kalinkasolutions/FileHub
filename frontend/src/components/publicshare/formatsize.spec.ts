import { describe, expect, it } from 'vitest';
import { formatSize } from './formatsize';

describe('formatSize', () => {
  it('counts whole bytes below a kilobyte', () => {
    expect(formatSize(0)).toBe('0 bytes');
    expect(formatSize(1)).toBe('1 bytes');
    expect(formatSize(999)).toBe('999 bytes');
  });

  it('steps up a unit at every thousand', () => {
    expect(formatSize(1000)).toBe('1.00 KB');
    expect(formatSize(1_500_000)).toBe('1.50 MB');
    expect(formatSize(2_000_000_000)).toBe('2.00 GB');
    expect(formatSize(3_000_000_000_000)).toBe('3.00 TB');
  });

  it('drops a decimal once the number is big enough to carry the precision', () => {
    expect(formatSize(12_500_000)).toBe('12.5 MB');
    expect(formatSize(999_000_000)).toBe('999.0 MB');
  });

  it('stops at the largest unit it knows rather than inventing one', () => {
    expect(formatSize(5e15)).toBe('5.00 PB');
    expect(formatSize(5e18)).toBe('5000.0 PB');
  });

  it('says so rather than printing nonsense for a size it cannot read', () => {
    expect(formatSize(-1)).toBe('unknown size');
    expect(formatSize(Number.NaN)).toBe('unknown size');
  });
});
