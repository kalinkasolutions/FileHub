/**
 * Bytes as a short human string, in decimal units — the unit a disk is sold in and the one every
 * file manager shows, so 1 kB is 1000 bytes here and not 1024.
 */
export function formatBytes(bytes: number): string {
  if (bytes < 1000) {
    return `${bytes} B`;
  }

  const units = ['kB', 'MB', 'GB', 'TB', 'PB'];
  let value = bytes / 1000;
  let unit = 0;

  while (value >= 1000 && unit < units.length - 1) {
    value = value / 1000;
    unit = unit + 1;
  }

  // One decimal below 10 and none above it: "9.4 MB" is worth the digit, "412.7 MB" is not.
  const rounded = value < 10 ? value.toFixed(1) : Math.round(value).toString();

  return `${rounded} ${units[unit]}`;
}

/**
 * What the size column shows for one listing row. A directory's `size` is **the number of entries
 * it holds**, not a byte count — the API measures it that way, so it has to be labelled that way.
 */
export function formatEntrySize(isDir: boolean, size: number): string {
  if (!isDir) {
    return formatBytes(size);
  }

  return size === 1 ? '1 item' : `${size} items`;
}

/** The download counter on a share link. `0` as a maximum means unlimited. */
export function formatDownloads(downloadCount: number, maxDownloadCount: number): string {
  if (maxDownloadCount === 0) {
    return `${downloadCount} downloads`;
  }

  return `${downloadCount} / ${maxDownloadCount} downloads`;
}
