import { Component, computed, input, linkedSignal, output } from '@angular/core';

/** One tickable grant: a user on a base path's list, or a base path on a user's. */
export interface IAccessOption {
  id: string;
  label: string;
  /** The second line — an email address, or the path a base path points at. */
  hint: string;
}

/**
 * The grant editor. Five screens use it, because the access model is three tables edited from both
 * ends: who may see this base path, which base paths may this user see, which groups may see this
 * base path, who is in this group, and what does this group grant. Every one of those routes
 * replaces the whole set, so this edits a complete list of ticks and saves all of it — an id left
 * unticked is a revocation, not a no-op.
 *
 * The draft is a `linkedSignal` on the input: reopening the editor on another row re-seeds it,
 * so the component can be reused for the next row without being torn down.
 */
@Component({
  selector: 'admin-access-list',
  standalone: true,
  templateUrl: 'access-list.component.html',
  styleUrl: 'access-list.component.scss',
})
export class AccessListComponent {
  public readonly heading = input.required<string>();
  public readonly options = input.required<readonly IAccessOption[]>();
  /** The ids currently granted, as the server has them. */
  public readonly granted = input.required<readonly string[]>();
  public readonly emptyText = input('There is nothing to grant yet.');
  /**
   * Whether the options have actually been read yet. The options are a *second* list on every one
   * of these screens — the accounts on a base path's editor, the base paths on a user's — and the
   * loading overlay is raised for writes only, so an ungated empty message tells an admin to go
   * and create something the installation already has while its list is still on the wire.
   */
  public readonly optionsLoaded = input(true);
  /** What unticking something costs — a revocation takes share links with it, always. */
  public readonly note = input('');
  public readonly busy = input(false);

  public readonly save = output<string[]>();
  public readonly cancel = output<void>();

  public readonly draft = linkedSignal<readonly string[], string[]>({
    source: this.granted,
    computation: (granted) => [...granted],
  });

  public readonly count = computed(() => `${this.draft().length} of ${this.options().length}`);

  public isChecked(id: string): boolean {
    return this.draft().includes(id);
  }

  public toggle(id: string, checked: boolean): void {
    if (checked) {
      this.draft.update((ids) => (ids.includes(id) ? ids : [...ids, id]));
      return;
    }

    this.draft.update((ids) => ids.filter((current) => current !== id));
  }

  public onSave(): void {
    this.save.emit(this.draft());
  }
}

/**
 * The ids that were granted and are not in the draft. Every one of these five editors saves a whole
 * list, so this is what tells a caller whether the save is a revocation — and a revocation deletes
 * share links, which is worth asking about before it happens rather than reporting afterwards.
 */
export function revokedIds(granted: readonly string[], draft: readonly string[]): string[] {
  return granted.filter((id) => !draft.includes(id));
}
