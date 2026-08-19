import { IFileEntry } from './IFileEntry';

/** The answer to `POST /api/files/navigate` — one directory's listing. */
export interface INavigation {
  /** Display name of the directory that was navigated into. */
  navigationName: string;

  entries: IFileEntry[];
}
