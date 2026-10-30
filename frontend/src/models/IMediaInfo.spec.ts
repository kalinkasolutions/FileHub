import { describe, expect, it } from 'vitest';
import { IMediaInfo, mediaMetaLine, mediaTypeLabel, sourceName } from '@models/IMediaInfo';

function info(overrides: Partial<IMediaInfo> = {}): IMediaInfo {
  return {
    id: 'm',
    found: true,
    query: 'Dune Part Two',
    source: 'tmdb',
    mediaType: 'movie',
    title: 'Dune: Part Two',
    year: 2024,
    overview: '',
    rating: 8.2,
    genres: [],
    platforms: [],
    hasPoster: false,
    externalUrl: '',
    fetchedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

describe('mediaMetaLine', () => {
  it('joins year, kind and rating with a middle dot', () => {
    expect(mediaMetaLine(info())).toBe('2024 · Movie · ★ 8.2');
  });

  it('leaves out what the source did not say rather than showing a blank', () => {
    expect(mediaMetaLine(info({ year: null, rating: null, mediaType: 'game' }))).toBe('Game');
    expect(mediaMetaLine(info({ rating: null }))).toBe('2024 · Movie');
  });

  it('always shows one decimal, so 8 and 8.25 read as the same kind of number', () => {
    expect(mediaMetaLine(info({ rating: 8 }))).toBe('2024 · Movie · ★ 8.0');
    expect(mediaMetaLine(info({ rating: 8.25 }))).toBe('2024 · Movie · ★ 8.3');
  });
});

describe('labels', () => {
  it('names the kind and the source in words', () => {
    expect(mediaTypeLabel('tv')).toBe('Series');
    expect(mediaTypeLabel('')).toBe('');
    expect(sourceName('igdb')).toBe('IGDB');
    expect(sourceName('tmdb')).toBe('TMDb');
  });
});
