import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { Notification } from '@components/notification/notification.component';
import { LoadingService } from '@services/loading.service';

@Component({
  standalone: true,
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [RouterOutlet, Notification, AsyncPipe],
})
export class AppComponent {
  private readonly loadingService = inject(LoadingService);

  public readonly isLoading = this.loadingService.loading$;
}
