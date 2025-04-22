import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { DirectoryList } from '@components/directorylist/directorylist.component';
import { GlobalHeader } from '@components/header/header.component';
import { Observable, of } from 'rxjs';


@Component({
  standalone: true,
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  imports: [CommonModule, DirectoryList, GlobalHeader]
})
export class AppComponent implements OnInit {

  public files$: Observable<{ name: string; isDir: boolean; size: number; }[]> = of([]);

  constructor(private http: HttpClient) { }

  ngOnInit(): void {
    this.files$ = this.http.get<{ name: string, isDir: boolean, size: number }[]>("http://localhost:4122/admin/files")
  }

}
