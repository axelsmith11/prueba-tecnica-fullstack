import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { Routes } from '@angular/router';
import { SpanishPaginatorIntl } from '../../core/i18n/spanish-paginator-intl';

/**
 * Rutas perezosas del módulo. Los proveedores de Material viven aquí (no en app.config)
 * para que paginador y campos de formulario no entren en el bundle inicial.
 */
export const bonusesRoutes: Routes = [
  {
    path: '',
    providers: [
      { provide: MatPaginatorIntl, useClass: SpanishPaginatorIntl },
      { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline', subscriptSizing: 'dynamic' } },
    ],
    loadComponent: () => import('./bonuses').then((m) => m.Bonuses),
  },
];
