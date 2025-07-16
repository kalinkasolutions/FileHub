import { Component, OnInit } from '@angular/core';
import { DirectoryList } from '@components/directorylist/directorylist.component';
import { GlobalHeader } from '@components/header/header.component';

@Component({
  standalone: true,
  selector: 'file-browser',
  templateUrl: './filebrowser.component.html',
  styleUrl: './filebrowser.component.scss',
  imports: [GlobalHeader, DirectoryList]
})
export class FilebrowserComponent {

}
