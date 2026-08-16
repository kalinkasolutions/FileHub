import { describe, expect, it } from 'vitest';
import { FileService, downloadName, encodeRelativePath } from '@services/file.service';

describe('encodeRelativePath', () => {
  it('leaves an ordinary path alone', () => {
    expect(encodeRelativePath('music/albums/track.flac')).toBe('music/albums/track.flac');
  });

  // The route's catch-all segment has to keep its separators: encoding the path in one go would
  // turn them into %2F, which never reaches routing.
  it('keeps the separators as separators', () => {
    expect(encodeRelativePath('a/b/c')).toBe('a/b/c');
  });

  it('encodes what would otherwise end the path', () => {
    expect(encodeRelativePath('mix #1/what?.mp3')).toBe('mix%20%231/what%3F.mp3');
  });

  it('answers empty for the base path itself', () => {
    expect(encodeRelativePath('')).toBe('');
  });
});

describe('downloadName', () => {
  it('leaves a file name as it is', () => {
    expect(downloadName('track.flac', false)).toBe('track.flac');
  });

  // A directory arrives as a zip built on the fly, and the anchor's download attribute overrides
  // the server's own name — so the extension has to be added on this side.
  it('adds .zip to a directory', () => {
    expect(downloadName('albums', true)).toBe('albums.zip');
  });
});

describe('FileService.downloadUrl', () => {
  const service = new FileService();
  const id = '3f2504e0-4f89-11d3-9a0c-0305e82c3301';

  it('addresses an entry by base path and relative path', () => {
    expect(service.downloadUrl(id, 'music/track.flac')).toBe(
      `/api/files/download/${id}/music/track.flac`,
    );
  });

  it('leaves a trailing slash for the base path itself', () => {
    expect(service.downloadUrl(id, '')).toBe(`/api/files/download/${id}/`);
  });
});
