/**
 * What TMDb or IGDB knows about one folder (`MediaInfoDto`). Cached on the server: the first open
 * asks the source, every later one reads the row, so none of this is live.
 */
export interface IMediaInfo {
  id: string;

  /** False when the source was asked and had nothing; `query` then says what it was asked for. */
  found: boolean;

  /** The title the folder name was reduced to before searching. */
  query: string;

  source: 'tmdb' | 'igdb';

  mediaType: 'movie' | 'tv' | 'game' | '';

  title: string;

  year: number | null;

  overview: string;

  /** 0–10, or null when the source has no votes for it. */
  rating: number | null;

  genres: string[];

  /** Games only. */
  platforms: string[];

  /** The poster is at `api/media/poster/{id}` when this is true. */
  hasPoster: boolean;

  /** The title's own page at the source. */
  externalUrl: string;

  fetchedAt: string;
}

/** What the folder is addressed by: the same pair a download or a share uses. */
export interface IMediaInfoTarget {
  basePathId: string;
  relativePath: string;
  /** The folder's name, for the popover's heading while the answer is on its way. */
  name: string;
}

/** What to call the source in a sentence. */
export function sourceName(source: IMediaInfo['source']): string {
  return source === 'igdb' ? 'IGDB' : 'TMDb';
}

/** "Movie", "Series" or "Game" — a word for the kind of thing it is, or empty. */
export function mediaTypeLabel(mediaType: IMediaInfo['mediaType']): string {
  switch (mediaType) {
    case 'movie':
      return 'Movie';
    case 'tv':
      return 'Series';
    case 'game':
      return 'Game';
    default:
      return '';
  }
}

/**
 * The line under the title: year, kind and rating, with whatever is missing left out rather than
 * shown as a blank — "2024 · Movie · ★ 8.2", or just "Game" for a title nobody has rated yet.
 */
export function mediaMetaLine(info: IMediaInfo): string {
  const parts = [
    info.year === null ? '' : String(info.year),
    mediaTypeLabel(info.mediaType),
    info.rating === null ? '' : `★ ${info.rating.toFixed(1)}`,
  ];

  return parts.filter((part) => part.length > 0).join(' · ');
}
