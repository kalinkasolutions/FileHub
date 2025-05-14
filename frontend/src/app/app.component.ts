import { Component, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { Notification } from '@components/notification/notification.component';


@Component({
  standalone: true,
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [RouterModule, Notification],
})
export class AppComponent { }
