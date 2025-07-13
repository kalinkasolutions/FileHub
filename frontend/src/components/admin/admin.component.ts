import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BasePathComponent } from './basepath/basepath.component';
import { AdminShareComponent } from './share/adminshare.component';
import { GlobalHeader } from "@components/header/header.component";

@Component({
    standalone: true,
    selector: 'app-component',
    templateUrl: './admin.component.html',
    imports: [CommonModule, FormsModule, BasePathComponent, AdminShareComponent, GlobalHeader]
})
export class AdminComponent { }
