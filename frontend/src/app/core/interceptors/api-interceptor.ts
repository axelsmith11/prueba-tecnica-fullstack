import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Notification } from '../services/notification';

/**
 * Agrega el header UserCode y muestra los errores de la API:
 * 409 = regla de negocio ({ code, description }), cualquier otro = error inesperado.
 */
export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  const notification = inject(Notification);
  const request = req.clone({ setHeaders: { UserCode: environment.userCode } });

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 409) {
        notification.show('warning', error.error?.description ?? 'La operación no es válida.');
      } else {
        notification.show('error', 'El servicio no está disponible o respondió con un error. Intenta nuevamente.');
      }
      return throwError(() => error);
    }),
  );
};
