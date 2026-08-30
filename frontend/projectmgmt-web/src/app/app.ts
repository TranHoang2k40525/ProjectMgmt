import { Component } from '@angular/core';
import { AppShellComponent } from './layouts/app-shell/app-shell';

@Component({
  selector: 'app-root',
  imports: [AppShellComponent],
  template: '<app-shell />',
  styleUrl: './app.scss'
})
export class App {}
