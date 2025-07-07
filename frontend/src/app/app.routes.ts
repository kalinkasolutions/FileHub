import { Routes } from '@angular/router';
import { AdminComponent } from '@components/admin/admin.component';
import { FilebrowserComponent } from '@components/filebrowser/filebrowser.component';
import { NotFoundComponent } from '@components/notfound/notfound.component';
import { PublicShare as PublicShareComponent } from '@components/publicshare/publicshare.component';

export const routes: Routes = [
    { path: "", component: FilebrowserComponent },
    { path: "admin", component: AdminComponent },
    { path: "share/:id", component: PublicShareComponent },
    { path: '**', component: NotFoundComponent }
];
