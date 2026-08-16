import { Component, computed, input, linkedSignal, output } from '@angular/core';

/** One tickable grant: a user on a base path's list, or a base path on a user's. */
export interface IAccessOption {
  id: string;
  label: string;
  /** The second line — an email address, or the path a base path points at. */
  hint: string;
}

/**
 * The grant editor, used from both ends of the same table: "who may see this base path" and
 * "which base paths may this user see". Both routes replace the whole set, so this edits a
 * complete list of ticks and saves all of it — an id left unticked is a revocation, not a no-op.
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
