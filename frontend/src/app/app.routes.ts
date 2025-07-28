import { Routes } from '@angular/router';
import { AdminComponent } from '@components/admin/admin.component';
import { FilebrowserComponent } from '@components/filebrowser/filebrowser.component';
import { NotFoundComponent } from '@components/notfound/notfound.component';
import { PublicShare as PublicShareComponent } from '@components/publicshare/publicshare.component';
import { environment } from '@env/environment';

const devOnlyRoutes: Routes = [];

if (!environment.production) {
    devOnlyRoutes.push({
        path: 'og/share/:id',
        redirectTo: 'share/:id',
        pathMatch: 'full'
    });
}

export const routes: Routes = [
    ...devOnlyRoutes,
    { path: "", component: FilebrowserComponent, data: { showPathSegments: true } },
    { path: "admin", component: AdminComponent },
    { path: "share/:id", component: PublicShareComponent, data: { showHeader: false } },
    { path: '**', component: NotFoundComponent, data: { showHeader: false } }
];
