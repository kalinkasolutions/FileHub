/**
 * A byte count as the share page says it. Decimal units, because that is what the disks these files
 * come off are sold in, and because a share link is read by whoever was sent it rather than by an
 * administrator.
 *
 * Lives here rather than in `@util/filesize` because the public page is the one screen an anonymous
 * visitor sees: `0` is a real, sayable size for an empty file, not a blank.
 */
export function formatSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return 'unknown size';
  }

  const units = ['bytes', 'KB', 'MB', 'GB', 'TB', 'PB'];

  let value = bytes;
  let unit = 0;
  while (value >= 1000 && unit < units.length - 1) {
    value /= 1000;
    unit++;
  }

  // Whole bytes are counted, not measured — 512 bytes, not 512.00 bytes.
  if (unit === 0) {
    return `${Math.round(value)} ${units[unit]}`;
  }

  // Two decimals below 10 (1.25 MB), one above it (12.5 MB): the digits stay roughly as precise
  // as they are useful.
  return `${value.toFixed(value < 10 ? 2 : 1)} ${units[unit]}`;
}
