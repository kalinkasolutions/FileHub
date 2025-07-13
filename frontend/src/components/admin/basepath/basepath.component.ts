import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IBasePathModel } from '@models/IBasePath';
import { NotificationLevel } from '@models/INotifcation';
import { BasePathService } from '@services/basepath.service';
import { NotificationService } from '@services/notification.service';

@Component({
  standalone: true,
  selector: 'app-base-path',
  templateUrl: './basepath.component.html',
  styleUrl: './basepath.component.scss',
  imports: [CommonModule, FormsModule]
})
export class BasePathComponent implements OnInit {
  public basePaths: IBasePathModel[] = [];
  public insertPath: string = "";

  constructor(private adminService: BasePathService, private notificationService: NotificationService) { }

  public ngOnInit(): void {
    this.adminService.getBasePaths().subscribe(basePaths => {
      this.basePaths = basePaths.map(x => ({ ...x, updatePath: x.path, edit: false }));
    });
  }

  public edit(model: IBasePathModel) {
    model.edit = true;
  }

  public insert() {
    if (!this.insertPath) {
      return;
    }

    this.adminService.insert({
      path: this.insertPath,
      id: 0,
      createdAt: ''
    }).subscribe(insertedPath => {
      this.insertPath = "";
      this.basePaths.push({ ...insertedPath, updatePath: insertedPath.path, edit: false });
      this.notificationService.notify({
        title: "Path successfully created",
        message: `The path ${insertedPath.path} was added to the shared paths.`,
        level: NotificationLevel.success,
      });
    });
  }

  public deleteEntry(basePath: IBasePathModel) {
    this.adminService.delete(basePath).subscribe(deletedBasePath => {
      this.basePaths = this.basePaths.filter(x => x.id !== deletedBasePath.id)
      this.notificationService.notify({
        title: "Path successfully deleted",
        message: `The path ${deletedBasePath.path} was deleted from the shared paths.`,
        level: NotificationLevel.success,
      });
    })
  }

  public update(basePath: IBasePathModel) {
    this.adminService.update({
      path: basePath.updatePath,
      id: basePath.id,
      createdAt: basePath.createdAt
    }).subscribe(updatedPath => {
      this.basePaths = this.basePaths.map(x => {
        if (x.id === updatedPath.id) {
          return { ...updatedPath, edit: false, updatePath: x.path };
        }
        return x;
      })
      this.notificationService.notify({
        title: "Path successfully updated",
        message: `The path ${updatedPath.path} was updated.`,
        level: NotificationLevel.success,
      });
    });
  }

  public close(basePath: IBasePathModel) {
    basePath.updatePath = basePath.path;
    basePath.edit = false;
  }

}
