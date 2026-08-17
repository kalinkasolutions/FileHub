import { Component, inject } from '@angular/core';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle,
} from '@angular/material/dialog';
import { Observable, map } from 'rxjs';

/**
 * What the dialog says. `message` is the consequence, not the question — every destructive action
 * in the admin area takes something else with it (a base path revokes its links, a user takes
 * their links with them), and that is the part the admin needs before answering.
 */
export interface IConfirm {
  title: string;
  message: string;
  /** Label of the destructive button, e.g. "Delete base path". */
  confirm: string;
}

@Component({
  selector: 'admin-confirm-dialog',
  standalone: true,
  imports: [MatDialogTitle, MatDialogContent, MatDialogActions, MatDialogClose],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>{{ data.message }}</mat-dialog-content>
    <!-- Material justifies these to the end already, so the deprecated align input is not needed. -->
    <mat-dialog-actions>
      <button class="secondary" type="button" mat-dialog-close>Cancel</button>
      <button class="danger" type="button" [mat-dialog-close]="true">{{ data.confirm }}</button>
    </mat-dialog-actions>
  `,
})
export class ConfirmDialogComponent {
  public readonly data = inject<IConfirm>(MAT_DIALOG_DATA);
}

/**
 * Opens the dialog and answers `true` only when the destructive button was pressed — closing it
 * any other way (backdrop, Escape, Cancel) answers `false`, so a call site is one `if` and no
 * undefined case.
 */
export function confirm(dialog: MatDialog, data: IConfirm): Observable<boolean> {
  return dialog
    .open<ConfirmDialogComponent, IConfirm, boolean>(ConfirmDialogComponent, {
      data,
      width: '340px',
    })
    .afterClosed()
    .pipe(map((answer) => answer === true));
}
