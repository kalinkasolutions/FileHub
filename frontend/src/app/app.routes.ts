import { Routes } from '@angular/router';
import { AdminComponent } from '@components/admin/admin.component';
import { FilebrowserComponent } from '@components/filebrowser/filebrowser.component';

export const routes: Routes = [
    { path: "", component: FilebrowserComponent },
    { path: "admin", component: AdminComponent }

];
