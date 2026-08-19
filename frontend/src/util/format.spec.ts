import { describe, expect, it } from 'vitest';
import { formatBytes, formatDownloads, formatEntrySize } from '@util/format';

describe('formatBytes', () => {
  it('leaves anything under a kilobyte in bytes', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(999)).toBe('999 B');
  });

  it('steps up a decimal unit at a time', () => {
    expect(formatBytes(1000)).toBe('1.0 kB');
    expect(formatBytes(1_500_000)).toBe('1.5 MB');
    expect(formatBytes(2_400_000_000)).toBe('2.4 GB');
    expect(formatBytes(3_000_000_000_000)).toBe('3.0 TB');
  });

  it('drops the decimal once the number is big enough not to need it', () => {
    expect(formatBytes(412_700_000)).toBe('413 MB');
    expect(formatBytes(9_940_000)).toBe('9.9 MB');
  });

  it('stops at petabytes rather than running off the unit list', () => {
    expect(formatBytes(5_000_000_000_000_000_000)).toBe('5000 PB');
  });
});

describe('formatEntrySize', () => {
  it('measures a file in bytes', () => {
    expect(formatEntrySize(false, 2048)).toBe('2.0 kB');
  });

  // The API sends a directory's entry count in the same field, so labelling it as bytes would be
  // wrong by a factor of anything at all.
  it('measures a directory in items', () => {
    expect(formatEntrySize(true, 0)).toBe('0 items');
    expect(formatEntrySize(true, 1)).toBe('1 item');
    expect(formatEntrySize(true, 42)).toBe('42 items');
  });
});

describe('formatDownloads', () => {
  it('reads 0 as unlimited', () => {
    expect(formatDownloads(3, 0)).toBe('3 downloads');
  });

  it('shows the cap when there is one', () => {
    expect(formatDownloads(3, 10)).toBe('3 / 10 downloads');
  });
});
