import { describe, expect, it } from 'vitest';
import { IAdminShare, downloadsLabel, formatSize, shareLocation } from '@models/IAdminShare';

function share(overrides: Partial<IAdminShare> = {}): IAdminShare {
  return {
    id: 's',
    name: 'holiday.zip',
    basePathId: 'b',
    basePathName: 'Media',
    relativePath: 'photos/holiday.zip',
    isDir: false,
    size: 0,
    downloadCount: 0,
    maxDownloadCount: 0,
    createdAt: '2026-01-01T00:00:00Z',
    createdById: 'u',
    createdBy: 'Ada',
    link: 'https://files.example.com/share/s',
    ...overrides,
  };
}

describe('shareLocation', () => {
  it('joins the base path name and the relative path', () => {
    expect(shareLocation(share())).toBe('Media/photos/holiday.zip');
  });

  it('is the base path itself when the link points at its root', () => {
    expect(shareLocation(share({ relativePath: '' }))).toBe('Media');
  });
});

describe('downloadsLabel', () => {
  it('counts downloads for an unlimited link', () => {
    expect(downloadsLabel(share({ downloadCount: 0 }))).toBe('0 downloads');
    expect(downloadsLabel(share({ downloadCount: 1 }))).toBe('1 download');
    expect(downloadsLabel(share({ downloadCount: 4 }))).toBe('4 downloads');
  });

  it('shows the limit when there is one, since reaching it kills the link', () => {
    expect(downloadsLabel(share({ downloadCount: 3, maxDownloadCount: 5 }))).toBe(
      '3 of 5 downloads',
    );
  });
});

describe('formatSize', () => {
  it('steps through decimal units', () => {
    expect(formatSize(512)).toBe('512 B');
    expect(formatSize(1500)).toBe('1.5 kB');
    expect(formatSize(20_000)).toBe('20 kB');
    expect(formatSize(3_400_000)).toBe('3.4 MB');
    expect(formatSize(2_000_000_000)).toBe('2.0 GB');
  });

  it('has nothing to say about an unmeasured size', () => {
    expect(formatSize(0)).toBe('—');
    expect(formatSize(-1)).toBe('—');
  });
});
