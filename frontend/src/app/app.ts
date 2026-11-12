import { afterRenderEffect, Component, effect, ElementRef, inject, viewChild } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Notification } from './core/services/notification';

const SUCCESS_AUTO_DISMISS_MS = 4000;

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly notification = inject(Notification);
  private readonly banner = viewChild<ElementRef<HTMLElement>>('banner');

  constructor() {
    // Reabre el aviso como popover para que quede por encima de los diálogos (capa superior).
    afterRenderEffect(() => {
      this.notification.message();
      const element = this.banner()?.nativeElement;
      if (!element) return;
      if (element.matches(':popover-open')) element.hidePopover();
      element.showPopover();
    });

    // Los éxitos se cierran solos; un mensaje nuevo cancela el temporizador del anterior.
    effect((onCleanup) => {
      const message = this.notification.message();
      if (message?.type !== 'success') return;
      const timer = setTimeout(() => this.notification.clear(), SUCCESS_AUTO_DISMISS_MS);
      onCleanup(() => clearTimeout(timer));
    });
  }
}
