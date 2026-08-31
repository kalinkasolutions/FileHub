/**
 * One line of the server's log. `GET /api/admin/logs`.
 *
 * The sink's structured `Properties` are deliberately not on the wire: they repeat what the
 * rendered message already says, and they carry every argument a call site passed.
 */
export interface ILogEntry {
  /**
   * The sink's row id, and the tail cursor — the screen sends the highest one it holds back as
   * `afterId` to ask for only what has arrived since. Never compare these across filters.
   */
  id: number;
  /** UTC, ISO-8601. Rendered in the viewer's own zone. */
  timestamp: string;
  /** A Serilog level name — see `logLevels`. */
  level: string;
  message: string;
  /** The exception's full text, when the entry carried one. */
  exception: string | null;
}

/** One page of the log, newest first. */
export interface ILogPage {
  entries: ILogEntry[];
  /** How many entries match the filter, ignoring paging. */
  totalCount: number;
  /** True when the page is a window onto something larger. */
  hasMore: boolean;
}

/** What the log screen asks for. Every field is optional. */
export interface ILogQuery {
  /** The *lowest* level to include, so "Warning" answers warnings, errors and fatals. */
  minLevel?: string;
  search?: string;
  /** ISO-8601. Inclusive. */
  from?: string;
  /** ISO-8601. Inclusive. */
  to?: string;
  /** Only entries newer than this id — how the screen tails the log. */
  afterId?: number;
  take?: number;
}

/**
 * The Serilog level names, least severe first. Mirrors `Shared.LogLevels` on the server; the level
 * list is also served by `GET /api/admin/logs/levels`, but the picker needs an order to draw before
 * any request comes back and these six are fixed.
 *
 * Note the Serilog spelling and not the Microsoft one: `Verbose`, not Trace; `Fatal`, not Critical.
 */
export const logLevels = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'] as const;

/**
 * Which of the five colour treatments a row wears. Grouped rather than one class per level because
 * the eye only needs three answers on a log screen — routine, worth a look, and wrong — and Verbose
 * and Debug are the same kind of routine.
 */
export function levelClass(level: string): string {
  switch (level) {
    case 'Fatal':
    case 'Error':
      return 'error';
    case 'Warning':
      return 'warning';
    case 'Information':
      return 'info';
    default:
      return 'quiet';
  }
}
