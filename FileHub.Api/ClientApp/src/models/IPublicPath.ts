/**
 * @deprecated The pre-rewrite listing row, in the Go build's PascalCase and with an int base-path
 * id. The browsing screens are on {@link IFileEntry} now; this only survives for the header, which
 * is still the pre-rewrite one and reads a breadcrumb off `PathService`. Delete it — and the two
 * legacy members on `PathService` — with that header.
 */
export interface IPublicPath {
  Id: number;
  Name: string;
  IsDir: boolean;
  Size: number;
  NextSegment: string;
  IsBasePath: boolean;
  ItemId: string;
}
