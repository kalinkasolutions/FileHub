import { describe, expect, it } from 'vitest';
import {
  IAdminShare,
  audienceLabel,
  downloadsLabel,
  isRestricted,
  shareLocation,
} from '@models/IAdminShare';

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
    audienceGroupId: null,
    audienceGroupName: '',
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

describe('audienceLabel', () => {
  it('says a link with no audience answers anyone, because that is the whole risk of it', () => {
    expect(audienceLabel(share())).toBe('Anyone with the link');
    expect(isRestricted(share())).toBe(false);
  });

  it('names the group a restricted link answers', () => {
    const restricted = share({ audienceGroupId: 'g', audienceGroupName: 'Family' });

    expect(audienceLabel(restricted)).toBe('Only Family');
    expect(isRestricted(restricted)).toBe(true);
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
