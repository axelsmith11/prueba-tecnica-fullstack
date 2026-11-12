import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Notification } from './core/services/notification';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly notification = inject(Notification);
}
