import { Injectable, signal } from '@angular/core';

export type NotificationType = 'error' | 'warning' | 'success';

export interface NotificationMessage {
  type: NotificationType;
  text: string;
}

/** Aviso global que se muestra en la parte superior de la aplicación. */
@Injectable({ providedIn: 'root' })
export class Notification {
  readonly message = signal<NotificationMessage | null>(null);

  show(type: NotificationType, text: string): void {
    this.message.set({ type, text });
  }

  clear(): void {
    this.message.set(null);
  }
}
